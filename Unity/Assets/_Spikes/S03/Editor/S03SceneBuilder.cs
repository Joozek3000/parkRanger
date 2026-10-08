using System.Collections.Generic;
using System.Linq;
using ParkRangers.Spikes.Shared;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ParkRangers.Spikes.S03
{
    /// <summary>
    /// Builds S03_Witness, its prefabs and materials from code, so the layout can be regenerated.
    /// Rerunning overwrites them. Throwaway, like the rest of the spike.
    /// </summary>
    public static class S03SceneBuilder
    {
        const string Root = "Assets/_Spikes/S03";
        const string ScenePath = Root + "/S03_Witness.unity";
        const int IgnoreRaycast = 2;

        [MenuItem("Park Rangers/S03/Build scene and prefabs")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Root + "/Prefabs")) AssetDatabase.CreateFolder(Root, "Prefabs");
            if (!AssetDatabase.IsValidFolder(Root + "/Materials")) AssetDatabase.CreateFolder(Root, "Materials");

            var mats = new Dictionary<string, Material>
            {
                ["Ground"] = Mat("Ground", new Color(0.32f, 0.45f, 0.25f)),
                ["Road"] = Mat("Road", new Color(0.25f, 0.25f, 0.27f)),
                ["Trunk"] = Mat("Trunk", new Color(0.35f, 0.24f, 0.15f)),
                ["Canopy"] = Mat("Canopy", new Color(0.15f, 0.32f, 0.15f)),
                ["Rock"] = Mat("Rock", new Color(0.5f, 0.5f, 0.52f)),
                ["Bush"] = Mat("Bush", new Color(0.2f, 0.4f, 0.18f)),
                ["Diner"] = Mat("Diner", new Color(0.7f, 0.25f, 0.2f)),
                ["DinerZone"] = Mat("DinerZone", new Color(0.2f, 0.9f, 0.3f)),
                ["Tourist"] = Mat("Tourist", Color.white),
                ["Bigfoot"] = Mat("Bigfoot", new Color(0.4f, 0.25f, 0.12f)),
                ["Cone"] = Mat("Cone", new Color(1f, 0.9f, 0.2f), "Universal Render Pipeline/Unlit"),
            };

            var tourist = BuildTourist(mats);
            var bigfoot = BuildBigfoot(mats);
            var ranger = BuildRanger();

            // Scene management is off: in-scene NetworkObjects must be registered prefab instances (S01).
            var directorGo = new GameObject("WitnessDirector");
            directorGo.AddComponent<NetworkObject>();
            directorGo.AddComponent<WitnessDirector>();
            var director = SavePrefab(directorGo, "WitnessDirector");

            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            list.Add(new NetworkPrefab { Prefab = tourist.gameObject });
            list.Add(new NetworkPrefab { Prefab = bigfoot.gameObject });
            list.Add(new NetworkPrefab { Prefab = director });
            AssetDatabase.CreateAsset(list, Root + "/S03_NetworkPrefabs.asset");

            BuildScene(mats, tourist, bigfoot, ranger, director, list);
            AssetDatabase.SaveAssets();
            Debug.Log("[S03] Scene and prefabs built.");
        }

        static void BuildScene(Dictionary<string, Material> mats, Tourist tourist, Anomaly bigfoot, GameObject ranger, GameObject directorPrefab, NetworkPrefabsList list)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            Random.InitState(3);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var overview = new GameObject("Overview Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            overview.transform.SetPositionAndRotation(new Vector3(0f, 35f, -60f), Quaternion.Euler(30f, 0f, 0f));

            // Level: everything the NavMesh is baked from (host, at runtime).
            var level = new GameObject("Level");
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            Box("Ground", level, new Vector3(0f, -0.5f, 0f), new Vector3(130f, 1f, 130f), mats["Ground"]);
            NoCollider(Box("Road", level, new Vector3(0f, 0.01f, 0f), new Vector3(130f, 0.02f, 6f), mats["Road"]));

            // Diner: building + green zone in front; tourists with a photo run to the zone.
            Box("Diner", level, new Vector3(50f, 2f, -16f), new Vector3(10f, 4f, 8f), mats["Diner"]);
            var dinerZone = NoCollider(Box("DinerZone", level, new Vector3(50f, 0.02f, -8f), new Vector3(8f, 0.04f, 5f), mats["DinerZone"]));

            // Forest north of the road, poisson-ish scatter.
            var forest = Child("Forest", level);
            var placed = new List<Vector2>();
            for (var i = 0; i < 600; i++)
            {
                var p = new Vector2(Random.Range(-55f, 45f), Random.Range(9f, 58f));
                if (placed.Any(q => (q - p).sqrMagnitude < 3.5f * 3.5f)) continue;
                placed.Add(p);
                Tree(forest, new Vector3(p.x, 0f, p.y), Random.Range(0.45f, 0.8f), mats);
            }

            // Test props in the open meadow south of the road.
            var tests = Child("Tests", level);
            var grove = Child("ThickGrove", tests);
            for (var x = -2; x <= 2; x++)
            for (var z = -2; z <= 2; z++)
                Tree(grove, new Vector3(-18f + x * 1.4f + Random.Range(-0.3f, 0.3f), 0f, -22f + z * 1.4f + Random.Range(-0.3f, 0.3f)), 0.6f, mats);
            Box("BigRock", tests, new Vector3(8f, 1.25f, -18f), new Vector3(4f, 2.5f, 3f), mats["Rock"]);
            Box("Rock2", tests, new Vector3(-35f, 1f, -32f), new Vector3(3f, 2f, 3f), mats["Rock"], 25f);
            Box("Rock3", tests, new Vector3(30f, 1.1f, -36f), new Vector3(3.5f, 2.2f, 2.5f), mats["Rock"], -15f);
            Box("RockForest", tests, new Vector3(-4f, 1.2f, 14f), new Vector3(3.5f, 2.4f, 3f), mats["Rock"], 10f);
            for (var i = 0; i < 12; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.name = "Bush";
                b.transform.SetParent(tests.transform);
                b.transform.position = new Vector3(Random.Range(-50f, 40f), 0.6f, Random.Range(-50f, -8f));
                b.transform.localScale = new Vector3(2.2f, 1.6f, 2.2f);
                b.GetComponent<Renderer>().sharedMaterial = mats["Bush"];
            }

            var waypoints = Points("Waypoints",
                new(-45f, 0f, -3f), new(-25f, 0f, 3f), new(-5f, 0f, -3f), new(15f, 0f, 3f), new(35f, 0f, -3f),
                new(-35f, 0f, -22f), new(-8f, 0f, -36f), new(15f, 0f, -30f), new(35f, 0f, -22f), new(0f, 0f, -10f),
                new(-22f, 0f, 22f), new(12f, 0f, 30f));
            var anomalySpawns = Points("AnomalySpawns", new(8f, 1.2f, -21f), new(-25f, 1.2f, 30f), new(24f, 1.2f, -42f));
            var entrance = new GameObject("Entrance").transform;
            entrance.SetPositionAndRotation(new Vector3(-58f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));

            // Network.
            var netGo = new GameObject("NetworkManager");
            var transport = netGo.AddComponent<UnityTransport>();
            var net = netGo.AddComponent<NetworkManager>();
            net.NetworkConfig ??= new NetworkConfig();
            net.NetworkConfig.NetworkTransport = transport;
            net.NetworkConfig.PlayerPrefab = Load<Transform>("S03_Ranger").gameObject;
            net.NetworkConfig.EnableSceneManagement = false;
            net.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            netGo.AddComponent<NetworkBootstrap>();
            netGo.AddComponent<NetworkStatsOverlay>();

            var director = ((GameObject)PrefabUtility.InstantiatePrefab(directorPrefab)).GetComponent<WitnessDirector>();
            // Reloaded: the objects SaveAsPrefabAsset returned can go stale after later asset writes.
            Set(director, "touristPrefab", Load<Tourist>("Tourist"));
            Set(director, "anomalyPrefab", Load<Anomaly>("Bigfoot"));
            Set(director, "waypointRoot", waypoints);
            Set(director, "diner", dinerZone.transform);
            Set(director, "entrance", entrance);
            Set(director, "navMesh", surface);
            Set(director, "sun", sun);
            var so = new SerializedObject(director);
            var spawns = so.FindProperty("anomalySpawns");
            spawns.arraySize = anomalySpawns.childCount;
            for (var i = 0; i < anomalySpawns.childCount; i++) spawns.GetArrayElementAtIndex(i).objectReferenceValue = anomalySpawns.GetChild(i);
            so.ApplyModifiedPropertiesWithoutUndo();

            // In Build Settings before saving, or NGO doesn't record the in-scene hashes.
            if (EditorBuildSettings.scenes.All(s => s.path != ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        }

        static Tourist BuildTourist(Dictionary<string, Material> mats)
        {
            var go = new GameObject("Tourist");
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.height = 1.8f;
            col.radius = 0.35f;
            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.acceleration = 20f;
            agent.angularSpeed = 360f;
            agent.enabled = false; // the host enables it on spawn; clients have no NavMesh
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncRotAngleX = nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 3f;
            audio.maxDistance = 60f;

            var body = Part(go, PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), mats["Tourist"]);
            Part(go, PrimitiveType.Cube, new Vector3(0f, 1.55f, 0.35f), new Vector3(0.2f, 0.12f, 0.25f), mats["Road"]); // camera/nose: facing

            var coneGo = new GameObject("Cone");
            coneGo.transform.SetParent(go.transform, false);
            var cone = coneGo.AddComponent<LineRenderer>();
            cone.useWorldSpace = false;
            cone.widthMultiplier = 0.06f;
            cone.sharedMaterial = mats["Cone"];
            cone.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cone.loop = true;

            var t = go.AddComponent<Tourist>();
            Set(t, "body", body.GetComponent<Renderer>());
            Set(t, "cone", cone);
            Set(t, "voice", audio);
            SetLayer(go, IgnoreRaycast);
            return SavePrefab(go, "Tourist").GetComponent<Tourist>();
        }

        static Anomaly BuildBigfoot(Dictionary<string, Material> mats)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Bigfoot";
            go.transform.localScale = new Vector3(1f, 1.1f, 1f);
            go.GetComponent<Renderer>().sharedMaterial = mats["Bigfoot"];
            Part(go, PrimitiveType.Cube, new Vector3(0f, 0.6f, 0.4f), new Vector3(0.6f, 0.15f, 0.3f), mats["Road"]); // face
            var body = go.AddComponent<Rigidbody>();
            body.mass = 120f;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            go.AddComponent<NetworkRigidbody>();
            go.AddComponent<Anomaly>();
            SetLayer(go, IgnoreRaycast);
            return SavePrefab(go, "Bigfoot").GetComponent<Anomaly>();
        }

        // Variant of the Shared player + Ranger, with the AudioListener the Shared camera lacks.
        static GameObject BuildRanger()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Spikes/Shared/Prefabs/SpikePlayer.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            go.AddComponent<Ranger>();
            go.GetComponentInChildren<Camera>(true).gameObject.AddComponent<AudioListener>();
            return SavePrefab(go, "S03_Ranger");
        }

        static T Load<T>(string prefab) where T : Component =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/{prefab}.prefab").GetComponent<T>();

        static GameObject SavePrefab(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void Tree(GameObject parent, Vector3 at, float trunkWidth, Dictionary<string, Material> mats)
        {
            var tree = Child("Tree", parent);
            tree.transform.position = at;
            Part(tree, PrimitiveType.Cylinder, new Vector3(0f, 3f, 0f), new Vector3(trunkWidth, 3f, trunkWidth), mats["Trunk"], keepCollider: true);
            Part(tree, PrimitiveType.Sphere, new Vector3(0f, 7f, 0f), new Vector3(4f, 5f, 4f), mats["Canopy"]);
        }

        static GameObject Part(GameObject parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Box(string name, GameObject parent, Vector3 pos, Vector3 scale, Material mat, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject NoCollider(GameObject go)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Child(string name, GameObject parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            return go;
        }

        static Transform Points(string name, params Vector3[] points)
        {
            var root = new GameObject(name).transform;
            for (var i = 0; i < points.Length; i++) new GameObject($"{name}_{i}").transform.SetParent(root, false);
            for (var i = 0; i < points.Length; i++) root.GetChild(i).position = points[i];
            return root;
        }

        static Material Mat(string name, Color color, string shader = "Universal Render Pipeline/Lit")
        {
            var mat = new Material(Shader.Find(shader));
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, $"{Root}/Materials/{name}.mat");
            return mat;
        }

        static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
