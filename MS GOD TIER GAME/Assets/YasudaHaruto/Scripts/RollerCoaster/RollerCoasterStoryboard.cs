using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster
{
    /// <summary>画像の11区間の進行と演出。旧コースの区間番号には依存しない。</summary>
    [DefaultExecutionOrder(1060)]
    public sealed class RollerCoasterStoryboard : MonoBehaviour
    {
        public enum ACTOR_KIND
        {
            ONCOMING, PARALLEL, CROSSING
        }
        [Serializable]
        public sealed class ACTOR
        {
            public int Section;
            public ACTOR_KIND Kind;
            public SplineContainer Route;
            public Transform Cart;
            public float Meeting;
            public float Span = 30f;
            [NonSerialized] public bool Seen;
            [NonSerialized] public bool Passed;
        }
        [Serializable]
        public sealed class CEILING
        {
            public int Section;
            public Transform Panel;
            public float Entry;
            public float Exit;
            public float Lift = 2.6f;
            public float Lead = 15f;
            [NonSerialized] public bool Opened;
        }
        [SerializeField] private RollerCoasterTrialRide m_ride;
        [SerializeField] private float[] m_markers;
        [SerializeField] private AnimationCurve m_speeds;
        [SerializeField] private ACTOR[] m_actors = new ACTOR[0];
        [SerializeField] private CEILING[] m_ceilings = new CEILING[0];
        [SerializeField] private float m_fallEnd;

        [Header("乗車演出の調整")]
        [SerializeField, Min(.5f)] private float m_bankSnapMeters = 2.6f;
        [SerializeField, Min(1f)] private float m_bankHoldMeters = 8f;
        [Tooltip("カーブ復帰直後の回転振動と沈み込みの強さ。1で従来と同じ。")]
        [SerializeField, Range(0f, 1f)] private float m_bankReturnStrength = .65f;
        [SerializeField, Range(.6f, 1)] private float m_cruiseScale = .8f;
        [SerializeField, Range(.1f, .5f)] private float m_oncomingRate = .405f;
        [SerializeField, Range(0, 40)] private float m_parallelLookDegrees = 26f;
        [SerializeField, Range(3, 12)] private float m_oncomingApproach = 6.3f;
        [SerializeField, Range(.5f, 6)] private float m_crossingLead = 2.1f;
        [SerializeField, Range(0, 35)] private float m_brakePitch = 22f;
        [SerializeField, Min(0)] private float m_landingStopSeconds = .65f;
        [SerializeField, Min(.1f)] private float m_landingRecoverySeconds = .55f;
        [SerializeField, Min(.1f)] private float m_landingLaunchSeconds = 2.5f;
        private bool m_landingReached;
        private float m_landingElapsed;
        private float m_length;
        private List<(float T, float From)> m_bankReturns;

        public bool HoldingAfterLanding => m_landingReached && m_landingElapsed < m_landingRecoverySeconds + m_landingStopSeconds;

        public float LandingRecovery => Mathf.Clamp01(m_landingElapsed / m_landingRecoverySeconds);

        public float LandingStopSeconds => m_landingStopSeconds;

        public float CrossingLead => m_crossingLead;

        public bool BrakeFailing => isActiveAndEnabled && m_ride.Progress > m_markers[10] - 18f / Length && m_ride.Progress < m_markers[10];

        public float BrakePhase => Mathf.InverseLerp(m_markers[10] - 18f / Length, m_markers[10], m_ride.Progress);

        public float[] Markers => m_markers;

        public ACTOR[] Actors => m_actors;

        public CEILING[] Ceilings => m_ceilings;

        public float FallEnd => m_fallEnd;

        public float Length => m_length > 0 ? m_length : m_ride.Course.Spline.CalculateLength();

        public int Section
        {
            get
            {
                int n = 1;
                for (int i = 1; i < m_markers.Length; i++)
                    if (m_ride.Progress >= m_markers[i])
                        n = i + 1;
                return n;
            }
        }

        public void RestartSequence()
        {
            m_length = m_ride.Course.Spline.CalculateLength();
            m_bankReturns = null;
            m_landingReached = m_ride.Progress >= m_fallEnd;
            m_landingElapsed = m_landingReached ? 100f : 0f;
            foreach (var actor in m_actors)
            {
                actor.Seen = false;
                actor.Passed = false;
                actor.Cart.gameObject.SetActive(false);
            }
            foreach (var ceiling in m_ceilings)
            {
                ceiling.Opened = false;
                ceiling.Panel.localPosition = Vector3.zero;
            }
            Evaluate(m_ride.Progress);
        }

        public float LookYawAt(float progress)
        {
            float distance = (progress - m_markers[5]) * Length;
            return -m_parallelLookDegrees * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-24, -8, distance)) *
                (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(10, 24, distance)));
        }

        public void Tick(float progress, float deltaTime)
        {
            // 着地を最初に越えたフレームだけ位置を固定し、復帰・停止・再発進の時計を開始する。
            if (!m_landingReached && progress >= m_fallEnd)
            {
                m_landingReached = true;
                m_landingElapsed = 0;
                m_ride.Cart.SetSpline(m_ride.Course.Spline, m_fallEnd * Length, false, true);
                m_ride.Cart.SetSpeed(0);
                return;
            }
            if (m_landingReached)
                m_landingElapsed += Mathf.Max(0, deltaTime);
            m_ride.Cart.SetSpeed(SpeedAt(progress));
        }

        public float SpeedAt(float progress)
        {
            if (m_landingReached && progress >= m_fallEnd)
            {
                if (HoldingAfterLanding)
                    return 0;
                float launch = Mathf.Clamp01((m_landingElapsed - m_landingRecoverySeconds - m_landingStopSeconds) / m_landingLaunchSeconds);
                return Mathf.Lerp(.45f, 2.4f, Mathf.SmoothStep(0, 1, launch));
            }
            float speed = m_speeds.Evaluate(progress);
            if (progress > m_markers[0] && progress < m_markers[1])
            {
                float top = ((Vector3)m_ride.Course.Spline.EvaluatePosition(0, m_markers[0])).y;
                float bottom = ((Vector3)m_ride.Course.Spline.EvaluatePosition(0, m_markers[1])).y;
                float height = ((Vector3)m_ride.Course.Spline.EvaluatePosition(0, progress)).y;
                float drop = Mathf.Clamp01((top - height) / (top - bottom));
                return Mathf.Sqrt(9f + (36f * 36f - 9f) * drop);
            }
            // ⑪はブレーキを踏む演出でも実速度を落とさず、そのまま落下する。
            if (progress > m_markers[10] && progress < m_fallEnd)
            {
                float drop = Mathf.Max(0, ((Vector3)m_ride.Course.Spline.EvaluatePosition(0, m_markers[10])).y -
                    ((Vector3)m_ride.Course.Spline.EvaluatePosition(0, progress)).y);
                speed = Mathf.Sqrt(speed * speed + 2f * 9.81f * drop);
            }
            float reveal = (progress - m_markers[7]) * Length;
            float revealWeight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-22, -12, reveal)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(8, 20, reveal)));
            speed = Mathf.Lerp(speed, 16f, revealWeight);
            speed *= Mathf.Lerp(1, m_cruiseScale, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(m_markers[1], m_markers[1] + 16f / Length, progress)));
            return Mathf.Max(.5f, speed);
        }

        public Quaternion RotationAt(float progress)
        {
            var route = m_ride.Course.Spline;
            route.Evaluate(0, progress, out _, out var tangent, out var up);
            if (progress > m_markers[10] && progress < m_fallEnd)
            {
                // 最終落下は入口の水平向きを保ち、姿勢の前傾だけを滑らかに増やす。
                Vector3 heading = Vector3.ProjectOnPlane(route.EvaluateTangent(0, m_markers[10]), Vector3.up).normalized;
                float phase = Mathf.InverseLerp(m_markers[10], m_fallEnd, progress);
                var falling = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(Mathf.Lerp(m_brakePitch, 65f, Mathf.SmoothStep(0, 1, phase * 3)), 0, 0);
                return falling;
            }
            Quaternion rotation = Quaternion.LookRotation(tangent, up);
            if (m_landingReached && progress >= m_fallEnd && LandingRecovery < 1)
            {
                Vector3 heading = Vector3.ProjectOnPlane(route.EvaluateTangent(0, m_markers[10]), Vector3.up).normalized;
                return Quaternion.Slerp(Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(65, 0, 0), rotation, Mathf.SmoothStep(0, 1, LandingRecovery));
            }
            if (progress > m_markers[1] && progress < m_markers[10])
                rotation = Quaternion.LookRotation(tangent, Vector3.up) * Quaternion.Euler(0, 0, RideBankDegreesAt(progress));
            if (progress <= m_markers[10] && BrakeFailing)
                rotation *= Quaternion.Euler(m_brakePitch * Mathf.SmoothStep(0, 1, BrakePhase), 0, 0);
            return rotation;
        }

        private void EnsureBankReturns()
        {
            // 大きなバンクを抜ける位置だけを記録する。Restartで破棄して再走行と同期する。
            if (m_bankReturns != null)
                return;
            if (m_length <= 0)
                m_length = m_ride.Course.Spline.CalculateLength();
            m_bankReturns = new List<(float T, float From)>();
            bool armed = false;
            for (float t = m_markers[1]; t < m_markers[3]; t += .00025f)
            {
                float roll = RailBankDegreesAt(t);
                if (Mathf.Abs(roll) > 20)
                    armed = true;
                if (armed && Mathf.Abs(roll) < 5)
                {
                    float from = RailBankDegreesAt(Mathf.Max(m_markers[1], t - m_bankHoldMeters / Length));
                    if (Mathf.Abs(from) > 10)
                        m_bankReturns.Add((t, from));
                    armed = false;
                }
            }
        }

        public float RailBankDegreesAt(float progress)
        {
            m_ride.Course.Spline.Evaluate(0, progress, out _, out var tangent, out var up);
            return Vector3.SignedAngle(Quaternion.LookRotation(tangent, Vector3.up) * Vector3.up, up, tangent);
        }

        public float RideBankDegreesAt(float progress)
        {
            // 傾きを短く保持したあと、距離で補間する。Pause中は復帰演出も進まない。
            EnsureBankReturns();
            float rail = RailBankDegreesAt(progress);
            foreach (var reset in m_bankReturns)
            {
                float distance = (progress - reset.T) * Length;
                if (distance >= -m_bankHoldMeters && distance < 0)
                    return reset.From;
                if (distance >= 0 && distance < m_bankSnapMeters)
                    return Mathf.LerpAngle(reset.From, rail, Mathf.SmoothStep(0, 1, distance / m_bankSnapMeters));
            }
            return rail;
        }

        public int BankReturnCount
        {
            get
            {
                EnsureBankReturns();
                return m_bankReturns.Count;
            }
        }

        public Vector3 BankReturnShakeAt(float progress)
        {
            EnsureBankReturns();
            Vector3 shake = Vector3.zero;
            foreach (var reset in m_bankReturns)
            {
                float phase = ((progress - reset.T) * Length - m_bankSnapMeters) / 5f;
                if (phase >= 0 && phase < 1)
                    shake += new Vector3(2.8f * Mathf.Sin(phase * Mathf.PI * 7), 0, 1.2f * Mathf.Sin(phase * Mathf.PI * 5)) * (1 - phase) * (1 - phase);
            }
            // レールのバンク角は維持し、復帰直後に重ねる振動だけを弱める。
            return shake * m_bankReturnStrength;
        }

        public float BankReturnDipAt(float progress)
        {
            EnsureBankReturns();
            float dip = 0;
            foreach (var reset in m_bankReturns)
            {
                float phase = ((progress - reset.T) * Length - m_bankSnapMeters) / 5f;
                if (phase >= 0 && phase < 1)
                    dip -= .09f * Mathf.Sin(phase * Mathf.PI) * (1 - phase);
            }
            return dip * m_bankReturnStrength;
        }

        public Vector3 WheelPivotOffset(float progress)
        {
            if (progress <= m_markers[1] || progress >= m_markers[10] - 18f / Length)
                return Vector3.zero;
            var route = m_ride.Course.Spline;
            route.Evaluate(0, progress, out _, out var tangent, out var up);
            float difference = Vector3.SignedAngle(up, RotationAt(progress) * Vector3.up, tangent);
            if (Mathf.Abs(difference) < .01f)
                return Vector3.zero;
            float sign = Mathf.Sign(difference);
            Vector3 pivot = new Vector3(-sign * .72f, 0, 0);
            return Quaternion.LookRotation(tangent, up) * pivot - RotationAt(progress) * pivot;
        }

        private void LateUpdate()
        {
            if (m_ride != null && Application.isPlaying)
                Evaluate(m_ride.Progress);
        }

        public void Evaluate(float progress)
        {
            if (m_length <= 0)
                m_length = m_ride.Course.Spline.CalculateLength();
            foreach (var actor in m_actors)
            {
                // 自車との進行距離差から相手位置を決め、Pause/Restartで演出時刻がずれないようにする。
                float delta = (progress - actor.Meeting) * m_length;
                bool active = (actor.Kind == ACTOR_KIND.ONCOMING || delta > -actor.Span) && delta < actor.Span;
                actor.Cart.gameObject.SetActive(active);
                if (!active)
                {
                    if (delta >= actor.Span && actor.Seen)
                        actor.Passed = true;
                    continue;
                }
                actor.Seen = true;
                float t;
                bool reverse = actor.Kind == ACTOR_KIND.ONCOMING;
                if (reverse)
                    t = Mathf.Clamp01((m_oncomingApproach - delta * m_oncomingRate) / actor.Route.CalculateLength());
                else if (actor.Kind == ACTOR_KIND.PARALLEL)
                    t = Mathf.InverseLerp(-actor.Span, actor.Span, delta + 7f);
                else
                    t = Mathf.Clamp01(.5f + (delta + m_crossingLead) * .1f);
                actor.Route.Evaluate(0, t, out var p, out var tangent, out var up);
                actor.Cart.SetPositionAndRotation(p, Quaternion.LookRotation(reverse ? -(Vector3)tangent : (Vector3)tangent, up));
                if (delta > 5f)
                    actor.Passed = true;
            }
            foreach (var ceiling in m_ceilings)
            {
                float distance = (ceiling.Entry - progress) * m_length;
                float clear = 1f;
                float weight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ceiling.Lead, clear, distance));
                if (progress > ceiling.Exit)
                    weight = 1f;
                ceiling.Panel.localPosition = Vector3.up * ceiling.Lift * weight;
                if (weight > .99f)
                    ceiling.Opened = true;
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || m_ride == null)
                return;
            GUI.Box(new Rect(12, 94, 340, 30), $"Course sequence {Section} / 11");
        }

#if UNITY_EDITOR
        public void ApplyRideRefinement()
        {
            m_cruiseScale = .8f;
            m_oncomingRate = .405f;
            m_bankReturns = null;
        }

        public void ConfigureCeilings(CEILING[] ceilings)
        {
            m_ceilings = ceilings;
        }

        public void Configure(RollerCoasterTrialRide ride, float[] markers, float fallEnd, AnimationCurve speeds, ACTOR[] actors, CEILING[] ceilings)
        {
            m_ride = ride;
            m_markers = markers;
            m_fallEnd = fallEnd;
            m_speeds = speeds;
            m_actors = actors;
            m_ceilings = ceilings;
        }
#endif
    }
}
