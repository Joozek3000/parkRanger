using Unity.Netcode;
using UnityEngine;

namespace ParkRangers.Spikes.S01
{
    /// <summary>
    /// A prop that can be carried with model A, B or C (see CarryMode).
    /// Needs Rigidbody, NetworkTransform (Owner authority, SwitchTransformSpaceWhenParented) and NetworkRigidbody.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarryableProp : NetworkBehaviour
    {
        public const ulong NoCarrier = ulong.MaxValue;
        public static readonly Vector3 HoldOffset = new(0f, 1.3f, 1.2f);

        [SerializeField] float throwSpeed = 8f;
        [SerializeField] float followGain = 15f;
        [SerializeField] float maxFollowSpeed = 20f;

        public readonly NetworkVariable<ulong> Carrier = new(NoCarrier);
        public readonly NetworkVariable<CarryMode> ActiveMode = new();

        public bool IsCarried => Carrier.Value != NoCarrier;
        public float ThrowSpeed => throwSpeed;

        Rigidbody body;
        Collider propCollider;
        CarryModeSwitch modeSwitch;
        float restTime;
        // B: the owner stops following as soon as it releases; Carrier only clears a round trip later.
        bool releasedLocally;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            propCollider = GetComponent<Collider>();
        }

        public override void OnNetworkSpawn()
        {
            modeSwitch = FindAnyObjectByType<CarryModeSwitch>();
            Carrier.OnValueChanged += OnCarrierChanged;
        }

        public override void OnNetworkDespawn() => Carrier.OnValueChanged -= OnCarrierChanged;

        // The carrier's capsule would otherwise bump into what it holds.
        void OnCarrierChanged(ulong previous, ulong current)
        {
            releasedLocally = false;
            if (previous != NoCarrier) IgnorePlayer(previous, false);
            if (current != NoCarrier) IgnorePlayer(current, true);
        }

        void IgnorePlayer(ulong clientId, bool ignore)
        {
            var player = PlayerTransform(clientId);
            if (player != null) Physics.IgnoreCollision(player.GetComponent<CharacterController>(), propCollider, ignore);
        }

        // Not GetPlayerNetworkObject: on clients it errors for other clients' players.
        Transform PlayerTransform(ulong clientId)
        {
            var players = NetworkManager.SpawnManager.GetPlayerNetworkObjects(clientId);
            return players is { Count: > 0 } ? players[0].transform : null;
        }

        void FixedUpdate()
        {
            if (!IsSpawned) return;

            var mode = ActiveMode.Value;
            bool drives = IsCarried && !releasedLocally && (
                (mode == CarryMode.A_HostSim && IsServer) ||
                (mode == CarryMode.B_Ownership && IsOwner));
            if (drives) FollowHold(PlayerTransform(Carrier.Value));

            // B: the last carrier keeps simulating after release, then hands back to the host at rest.
            if (!IsCarried && IsOwner && !IsServer)
            {
                restTime = body.linearVelocity.sqrMagnitude < 0.01f ? restTime + Time.fixedDeltaTime : 0f;
                if (restTime > 0.5f)
                {
                    restTime = float.NegativeInfinity;
                    ReturnOwnershipRpc();
                }
            }
        }

        void FollowHold(Transform player)
        {
            if (player == null) return;
            var target = player.TransformPoint(HoldOffset);
            body.linearVelocity = Vector3.ClampMagnitude((target - body.position) * followGain, maxFollowSpeed);
            body.angularVelocity = Vector3.zero;
            body.MoveRotation(Quaternion.Slerp(body.rotation, player.rotation, 0.2f));
        }

        /// <summary>Called on the carrier's machine.</summary>
        public void RequestPickup() => PickupRpc();

        /// <summary>Called on the carrier's machine. Zero velocity = drop.</summary>
        public void Release(Vector3 velocity)
        {
            if (releasedLocally) return;
            if (ActiveMode.Value == CarryMode.B_Ownership && IsOwner)
            {
                body.linearVelocity = velocity;
                restTime = 0f;
                releasedLocally = true;
            }
            ReleaseRpc(transform.position, transform.rotation, velocity);
        }

        [Rpc(SendTo.Server)]
        void PickupRpc(RpcParams rpcParams = default)
        {
            if (IsCarried) return;
            var sender = rpcParams.Receive.SenderClientId;
            var mode = modeSwitch.Mode.Value;
            ActiveMode.Value = mode;
            Carrier.Value = sender;

            switch (mode)
            {
                case CarryMode.B_Ownership:
                    if (OwnerClientId != sender) NetworkObject.ChangeOwnership(sender);
                    break;
                case CarryMode.C_Kinematic:
                    if (OwnerClientId != NetworkManager.ServerClientId) NetworkObject.RemoveOwnership();
                    body.isKinematic = true;
                    NetworkObject.TrySetParent(PlayerTransform(sender), false);
                    transform.SetLocalPositionAndRotation(HoldOffset, Quaternion.identity);
                    break;
                default:
                    if (OwnerClientId != NetworkManager.ServerClientId) NetworkObject.RemoveOwnership();
                    break;
            }
        }

        [Rpc(SendTo.Server)]
        void ReleaseRpc(Vector3 carrierPosition, Quaternion carrierRotation, Vector3 velocity, RpcParams rpcParams = default)
        {
            if (Carrier.Value != rpcParams.Receive.SenderClientId) return;
            Carrier.Value = NoCarrier;

            switch (ActiveMode.Value)
            {
                case CarryMode.B_Ownership:
                    break; // the owner already applied it
                case CarryMode.C_Kinematic:
                    NetworkObject.TryRemoveParent(true);
                    // Start the flight where the carrier saw it, not where the host's lagged view had it.
                    transform.SetPositionAndRotation(carrierPosition, carrierRotation);
                    body.isKinematic = false;
                    body.linearVelocity = velocity;
                    break;
                default:
                    body.linearVelocity = velocity;
                    break;
            }
        }

        [Rpc(SendTo.Server)]
        void ReturnOwnershipRpc()
        {
            if (!IsCarried) NetworkObject.RemoveOwnership();
        }
    }
}
