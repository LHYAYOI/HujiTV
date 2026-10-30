using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// エリアの「雰囲気」。ライトの色・明るさ・オンオフ、太陽（Directional Light）、霧や距離フォグなどの Volume、環境光、表示する物をまとめて持つ。
/// エリアの箱にカメラが入ったとき、LightMoodTrigger の範囲に入ったとき、または Apply() を呼んだときに、LightMoodDirector が滑らかに切り替える。
/// 太陽はゴッドレイの光源なので、どのムードでも必ず値を持ち、消さずに色・明るさ・向きを切り替える。
/// </summary>
[DisallowMultipleComponent, AddComponentMenu("hatake/Lighting/Light Mood")]
public sealed class LightMood : MonoBehaviour
{
    [Serializable]
    public struct LightSetting
    {
        public Light m_light;
        [Tooltip("このムードで点けるか。消す場合は明るさを 0 へ下げてから消す。")]
        public bool m_enabledFlag;
        public Color m_color;
        [Min(0)]
        public float m_intensity;
    }

    [Tooltip("このムードの霧・距離フォグ・ポストエフェクト（Global Volume）。ムードに切り替わると重みが 1 になり、ほかのムードの Volume は 0 になる。")]
    [SerializeField] private Volume m_volume;

    [Header("エリア")]
    [Tooltip("エリアの箱の大きさ（このオブジェクトのローカル、Scale も掛かる）。ライトの自動取り込みと、カメラが入ったときの切り替えに使う。")]
    [SerializeField] private Vector3 m_areaSize = new Vector3(10, 6, 20);
    [Tooltip("カメラ（トロッコ）がエリアに入ったら、このムードへ切り替える。")]
    [SerializeField] private bool m_enterByAreaFlag = true;
    [Tooltip("エリアに入ったかを調べる対象。未指定ならメインカメラ。")]
    [SerializeField] private Transform m_areaTarget;

    [Header("太陽・月（Directional Light）")]
    [Tooltip("切り替える Directional Light。未指定なら Lighting の Sun Source、なければシーンの最初の Directional Light。")]
    [SerializeField] private Light m_sun;
    [SerializeField] private Color m_sunColor = new Color(0.6f, 0.7f, 1f);
    [Min(0)]
    [SerializeField] private float m_sunIntensity = 0.3f;
    [Tooltip("向きも切り替える（窓から差す月明かりの角度を変えるなど）。")]
    [SerializeField] private bool m_sunRotationFlag;
    [SerializeField] private Vector3 m_sunEuler = new Vector3(40, -30, 0);

    [Header("ライト（エリア内のライトは右クリックの「エリア内のライトを全部取り込む」で自動で入る）")]
    [SerializeField] private LightSetting[] m_lights = new LightSetting[0];

    [Header("環境光")]
    [Tooltip("環境光（Lighting の Environment）の色も切り替える。Source が Color のときに効く。")]
    [SerializeField] private bool m_overrideAmbientFlag;
    [SerializeField, ColorUsage(false, true)] private Color m_ambientColor = new Color(0.05f, 0.05f, 0.08f);

    [Header("表示の切り替え（切り替えの途中で行う）")]
    [SerializeField] private GameObject[] m_showObjects = new GameObject[0];
    [SerializeField] private GameObject[] m_hideObjects = new GameObject[0];

    [Header("その他")]
    [Min(0), Tooltip("エリアに入ったときや Apply() で切り替えるときの秒数。")]
    [SerializeField] private float m_blendSeconds = 2;
    [Tooltip("ゲーム開始時のムードにする（ひとつだけオンにする）。")]
    [SerializeField] private bool m_initialMoodFlag;
    [SerializeField] private UnityEvent m_onEnter = new UnityEvent();

    private static readonly List<LightMood> s_all = new List<LightMood>();
    private bool m_insideFlag;

    public static IReadOnlyList<LightMood> All { get { return s_all; } }
    public Volume Volume { get { return m_volume; } }
    public LightSetting[] Lights { get { return m_lights; } }
    public Light Sun { get { return m_sun != null ? m_sun : FindSun(); } }
    public Color SunColor { get { return m_sunColor; } }
    public float SunIntensity { get { return m_sunIntensity; } }
    public bool SunRotationFlag { get { return m_sunRotationFlag; } }
    public Quaternion SunRotation { get { return Quaternion.Euler(m_sunEuler); } }
    public bool OverrideAmbientFlag { get { return m_overrideAmbientFlag; } }
    public Color AmbientColor { get { return m_ambientColor; } }
    public GameObject[] ShowObjects { get { return m_showObjects; } }
    public GameObject[] HideObjects { get { return m_hideObjects; } }

