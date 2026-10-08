using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkRangers.Spikes.S06
{
    /// <summary>
    /// Afternoon / dusk / night presets with linear fog, cycled with N. Sits on the sun.
    /// Local only: each player switches their own view (enough to judge sightlines).
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class MapLighting : MonoBehaviour
    {
        static readonly (string name, Color sun, float intensity, float pitch, Color ambient, Color fog, float fogStart, float fogEnd)[] Presets =
        {
            ("afternoon", new Color(1f, 0.95f, 0.85f), 1.2f, 45f, new Color(0.55f, 0.6f, 0.65f), new Color(0.7f, 0.76f, 0.82f), 60f, 350f),
            ("dusk", new Color(1f, 0.55f, 0.3f), 0.7f, 8f, new Color(0.35f, 0.3f, 0.35f), new Color(0.55f, 0.42f, 0.42f), 30f, 180f),
            ("night", new Color(0.45f, 0.55f, 0.85f), 0.1f, 40f, new Color(0.05f, 0.06f, 0.1f), new Color(0.02f, 0.03f, 0.05f), 5f, 60f),
        };

        Light sun;
        int index;

        void Start()
        {
            sun = GetComponent<Light>();
            Apply();
        }

        void Update()
        {
            if (Keyboard.current.nKey.wasPressedThisFrame)
            {
                index = (index + 1) % Presets.Length;
                Apply();
            }

            // ponytail: re-set every frame because the player camera spawns later; fine for a handful of cameras.
            foreach (var cam in Camera.allCameras)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = RenderSettings.fogColor;
            }
        }

        void Apply()
        {
            var p = Presets[index];
            sun.color = p.sun;
            sun.intensity = p.intensity;
            transform.rotation = Quaternion.Euler(p.pitch, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = p.ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = p.fog;
            RenderSettings.fogStartDistance = p.fogStart;
            RenderSettings.fogEndDistance = p.fogEnd;
        }

        void OnGUI() =>
            GUI.Label(new Rect(10, Screen.height - 45, 400, 20), $"Light [N]: {Presets[index].name}, fog {Presets[index].fogStart:F0}–{Presets[index].fogEnd:F0} m");
    }
}
