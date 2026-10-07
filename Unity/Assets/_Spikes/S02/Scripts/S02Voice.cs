using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ParkRangers.Spikes.Shared;
using Unity.Netcode;
using Unity.Services.Vivox;
using Unity.Services.Vivox.AudioTaps;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ParkRangers.Spikes.S02
{
    /// <summary>
    /// Vivox voice, copied from S05Voice. One group channel, one participant tap per remote player, Vivox output muted.
    /// Change from S05: the tap lives under this object (alive for the whole session) and only follows the
    /// player's head, so the radio keeps playing when the body is disabled or gone. Taps are re-created after
    /// any scene load (Vivox taps can go silent after one).
    /// Radios mirror the tap's stream clip (Mirror); two taps per participant share one buffer in Vivox.
    /// Keys: M mic mute, O Vivox output (diagnosis), J 440 Hz test tone, K hide remote bodies locally,
    /// L load + unload an empty scene (tap re-create test). Command line -s02tone starts the tone;
    /// S02RadioSystem's -s02cmd file drives the same actions.
    /// </summary>
    public class S02Voice : MonoBehaviour
    {
        [SerializeField] float voiceMaxDistance = 25f;
        [SerializeField] string emptyScene = "S02_Empty";

        static bool vivoxInitialized;

        readonly Dictionary<string, (ulong clientId, GameObject tap, Transform body)> taps = new();
        string channel;
        string state = "voice: waiting for connection";
        bool started;
        float nextPoll;
        public bool toneOn = Environment.GetCommandLineArgs().Contains("-s02tone");
        bool toneSent;
        float nextTone;
        bool bodiesHidden;
        int tapsCreated;

        NetworkManager Net => NetworkManager.Singleton;

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (taps.Count == 0) return;
            Debug.Log($"[S02] scene {scene.name} loaded, re-creating {taps.Count} taps");
            foreach (var key in taps.Keys.ToList()) RemoveTap(key);
            nextPoll = 0f;
        }

        /// <summary>The remote player's proximity voice source, or null.</summary>
        public AudioSource VoiceOf(ulong clientId)
        {
            foreach (var t in taps.Values)
                if (t.clientId == clientId && t.tap != null) return t.tap.GetComponent<AudioSource>();
            return null;
        }

        void Update()
        {
            if (Net == null) return;
            if (!started && Net.IsConnectedClient) _ = StartVoice();
            if (started && !Net.IsListening) _ = StopVoice();

            var kb = Keyboard.current;
            if (kb != null && vivoxInitialized && VivoxService.Instance.IsLoggedIn)
            {
                if (kb.jKey.wasPressedThisFrame) toneOn = !toneOn;
                if (kb.mKey.wasPressedThisFrame)
                {
                    if (VivoxService.Instance.IsInputDeviceMuted) VivoxService.Instance.UnmuteInputDevice();
                    else VivoxService.Instance.MuteInputDevice();
                }
                if (kb.oKey.wasPressedThisFrame)
                {
                    if (VivoxService.Instance.IsOutputDeviceMuted) VivoxService.Instance.UnmuteOutputDevice();
                    else VivoxService.Instance.MuteOutputDevice();
                }
                if (kb.kKey.wasPressedThisFrame) HideRemoteBodies(!bodiesHidden);
                if (kb.lKey.wasPressedThisFrame) _ = ReloadTest();
            }

            if (Time.unscaledTime >= nextPoll)
            {
                nextPoll = Time.unscaledTime + 0.5f;
                MoveListenerToLocalPlayer();
                SyncTaps();
            }
            if (toneOn != toneSent && channel != null && (!toneOn || Time.unscaledTime >= nextTone)) SendTone();
        }

        // Tap follows the head while the body is there; otherwise it stays put and keeps playing.
        void LateUpdate()
        {
            foreach (var t in taps.Values)
                if (t.tap != null && t.body != null && t.body.gameObject.activeInHierarchy)
                    t.tap.transform.position = t.body.position + Vector3.up * 1.6f;
        }

        async Task StartVoice()
        {
            started = true;
            try
            {
                state = "voice: signing in";
                await NetworkBootstrap.EnsureSignedIn();
                if (!vivoxInitialized)
                {
                    await VivoxService.Instance.InitializeAsync();
                    vivoxInitialized = true;
                }
                state = "voice: logging in";
                await VivoxService.Instance.LoginAsync(new LoginOptions { DisplayName = $"c{Net.LocalClientId}" });
                channel = NetworkBootstrap.Session?.Id ?? "s02lan";
                state = "voice: joining channel";
                await VivoxService.Instance.JoinGroupChannelAsync(channel, ChatCapability.AudioOnly);
                // Only the Unity taps make sound (S05: Vivox's own output plays untapped participants and injected audio).
                VivoxService.Instance.MuteOutputDevice();
                state = $"voice: in {channel}";
            }
            catch (Exception e)
            {
                state = $"voice failed: {e.Message}";
                Debug.LogException(e);
            }
        }

        async Task StopVoice()
        {
            started = false;
            foreach (var key in taps.Keys.ToList()) RemoveTap(key);
            channel = null;
            state = "voice: waiting for connection";
            if (vivoxInitialized && VivoxService.Instance.IsLoggedIn) await VivoxService.Instance.LogoutAsync();
        }

        void SyncTaps()
        {
            if (channel == null || !VivoxService.Instance.ActiveChannels.TryGetValue(channel, out var participants)) return;

            foreach (var p in participants)
            {
                if (p.IsSelf || (taps.TryGetValue(p.PlayerId, out var t) && t.tap != null)) continue;
                if (!ulong.TryParse(p.DisplayName.TrimStart('c'), out var clientId)) continue;
                var player = Net.SpawnManager.SpawnedObjectsList.FirstOrDefault(o => o.IsPlayerObject && o.OwnerClientId == clientId);
                if (player == null) continue; // not spawned yet, next poll

                RemoveTap(p.PlayerId);
                var go = new GameObject($"VoiceProximity {p.DisplayName}");
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                go.transform.position = player.transform.position + Vector3.up * 1.6f;
                // Same steps as VivoxParticipant.CreateVivoxParticipantTap.
                var tap = go.AddComponent<VivoxParticipantTap>();
                tap.AutoAcquireChannel = false;
                tap.ChannelName = p.ChannelName;
                tap.ParticipantName = p.PlayerId;
                tap.SilenceInChannelAudioMix = true;
                go.SetActive(true);
                var src = go.GetComponent<AudioSource>();
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 1f;
                src.maxDistance = voiceMaxDistance;

                taps[p.PlayerId] = (clientId, go, player.transform);
                tapsCreated++;
                Debug.Log($"[S02] tap for {p.DisplayName} ({p.PlayerId})");
            }

            foreach (var key in taps.Keys.Where(k => participants.All(p => p.PlayerId != k)).ToList())
                RemoveTap(key);
        }

        void RemoveTap(string playerId)
        {
            if (taps.Remove(playerId, out var t) && t.tap != null) Destroy(t.tap);
        }

        /// <summary>
        /// Plays the proximity source's stream clip just ahead of its read position. The tap writes ~40 ms ahead
        /// of the proximity read pointer and silences what has been played, so a copy behind the pointer plays
        /// 10 ms gaps (chop). Looping: the tap clip is a ~3 s ring buffer.
        /// </summary>
        public static void Mirror(AudioSource prox, AudioSource src)
        {
            const int lead = 256, maxLead = 1024; // samples, well inside the tap's ~1900-sample write lead
            if (src.clip != prox.clip) { src.Stop(); src.clip = prox.clip; src.loop = true; }
            if (prox.clip == null || !prox.isPlaying)
            {
                if (src.isPlaying) src.Pause();
                return;
            }
            var len = prox.clip.samples;
            var drift = ((src.timeSamples - prox.timeSamples) % len + len) % len;
            if (drift > len / 2) drift -= len;
            if (src.isPlaying && drift >= 0 && drift <= maxLead) return;
            src.timeSamples = (prox.timeSamples + lead) % len; // ponytail: hard resync (click); only at start in tests
            if (!src.isPlaying) src.Play();
        }

        // Scene listener sits on the static Main Camera; 3D voice needs it on the local player's head camera.
        void MoveListenerToLocalPlayer()
        {
            var local = Net.SpawnManager?.GetLocalPlayerObject();
            var cam = local != null ? local.GetComponentInChildren<Camera>() : null;
            if (cam == null || cam.GetComponent<AudioListener>() != null) return;
            foreach (var l in FindObjectsByType<AudioListener>()) Destroy(l);
            cam.gameObject.AddComponent<AudioListener>();
        }

        // Stand-in for a body that is despawned, ragdolled or hidden. Local only.
        public void HideRemoteBodies(bool hide)
        {
            bodiesHidden = hide;
            foreach (var o in Net.SpawnManager.SpawnedObjectsList.Where(o => o.IsPlayerObject && !o.IsLocalPlayer).ToList())
                o.gameObject.SetActive(!hide);
            Debug.Log($"[S02] remote bodies {(hide ? "hidden" : "shown")}");
        }

        public async Task ReloadTest()
        {
            await SceneManager.LoadSceneAsync(emptyScene, LoadSceneMode.Additive);
            await SceneManager.UnloadSceneAsync(emptyScene);
        }

        // Injection plays the 10 s file once and throws until a second participant is in the channel; loop on a timer.
        void SendTone()
        {
            CancelInvoke(nameof(ToneEnded));
            try
            {
                if (toneOn) VivoxService.Instance.StartAudioInjection(TonePath());
                else VivoxService.Instance.StopAudioInjection();
                toneSent = toneOn;
                nextTone = Time.unscaledTime + (toneOn ? 9.9f : 0f); // a stop or re-start applies at once
                if (toneOn) Invoke(nameof(ToneEnded), 9.9f);
            }
            catch (Exception e)
            {
                nextTone = Time.unscaledTime + 1f;
                Debug.Log($"[S02] tone: {e.Message}");
            }
        }

        void ToneEnded() => toneSent = false;

        // 10 s of 440 Hz, 16-bit mono 48 kHz WAV, written once per launch.
        static string TonePath()
        {
            var path = System.IO.Path.Combine(Application.temporaryCachePath, "s02_tone440.wav");
            if (System.IO.File.Exists(path)) return path;
            const int rate = 48000, samples = rate * 10;
            using var w = new System.IO.BinaryWriter(System.IO.File.Create(path));
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + samples * 2); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(samples * 2);
            for (var i = 0; i < samples; i++) w.Write((short)(Mathf.Sin(2f * Mathf.PI * 440f * i / rate) * 16000));
            return path;
        }

        public string OverlayText()
        {
            if (!vivoxInitialized) return state + "\n";
            var vivox = VivoxService.Instance;
            var lines = $"{state}  taps made {tapsCreated}\nmic (M): {(vivox.IsLoggedIn && vivox.IsInputDeviceMuted ? "muted" : "live")}  tone (J): {(toneOn ? "on" : "off")}"
                        + $"  vivox out (O): {(vivox.IsLoggedIn && vivox.IsOutputDeviceMuted ? "muted" : "ON")}  hide bodies (K): {bodiesHidden}  scene reload (L)";
            if (channel != null && vivox.ActiveChannels.TryGetValue(channel, out var participants))
                foreach (var p in participants)
                {
                    var src = taps.TryGetValue(p.PlayerId, out var t) && t.tap != null ? t.tap.GetComponent<AudioSource>() : null;
                    lines += $"\n  {p.DisplayName}{(p.IsSelf ? " (me)" : "")} {(p.SpeechDetected ? "SPEAKING" : "")}{(src != null ? $" tap {(src.isPlaying ? "playing" : "idle")}" : "")}";
                }
            return lines + "\n";
        }
    }
}
