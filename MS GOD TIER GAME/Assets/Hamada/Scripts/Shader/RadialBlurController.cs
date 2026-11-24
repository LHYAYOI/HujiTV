using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class RadialBlurController : MonoBehaviour
{
    [Header("Blur Settings")]

    [SerializeField]
    private Vector2 m_center = new Vector2(0.5f, 0.5f);

    [SerializeField]
    private AnimationCurve m_intensityCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.2f, 1f),
        new Keyframe(1f, 0f)
    );

    public static RadialBlurController Instance;

    private Coroutine m_coroutine;

    public float Intensity { get; private set; }

    public Vector2 Center => m_center;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.J)) 
        {
            Play(10, .3f);
        }
    }

    public void Play(float duration,float maxStrength)
    {
        if (m_coroutine != null)
        {
            StopCoroutine(m_coroutine);
        }

        m_coroutine = StartCoroutine(BlurRoutine(duration, maxStrength));
    }

    private IEnumerator BlurRoutine(float duration, float maxStrength)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float rate = Mathf.Clamp01(elapsedTime / duration);

            Intensity = maxStrength * Mathf.Max(0f, m_intensityCurve.Evaluate(rate));

            yield return null;
        }

        Intensity = 0f;
        m_coroutine = null;
    }

    private void OnEnable()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (m_coroutine != null)
        {
            StopCoroutine(m_coroutine);
        }

        m_coroutine = null;
        Intensity = 0f;
    }
}
