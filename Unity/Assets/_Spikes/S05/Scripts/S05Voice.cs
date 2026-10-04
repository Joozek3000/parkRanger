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

namespace ParkRangers.Spikes.S05
{
    /// <summary>
    /// Vivox voice once NGO is connected. Each remote player gets one participant tap on a 3D proximity
    /// AudioSource on their body, plus a 2D radio AudioSource with filters at the listener that plays the
    /// tap's own stream clip in sync. Two taps per participant don't work: Vivox hands both the same native
    /// tap id, and whichever reads first drains the buffer, so the other starves and pauses.
    /// Group channel, not positional: Vivox positional culls out-of-range speakers, which would kill the radio.
    /// Vivox DisplayName = "c" + NGO client ID, so a participant maps to its player object without extra sync.
    /// Vivox's own output is muted; only the taps make sound.
    /// T toggles the radio, M mutes the mic, O unmutes Vivox's own output (diagnosis only), J injects a 440 Hz test tone,
    /// F5-F8 place you 2/10/20/30 m from the other player.
    /// Once per second: distances, listener, tap parents, AudioSource settings and RMS to the overlay and Player.log ([S05diag]).
    /// Command line: -s05tone injects the tone; -s05sweep (listener side) mutes the mic, turns the radio off and steps through
    /// 2/10/20/30 m, then 2 m with Unity audio muted and Vivox output unmuted (anything audible then is Vivox's own playback).
    /// </summary>
    public class S05Voice : MonoBehaviour
    {
        [SerializeField] float voiceMaxDistance = 25f;
        [SerializeField] float radioHighPass = 400f;
        [SerializeField] float radioLowPass = 3000f;
        [SerializeField] float radioDistortion = 0.5f;

        static readonly float[] DebugDistances = { 2f, 10f, 20f, 30f };
        static bool vivoxInitialized;

        readonly Dictionary<string, (GameObject proximity, GameObject radio)> taps = new();
        string channel;
        string state = "voice: waiting for connection";
        bool started;
        bool radioOn = true;
        float nextPoll;
        float nextReport;
        string diag = "";
        bool toneOn = HasArg("-s05tone");
        bool toneSent;
        float nextTone;
        bool sweep = HasArg("-s05sweep");
        int sweepStep = -1;
        float sweepNext;
        readonly float[] listenerBuf = new float[2048];

        NetworkManager Net => NetworkManager.Singleton;

        void Start()
        {
            var overlay = FindAnyObjectByType<NetworkStatsOverlay>();
            if (overlay != null) overlay.ExtraText = OverlayText;
        }

        void Update()
        {
            if (Net == null) return;
            if (!started && Net.IsConnectedClient) _ = StartVoice();
            if (started && !Net.IsListening) _ = StopVoice();

            var kb = Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame) SetRadio(!radioOn);
            if (kb != null && kb.jKey.wasPressedThisFrame) toneOn = !toneOn;
            for (var i = 0; i < DebugDistances.Length; i++)
                if (kb != null && kb[Key.F5 + i].wasPressedThisFrame) PlaceAt(DebugDistances[i]);
            foreach (var t in taps.Values) if (t.proximity != null && t.radio != null && t.radio.activeSelf) Mirror(t.proximity, t.radio);
            if (kb != null && kb.mKey.wasPressedThisFrame && VivoxService.Instance.IsLoggedIn)
            {
                if (VivoxService.Instance.IsInputDeviceMuted) VivoxService.Instance.UnmuteInputDevice();
                else VivoxService.Instance.MuteInputDevice();
            }
            if (kb != null && kb.oKey.wasPressedThisFrame && VivoxService.Instance.IsLoggedIn)
            {
                if (VivoxService.Instance.IsOutputDeviceMuted) VivoxService.Instance.UnmuteOutputDevice();
                else VivoxService.Instance.MuteOutputDevice();
            }

            if (Time.unscaledTime >= nextPoll)
            {
                nextPoll = Time.unscaledTime + 0.5f;
                MoveListenerToLocalPlayer();
                SyncTaps();
            }

