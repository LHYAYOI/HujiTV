using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 天井の指定箇所から雨粒を落とす雨漏り。この物体の位置から真下の床へ落とし、着地点で水しぶきと水たまりの波紋を起こす。
/// 落ちる時間を計算して着地を決めるので、パーティクルの当たり判定は使わない（軽い）。
/// </summary>
[DisallowMultipleComponent, AddComponentMenu("hatake/Rain/Rain Leak")]
public sealed class RainLeak : MonoBehaviour
{
    public enum RHYTHM
    {
        // 間隔がばらつく
        RANDOM = 0,
        // 「ぽた…ぽた…」と一定の間隔
        STEADY = 1
    }

    private struct Impact
    {
        public float m_time;
        public Vector3 m_position;
    }

    [Header("落ち方")]
    [SerializeField] private RHYTHM m_rhythm = RHYTHM.RANDOM;
    [Min(0.02f), Tooltip("雨粒の間隔（秒）。")]
    [SerializeField] private float m_interval = 0.6f;
    [Range(0, 1), Tooltip("RANDOM のときの間隔のばらつき。")]
    [SerializeField] private float m_randomness = 0.6f;
    [Min(0), Tooltip("雨粒が出る範囲の半径（m）。0 なら一点から落ちる。")]
    [SerializeField] private float m_spreadRadius = 0.05f;
    [Min(0), Tooltip("落ち始めの速さ（m/s）。")]
    [SerializeField] private float m_initialSpeed = 0.5f;
    [Min(0.1f), Tooltip("床を探す最大の距離（m）。")]
    [SerializeField] private float m_maxFall = 12;
    [Tooltip("床として当てるレイヤー。")]
    [SerializeField] private LayerMask m_floorMask = Physics.DefaultRaycastLayers;

    [Header("見た目")]
    [Tooltip("雨粒のマテリアル（custom/hatake/RainDrop）。未設定ならシェーダーから作る。")]
    [SerializeField] private Material m_dropMaterial;
    [Tooltip("水しぶきのマテリアル。未設定なら雨粒と同じ。")]
    [SerializeField] private Material m_splashMaterial;
    [SerializeField] private Color m_dropColor = new Color(0.8f, 0.88f, 1f, 0.7f);
    [Min(0.001f), Tooltip("雨粒の太さ（m）。")]
    [SerializeField] private float m_dropWidth = 0.015f;
    [Min(0), Tooltip("速さに対する筋の長さ。")]
    [SerializeField] private float m_streak = 0.035f;
    [Range(0, 32), Tooltip("着地で飛ぶしぶきの数。")]
    [SerializeField] private int m_splashCount = 6;
    [Min(0), Tooltip("しぶきの速さ（m/s）。")]
    [SerializeField] private float m_splashSpeed = 1.2f;
    [Min(0.001f), Tooltip("しぶきの大きさ（m）。")]
    [SerializeField] private float m_splashSize = 0.025f;

    [Header("水たまり")]
    [Tooltip("波紋を起こす水たまり。未指定なら着地点にある水たまりを探す。")]
    [SerializeField] private Puddle m_puddle;
    [Min(0), Tooltip("波紋の強さ。")]
    [SerializeField] private float m_rippleStrength = 1;

    [Header("音（任意）")]
    [SerializeField] private AudioSource m_audio;
    [SerializeField] private AudioClip[] m_dropClips = new AudioClip[0];
    [Tooltip("音の高さのばらつき（最小・最大）。")]
    [SerializeField] private Vector2 m_pitch = new Vector2(0.9f, 1.15f);

    [Header("その他")]
    [Tooltip("開始時から落とす。")]
    [SerializeField] private bool m_playOnStartFlag = true;

    private readonly List<Impact> m_impacts = new List<Impact>();
    private ParticleSystem m_drops;
    private ParticleSystem m_splash;
    private Material m_runtimeMaterial;
    private Vector3 m_floorPoint;
    private bool m_hasFloorFlag;
    private bool m_playingFlag;
    private float m_timer;

    public bool IsPlaying { get { return m_playingFlag; } }

    private void Awake()
    {
        FindFloor();
        Material dropMaterial = m_dropMaterial != null ? m_dropMaterial : RuntimeMaterial();
        m_drops = CreateSystem("Drops", dropMaterial, true);
        m_splash = CreateSystem("Splash", m_splashMaterial != null ? m_splashMaterial : dropMaterial, false);
        m_timer = NextInterval();
        m_playingFlag = m_playOnStartFlag;
    }

    private void OnDestroy()
    {
        if (m_runtimeMaterial != null)
        {
            Destroy(m_runtimeMaterial);
        }
    }

    /// <summary>雨粒を落とし始める。</summary>
    [ContextMenu("Play")]
    public void Play()
    {
        m_playingFlag = true;
    }

    /// <summary>雨粒を止める（落ちている粒はそのまま着地する）。</summary>
    [ContextMenu("Stop")]
    public void Stop()
    {
        m_playingFlag = false;
    }

