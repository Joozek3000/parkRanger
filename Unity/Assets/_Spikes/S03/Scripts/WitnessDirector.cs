using System.Collections.Generic;
using System.Diagnostics;
using ParkRangers.Spikes.Shared;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using Debug = UnityEngine.Debug;

namespace ParkRangers.Spikes.S03
{
    /// <summary>
    /// In-scene. Host: bakes the NavMesh, spawns tourists and anomalies at runtime, runs the
    /// staggered witness check (distance → view cone → head/centre/feet linecasts), counts Weirdness.
    /// Every peer: day/night lighting and the overlay line.
    /// </summary>
    public class WitnessDirector : NetworkBehaviour
    {
        static readonly ProfilerMarker CheckMarker = new("S03.WitnessCheck");

        [Header("Spawning")]
        [SerializeField] Tourist touristPrefab;
        [SerializeField] Anomaly anomalyPrefab;
        [SerializeField] int touristCount = 8;
        [SerializeField] int anomalyCount = 1;
        [SerializeField] Transform[] anomalySpawns;
        [SerializeField] Transform waypointRoot;
        [SerializeField] Transform diner;
        [SerializeField] Transform entrance;
        [SerializeField] NavMeshSurface navMesh;

        [Header("Witness check")]
        [SerializeField] int checkEveryNFrames = 6;
        [SerializeField] float halfAngle = 60f;
        [SerializeField] float dayRange = 40f;
        [SerializeField] float nightRange = 15f;
        [Tooltip("At or below this distance the 'close' fill time applies.")]
        [SerializeField] float closeDistance = 10f;
        [Tooltip("Seconds to fill the meter: close, at max range.")]
        [SerializeField] Vector2 dayFillTime = new(1f, 3f);
        [SerializeField] Vector2 nightFillTime = new(2f, 4f);
        [SerializeField] LayerMask occluders = 1; // Default: terrain, trees, rocks, ranger capsules
        [SerializeField] bool drawRays = true;

        [Header("Day/night")]
        [SerializeField] Light sun;
        [SerializeField] float dayIntensity = 1.2f;
        [SerializeField] float nightIntensity = 0.06f;
        [SerializeField] Color dayAmbient = new(0.55f, 0.6f, 0.65f);
        [SerializeField] Color nightAmbient = new(0.03f, 0.04f, 0.08f);

        public readonly NetworkVariable<int> Weirdness = new();
        public readonly NetworkVariable<bool> Night = new();

        public float Range => Night.Value ? nightRange : dayRange;
        public float HalfAngle => halfAngle;
        public Transform Diner => diner;
        public Transform Entrance => entrance;

        readonly List<Tourist> tourists = new();
        readonly List<Anomaly> anomalies = new();
        readonly Vector3[] sightPoints = new Vector3[3];
        Transform[] waypoints;
        Material sky;
        int cursor;
        float checkMs;

        public override void OnNetworkSpawn()
        {
            Night.OnValueChanged += (_, night) => ApplyLighting(night);
            ApplyLighting(Night.Value);
            var overlay = FindAnyObjectByType<NetworkStatsOverlay>();
            if (overlay != null) overlay.ExtraText = OverlayText;

            waypoints = new Transform[waypointRoot.childCount];
            for (var i = 0; i < waypoints.Length; i++) waypoints[i] = waypointRoot.GetChild(i);

            if (!IsServer) return;
            navMesh.BuildNavMesh();
            for (var i = 0; i < anomalyCount; i++)
            {
                var at = anomalySpawns[i % anomalySpawns.Length];
                var a = Instantiate(anomalyPrefab, at.position, at.rotation);
                a.NetworkObject.Spawn(true);
                anomalies.Add(a);
            }
            for (var i = 0; i < touristCount; i++)
            {
                var at = RandomWaypoint();
                var t = Instantiate(touristPrefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                t.NetworkObject.Spawn(true);
                tourists.Add(t);
            }
        }

        public Vector3 RandomWaypoint()
        {
            var p = waypoints[Random.Range(0, waypoints.Length)].position + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            return NavMesh.SamplePosition(p, out var hit, 5f, NavMesh.AllAreas) ? hit.position : p;
        }

        public void AddWeirdness() => Weirdness.Value++;

        [Rpc(SendTo.Server)]
        public void ToggleNightRpc() => Night.Value = !Night.Value;

        void Update()
        {
            if (!IsServer || tourists.Count == 0) return;

            var start = Stopwatch.GetTimestamp();
            using (CheckMarker.Auto())
            {
                var perFrame = Mathf.CeilToInt(tourists.Count / (float)checkEveryNFrames);
                for (var k = 0; k < perFrame; k++)
                {
                    var t = tourists[cursor];
                    cursor = (cursor + 1) % tourists.Count;
                    var target = default(Vector3);
                    var rate = t.CanNotice ? BestSightRate(t, out target) : 0f;
                    t.ApplySight(rate, target);
                }
            }
            var ms = (Stopwatch.GetTimestamp() - start) * 1000f / Stopwatch.Frequency;
            checkMs = Mathf.Lerp(checkMs, ms, 0.05f);
        }

        float BestSightRate(Tourist t, out Vector3 target)
        {
            target = default;
            var best = 0f;
            var eye = t.Eye;
            var forward = t.transform.forward;
            var range = Range;
            var fill = Night.Value ? nightFillTime : dayFillTime;

            foreach (var a in anomalies)
            {
                a.GetSightPoints(sightPoints);
                var toCentre = sightPoints[1] - eye;
                var distance = toCentre.magnitude;
                if (distance > range || Vector3.Angle(forward, toCentre) > halfAngle) continue;

                var seen = 0;
                foreach (var p in sightPoints)
                {
                    var blocked = Physics.Linecast(eye, p, occluders, QueryTriggerInteraction.Ignore);
                    if (!blocked) seen++;
                    if (drawRays) Debug.DrawLine(eye, p, blocked ? Color.red : Color.green, Time.deltaTime * checkEveryNFrames);
                }
                if (seen == 0) continue;

                // ponytail: partly visible fills proportionally slower (1 of 3 points = 1/3 speed); a design call, not a law
                var fillTime = Mathf.Lerp(fill.x, fill.y, Mathf.InverseLerp(closeDistance, range, distance));
                var rate = seen / (float)sightPoints.Length / fillTime;
                if (rate > best)
                {
                    best = rate;
                    target = sightPoints[1];
                }
            }
            return best;
        }

        void ApplyLighting(bool night)
        {
            sun.intensity = night ? nightIntensity : dayIntensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = night ? nightAmbient : dayAmbient;
            // Runtime copy so the Editor's skybox asset isn't changed.
            if (sky == null && RenderSettings.skybox != null) RenderSettings.skybox = sky = new Material(RenderSettings.skybox);
            if (sky != null && sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", night ? 0.05f : 1f);
        }

        string OverlayText()
        {
            var text = $"<size=18><b>Weirdness: {Weirdness.Value}</b></size>   {(Night.Value ? "NIGHT" : "DAY")}, notice range {Range:0} m\n";
            if (IsServer)
                text += $"Witness check: {tourists.Count} tourists x {anomalies.Count} anomalies, every {checkEveryNFrames} frames: {checkMs:F3} ms/frame\n";
            return text + "E carry Bigfoot   hold F on a fleeing tourist   N day/night   G debug\n";
        }
    }
}