            if (toneOn != toneSent && channel != null && Time.unscaledTime >= nextTone) SendTone();
            if (sweep && taps.Count > 0 && Time.unscaledTime >= sweepNext) SweepStep();
            if (Time.unscaledTime >= nextReport)
            {
                nextReport = Time.unscaledTime + 1f;
                diag = Report();
                if (started) Debug.Log("[S05diag] " + diag.Replace("\n", " | "));
            }
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
                channel = NetworkBootstrap.Session?.Id ?? "s05lan";
                state = "voice: joining channel";
                await VivoxService.Instance.JoinGroupChannelAsync(channel, ChatCapability.AudioOnly);
                if (sweep) VivoxService.Instance.MuteInputDevice();
                // All voice plays through the Unity taps. Vivox's own output would play any participant without a tap
                // (no player object yet, or SilenceInChannelAudioMix not honoured) and injected audio on the sender
                // at full volume, everywhere. Muting it leaves the taps running (measured).
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
            foreach (var key in taps.Keys.ToList()) RemoveTaps(key);
            state = "voice: waiting for connection";
            if (VivoxService.Instance.IsLoggedIn) await VivoxService.Instance.LogoutAsync();
        }

        void SyncTaps()
        {
            if (channel == null || !VivoxService.Instance.ActiveChannels.TryGetValue(channel, out var participants)) return;

            foreach (var p in participants)
            {
                if (p.IsSelf || (taps.TryGetValue(p.PlayerId, out var t) && t.proximity != null)) continue;
                if (!ulong.TryParse(p.DisplayName.TrimStart('c'), out var clientId)) continue;
                // GetPlayerNetworkObject only knows other players on the server.
                var player = Net.SpawnManager.SpawnedObjectsList.FirstOrDefault(o => o.IsPlayerObject && o.OwnerClientId == clientId);
                if (player == null) continue; // not spawned yet, next poll

                RemoveTaps(p.PlayerId);
                var proximity = CreateTap(p, "VoiceProximity", player.transform, new Vector3(0f, 1.6f, 0f));
                var src = proximity.GetComponent<AudioSource>();
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 1f;
                src.maxDistance = voiceMaxDistance;
                proximity.AddComponent<S05RmsMeter>();

                var radio = new GameObject($"VoiceRadio {p.DisplayName}", typeof(AudioSource));
                radio.transform.SetParent(transform, false);
                var radioSrc = radio.GetComponent<AudioSource>();
                radioSrc.spatialBlend = 0f;
                radioSrc.loop = true;
                radio.AddComponent<AudioHighPassFilter>().cutoffFrequency = radioHighPass;
                radio.AddComponent<AudioLowPassFilter>().cutoffFrequency = radioLowPass;
                radio.AddComponent<AudioDistortionFilter>().distortionLevel = radioDistortion;
                radio.AddComponent<S05RmsMeter>();
                radio.SetActive(radioOn);

                taps[p.PlayerId] = (proximity, radio);
                Debug.Log($"[S05] taps for {p.DisplayName} ({p.PlayerId}) on player {clientId}");
            }

            foreach (var key in taps.Keys.Where(k => participants.All(p => p.PlayerId != k)).ToList())
                RemoveTaps(key);
        }

        // Same steps as VivoxParticipant.CreateVivoxParticipantTap, which only allows one tap per participant.
        GameObject CreateTap(VivoxParticipant p, string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject($"{name} {p.DisplayName}");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tap = go.AddComponent<VivoxParticipantTap>();
            tap.AutoAcquireChannel = false;
            tap.ChannelName = p.ChannelName;
            tap.ParticipantName = p.PlayerId;
            tap.SilenceInChannelAudioMix = true;
            go.SetActive(true);
            return go;
        }

