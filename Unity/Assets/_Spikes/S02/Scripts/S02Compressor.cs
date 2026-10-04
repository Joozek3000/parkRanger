using UnityEngine;

namespace ParkRangers.Spikes.S02
{
    /// <summary>
    /// Feed-forward peak compressor on the audio thread, then a hard clip at ±1.
    /// Fields are written from the main thread each frame; torn reads only cost one block.
    /// </summary>
    public class S02Compressor : MonoBehaviour
    {
        public float threshold = 0.1f;
        public float ratio = 6f;
        public float makeup = 3f;

        float envelope;

        void OnAudioFilterRead(float[] data, int channels)
        {
            const float attack = 0.01f, release = 0.0005f; // per-sample smoothing, ~2 ms / ~40 ms at 48 kHz
            for (var i = 0; i < data.Length; i += channels)
            {
                var peak = 0f;
                for (var c = 0; c < channels; c++) peak = Mathf.Max(peak, Mathf.Abs(data[i + c]));
                envelope += (peak - envelope) * (peak > envelope ? attack : release);
                var gain = envelope > threshold ? Mathf.Pow(envelope / threshold, 1f / ratio - 1f) : 1f;
                for (var c = 0; c < channels; c++) data[i + c] = Mathf.Clamp(data[i + c] * gain * makeup, -1f, 1f);
            }
        }
    }
}
