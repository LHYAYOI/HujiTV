using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// オバケ（GhostThreat）が近いほど、画面の縁を暗くし、心拍のように脈打たせる。
/// 専用の Global Volume を実行時に作って URP の Vignette を動かす（シーンの Volume は書き換えない）。
/// 演出から緊張を上げたいときは SetManualThreat を呼ぶ。
/// </summary>
[DisallowMultipleComponent, AddComponentMenu("hatake/Lighting/Heartbeat Vignette")]
public sealed class HeartbeatVignette : MonoBehaviour
{
    [Tooltip("距離を測る位置。未指定ならメインカメラ。")]
    [SerializeField] private Transform m_listener;

    [Header("ビネット")]
    [Range(0, 1), Tooltip("落ち着いているときの縁の暗さ。")]
    [SerializeField] private float m_calmIntensity = 0.2f;
    [Range(0, 1), Tooltip("一番緊張したときの縁の暗さ。")]
    [SerializeField] private float m_panicIntensity = 0.5f;
    [Range(0, 0.5f), Tooltip("心拍で脈打つ幅。")]
    [SerializeField] private float m_beatAmount = 0.12f;
    [Tooltip("縁の色。黒に少し紫を混ぜると絵本らしくなる。")]
    [SerializeField] private Color m_color = new Color(0.08f, 0, 0.12f);
    [Range(0.01f, 1), Tooltip("縁のぼかし。")]
    [SerializeField] private float m_smoothness = 0.45f;

    [Header("心拍")]
    [Min(1), Tooltip("落ち着いているときの心拍（回/分）。")]
    [SerializeField] private float m_calmBpm = 60;
    [Min(1), Tooltip("一番緊張したときの心拍（回/分）。")]
    [SerializeField] private float m_panicBpm = 140;

    [Header("反応")]
    [Min(0.01f), Tooltip("緊張が高まる速さ（秒）。")]
    [SerializeField] private float m_riseSeconds = 0.3f;
    [Min(0.01f), Tooltip("緊張が解ける速さ（秒）。")]
    [SerializeField] private float m_fallSeconds = 1.5f;
    [Tooltip("専用 Volume の優先度。シーンの Vignette より上にする。")]
    [SerializeField] private float m_priority = 50;

    private GameObject m_volumeObject;
    private VolumeProfile m_profile;
    private Vignette m_vignette;
    private float m_threat;
    private float m_manualThreat;
    private float m_beatPhase;

    /// <summary>今の緊張の強さ（0〜1）。</summary>
    public float Threat { get { return m_threat; } }

    /// <summary>演出から緊張を上げる（0〜1）。オバケとの距離による緊張と大きいほうを使う。</summary>
    public void SetManualThreat(float threat)
    {
        m_manualThreat = Mathf.Clamp01(threat);
    }

    private void OnEnable()
    {
        m_volumeObject = new GameObject("HeartbeatVignette Volume") { hideFlags = HideFlags.DontSave };
        m_volumeObject.transform.SetParent(transform, false);
        var volume = m_volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = m_priority;
        m_profile = ScriptableObject.CreateInstance<VolumeProfile>();
        m_profile.hideFlags = HideFlags.DontSave;
        m_vignette = m_profile.Add<Vignette>(true);
        volume.sharedProfile = m_profile;
        Apply(0);
    }

    private void OnDisable()
    {
        if (m_volumeObject != null)
        {
            Destroy(m_volumeObject);
        }
        if (m_profile != null)
        {
            Destroy(m_profile);
        }
        m_volumeObject = null;
        m_profile = null;
        m_vignette = null;
    }

    private void Update()
    {
        Transform listener = m_listener;
        if (listener == null && Camera.main != null)
        {
            listener = Camera.main.transform;
        }
        // 一番近いオバケと、演出からの指定の大きいほう
        float target = m_manualThreat;
        if (listener != null)
        {
            foreach (GhostThreat ghost in GhostThreat.Active)
            {
                target = Mathf.Max(target, ghost.ThreatAt(listener.position));
            }
        }
        float seconds = target > m_threat ? m_riseSeconds : m_fallSeconds;
        m_threat = Mathf.MoveTowards(m_threat, target, Time.deltaTime / seconds);

        float bpm = Mathf.Lerp(m_calmBpm, m_panicBpm, m_threat);
        m_beatPhase = Mathf.Repeat(m_beatPhase + Time.deltaTime * bpm / 60, 1);
        Apply(Beat(m_beatPhase));
    }

    private void Apply(float beat)
    {
        if (m_vignette == null)
        {
            return;
        }
        float intensity = Mathf.Lerp(m_calmIntensity, m_panicIntensity, m_threat) + beat * m_beatAmount * m_threat;
        m_vignette.intensity.value = Mathf.Clamp01(intensity);
        m_vignette.color.value = m_color;
        m_vignette.smoothness.value = m_smoothness;
    }

    // 「ドクン、ドクン」の 2 つの山（0〜1 程度）
    private static float Beat(float phase)
    {
        float first = (phase - 0.06f) / 0.05f;
        float second = (phase - 0.28f) / 0.06f;
        return Mathf.Exp(-first * first) + 0.6f * Mathf.Exp(-second * second);
    }
}