    /// <summary>シーンの太陽。Lighting の Sun Source、なければ最初に見つかった有効な Directional Light。</summary>
    public static Light FindSun()
    {
        if (RenderSettings.sun != null)
        {
            return RenderSettings.sun;
        }
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional)
            {
                return light;
            }
        }
        return null;
    }

    private void Reset()
    {
        m_volume = GetComponent<Volume>();
    }

    private void Awake()
    {
        // 開始時は、開始時のムードの Volume だけを効かせる
        if (m_volume != null)
        {
            m_volume.isGlobal = true;
            m_volume.weight = m_initialMoodFlag ? 1 : 0;
        }
    }

    private void Start()
    {
        if (m_initialMoodFlag)
        {
            LightMoodDirector.Instance.Request(this, 0);
        }
    }

    private void OnEnable()
    {
        if (!s_all.Contains(this))
        {
            s_all.Add(this);
        }
    }

    private void OnDisable()
    {
        s_all.Remove(this);
    }

    private void Update()
    {
        if (!m_enterByAreaFlag)
        {
            return;
        }
        Transform target = m_areaTarget;
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }
        if (target == null)
        {
            return;
        }
        bool insideFlag = Contains(target.position);
        // 入った瞬間に、まだこのムードでなければ切り替える
        if (insideFlag && !m_insideFlag && LightMoodDirector.Instance.Current != this)
        {
            Apply();
        }
        m_insideFlag = insideFlag;
    }

    /// <summary>ワールド座標の点がエリアの箱の中か。</summary>
    public bool Contains(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        return Mathf.Abs(local.x) <= m_areaSize.x * 0.5f && Mathf.Abs(local.y) <= m_areaSize.y * 0.5f && Mathf.Abs(local.z) <= m_areaSize.z * 0.5f;
    }

    /// <summary>このムードへ切り替える（UnityEvent からも呼べる）。</summary>
    [ContextMenu("このムードにする")]
    public void Apply()
    {
        LightMoodDirector.Instance.Request(this, m_blendSeconds);
    }

    /// <summary>秒数を指定してこのムードへ切り替える。</summary>
    public void Apply(float seconds)
    {
        LightMoodDirector.Instance.Request(this, seconds);
    }

    internal void InvokeEnter()
    {
        m_onEnter.Invoke();
    }

    /// <summary>エリアの箱の中にあるライト（Directional 以外、非表示のものも含む）を全部取り込み、今の値を記録する。</summary>
    [ContextMenu("エリア内のライトを全部取り込む")]
    public void CollectLightsInArea()
    {
        RecordUndo("エリア内のライトを取り込む");
        var settings = new List<LightSetting>();
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional || !light.gameObject.scene.IsValid() || !Contains(light.transform.position))
            {
                continue;
            }
            settings.Add(CaptureSetting(light));
        }
        // 階層の順に並べて見つけやすくする
        settings.Sort((a, b) => string.CompareOrdinal(HierarchyPath(a.m_light.transform), HierarchyPath(b.m_light.transform)));
        m_lights = settings.ToArray();
        CaptureSun();
        SetDirty();
        Debug.Log($"{name}: エリア内のライトを {m_lights.Length} 個取り込みました。", this);
    }

    /// <summary>リストにあるライトと太陽の、今の値を記録する。</summary>
    [ContextMenu("今のライトの値を取り込む")]
    public void CaptureCurrentValues()
    {
        RecordUndo("ライトの値を取り込む");
        for (int i = 0; i < m_lights.Length; i++)
        {
            if (m_lights[i].m_light != null)
            {
                m_lights[i] = CaptureSetting(m_lights[i].m_light);
            }
        }
        CaptureSun();
        SetDirty();
    }

    private static LightSetting CaptureSetting(Light light)
    {
        // LightPulse が付いていれば、揺らぎの基準の値を記録する
        LightPulse pulse = light.GetComponent<LightPulse>();
        bool useBaseFlag = Application.isPlaying && pulse != null && pulse.isActiveAndEnabled;
        return new LightSetting
        {
            m_light = light,
            m_enabledFlag = light.enabled && light.gameObject.activeInHierarchy,
            m_color = useBaseFlag ? pulse.BaseColor : light.color,
            m_intensity = useBaseFlag ? pulse.BaseIntensity : light.intensity
        };
    }

    private void CaptureSun()
    {
        Light sun = Sun;
        if (sun == null)
        {
            return;
        }
        m_sunColor = sun.color;
        m_sunIntensity = sun.intensity;
        m_sunEuler = sun.transform.rotation.eulerAngles;
    }

    private static string HierarchyPath(Transform target)
    {
        return target.parent != null ? HierarchyPath(target.parent) + "/" + target.name : target.name;
    }

    private void RecordUndo(string label)
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, label);
#endif
    }

    private void SetDirty()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void OnDrawGizmosSelected()
    {
        // エリアの箱と、取り込んだライトへの線
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.08f);
        Gizmos.DrawCube(Vector3.zero, m_areaSize);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, m_areaSize);
        Gizmos.matrix = Matrix4x4.identity;
        foreach (LightSetting setting in m_lights)
        {
            if (setting.m_light != null)
            {
                Gizmos.color = setting.m_enabledFlag ? setting.m_color : Color.gray;
                Gizmos.DrawLine(transform.position, setting.m_light.transform.position);
            }
        }
    }
}
