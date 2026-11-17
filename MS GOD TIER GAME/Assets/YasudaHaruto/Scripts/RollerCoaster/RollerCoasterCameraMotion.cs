using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster
{
    /// <summary>HamadaのCameraShakeを走行進捗から制御する。車体姿勢には揺れを加えない。</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class RollerCoasterCameraMotion : MonoBehaviour
    {
        [SerializeField] private RollerCoasterCourse m_course;
        [SerializeField] private CartController m_cart;
        [SerializeField] private CameraShake m_shake;
        [SerializeField] private CinemachineCamera m_virtualCamera;
        [SerializeField] private CinemachineBrain m_brain;
        [Header("走行中の揺れ")]
        [SerializeField, Range(0f, 1f)] private float m_rollingStrength = 0.75f;
        [SerializeField, Min(1f)] private float m_referenceSpeed = 28f;
        [SerializeField, Range(0f, 1f)] private float m_uphillBoost = 0.1f;
        [SerializeField, Range(0f, 1f)] private float m_downhillBoost = 0.3f;
        [SerializeField, Range(0f, 1f)] private float m_gradeChangeBoost = 0.12f;
        [SerializeField, Range(0f, 0.5f)] private float m_airborneStrength = 0.3f;
        [SerializeField, Range(1f,3f)] private float m_downhillVerticalScale = 1.5f;
        [SerializeField, Range(1f,3f)] private float m_fallVerticalScale = 1.25f;
        private RollerCoasterVerticalNoiseExtension m_verticalNoise;
        [SerializeField,Range(0f,25f)] private float m_encounterLookDegrees=15f;
        private RollerCoasterEncounter[] m_encounters;
        private CinemachineRotationExtension m_lookExtension;
        private float m_lookYaw;
        [Header("着地の衝撃（回転の強さ、度）")]
        [SerializeField, Range(0.1f, 1f)] private float m_landingDuration = 0.5f;
        [SerializeField, Range(0f, 6f)] private float m_landingPitch = 2.5f;
        [SerializeField, Range(0f, 3f)] private float m_landingYaw = 0.45f;
        [SerializeField, Range(0f, 3f)] private float m_landingRoll = 0.45f;
        [Header("試走構成のカメラ管理")]
        [SerializeField] private bool m_manageCameraActivation = true;
        [SerializeField] private bool m_manualBrainUpdate = true;
        private bool m_driving;
        private bool m_landed;
        private float m_length;
        private float m_gapStart;
        private float m_gapEnd;
        private float m_previousProgress;
        private float m_previousSlope;
        private float m_currentRate;

        public float CurrentRollingRate => m_currentRate;
        public int LandingImpactCount { get; private set; }
        public CinemachineCamera VirtualCamera => m_virtualCamera;
        public CameraShake Shake => m_shake;

        public void BeginRide(float startProgress)
        {
            if (m_course == null || m_course.Spline == null || m_cart == null || m_shake == null || m_virtualCamera == null) return;
            m_length = m_course.Spline.CalculateLength();
            m_gapStart = SplineUtility.ConvertIndexUnit(m_course.Spline.Spline, m_course.FallStartKnot, PathIndexUnit.Knot, PathIndexUnit.Normalized);
            m_gapEnd = SplineUtility.ConvertIndexUnit(m_course.Spline.Spline, m_course.FallEndKnot, PathIndexUnit.Knot, PathIndexUnit.Normalized);
            m_landed = startProgress >= m_gapEnd;
            m_previousProgress = startProgress;
            m_previousSlope = SlopeAt(startProgress);
            m_currentRate = 0f;
            LandingImpactCount = 0;
            m_driving = true;
            m_verticalNoise=m_virtualCamera.GetComponent<RollerCoasterVerticalNoiseExtension>();
            m_encounters=m_course.GetComponentsInChildren<RollerCoasterEncounter>(true);
            m_lookExtension=m_virtualCamera.GetComponent<CinemachineRotationExtension>();m_lookYaw=0f;
            m_shake.SetRateOnTrolleyShake(0f);
            m_virtualCamera.PreviousStateIsValid = false;
            if (m_manageCameraActivation)
            {
                m_virtualCamera.enabled = true;
                if (m_brain != null) m_brain.enabled = true;
            }
        }

        public void EndRide()
        {
            m_driving = false; m_currentRate = 0f;
            if(m_verticalNoise!=null)m_verticalNoise.VerticalScale=1f;
            if(m_lookExtension!=null)m_lookExtension.SetTiltRotationOffset(Vector3.zero);
            if (m_shake != null) m_shake.SetRateOnTrolleyShake(0f);
            if (m_manageCameraActivation)
            {
                if (m_virtualCamera != null) m_virtualCamera.enabled = false;
                if (m_brain != null) m_brain.enabled = false;
            }
        }

        public float RollingRateFor(float speed, float slope, float slopeChange, bool airborne)
        {
            if (Mathf.Abs(speed) < 0.05f) return 0f;
            if (airborne) return m_airborneStrength;
            float grade = 1f + Mathf.Abs(slope) * (slope < 0f ? m_downhillBoost : m_uphillBoost);
            return Mathf.Clamp01(Mathf.Abs(speed)/Mathf.Max(1f,m_referenceSpeed) * m_rollingStrength * grade
                + Mathf.Clamp01(slopeChange) * m_gradeChangeBoost);
        }

        private float SlopeAt(float progress)
        {
            Vector3 tangent = m_course.Spline.EvaluateTangent(0, Mathf.Clamp01(progress));
            return tangent.sqrMagnitude > 1e-8f ? Vector3.Dot(tangent.normalized, Vector3.up) : 0f;
        }

        private void LateUpdate()
        {
            if (!m_driving || m_length <= 0f) return;
            float progress = Mathf.Clamp01(m_cart.SplineOffset/m_length);
            float slope = SlopeAt(progress);
            float slopeChange = Mathf.Abs(slope-m_previousSlope)/Mathf.Max(0.001f, Time.deltaTime);
            bool airborne = progress > m_gapStart && progress < m_gapEnd;
            if(m_verticalNoise!=null)m_verticalNoise.VerticalScale=airborne?m_fallVerticalScale:
                Mathf.Lerp(1f,m_downhillVerticalScale,Mathf.Clamp01(-slope/.5f));
            float rate = RollingRateFor(m_cart.Speed, slope, slopeChange, airborne);
            m_currentRate = Mathf.MoveTowards(m_currentRate, rate, Mathf.Max(0f, Time.deltaTime)*3f);
            m_shake.SetRateOnTrolleyShake(m_currentRate);
            if (!m_landed && m_previousProgress < m_gapEnd && progress >= m_gapEnd)
            {
                m_landed = true;
                LandingImpactCount++;
                // VHShakeの引数は横→縦→ロール。CameraManagerの引数順とは異なる。
                m_shake.VHShake(duration:m_landingDuration, horizontalIntensity:m_landingYaw,
                    verticalIntensity:m_landingPitch, forwardIntensity:m_landingRoll);
            }
            m_previousProgress = progress;
            m_previousSlope = slope;
            float look=0f;
            if(m_encounters!=null)foreach(var encounter in m_encounters)
            {
                float weight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(encounter.Begin-.025f,encounter.Begin-.004f,progress))*
                    (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(encounter.Begin,encounter.Peak,progress)));
                if(weight>0)look=(encounter.AvoidRight?-1f:1f)*m_encounterLookDegrees*weight;
            }
            m_lookYaw=Mathf.MoveTowards(m_lookYaw,look,Time.deltaTime*70f);
            if(m_lookExtension!=null)m_lookExtension.SetTiltRotationOffset(new Vector3(0,m_lookYaw,0));
            // 車体のLateUpdate後に更新し、前フレーム姿勢への追従遅れを作らない。
            if (m_manualBrainUpdate && m_brain != null && m_brain.enabled && m_brain.UpdateMethod == CinemachineBrain.UpdateMethods.ManualUpdate)
                m_brain.ManualUpdate();
        }

        private void OnDisable() { EndRide(); }

#if UNITY_EDITOR
        public void Configure(RollerCoasterCourse course, CartController cart, CameraShake shake, CinemachineCamera camera, CinemachineBrain brain)
        {
            m_course = course; m_cart = cart; m_shake = shake; m_virtualCamera = camera; m_brain = brain;
        }
#endif
    }
}
