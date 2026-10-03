using ParkRangers.Spikes.Shared;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkRangers.Spikes.S01
{
    /// <summary>
    /// Owner input for carrying: E picks up / drops, left mouse throws.
    /// </summary>
    [RequireComponent(typeof(FirstPersonController))]
    public class PlayerCarrier : NetworkBehaviour
    {
        [SerializeField] float reach = 3f;

        FirstPersonController controller;

        void Awake() => controller = GetComponent<FirstPersonController>();

        void Update()
        {
            if (!IsOwner || Cursor.lockState != CursorLockMode.Locked) return;

            var held = FindHeld();
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (held != null) held.Release(Vector3.zero);
                else TryPickup();
            }
            else if (held != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                var dir = (controller.Head.forward + Vector3.up * 0.2f).normalized;
                held.Release(dir * held.ThrowSpeed);
            }
        }

        // ponytail: scans all props each frame; fine for 10, keep a reference if prop count grows
        CarryableProp FindHeld()
        {
            foreach (var p in FindObjectsByType<CarryableProp>())
                if (p.Carrier.Value == OwnerClientId) return p;
            return null;
        }

        void TryPickup()
        {
            var head = controller.Head;
            if (Physics.Raycast(head.position, head.forward, out var hit, reach) &&
                hit.collider.TryGetComponent<CarryableProp>(out var prop) && !prop.IsCarried)
                prop.RequestPickup();
        }
    }
}
