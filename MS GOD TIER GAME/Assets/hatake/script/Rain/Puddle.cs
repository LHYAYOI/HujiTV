using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 床の水たまり。Quad を床に寝かせて Puddle シェーダーで描く。
/// 部屋の景色は反射プローブから、ゴーストは GhostReflection（ゴースト専用の平面反射）から映す。
/// 雨粒が落ちると AddRipple で波紋が広がり、反射が揺れる。
/// </summary>
[RequireComponent(typeof(MeshRenderer)), DisallowMultipleComponent, AddComponentMenu("hatake/Rain/Puddle")]
public sealed class Puddle : MonoBehaviour
{
    private const int RIPPLE_COUNT = 16;

    [Header("波紋")]
    [Min(0), Tooltip("雨粒が当たったときの波紋の強さの倍率。")]
    [SerializeField] private float m_rippleStrength = 1;
    [Min(0), Tooltip("雨粒がなくても 1 秒あたりに起きる小さな波紋の数（天井からの細かい滴など）。")]
    [SerializeField] private float m_idleRipplesPerSecond;

    [Header("ゴーストの映り込み")]
    [Tooltip("GhostReflection がシーンにあれば、ゴーストを映す。")]
    [SerializeField] private bool m_ghostReflectionFlag = true;
    [Min(1), Tooltip("この距離（m）より遠いときはゴーストを映さない（軽くするため）。")]
    [SerializeField] private float m_ghostMaxDistance = 25;

    [Header("形")]
    [Tooltip("縁の形を決める番号。変えると形が変わる。")]
    [SerializeField] private int m_shapeSeed;

    private static readonly List<Puddle> s_active = new List<Puddle>();
    private static readonly int g_ripplesId = Shader.PropertyToID("_Ripples");
    private static readonly int g_seedId = Shader.PropertyToID("_ShapeSeed");
    private static readonly int g_ghostTextureId = Shader.PropertyToID("_GhostReflectionTex");
    private static readonly int g_ghostEnabledId = Shader.PropertyToID("_GhostEnabled");

    private readonly Vector4[] m_ripples = new Vector4[RIPPLE_COUNT];
    private MeshRenderer m_renderer;
    private MaterialPropertyBlock m_block;
    private int m_nextRipple;
    private float m_idleTimer;

    private void Reset()
    {
        m_shapeSeed = Random.Range(0, 1000);
    }

    private void OnEnable()
    {
        m_renderer = GetComponent<MeshRenderer>();
        if (!s_active.Contains(this))
        {
            s_active.Add(this);
        }
    }

    private void OnDisable()
    {
        s_active.Remove(this);
    }

    /// <summary>点の真下（真上）にある水たまりを探す。なければ null。</summary>
    public static Puddle FindAt(Vector3 worldPosition)
    {
        foreach (Puddle puddle in s_active)
        {
            if (puddle.Contains(worldPosition))
            {
                return puddle;
            }
        }
        return null;
    }

    /// <summary>点が水たまりの範囲（Quad の上）か。</summary>
    public bool Contains(Vector3 worldPosition)
    {
        if (Mathf.Abs(worldPosition.y - transform.position.y) > 0.3f)
        {
            return false;
        }
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        return Mathf.Abs(local.x) <= 0.5f && Mathf.Abs(local.y) <= 0.5f;
    }

    /// <summary>この位置から波紋を広げる。</summary>
    public void AddRipple(Vector3 worldPosition, float strength = 1)
    {
        m_ripples[m_nextRipple] = new Vector4(worldPosition.x, worldPosition.z, strength * m_rippleStrength, Time.timeSinceLevelLoad);
        m_nextRipple = (m_nextRipple + 1) % RIPPLE_COUNT;
    }

    private void LateUpdate()
    {
        // 雨粒がなくても起きる小さな波紋
        if (m_idleRipplesPerSecond > 0)
        {
            m_idleTimer -= Time.deltaTime;
            if (m_idleTimer <= 0)
            {
                Vector3 point = transform.TransformPoint(new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(-0.35f, 0.35f), 0));
                AddRipple(point, 0.35f);
                m_idleTimer = Random.Range(0.5f, 1.5f) / m_idleRipplesPerSecond;
            }
        }

        if (m_block == null)
        {
            m_block = new MaterialPropertyBlock();
        }
        m_renderer.GetPropertyBlock(m_block);
        m_block.SetVectorArray(g_ripplesId, m_ripples);
        m_block.SetFloat(g_seedId, m_shapeSeed * 0.731f);

        // 見えていて近いときだけ、ゴースト専用の反射を頼む
        Texture ghost = null;
        GhostReflection reflection = GhostReflection.Active;
        Camera viewer = Camera.main;
        if (m_ghostReflectionFlag && reflection != null && viewer != null && m_renderer.isVisible &&
            Vector3.Distance(viewer.transform.position, transform.position) <= m_ghostMaxDistance)
        {
            ghost = reflection.Request(transform.position.y);
        }
        m_block.SetTexture(g_ghostTextureId, ghost != null ? ghost : Texture2D.blackTexture);
        m_block.SetFloat(g_ghostEnabledId, ghost != null ? 1 : 0);
        m_renderer.SetPropertyBlock(m_block);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.8f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1, 1, 0));
    }
}
