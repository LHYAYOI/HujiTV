using UnityEngine;

/// <summary>
/// 光だまりをゆっくり脈打たせたり、色を回したりする（こわかわいいハロウィン感）。
/// 基準の色と明るさ（BaseColor / BaseIntensity）に揺らぎを掛ける。LightMoodDirector は基準のほうを切り替えるので、両方を同時に使える。
/// ランプの笠や燭台の火など、光っている物の発光（_EmissionColor）も同じ揺らぎで動かせる。
/// </summary>
[RequireComponent(typeof(Light)), DisallowMultipleComponent, AddComponentMenu("hatake/Lighting/Light Pulse")]
public sealed class LightPulse : MonoBehaviour
{
    public enum PULSE_SHAPE
    {
        // なめらかな波
        SINE = 0,
        // 息をするように、明るい時間が短い
        BREATH = 1,
        // 不規則にゆらぐ（ろうそく）
        FLICKER = 2,
        // 明るさは変えない（色だけ回す）
        NONE = 3
    }

    [Header("明るさの揺らぎ")]
    [SerializeField] private PULSE_SHAPE m_shape = PULSE_SHAPE.BREATH;
    [Range(0, 1), Tooltip("暗くなる幅（基準の明るさに対する割合）。")]
    [SerializeField] private float m_intensityAmount = 0.35f;
    [Min(0.05f), Tooltip("1 周期の秒数。FLICKER では揺らぎの細かさ。")]
    [SerializeField] private float m_periodSeconds = 3;

    [Header("色を回す")]
    [SerializeField] private bool m_colorCycleFlag;
    [Tooltip("回す色。左から右へ進み、右端から左端へ戻る。")]
    [SerializeField] private Gradient m_colorGradient = DefaultGradient();
    [Min(0.1f), Tooltip("ひと回りの秒数。")]
    [SerializeField] private float m_colorCycleSeconds = 12;
    [Range(0, 1), Tooltip("0 = 基準の色のまま、1 = 回す色そのもの。")]
    [SerializeField] private float m_colorAmount = 1;

    [Header("その他")]
    [Tooltip("ライトごとに揺らぎの位置をずらす（同じ設定のライトが揃って点滅しない）。")]
    [SerializeField] private bool m_randomPhaseFlag = true;
    [Tooltip("同じ揺らぎで発光させるレンダラー（マテリアルの _EmissionColor を上書きする）。")]
    [SerializeField] private Renderer[] m_emissionRenderers = new Renderer[0];
    [Min(0), Tooltip("発光の強さ。")]
    [SerializeField] private float m_emissionIntensity = 2;

    private static readonly int g_emissionId = Shader.PropertyToID("_EmissionColor");

    private Light m_light;
    private float m_phase;
    private MaterialPropertyBlock m_block;
    private bool m_capturedFlag;
    private float m_baseIntensity;
    private Color m_baseColor;

    /// <summary>揺らぎの基準の明るさ。</summary>
    public float BaseIntensity
    {
        get { Capture(); return m_baseIntensity; }
        set { Capture(); m_baseIntensity = value; }
    }

    /// <summary>揺らぎの基準の色。</summary>
    public Color BaseColor
    {
        get { Capture(); return m_baseColor; }
        set { Capture(); m_baseColor = value; }
    }

    private void Awake()
    {
        Capture();
        m_phase = m_randomPhaseFlag ? Random.value * 100 : 0;
    }

    // 最初に、ライトの今の値を基準として覚える
    private void Capture()
    {
        if (m_capturedFlag)
        {
            return;
        }
        m_light = GetComponent<Light>();
        m_baseIntensity = m_light.intensity;
        m_baseColor = m_light.color;
        m_capturedFlag = true;
    }

    private void OnDisable()
    {
        // 止めたら基準の値に戻す
        if (m_capturedFlag && m_light != null)
        {
            m_light.intensity = m_baseIntensity;
            m_light.color = m_baseColor;
        }
    }

    private void Update()
    {
        float time = Time.time + m_phase;
        float factor = Mathf.Lerp(1 - m_intensityAmount, 1, Wave(time));
        Color color = m_baseColor;
        if (m_colorCycleFlag)
        {
            Color cycled = m_colorGradient.Evaluate(Mathf.Repeat(time / m_colorCycleSeconds, 1));
            color = Color.Lerp(m_baseColor, cycled, m_colorAmount);
        }
        m_light.color = color;
        m_light.intensity = m_baseIntensity * factor;
        ApplyEmission(color, factor);
    }

    // 揺らぎの形（0〜1）
    private float Wave(float time)
    {
        float cycle = time / m_periodSeconds;
        float sine = 0.5f + 0.5f * Mathf.Sin(cycle * Mathf.PI * 2);
        switch (m_shape)
        {
            case PULSE_SHAPE.SINE:
                return sine;
            case PULSE_SHAPE.BREATH:
                return sine * sine;
            case PULSE_SHAPE.FLICKER:
                // 2 つの速さのノイズを重ねて、ろうそくらしくする
                float slow = Mathf.PerlinNoise(cycle * 2.0f, m_phase);
                float fast = Mathf.PerlinNoise(cycle * 9.0f, m_phase + 31.7f);
                return Mathf.Clamp01(slow * 0.7f + fast * 0.5f - 0.1f);
            case PULSE_SHAPE.NONE:
                return 1;
        }
        return 1;
    }

    private void ApplyEmission(Color color, float factor)
    {
        if (m_emissionRenderers.Length == 0)
        {
            return;
        }
        if (m_block == null)
        {
            m_block = new MaterialPropertyBlock();
        }
        Color emission = color.linear * (m_emissionIntensity * factor);
        foreach (Renderer target in m_emissionRenderers)
        {
            if (target == null)
            {
                continue;
            }
            target.GetPropertyBlock(m_block);
            m_block.SetColor(g_emissionId, emission);
            target.SetPropertyBlock(m_block);
        }
    }

    // 紫 → 橙 → 黄緑 → 青 → 紫 のハロウィン配色
    private static Gradient DefaultGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.62f, 0.28f, 1f), 0),
                new GradientColorKey(new Color(1f, 0.5f, 0.12f), 0.3f),
                new GradientColorKey(new Color(0.55f, 1f, 0.3f), 0.55f),
                new GradientColorKey(new Color(0.3f, 0.45f, 1f), 0.8f),
                new GradientColorKey(new Color(0.62f, 0.28f, 1f), 1)
            },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
        return gradient;
    }
}
