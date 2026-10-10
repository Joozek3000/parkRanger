using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ParkRangers.Spikes.S06
{
    /// <summary>
    /// Builds the night assets in S06/Lights (flashlight cookie, sky and beam cone materials) and wires them into the
    /// S06_Ranger prefab and the scene's MapLighting. Safe to re-run.
    /// </summary>
    public static class S06NightAssets
    {
        const string Dir = "Assets/_Spikes/S06/Lights";
        const string CookiePath = Dir + "/FlashlightCookie.png";
        const string PlayerPath = "Assets/_Spikes/S06/Prefabs/S06_Ranger.prefab";

        [MenuItem("Park Rangers/S06/Build night assets")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Spikes/S06", "Lights");
            var cookie = Cookie();
            var sky = Mat("Sky", "ParkRangers/S06/Sky");
            var cone = Mat("BeamCone", "ParkRangers/S06/BeamCone");

            var player = PrefabUtility.LoadPrefabContents(PlayerPath);
            var flashlight = new SerializedObject(player.GetComponent<S06Flashlight>());
            flashlight.FindProperty("cookie").objectReferenceValue = cookie;
            flashlight.FindProperty("coneMaterial").objectReferenceValue = cone;
            flashlight.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
            PrefabUtility.UnloadPrefabContents(player);

            var lighting = Object.FindAnyObjectByType<MapLighting>();
            var so = new SerializedObject(lighting);
            so.FindProperty("skyMaterial").objectReferenceValue = sky;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(lighting.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        // Torch beam over a 60° spot: tight hotspot (~13° wide at half brightness), a soft ring, dim spill to the edge.
        // The spill stays at a few percent of the hotspot (as on a real LED torch), or the ground at your feet burns out
        // long before the hotspot reaches 40 m.
        static Texture Cookie()
        {
            const int Size = 256;
            const float HalfAngle = 30f;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var r = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f).magnitude;
                var deg = Mathf.Atan(r * Mathf.Tan(HalfAngle * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
                var v = Mathf.Exp(-Sq(deg / 7.8f)) + 0.12f * Mathf.Exp(-Sq((deg - 11f) / 2.2f)) + 0.035f * (1f - Mathf.SmoothStep(0f, 1f, (deg - 21f) / 8f));
                v = Mathf.Clamp01(v / 1.04f);
                tex.SetPixel(x, y, new Color(v, v, v, v));
            }
            File.WriteAllBytes(CookiePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(CookiePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(CookiePath);
            importer.textureType = TextureImporterType.Cookie;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture>(CookiePath);
        }

        static float Sq(float v) => v * v;

        static Material Mat(string name, string shader)
        {
            var path = $"{Dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
