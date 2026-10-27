using UnityEngine;

namespace LightShaftLab
{
    /// <summary>
    /// 扉が開いたときに、床を這う煙を流れ出させる。
    /// 床霧ボリューム（LocalFogVolume の GROUND）の広がり半径と濃さ、煙パーティクルの量を 1 か所でまとめて動かす。
    /// 扉の開閉からは Open / Close を呼ぶ（UnityEvent からも呼べる）。
    /// 廊下に最初から漂わせる場合は m_playOnStartFlag をオンにする。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorSmoke : MonoBehaviour
    {
        public enum DOOR_SMOKE_STATE
        {
            CLOSED = 0,
            OPEN = 1,
            CLOSING = 2
        }

        [Tooltip("床霧ボリューム。Shape は GROUND にする。")]
        public LocalFogVolume m_groundFog;
        [Tooltip("扉の隙間から押し出される煙パーティクル。")]
        public ParticleSystem m_puffs;
        [Tooltip("開始時から開いた状態にする（廊下に常に漂わせる場合）。")]
        public bool m_playOnStartFlag;

        [Header("床霧")]
        [Min(0), Tooltip("開いたときの床霧の濃さ（1mあたり）。")]
        public float m_fogDensity = 1.5f;
        [Min(0.1f), Tooltip("床霧が最大まで広がる半径（m）。")]
        public float m_maxRadius = 7;
        [Min(0.1f), Tooltip("最大の半径まで広がるのにかかる秒数。最初は速く、だんだん遅くなる。")]
        public float m_spreadSeconds = 4;
        [Min(0), Tooltip("濃さが立ち上がる秒数。")]
        public float m_fadeInSeconds = 0.4f;

        [Header("煙パーティクル")]
        [Min(0), Tooltip("開いた直後の1秒あたりの粒の数。")]
        public float m_burstRate = 45;
        [Range(0, 1), Tooltip("開きっぱなしのとき、開いた直後に対して出し続ける割合。")]
        public float m_sustainRatio = 0.2f;
        [Min(0.01f), Tooltip("開いた直後の勢いが落ち着くまでの秒数。")]
        public float m_burstSeconds = 1.2f;

        [Header("扉を閉じたとき")]
        [Min(0.01f), Tooltip("床霧が消えるまでの秒数。")]
        public float m_fadeOutSeconds = 3;

        [Header("扉の開き具合")]
        [Range(0, 1), Tooltip("0 = 閉、1 = 全開。扉のアニメーションから書き込むと煙の量が開き具合に合う。")]
        public float m_doorOpenAmount = 1;

        public DOOR_SMOKE_STATE State { get; private set; } = DOOR_SMOKE_STATE.CLOSED;

        float m_elapsed;
        float m_fogWeight;
        float m_radius;

        void Awake()
        {
            // 床霧の広がりの中心は扉（この物体）にする
            if (m_groundFog && !m_groundFog.m_spreadOrigin)
            {
                m_groundFog.m_spreadOrigin = transform;
            }
            ResetToClosed();
        }

        void Start()
        {
            if (m_playOnStartFlag)
            {
                Open();
                // 最初から漂わせる場合は広がりきった状態から始める
                m_elapsed = m_spreadSeconds + m_burstSeconds * 4;
                m_fogWeight = 1;
                Apply();
            }
        }

        void OnDisable()
        {
            if (m_puffs)
            {
                SetEmission(0);
            }
        }

        /// <summary>扉が開いた。煙が流れ出し始める。</summary>
        [ContextMenu("Open")]
        public void Open()
        {
            if (State == DOOR_SMOKE_STATE.OPEN)
            {
                return;
            }
            // 閉じかけからもう一度開いたときは、今の広がりを続きとして使う
            if (State == DOOR_SMOKE_STATE.CLOSED)
            {
                m_elapsed = 0;
            }
            else
            {
                m_elapsed = InverseSpread(m_radius / Mathf.Max(0.0001f, m_maxRadius)) * m_spreadSeconds;
            }
            State = DOOR_SMOKE_STATE.OPEN;
            if (m_puffs && !m_puffs.isPlaying)
            {
                m_puffs.Play(true);
            }
        }

        /// <summary>扉が閉じた。新しい煙は止まり、床霧はゆっくり消える。</summary>
        [ContextMenu("Close")]
        public void Close()
        {
            if (State != DOOR_SMOKE_STATE.OPEN)
            {
                return;
            }
            State = DOOR_SMOKE_STATE.CLOSING;
        }

        /// <summary>扉の開き具合（0〜1）を設定する。</summary>
        public void SetDoorOpenAmount(float amount)
        {
            m_doorOpenAmount = Mathf.Clamp01(amount);
        }

        /// <summary>煙をすぐに消して閉じた状態に戻す。</summary>
        [ContextMenu("Reset To Closed")]
        public void ResetToClosed()
        {
            State = DOOR_SMOKE_STATE.CLOSED;
            m_elapsed = 0;
            m_fogWeight = 0;
            m_radius = 0;
            if (m_puffs)
            {
                m_puffs.Clear(true);
            }
            Apply();
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            switch (State)
            {
                case DOOR_SMOKE_STATE.OPEN:
                    // 開いている間は広がり続け、濃さを立ち上げる
                    m_elapsed += deltaTime * m_doorOpenAmount;
                    m_fogWeight = m_fadeInSeconds > 0 ? Mathf.MoveTowards(m_fogWeight, 1, deltaTime / m_fadeInSeconds) : 1;
                    break;
                case DOOR_SMOKE_STATE.CLOSING:
                    // 閉じたら広がりはそのままで薄くしていく
                    m_fogWeight = Mathf.MoveTowards(m_fogWeight, 0, deltaTime / m_fadeOutSeconds);
                    if (m_fogWeight <= 0)
                    {
                        ResetToClosed();
                        return;
                    }
                    break;
                case DOOR_SMOKE_STATE.CLOSED:
                    return;
            }
            Apply();
        }

        // 今の状態を床霧とパーティクルへ書き込む
        void Apply()
        {
            if (State == DOOR_SMOKE_STATE.OPEN)
            {
                m_radius = Spread(Mathf.Clamp01(m_elapsed / m_spreadSeconds)) * m_maxRadius;
            }
            if (m_groundFog)
            {
                m_groundFog.m_density = m_fogDensity * m_fogWeight;
                m_groundFog.m_spreadRadius = m_radius;
            }
            if (m_puffs)
            {
                float rate = 0;
                if (State == DOOR_SMOKE_STATE.OPEN)
                {
                    float burst = Mathf.Exp(-m_elapsed / m_burstSeconds);
                    rate = m_burstRate * Mathf.Lerp(m_sustainRatio, 1, burst) * m_doorOpenAmount;
                }
                SetEmission(rate);
            }
        }

        void SetEmission(float rate)
        {
            var emission = m_puffs.emission;
            emission.rateOverTime = rate;
        }

        // 最初は速く、だんだん遅く広がる（3 次の ease out）
        static float Spread(float t)
        {
            float rest = 1 - t;
            return 1 - rest * rest * rest;
        }

        static float InverseSpread(float value)
        {
            return 1 - Mathf.Pow(Mathf.Clamp01(1 - value), 1f / 3f);
        }

        void OnDrawGizmosSelected()
        {
            // 煙が流れ出す向き
            Gizmos.color = new Color(0.7f, 0.85f, 1f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
            Gizmos.DrawWireSphere(transform.position + transform.forward * 1.5f, 0.08f);
        }
    }
}
