#if UNITY_EDITOR_WIN
using UnityEngine;

namespace Laboratory.ReplayCapture
{
    [RequireComponent(typeof(AudioListener))]
    [AddComponentMenu("")]
    public sealed class ReplayListenerTap : MonoBehaviour
    {
        internal volatile ReplayAudioRing Ring;
        private void OnAudioFilterRead(float[] data, int channels)
        {
            Ring?.Write(data, channels, AudioSettings.dspTime);
        }
    }
}
#endif
