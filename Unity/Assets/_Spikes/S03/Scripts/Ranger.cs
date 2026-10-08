using ParkRangers.Spikes.Shared;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkRangers.Spikes.S03
{
    /// <summary>
    /// Owner input: E carry/drop Bigfoot, hold F on a fleeing tourist (Q4 interrupt),
    /// N day/night, G debug view. The ranger's CharacterController blocks witness rays.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class Ranger : NetworkBehaviour
    {
        [SerializeField] float reach = 2.5f;

        /// <summary>This machine's view, for world-space labels.</summary>
        public static Camera LocalCamera { get; private set; }

        WitnessDirector director;
        Tourist holding;

        public override void OnNetworkSpawn()
        {
            director = FindAnyObjectByType<WitnessDirector>();
            if (!IsOwner) return;
            LocalCamera = GetComponentInChildren<Camera>(true);
            // The scene's overview camera (tagged MainCamera) would draw over ours at the same depth.
            if (Camera.main != null && Camera.main != LocalCamera) Camera.main.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!IsOwner) return;
            var kb = Keyboard.current;

            if (kb.fKey.wasPressedThisFrame)
            {
                holding = Nearest<Tourist>(t => t.State.Value == TouristState.Flee);
                if (holding != null) HoldRpc(holding, true);
            }
            if (holding != null && (!kb.fKey.isPressed || holding.State.Value != TouristState.Flee))
            {
                HoldRpc(holding, false);
                holding = null;
            }

            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (kb.eKey.wasPressedThisFrame) ToggleCarry();
            if (kb.nKey.wasPressedThisFrame) director.ToggleNightRpc();
            if (kb.gKey.wasPressedThisFrame) Tourist.ShowDebug = !Tourist.ShowDebug;
        }

        void ToggleCarry()
        {
            var carried = Nearest<Anomaly>(a => a.Carrier.Value == OwnerClientId, float.MaxValue);
            if (carried != null) carried.RequestDrop();
            else Nearest<Anomaly>(a => !a.IsCarried)?.RequestPickup();
        }

        // ponytail: scans every object of the type; fine for 30 tourists on a key press
        T Nearest<T>(System.Func<T, bool> filter, float maxDistance = -1f) where T : Component
        {
            if (maxDistance < 0f) maxDistance = reach;
            T best = null;
            foreach (var c in FindObjectsByType<T>())
            {
                var d = Vector3.Distance(c.transform.position, transform.position);
                if (d <= maxDistance && filter(c))
                {
                    best = c;
                    maxDistance = d;
                }
            }
            return best;
        }

        [Rpc(SendTo.Server)]
        void HoldRpc(NetworkBehaviourReference tourist, bool held)
        {
            if (tourist.TryGet(out Tourist t)) t.SetHeld(OwnerClientId, held);
        }
    }
}
