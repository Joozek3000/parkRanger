using UnityEngine;

namespace ParkRangers.Spikes.S06
{
    /// <summary>
    /// Landmark lamps and glowing parts: on at dusk and night, off in the afternoon. The radio mast beacon blinks
    /// 1 s on / 1 s off. Built by "Park Rangers/S06/Build landmark lights". Local only, like MapLighting.
    /// </summary>
    public class S06LandmarkLights : MonoBehaviour
    {
        [SerializeField] Light[] lights;
        [SerializeField] GameObject[] glows;
        [SerializeField] Light beaconLight;
        [SerializeField] GameObject beaconGlow;

        MapLighting lighting;

        public void Init(Light[] lights, GameObject[] glows, Light beaconLight, GameObject beaconGlow) =>
            (this.lights, this.glows, this.beaconLight, this.beaconGlow) = (lights, glows, beaconLight, beaconGlow);

        void Start() => lighting = FindAnyObjectByType<MapLighting>();

        void Update()
        {
            var dark = lighting.Preset != "afternoon";
            foreach (var l in lights) l.enabled = dark;
            foreach (var g in glows) g.SetActive(dark);
            var blink = dark && Time.time % 2f < 1f;
            beaconLight.enabled = blink;
            beaconGlow.SetActive(blink);
        }
    }
}
