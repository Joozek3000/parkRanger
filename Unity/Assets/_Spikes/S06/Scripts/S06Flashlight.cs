using ParkRangers.Spikes.Shared;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkRangers.Spikes.S06
{
    /// <summary>
    /// F toggles a head-mounted spot light. On/off is owner-written, so others see the beam.
    /// Only the owner's light casts shadows. Head pitch isn't networked, so remote beams point level.
    /// </summary>
    public class S06Flashlight : NetworkBehaviour
    {
        readonly NetworkVariable<bool> on = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        Light beam;

        public bool On => on.Value;

        public override void OnNetworkSpawn()
        {
            beam = new GameObject("Flashlight").AddComponent<Light>();
            // Right of the nose and in front of the body, so the owner's shadows show.
            beam.transform.SetParent(GetComponent<FirstPersonController>().Head, false);
            beam.transform.localPosition = new Vector3(0.3f, -0.25f, 0.45f);
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.9f, 0.75f);
            beam.intensity = 20f;
            beam.range = 35f;
            beam.spotAngle = 55f;
            beam.innerSpotAngle = 30f;
            beam.shadows = IsOwner ? LightShadows.Soft : LightShadows.None;
        }

        void Update()
        {
            if (beam == null) return;
            if (IsOwner && Keyboard.current.fKey.wasPressedThisFrame) on.Value = !on.Value;
            beam.enabled = on.Value;
        }
    }
}
