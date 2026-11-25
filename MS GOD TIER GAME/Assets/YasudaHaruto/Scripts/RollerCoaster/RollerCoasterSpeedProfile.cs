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
        [SerializeField, Min(0.1f)] private float m_descentSpeed = 36f;
        [SerializeField, Min(0.1f)] private float m_spiralSpeed = 24f;
        [SerializeField, Min(0.1f)] private float m_fallSpeed = 16f;

        [Header("レール先端でのためと落下")]
        [SerializeField, Min(1f)] private float m_dropBrakeDistance = 9f;
        [SerializeField, Min(0.01f)] private float m_dropCreepSpeed = 0.18f;
        [SerializeField, Min(0.1f)] private float m_dropAnticipationSeconds = 1.15f;
        [SerializeField, Min(0f)] private float m_dropEdgePauseSeconds = .08f;
        [SerializeField, Min(0f)] private float m_dropTipHoldSeconds = .35f;
        [SerializeField, Min(0.1f)] private float m_dropGravity = 9.81f;
        [SerializeField, Min(1f)] private float m_dropTerminalSpeed = 28f;
        [SerializeField, Range(0f, 50f)] private float m_dropTipDegrees = 38f;

        [Header("図の黒・赤・青・赤・青")]
        [SerializeField, Min(0.1f)] private float m_flatSpeed = 10f;
        [SerializeField, Min(0.1f)] private float m_firstClimbSpeed = 7f;
        [SerializeField, Min(0.1f)] private float m_firstDescentSpeed = 16f;
        [SerializeField, Min(0.1f)] private float m_secondClimbSpeed = 8f;
        [SerializeField, Min(0.1f)] private float m_finalDescentSpeed = 18f;

        [Header("最初の下り直前")]
        [SerializeField, Min(0f)] private float m_crestStopSeconds = 0f;
        [SerializeField, Min(0.1f)] private float m_crestApproachSpeed = 0.65f;
        [SerializeField, Min(0.1f)] private float m_crestLaunchSeconds = 3.2f;

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
        private float m_dropStart;
        private float m_dropElapsed;
        private float m_dropAccelerationElapsed;
        public enum DROP_STAGE
        {
            APPROACH, EDGE_PAUSE, CREEPING, TIP_HOLD, FALLING
        }
        private DROP_STAGE m_dropStage;
        private float m_dropFallVelocity;
        private bool m_dropReleased;
        private bool m_dropBrakeStarted;
        private float m_dropBrakeElapsed;
        private float m_dropBrakeDuration;
        private float m_dropBrakeEntrySpeed;


        public bool DropBrakeStarted => UseSectionSpeeds && m_dropBrakeStarted;

        public float DropBrakeElapsed => m_dropBrakeElapsed;

        public float DropBrakeDistance => m_dropBrakeDistance;

        public float DropBrakeProgress => Mathf.Clamp01(m_dropBrakeElapsed / m_dropBrakeDuration);


        public DROP_STAGE DropStage => m_dropStage;

        public bool AnticipatingDrop => UseSectionSpeeds && m_dropStage >= DROP_STAGE.EDGE_PAUSE && m_dropStage <= DROP_STAGE.TIP_HOLD;

        public float DropReleaseElapsed => m_dropAccelerationElapsed;

        public float DropReleaseTiltWeight => m_dropStage == DROP_STAGE.FALLING ? Smoother(m_dropAccelerationElapsed / .65f) : 0f;

        public float DropTerminalSpeed => m_dropTerminalSpeed;

        public float DropTipDegrees => m_dropTipDegrees;

        public float DropTipWeight => !UseSectionSpeeds || m_dropStage < DROP_STAGE.CREEPING ? 0f
            : m_dropStage == DROP_STAGE.CREEPING ? Smoother(m_dropElapsed / Mathf.Max(.1f, m_dropAnticipationSeconds)) : 1f;

        private static float Smoother(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }


        public bool UseSectionSpeeds => isActiveAndEnabled && m_useSectionSpeeds;

        public bool HoldingAtCrest => UseSectionSpeeds && m_holdRemaining > 0f;

        public float CrestStopSeconds => m_crestStopSeconds;

        public bool HoldingAfterLanding => UseSectionSpeeds && m_landingHoldRemaining > 0f;

        public bool LaunchingAfterLanding => UseSectionSpeeds && m_launching;

        public float LandingStopSeconds => m_landingStopSeconds;

        public float LandingStartSpeed => m_landingStartSpeed;


        public void Initialize(RollerCoasterCourse course, CartController cart, float startProgress)
        {
            m_course = course;
            m_cart = cart;
            m_length = course.Spline.CalculateLength();
            m_crestProgress = At(course.CrestKnot);
            m_holdRemaining = 0f;
            m_crestLaunching = false;
            m_crestLaunchElapsed = 0f;
            m_stopConsumed = startProgress >= m_crestProgress;
            m_landingProgress = At(course.FallEndKnot);
            m_landingStopConsumed = startProgress >= m_landingProgress;
            m_landingHoldRemaining = 0f;
            m_launchElapsed = 0f;
            m_launching = false;
            m_dropStart = course.FallLaunchParameter;
            m_dropElapsed = m_dropAccelerationElapsed = 0f;
            m_dropStage = startProgress >= m_dropStart ? DROP_STAGE.FALLING : DROP_STAGE.APPROACH;
            m_dropFallVelocity = .25f;
            m_dropReleased = startProgress >= m_dropStart;
            m_dropBrakeStarted = false;
            m_dropBrakeElapsed = 0f;
            m_dropBrakeDuration = 1f;
            m_dropBrakeEntrySpeed = m_fallSpeed;
            var sections = course.ReturnSectionKnots;
            if (sections == null || sections.Length != 6)
            {
                m_points = null;
                return;
            }
            float descentStart = At(course.SpiralStartKnot);
            m_points = new[] { 0f, m_crestProgress, Mathf.Lerp(m_crestProgress, descentStart, 0.3f), descentStart,
                At(course.SpiralEndKnot), At(course.FallStartKnot), At(course.FallEndKnot),
                At(sections[1]), At(sections[2]), At(sections[3]), At(sections[4]), At(sections[5]) };
        }


        private float At(int knot) => SplineUtility.ConvertIndexUnit(m_course.Spline.Spline, knot, PathIndexUnit.Knot, PathIndexUnit.Normalized);


        public float SpeedAt(float progress, float fallback)
        {
            if (!UseSectionSpeeds || m_points == null)
                return Mathf.Max(0.1f, fallback);
            if (progress < m_crestProgress)
                return Mathf.Lerp(m_entrySpeed, m_crestApproachSpeed,
                    Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, m_crestProgress, progress)));
            if (progress >= m_crestProgress && progress < m_points[3])
                return m_descentSpeed;
            float toEdge = (m_dropStart - progress) * m_length;
            if (toEdge >= 0f && toEdge <= m_dropBrakeDistance)
            {
                if (m_dropBrakeStarted)
                    return Mathf.Lerp(m_dropBrakeEntrySpeed, .02f, Mathf.Clamp01(m_dropBrakeElapsed / m_dropBrakeDuration));
                // 等減速度の停止距離から速度を求め、視認直後から明確にブレーキをかける。
                return Mathf.Sqrt(Mathf.Lerp(.02f * .02f, m_fallSpeed * m_fallSpeed,
                    Mathf.InverseLerp(.65f, m_dropBrakeDistance, toEdge)));
            }
            if (progress >= m_dropStart && progress <= m_points[6])
                return Mathf.Max(.1f, m_fallSpeed);
            int section = 0;
            while (section < m_points.Length - 2 && progress > m_points[section + 1])
                section++;
            // 着地後は停止と発進を別に扱うため、平坦区間へ落下速度を引き継がない。
            if (section == 6)
                return Mathf.Max(0.1f, m_flatSpeed);
            float rate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(m_points[section], m_points[section + 1], progress));
            return Mathf.Max(0.1f, Mathf.Lerp(Target(section), Target(section + 1), rate));
        }


        public float CurrentSpeed(float progress, float fallback)
        {
            if (!UseSectionSpeeds)
                return Mathf.Max(0.1f, fallback);
            if (HoldingAtCrest || HoldingAfterLanding)
                return 0f;
            if (AnticipatingDrop)
                return m_dropStage == DROP_STAGE.CREEPING ? CreepSpeed() : 0f;
            if (m_dropReleased && progress < m_landingProgress)
                return DropReleaseSpeed();
            if (m_crestLaunching)
                return CrestLaunchSpeed();
            if (m_launching)
                return LaunchSpeed();
            return SpeedAt(progress, fallback);
        }


        private float LaunchSpeed()
        {
            float rate = Mathf.SmoothStep(0f, 1f, m_launchElapsed / Mathf.Max(0.1f, m_landingAccelerationSeconds));
            return Mathf.Max(0.1f, Mathf.Lerp(m_landingStartSpeed, m_flatSpeed, rate));
        }


        private float CrestLaunchSpeed() => Mathf.Lerp(m_crestApproachSpeed, m_descentSpeed,
            Mathf.SmoothStep(0f, 1f, m_crestLaunchElapsed / Mathf.Max(0.1f, m_crestLaunchSeconds)));


        private float DropReleaseSpeed() => m_dropFallVelocity;

        private float CreepSpeed() => m_dropCreepSpeed * Mathf.Sin(Mathf.PI * Mathf.Clamp01(m_dropElapsed / Mathf.Max(.1f, m_dropAnticipationSeconds)));


        private float Target(int index)
        {
            switch (index)
            {
                case 0:
                    return m_entrySpeed;
                case 1:
                    return m_crestApproachSpeed;
                case 2:
                case 3:
                    return m_descentSpeed;
                case 4:
                    return m_spiralSpeed;
                case 5:
                    return m_fallSpeed;
                case 6:
                    return m_fallSpeed;
                case 7:
                    return m_flatSpeed;
                case 8:
                    return m_firstClimbSpeed;
                case 9:
                    return m_firstDescentSpeed;
                case 10:
                    return m_secondClimbSpeed;
                default:
                    return m_finalDescentSpeed;
            }
        }


        public void Tick(float progress, float deltaTime, float fallback)
        {
            if (!UseSectionSpeeds || m_points == null)
                return;
            float toEdge = (m_dropStart - progress) * m_length;
            if (!m_dropBrakeStarted && !m_dropReleased && toEdge <= m_dropBrakeDistance && toEdge > 0f)
            {
                m_dropBrakeStarted = true;
                m_dropBrakeEntrySpeed = Mathf.Max(.1f, m_cart.Speed);
                m_dropBrakeDuration = Mathf.Max(.1f, 2f * Mathf.Max(.05f, toEdge - .65f) / (m_dropBrakeEntrySpeed + .02f));
            }
            if (m_dropBrakeStarted)
                m_dropBrakeElapsed += Mathf.Max(0f, deltaTime);
            if (!m_dropReleased && m_dropStage == DROP_STAGE.APPROACH && m_dropBrakeStarted && m_dropBrakeElapsed >= m_dropBrakeDuration)
            {
                m_dropStage = DROP_STAGE.EDGE_PAUSE;
                m_dropElapsed = 0f;
            }
            if (AnticipatingDrop)
            {
                m_dropElapsed += Mathf.Max(0f, deltaTime);
                m_cart.SetSpeed(m_dropStage == DROP_STAGE.CREEPING ? CreepSpeed() : 0f);
                float duration = m_dropStage == DROP_STAGE.EDGE_PAUSE ? m_dropEdgePauseSeconds
                    : m_dropStage == DROP_STAGE.CREEPING ? m_dropAnticipationSeconds : m_dropTipHoldSeconds;
                if (m_dropElapsed >= duration)
                {
                    m_dropElapsed = 0f;
                    m_dropStage = m_dropStage == DROP_STAGE.EDGE_PAUSE ? DROP_STAGE.CREEPING
                        : m_dropStage == DROP_STAGE.CREEPING ? DROP_STAGE.TIP_HOLD : DROP_STAGE.FALLING;
                    if (m_dropStage == DROP_STAGE.FALLING)
                    {
                        m_dropReleased = true;
                        m_dropAccelerationElapsed = 0f;
                        m_dropFallVelocity = .25f;
                    }
                }
                return;
            }
            if (m_dropReleased && progress < m_landingProgress)
            {
                m_dropAccelerationElapsed += Mathf.Max(0f, deltaTime);
                Vector3 tangent = m_course.Spline.EvaluateTangent(0, progress);
                float gravityAlongTrack = Mathf.Max(.04f, -tangent.normalized.y);
                m_dropFallVelocity = Mathf.Min(m_dropTerminalSpeed, m_dropFallVelocity + m_dropGravity * gravityAlongTrack * Mathf.Max(0f, deltaTime));
                m_cart.SetSpeed(DropReleaseSpeed());
                return;
            }
            if (!m_stopConsumed && progress >= m_crestProgress)
            {
                m_stopConsumed = true;
                m_crestLaunching = true;
                m_crestLaunchElapsed = 0f;
                m_holdRemaining = Mathf.Max(0f, m_crestStopSeconds);
                if (m_holdRemaining > 0f)
                    m_cart.SetSpline(m_course.Spline, m_crestProgress * m_length, false, true);
            }
            if (m_holdRemaining > 0f)
            {
                m_holdRemaining = Mathf.Max(0f, m_holdRemaining - Mathf.Max(0f, deltaTime));
                m_cart.SetSpeed(0f);
                return;
            }
            if (m_crestLaunching)
            {
                m_crestLaunchElapsed += Mathf.Max(0f, deltaTime);
                m_cart.SetSpeed(CrestLaunchSpeed());
                if (m_crestLaunchElapsed >= Mathf.Max(0.1f, m_crestLaunchSeconds))
                    m_crestLaunching = false;
                return;
            }
            if (!m_landingStopConsumed && progress >= m_landingProgress)
            {
                m_landingStopConsumed = true;
                m_landingHoldRemaining = Mathf.Max(0f, m_landingStopSeconds);
                m_launchElapsed = 0f;
                m_launching = true;
                m_cart.SetSpline(m_course.Spline, m_landingProgress * m_length, false, true);
            }
            if (m_landingHoldRemaining > 0f)
            {
                m_landingHoldRemaining = Mathf.Max(0f, m_landingHoldRemaining - Mathf.Max(0f, deltaTime));
                m_cart.SetSpeed(0f);
                return;
            }
            if (m_launching)
            {
                m_launchElapsed += Mathf.Max(0f, deltaTime);
                m_cart.SetSpeed(LaunchSpeed());
                if (m_launchElapsed >= Mathf.Max(0.1f, m_landingAccelerationSeconds))
                    m_launching = false;
                return;
            }
            m_cart.SetSpeed(SpeedAt(progress, fallback));
        }
    }
}
