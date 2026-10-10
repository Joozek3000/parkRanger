using ParkRangers.Spikes.Shared;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkRangers.Spikes.S06
{
    /// <summary>
    /// Owner only: spawns the player at the "Spawn" point, adds position, distances to landmarks,
    /// speed and a stopwatch (T start/stop) to the debug overlay. Stopwatch results go to the Console.
    /// </summary>
    public class MapWalker : NetworkBehaviour
    {
        static readonly string[] Landmarks = { "RangerStation", "Diner", "Cave_Entrance", "RadioMast", "FireTower", "DinerSign" };

        Transform[] landmarks;
        S06Flashlight flashlight;
        Vector3 lastPos;
        float speed;
        float watchStart = -1f;
        Vector3 watchFrom;
        float watchPath;
        string lastResult = "";

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }

            var spawn = GameObject.Find("Spawn").transform;
            var controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetPositionAndRotation(spawn.position + spawn.right * (OwnerClientId * 2f), spawn.rotation);
            controller.enabled = true;

            landmarks = System.Array.ConvertAll(Landmarks, n => GameObject.Find(n).transform);
            lastPos = transform.position;
            flashlight = GetComponent<S06Flashlight>();
            FindAnyObjectByType<NetworkStatsOverlay>().ExtraText = Text;
        }

        void Update()
        {
            var step = Flat(transform.position - lastPos);
            lastPos = transform.position;
            if (Time.deltaTime > 0f) speed = Mathf.Lerp(speed, step / Time.deltaTime, 0.2f);
            if (watchStart >= 0f) watchPath += step;

            if (!Keyboard.current.tKey.wasPressedThisFrame) return;
            if (watchStart < 0f)
            {
                watchStart = Time.time;
                watchFrom = transform.position;
                watchPath = 0f;
                return;
            }
            var time = Time.time - watchStart;
            lastResult = $"{time:F1} s, path {watchPath:F0} m, straight {Flat(transform.position - watchFrom):F0} m";
            Debug.Log($"[S06] Stopwatch {lastResult}, from {watchFrom:F0} to {transform.position:F0}");
            watchStart = -1f;
        }

        string Text()
        {
            var p = transform.position;
            var text = $"\n<b>Map</b>  pos {p.x:F0}, {p.y:F1}, {p.z:F0}   speed {speed:F1} m/s\n";
            for (var i = 0; i < Landmarks.Length; i++)
                text += $"{Landmarks[i]}: {Flat(landmarks[i].position - p):F0} m\n";
            var watch = watchStart >= 0f ? $"<color=yellow>{Time.time - watchStart:F1} s  ({watchPath:F0} m)</color>" : "stopped";
            // What the crosshair is on (terrain trees count as the terrain), to read how far the torch reaches.
            var head = GetComponent<FirstPersonController>().Head;
            var aim = Physics.Raycast(head.position, head.forward, out var hit, 300f, ~0, QueryTriggerInteraction.Ignore) ? $"{hit.distance:F0} m" : "-";
            return text + $"Stopwatch [T]: {watch}\nLast: {lastResult}\nFlashlight [F]: {(flashlight.On ? "on" : "off")}   aim {aim}\n";
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
