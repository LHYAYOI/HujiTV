using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 今のムードから次のムードへ、ライト・Volume の重み・環境光を滑らかに切り替える。シーンに 1 つ（なければ自動で作る）。
/// LightPulse が付いたライトは、揺らぎの基準の色と明るさを切り替える（揺らぎはそのまま続く）。
/// 太陽（Directional Light）はゴッドレイの光源なので、どのムードでも必ず切り替え、消さずに明るさで調整する。
/// </summary>
[DisallowMultipleComponent, AddComponentMenu("hatake/Lighting/Light Mood Director")]
public sealed class LightMoodDirector : MonoBehaviour
{
    private struct LightBlend
    {
        public Light m_light;
        public LightPulse m_pulse;
        public bool m_targetEnabledFlag;
        public Color m_fromColor;
        public Color m_toColor;
        public float m_fromIntensity;
        public float m_toIntensity;
    }

    private static LightMoodDirector s_instance;

    private readonly List<LightBlend> m_lightBlends = new List<LightBlend>();
    private readonly Dictionary<Volume, float> m_fromWeights = new Dictionary<Volume, float>();
    private Color m_fromAmbient;
    private Light m_sun;
    private LightPulse m_sunPulse;
    private Color m_fromSunColor;
    private float m_fromSunIntensity;
    private Quaternion m_fromSunRotation;
    private float m_elapsed;
    private float m_seconds;
    private bool m_blendingFlag;
    private bool m_objectsSwitchedFlag;

    public static LightMoodDirector Instance
    {
        get
        {
            if (s_instance == null)
            {
                s_instance = FindFirstObjectByType<LightMoodDirector>();
                if (s_instance == null)
                {
                    s_instance = new GameObject("LightMoodDirector").AddComponent<LightMoodDirector>();
                }
            }
            return s_instance;
        }
    }

