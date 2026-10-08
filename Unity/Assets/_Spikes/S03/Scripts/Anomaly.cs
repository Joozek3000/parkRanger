using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace ParkRangers.Spikes.S03
{
    /// <summary>
    /// Bigfoot stand-in. Carried with S01 model C: kinematic while carried, every peer pins it
    /// to its own view of the carrier, host keeps physics otherwise.
    /// Lives on Ignore Raycast so it never blocks a witness ray to itself.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Anomaly : NetworkBehaviour
    {
        public const ulong NoCarrier = ulong.MaxValue;
        static readonly Vector3 HoldOffset = new(0f, 0.3f, 1.4f);

        public readonly NetworkVariable<ulong> Carrier = new(NoCarrier);

        public bool IsCarried => Carrier.Value != NoCarrier;

        Rigidbody body;
        Collider bodyCollider;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            bodyCollider = GetComponent<Collider>();
        }

        /// <summary>Witness ray targets: head, centre, feet.</summary>
        public void GetSightPoints(Vector3[] points)
        {
            var b = bodyCollider.bounds;
            points[0] = new Vector3(b.center.x, b.max.y - 0.15f, b.center.z);
            points[1] = b.center;
            points[2] = new Vector3(b.center.x, b.min.y + 0.2f, b.center.z);
        }

        void LateUpdate()
        {
            if (!IsSpawned || !IsCarried) return;
            var players = NetworkManager.SpawnManager.GetPlayerNetworkObjects(Carrier.Value);
            if (players is not { Count: > 0 }) return;
            var player = players[0].transform;
            transform.SetPositionAndRotation(player.TransformPoint(HoldOffset), player.rotation);
        }

        public void RequestPickup() => PickupRpc();

        public void RequestDrop() => DropRpc(transform.position, transform.rotation);

        [Rpc(SendTo.Server)]
        void PickupRpc(RpcParams rpcParams = default)
        {
            if (IsCarried) return;
            body.isKinematic = true;
            Carrier.Value = rpcParams.Receive.SenderClientId;
        }

        [Rpc(SendTo.Server)]
        void DropRpc(Vector3 position, Quaternion rotation, RpcParams rpcParams = default)
        {
            if (Carrier.Value != rpcParams.Receive.SenderClientId) return;
            Carrier.Value = NoCarrier;
            // Drop where the carrier saw it, not where the host's lagged view had it.
            GetComponent<NetworkTransform>().Teleport(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f), transform.localScale);
            body.isKinematic = false;
        }
    }
}
