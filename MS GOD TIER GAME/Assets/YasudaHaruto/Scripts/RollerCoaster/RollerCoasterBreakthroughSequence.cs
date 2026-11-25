using System;
using System.Collections.Generic;
using UnityEngine;

namespace YasudaHaruto.RollerCoaster
{
    /// <summary>進入禁止板の突破、距離に同期した減速、木片と衝突シェイクを管理する。</summary>
    [DefaultExecutionOrder(1080)]
    public sealed class RollerCoasterBreakthroughSequence : MonoBehaviour
    {
        [Serializable]
        public sealed class Barrier
        {
            public float MetersBeforeEdge;
            public Transform Root;
            public GameObject Lettering;
            public GameObject IntactVisual;
            public Transform[] Fragments;
        }

        [SerializeField] private Barrier[] m_barriers = Array.Empty<Barrier>();
        [SerializeField, Min(20)] private float m_brakeDistance = 40f;
        [SerializeField, Min(1)] private float m_edgeSpeed = 8f;
        [SerializeField, HideInInspector] private float m_straightExtension;
        private RollerCoasterStoryboard m_story;
        private RollerCoasterTrialRide m_ride;
        private Vector3[][] m_intactPositions;
        private Quaternion[][] m_intactRotations;
        private bool[] m_broken;
        private float m_impactAge = 10f;
        private int m_impactSide;
        private readonly List<Flight> m_flights = new List<Flight>();

        private sealed class Flight
        {
            public Transform Piece;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float Age;
        }

        public Barrier[] Barriers => m_barriers;
        public float BrakeDistance => m_brakeDistance;
        public float StraightExtension => m_straightExtension;
        public int BrokenCount { get; private set; }
        public int FlyingFragmentCount => m_flights.Count;
        public float ImpactElapsed => m_impactAge;
        public Vector3 ImpactRotation { get; private set; }
        public Vector3 ImpactPosition { get; private set; }

        private void EnsureReferences()
        {
            if (m_story == null) m_story = GetComponent<RollerCoasterStoryboard>();
            if (m_ride == null) m_ride = GetComponent<RollerCoasterTrialRide>();
        }

        public void RestartSequence()
        {
            EnsureReferences();
            if (m_intactPositions == null || m_intactPositions.Length != m_barriers.Length)
            {
                m_intactPositions = new Vector3[m_barriers.Length][];
                m_intactRotations = new Quaternion[m_barriers.Length][];
                for (int i = 0; i < m_barriers.Length; i++)
                {
                    var pieces = m_barriers[i].Fragments;
                    m_intactPositions[i] = new Vector3[pieces.Length];
                    m_intactRotations[i] = new Quaternion[pieces.Length];
                    for (int j = 0; j < pieces.Length; j++)
                    {
                        m_intactPositions[i][j] = pieces[j].localPosition;
                        m_intactRotations[i][j] = pieces[j].localRotation;
                    }
                }
            }
            m_flights.Clear();
            m_broken = new bool[m_barriers.Length];
            BrokenCount = 0;
            m_impactAge = 10f;
            ImpactRotation = ImpactPosition = Vector3.zero;
            for (int i = 0; i < m_barriers.Length; i++)
            {
                var barrier = m_barriers[i];
                if (barrier.Root == null) continue;
                barrier.Root.gameObject.SetActive(true);
                if (barrier.Lettering != null) barrier.Lettering.SetActive(true);
                if (barrier.IntactVisual != null) barrier.IntactVisual.SetActive(true);
                for (int j = 0; j < barrier.Fragments.Length; j++)
                {
                    var piece = barrier.Fragments[j];
                    if (piece == null) continue;
                    piece.localPosition = m_intactPositions[i][j];
                    piece.localRotation = m_intactRotations[i][j];
                    piece.gameObject.SetActive(barrier.IntactVisual == null);
                }
            }
        }

