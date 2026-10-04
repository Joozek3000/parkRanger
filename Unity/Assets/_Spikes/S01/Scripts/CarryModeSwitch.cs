using ParkRangers.Spikes.Shared;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkRangers.Spikes.S01
{
    /// <summary>
    /// Scene object holding the current carry mode. Host switches with 1/2/3.
    /// A prop keeps the mode it was picked up with until released.
    /// </summary>
    public class CarryModeSwitch : NetworkBehaviour
    {
        public readonly NetworkVariable<CarryMode> Mode = new(CarryMode.A_HostSim);

        public override void OnNetworkSpawn()
        {
            var overlay = FindAnyObjectByType<NetworkStatsOverlay>();
            if (overlay != null) overlay.ExtraText = OverlayText;
        }

        void Update()
        {
            if (!IsServer) return;
            var kb = Keyboard.current;
            if (kb.digit1Key.wasPressedThisFrame) Mode.Value = CarryMode.A_HostSim;
            if (kb.digit2Key.wasPressedThisFrame) Mode.Value = CarryMode.B_Ownership;
            if (kb.digit3Key.wasPressedThisFrame) Mode.Value = CarryMode.C_Kinematic;
        }

        string OverlayText()
        {
            if (!IsSpawned) return "";
            var text = $"Carry mode: {Mode.Value} (host: 1/2/3)\n";
            // Live spawned set, so despawned/destroyed props never show up.
            foreach (var obj in NetworkManager.SpawnManager.SpawnedObjectsList)
                if (obj.TryGetComponent<CarryableProp>(out var p) && (p.IsCarried || p.OwnerClientId != NetworkManager.ServerClientId))
                    text += $"{p.name}: owner {p.OwnerClientId}, carrier {(p.IsCarried ? p.Carrier.Value.ToString() : "-")}, {p.ActiveMode.Value}\n";
            return text;
        }
    }
}
