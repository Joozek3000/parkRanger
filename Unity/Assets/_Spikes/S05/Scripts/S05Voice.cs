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
    /// Vivox voice once NGO is connected. Each remote player gets two participant taps:
    /// a 3D proximity AudioSource on their body and a 2D radio AudioSource with filters at the listener.
    /// Group channel, not positional: Vivox positional culls out-of-range speakers, which would kill the radio.
    /// Vivox DisplayName = "c" + NGO client ID, so a participant maps to its player object without extra sync.
    /// T toggles the radio, M mutes the mic.
    /// </summary>
    public class S05Voice : MonoBehaviour
    {
        [SerializeField] float voiceMaxDistance = 25f;
        [SerializeField] float radioHighPass = 400f;
        [SerializeField] float radioLowPass = 3000f;
        [SerializeField] float radioDistortion = 0.5f;

        static bool vivoxInitialized;

        readonly Dictionary<string, (GameObject proximity, GameObject radio)> taps = new();
        string channel;
        string state = "voice: waiting for connection";
        bool started;
        bool radioOn = true;
        float nextPoll;

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
            if (kb != null && kb.tKey.wasPressedThisFrame)
            {
                radioOn = !radioOn;
                foreach (var t in taps.Values) if (t.radio != null) t.radio.SetActive(radioOn);
            }
            if (kb != null && kb.mKey.wasPressedThisFrame && VivoxService.Instance.IsLoggedIn)
            {
                if (VivoxService.Instance.IsInputDeviceMuted) VivoxService.Instance.UnmuteInputDevice();
                else VivoxService.Instance.MuteInputDevice();
            }

            if (Time.unscaledTime >= nextPoll)
            {
                nextPoll = Time.unscaledTime + 0.5f;
                MoveListenerToLocalPlayer();
                SyncTaps();
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
                var player = Net.SpawnManager.GetPlayerNetworkObject(clientId);
                if (player == null) continue; // not spawned yet, next poll

                RemoveTaps(p.PlayerId);
                var proximity = CreateTap(p, "VoiceProximity", player.transform, new Vector3(0f, 1.6f, 0f));
                var src = proximity.GetComponent<AudioSource>();
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 1f;
                src.maxDistance = voiceMaxDistance;

                var radio = CreateTap(p, "VoiceRadio", transform, Vector3.zero);
                radio.GetComponent<AudioSource>().spatialBlend = 0f;
                radio.AddComponent<AudioHighPassFilter>().cutoffFrequency = radioHighPass;
                radio.AddComponent<AudioLowPassFilter>().cutoffFrequency = radioLowPass;
                radio.AddComponent<AudioDistortionFilter>().distortionLevel = radioDistortion;
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
            foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) Destroy(l);
            cam.gameObject.AddComponent<AudioListener>();
        }

        string OverlayText()
        {
            var lines = $"{state}\nradio (T): {(radioOn ? "on" : "off")}  mic (M): {(VivoxService.Instance.IsLoggedIn && VivoxService.Instance.IsInputDeviceMuted ? "muted" : "live")}";
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
            go == null ? "-" : go.activeInHierarchy && go.GetComponent<VivoxParticipantTap>().IsRunning ? "ok" : "off";
    }
}
