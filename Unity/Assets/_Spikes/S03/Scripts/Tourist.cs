using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

namespace ParkRangers.Spikes.S03
{
    /// <summary>
    /// Host-run tourist: wander → notice → photo → flee to the diner → Weirdness +1.
    /// The witness check itself lives in WitnessDirector (staggered); it feeds ApplySight.
    /// Clients only read the NetworkVariables: body tint, label, cone, sound cues.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Tourist : NetworkBehaviour
    {
        const ulong NoHolder = ulong.MaxValue;
        const float EyeHeight = 1.6f;

        [SerializeField] float wanderSpeed = 1.6f;
        [SerializeField] float fleeSpeed = 4.5f;
        [Tooltip("'!' beat before the camera comes up.")]
        [SerializeField] float noticeTime = 0.6f;
        [SerializeField] float photoWindow = 2f;
        [SerializeField] float decayPerSecond = 0.33f;
        [Tooltip("Notice/photo gives up when the meter falls below this.")]
        [SerializeField] float loseSightBelow = 0.5f;
        [SerializeField] float calmHoldTime = 2f;
        [Tooltip("Server-side slack over the ranger's 2.5 m grab reach, for lag.")]
        [SerializeField] float holdReach = 3.5f;
        [SerializeField] float calmTime = 3f;
        [SerializeField] float immuneAfterCalm = 8f;
        [SerializeField] Renderer body;
        [SerializeField] LineRenderer cone;
        [SerializeField] AudioSource voice;

        public readonly NetworkVariable<TouristState> State = new();
        public readonly NetworkVariable<float> Exposure = new();
        public readonly NetworkVariable<float> CalmProgress = new();

        /// <summary>Local view toggle (G): cones, meter numbers.</summary>
        public static bool ShowDebug = true;

        public Vector3 Eye => transform.position + Vector3.up * EyeHeight;
        public bool CanNotice => State.Value <= TouristState.Photo && Time.time >= immuneUntil;

        static AudioClip beep, shutter;
        static GUIStyle labelStyle;

        WitnessDirector director;
        NavMeshAgent agent;
        MaterialPropertyBlock tint;
        float exposure, stateTime, immuneUntil, lastSight, coneRange;
        Vector3 lookTarget;
        ulong heldBy = NoHolder;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            tint = new MaterialPropertyBlock();
            beep ??= Tone("beep", 880f, 0.2f, false);
            shutter ??= Tone("shutter", 0f, 0.08f, true);
        }

        public override void OnNetworkSpawn()
        {
            director = FindAnyObjectByType<WitnessDirector>();
            agent.enabled = IsServer;
            State.OnValueChanged += OnStateChanged;
            if (IsServer)
            {
                lastSight = Time.time;
                SetState(TouristState.Wander);
            }
        }

        public override void OnNetworkDespawn() => State.OnValueChanged -= OnStateChanged;

        // ---------- server ----------

        /// <summary>Server. rate = meter fill per second from the best visible anomaly, 0 if none.</summary>
        public void ApplySight(float rate, Vector3 anomalyPosition)
        {
            var dt = Time.time - lastSight;
            lastSight = Time.time;
            if (!CanNotice) return;

            if (rate > 0f)
            {
                exposure = Mathf.Min(1f, exposure + rate * dt);
                lookTarget = anomalyPosition;
            }
            else
            {
                exposure = Mathf.Max(0f, exposure - decayPerSecond * dt);
            }
            SyncExposure();

            if (State.Value == TouristState.Wander && exposure >= 1f) SetState(TouristState.Notice);
            else if (State.Value != TouristState.Wander && exposure < loseSightBelow) SetState(TouristState.Wander);
        }

        /// <summary>Server. A ranger starts or stops holding this tourist (Q4 interrupt).</summary>
        public void SetHeld(ulong clientId, bool held)
        {
            if (held && State.Value == TouristState.Flee && heldBy == NoHolder) heldBy = clientId;
            else if (!held && heldBy == clientId) heldBy = NoHolder;
        }

        void Update()
        {
            if (IsServer && IsSpawned) ServerUpdate();
            UpdateVisuals();
        }

        void ServerUpdate()
        {
            stateTime += Time.deltaTime;
            switch (State.Value)
            {
                case TouristState.Wander:
                    if (!agent.pathPending && agent.remainingDistance < 1f) agent.SetDestination(director.RandomWaypoint());
                    break;
                case TouristState.Notice:
                    Face(lookTarget);
                    if (stateTime >= noticeTime) SetState(TouristState.Photo);
                    break;
                case TouristState.Photo:
                    Face(lookTarget);
                    if (stateTime >= photoWindow) SetState(TouristState.Flee);
                    break;
                case TouristState.Flee:
                    UpdateHold();
                    if (heldBy == NoHolder && !agent.pathPending && agent.remainingDistance < 1.5f)
                    {
                        director.AddWeirdness();
                        LeaveAndReturn();
                    }
                    break;
                case TouristState.Calm:
                    if (stateTime >= calmTime)
                    {
                        immuneUntil = Time.time + immuneAfterCalm;
                        SetState(TouristState.Wander);
                    }
                    break;
            }
        }

