using UnityEngine;

namespace ParkRangers.Spikes.S02
{
    /// <summary>
    /// Gain at the end of a radio voice chain. AudioSource.volume applies before the filters, so the compressor
    /// would make up any signal-strength drop set there; this stage comes after it.
    /// </summary>
    public class S02Gain : MonoBehaviour
    {
        public float gain = 1f;

        void OnAudioFilterRead(float[] data, int channels)
        {
            var g = gain;
            for (var i = 0; i < data.Length; i++) data[i] *= g;
        }
    }
}
