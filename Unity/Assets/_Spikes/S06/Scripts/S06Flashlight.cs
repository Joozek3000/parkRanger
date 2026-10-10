using ParkRangers.Spikes.Shared;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace ParkRangers.Spikes.S06
{
    /// <summary>
    /// Handheld LED torch, F toggles it. Held low-right, aimed at a point ahead of the eyes, turning with a short lag
    /// and a small walk bob. On/off and head pitch are owner-written, so remote beams aim where their owner looks.
    /// Only the owner's light casts shadows; only remote torches show a beam cone.
    /// </summary>
    public class S06Flashlight : NetworkBehaviour
    {
        const float LagSeconds = 0.1f;
        const float PitchStep = 1f;
        const float AimDistance = 15f;
        const float BobHeight = 0.012f;
        const float BobRadiansPerMetre = 2.4f;
        const float ConeLength = 18f;
        const float ConeHalfAngle = 12f;
        static readonly Vector3 Hand = new(0.3f, -0.35f, 0.45f);
        // Rendering layer for the owner's own (shadow-only) body, which must not shadow its own torch.
        const uint OwnBodyLayer = 1u << 7;
        static Mesh coneMesh;

        [SerializeField] Texture cookie;
        [SerializeField] Material coneMaterial;

        readonly NetworkVariable<bool> on = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<float> pitch = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        Transform head;
        Light beam;
        GameObject cone;
        Vector3 lastPos;
        float bobPhase;

        public bool On => on.Value;

        public override void OnNetworkSpawn()
        {
            head = GetComponent<FirstPersonController>().Head;
            beam = new GameObject($"Flashlight {OwnerClientId}").AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.useColorTemperature = true;
            beam.colorTemperature = 6000f;
            beam.color = Color.white;
            // Inverse-square: ~130 lights trunks well to ~30 m and faintly at 40 m without burning out the ground at your feet.
            beam.intensity = 130f;
            beam.range = 60f;
            // The cookie shapes the beam (hotspot, ring, spill); the spot cone only clips it.
            beam.spotAngle = 60f;
            beam.innerSpotAngle = 55f;
            beam.cookie = cookie;
            if (IsOwner)
            {
                beam.shadows = LightShadows.Soft;
                var data = beam.GetUniversalAdditionalLightData();
                data.additionalLightsShadowResolutionTier = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh;
                foreach (var r in GetComponentsInChildren<Renderer>()) r.renderingLayerMask = OwnBodyLayer;
                data.customShadowLayers = true;
                data.shadowRenderingLayers = ~OwnBodyLayer;
            }
            else
            {
                cone = new GameObject("Cone", typeof(MeshFilter), typeof(MeshRenderer));
                cone.transform.SetParent(beam.transform, false);
                cone.GetComponent<MeshFilter>().sharedMesh = coneMesh ??= ConeMesh();
                var r = cone.GetComponent<MeshRenderer>();
                r.sharedMaterial = coneMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            lastPos = transform.position;
            Follow(1f);
        }

        public override void OnNetworkDespawn()
        {
            if (beam != null) Destroy(beam.gameObject);
        }

        void Update()
        {
            if (beam == null) return;
            if (IsOwner)
            {
                if (Keyboard.current.fKey.wasPressedThisFrame) on.Value = !on.Value;
                var p = Mathf.DeltaAngle(0f, head.localEulerAngles.x);
                if (Mathf.Abs(p - pitch.Value) > PitchStep) pitch.Value = p;
            }
            else
            {
                head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(pitch.Value, 0f, 0f), Smoothing());
            }
            beam.enabled = on.Value;
            if (cone != null) cone.SetActive(on.Value);
        }

        // After the controller has turned the head this frame.
        void LateUpdate()
        {
            if (beam != null) Follow(Smoothing());
        }

        void Follow(float t)
        {
            var step = new Vector2(transform.position.x - lastPos.x, transform.position.z - lastPos.z).magnitude;
            lastPos = transform.position;
            bobPhase += step * BobRadiansPerMetre;
            var bob = BobHeight * (Mathf.Sin(2f * bobPhase) * head.up + Mathf.Sin(bobPhase) * head.right);
            var hand = head.TransformPoint(Hand) + bob;
            beam.transform.position = hand;
            var aim = Quaternion.LookRotation(head.position + AimDistance * head.forward - hand, head.up);
            beam.transform.rotation = Quaternion.Slerp(beam.transform.rotation, aim, t);
        }

        static float Smoothing() => 1f - Mathf.Exp(-Time.deltaTime / LagSeconds);

        // Open cone along +z: uv.y = 0 at the lens, 1 at the far end.
        static Mesh ConeMesh()
        {
            const int Sides = 24;
            var end = ConeLength * Mathf.Tan(ConeHalfAngle * Mathf.Deg2Rad);
            var vertices = new Vector3[Sides * 2];
            var normals = new Vector3[Sides * 2];
            var uv = new Vector2[Sides * 2];
            var triangles = new int[Sides * 6];
            for (var i = 0; i < Sides; i++)
            {
                var a = 2f * Mathf.PI * i / Sides;
                var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                vertices[i] = 0.03f * radial;
                vertices[i + Sides] = end * radial + ConeLength * Vector3.forward;
                normals[i] = normals[i + Sides] = (radial - end / ConeLength * Vector3.forward).normalized;
                uv[i] = new Vector2((float)i / Sides, 0f);
                uv[i + Sides] = new Vector2((float)i / Sides, 1f);
                var next = (i + 1) % Sides;
                triangles[i * 6] = i;
                triangles[i * 6 + 1] = i + Sides;
                triangles[i * 6 + 2] = next;
                triangles[i * 6 + 3] = next;
                triangles[i * 6 + 4] = i + Sides;
                triangles[i * 6 + 5] = next + Sides;
            }
            var mesh = new Mesh { name = "BeamCone", vertices = vertices, normals = normals, uv = uv, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