    /// <summary>床の位置を探し直す（雨漏りや床を動かしたとき）。</summary>
    public void FindFloor()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, m_maxFall, m_floorMask, QueryTriggerInteraction.Ignore))
        {
            m_floorPoint = hit.point;
            m_hasFloorFlag = true;
        }
        else
        {
            m_floorPoint = transform.position + Vector3.down * m_maxFall;
            m_hasFloorFlag = false;
        }
    }

    private void Update()
    {
        if (m_playingFlag)
        {
            m_timer -= Time.deltaTime;
            // フレームが重くても間隔どおりの数を落とす
            int guard = 0;
            while (m_timer <= 0 && guard < 8)
            {
                Drip();
                m_timer += NextInterval();
                guard++;
            }
        }

        // 着地の時間になった粒を、しぶきと波紋に変える
        float now = Time.time;
        for (int i = m_impacts.Count - 1; i >= 0; i--)
        {
            if (now >= m_impacts[i].m_time)
            {
                Splash(m_impacts[i].m_position);
                m_impacts.RemoveAt(i);
            }
        }
    }

    private float NextInterval()
    {
        if (m_rhythm == RHYTHM.STEADY)
        {
            return m_interval;
        }
        return m_interval * Random.Range(1 - m_randomness, 1 + m_randomness);
    }

    // 雨粒を 1 つ落とし、着地の時刻を予約する
    private void Drip()
    {
        Vector2 offset = Random.insideUnitCircle * m_spreadRadius;
        Vector3 start = transform.position + new Vector3(offset.x, 0, offset.y);
        float height = Mathf.Max(0.01f, start.y - m_floorPoint.y);
        float gravity = Mathf.Max(0.01f, -Physics.gravity.y);
        // 初速と重力で、床に届くまでの時間を求める
        float fallTime = (-m_initialSpeed + Mathf.Sqrt(m_initialSpeed * m_initialSpeed + 2 * gravity * height)) / gravity;
        var emit = new ParticleSystem.EmitParams
        {
            position = start,
            velocity = Vector3.down * m_initialSpeed,
            startLifetime = fallTime,
            startSize = m_dropWidth,
            startColor = m_dropColor,
            applyShapeToPosition = false
        };
        m_drops.Emit(emit, 1);
        if (m_hasFloorFlag)
        {
            m_impacts.Add(new Impact { m_time = Time.time + fallTime, m_position = new Vector3(start.x, m_floorPoint.y, start.z) });
        }
    }

    // 着地：しぶき、波紋、音
    private void Splash(Vector3 position)
    {
        for (int i = 0; i < m_splashCount; i++)
        {
            Vector2 side = Random.insideUnitCircle * 0.8f;
            Vector3 direction = new Vector3(side.x, 1, side.y).normalized;
            var emit = new ParticleSystem.EmitParams
            {
                position = position + Vector3.up * 0.01f,
                velocity = direction * (m_splashSpeed * Random.Range(0.5f, 1)),
                startLifetime = Random.Range(0.2f, 0.4f),
                startSize = m_splashSize * Random.Range(0.6f, 1.2f),
                startColor = m_dropColor,
                applyShapeToPosition = false
            };
            m_splash.Emit(emit, 1);
        }

        Puddle puddle = m_puddle != null ? m_puddle : Puddle.FindAt(position);
        if (puddle != null)
        {
            puddle.AddRipple(position, m_rippleStrength);
        }

        if (m_audio != null && m_dropClips.Length > 0)
        {
            AudioClip clip = m_dropClips[Random.Range(0, m_dropClips.Length)];
            if (clip != null)
            {
                m_audio.pitch = Random.Range(m_pitch.x, m_pitch.y);
                m_audio.PlayOneShot(clip);
            }
        }
    }

    // 手で粒を出すだけのパーティクルを作る（重力で落ち、寿命で消える）
    private ParticleSystem CreateSystem(string label, Material material, bool stretchFlag)
    {
        var child = new GameObject(label);
        child.transform.SetParent(transform, false);
        var system = child.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startSpeed = 0;
        main.gravityModifier = 1;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 128;
        var emission = system.emission;
        emission.enabled = false;
        var shape = system.shape;
        shape.enabled = false;
        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = stretchFlag ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        if (stretchFlag)
        {
            renderer.velocityScale = m_streak;
            renderer.lengthScale = 1.5f;
        }
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = material;
        system.Play(true);
        return system;
    }

    private Material RuntimeMaterial()
    {
        if (m_runtimeMaterial == null)
        {
            Shader shader = Shader.Find("custom/hatake/RainDrop");
            if (shader == null)
            {
                Debug.LogWarning("雨粒のシェーダーが見つかりません。RainLeak にマテリアルを設定してください。", this);
                return null;
            }
            m_runtimeMaterial = new Material(shader) { name = "RainDrop (runtime)" };
        }
        return m_runtimeMaterial;
    }

    private void OnDrawGizmosSelected()
    {
        // 落ちる線と、着地点
        Vector3 floor = m_floorPoint;
        if (!Application.isPlaying)
        {
            floor = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, m_maxFall, m_floorMask, QueryTriggerInteraction.Ignore) ? hit.point : transform.position + Vector3.down * m_maxFall;
        }
        Gizmos.color = new Color(0.5f, 0.75f, 1f, 0.9f);
        Gizmos.DrawLine(transform.position, floor);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.03f, m_spreadRadius));
        Gizmos.DrawWireSphere(floor, 0.08f);
    }
}
