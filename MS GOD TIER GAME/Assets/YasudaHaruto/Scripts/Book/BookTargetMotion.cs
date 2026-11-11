using UnityEngine;

namespace YasudaHaruto.BookValidation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BookTargetMotion : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float m_introDuration = 0.6f;
        [SerializeField, Min(1f)] private float m_introScale = 2f;
        [SerializeField, Min(0.1f)] private float m_rotationPeriod = 4f;
        [SerializeField, Range(0f, 1f)] private float m_rotationEasing = 0.55f;

        private Quaternion m_restRotation;
        private Vector3 m_restScale;
        private double m_startTime;

        private void OnEnable()
        {
            m_restRotation = transform.localRotation;
            m_restScale = transform.localScale;
            m_startTime = Time.timeAsDouble;
            ApplyMotion(0.0);
        }

        private void Update()
        {
            //ApplyMotion(Time.timeAsDouble - m_startTime);
        }

        private void OnDisable()
        {
            transform.localRotation = m_restRotation;
            transform.localScale = m_restScale;
        }

        private void ApplyMotion(double elapsed)
        {
            float intro = Mathf.Clamp01((float)(elapsed / m_introDuration));
            float remaining = 1f - intro;
            float scale = Mathf.Lerp(1f, m_introScale, remaining * remaining * remaining);
            transform.localScale = m_restScale * scale;

            float phase = (float)(elapsed % m_rotationPeriod / m_rotationPeriod);
            // Vary the speed continuously across the seam between revolutions.
            float easedPhase = phase - m_rotationEasing * Mathf.Sin(2f * Mathf.PI * phase) / (2f * Mathf.PI);
            transform.localRotation = m_restRotation * Quaternion.AngleAxis(-360f * easedPhase, Vector3.forward);
        }
    }
}
