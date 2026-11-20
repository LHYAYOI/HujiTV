using System.Collections;
using UnityEngine;

public class RadialBlurController : MonoBehaviour
{
    [Header("Blur Settings")]
    [SerializeField, Min(0.01f)]
    private float m_duration = 0.8f;

    [SerializeField, Range(0f, 0.15f)]
    private float m_maxStrength = 0.06f;

    [SerializeField]
    private Vector2 m_center = new Vector2(0.5f, 0.5f);

    [SerializeField]
    private AnimationCurve m_intensityCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 1f),
        new Keyframe(1f, 0f)
    );

    private Coroutine m_coroutine;

    public float Intensity { get; private set; }

    public Vector2 Center => m_center;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.J)) 
        {
            Play();
        }
    }

    public void Play()
    {
        if (m_coroutine != null)
        {
            StopCoroutine(m_coroutine);
        }

        m_coroutine = StartCoroutine(BlurRoutine());
    }

    private IEnumerator BlurRoutine()
    {
        float elapsedTime = 0f;

        while (elapsedTime < m_duration)
        {
            elapsedTime += Time.deltaTime;

            float rate = Mathf.Clamp01(elapsedTime / m_duration);

            Intensity =
                m_maxStrength *
                Mathf.Max(0f, m_intensityCurve.Evaluate(rate));

            yield return null;
        }

        Intensity = 0f;
        m_coroutine = null;
    }

    private void OnDisable()
    {
        if (m_coroutine != null)
        {
            StopCoroutine(m_coroutine);
        }

        m_coroutine = null;
        Intensity = 0f;
    }
}