        // Radio plays the proximity tap's stream clip at the same read position. The tap writes ~40 ms ahead
        // of the proximity read pointer and silences what it has played, so the radio must stay on that pointer.
        static void Mirror(GameObject proximity, GameObject radio)
        {
            var prox = proximity.GetComponent<AudioSource>();
            var src = radio.GetComponent<AudioSource>();
            if (src.clip != prox.clip) { src.Stop(); src.clip = prox.clip; }
            if (prox.clip == null || !prox.isPlaying)
            {
                if (src.isPlaying) src.Pause();
                return;
            }
            if (!src.isPlaying) { src.timeSamples = prox.timeSamples; src.Play(); return; }
            var len = prox.clip.samples;
            var drift = ((src.timeSamples - prox.timeSamples) % len + len) % len;
            if (drift > len / 2) drift -= len;
            if (Mathf.Abs(drift) > 1024) src.timeSamples = prox.timeSamples; // ponytail: hard resync (audible click), fine at this drift
        }

        void RemoveTaps(string playerId)
        {
            if (!taps.Remove(playerId, out var t)) return;
            if (t.proximity != null) Destroy(t.proximity);
            if (t.radio != null) Destroy(t.radio);
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

        void SetRadio(bool on)
        {
            radioOn = on;
            foreach (var t in taps.Values) if (t.radio != null) t.radio.SetActive(radioOn);
        }

        // Puts the local player d metres from the other player along X, towards the middle of the ground.
        void PlaceAt(float d)
        {
            var local = Net.SpawnManager?.GetLocalPlayerObject();
            var remote = Net.SpawnManager?.SpawnedObjectsList.FirstOrDefault(o => o.IsPlayerObject && !o.IsLocalPlayer);
            if (local == null || remote == null) return;
            var target = remote.transform.position;
            var cc = local.GetComponent<CharacterController>();
            cc.enabled = false;
            local.transform.position = target + (target.x > 0f ? Vector3.left : Vector3.right) * d;
            local.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(target - local.transform.position, Vector3.up));
            cc.enabled = true;
            Debug.Log($"[S05diag] placed at {d} m from c{remote.OwnerClientId}");
        }

        void SweepStep()
        {
            sweepStep++;
            sweepNext = Time.unscaledTime + 8f;
            SetRadio(false);
            if (sweepStep < DebugDistances.Length)
            {
                PlaceAt(DebugDistances[sweepStep]);
                Debug.Log($"[S05sweep] {DebugDistances[sweepStep]} m, radio off");
            }
            else if (sweepStep == DebugDistances.Length)
            {
                PlaceAt(2f);
                AudioListener.volume = 0f;
                VivoxService.Instance.UnmuteOutputDevice();
                Debug.Log("[S05sweep] 2 m, Unity audio muted, Vivox output unmuted (anything audible is Vivox's own playback)");
            }
            else
            {
                AudioListener.volume = 1f;
                VivoxService.Instance.MuteOutputDevice();
                sweep = false;
                Debug.Log("[S05sweep] done");
            }
        }

        string Report()
        {
            var listener = FindAnyObjectByType<AudioListener>();
            var lp = listener != null ? listener.transform.position : Vector3.zero;
            AudioListener.GetOutputData(listenerBuf, 0);
            var text = $"listener {(listener != null ? listener.name : "NONE")} {lp:F1}  out RMS {Rms(listenerBuf):F3}  vol {AudioListener.volume}";
            if (VivoxService.Instance.IsLoggedIn)
                text += $"  vivox out vol {VivoxService.Instance.OutputDeviceVolume} muted {VivoxService.Instance.IsOutputDeviceMuted} inject {VivoxService.Instance.IsInjectingAudio}";
            foreach (var t in taps.Values)
            {
                if (t.proximity == null) continue;
                var src = t.proximity.GetComponent<AudioSource>();
                var tap = t.proximity.GetComponent<VivoxParticipantTap>();
                var parent = t.proximity.transform.parent;
                var owner = parent != null ? parent.GetComponentInParent<NetworkObject>() : null;
                var dist = Vector3.Distance(lp, t.proximity.transform.position);
                // Unity's linear rolloff: full volume at minDistance, 0 at maxDistance.
                var gain = Mathf.Clamp01(1f - (dist - src.minDistance) / (src.maxDistance - src.minDistance)) * src.volume;
                var proxSrc = t.proximity.GetComponent<S05RmsMeter>().Take();
                var radio = t.radio != null && t.radio.activeInHierarchy ? t.radio.GetComponent<S05RmsMeter>().Take() : 0f;
                text += $"\n  {t.proximity.name}: dist {dist:F1} m  parent {(parent != null ? parent.name : "NONE")}"
                        + $" (owner c{(owner != null ? owner.OwnerClientId.ToString() : "?")}{(owner != null && owner.IsLocalPlayer ? " LOCAL!" : "")}) {t.proximity.transform.position:F1}"
                        + $"\n    blend {src.spatialBlend} {src.rolloffMode} min {src.minDistance} max {src.maxDistance} vol {src.volume}"
                        + $"  tap {tap.TapId} running {tap.IsRunning} silenceMix {tap.SilenceInChannelAudioMix}"
                        + $"\n    prox src RMS {proxSrc:F3} x gain {gain:F2} = {proxSrc * gain:F3} at listener   radio RMS {radio:F3}";
            }
            return text;
        }

