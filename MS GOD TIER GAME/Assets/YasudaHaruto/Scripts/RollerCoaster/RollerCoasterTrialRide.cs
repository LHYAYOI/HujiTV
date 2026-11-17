using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster
{
    /// <summary>既存CartControllerを起動する試走用アダプター。演出速度は後で調整する。</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class RollerCoasterTrialRide : MonoBehaviour
    {
        [SerializeField] private RollerCoasterCourse m_course;
        [SerializeField] private CartController m_cart;
        [SerializeField] private Camera m_rideCamera;
        [SerializeField] private Light m_trialLight;
        [SerializeField] private RollerCoasterSpeedProfile m_speedProfile;
        [SerializeField] private RollerCoasterCameraMotion m_cameraMotion;
        [SerializeField, Min(0.1f), Tooltip("区間速度が無効なときの一定速度。")]
        private float m_speed = 12f;
        [SerializeField, Range(0f, 0.99f)] private float m_startProgress;
        [SerializeField, Range(0f, 30f)] private float m_fallPitch = 15f;
        private readonly List<Camera> m_disabledCameras = new List<Camera>();
        private readonly List<AudioListener> m_disabledListeners = new List<AudioListener>();
        private readonly List<CinemachineBrain> m_disabledBrains = new List<CinemachineBrain>();
        private bool m_started;
        private bool m_paused;
        private bool m_finished;
        private float m_length;
        private float m_gapStart;
        private float m_gapEnd;
        private Quaternion m_takeoffRotation;
        private Quaternion m_landingRotation;
        private Quaternion m_fallHeading;

        public CartController Cart => m_cart;
        public Camera RideCamera => m_rideCamera;
        public RollerCoasterCourse Course => m_course;
        public float Progress => m_length > 0f && m_cart != null ? Mathf.Clamp01(m_cart.SplineOffset / m_length) : 0f;
        public bool Finished => m_finished;
        public bool Paused => m_paused;

        private void Start()
        {
            if (m_course == null || m_course.Spline == null || m_cart == null || m_rideCamera == null || m_course.Spline.Spline.Count < 2)
            {
                Debug.LogError("試走のコース・トロッコ・カメラ参照を設定してください。", this);
                enabled = false;
                return;
            }
            RefreshRouteFrames();
            if (m_length <= 0f) { enabled = false; return; }
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (camera == m_rideCamera || !camera.enabled || camera.gameObject.scene != gameObject.scene) continue;
                m_disabledCameras.Add(camera);
                camera.enabled = false;
                var brain = camera.GetComponent<CinemachineBrain>();
                if (brain != null && brain.enabled) { m_disabledBrains.Add(brain); brain.enabled = false; }
            }
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (!listener.enabled || listener.gameObject == m_rideCamera.gameObject || listener.gameObject.scene != gameObject.scene) continue;
                m_disabledListeners.Add(listener);
                listener.enabled = false;
            }
            m_rideCamera.enabled = true;
            if (m_trialLight != null) m_trialLight.enabled = true;
            m_started = true;
            Restart();
        }

        private void Update()
        {
            if (!m_started) return;
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
            if (Input.GetKeyDown(KeyCode.R)) Restart();
        }

        private void LateUpdate()
        {
            if (!m_started) return;
            if (!m_paused && !m_finished && m_speedProfile != null)
            {
                if (m_speedProfile.UseSectionSpeeds) m_speedProfile.Tick(Progress, Time.deltaTime, m_speed);
                else m_cart.SetSpeed(m_speed);
            }
            ApplyPose(Progress);
            if (Progress >= 1f)
            {
                m_finished = true;
                m_cart.SetSpeed(0f);
            }
        }

        public void ApplyPose(float parameter)
        {
            Vector3 position = m_course.Spline.EvaluatePosition(0, Mathf.Clamp01(parameter));
            // 既存の回転Lerp遅延を、この確認用車両に限って補正する。
            m_cart.transform.SetPositionAndRotation(position, EvaluateRideRotation(parameter));
        }

        public void RefreshRouteFrames()
        {
            m_length = m_course.Spline.CalculateLength();
            m_gapStart = SplineUtility.ConvertIndexUnit(m_course.Spline.Spline, m_course.FallStartKnot, PathIndexUnit.Knot, PathIndexUnit.Normalized);
            m_gapEnd = SplineUtility.ConvertIndexUnit(m_course.Spline.Spline, m_course.FallEndKnot, PathIndexUnit.Knot, PathIndexUnit.Normalized);
            m_course.Spline.Evaluate(0, m_gapStart, out _, out var takeoff, out var takeoffUp);
            m_course.Spline.Evaluate(0, m_gapEnd, out _, out var landing, out var landingUp);
            m_takeoffRotation = Quaternion.LookRotation(takeoff, takeoffUp);
            m_landingRotation = Quaternion.LookRotation(landing, landingUp);
            Vector3 heading = Vector3.ProjectOnPlane(takeoff, Vector3.up);
            if (heading.sqrMagnitude < 1e-6f) heading = Vector3.ProjectOnPlane(landing, Vector3.up);
            if (heading.sqrMagnitude < 1e-6f) heading = Vector3.ProjectOnPlane(m_course.Spline.transform.forward, Vector3.up);
            if (heading.sqrMagnitude < 1e-6f) heading = Vector3.forward;
            m_fallHeading = Quaternion.LookRotation(heading.normalized, Vector3.up);
        }

        public Quaternion EvaluateRideRotation(float parameter)
        {
            parameter = Mathf.Clamp01(parameter);
            if (parameter >= m_gapStart && parameter <= m_gapEnd && m_gapEnd > m_gapStart)
            {
                float progress = Mathf.InverseLerp(m_gapStart, m_gapEnd, parameter);
                Quaternion falling = m_fallHeading * Quaternion.Euler(m_fallPitch, 0f, 0f);
                // 落下の接線はほぼ鉛直になり、水平成分が途中で反転する場合がある。
                // その接線を視線へ使わず、離陸時の方位と着地時の姿勢を直接つなぐ。
                if (progress < 0.25f)
                    return Quaternion.Slerp(m_takeoffRotation, falling, Mathf.SmoothStep(0f, 1f, progress / 0.25f));
                if (progress > 0.75f)
                    return Quaternion.Slerp(falling, m_landingRotation, Mathf.SmoothStep(0f, 1f, (progress - 0.75f) / 0.25f));
                return falling;
            }
            m_course.Spline.Evaluate(0, parameter, out _, out var tangent, out var up);
            return Quaternion.LookRotation(tangent, up);
        }

        public void TogglePause()
        {
            if (!m_started) return;
            if (m_finished) { Restart(); return; }
            m_paused = !m_paused;
            m_cart.SetSpeed(m_paused ? 0f : CurrentTargetSpeed());
        }

        public void Restart()
        {
            if (!m_started) return;
            m_paused = false;
            m_finished = false;
            m_cart.SetSpline(m_course.Spline, m_startProgress * m_length, false, true);
            if (m_speedProfile != null) m_speedProfile.Initialize(m_course, m_cart, m_startProgress);
            m_cart.SetSpeed(CurrentTargetSpeed());
            ApplyPose(m_startProgress);
            if (m_cameraMotion != null) m_cameraMotion.BeginRide(m_startProgress);
        }

        private void OnDisable()
        {
            if (m_cameraMotion != null) m_cameraMotion.EndRide();
            if (m_started && m_cart != null) m_cart.SetSpeed(0f);
            foreach (var camera in m_disabledCameras) if (camera != null) camera.enabled = true;
            foreach (var listener in m_disabledListeners) if (listener != null) listener.enabled = true;
            foreach (var brain in m_disabledBrains) if (brain != null) brain.enabled = true;
            m_disabledCameras.Clear(); m_disabledListeners.Clear();
            m_disabledBrains.Clear();
            if (m_rideCamera != null) m_rideCamera.enabled = false;
            if (m_trialLight != null) m_trialLight.enabled = false;
            m_started = false;
        }

        private void OnGUI()
        {
            if (!m_started) return;
            GUI.Box(new Rect(12f, 12f, 340f, 76f), "Trial Ride  " + Mathf.RoundToInt(Progress * 100f) + "%   Speed " + m_cart.Speed.ToString("F1"));
            if (GUI.Button(new Rect(24f, 43f, 148f, 30f), m_finished ? "Restart (Space)" : m_paused ? "Resume (Space)" : "Pause (Space)")) TogglePause();
            if (GUI.Button(new Rect(184f, 43f, 156f, 30f), "Restart (R)")) Restart();
        }

#if UNITY_EDITOR
        public void SetSpeedProfile(RollerCoasterSpeedProfile profile) { m_speedProfile = profile; }
        public void SetCameraMotion(RollerCoasterCameraMotion motion) { m_cameraMotion = motion; }
        public void Configure(RollerCoasterCourse course, CartController cart, Camera camera, Light light)
        {
            m_course = course; m_cart = cart; m_rideCamera = camera; m_trialLight = light;
        }
#endif
        private float CurrentTargetSpeed() => m_speedProfile != null ? m_speedProfile.CurrentSpeed(Progress, m_speed) : m_speed;
    }
}