    /// <summary>今のムード（切り替え中なら切り替え先）。</summary>
    public LightMood Current { get; private set; }
    public bool IsBlending { get { return m_blendingFlag; } }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Debug.LogWarning("LightMoodDirector はシーンに 1 つだけ置いてください。", this);
            enabled = false;
            return;
        }
        s_instance = this;
    }

    private void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }
    }

    /// <summary>ムードを切り替える。seconds が 0 ならすぐに切り替える。</summary>
    public void Request(LightMood mood, float seconds)
    {
        if (mood == null)
        {
            return;
        }
        // 切り替え途中で呼ばれたときは、途中の状態から次へ向かう
        Current = mood;
        m_seconds = Mathf.Max(0, seconds);
        m_elapsed = 0;

        m_fromWeights.Clear();
        foreach (LightMood each in LightMood.All)
        {
            if (each.Volume != null && !m_fromWeights.ContainsKey(each.Volume))
            {
                m_fromWeights.Add(each.Volume, each.Volume.weight);
            }
        }
        if (mood.Volume != null && !m_fromWeights.ContainsKey(mood.Volume))
        {
            m_fromWeights.Add(mood.Volume, mood.Volume.weight);
        }

        m_lightBlends.Clear();
        foreach (LightMood.LightSetting setting in mood.Lights)
        {
            Light light = setting.m_light;
            // 太陽は下で必ず扱うので、リストに入っていても二重には動かさない
            if (light == null || light.type == LightType.Directional)
            {
                continue;
            }
            LightPulse pulse = light.GetComponent<LightPulse>();
            bool onFlag = light.enabled && light.gameObject.activeInHierarchy;
            var blend = new LightBlend
            {
                m_light = light,
                m_pulse = pulse,
                m_targetEnabledFlag = setting.m_enabledFlag,
                m_fromColor = pulse != null && pulse.isActiveAndEnabled ? pulse.BaseColor : light.color,
                m_toColor = setting.m_color,
                m_fromIntensity = onFlag ? (pulse != null && pulse.isActiveAndEnabled ? pulse.BaseIntensity : light.intensity) : 0,
                m_toIntensity = setting.m_enabledFlag ? setting.m_intensity : 0
            };
            // 点けるライトは 0 から明るくする
            if (setting.m_enabledFlag && !onFlag)
            {
                light.gameObject.SetActive(true);
                light.enabled = true;
            }
            m_lightBlends.Add(blend);
        }

        m_fromAmbient = RenderSettings.ambientLight;

        // 太陽は消さずに、色・明るさ・向きを切り替える
        m_sun = mood.Sun;
        m_sunPulse = m_sun != null ? m_sun.GetComponent<LightPulse>() : null;
        if (m_sun != null)
        {
            bool pulseFlag = m_sunPulse != null && m_sunPulse.isActiveAndEnabled;
            m_fromSunColor = pulseFlag ? m_sunPulse.BaseColor : m_sun.color;
            m_fromSunIntensity = pulseFlag ? m_sunPulse.BaseIntensity : m_sun.intensity;
            m_fromSunRotation = m_sun.transform.rotation;
            m_sun.enabled = true;
        }
        m_objectsSwitchedFlag = false;
        m_blendingFlag = true;
        mood.InvokeEnter();
        if (m_seconds <= 0)
        {
            Step(1);
        }
    }

    private void Update()
    {
        if (!m_blendingFlag)
        {
            return;
        }
        m_elapsed += Time.deltaTime;
        Step(m_seconds > 0 ? Mathf.Clamp01(m_elapsed / m_seconds) : 1);
    }

    // 切り替えを t（0〜1）まで進める
    private void Step(float t)
    {
        float eased = Mathf.SmoothStep(0, 1, t);
        Volume target = Current != null ? Current.Volume : null;
        foreach (KeyValuePair<Volume, float> pair in m_fromWeights)
        {
            if (pair.Key != null)
            {
                pair.Key.weight = Mathf.Lerp(pair.Value, pair.Key == target ? 1 : 0, eased);
            }
        }

        foreach (LightBlend blend in m_lightBlends)
        {
            if (blend.m_light == null)
            {
                continue;
            }
            Color color = Color.Lerp(blend.m_fromColor, blend.m_toColor, eased);
            float intensity = Mathf.Lerp(blend.m_fromIntensity, blend.m_toIntensity, eased);
            if (blend.m_pulse != null && blend.m_pulse.isActiveAndEnabled)
            {
                blend.m_pulse.BaseColor = color;
                blend.m_pulse.BaseIntensity = intensity;
            }
            else
            {
                blend.m_light.color = color;
                blend.m_light.intensity = intensity;
            }
            // 消すライトは暗くなりきってから消す
            if (t >= 1 && !blend.m_targetEnabledFlag)
            {
                blend.m_light.enabled = false;
            }
        }

        if (m_sun != null && Current != null)
        {
            Color sunColor = Color.Lerp(m_fromSunColor, Current.SunColor, eased);
            float sunIntensity = Mathf.Lerp(m_fromSunIntensity, Current.SunIntensity, eased);
            if (m_sunPulse != null && m_sunPulse.isActiveAndEnabled)
            {
                m_sunPulse.BaseColor = sunColor;
                m_sunPulse.BaseIntensity = sunIntensity;
            }
            else
            {
                m_sun.color = sunColor;
                m_sun.intensity = sunIntensity;
            }
            if (Current.SunRotationFlag)
            {
                m_sun.transform.rotation = Quaternion.Slerp(m_fromSunRotation, Current.SunRotation, eased);
            }
        }

        if (Current != null && Current.OverrideAmbientFlag)
        {
            RenderSettings.ambientLight = Color.Lerp(m_fromAmbient, Current.AmbientColor, eased);
        }

        // 表示の切り替えは途中で一度だけ行う
        if (!m_objectsSwitchedFlag && t >= 0.5f && Current != null)
        {
            m_objectsSwitchedFlag = true;
            foreach (GameObject shown in Current.ShowObjects)
            {
                if (shown != null)
                {
                    shown.SetActive(true);
                }
            }
            foreach (GameObject hidden in Current.HideObjects)
            {
                if (hidden != null)
                {
                    hidden.SetActive(false);
                }
            }
        }

        if (t >= 1)
        {
            m_blendingFlag = false;
        }
    }
}
