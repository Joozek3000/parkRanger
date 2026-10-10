using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ParkRangers.Spikes.S06
{
    /// <summary>
    /// Lamps and glowing parts on the landmarks, each a child of its landmark so Re-snap moves it along.
    /// Placed from the landmarks' mesh bounds. Safe to re-run: removes the old Lamp_* objects first.
    /// </summary>
    public static class S06LandmarkLightsBuilder
    {
        const string Prefix = "Lamp_";
        const string Dir = "Assets/_Spikes/S06/Lights";

        [MenuItem("Park Rangers/S06/Build landmark lights")]
        static void Menu()
        {
            Build();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        public static void Build()
        {
            var map = GameObject.Find("map_greybox_v01").transform;
            foreach (var old in map.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(Prefix)).ToArray())
                Object.DestroyImmediate(old.gameObject);
            var oldRoot = GameObject.Find("LandmarkLights");
            if (oldRoot) Object.DestroyImmediate(oldRoot);

            Color red = new(1f, 0.1f, 0.05f), warm = new(1f, 0.8f, 0.45f), pink = new(1f, 0.15f, 0.6f), cool = new(0.85f, 0.9f, 1f);
            var lights = new List<Light>();
            var glows = new List<GameObject>();

            // Radio mast: blinking red beacon over the red lamp at the top.
            var mast = map.Find("RadioMast");
            var top = Center(mast, 1);
            var beaconGlow = Glow(mast, "Beacon", PrimitiveType.Sphere, top, 1.2f * Vector3.one, Mat("Beacon_Red", red));
            var beaconLight = Lamp(mast, "Beacon", LightType.Point, top, red, 4f, 15f);

            // Fire tower: lamp in the cabin, a band of lit windows around it.
            var tower = map.Find("FireTower");
            var cabin = Sub(tower, 1);
            glows.Add(Glow(tower, "Windows", PrimitiveType.Cube, tower.TransformPoint(cabin.center),
                Vector3.Scale(cabin.size, new Vector3(1.02f, 0.35f, 1.02f)), Mat("Beacon_Warm", warm)));
            lights.Add(Lamp(tower, "Cabin", LightType.Point, tower.TransformPoint(cabin.center), warm, 6f, 12f));

            // Diner sign: the panel glows; a lamp over the parking.
            var sign = map.Find("DinerSign");
            var panel = Sub(sign, 1);
            glows.Add(Glow(sign, "Panel", PrimitiveType.Cube, sign.TransformPoint(panel.center),
                Vector3.Scale(panel.size, new Vector3(1.02f, 1.02f, 1.2f)), Mat("Beacon_Sign", pink)));
            var parking = map.Find("Pad_Parking").GetComponent<Renderer>().bounds;
            lights.Add(Lamp(sign, "Parking", LightType.Point, parking.center + 7f * Vector3.up, Color.Lerp(pink, Color.white, 0.6f), 30f, 22f));

            // Station: porch lamp on the yard side, one floodlight on the roof edge aimed down into the yard.
            var station = map.Find("RangerStation");
            var house = station.GetComponent<Renderer>().bounds;
            var yard = map.Find("Pad_StationYard").GetComponent<Renderer>().bounds;
            var edge = house.ClosestPoint(new Vector3(yard.center.x, house.max.y, yard.center.z));
            var outward = Vector3.ProjectOnPlane(yard.center - edge, Vector3.up).normalized;
            var porch = edge + 0.5f * outward + 0.8f * Vector3.down;
            glows.Add(Glow(station, "Porch", PrimitiveType.Sphere, porch, 0.35f * Vector3.one, Mat("Beacon_Warm", warm)));
            lights.Add(Lamp(station, "Porch", LightType.Point, porch, warm, 5f, 10f));
            var flood = Lamp(station, "Flood", LightType.Spot, edge + outward + Vector3.up, cool, 40f, 25f);
            var aim = edge + 10f * outward;
            aim.y = yard.max.y;
            flood.transform.rotation = Quaternion.LookRotation(aim - flood.transform.position);
            flood.spotAngle = 110f;
            lights.Add(flood);

            // Gas station canopy: two downlights along its long side.
            var canopy = map.Find("GasStation_Canopy");
            var roof = Sub(canopy, 0);
            foreach (var side in new[] { -1f, 1f })
            {
                var down = Lamp(canopy, side < 0f ? "DownL" : "DownR", LightType.Spot,
                    canopy.TransformPoint(roof.center + new Vector3(side * roof.extents.x * 0.5f, -roof.extents.y - 0.1f, 0f)), cool, 15f, 10f);
                down.transform.rotation = Quaternion.LookRotation(Vector3.down);
                down.spotAngle = 110f;
                lights.Add(down);
            }

            // Bridge: a small lamp at each end of the railings (they run along local x).
            var bridge = map.Find("Bridge_Railings");
            var rails = Sub(bridge, 0);
            foreach (var side in new[] { -1f, 1f })
                lights.Add(Lamp(bridge, side < 0f ? "EndA" : "EndB", LightType.Point,
                    bridge.TransformPoint(new Vector3(side * rails.extents.x, rails.max.y + 0.8f, rails.center.z)), warm, 3f, 6f));

            new GameObject("LandmarkLights").AddComponent<S06LandmarkLights>().Init(lights.ToArray(), glows.ToArray(), beaconLight, beaconGlow);
            Debug.Log($"[S06] Landmark lights built: {lights.Count + 1} lights, {glows.Count + 1} glows.");
        }

        static Bounds Sub(Transform t, int submesh) => t.GetComponent<MeshFilter>().sharedMesh.GetSubMesh(submesh).bounds;

        static Vector3 Center(Transform t, int submesh) => t.TransformPoint(Sub(t, submesh).center);

        static GameObject Glow(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = Prefix + name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static Light Lamp(Transform parent, string name, LightType type, Vector3 position, Color color, float intensity, float range)
        {
            var light = new GameObject(Prefix + name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.position = position;
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        static Material Mat(string name, Color color)
        {
            var path = $"{Dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Spikes/S06", "Lights");
                mat = new Material(Shader.Find("ParkRangers/S06/Beacon"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