        // Injection plays the 10 s file once and refuses until the channel's audio is connected (needs a second
        // participant). IsInjectingAudio flickers, so loop on a timer instead: toneSent goes false again after 10 s.
        void SendTone()
        {
            CancelInvoke(nameof(ToneEnded));
            try
            {
                if (toneOn) VivoxService.Instance.StartAudioInjection(TonePath());
                else VivoxService.Instance.StopAudioInjection();
                toneSent = toneOn;
                nextTone = Time.unscaledTime + 9.9f;
                if (toneOn) Invoke(nameof(ToneEnded), 9.9f);
            }
            catch (Exception e)
            {
                nextTone = Time.unscaledTime + 1f;
                Debug.Log($"[S05diag] tone: {e.Message}");
            }
        }

        void ToneEnded() => toneSent = false;

        static float Rms(float[] buf)
        {
            double s = 0;
            foreach (var v in buf) s += v * v;
            return Mathf.Sqrt((float)(s / buf.Length));
        }

        static bool HasArg(string arg) => Environment.GetCommandLineArgs().Contains(arg);

        // 10 s of 440 Hz, 16-bit mono 48 kHz WAV, written once per launch.
        static string TonePath()
        {
            var path = System.IO.Path.Combine(Application.temporaryCachePath, "s05_tone440.wav");
            if (System.IO.File.Exists(path)) return path;
            const int rate = 48000, samples = rate * 10;
            using var w = new System.IO.BinaryWriter(System.IO.File.Create(path));
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + samples * 2); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(samples * 2);
            for (var i = 0; i < samples; i++) w.Write((short)(Mathf.Sin(2f * Mathf.PI * 440f * i / rate) * 16000));
            return path;
        }

        string OverlayText()
        {
            var lines = $"{state}\nradio (T): {(radioOn ? "on" : "off")}  mic (M): {(VivoxService.Instance.IsLoggedIn && VivoxService.Instance.IsInputDeviceMuted ? "muted" : "live")}  tone (J): {(toneOn ? "on" : "off")}  vivox out (O): {(VivoxService.Instance.IsLoggedIn && VivoxService.Instance.IsOutputDeviceMuted ? "muted" : "ON")}  place F5-F8: 2/10/20/30 m\n{diag}";
            if (channel != null && VivoxService.Instance.ActiveChannels.TryGetValue(channel, out var participants))
                foreach (var p in participants)
                {
                    var tapInfo = taps.TryGetValue(p.PlayerId, out var t)
                        ? $" prox={Running(t.proximity)} radio={Running(t.radio)}"
                        : "";
                    lines += $"\n  {p.DisplayName}{(p.IsSelf ? " (me)" : "")} {(p.SpeechDetected ? "SPEAKING" : "")}{tapInfo}";
                }
            return lines;
        }

        static string Running(GameObject go) =>
            go == null ? "-" : !go.activeInHierarchy ? "off" : go.GetComponent<AudioSource>().isPlaying ? "playing" : "idle";
    }
}
