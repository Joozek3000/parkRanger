using UnityEngine;

namespace ParkRangers.Spikes.S05
{
    /// <summary>
    /// RMS of the audio passing this point of the GameObject's filter chain, averaged since the last Take().
    /// On a 3D AudioSource this is the signal before distance attenuation.
    /// </summary>
    public class S05RmsMeter : MonoBehaviour
    {
        readonly object gate = new();
        double sum;
        int count;

        void OnAudioFilterRead(float[] data, int channels)
        {
            double s = 0;
            foreach (var v in data) s += v * v;
            lock (gate) { sum += s; count += data.Length; }
        }

        public float Take()
        {
            lock (gate)
            {
                var rms = count > 0 ? Mathf.Sqrt((float)(sum / count)) : 0f;
                sum = 0;
                count = 0;
                return rms;
            }
        }
    }
}
