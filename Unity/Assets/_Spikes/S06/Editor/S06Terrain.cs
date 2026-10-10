using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ParkRangers.Spikes.S06
{
    /// <summary>
    /// S06 ground as a Unity Terrain (D36). The first build samples the Blender ground mesh and the scatter JSON
    /// into S06_Terrain.asset; later builds reuse it, so sculpting survives a re-import from Blender.
    /// Trees are terrain trees; bushes stay chunk meshes built from the scatter JSON (3,300 terrain trees cost ~5 ms of
    /// main thread in the Editor, chunks almost nothing). Re-snap puts trees, bushes, props and trails back on the ground.
    /// </summary>
    public static class S06Terrain
    {
        const string Dir = "Assets/_Spikes/S06/Terrain";
        const string DataPath = Dir + "/S06_Terrain.asset";
        // The Blender ground, never edited. Props and trails keep their height offset to it.
        const string OriginalPath = Dir + "/S06_Original_Terrain.asset";
        const string Fbx = "Assets/_Spikes/S06/map_greybox_v01.fbx";
        const string Scatter = "../Art/Export/map_greybox_v01_scatter.json";
        const string BushMeshPath = Dir + "/Bush.asset";
        // Chunk meshes live here, not in the scene (they are ~10 MB of YAML); unchanged chunks stay byte-identical.
        const string BushChunksPath = Dir + "/Bushes.asset";
        const int Resolution = 513;
        static readonly Vector3 Origin = new(-175f, -12f, -175f);
        static readonly Vector3 Size = new(350f, 42f, 350f);
        // Re-snap leaves these alone (the ground was flattened for them); it reports when the ground moved away.
        static readonly string[] Fixed = { "Road", "Pad_", "Bridge_", "Creek" };
        // Follow the ground vertex by vertex. The dead-zone outline is only a marker.
        static readonly string[] Draped = { "Trail", "DeadZone_" };
        // Vegetation stays off bad ground: rock (TL_Rock weight within the plant's radius), steep slope, or anything
        // but terrain under it (road, trails, pads, bridge, creek, buildings, Rock_*). A plant on bad ground moves to the
        // first good one of MoveTries spots within MoveRadius, else it goes.
        const float MaxRock = 0.25f, MaxSteepness = 32f, TreeRadius = 2f, BushRadius = 1f, MoveRadius = 10f;
        const int MoveTries = 24;

        /// <summary>Called by the scene builder on the fresh map instance (ground has a MeshCollider).</summary>
        public static void Setup(GameObject map)
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);
            if (data == null) data = Convert(map.transform);

            PrefabUtility.UnpackPrefabInstance(map, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var t in map.GetComponentsInChildren<Transform>().Where(t => t.name == "Terrain" || t.name.StartsWith("Tree_") || t.name.StartsWith("Bushes_")).ToArray())
                Object.DestroyImmediate(t.gameObject);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Ground";
            go.transform.position = Origin;
            var terrain = go.GetComponent<Terrain>();
            terrain.heightmapPixelError = 2f;
            terrain.treeDistance = 400f;
            // No billboards: Unity bakes them only for SpeedTree-style shaders, ours render dark blue.
            terrain.treeBillboardDistance = 400f;
            terrain.treeMaximumFullLODCount = 10000;
            Resnap(terrain, map.transform);
        }

        [MenuItem("Park Rangers/S06/Re-snap vegetation and props to terrain")]
        static void ResnapMenu()
        {
            var terrain = Object.FindAnyObjectByType<Terrain>();
            Resnap(terrain, GameObject.Find("map_greybox_v01").transform);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        }

        static void Resnap(Terrain terrain, Transform map)
        {
            var data = terrain.terrainData;
            var original = AssetDatabase.LoadAssetAtPath<TerrainData>(OriginalPath);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx).transform;

            // How far the ground moved since Blender. Always from the FBX original, so re-snapping twice changes nothing.
            float Delta(Vector3 p) => Height(data, p) - Height(original, p);
            foreach (Transform t in map)
            {
                var src = source.Find(t.name);
                if (src == null || Fixed.Any(t.name.StartsWith)) continue;
                if (Draped.Any(t.name.StartsWith)) Conform(t, src.GetComponent<MeshFilter>().sharedMesh, Delta);
                else
                {
                    var from = map.TransformPoint(src.localPosition);
                    var p = t.position;
                    t.position = new Vector3(p.x, from.y - Height(original, from) + Height(data, p), p.z);
                }
            }

            // After props and trails, so the ground rule sees their colliders where they end up.
            var good = GoodGround(terrain);
            int treesMoved = 0, treesRemoved = 0;
            var trees = new List<TreeInstance>();
            var old = data.treeInstances;
            for (var i = 0; i < old.Length; i++)
            {
                var tree = old[i];
                var at = new Vector2(Origin.x + tree.position.x * Size.x, Origin.z + tree.position.z * Size.z);
                var spot = Spot(good, at, TreeRadius, i);
                if (spot == null) { treesRemoved++; continue; }
                if (spot != at)
                {
                    treesMoved++;
                    tree.position = new Vector3((spot.Value.x - Origin.x) / Size.x, 0f, (spot.Value.y - Origin.z) / Size.z);
                }
                trees.Add(tree);
            }
            data.SetTreeInstances(trees.ToArray(), true);
            EditorUtility.SetDirty(data);
            var (bushesMoved, bushesRemoved) = BuildBushes(terrain, good);
            Debug.Log($"[S06] Vegetation: trees moved {treesMoved}, removed {treesRemoved}; bushes moved {bushesMoved}, removed {bushesRemoved}.");

            Report(map, Delta);
            AssetDatabase.SaveAssets();
        }

        // Trails, marker posts and the dead-zone outline: each vertex moves by how much the ground under it moved.
        static void Conform(Transform t, Mesh src, System.Func<Vector3, float> delta)
        {
            // DeadZone_Outline is edges only in Blender; the FBX importer drops those, so its mesh is empty.
            if (src.vertexCount == 0) return;
            var mesh = Object.Instantiate(src);
            mesh.name = src.name + " (snapped)";
            mesh.vertices = src.vertices.Select(v =>
            {
                var w = t.TransformPoint(v);
                return t.InverseTransformPoint(w + Vector3.up * delta(w));
            }).ToArray();
            mesh.RecalculateBounds();

            var filter = t.GetComponent<MeshFilter>();
            if (!AssetDatabase.Contains(filter.sharedMesh)) Object.DestroyImmediate(filter.sharedMesh);
            filter.sharedMesh = mesh;
            if (t.TryGetComponent<MeshCollider>(out var collider)) collider.sharedMesh = mesh;
        }

        // One renderer + sight collider per 50 m cell, on the walk-through SightBlocker layer. Pivots sit on the ground.
        static (int moved, int removed) BuildBushes(Terrain terrain, System.Func<Vector2, float, bool> good)
        {
            const float Cell = 50f;
            var old = terrain.transform.Find("Bushes");
            if (old) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Bushes").transform;
            root.SetParent(terrain.transform, true);

            var bush = AssetDatabase.LoadAssetAtPath<Mesh>(BushMeshPath);
            var material = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx).transform.Find("Bushes_0_0").GetComponent<Renderer>().sharedMaterial;
            var layer = LayerMask.NameToLayer("SightBlocker");
            var stored = AssetDatabase.LoadAllAssetsAtPath(BushChunksPath).OfType<Mesh>().ToDictionary(m => m.name);
            var bushes = Records(File.ReadAllText(Scatter), "bushes").Select((r, i) =>
            {
                var from = new Vector2(r.f["x"], r.f["y"]);
                return (b: r.f, from, at: Spot(good, from, BushRadius, i));
            }).ToList();
            var cells = bushes.Where(c => c.at != null)
                .GroupBy(c => (Mathf.FloorToInt((c.at.Value.x - Origin.x) / Cell), Mathf.FloorToInt((c.at.Value.y - Origin.z) / Cell)));
            foreach (var cell in cells)
            {
                var name = $"Bushes_{cell.Key.Item1}_{cell.Key.Item2}";
                if (!stored.TryGetValue(name, out var mesh))
                {
                    mesh = new Mesh { name = name };
                    if (AssetDatabase.LoadMainAssetAtPath(BushChunksPath) == null) AssetDatabase.CreateAsset(mesh, BushChunksPath);
                    else AssetDatabase.AddObjectToAsset(mesh, BushChunksPath);
                }
                mesh.CombineMeshes(cell.Select(c =>
                {
                    var (b, at) = (c.b, c.at.Value);
                    return new CombineInstance
                    {
                        mesh = bush,
                        // Blender yaw (CCW from above) is negative Unity yaw; Blender x/y/z scale is Unity x/z/y.
                        transform = Matrix4x4.TRS(new Vector3(at.x, Height(terrain.terrainData, new Vector3(at.x, 0f, at.y)), at.y),
                            Quaternion.Euler(0f, -b["rot"] * Mathf.Rad2Deg, 0f), new Vector3(b["rx"], b["rz"], b["ry"])),
                    };
                }).ToArray());
                EditorUtility.SetDirty(mesh);
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)) { layer = layer };
                go.transform.SetParent(root, true);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            return (bushes.Count(c => c.at != null && c.at != c.from), bushes.Count(c => c.at == null));
        }

        // Is (x, z) good ground for a plant of this radius? See MaxRock.
        static System.Func<Vector2, float, bool> GoodGround(Terrain terrain)
        {
            var data = terrain.terrainData;
            var n = data.alphamapResolution;
            var rockLayer = System.Array.FindIndex(data.terrainLayers, l => l.name == "TL_Rock");
            var rock = data.GetAlphamaps(0, 0, n, n);
            var texel = Size.x / (n - 1);
            var mask = ~(1 << LayerMask.NameToLayer("SightBlocker"));
            Physics.SyncTransforms();
            return (p, radius) =>
            {
                float u = (p.x - Origin.x) / Size.x, v = (p.y - Origin.z) / Size.z;
                if (u < 0f || u > 1f || v < 0f || v > 1f || data.GetSteepness(u, v) > MaxSteepness) return false;
                int cx = Mathf.RoundToInt(u * (n - 1)), cz = Mathf.RoundToInt(v * (n - 1)), r = Mathf.CeilToInt(radius / texel) + 1;
                for (var z = Mathf.Max(cz - r, 0); z <= Mathf.Min(cz + r, n - 1); z++)
                for (var x = Mathf.Max(cx - r, 0); x <= Mathf.Min(cx + r, n - 1); x++)
                    if (rock[z, x, rockLayer] > MaxRock && (new Vector2(Origin.x + x * texel, Origin.z + z * texel) - p).magnitude <= radius) return false;
                // Trees are part of the TerrainCollider, so skipping it skips them too; anything else down to the ground is in the way.
                var ground = Height(data, new Vector3(p.x, 0f, p.y));
                return Physics.RaycastAll(new Vector3(p.x, ground + 50f, p.y), Vector3.down, 50.05f, mask, QueryTriggerInteraction.Ignore)
                    .All(h => h.collider is TerrainCollider);
            };
        }

        // Where a plant from `from` stands: there if the ground is good, else the first good one of MoveTries random spots
        // within MoveRadius (seeded, so reruns pick the same one), else nowhere.
        static Vector2? Spot(System.Func<Vector2, float, bool> good, Vector2 from, float radius, int seed)
        {
            if (good(from, radius)) return from;
            var rng = new System.Random(seed);
            for (var i = 0; i < MoveTries; i++)
            {
                var angle = (float)rng.NextDouble() * 2f * Mathf.PI;
                var p = from + MoveRadius * Mathf.Sqrt((float)rng.NextDouble()) * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (good(p, radius)) return p;
            }
            return null;
        }

        // Fixed objects should see no ground change under them; snapped ones the same change as at their pivot.
        // Anything that was already off in Blender (bridge span, canopy) stays quiet.
        static void Report(Transform map, System.Func<Vector3, float> delta)
        {
            var issues = new List<string>();
            foreach (Transform t in map)
            {
                if (!t.TryGetComponent<MeshFilter>(out var filter) || Draped.Any(t.name.StartsWith) || t.name == "Creek") continue;
                var moved = Fixed.Any(t.name.StartsWith) ? 0f : delta(t.position);
                var worst = filter.sharedMesh.vertices.Select(t.TransformPoint).Select(w => (off: delta(w) - moved, at: w))
                    .OrderBy(x => Mathf.Abs(x.off)).LastOrDefault();
                if (Mathf.Abs(worst.off) > 0.2f)
                    issues.Add($"{t.name}: ground {(worst.off > 0f ? "above it" : "below it")} by {Mathf.Abs(worst.off):F2} m at {worst.at:F0}");
            }
            Debug.Log(issues.Count == 0 ? "[S06] Re-snap done, everything sits on the ground." : $"[S06] Re-snap done. Fix the ground here or move the object ({issues.Count}):\n" + string.Join("\n", issues));
        }

        static float Height(TerrainData data, Vector3 p) =>
            data.GetInterpolatedHeight((p.x - Origin.x) / Size.x, (p.z - Origin.z) / Size.z) + Origin.y;

        // ---- One-time conversion from the Blender export ----

        static TerrainData Convert(Transform map)
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Spikes/S06", "Terrain");
            var ground = map.Find("Terrain").GetComponent<MeshCollider>();
            Physics.SyncTransforms();

            var heights = new float[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                heights[z, x] = (GroundY(ground, Origin.x + Size.x * x / (Resolution - 1), Origin.z + Size.z * z / (Resolution - 1)) - Origin.y) / Size.y;
            AssetDatabase.CreateAsset(NewData(heights), OriginalPath);
            var data = NewData(heights);

            var groundColor = ground.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
            data.terrainLayers = new[] { Layer("Grass", groundColor), Layer("Rock", new Color(0.45f, 0.44f, 0.42f)) };

            var json = File.ReadAllText(Scatter);
            var trees = Records(json, "trees");
            var bushes = Records(json, "bushes");

            // Tree_001 is trees[0] in the JSON; take each type's mesh from its first tree.
            var treeObjects = map.Cast<Transform>().Where(t => t.name.StartsWith("Tree_")).ToArray();
            Mesh MeshOf(string type) => treeObjects.First(t => trees[int.Parse(t.name.Substring(5)) - 1].type == type).GetComponent<MeshFilter>().sharedMesh;
            var treeMats = treeObjects[0].GetComponent<Renderer>().sharedMaterials;

            data.treePrototypes = new[]
            {
                new TreePrototype { prefab = Prefab("Tree_Pine", MeshOf("tall"), treeMats) },
                new TreePrototype { prefab = Prefab("Tree_Spruce", MeshOf("low"), treeMats) },
            };
            // Blender yaw (CCW from above) is negative Unity yaw; the FBX tree mesh is turned 180°.
            data.SetTreeInstances(trees.Select(t => Instance(t.type == "tall" ? 0 : 1, t.f["x"], t.f["y"], t.f["s"], t.f["sz"], Mathf.PI - t.f["rot"])).ToArray(), true);
            BushMesh(map.Find("Bushes_0_0"), bushes, ground);

            AssetDatabase.CreateAsset(data, DataPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[S06] Ground converted to terrain: {trees.Count} trees, {bushes.Count} bushes.");
            return data;
        }

        static float GroundY(Collider ground, float x, float z)
        {
            x = Mathf.Clamp(x, Origin.x + 0.05f, Origin.x + Size.x - 0.05f);
            z = Mathf.Clamp(z, Origin.z + 0.05f, Origin.z + Size.z - 0.05f);
            if (!ground.Raycast(new Ray(new Vector3(x, 200f, z), Vector3.down), out var hit, 400f))
                throw new System.InvalidOperationException($"[S06] No ground under {x}, {z}");
            return hit.point.y;
        }

        static TerrainData NewData(float[,] heights)
        {
            var data = new TerrainData { heightmapResolution = Resolution };
            data.size = Size;
            data.SetHeights(0, 0, heights);
            return data;
        }

        static TerrainLayer Layer(string name, Color color)
        {
            var tex = new Texture2D(4, 4) { name = $"TL_{name}" };
            // Alpha 0: TerrainLit reads the diffuse alpha as smoothness.
            color.a = 0f;
            tex.SetPixels(Enumerable.Repeat(color, 16).ToArray());
            tex.Apply();
            AssetDatabase.CreateAsset(tex, $"{Dir}/TL_{name}_Colour.asset");
            var layer = new TerrainLayer { name = $"TL_{name}", diffuseTexture = tex, tileSize = new Vector2(10f, 10f) };
            AssetDatabase.CreateAsset(layer, $"{Dir}/TL_{name}.terrainlayer");
            return layer;
        }

        // Scatter JSON as (type, fields) records; Blender x/y are Unity x/z (the 180° map turn cancels the FBX flip).
        static List<(string type, Dictionary<string, float> f)> Records(string json, string array)
        {
            var start = json.IndexOf($"\"{array}\"");
            var section = json.Substring(start, json.IndexOf(']', start) - start);
            return Regex.Matches(section, @"\{[^}]*\}").Cast<Match>().Select(m => (
                Regex.Match(m.Value, @"""type"": ""(\w+)""").Groups[1].Value,
                Regex.Matches(m.Value, @"""(\w+)"": (-?[0-9.]+)").Cast<Match>()
                    .ToDictionary(f => f.Groups[1].Value, f => float.Parse(f.Groups[2].Value, CultureInfo.InvariantCulture)))).ToList();
        }

        static TreeInstance Instance(int prototype, float x, float z, float width, float height, float rotation) => new()
        {
            prototypeIndex = prototype,
            position = new Vector3((x - Origin.x) / Size.x, 0f, (z - Origin.z) / Size.z),
            widthScale = width,
            heightScale = height,
            rotation = rotation,
            color = Color.white,
            lightmapColor = Color.white,
        };

        // One bush (icosahedron) cut out of the first Blender chunk, undone to unit scale, pivot on the ground.
        static void BushMesh(Transform chunk, List<(string type, Dictionary<string, float> f)> bushes, Collider ground)
        {
            var src = chunk.GetComponent<MeshFilter>().sharedMesh;
            var tris = src.triangles.Take(60).ToArray();
            var world = src.vertices.Take(tris.Max() + 1).Select(chunk.TransformPoint).ToArray();
            var centre = world.Aggregate(Vector3.zero, (a, v) => a + v) / world.Length;
            var b = bushes.OrderBy(r => new Vector2(r.f["x"] - centre.x, r.f["y"] - centre.z).sqrMagnitude).First().f;
            var pivot = new Vector3(b["x"], GroundY(ground, b["x"], b["y"]), b["y"]);
            var unturn = Quaternion.Euler(0f, b["rot"] * Mathf.Rad2Deg, 0f);
            var mesh = new Mesh
            {
                name = "Bush",
                vertices = world.Select(w => unturn * (w - pivot)).Select(l => new Vector3(l.x / b["rx"], l.y / b["rz"], l.z / b["ry"])).ToArray(),
                triangles = tris,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, BushMeshPath);
        }

        static GameObject Prefab(string name, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials.Select(Instanced).ToArray();
            // Trunk only (submesh 0); canopies and spruce branches stay walk-through.
            var trunk = mesh.GetSubMesh(0).bounds;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.center = trunk.center;
            capsule.radius = Mathf.Max(trunk.extents.x, trunk.extents.z);
            capsule.height = trunk.size.y;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Dir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // Editable copy of an FBX material with GPU instancing on (terrain trees draw instanced).
        static Material Instanced(Material src)
        {
            var path = $"{Dir}/{src.name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(src) { enableInstancing = true };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
