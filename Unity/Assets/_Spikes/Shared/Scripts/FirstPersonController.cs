using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkRangers.Spikes.Shared
{
    /// <summary>
    /// Owner-authoritative first-person movement. Needs CharacterController and an
    /// owner-authority NetworkTransform on the same object; Head holds the camera.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : NetworkBehaviour
    {
        [SerializeField] Transform head;
        [SerializeField] float walkSpeed = 4f;
        [SerializeField] float sprintSpeed = 7f;
        [SerializeField] float jumpHeight = 1.1f;
        [SerializeField] float gravity = -20f;
        [SerializeField] float mouseSensitivity = 0.1f;

        public Transform Head => head;

        CharacterController controller;
        float pitch;
        float verticalVelocity;

        void Awake() => controller = GetComponent<CharacterController>();

        public override void OnNetworkSpawn()
        {
            var cam = head.GetComponentInChildren<Camera>(true);
            cam.gameObject.SetActive(IsOwner);
            if (IsOwner)
            {
                // Own body would block the camera; keep its shadow, others still see it.
                foreach (var r in GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

                // Spawn points spread out so players don't overlap.
                controller.enabled = false;
                transform.position += new Vector3(OwnerClientId * 2f, 0f, 0f);
                controller.enabled = true;
                LockCursor(true);
            }
        }

        void Update()
        {
            if (!IsOwner) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb.escapeKey.wasPressedThisFrame) LockCursor(false);
            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var look = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0f, look.x, 0f);
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
                head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            var input = new Vector2(
                (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
                (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));
            var speed = kb.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
            var move = (transform.right * input.x + transform.forward * input.y) * speed;

            if (controller.isGrounded)
            {
                verticalVelocity = -2f;
                if (kb.spaceKey.wasPressedThisFrame) verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
            }
            verticalVelocity += gravity * Time.deltaTime;
            move.y = verticalVelocity;

            controller.Move(move * Time.deltaTime);
        }

        static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
