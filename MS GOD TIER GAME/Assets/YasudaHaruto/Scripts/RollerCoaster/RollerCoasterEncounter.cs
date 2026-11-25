using System;
using UnityEngine;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster
{
    /// <summary>進行位置に応じて対向車の移動・表示と回避演出を評価する。</summary>
    [DefaultExecutionOrder(1050)]
    public sealed class RollerCoasterEncounter : MonoBehaviour
    {
        [SerializeField] private RollerCoasterTrialRide m_ride;
        [SerializeField] private SplineContainer m_originalRoute;
        [SerializeField] private SplineContainer m_oncomingRoute;
        [SerializeField] private Transform m_oncomingCart;
        [SerializeField] private int m_number;
        [SerializeField] private bool m_avoidRight;
        [SerializeField] private float m_begin, m_peak, m_end;
        [SerializeField, Min(1f)] private float m_triggerLeadMeters = 40f;
        [SerializeField, Min(.1f)] private float m_speed = 25f;
        [SerializeField, Min(1f)] private float m_startDistance = 15f;
        [SerializeField, HideInInspector] private float m_roomEntry, m_roomExit;
        [SerializeField] private Renderer[] m_branchRenderers = new Renderer[0];
        private float[] m_distances;
        private float m_distance, m_mainLength;

        public SplineContainer OriginalRoute => m_originalRoute;

        public SplineContainer OncomingRoute => m_oncomingRoute;

        public Transform OncomingCart => m_oncomingCart;

        public int Number => m_number;

        public float ApproachSpeed => m_speed;

        public float RouteDistance => m_distance;

        public bool AvoidRight => m_avoidRight;

        public float Begin => m_begin;

        public float Peak => m_peak;

        public float End => m_end;

        public float RoomEntry => m_roomEntry;

        public float RoomExit => m_roomExit;

        public float StartDistance => m_startDistance;

        public float StopDistance => 0f; // 旧検証用。現在は経路終端まで通過する。

        public float CurrentSpeed
        {
            get; private set;
        }

        public bool Seen
        {
            get; private set;
        }

        public bool Passed
        {
            get; private set;
        }

        public bool BranchesVisible
        {
            get
            {
                if (m_branchRenderers.Length < 2)
                    return false;
                foreach (var r in m_branchRenderers)
                    if (r == null || !r.enabled)
                        return false;
                return true;
            }
        }

        private void Cache()
        {
            m_distances = new float[513];
            Vector3 previous = m_oncomingRoute.EvaluatePosition(0, 0f);
            for (int i = 1; i < m_distances.Length; i++)
            {
                Vector3 next = m_oncomingRoute.EvaluatePosition(0, (float)i / (m_distances.Length - 1));
                m_distances[i] = m_distances[i - 1] + Vector3.Distance(previous, next);
                previous = next;
            }
            m_distance = Mathf.Min(m_startDistance, m_distances[m_distances.Length - 1]);
            m_mainLength = m_ride.Course.Spline.CalculateLength();
        }

        public float ParameterAtDistance(float distance)
        {
            if (m_distances == null)
                Cache();
            distance = Mathf.Clamp(distance, 0, m_distances[m_distances.Length - 1]);
            int index = Array.BinarySearch(m_distances, distance);
            if (index >= 0)
                return (float)index / (m_distances.Length - 1);
            int high = Mathf.Clamp(~index, 1, m_distances.Length - 1);
            return (high - 1 + Mathf.InverseLerp(m_distances[high - 1], m_distances[high], distance)) / (m_distances.Length - 1);
        }

        public float OncomingParameter(float progress) => ParameterAtDistance(Mathf.Lerp(m_startDistance, 0f, Mathf.InverseLerp(m_begin - .08f, m_peak, progress)));

        private void LateUpdate()
        {
            if (m_ride == null || m_oncomingRoute == null || m_oncomingCart == null)
                return;
            if (m_distances == null)
                Cache();
            float progress = m_ride.Progress;
            float trigger = m_begin - m_triggerLeadMeters / m_mainLength;
            if (progress < trigger)
            {
                Seen = false;
                Passed = false;
                CurrentSpeed = 0;
                m_distance = Mathf.Min(m_startDistance, m_distances[m_distances.Length - 1]);
                m_oncomingCart.gameObject.SetActive(false);
                return;
            }
            if (!m_ride.Paused && m_ride.Cart.Speed > .05f)
                m_distance -= m_speed * Time.deltaTime;
            bool active = m_distance > 0f;
            bool previousActive = m_oncomingCart.gameObject.activeSelf;
            m_oncomingCart.gameObject.SetActive(active);
            if (active)
                Seen = true;
            if (Seen && progress >= m_end)
                Passed = true;
            float t = ParameterAtDistance(m_distance);
            m_oncomingRoute.Evaluate(0, t, out var position, out var tangent, out var up);
            CurrentSpeed = active && previousActive && Time.deltaTime > .00001f ? Vector3.Distance(m_oncomingCart.position, position) / Time.deltaTime : 0;
            m_oncomingCart.SetPositionAndRotation(position, Quaternion.LookRotation(-(Vector3)tangent, up));
        }

        private void OnDisable()
        {
            if (m_oncomingCart != null)
                m_oncomingCart.gameObject.SetActive(false);
            CurrentSpeed = 0;
        }

#if UNITY_EDITOR
        public void Configure(RollerCoasterTrialRide ride, SplineContainer baseline, SplineContainer opposing, Transform cart, int number, bool avoidRight,
            float begin, float peak, float end, float roomEntry, float roomExit, Renderer[] branches)
        {
            m_ride = ride;
            m_originalRoute = baseline;
            m_oncomingRoute = opposing;
            m_oncomingCart = cart;
            m_number = number;
            m_avoidRight = avoidRight;
            m_begin = begin;
            m_peak = peak;
            m_end = end;
            m_roomEntry = roomEntry;
            m_roomExit = roomExit;
            m_branchRenderers = branches;
            m_triggerLeadMeters = number == 1 ? 13f : 11f;
            m_distances = null;
        }
#endif
    }
}
