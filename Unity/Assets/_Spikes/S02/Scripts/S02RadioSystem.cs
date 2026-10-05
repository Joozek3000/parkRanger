using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ParkRangers.Spikes.Shared;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkRangers.Spikes.S02
{
    /// <summary>
    /// Radio routing, signal model and all tuning (edit live in the inspector during Play). Runs on every machine:
    /// each remote player who holds PTT is played on every other powered radio that isn't transmitting (half duplex),
    /// at the signal between the two radios. Nothing here is synced; PTT and battery come from S02PlayerRadio.
    /// Signal = max(direct, via repeater). Direct: 1 up to cleanRange, linear to 0 at cutRange, x blockedFactor
    /// if terrain blocks line of sight, 0 if either radio is in a dead zone. Via repeater: min of both radios'
    /// links to the repeater (same curve with the repeater's ranges, 0 without line of sight).
    /// Keys: V talk, B recharge, R repeater (this machine), 1-6 teleport spots, F5-F8 place at placeDistances
    /// from the other player.
    /// Unattended tests: -s02cmd PATH polls that file, runs its lines (see Command) and deletes it.
    /// [S02edge] lines log wall-clock ms when a remote player's PTT flag, tap stream or radio output switches.
    /// </summary>
    public class S02RadioSystem : MonoBehaviour
    {
        [Header("Range (m)")]
        public float cleanRange = 120f;
        public float cutRange = 220f;
        [Range(0f, 1f)] public float blockedFactor = 0.5f;
        public bool repeaterOn;
        public float repeaterCleanRange = 260f;
        public float repeaterCutRange = 360f;

        [Header("Signal to sound")]
        [Range(0f, 1f)] public float weakVoiceGain = 0.5f;
        [Range(0f, 1f)] public float staticFloor = 0.03f;
        [Range(0f, 1f)] public float staticMax = 0.4f;
        [Range(0f, 1f)] public float dropoutBelow = 0.35f;
        public float dropoutsPerSecond = 4f;
        public Vector2 dropoutLength = new(0.05f, 0.3f);
        public float pttTail = 0.3f;

        [Header("Filter chain")]
        public float highPass = 300f;
        public float lowPass = 3400f;
        [Range(0f, 1f)] public float distortion = 0.4f;
        public float compThreshold = 0.1f;
        public float compRatio = 6f;
        public float compMakeup = 3f;
        public float radioVolume = 1f;
        public float radioMaxDistance = 12f;

        [Header("World")]
        public Collider terrain;
        public Transform repeater;
        public Collider[] deadZones;
        public Transform anomaly;
        public float anomalyRange = 25f;
        [Range(0f, 1f)] public float anomalyStaticMax = 0.4f;
        public Transform npc;
        public float overheardRange = 8f;
        [Tooltip("Radio output RMS a talker must reach to count as overheard")] public float overheardRms = 0.01f;
        public float edgeRms = 0.02f;
        public Transform[] spots;
        public float[] placeDistances = { 2f, 60f, 170f, 250f };

        readonly Dictionary<ulong, float> lastTx = new();
        readonly HashSet<ulong> overheard = new();
        readonly Dictionary<string, bool> edges = new();
        static readonly string CmdFile = ArgAfter("-s02cmd");
        float nextCmd;
        S02Voice voice;
        S02Radio[] radios = { };
        S02PlayerRadio[] users = { };
        float nextScan;
        float npcFlashUntil;
        string npcLine = "";
        Texture2D plot;
        float nextPlot;
        float nextDiag;

        NetworkManager Net => NetworkManager.Singleton;

        void Start()
        {
            voice = FindAnyObjectByType<S02Voice>();
            var overlay = FindAnyObjectByType<NetworkStatsOverlay>();
            if (overlay != null) overlay.ExtraText = () => voice.OverlayText() + OverlayText();
            plot = new Texture2D(300, 60) { filterMode = FilterMode.Point };
        }

        public (float direct, bool blocked, bool dead, float viaRepeater, float total) Link(Vector3 a, Vector3 b)
        {
            var dead = deadZones.Any(z => z.bounds.Contains(a) || z.bounds.Contains(b));
            var blocked = Blocked(a, b);
            var direct = dead ? 0f : Curve(Vector3.Distance(a, b), cleanRange, cutRange) * (blocked ? blockedFactor : 1f);
            var rep = repeaterOn && repeater != null ? Mathf.Min(RepeaterLink(a), RepeaterLink(b)) : 0f;
            return (direct, blocked, dead, rep, Mathf.Max(direct, rep));
        }

        float RepeaterLink(Vector3 p) => Blocked(p, repeater.position) ? 0f : Curve(Vector3.Distance(p, repeater.position), repeaterCleanRange, repeaterCutRange);
        bool Blocked(Vector3 a, Vector3 b) => terrain != null && terrain.Raycast(new Ray(a, b - a), out _, Vector3.Distance(a, b));
        static float Curve(float d, float clean, float cut) => Mathf.Clamp01((cut - d) / (cut - clean));

        void Update()
        {
            if (Net == null || !Net.IsListening) return;
            Keys();
            PollCommands();

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.5f;
                radios = FindObjectsByType<S02Radio>();
                users = FindObjectsByType<S02PlayerRadio>(FindObjectsInactive.Include); // hidden bodies still talk
            }

            foreach (var r in radios)
                if (r != null)
                    r.AnomalyStatic = anomaly == null ? 0f : anomalyStaticMax * Mathf.Clamp01(1f - Vector3.Distance(r.transform.position, anomaly.position) / anomalyRange);

            foreach (var u in users)
            {
                if (u == null || !u.IsSpawned || u.IsOwner) continue; // own voice has no tap
                var id = u.OwnerClientId;
                Edge($"c{id} ptt", u.Transmitting.Value);
                var tap = voice.VoiceOf(id);
                if (tap != null) Edge($"c{id} tap", S02Radio.ClipRms(tap) > edgeRms);
                var mine = LocalRadio();
                if (mine != null) Edge($"c{id} radio", mine.VoiceRms(id) > edgeRms);
                if (u.Transmitting.Value) lastTx[id] = Time.time;
                if (Time.time - lastTx.GetValueOrDefault(id, -99f) > pttTail)
                {
                    overheard.Remove(id);
                    continue;
                }
                if (tap == null) continue;
                foreach (var r in radios)
                    if (r != null && r != u.Radio && r.Powered && !r.Transmitting)
                        r.Receive(id, tap, Link(u.Radio.transform.position, r.transform.position).total);
            }

            CheckOverheard();

            if (Time.unscaledTime >= nextDiag)
            {
                nextDiag = Time.unscaledTime + 1f;
                Debug.Log("[S02diag] " + string.Join("  ||  ", radios.Where(r => r != null).Select(r => r.Diag())));
            }
        }

        void CheckOverheard()
        {
            if (npc == null) return;
            foreach (var r in radios)
            {
                if (r == null) continue;
                var d = Vector3.Distance(r.transform.position, npc.position);
                if (d > overheardRange) continue;
                foreach (var (talker, signal) in r.Heard)
                {
                    // Audible playback, not Vivox's speech flag: PTT + signal + level coming out of the radio.
                    var rms = r.VoiceRms(talker);
                    if (overheard.Contains(talker) || rms < overheardRms) continue;
                    overheard.Add(talker);
                    npcFlashUntil = Time.time + 1.5f;
                    npcLine = $"NPC overheard c{talker} via {r.name} at {d:F1} m (signal {signal:F2}, rms {rms:F3})";
                    Debug.Log($"[S02] {npcLine}");
                }
            }
            npc.GetComponentInChildren<Renderer>().material.color = Time.time < npcFlashUntil ? Color.red : Color.white;
        }

        void Edge(string key, bool on)
        {
            if (edges.GetValueOrDefault(key) == on) return;
            edges[key] = on;
            Debug.Log($"[S02edge] {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()} {key} {(on ? "on" : "off")}");
        }

        static string ArgAfter(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        void PollCommands()
        {
            if (CmdFile == null || Time.unscaledTime < nextCmd) return;
            nextCmd = Time.unscaledTime + 0.1f;
            if (!File.Exists(CmdFile)) return;
            var lines = File.ReadAllLines(CmdFile);
            File.Delete(CmdFile);
            foreach (var line in lines)
            {
                var a = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (a.Length == 0) continue;
                Debug.Log($"[S02cmd] {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()} {line}");
                try { Command(a); }
                catch (Exception e) { Debug.LogWarning($"[S02cmd] failed: {e.Message}"); }
            }
        }

        // ptt 0|1, tone 0|1, mic 0|1, battery F, repeater 0|1, place M, spot N, tp X Z, npc X Z, hide 0|1, reload
        void Command(string[] a)
        {
            var on = a.Length > 1 && a[1] == "1";
            float F(int i) => float.Parse(a[i], System.Globalization.CultureInfo.InvariantCulture);
            switch (a[0])
            {
                case "ptt": S02PlayerRadio.AutoTalk = on; break;
                case "tone": voice.toneOn = on; break;
                case "mic": if (on) Unity.Services.Vivox.VivoxService.Instance.UnmuteInputDevice(); else Unity.Services.Vivox.VivoxService.Instance.MuteInputDevice(); break;
                case "battery": LocalRadio().User.Battery.Value = F(1); break;
                case "repeater": repeaterOn = on; break;
                case "place": PlaceAt(F(1)); break;
                case "spot": Teleport(spots[(int)F(1) - 1].position, spots[(int)F(1) - 1].forward); break;
                case "tp": Teleport(new Vector3(F(1), 0f, F(2)), Vector3.forward); break;
                case "npc": npc.position = new Vector3(F(1), npc.position.y, F(2)); break;
                case "hide": voice.HideRemoteBodies(on); break;
                case "reload": _ = voice.ReloadTest(); break;
                default: Debug.LogWarning($"[S02cmd] unknown: {a[0]}"); break;
            }
        }

        void Keys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.rKey.wasPressedThisFrame) repeaterOn = !repeaterOn;
            for (var i = 0; i < spots.Length && i < 6; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) Teleport(spots[i].position, spots[i].forward);
            for (var i = 0; i < placeDistances.Length && i < 4; i++)
                if (kb[Key.F5 + i].wasPressedThisFrame) PlaceAt(placeDistances[i]);
        }

        // d metres from the other player along X, towards the open side of the map.
        void PlaceAt(float d)
        {
            var remote = Net.SpawnManager.SpawnedObjectsList.FirstOrDefault(o => o.IsPlayerObject && !o.IsLocalPlayer);
            if (remote == null) return;
            var target = remote.transform.position;
            var dir = target.x < 100f ? Vector3.right : Vector3.left;
            Teleport(target + dir * d, -dir);
            Debug.Log($"[S02] placed at {d} m from c{remote.OwnerClientId}");
        }

        void Teleport(Vector3 pos, Vector3 forward)
        {
            var local = Net.SpawnManager.GetLocalPlayerObject();
            if (local == null) return;
            if (terrain != null && terrain.Raycast(new Ray(new Vector3(pos.x, 500f, pos.z), Vector3.down), out var hit, 1000f)) pos = hit.point;
            var cc = local.GetComponent<CharacterController>();
            cc.enabled = false;
            local.transform.SetPositionAndRotation(pos + Vector3.up * 0.1f, Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, Vector3.up)));
            cc.enabled = true;
        }

        S02Radio LocalRadio()
        {
            var local = Net.SpawnManager?.GetLocalPlayerObject();
            return local != null ? local.GetComponentInChildren<S02Radio>() : null;
        }

        string OverlayText()
        {
            var me = LocalRadio();
            var user = me != null ? me.User : null;
            var text = $"radio: talk (V) {(user != null && user.Transmitting.Value ? "<color=red>TX</color>" : "-")}  battery (B) {(user != null ? user.Battery.Value * 100f : 0f):F0}%"
                       + $"  repeater (R) {(repeaterOn ? "ON" : "off")}  spots 1-6  place F5-F8: {string.Join("/", placeDistances)} m\n"
                       + $"curve: clean {cleanRange} / cut {cutRange} m, blocked x{blockedFactor}, repeater {repeaterCleanRange}/{repeaterCutRange} m"
                       + $"  anomaly static {(me != null ? me.AnomalyStatic : 0f):F2}\n";
            if (me == null) return text;
            foreach (var r in radios)
            {
                if (r == null || r == me) continue;
                var l = Link(me.transform.position, r.transform.position);
                var d = Vector3.Distance(me.transform.position, r.transform.position);
                var heard = string.Join(" ", r.Heard.Select(h => $"c{h.talker}@{h.signal:F2}"));
                text += $"  to {r.name}: {d:F0} m  direct {l.direct:F2}{(l.blocked ? " BLOCKED" : "")}{(l.dead ? " DEADZONE" : "")}  repeater {l.viaRepeater:F2}  = <b>{l.total:F2}</b>  {heard}\n";
            }
            return text + npcLine + "\n";
        }

        void OnGUI()
        {
            var me = Net != null && Net.IsListening ? LocalRadio() : null;
            if (me == null) return;
            if (Time.unscaledTime >= nextPlot)
            {
                nextPlot = Time.unscaledTime + 0.25f;
                DrawPlot(me);
            }
            var rect = new Rect(Screen.width - 620, Screen.height - 150, 600, 120);
            GUI.DrawTexture(rect, plot);
            GUI.Label(new Rect(rect.x, rect.yMax, 600, 20), "signal vs distance, 0-300 m: white direct, cyan via repeater; red = other radios, yellow = you to repeater");
        }

        void DrawPlot(S02Radio me)
        {
            var px = new Color32[plot.width * plot.height];
            for (var i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 160);
            void Dot(int x, float v, Color32 c) { var y = Mathf.Clamp(Mathf.RoundToInt(v * (plot.height - 1)), 0, plot.height - 1); px[y * plot.width + x] = c; }
            void Bar(float m, Color32 c) { var x = Mathf.RoundToInt(m); if (x < 0 || x >= plot.width) return; for (var y = 0; y < plot.height; y++) px[y * plot.width + x] = c; }
            foreach (var r in radios)
                if (r != null && r != me) Bar(Vector3.Distance(me.transform.position, r.transform.position), new Color32(255, 60, 60, 255));
            if (repeater != null) Bar(Vector3.Distance(me.transform.position, repeater.position), new Color32(255, 220, 0, 255));
            for (var x = 0; x < plot.width; x++)
            {
                Dot(x, Curve(x, repeaterCleanRange, repeaterCutRange), new Color32(0, 220, 255, 255));
                Dot(x, Curve(x, cleanRange, cutRange), Color.white);
            }
            plot.SetPixels32(px);
            plot.Apply();
        }
    }
}
