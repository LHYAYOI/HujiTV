using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace hatake
{
    /// <summary>
    /// 部品ごとに分けたシャンデリア（Shanderia_Broken.fbx）を、吊るす → 落とす → 床に当たって部品ごとに砕ける、で動かす。
    /// モデルの根に付ける。子の MeshRenderer（Crown / Finial / Core / Arm_xx / Lamp_xx）を1つずつ破片として扱う。
    ///   ・割れる前：根の Rigidbody 1つ＋子の凸の MeshCollider（まとめて1つの物体として落ちる）
    ///   ・割れた後：各部品に Rigidbody を付けて切り離し、落ちてきた勢いを引き継いで当たった点から飛び散らせる
    ///   ・数秒後に破片を床へ沈めて消す。ResetChandelier で吊るした状態へ戻す
    /// 衝突の演出は ChandelierImpactEffect（あれば）と On Break イベント（VFX Graph などを後から足す口）で出す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChandelierBreakable : MonoBehaviour
    {
        [System.Serializable]
        public sealed class BreakEvent : UnityEvent<Vector3, Vector3, float> { }

        [Header("落下")]
        [Tooltip("ON：開始時は吊るしたまま（Drop で落ちる）。OFF：開始と同時に落ちる")]
        [SerializeField] private bool m_hangOnStart = true;
        [Tooltip("全体の重さ（kg）。部品の大きさで割り振る")]
        [SerializeField, Min(0.1f)] private float m_totalMass = 60f;

        [Header("割れ方")]
        [Tooltip("この速さ（m/s）以上でぶつかったら割れる")]
        [SerializeField, Min(0)] private float m_breakSpeed = 3f;
        [Tooltip("当たった点から外へ飛ばす勢い（m/s）。ぶつかった速さに比例して強くなる（Break Speed で 1 倍）")]
        [SerializeField, Min(0)] private float m_scatterSpeed = 2.5f;
        [Tooltip("上向きへの飛び散り（0 で真横〜外向き）")]
        [SerializeField, Range(0, 3)] private float m_scatterUpward = 0.6f;
        [Tooltip("落ちてきた勢いのうち、割れた後も残す割合（床に当たって止まるので小さめ）")]
        [SerializeField, Range(0, 1)] private float m_keepVelocity = 0.25f;
        [Tooltip("破片の回転の勢い（rad/s）")]
        [SerializeField, Min(0)] private float m_spin = 6f;
        [Tooltip("OFF：破片どうしは当たらない（凸の当たり判定が重なって弾け飛ぶのを防ぐ。床・壁には当たる）")]
        [SerializeField] private bool m_piecesCollideEachOther;

        [Header("後始末")]
        [Tooltip("割れてから破片を沈め始めるまでの秒数。0 で消さない")]
        [SerializeField, Min(0)] private float m_pieceLifetime = 8f;
        [Tooltip("床へ沈めて消すまでの秒数")]
        [SerializeField, Min(0.01f)] private float m_sinkDuration = 1.5f;

        [Header("演出")]
        [Tooltip("衝突の演出（粉じん・火花・結晶片・閃光・揺れ・音）。未設定なら出さない")]
        [SerializeField] private ChandelierImpactEffect m_effect;
        [Tooltip("割れたとき（当たった点, 面の向き, 強さ 0〜）。VFX Graph の SendEvent などをここにつなぐ")]
        [SerializeField] private BreakEvent m_onBreak = new BreakEvent();
        [Tooltip("割れたら消す灯り（シャンデリアの子の Light など）")]
        [SerializeField] private Light[] m_lampLights = new Light[0];

        private sealed class Piece
        {
            public Transform transform;
            public Transform parent;
            public MeshCollider collider;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
            public float mass;
        }

        private readonly List<Piece> m_pieces = new List<Piece>();
        private Rigidbody m_body;
        private Vector3 m_startPosition;
        private Quaternion m_startRotation;
        private bool m_broken;
        private Coroutine m_cleanup;
        private float m_size = 1f;

        public bool IsBroken => m_broken;
        public bool IsHanging => m_body != null && m_body.isKinematic && !m_broken;
        public BreakEvent OnBreak => m_onBreak;
        /// <summary>割れる前の大きさ（外形の対角線、m）。演出の大きさの目安</summary>
        public float Size => m_size;

        private void Awake()
        {
            m_startPosition = transform.position;
            m_startRotation = transform.rotation;
            CollectPieces();
            m_body = GetComponent<Rigidbody>();
            if (m_body == null) m_body = gameObject.AddComponent<Rigidbody>();
            m_body.mass = m_totalMass;
            m_body.interpolation = RigidbodyInterpolation.Interpolate;
            // 吊るしている間（キネマティック）は連続衝突判定を使えないので Speculative。落とすときに切り替える
            m_body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            m_body.isKinematic = m_hangOnStart;
            if (!m_hangOnStart) m_body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        // 子のメッシュを破片として集め、凸の当たり判定を付ける（割れる前は根の Rigidbody にまとめてつながる）
        private void CollectPieces()
        {
            var renderers = GetComponentsInChildren<MeshRenderer>();
            float totalVolume = 0f;
            Bounds world = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (MeshRenderer r in renderers)
            {
                MeshFilter filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                MeshCollider col = r.GetComponent<MeshCollider>();
                if (col == null) col = r.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = filter.sharedMesh;
                col.convex = true;
                Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, r.transform.lossyScale);
                float volume = Mathf.Max(size.x * size.y * size.z, 1e-6f);
                totalVolume += volume;
                m_pieces.Add(new Piece
                {
                    transform = r.transform,
                    parent = r.transform.parent,
                    collider = col,
                    localPosition = r.transform.localPosition,
                    localRotation = r.transform.localRotation,
                    localScale = r.transform.localScale,
                    mass = volume
                });
                if (first)
                {
                    world = r.bounds;
                    first = false;
                }
                else
                {
                    world.Encapsulate(r.bounds);
                }
            }
            m_size = Mathf.Max(0.1f, world.size.magnitude);
            // 重さは大きさ（外形の体積）で割り振る
            foreach (Piece p in m_pieces)
            {
                p.mass = Mathf.Max(0.05f, m_totalMass * p.mass / Mathf.Max(totalVolume, 1e-6f));
            }
            if (m_pieces.Count == 0) Debug.LogWarning("ChandelierBreakable : 子に MeshRenderer がありません", this);
        }

        private void Start()
        {
            if (m_effect != null) m_effect.SetSize(Size);
        }

        /// <summary>衝突の演出を設定する（コードで付けたとき用）</summary>
        public void SetEffect(ChandelierImpactEffect effect)
        {
            m_effect = effect;
            if (m_effect != null) m_effect.SetSize(Size);
        }

        /// <summary>吊るしている状態から落とす</summary>
        public void Drop()
        {
            if (m_broken || m_body == null) return;
            m_body.isKinematic = false;
            // 速く落ちても床をすり抜けない
            m_body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            m_body.WakeUp();
        }

        private Vector3 m_lastVelocity;
        private Vector3 m_lastAngularVelocity;

        // 衝突の時点の速さは、ぶつかって止まった後の値になっているので、直前の物理の段の速さを覚えておく
        private void FixedUpdate()
        {
            if (m_broken || m_body == null || m_body.isKinematic) return;
            m_lastVelocity = m_body.linearVelocity;
            m_lastAngularVelocity = m_body.angularVelocity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (m_broken || m_body == null || m_body.isKinematic) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < m_breakSpeed) return;
            // 当たった点：接触点の平均。向き：最初の接触の法線（床なら上向き）
            Vector3 point = Vector3.zero;
            int count = collision.contactCount;
            for (int i = 0; i < count; i++) point += collision.GetContact(i).point;
            point = count > 0 ? point / count : transform.position;
            Vector3 normal = count > 0 ? collision.GetContact(0).normal : Vector3.up;
            Break(point, normal, speed);
        }

        /// <summary>部品ごとに砕く。point：当たった点、normal：当たった面の向き、speed：ぶつかった速さ（m/s）</summary>
        public void Break(Vector3 point, Vector3 normal, float speed)
        {
            if (m_broken) return;
            m_broken = true;
            Vector3 velocity = m_lastVelocity;
            Vector3 angular = m_lastAngularVelocity;
            Vector3 center = m_body != null ? m_body.worldCenterOfMass : transform.position;
            float strength = Mathf.Max(speed, 0.01f) / Mathf.Max(m_breakSpeed, 0.01f);

            // 根の Rigidbody を外す（子の当たり判定は、各破片の Rigidbody へ移る）
            if (m_body != null)
            {
                m_body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                m_body.isKinematic = true;
                m_body.detectCollisions = false;
            }

            var bodies = new List<Rigidbody>(m_pieces.Count);
            foreach (Piece p in m_pieces)
            {
                p.transform.SetParent(null, true);
                Rigidbody rb = p.transform.gameObject.AddComponent<Rigidbody>();
                rb.mass = p.mass;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                // 落ちてきた勢い（回転していた分も）を一部引き継ぐ
                Vector3 offset = p.transform.position - center;
                rb.linearVelocity = (velocity + Vector3.Cross(angular, offset)) * m_keepVelocity;
                // 当たった点から外へ、少し上向きに飛ばす。重さに関係なく同じ勢い（VelocityChange）
                Vector3 away = p.transform.position - point;
                away = Vector3.ProjectOnPlane(away, normal).normalized + normal * m_scatterUpward;
                float jitter = Random.Range(0.6f, 1.3f);
                rb.AddForce(away.normalized * m_scatterSpeed * Mathf.Min(strength, 3f) * jitter, ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * m_spin, ForceMode.VelocityChange);
                bodies.Add(rb);
            }
            if (!m_piecesCollideEachOther)
            {
                for (int i = 0; i < m_pieces.Count; i++)
                {
                    for (int j = i + 1; j < m_pieces.Count; j++)
                    {
                        Physics.IgnoreCollision(m_pieces[i].collider, m_pieces[j].collider, true);
                    }
                }
            }

            foreach (Light lamp in m_lampLights)
            {
                if (lamp != null) lamp.enabled = false;
            }
            if (m_effect != null) m_effect.Play(point, normal, strength);
            m_onBreak.Invoke(point, normal, strength);
            if (m_pieceLifetime > 0f) m_cleanup = StartCoroutine(SinkPieces(bodies));
        }

        // 一定時間後、破片を床へ沈めて消す（当たり判定を切って下へずらし、最後に表示を消す）
        private IEnumerator SinkPieces(List<Rigidbody> bodies)
        {
            yield return new WaitForSeconds(m_pieceLifetime);
            float depth = Mathf.Max(0.2f, Size * 0.2f);
            var starts = new Vector3[bodies.Count];
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i] == null) continue;
                bodies[i].collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                bodies[i].isKinematic = true;
                bodies[i].detectCollisions = false;
                bodies[i].interpolation = RigidbodyInterpolation.None;
                starts[i] = bodies[i].transform.position;
            }
            for (float t = 0f; t < m_sinkDuration; t += Time.deltaTime)
            {
                float k = t / m_sinkDuration;
                for (int i = 0; i < bodies.Count; i++)
                {
                    if (bodies[i] != null) bodies[i].transform.position = starts[i] + Vector3.down * depth * k * k;
                }
                yield return null;
            }
            foreach (Piece p in m_pieces) p.transform.gameObject.SetActive(false);
            m_cleanup = null;
        }

        /// <summary>割れる前の、吊るした状態へ戻す</summary>
        public void ResetChandelier()
        {
            if (m_cleanup != null) StopCoroutine(m_cleanup);
            m_cleanup = null;
            for (int i = 0; i < m_pieces.Count; i++)
            {
                Piece p = m_pieces[i];
                Rigidbody rb = p.transform.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);
                p.transform.gameObject.SetActive(true);
                p.transform.SetParent(p.parent, false);
                p.transform.localPosition = p.localPosition;
                p.transform.localRotation = p.localRotation;
                p.transform.localScale = p.localScale;
                for (int j = i + 1; j < m_pieces.Count; j++)
                {
                    Physics.IgnoreCollision(p.collider, m_pieces[j].collider, false);
                }
            }
            transform.SetPositionAndRotation(m_startPosition, m_startRotation);
            if (m_body != null)
            {
                m_body.detectCollisions = true;
                m_body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                m_body.isKinematic = true;
                m_body.position = m_startPosition;
                m_body.rotation = m_startRotation;
            }
            foreach (Light lamp in m_lampLights)
            {
                if (lamp != null) lamp.enabled = true;
            }
            m_broken = false;
            m_lastVelocity = Vector3.zero;
            m_lastAngularVelocity = Vector3.zero;
        }

        // 割れた後に消されたとき、切り離した破片も一緒に消す（残ると場面に破片が残り続ける）
        private void OnDestroy()
        {
            foreach (Piece p in m_pieces)
            {
                if (p.transform != null && p.transform.parent != p.parent) Destroy(p.transform.gameObject);
            }
        }
    }
}