        void UpdateHold()
        {
            if (heldBy != NoHolder)
            {
                var players = NetworkManager.SpawnManager.GetPlayerNetworkObjects(heldBy);
                var inReach = players is { Count: > 0 } && Vector3.Distance(players[0].transform.position, transform.position) <= holdReach;
                if (!inReach) heldBy = NoHolder;
            }

            var held = heldBy != NoHolder;
            agent.isStopped = held;
            CalmProgress.Value = held ? CalmProgress.Value + Time.deltaTime / calmHoldTime : 0f;
            if (CalmProgress.Value >= 1f) SetState(TouristState.Calm);
        }

        void SetState(TouristState state)
        {
            State.Value = state;
            stateTime = 0f;
            var standing = state is TouristState.Notice or TouristState.Photo or TouristState.Calm;
            agent.isStopped = standing;
            agent.updateRotation = !standing;
            agent.speed = state == TouristState.Flee ? fleeSpeed : wanderSpeed;

            switch (state)
            {
                case TouristState.Wander:
                    agent.SetDestination(director.RandomWaypoint());
                    break;
                case TouristState.Flee:
                    agent.SetDestination(director.Diner.position);
                    break;
                case TouristState.Calm:
                    heldBy = NoHolder;
                    CalmProgress.Value = 0f;
                    exposure = 0f;
                    SyncExposure();
                    break;
            }
        }

        // Photo reached the diner: this tourist leaves town and a fresh one walks in.
        void LeaveAndReturn()
        {
            var entrance = director.Entrance;
            agent.Warp(entrance.position);
            GetComponent<NetworkTransform>().Teleport(entrance.position, entrance.rotation, transform.localScale);
            exposure = 0f;
            SyncExposure();
            SetState(TouristState.Wander);
        }

        void Face(Vector3 target)
        {
            var d = target - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 360f * Time.deltaTime);
        }

        // ponytail: 2% steps cut NetworkVariable traffic; send the raw float if the meter needs to be smoother
        void SyncExposure()
        {
            var q = Mathf.Round(exposure * 50f) / 50f;
            if (!Mathf.Approximately(q, Exposure.Value)) Exposure.Value = q;
        }

        // ---------- every peer ----------

        void OnStateChanged(TouristState previous, TouristState current)
        {
            if (current == TouristState.Notice) voice.PlayOneShot(beep);
            if (previous == TouristState.Photo && current == TouristState.Flee) voice.PlayOneShot(shutter);
        }

        void UpdateVisuals()
        {
            var color = State.Value switch
            {
                TouristState.Notice => new Color(1f, 0.55f, 0f),
                TouristState.Photo => Time.time % 0.3f < 0.15f ? Color.red : Color.white,
                TouristState.Flee => new Color(0.8f, 0f, 0.8f),
                TouristState.Calm => Color.green,
                _ => Color.Lerp(new Color(0.3f, 0.6f, 1f), Color.yellow, Exposure.Value),
            };
            body.GetPropertyBlock(tint);
            tint.SetColor("_BaseColor", color);
            body.SetPropertyBlock(tint);

            cone.enabled = ShowDebug && director != null;
            if (cone.enabled && !Mathf.Approximately(coneRange, director.Range)) BuildCone(director.Range, director.HalfAngle);
        }

        void BuildCone(float range, float halfAngle)
        {
            coneRange = range;
            const int arc = 12;
            cone.positionCount = arc + 2;
            var eye = Vector3.up * EyeHeight;
            cone.SetPosition(0, eye);
            for (var i = 0; i <= arc; i++)
            {
                var angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)arc);
                cone.SetPosition(i + 1, eye + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * range);
            }
        }

        void OnGUI()
        {
            var cam = Ranger.LocalCamera;
            if (cam == null) return;
            var p = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.3f);
            if (p.z <= 0f || p.z > 80f) return;

            var meter = ShowDebug ? $" {Exposure.Value * 100f:0}%" : "";
            var text = State.Value switch
            {
                TouristState.Notice => "<color=orange>!</color>",
                TouristState.Photo => "<color=red>[PHOTO]</color>",
                TouristState.Flee => CalmProgress.Value > 0f ? $"<color=lime>calming {CalmProgress.Value * 100f:0}%</color>" : "<color=magenta>RUN</color>",
                TouristState.Calm => "<color=lime>calm</color>",
                _ => Exposure.Value > 0f ? $"<color=yellow>?{meter}</color>" : "",
            };
            if (text.Length == 0) return;

            labelStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold, richText = true };
            GUI.Label(new Rect(p.x - 80f, Screen.height - p.y - 12f, 160f, 24f), text, labelStyle);
        }

        // Placeholder cues: a sine beep and a noise-burst "shutter".
        static AudioClip Tone(string name, float frequency, float seconds, bool noise)
        {
            const int rate = 44100;
            var data = new float[(int)(rate * seconds)];
            for (var i = 0; i < data.Length; i++)
            {
                var envelope = 1f - i / (float)data.Length;
                data[i] = 0.5f * envelope * (noise ? Random.Range(-1f, 1f) : Mathf.Sin(2f * Mathf.PI * frequency * i / rate));
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
