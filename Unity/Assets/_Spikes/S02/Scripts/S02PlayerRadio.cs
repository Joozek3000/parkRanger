using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkRangers.Spikes.S02
{
    /// <summary>
    /// The player's held radio: push-to-talk (V) and battery, both owner-written NetworkVariables.
    /// Every machine reads them to decide locally which radios play this player's voice. B recharges.
    /// </summary>
    public class S02PlayerRadio : NetworkBehaviour
    {
        [SerializeField] S02Radio radio;
        [SerializeField] float drainPerSecond = 0.01f;

        public readonly NetworkVariable<bool> Transmitting = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public readonly NetworkVariable<float> Battery = new(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public S02Radio Radio => radio;

        void Update()
        {
            if (!IsOwner || !IsSpawned) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.bKey.wasPressedThisFrame) Battery.Value = 1f;
            var talk = kb.vKey.isPressed && Battery.Value > 0f;
            if (Transmitting.Value != talk) Transmitting.Value = talk;
            // ponytail: battery syncs every tick while talking; send coarse steps if bandwidth ever matters
            if (talk) Battery.Value = Mathf.Max(0f, Battery.Value - drainPerSecond * Time.deltaTime);
        }
    }
}
