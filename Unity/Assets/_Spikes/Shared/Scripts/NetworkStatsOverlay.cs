using System;
using Unity.Netcode;
using UnityEngine;

namespace ParkRangers.Spikes.Shared
{
    /// <summary>
    /// Debug overlay: role, client ID, RTT, sim preset, received payload bytes/s.
    /// Spikes add their own lines through ExtraText.
    /// </summary>
    public class NetworkStatsOverlay : MonoBehaviour
    {
        public Func<string> ExtraText;

        NetworkBootstrap bootstrap;
        NetworkTransport subscribed;
        int bytesThisWindow;
        float windowStart;
        float kbPerSecond;

        NetworkManager Net => NetworkManager.Singleton;

        void Awake() => bootstrap = GetComponent<NetworkBootstrap>();

        void Update()
        {
            var transport = Net.NetworkConfig.NetworkTransport;
            if (Net.IsListening && subscribed != transport)
            {
                transport.OnTransportEvent += OnTransportEvent;
                subscribed = transport;
            }

            if (Time.unscaledTime - windowStart >= 1f)
            {
                kbPerSecond = bytesThisWindow / 1024f / (Time.unscaledTime - windowStart);
                bytesThisWindow = 0;
                windowStart = Time.unscaledTime;
            }
        }

        void OnDestroy()
        {
            if (subscribed != null) subscribed.OnTransportEvent -= OnTransportEvent;
        }

        // ponytail: counts NGO payload only (no UTP/UDP headers, ~+30 B per packet); Multiplayer Tools if exact numbers matter
        void OnTransportEvent(NetworkEvent type, ulong clientId, ArraySegment<byte> payload, float time)
        {
            if (type == NetworkEvent.Data) bytesThisWindow += payload.Count;
        }

        void OnGUI()
        {
            if (!Net.IsListening) return;

            var role = Net.IsHost ? "Host" : Net.IsServer ? "Server" : "Client";
            var text = $"{role}  id {Net.LocalClientId}  sim {bootstrap?.PresetLabel}\n";
            var transport = Net.NetworkConfig.NetworkTransport;
            if (Net.IsServer)
            {
                foreach (var id in Net.ConnectedClientsIds)
                    if (id != NetworkManager.ServerClientId)
                        text += $"RTT client {id}: {transport.GetCurrentRtt(id)} ms\n";
            }
            else
            {
                text += $"RTT: {transport.GetCurrentRtt(NetworkManager.ServerClientId)} ms\n";
            }
            text += $"In: {kbPerSecond:F1} kB/s\n";
            if (ExtraText != null) text += ExtraText();

            GUI.Label(new Rect(10, 10, 400, 200), text);
        }
    }
}
