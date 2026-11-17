using UnityEngine;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster
{
    /// <summary>区間に応じた設定速度を既存CartController.SetSpeedへ渡す。</summary>
    public sealed class RollerCoasterSpeedProfile : MonoBehaviour
    {
        [SerializeField, InspectorName("区間速度を使う")] private bool m_useSectionSpeeds = true;
        [Header("入口・下降・らせん・落下")]
        [SerializeField, Min(0.1f)] private float m_entrySpeed = 6f;
        [SerializeField, Min(0.1f)] private float m_descentSpeed = 28f;
        [SerializeField, Min(0.1f)] private float m_spiralSpeed = 16f;
        [SerializeField, Min(0.1f)] private float m_fallSpeed = 24f;
        [Header("図の黒・赤・青・赤・青")]
        [SerializeField, Min(0.1f)] private float m_flatSpeed = 10f;
        [SerializeField, Min(0.1f)] private float m_firstClimbSpeed = 7f;
        [SerializeField, Min(0.1f)] private float m_firstDescentSpeed = 16f;
        [SerializeField, Min(0.1f)] private float m_secondClimbSpeed = 8f;
        [SerializeField, Min(0.1f)] private float m_finalDescentSpeed = 18f;
        [Header("最初の下り直前")]
        [SerializeField, Min(0f)] private float m_crestStopSeconds = 0.7f;
        [SerializeField, Min(0.1f)] private float m_crestApproachSpeed = 0.65f;
        [SerializeField, Min(0.1f)] private float m_crestLaunchSeconds = 1.3f;
        [Header("着地後の停止とゆっくりした発進")]
        [SerializeField, Min(0f)] private float m_landingStopSeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float m_landingStartSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float m_landingAccelerationSeconds = 3f;
        private float[] m_points;
        private RollerCoasterCourse m_course;
        private CartController m_cart;
        private float m_length;
        private float m_crestProgress;
        private float m_holdRemaining;
        private bool m_stopConsumed;
        private float m_landingProgress;
        private float m_landingHoldRemaining;
        private float m_launchElapsed;
        private bool m_landingStopConsumed;
        private bool m_launching;
        private bool m_crestLaunching;
        private float m_crestLaunchElapsed;

        public bool UseSectionSpeeds => isActiveAndEnabled && m_useSectionSpeeds;
        public bool HoldingAtCrest => UseSectionSpeeds && m_holdRemaining > 0f;
        public float CrestStopSeconds => m_crestStopSeconds;
        public bool HoldingAfterLanding => UseSectionSpeeds && m_landingHoldRemaining > 0f;
        public bool LaunchingAfterLanding => UseSectionSpeeds && m_launching;
        public float LandingStopSeconds => m_landingStopSeconds;
        public float LandingStartSpeed => m_landingStartSpeed;

        public void Initialize(RollerCoasterCourse course, CartController cart, float startProgress)
        {
            m_course = course; m_cart = cart; m_length = course.Spline.CalculateLength();
            m_crestProgress = At(course.CrestKnot);
            m_holdRemaining = 0f;
            m_crestLaunching = false; m_crestLaunchElapsed = 0f;
            m_stopConsumed = startProgress >= m_crestProgress;
            m_landingProgress = At(course.FallEndKnot);
            m_landingStopConsumed = startProgress >= m_landingProgress;
            m_landingHoldRemaining = 0f; m_launchElapsed = 0f; m_launching = false;
            var sections = course.ReturnSectionKnots;
            if (sections == null || sections.Length != 6) { m_points = null; return; }
            float descentStart = At(course.SpiralStartKnot);
            m_points = new[] { 0f, m_crestProgress, Mathf.Lerp(m_crestProgress, descentStart, 0.3f), descentStart,
                At(course.SpiralEndKnot), At(course.FallStartKnot), At(course.FallEndKnot),
                At(sections[1]), At(sections[2]), At(sections[3]), At(sections[4]), At(sections[5]) };
        }

        private float At(int knot) => SplineUtility.ConvertIndexUnit(m_course.Spline.Spline, knot, PathIndexUnit.Knot, PathIndexUnit.Normalized);

        public float SpeedAt(float progress, float fallback)
        {
            if (!UseSectionSpeeds || m_points == null) return Mathf.Max(0.1f, fallback);
            if (progress < m_crestProgress)
                return Mathf.Lerp(m_entrySpeed, m_crestApproachSpeed,
                    Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, m_crestProgress, progress)));
            int section = 0;
            while (section < m_points.Length - 2 && progress > m_points[section+1]) section++;
            // 着地後は停止と発進を別に扱うため、平坦区間へ落下速度を引き継がない。
            if (section == 6) return Mathf.Max(0.1f, m_flatSpeed);
            float rate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(m_points[section], m_points[section+1], progress));
            return Mathf.Max(0.1f, Mathf.Lerp(Target(section), Target(section+1), rate));
        }

        public float CurrentSpeed(float progress, float fallback)
        {
            if (!UseSectionSpeeds) return Mathf.Max(0.1f, fallback);
            if (HoldingAtCrest || HoldingAfterLanding) return 0f;
            if (m_crestLaunching) return CrestLaunchSpeed();
            if (m_launching) return LaunchSpeed();
            return SpeedAt(progress, fallback);
        }

        private float LaunchSpeed()
        {
            float rate = Mathf.SmoothStep(0f, 1f, m_launchElapsed / Mathf.Max(0.1f, m_landingAccelerationSeconds));
            return Mathf.Max(0.1f, Mathf.Lerp(m_landingStartSpeed, m_flatSpeed, rate));
        }

        private float CrestLaunchSpeed() => Mathf.Lerp(0f, m_entrySpeed,
            Mathf.SmoothStep(0f, 1f, m_crestLaunchElapsed / Mathf.Max(0.1f, m_crestLaunchSeconds)));

        private float Target(int index)
        {
            switch (index)
            {
                case 0: case 1: return m_entrySpeed;
                case 2: case 3: return m_descentSpeed;
                case 4: case 5: return m_spiralSpeed;
                case 6: return m_fallSpeed;
                case 7: return m_flatSpeed;
                case 8: return m_firstClimbSpeed;
                case 9: return m_firstDescentSpeed;
                case 10: return m_secondClimbSpeed;
                default: return m_finalDescentSpeed;
            }
        }

        public void Tick(float progress, float deltaTime, float fallback)
        {
            if (!UseSectionSpeeds || m_points == null) return;
            if (!m_stopConsumed && progress >= m_crestProgress)
            {
                m_stopConsumed = true;
                m_crestLaunching = true; m_crestLaunchElapsed = 0f;
                m_holdRemaining = Mathf.Max(0f, m_crestStopSeconds);
                if (m_holdRemaining > 0f) m_cart.SetSpline(m_course.Spline, m_crestProgress*m_length, false, true);
            }
            if (m_holdRemaining > 0f)
            {
                m_holdRemaining = Mathf.Max(0f, m_holdRemaining-Mathf.Max(0f, deltaTime));
                m_cart.SetSpeed(0f);
                return;
            }
            if (m_crestLaunching)
            {
                m_crestLaunchElapsed += Mathf.Max(0f, deltaTime);
                m_cart.SetSpeed(CrestLaunchSpeed());
                if (m_crestLaunchElapsed >= Mathf.Max(0.1f, m_crestLaunchSeconds)) m_crestLaunching = false;
                return;
            }
            if (!m_landingStopConsumed && progress >= m_landingProgress)
            {
                m_landingStopConsumed = true;
                m_landingHoldRemaining = Mathf.Max(0f, m_landingStopSeconds);
                m_launchElapsed = 0f; m_launching = true;
                m_cart.SetSpline(m_course.Spline, m_landingProgress*m_length, false, true);
            }
            if (m_landingHoldRemaining > 0f)
            {
                m_landingHoldRemaining = Mathf.Max(0f, m_landingHoldRemaining-Mathf.Max(0f, deltaTime));
                m_cart.SetSpeed(0f);
                return;
            }
            if (m_launching)
            {
                m_launchElapsed += Mathf.Max(0f, deltaTime);
                m_cart.SetSpeed(LaunchSpeed());
                if (m_launchElapsed >= Mathf.Max(0.1f, m_landingAccelerationSeconds)) m_launching = false;
                return;
            }
            m_cart.SetSpeed(SpeedAt(progress, fallback));
        }
    }
}
