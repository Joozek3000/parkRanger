using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ParkRangers.Spikes.S06
{
    /// <summary>
    /// Afternoon / dusk (blue hour) / night (moon) / night (no moon), cycled with N. Sits on the sun, which is the moon
    /// at night (URP gives directional shadows to the main light only). Drives the sky material, trilight ambient,
    /// exponential fog and a global post-processing Volume. Local only: each player switches their own view.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class MapLighting : MonoBehaviour
    {
        struct Look
        {
            public string Name;
            public Color Light;
            public float Intensity, Elevation, Azimuth;
            public bool Shadows;
            public Color Zenith, Horizon, Glow, Disc;
            public float Stars;
            public Color AmbientSky, AmbientEquator, AmbientGround, Fog;
            public float FogDensity, Reflections, Exposure, Saturation, Temperature, Vignette, Grain;
        }

        // Azimuth is where the sun/moon stands, clockwise from north (+z).
        static readonly Look[] Looks =
        {
            new()
            {
                Name = "afternoon", Light = new(1f, 0.95f, 0.85f), Intensity = 1.2f, Elevation = 45f, Azimuth = 150f, Shadows = true,
                Zenith = new(0.22f, 0.4f, 0.75f), Horizon = new(0.68f, 0.78f, 0.88f), Disc = new(4f, 3.8f, 3.4f),
                AmbientSky = new(0.55f, 0.62f, 0.72f), AmbientEquator = new(0.45f, 0.48f, 0.45f), AmbientGround = new(0.25f, 0.23f, 0.2f),
                Fog = new(0.68f, 0.76f, 0.85f), FogDensity = 0.002f, Reflections = 1f,
            },
            new()
            {
                Name = "dusk", Light = new(1f, 0.5f, 0.25f), Intensity = 0.25f, Elevation = 2f, Azimuth = 300f, Shadows = true,
                Zenith = new(0.05f, 0.08f, 0.22f), Horizon = new(0.28f, 0.3f, 0.45f), Glow = new(1.1f, 0.45f, 0.15f),
                AmbientSky = new(0.16f, 0.2f, 0.34f), AmbientEquator = new(0.1f, 0.1f, 0.15f), AmbientGround = new(0.04f, 0.04f, 0.05f),
                Fog = new(0.22f, 0.24f, 0.36f), FogDensity = 0.004f, Reflections = 0.4f, Exposure = 0.6f, Saturation = -10f, Temperature = -5f,
            },
            new()
            {
                Name = "night (moon)", Light = new(0.62f, 0.72f, 0.95f), Intensity = 0.08f, Elevation = 48f, Azimuth = 200f, Shadows = true,
                Zenith = new(0.012f, 0.018f, 0.05f), Horizon = new(0.08f, 0.1f, 0.16f), Disc = new(2.5f, 2.6f, 2.8f), Stars = 0.5f,
                AmbientSky = new(0.03f, 0.04f, 0.075f), AmbientEquator = new(0.01f, 0.012f, 0.02f), AmbientGround = new(0.003f, 0.003f, 0.004f),
                Fog = new(0.06f, 0.075f, 0.11f), FogDensity = 0.005f, Reflections = 0.1f,
                Exposure = 1.1f, Saturation = -45f, Temperature = -15f, Vignette = 0.3f, Grain = 0.25f,
            },
            new()
            {
                Name = "night (no moon)", Light = new(0.55f, 0.65f, 0.95f), Intensity = 0.02f, Elevation = 48f, Azimuth = 200f, Shadows = false,
                Zenith = new(0.004f, 0.006f, 0.016f), Horizon = new(0.035f, 0.045f, 0.075f), Stars = 1f,
                AmbientSky = new(0.01f, 0.013f, 0.026f), AmbientEquator = new(0.0035f, 0.004f, 0.007f), AmbientGround = new(0.001f, 0.001f, 0.0012f),
                Fog = new(0.025f, 0.032f, 0.05f), FogDensity = 0.005f, Reflections = 0.03f,
                Exposure = 1.6f, Saturation = -60f, Temperature = -15f, Vignette = 0.35f, Grain = 0.3f,
            },
        };

        [SerializeField] Material skyMaterial;

        Light sun;
        Material sky;
        ColorAdjustments color;
        WhiteBalance white;
        Vignette vignette;
        FilmGrain grain;
        int index;

        public string Preset => Looks[index].Name;

        void Start()
        {
            sun = GetComponent<Light>();
            sky = new Material(skyMaterial);
            RenderSettings.skybox = sky;

            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            var profile = volume.profile;
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.3f);
            bloom.scatter.Override(0.6f);
            color = profile.Add<ColorAdjustments>();
            white = profile.Add<WhiteBalance>();
            vignette = profile.Add<Vignette>();
            grain = profile.Add<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Medium1);
            Apply();
        }

        void Update()
        {
            if (Keyboard.current.nKey.wasPressedThisFrame)
            {
                index = (index + 1) % Looks.Length;
                Apply();
            }

            // ponytail: re-set every frame because the player camera spawns later; fine for a handful of cameras.
            foreach (var cam in Camera.allCameras)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }
        }

        void Apply()
        {
            var l = Looks[index];
            sun.color = l.Light;
            sun.intensity = l.Intensity;
            sun.shadows = l.Shadows ? LightShadows.Soft : LightShadows.None;
            // The light shines from the disc: yaw = azimuth + 180.
            transform.rotation = Quaternion.Euler(l.Elevation, l.Azimuth + 180f, 0f);

            sky.SetColor("_Zenith", l.Zenith);
            sky.SetColor("_Horizon", l.Horizon);
            sky.SetColor("_Ground", l.Fog);
            sky.SetColor("_Glow", l.Glow);
            sky.SetColor("_Disc", l.Disc);
            sky.SetVector("_DiscDir", -transform.forward);
            sky.SetFloat("_Stars", l.Stars);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = l.AmbientSky;
            RenderSettings.ambientEquatorColor = l.AmbientEquator;
            RenderSettings.ambientGroundColor = l.AmbientGround;
            RenderSettings.reflectionIntensity = l.Reflections;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = l.Fog;
            RenderSettings.fogDensity = l.FogDensity;

            color.postExposure.Override(l.Exposure);
            color.saturation.Override(l.Saturation);
            white.temperature.Override(l.Temperature);
            vignette.intensity.Override(l.Vignette);
            grain.intensity.Override(l.Grain);
        }

        void OnGUI() =>
            GUI.Label(new Rect(10, Screen.height - 45, 400, 20), $"Light [N]: {Preset}, fog density {Looks[index].FogDensity:F3}");
    }
}
