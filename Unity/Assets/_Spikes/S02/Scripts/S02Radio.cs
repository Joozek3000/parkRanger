using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ParkRangers.Spikes.S02
{
    /// <summary>
    /// A radio in the world (held by a player, or the station set). Receive-only on its own: S02RadioSystem calls
    /// Receive() each frame for every voice that reaches it, on every machine. Each incoming voice plays from a
    /// 3D AudioSource here, so anyone near the radio hears it. Chain per voice: compressor, high-pass, low-pass,
    /// distortion. Static is a second 3D source looping a noise clip through the same band filters, level from signal and anomalies.
    /// </summary>
    public class S02Radio : MonoBehaviour
    {
        static AudioClip noiseClip;

        readonly Dictionary<ulong, AudioSource> voices = new();
        readonly Dictionary<ulong, float> signals = new();
        readonly Dictionary<ulong, float> dropoutUntil = new();
        readonly HashSet<ulong> seen = new();
        S02RadioSystem system;
        AudioSource noise;
        Renderer body;

        public S02PlayerRadio User { get; private set; }
        public bool Powered => User == null || !User.IsSpawned || User.Battery.Value > 0f;
        public bool Transmitting => User != null && User.IsSpawned && User.Transmitting.Value;
        public float AnomalyStatic { get; set; }
        /// <summary>Talkers heard this frame with signal above 0, and their signal.</summary>
        public IEnumerable<(ulong talker, float signal)> Heard => seen.Where(t => signals[t] > 0f).Select(t => (t, signals[t]));

        void Awake()
        {
            User = GetComponentInParent<S02PlayerRadio>();
            system = FindAnyObjectByType<S02RadioSystem>();
            body = GetComponentInChildren<Renderer>();
            noise = MakeSource("Static");
            noise.gameObject.AddComponent<AudioHighPassFilter>();
            noise.gameObject.AddComponent<AudioLowPassFilter>();
            noise.clip = NoiseClip();
            noise.loop = true;
            noise.volume = 0f;
            noise.Play();
        }

        public void Receive(ulong talker, AudioSource voice, float signal)
        {
            if (!voices.TryGetValue(talker, out var src))
            {
                src = MakeSource($"Voice c{talker}");
                src.gameObject.AddComponent<S02Compressor>();
                src.gameObject.AddComponent<AudioHighPassFilter>();
                src.gameObject.AddComponent<AudioLowPassFilter>();
                src.gameObject.AddComponent<AudioDistortionFilter>();
                voices[talker] = src;
            }
            S02Voice.Mirror(voice, src);
            signals[talker] = signal;
            seen.Add(talker);
        }

        AudioSource MakeSource(string sourceName)
        {
            var go = new GameObject(sourceName, typeof(AudioSource));
            go.transform.SetParent(transform, false);
            var src = go.GetComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 0.5f;
            return src;
        }

        void LateUpdate()
        {
            var s = system;
            var rxStatic = 0f;
            foreach (var (talker, src) in voices)
            {
                src.maxDistance = s.radioMaxDistance;
                if (!seen.Contains(talker) || !Powered)
                {
                    if (src.isPlaying) src.Stop();
                    continue;
                }
                var signal = signals[talker];
                var gain = signal > 0f ? Mathf.Lerp(s.weakVoiceGain, 1f, signal) : 0f;
                if (signal > 0f && signal < s.dropoutBelow)
                {
                    var until = dropoutUntil.GetValueOrDefault(talker);
                    if (Time.time > until && Random.value < s.dropoutsPerSecond * (1f - signal / s.dropoutBelow) * Time.deltaTime)
                        dropoutUntil[talker] = Time.time + Random.Range(s.dropoutLength.x, s.dropoutLength.y);
                    if (Time.time < dropoutUntil.GetValueOrDefault(talker)) gain = 0f;
                }
                src.volume = gain * s.radioVolume;
                if (signal > 0f) rxStatic = Mathf.Max(rxStatic, gain == 0f ? s.staticMax : Mathf.Lerp(s.staticMax, s.staticFloor, signal));

                var comp = src.GetComponent<S02Compressor>();
                comp.threshold = s.compThreshold;
                comp.ratio = s.compRatio;
                comp.makeup = s.compMakeup;
                src.GetComponent<AudioHighPassFilter>().cutoffFrequency = s.highPass;
                src.GetComponent<AudioLowPassFilter>().cutoffFrequency = s.lowPass;
                src.GetComponent<AudioDistortionFilter>().distortionLevel = s.distortion;
            }
            noise.maxDistance = s.radioMaxDistance;
            noise.GetComponent<AudioHighPassFilter>().cutoffFrequency = s.highPass;
            noise.GetComponent<AudioLowPassFilter>().cutoffFrequency = s.lowPass;
            noise.volume = Powered ? Mathf.Max(rxStatic, AnomalyStatic) * s.radioVolume : 0f;
            if (body != null) body.material.color = !Powered ? Color.black : Transmitting ? Color.red : Heard.Any() ? Color.green : Color.gray;
            seen.Clear();
        }

        public string Diag()
        {
            var text = $"{name} pow {Powered} static {noise.volume:F2} rms {Rms(noise):F3}";
            foreach (var (talker, src) in voices)
                text += $" | c{talker} sig {signals.GetValueOrDefault(talker):F2} {(src.isPlaying ? "playing" : "stopped")} vol {src.volume:F2} rms {Rms(src):F3}";
            return text;
        }

        // Source output (after its filters, before distance attenuation), last 1024 samples.
        static float Rms(AudioSource src)
        {
            var buf = new float[1024];
            src.GetOutputData(buf, 0);
            var sum = 0f;
            foreach (var v in buf) sum += v * v;
            return Mathf.Sqrt(sum / buf.Length);
        }

        // 2 s of hiss with crackle bursts, looped; the radio band filters shape it like the voice.
        static AudioClip NoiseClip()
        {
            if (noiseClip != null) return noiseClip;
            const int rate = 48000, len = rate * 2;
            var data = new float[len];
            var rng = new System.Random(2);
            var burst = 0f;
            for (var i = 0; i < len; i++)
            {
                if (rng.NextDouble() < 8.0 / rate) burst = 1f; // ~8 crackles per second
                burst *= 0.9995f;
                data[i] = (float)(rng.NextDouble() * 2 - 1) * (0.25f + 0.75f * burst);
            }
            noiseClip = AudioClip.Create("S02Static", len, 1, rate, false);
            noiseClip.SetData(data, 0);
            return noiseClip;
        }
    }
}
