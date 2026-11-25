using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster
{
    /// <summary>HamadaのCameraShakeを走行進捗から制御する。車体姿勢には揺れを加えない。</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class RollerCoasterCameraMotion : MonoBehaviour
    {
        public enum CURVE_CAMERA_MODE { FOLLOW_CART, FIXED_ROLL, DELAYED_ROLL }

        [Header("連続カーブのカメラ")]
        [SerializeField] private CURVE_CAMERA_MODE m_curveCameraMode = CURVE_CAMERA_MODE.DELAYED_ROLL;
        [SerializeField, Range(.05f, 1.5f)] private float m_bankDelaySeconds = .55f;
        [SerializeField, Range(10f, 120f)] private float m_bankMaxDegreesPerSecond = 45f;
        private float m_cameraBank;
        private float m_bankVelocity;
        private Quaternion m_baseCameraRotation;

        public CURVE_CAMERA_MODE CurveCameraMode { get => m_curveCameraMode; set => m_curveCameraMode = value; }
        public float CameraBankDegrees => m_cameraBank;
        public float CameraBankAngularSpeed { get; private set; }

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
        [SerializeField, Range(0f, 0.5f)] private float m_airborneStrength = 0.5f;
        [SerializeField, Range(1f, 3f)] private float m_downhillVerticalScale = 1.5f;
        [SerializeField, Range(1f, 3f)] private float m_fallVerticalScale = 1.8f;
        [SerializeField, Range(0f, 5f)] private float m_fallShakeDegrees = 1.8f;
        private float m_fallShakeElapsed;
        public Vector3 FallShakeRotation { get; private set; }
        public Vector3 LandingShakeRotation { get; private set; }
        private RollerCoasterVerticalNoiseExtension m_verticalNoise;
        [SerializeField, Range(0f, 25f)] private float m_encounterLookDegrees = 15f;
        private RollerCoasterEncounter[] m_encounters;
        private CinemachineRotationExtension m_lookExtension;
        private float m_lookYaw;

        [Header("着地の衝撃（回転の強さ、度）")]
        [SerializeField, Range(0.1f, 1f)] private float m_landingDuration = 0.7f;
        [SerializeField, Range(0f, 12f)] private float m_landingPitch = 8f;
        [SerializeField, Range(0f, 5f)] private float m_landingYaw = 1.4f;
        [SerializeField, Range(0f, 5f)] private float m_landingRoll = 2.2f;
        [SerializeField, Range(0f, .6f)] private float m_landingSinkMeters = .3f;

        [Header("試走構成のカメラ管理")]
        [SerializeField] private bool m_manageCameraActivation = true;
        [SerializeField] private bool m_manualBrainUpdate = true;

        [Header("落下の浮遊感")]
        [SerializeField, Range(0f, .6f)] private float m_floatHeight = .32f;
        [SerializeField, Range(0f, 25f)] private float m_fallFovBoost = 16f;

        [Header("落下前ブレーキの慣性")]
        [SerializeField, Range(0f, .6f)] private float m_brakeForward = .32f;
        [SerializeField, Range(0f, 15f)] private float m_brakePitch = 7f;
        [SerializeField, Range(0f, 2f)] private float m_brakeRattleDegrees = .55f;
        private RollerCoasterSpeedProfile m_speedProfile;

        public Vector3 BrakeRattleRotation
        {
            get; private set;
        }

        public float BrakeLeanWeight
        {
            get; private set;
        }
        private Vector3 m_baseCameraPosition;
        private float m_baseFov;
        private bool m_cameraBaselineSaved;
        private float m_landingElapsed = 10f;
        private bool m_driving;
        private bool m_landed;
        private float m_length;
        private float m_gapStart;
        private float m_gapEnd;
        private float m_previousProgress;
        private float m_previousSlope;
        private float m_currentRate;


        public float CurrentRollingRate => m_currentRate;

        public int LandingImpactCount
        {
            get; private set;
        }

        public float LandingEffectElapsed => m_landingElapsed;

        public CinemachineCamera VirtualCamera => m_virtualCamera;

        public CameraShake Shake => m_shake;


        public void BeginRide(float startProgress)
        {
            if (m_course == null || m_course.Spline == null || m_cart == null || m_shake == null || m_virtualCamera == null)
                return;
            if (!m_cameraBaselineSaved)
            {
                m_baseCameraPosition = m_virtualCamera.transform.localPosition;
                m_baseCameraRotation = m_virtualCamera.transform.localRotation;
                m_baseFov = m_virtualCamera.Lens.FieldOfView;
                m_cameraBaselineSaved = true;
            }
            m_virtualCamera.transform.localPosition = m_baseCameraPosition;
            m_virtualCamera.transform.localRotation = m_baseCameraRotation;
            var story = GetComponent<RollerCoasterStoryboard>();
            m_cameraBank = story != null ? story.RideBankDegreesAt(startProgress) : 0f;
            m_bankVelocity = 0f;
            CameraBankAngularSpeed = 0f;
            var resetLens = m_virtualCamera.Lens;
            resetLens.FieldOfView = m_baseFov;
            m_virtualCamera.Lens = resetLens;
            m_landingElapsed = 10f;
            m_fallShakeElapsed = 0;
            FallShakeRotation = LandingShakeRotation = Vector3.zero;
            BrakeRattleRotation = Vector3.zero;
            BrakeLeanWeight = 0f;
            m_speedProfile = GetComponent<RollerCoasterSpeedProfile>();
            m_length = m_course.Spline.CalculateLength();
            m_gapStart = m_course.FallLaunchParameter;
            m_gapEnd = SplineUtility.ConvertIndexUnit(m_course.Spline.Spline, m_course.FallEndKnot, PathIndexUnit.Knot, PathIndexUnit.Normalized);
            m_landed = startProgress >= m_gapEnd;
            m_previousProgress = startProgress;
            m_previousSlope = SlopeAt(startProgress);
            m_currentRate = 0f;
            LandingImpactCount = 0;
            m_driving = true;
            m_verticalNoise = m_virtualCamera.GetComponent<RollerCoasterVerticalNoiseExtension>();
            m_encounters = GetComponent<RollerCoasterStoryboard>() != null ? new RollerCoasterEncounter[0] : m_course.GetComponentsInChildren<RollerCoasterEncounter>(true);
            m_lookExtension = m_virtualCamera.GetComponent<CinemachineRotationExtension>();
            m_lookYaw = 0f;
            m_shake.StopAllCoroutines();
            if (m_verticalNoise != null)
                m_verticalNoise.VerticalScale = 1f;
            if (m_lookExtension != null)
            {
                m_lookExtension.SetShakeRotationOffset(Vector3.zero);
                m_lookExtension.SetTiltRotationOffset(Vector3.zero);
            }
            m_shake.SetRateOnTrolleyShake(0f);
            m_virtualCamera.PreviousStateIsValid = false;
            if (m_manageCameraActivation)
            {
                m_virtualCamera.enabled = true;
                if (m_brain != null)
                    m_brain.enabled = true;
            }
        }


        public void EndRide()
        {
            m_driving = false;
            m_fallShakeElapsed = 0;
            FallShakeRotation = LandingShakeRotation = Vector3.zero;
            m_currentRate = 0f;
            BrakeRattleRotation = Vector3.zero;
            BrakeLeanWeight = 0f;
            if (m_cameraBaselineSaved && m_virtualCamera != null)
            {
                m_virtualCamera.transform.localPosition = m_baseCameraPosition;
                m_virtualCamera.transform.localRotation = m_baseCameraRotation;
                var lens = m_virtualCamera.Lens;
                lens.FieldOfView = m_baseFov;
                m_virtualCamera.Lens = lens;
            }
            if (m_verticalNoise != null)
                m_verticalNoise.VerticalScale = 1f;
            if (m_lookExtension != null)
            {
                m_lookExtension.SetTiltRotationOffset(Vector3.zero);
                m_lookExtension.SetShakeRotationOffset(Vector3.zero);
            }
            if (m_shake != null)
                m_shake.SetRateOnTrolleyShake(0f);
            if (m_manageCameraActivation)
            {
                if (m_virtualCamera != null)
                    m_virtualCamera.enabled = false;
                if (m_brain != null)
                    m_brain.enabled = false;
            }
        }


        public float RollingRateFor(float speed, float slope, float slopeChange, bool airborne)
        {
            if (Mathf.Abs(speed) < 0.05f)
                return 0f;
            if (airborne)
                return m_airborneStrength;
            float grade = 1f + Mathf.Abs(slope) * (slope < 0f ? m_downhillBoost : m_uphillBoost);
            return Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(1f, m_referenceSpeed) * m_rollingStrength * grade
                + Mathf.Clamp01(slopeChange) * m_gradeChangeBoost);
        }


        private float SlopeAt(float progress)
        {
            Vector3 tangent = m_course.Spline.EvaluateTangent(0, Mathf.Clamp01(progress));
            return tangent.sqrMagnitude > 1e-8f ? Vector3.Dot(tangent.normalized, Vector3.up) : 0f;
        }


        private void LateUpdate()
        {
            if (!m_driving || m_length <= 0f)
                return;
            float progress = Mathf.Clamp01(m_cart.SplineOffset / m_length);
            float slope = SlopeAt(progress);
            float slopeChange = Mathf.Abs(slope - m_previousSlope) / Mathf.Max(0.001f, Time.deltaTime);
            bool airborne = progress > m_gapStart && progress < m_gapEnd;
            if (m_verticalNoise != null)
                m_verticalNoise.VerticalScale = airborne ? m_fallVerticalScale :
                Mathf.Lerp(1f, m_downhillVerticalScale, Mathf.Clamp01(-slope / .5f));
            float rate = RollingRateFor(m_cart.Speed, slope, slopeChange, airborne);
            m_currentRate = Mathf.MoveTowards(m_currentRate, rate, Mathf.Max(0f, Time.deltaTime) * 3f);
            m_shake.SetRateOnTrolleyShake(m_currentRate);
            if (!m_landed && m_previousProgress < m_gapEnd && progress >= m_gapEnd)
            {
                m_landed = true;
                LandingImpactCount++;
                m_landingElapsed = 0f;
                // VHShakeの引数は横→縦→ロール。CameraManagerの引数順とは異なる。
                // 着地演出はこのコンポーネントの時計で管理し、再試走時に残さない。
            }
            m_previousProgress = progress;
            m_previousSlope = slope;
            var storyLook = GetComponent<RollerCoasterStoryboard>();
            float look = storyLook != null ? storyLook.LookYawAt(progress) : 0f;
            if (m_encounters != null)
                foreach (var encounter in m_encounters)
                {
                    float weight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(encounter.Begin - .025f, encounter.Begin - .004f, progress)) *
                        (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(encounter.Begin, encounter.Peak, progress)));
                    if (weight > 0)
                        look = (encounter.AvoidRight ? -1f : 1f) * m_encounterLookDegrees * weight;
                }
            m_lookYaw = Mathf.MoveTowards(m_lookYaw, look, Time.deltaTime * 70f);
            float air = airborne ? Mathf.InverseLerp(m_gapStart, m_gapEnd, progress) : 0f;
            float airWeight = airborne ? Mathf.Sin(Mathf.PI * air) : 0f;
            var ride = GetComponent<RollerCoasterTrialRide>();
            if (ride == null || !ride.Paused)
                m_landingElapsed += Time.deltaTime;
            float impact = Mathf.Clamp01(1f - m_landingElapsed / Mathf.Max(.1f, m_landingDuration));
            float rattle = 0f, rattleTime = 0f;
            if (m_speedProfile != null && m_speedProfile.DropBrakeStarted && m_speedProfile.DropStage == RollerCoasterSpeedProfile.DROP_STAGE.APPROACH)
            {
                rattleTime = m_speedProfile.DropBrakeElapsed;
                rattle = Mathf.Sin(Mathf.PI * m_speedProfile.DropBrakeProgress);
            }
            var storyboard = GetComponent<RollerCoasterStoryboard>();
            var breakthrough = GetComponent<RollerCoasterBreakthroughSequence>();
            ApplyCurveCameraBank(storyboard, progress, ride != null && ride.Paused);
            if (storyboard != null && storyboard.BrakeFailing && breakthrough == null)
            {
                rattleTime = storyboard.BrakePhase * 2f;
                rattle = 1f;
            }
            // 制動・着地とは別の復帰振動。強さはStoryboard側で調整し、ここで重ね合わせる。
            Vector3 bankJolt = storyboard != null ? storyboard.BankReturnShakeAt(progress) : Vector3.zero;
            float bankDip = storyboard != null ? storyboard.BankReturnDipAt(progress) : 0;
            // 比較用の2案では復帰衝撃を載せず、車体とカメラの傾きの差を見やすくする。
            if (m_curveCameraMode != CURVE_CAMERA_MODE.FOLLOW_CART)
            {
                bankJolt = Vector3.zero;
                bankDip = 0;
            }
            BrakeRattleRotation = new Vector3(Mathf.Sin(rattleTime * 2f * Mathf.PI * 23f), .3f * Mathf.Sin(rattleTime * 2f * Mathf.PI * 29f),
                .5f * Mathf.Sin(rattleTime * 2f * Mathf.PI * 18f)) * m_brakeRattleDegrees * rattle;
            if (airborne && (ride == null || !ride.Paused)) m_fallShakeElapsed += Time.deltaTime;
            if (!airborne) m_fallShakeElapsed = 0;
            float fallEnvelope = airborne ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(air / .12f)) * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.85f, 1f, air))) : 0;
            FallShakeRotation = new Vector3(Mathf.Sin(m_fallShakeElapsed * 47f), .45f * Mathf.Sin(m_fallShakeElapsed * 37f), .65f * Mathf.Sin(m_fallShakeElapsed * 53f)) * m_fallShakeDegrees * fallEnvelope;
            LandingShakeRotation = new Vector3(
                Mathf.Sin(m_landingElapsed * 45f) * m_landingPitch,
                Mathf.Sin(m_landingElapsed * 37f) * m_landingYaw,
                Mathf.Sin(m_landingElapsed * 41f) * m_landingRoll) * impact * impact;
            if (m_lookExtension != null)
                m_lookExtension.SetShakeRotationOffset(LandingShakeRotation + FallShakeRotation + BrakeRattleRotation + bankJolt + (breakthrough != null ? breakthrough.ImpactRotation : Vector3.zero));
            float sink = m_landingElapsed < .8f ? -m_landingSinkMeters * Mathf.Sin(Mathf.PI * m_landingElapsed / .8f) : 0f;
            float brake = 0f;
            if (m_speedProfile != null && m_speedProfile.DropBrakeStarted)
            {
                float elapsed = m_speedProfile.DropBrakeElapsed;
                float recovery = progress >= m_gapEnd ? 1f : airborne ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.65f, 1f, air)) : 0f;
                brake = Mathf.SmoothStep(0f, 1f, elapsed / .14f) * (1f - recovery);
            }
            BrakeLeanWeight = brake;
            m_virtualCamera.transform.localPosition = m_baseCameraPosition + new Vector3(0, m_floatHeight * airWeight + sink + bankDip - .06f * brake + .009f * rattle * Mathf.Sin(rattleTime * 2f * Mathf.PI * 27f), m_brakeForward * brake);
            if (breakthrough != null) m_virtualCamera.transform.localPosition += breakthrough.ImpactPosition;
            var lens = m_virtualCamera.Lens;
            lens.FieldOfView = m_baseFov + Mathf.Clamp01(m_cart.Speed / 36f) * 5f + m_fallFovBoost * airWeight;
            m_virtualCamera.Lens = lens;
            if (m_lookExtension != null)
                m_lookExtension.SetTiltRotationOffset(new Vector3(m_brakePitch * brake, m_lookYaw, 3f * airWeight * Mathf.Sin(air * Mathf.PI * 2f)));
            // 車体のLateUpdate後に更新し、前フレーム姿勢への追従遅れを作らない。
            if (m_manualBrainUpdate && m_brain != null && m_brain.enabled && m_brain.UpdateMethod == CinemachineBrain.UpdateMethods.ManualUpdate)
                m_brain.ManualUpdate();
        }


        private void OnDisable()
        {
            EndRide();
        }

        // 車体のピッチ・進行方向は維持し、カメラのロールだけを車体から分離する。
        private void ApplyCurveCameraBank(RollerCoasterStoryboard story, float progress, bool paused)
        {
            if (story == null || !story.enabled)
                return;
            float cartBank = story.RideBankDegreesAt(progress);
            float distanceIn = (progress - story.Markers[1]) * m_length;
            float distanceOut = (story.Markers[3] - progress) * m_length;
            float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Min(distanceIn, distanceOut) / 6f));
            float target = m_curveCameraMode == CURVE_CAMERA_MODE.FIXED_ROLL ? Mathf.LerpAngle(cartBank, 0, weight) : cartBank;
            float previousBank = m_cameraBank;
            if (m_curveCameraMode == CURVE_CAMERA_MODE.DELAYED_ROLL)
            {
                if (!paused)
                    // 車体の素早いバンク復帰も、カメラ側では角速度を制限して連続的につなぐ。
                    m_cameraBank = Mathf.SmoothDampAngle(m_cameraBank, target, ref m_bankVelocity, m_bankDelaySeconds,
                        m_bankMaxDegreesPerSecond, Mathf.Min(Time.deltaTime, .05f));
            }
            else
            {
                m_cameraBank = target;
                m_bankVelocity = 0;
            }
            CameraBankAngularSpeed = !paused && m_curveCameraMode == CURVE_CAMERA_MODE.DELAYED_ROLL && Time.deltaTime > 0 ?
                Mathf.DeltaAngle(previousBank, m_cameraBank) / Time.deltaTime : 0;
            m_virtualCamera.transform.localRotation = Quaternion.AngleAxis(Mathf.DeltaAngle(cartBank, m_cameraBank), Vector3.forward) * m_baseCameraRotation;
        }

        private void OnGUI()
        {
            if (!m_driving || GetComponent<RollerCoasterStoryboard>() == null)
                return;
            string label = m_curveCameraMode == CURVE_CAMERA_MODE.FIXED_ROLL ? "水平を保つ" : m_curveCameraMode == CURVE_CAMERA_MODE.DELAYED_ROLL ? "遅れて傾く" : "車体と同時";
            if (GUI.Button(new Rect(12, 130, 340, 28), "カーブ視点：" + label))
                m_curveCameraMode = m_curveCameraMode == CURVE_CAMERA_MODE.FIXED_ROLL ? CURVE_CAMERA_MODE.DELAYED_ROLL : CURVE_CAMERA_MODE.FIXED_ROLL;
        }


#if UNITY_EDITOR
        public void Configure(RollerCoasterCourse course, CartController cart, CameraShake shake, CinemachineCamera camera, CinemachineBrain brain)
        {
            m_course = course;
            m_cart = cart;
            m_shake = shake;
            m_virtualCamera = camera;
            m_brain = brain;
        }
#endif
    }
}
