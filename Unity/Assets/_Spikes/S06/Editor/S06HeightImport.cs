using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ParkRangers.Spikes.S06
{
    /// <summary>
    /// Imports the coordinator's v02 heightmap (Tools/terrain/s06_terrain_v02.py) and rock weights into the Ground terrain,
    /// then re-snaps vegetation and props. Both files are 513x513, row = z (row 0 = z -175), col = x.
    /// </summary>
    public static class S06HeightImport
    {
        const string HeightPath = "../Art/Source/terrain/S06_height_v02.r16";
        const string RockPath = "../Art/Source/terrain/S06_rock_v02.r8";
        const int Res = 513;

        [MenuItem("Park Rangers/S06/Import heightmap v02")]
        static void Import()
        {
            var terrain = GameObject.Find("Ground")?.GetComponent<Terrain>();
            if (terrain == null) throw new System.InvalidOperationException("[S06] No Ground terrain in the open scene.");
            var data = terrain.terrainData;
            if (data.heightmapResolution != Res || data.size != new Vector3(350f, 42f, 350f) || terrain.transform.position != new Vector3(-175f, -12f, -175f)
                || data.terrainLayers.Length != 2 || data.terrainLayers[0].name != "TL_Grass" || data.terrainLayers[1].name != "TL_Rock")
                throw new System.InvalidOperationException($"[S06] Ground doesn't match the v02 heightmap: resolution {data.heightmapResolution}, size {data.size}, " +
                    $"position {terrain.transform.position}, {data.terrainLayers.Length} layers (want 513, (350, 42, 350), (-175, -12, -175), Grass + Rock).");

            var raw = Read(HeightPath, Res * Res * 2);
            var heights = new float[Res, Res];
            for (var z = 0; z < Res; z++)
            for (var x = 0; x < Res; x++)
                heights[z, x] = System.BitConverter.ToUInt16(raw, (z * Res + x) * 2) / 65535f;
            data.SetHeights(0, 0, heights);

            var rock = Read(RockPath, Res * Res);
            var n = data.alphamapResolution;
            var maps = new float[n, n, 2];
            for (var z = 0; z < n; z++)
            for (var x = 0; x < n; x++)
            {
                // Alphamap texel i sits at i / (n - 1) across the terrain, same as the heightmap grid.
                var r = Bilinear(rock, (Res - 1f) * x / (n - 1), (Res - 1f) * z / (n - 1)) / 255f;
                maps[z, x, 0] = 1f - r;
                maps[z, x, 1] = r;
            }
            data.SetAlphamaps(0, 0, maps);
            EditorUtility.SetDirty(data);

            EditorApplication.ExecuteMenuItem("Park Rangers/S06/Re-snap vegetation and props to terrain");
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(terrain.gameObject.scene);
        }

        static byte[] Read(string path, int length)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length != length) throw new System.InvalidOperationException($"[S06] {path} is {bytes.Length} bytes, want {length}.");
            return bytes;
        }

        static float Bilinear(byte[] g, float x, float z)
        {
            int x0 = Mathf.Min((int)x, Res - 2), z0 = Mathf.Min((int)z, Res - 2);
            float fx = x - x0, fz = z - z0;
            float At(int dx, int dz) => g[(z0 + dz) * Res + x0 + dx];
            return Mathf.Lerp(Mathf.Lerp(At(0, 0), At(1, 0), fx), Mathf.Lerp(At(0, 1), At(1, 1), fx), fz);
        }
    }
}
