using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkRangers.Spikes.Shared
{
    /// <summary>
    /// Host/Join buttons and latency simulation. Sits next to NetworkManager + UnityTransport.
    /// LAN: direct IP. Relay: Unity Multiplayer Sessions, join code; the session starts NGO itself.
    /// Latency: UTP's global simulator layer (still built into the driver in the Editor).
    /// Each peer delays its own sends by RTT/2, so two peers give the full RTT.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        static readonly (string label, int rttMs, float lossPercent)[] Presets =
        {
            ("Off", 0, 0f),
            ("100 ms + 1%", 100, 1f),
            ("200 ms + 1%", 200, 1f),
        };

        [SerializeField] string address = "127.0.0.1";
        [SerializeField] ushort port = 7777;
        [SerializeField] int presetIndex = 1;

        public string PresetLabel => Presets[presetIndex].label;
        public static ISession Session { get; private set; }

        NetworkManager Net => NetworkManager.Singleton;
        string status;
        string joinCode = "";
        bool busy;

        void Start() => Net.OnClientStopped += OnClientStopped;

        void OnDestroy()
        {
            if (Net != null) Net.OnClientStopped -= OnClientStopped;
        }

        void OnClientStopped(bool wasHost)
        {
            var reason = Net.DisconnectReason;
            status = string.IsNullOrEmpty(reason) ? (wasHost ? "Host stopped." : "Disconnected (no host, or connection lost).") : $"Disconnected: {reason}";
            Debug.LogWarning($"[Net] {status}");
            Session?.LeaveAsync();
            Session = null;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || Net == null || !Net.IsListening) return;
            if (kb.f1Key.wasPressedThisFrame) ApplyPreset(0);
            if (kb.f2Key.wasPressedThisFrame) ApplyPreset(1);
            if (kb.f3Key.wasPressedThisFrame) ApplyPreset(2);
        }

        void OnGUI()
        {
            if (Net == null) return;
            if (Net.IsListening)
            {
                if (Net.IsClient && !Net.IsConnectedClient) GUI.Label(new Rect(10, 10, 300, 20), "Connecting...");
                if (Session != null) GUI.Label(new Rect(Screen.width - 210, 10, 200, 20), $"Join code: {Session.Code}");
                return;
            }

            GUILayout.BeginArea(new Rect(10, 10, 240, 260), GUI.skin.box);
            GUI.enabled = !busy;
            GUILayout.Label("LAN");
            address = GUILayout.TextField(address);
            if (GUILayout.Button("Host")) StartNetwork(true);
            if (GUILayout.Button("Join")) StartNetwork(false);
            GUILayout.Label("Relay (internet)");
            if (GUILayout.Button("Host Relay")) _ = RunRelay(null);
            joinCode = GUILayout.TextField(joinCode).Trim().ToUpperInvariant();
            if (GUILayout.Button("Join code")) _ = RunRelay(joinCode);
            GUI.enabled = true;
            GUILayout.Label($"Sim on start: {PresetLabel} (F1/F2/F3 in game)");
            if (status != null) GUILayout.Label($"<color=red>{status}</color>");
            GUILayout.EndArea();
        }

        void StartNetwork(bool host)
        {
            Net.GetComponent<UnityTransport>().SetConnectionData(address, port, host ? "0.0.0.0" : null);
            status = null;
            if (host ? Net.StartHost() : Net.StartClient())
                ApplyPreset(presetIndex);
            else
                status = host ? $"Host failed: port {port} in use?" : "Join failed, see Console.";
        }

        /// <summary>Signs in once per launch. Used by Relay and by voice spikes.</summary>
        public static async Task EnsureSignedIn()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                // ponytail: random profile per launch so two builds on one machine get different
                // anonymous player IDs. Costs a new anonymous player per run; fine for spikes.
                var profile = "p" + Guid.NewGuid().ToString("N").Substring(0, 12);
                await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(profile));
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        async Task RunRelay(string code)
        {
            busy = true;
            status = code == null ? "Creating session..." : "Joining...";
            try
            {
                await EnsureSignedIn();
                if (code == null)
                {
                    Session = await MultiplayerService.Instance.CreateSessionAsync(
                        new SessionOptions { MaxPlayers = 4, IsPrivate = true }.WithRelayNetwork());
                    GUIUtility.systemCopyBuffer = Session.Code;
                    Debug.Log($"[Net] Relay session up, join code {Session.Code} (copied to clipboard)");
                }
                else
                {
                    Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                }
                status = null;
                ApplyPreset(0); // real internet latency; F2/F3 add sim on top
            }
            catch (Exception e)
            {
                status = e is SessionException se ? $"{se.Error}: {se.Message}" : e.Message;
                Debug.LogException(e);
            }
            busy = false;
        }

        void ApplyPreset(int index)
        {
            presetIndex = index;
            var (_, rtt, loss) = Presets[index];
            Net.GetComponent<UnityTransport>().GetNetworkDriver().ModifyNetworkSimulatorParameters(new NetworkSimulatorParameter
            {
                SendDelayMS = (uint)(rtt / 2),
                SendPacketLossPercent = loss / 2f,
            });
            Debug.Log($"[NetSim] {PresetLabel}");
        }
    }
}