        public float ModifySpeed(float progress, float baseSpeed)
        {
            EnsureReferences();
            float remaining = (m_story.Markers[10] - progress) * m_story.Length;
            if (progress > m_story.Markers[10] && progress < m_story.FallEnd)
            {
                float edgeHeight = ((Vector3)m_ride.Course.Spline.EvaluatePosition(0, m_story.Markers[10])).y;
                float height = ((Vector3)m_ride.Course.Spline.EvaluatePosition(0, progress)).y;
                return Mathf.Min(30f, Mathf.Sqrt(m_edgeSpeed * m_edgeSpeed + 2f * 9.81f * Mathf.Max(0, edgeHeight - height)));
            }
            if (remaining < 0 || m_barriers.Length == 0 || remaining > m_barriers[0].MetersBeforeEdge + 1.1f) return baseSpeed;
            // 最初の接触直前の通常速度を基準にする。参照点は未接触区間なので再評価はそこで終わる。
            float speed = m_story.SpeedAt(m_story.Markers[10] - (m_barriers[0].MetersBeforeEdge + 1.15f) / m_story.Length);
            // 板の間は速度を保ち、接触後0.9mだけで抵抗を反映する。能動的な連続ブレーキは加えない。
            for (int i = 0; i < m_barriers.Length; i++)
            {
                float crossed = m_barriers[i].MetersBeforeEdge + 1.1f - remaining;
                float target = i == 0 ? m_edgeSpeed + 10f : i == 1 ? m_edgeSpeed + 5f : m_edgeSpeed;
                speed = Mathf.Lerp(speed, target, Mathf.SmoothStep(0, 1, Mathf.Clamp01(crossed / .9f)));
            }
            return Mathf.Max(m_edgeSpeed, speed);
        }

        public float ContactLeanAt(float progress)
        {
            EnsureReferences();
            if (m_barriers.Length == 0) return 0;
            float remaining = (m_story.Markers[10] - progress) * m_story.Length;
            float lean = 0;
            foreach (var barrier in m_barriers)
                lean += Mathf.SmoothStep(0, 1, Mathf.Clamp01((barrier.MetersBeforeEdge + 1.1f - remaining) / 1.4f));
            return lean / m_barriers.Length;
        }

        private void Start() => RestartSequence();

        private void LateUpdate()
        {
            EnsureReferences();
            if (m_ride == null || m_ride.Paused) return;
            if (m_broken == null) RestartSequence();
            float remaining = (m_story.Markers[10] - m_ride.Progress) * m_story.Length;
            for (int i = 0; i < m_barriers.Length; i++)
                if (!m_broken[i] && remaining <= m_barriers[i].MetersBeforeEdge + 1.1f)
                    BreakBarrier(i);
            float dt = Time.deltaTime;
            m_impactAge += dt;
            float envelope = Mathf.Exp(-m_impactAge * 7f) * Mathf.Clamp01(m_impactAge / .02f);
            ImpactRotation = new Vector3(12f * Mathf.Cos(m_impactAge * 83f), 3f * m_impactSide * Mathf.Sin(m_impactAge * 71f),
                5.5f * m_impactSide * Mathf.Sin(m_impactAge * 97f)) * envelope;
            ImpactPosition = new Vector3(.055f * m_impactSide * Mathf.Sin(m_impactAge * 73f), -.14f * Mathf.Cos(m_impactAge * 67f), .18f * Mathf.Cos(m_impactAge * 61f)) * envelope;
            for (int i = m_flights.Count - 1; i >= 0; i--)
            {
                var flight = m_flights[i];
                flight.Age += dt;
                flight.Velocity += Physics.gravity * dt;
                flight.Velocity *= Mathf.Exp(-.3f * dt);
                flight.Piece.position += flight.Velocity * dt;
                flight.Piece.Rotate(flight.Spin * dt, Space.World);
                if (flight.Age > 2.4f)
                {
                    flight.Piece.gameObject.SetActive(false);
                    m_flights.RemoveAt(i);
                }
            }
        }

        private void BreakBarrier(int index)
        {
            var barrier = m_barriers[index];
            m_broken[index] = true;
            BrokenCount++;
            m_impactAge = 0;
            m_impactSide = index % 2 == 0 ? 1 : -1;
            if (barrier.Lettering != null) barrier.Lettering.SetActive(false);
            if (barrier.IntactVisual != null) barrier.IntactVisual.SetActive(false);
            foreach (var piece in barrier.Fragments)
            {
                piece.gameObject.SetActive(true);
                float side = Mathf.Sign(piece.localPosition.x);
                var velocity = barrier.Root.forward * (m_ride.Cart.Speed * .7f + UnityEngine.Random.Range(4f, 8f)) +
                    barrier.Root.right * side * UnityEngine.Random.Range(3f, 7f) + barrier.Root.up * UnityEngine.Random.Range(2f, 6f);
                m_flights.Add(new Flight { Piece = piece, Velocity = velocity, Spin = UnityEngine.Random.insideUnitSphere * 420f });
            }
        }

        private void OnDisable()
        {
            ImpactRotation = ImpactPosition = Vector3.zero;
        }

#if UNITY_EDITOR
        public void Configure(Barrier[] barriers, float extension)
        {
            m_barriers = barriers;
            m_straightExtension = extension;
            m_intactPositions = null;
        }
#endif
    }
}
