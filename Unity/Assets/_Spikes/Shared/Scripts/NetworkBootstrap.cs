using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkRangers.Spikes.Shared
{
    /// <summary>
    /// Host/Join buttons and latency simulation. Sits next to NetworkManager + UnityTransport.
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

        NetworkManager Net => NetworkManager.Singleton;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !Net.IsListening) return;
            if (kb.f1Key.wasPressedThisFrame) ApplyPreset(0);
            if (kb.f2Key.wasPressedThisFrame) ApplyPreset(1);
            if (kb.f3Key.wasPressedThisFrame) ApplyPreset(2);
        }

        void OnGUI()
        {
            if (Net.IsListening) return;

            GUILayout.BeginArea(new Rect(10, 10, 220, 200), GUI.skin.box);
            address = GUILayout.TextField(address);
            if (GUILayout.Button("Host")) StartNetwork(true);
            if (GUILayout.Button("Join")) StartNetwork(false);
            GUILayout.Label($"Sim on start: {PresetLabel} (F1/F2/F3 in game)");
            GUILayout.EndArea();
        }

        void StartNetwork(bool host)
        {
            Net.GetComponent<UnityTransport>().SetConnectionData(address, port, host ? "0.0.0.0" : null);
            if (host ? Net.StartHost() : Net.StartClient())
                ApplyPreset(presetIndex);
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
