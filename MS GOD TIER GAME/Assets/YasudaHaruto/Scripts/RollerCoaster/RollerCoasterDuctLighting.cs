using UnityEngine;

namespace YasudaHaruto.RollerCoaster
{
    /// <summary>出発時は明るく、走行開始後にダクトの暗さへ移行する。再試走と終了時に復帰。</summary>
    public sealed class RollerCoasterDuctLighting : MonoBehaviour
    {
        [SerializeField] private RollerCoasterTrialRide m_ride;
        [SerializeField, Min(0f)] private float m_brightSeconds = 2f;
        [SerializeField, Min(.1f)] private float m_fadeSeconds = 4f;
        [SerializeField] private Color m_startAmbient = new Color(.32f, .34f, .38f);
        [SerializeField] private Color m_darkAmbient = new Color(.065f, .075f, .09f);
        [SerializeField, Range(0f, .1f)] private float m_darkFog = .028f;
        [SerializeField] private Light m_nearFillLight;
        private bool m_originalFillEnabled;
        private Color m_originalAmbient;
        private float m_originalFog, m_elapsed;
        private bool m_originalFogEnabled, m_active;

        public float Darkness
        {
            get; private set;
        }

        private void OnEnable()
        {
            m_originalAmbient = RenderSettings.ambientLight;
            m_originalFog = RenderSettings.fogDensity;
            m_originalFogEnabled = RenderSettings.fog;
            m_originalFillEnabled = m_nearFillLight != null && m_nearFillLight.enabled;
        }

        public void RestartLighting()
        {
            m_elapsed = 0;
            m_active = true;
            if (m_nearFillLight != null)
                m_nearFillLight.enabled = true;
            Apply(0f);
        }

        private void Update()
        {
            if (!m_active || m_ride == null)
                return;
            if (!m_ride.Paused && !m_ride.Finished)
                m_elapsed += Time.deltaTime;
            Apply(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(m_brightSeconds, m_brightSeconds + m_fadeSeconds, m_elapsed)));
        }

        private void Apply(float amount)
        {
            Darkness = amount;
            RenderSettings.ambientLight = Color.Lerp(m_startAmbient, m_darkAmbient, amount);
            RenderSettings.fog = true;
            RenderSettings.fogDensity = Mathf.Lerp(0f, m_darkFog, amount);
        }

        private void OnDisable()
        {
            if (!m_active)
                return;
            m_active = false;
            RenderSettings.ambientLight = m_originalAmbient;
            RenderSettings.fogDensity = m_originalFog;
            RenderSettings.fog = m_originalFogEnabled;
            if (m_nearFillLight != null)
                m_nearFillLight.enabled = m_originalFillEnabled;
        }

#if UNITY_EDITOR
        public void Configure(RollerCoasterTrialRide ride)
        {
            m_ride = ride;
        }
#endif
    }
}
