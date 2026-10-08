using System.IO;
using System.Linq;
using ParkRangers.Spikes.Shared;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ParkRangers.Spikes.S06
{
    /// <summary>
    /// Copies the Blender export into the spike, then builds S06_Map from it: colliders, spawn, network.
    /// Rerun after every re-export from Blender; it overwrites the scene and the player prefab.
    /// </summary>
    public static class S06SceneBuilder
    {
        const string Root = "Assets/_Spikes/S06";
        const string ScenePath = Root + "/S06_Map.unity";
        const string Fbx = "map_greybox_v01.fbx";

        [MenuItem("Park Rangers/S06/Import map and build scene")]
        public static void Build()
        {
            File.Copy(Path.Combine(Application.dataPath, "../../Art/Export", Fbx), Path.Combine(Application.dataPath, "_Spikes/S06", Fbx), true);
            AssetDatabase.ImportAsset($"{Root}/{Fbx}", ImportAssetOptions.ForceUpdate);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Before anyone spawns: top-down view of the whole map. Under the player camera (depth 0).
            var overview = new GameObject("Overview Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            overview.transform.SetPositionAndRotation(new Vector3(0f, 200f, 0f), Quaternion.Euler(90f, 0f, 0f));
            overview.orthographic = true;
            overview.orthographicSize = 180f;
            overview.depth = -1f;

            var map = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{Fbx}"));
            foreach (var r in map.GetComponentsInChildren<MeshRenderer>())
            {
                var go = r.gameObject;
                if (go.name.StartsWith("Tree_"))
                {
                    // Trunk only (submesh 0: r 0.35 m, 4 m tall, pivot at the base); canopies stay walk-under.
                    var capsule = go.AddComponent<CapsuleCollider>();
                    capsule.center = new Vector3(0f, 2f, 0f);
                    capsule.radius = 0.35f;
                    capsule.height = 4f;
                }
                else if (go.name != "DeadZone_Outline")
                {
                    go.AddComponent<MeshCollider>();
                }
            }

            // Station yard, clear of the buildings, facing the map centre.
            var yard = map.transform.Find("Pad_StationYard").GetComponent<Renderer>().bounds;
            var spawnPos = new Vector3(yard.center.x, yard.max.y + 0.1f, yard.min.z + 6f);
            var spawn = new GameObject("Spawn").transform;
            spawn.SetPositionAndRotation(spawnPos, Quaternion.LookRotation(Vector3.Scale(-spawnPos, new Vector3(1f, 0f, 1f))));

            var netGo = new GameObject("NetworkManager");
            var transport = netGo.AddComponent<UnityTransport>();
            var net = netGo.AddComponent<NetworkManager>();
            net.NetworkConfig ??= new NetworkConfig();
            net.NetworkConfig.NetworkTransport = transport;
            net.NetworkConfig.PlayerPrefab = BuildPlayer();
            net.NetworkConfig.EnableSceneManagement = false;
            netGo.AddComponent<NetworkBootstrap>();
            netGo.AddComponent<NetworkStatsOverlay>();

            if (EditorBuildSettings.scenes.All(s => s.path != ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[S06] Map imported and scene built.");
        }

        static GameObject BuildPlayer()
        {
            if (!AssetDatabase.IsValidFolder(Root + "/Prefabs")) AssetDatabase.CreateFolder(Root, "Prefabs");
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Spikes/Shared/Prefabs/SpikePlayer.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            go.AddComponent<MapWalker>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/Prefabs/S06_Ranger.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
