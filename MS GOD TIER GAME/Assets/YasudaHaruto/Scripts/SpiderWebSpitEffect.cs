using UnityEngine;
using UnityEngine.Rendering;

namespace PhysicalMonster.YasudaHaruto
{
    public sealed class SpiderWebSpitEffect : MonoBehaviour
    {
        private const int StrandCount = 32;
        private const int PointCount = 18;
        [SerializeField]
        private Shader m_strandShader;
        [SerializeField]
        private bool m_playOnEnable = true;
        [SerializeField]
        private bool m_destroyOnFinish = true;
        [SerializeField, Min(0.1f)]
        private float m_distance = 8f;
        [SerializeField, Min(0f)]
        private float m_spread = 2.1f;
        [SerializeField, Min(0.05f)]
        private float m_travelTime = 0.26f;
        [SerializeField, Min(0.05f)]
        private float m_fadeTime = 0.36f;
        [SerializeField]
        private Color m_color = new Color(0.3f, 0.88f, 0.76f, 0.78f);
        private readonly LineRenderer[] m_strands = new LineRenderer[StrandCount];
        private readonly Vector3[] m_points = new Vector3[PointCount];
        private Material m_material;
        private float m_elapsed;
        private bool m_playing;

        private void OnEnable()
        {
            if (m_playOnEnable)
                Play();
        }


        // Forward (+Z) points from the mouth toward the attack.
        public void Play()
        {
            if (!isActiveAndEnabled || m_strandShader == null)
                return;
            if (m_material == null)
                CreateStrands();
            m_elapsed = 0f;
            m_playing = true;
            Draw();
        }


        private void LateUpdate()
        {
            if (!m_playing)
                return;
            m_elapsed += Time.deltaTime;
            if (m_elapsed >= m_travelTime + m_fadeTime)
            {
                Hide();
                if (m_destroyOnFinish)
                    Destroy(gameObject);
                return;
            }

            Draw();
        }


        private void CreateStrands()
        {
            m_material = new Material(m_strandShader)
            {
                hideFlags = HideFlags.DontSave
            };
            for (int index = 0; index < StrandCount; index++)
            {
                var strandObject = new GameObject("Silk Strand");
                strandObject.layer = gameObject.layer;
                strandObject.transform.SetParent(transform, false);
                var strand = strandObject.AddComponent<LineRenderer>();
                strand.useWorldSpace = false;
                strand.sharedMaterial = m_material;
                strand.shadowCastingMode = ShadowCastingMode.Off;
                strand.receiveShadows = false;
                strand.positionCount = PointCount;
                strand.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.12f);
                strand.widthMultiplier = Mathf.Lerp(0.012f, 0.065f, Sample(index, 19));
                m_strands[index] = strand;
            }
        }


        private void Draw()
        {
            float fade = Mathf.Clamp01((m_elapsed - m_travelTime) / m_fadeTime);
            for (int index = 0; index < StrandCount; index++)
            {
                float delay = Sample(index, 3) * m_travelTime * 0.35f;
                float head = Mathf.Clamp01((m_elapsed - delay) / (m_travelTime - delay));
                float tail = fade * fade;
                LineRenderer strand = m_strands[index];
                strand.enabled = head > tail;
                if (!strand.enabled)
                    continue;
                float angle = Sample(index, 7) * 2f * Mathf.PI;
                float radius = Mathf.Sqrt(Sample(index, 11)) * m_spread;
                var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.48f, 0f) * radius;
                float reach = m_distance * Mathf.Lerp(0.72f, 1f, Sample(index, 23));
                float phase = angle + Sample(index, 31) * 4f;
                for (int point = 0; point < PointCount; point++)
                {
                    float t = Mathf.Lerp(tail, head, point / (PointCount - 1f));
                    float wave = t * radius * 0.24f;
                    float ripple = t * 15f - m_elapsed * 19f + phase;
                    m_points[point] = offset * (t * t) + new Vector3(Mathf.Sin(ripple) * wave, Mathf.Cos(ripple * 0.77f) * wave - t * t * 0.24f, reach * t);
                }

                strand.SetPositions(m_points);
                Color tint = Color.Lerp(m_color, Color.white, Sample(index, 37) * 0.32f);
                tint.a = m_color.a * (1f - fade) * Mathf.Lerp(0.4f, 1f, Sample(index, 41));
                strand.startColor = tint;
                tint.a *= 0.5f;
                strand.endColor = tint;
            }
        }


        private static float Sample(int index, int seed)
        {
            return Mathf.Repeat(Mathf.Sin(index * 71.17f + seed * 19.71f) * 4375.29f, 1f);
        }


        private void OnDisable()
        {
            Hide();
        }


        private void Hide()
        {
            m_playing = false;
            foreach (LineRenderer strand in m_strands)
            {
                if (strand != null)
                    strand.enabled = false;
            }
        }


        private void OnDestroy()
        {
            if (m_material != null)
                Destroy(m_material);
        }
    }
}
