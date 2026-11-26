using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster.Editor
{
    [InitializeOnLoad]
    internal static class RollerCoasterTrialValidation
    {
        private const string Folder = "Temp/RollerCoasterTrialValidation";
        private const string Active = "Yasuda.RollerCoasterTrial.Active";
        private static double s_begin;
        private static double s_next;
        private static int s_pauseStage;
        private static float s_pausedProgress;
        private static double s_pauseTime;
        private static bool s_visitedGap;
        private static bool s_returned;
        private static int s_errors;
        private static bool s_previousWasInGap;
        private static Quaternion s_previousCameraRotation;
        private static float s_maxFallCameraStep;
        private static bool s_observedCrestStop;
        private static float s_minPositiveSpeed = float.PositiveInfinity;
        private static float s_maxSpeed;
        private static bool s_observedLandingStop;
        private static bool s_observedSlowLaunch;
        private static double s_landingHoldStart;
        private static int s_landingPauseStage;
        private static double s_landingPauseTime;
        private static float s_landingPausedProgress;
        private static float s_maxRollingRate;
        private static float s_maxCameraCorrection;
        private static float s_lastApproachSpeed = float.PositiveInfinity;
        private static bool s_observedApproachDeceleration;
        private static float s_lastLaunchSpeed;
        private static bool s_observedLaunchAcceleration;


        private static float s_minEncounterDistance = float.PositiveInfinity;


        private static int s_environmentCapture;
        private static float s_visibleOpponentSpeed;
        private static float s_minViewClearance = float.PositiveInfinity;
        private static float[] s_closeDistances = new[] { float.PositiveInfinity, float.PositiveInfinity };
        private static bool[] s_closeCaptured = new bool[2];
        private static bool s_downhillVerticalBoost;
        private static bool s_fallVerticalBoost;
        private static bool[] s_seen = new bool[2], s_passed = new bool[2];
        private static int[] s_captures = new int[2];


        static RollerCoasterTrialValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                    s_begin = EditorApplication.timeSinceStartup;
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Active, false))
                {
                    File.AppendAllText(Folder + "/result.txt", "Stopped early before completing the test\n");
                    SessionState.SetBool(Active, false);
                }
            };
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (!SessionState.GetBool(Active, false) || !Application.isPlaying)
                    return;
                if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
                    return;
                s_errors++;
                File.AppendAllText(Folder + "/result.txt", "RUNTIME ERROR: " + message + "\n");
            };
        }


        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false))
            {
                string request = Folder + "/request.txt";
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(request))
                    return;
                File.Delete(request);
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Folder + "/result.txt", "Trial ride Play Mode validation\n");
                if (!UnityEngine.Object.FindObjectsByType<RollerCoasterTrialRide>(FindObjectsSortMode.None).Any())
                {
                    File.AppendAllText(Folder + "/result.txt", "FAIL No trial ride\n");
                    return;
                }
                SessionState.SetBool(Active, true);
                s_begin = 0d;
                s_next = 0d;
                s_pauseStage = 0;
                s_visitedGap = false;
                s_returned = false;
                s_errors = 0;
                s_previousWasInGap = false;
                s_maxFallCameraStep = 0f;
                s_observedCrestStop = false;
                s_minPositiveSpeed = float.PositiveInfinity;
                s_maxSpeed = 0f;
                s_observedLandingStop = false;
                s_observedSlowLaunch = false;
                s_landingHoldStart = 0d;
                s_landingPauseStage = 0;
                s_maxRollingRate = 0f;
                s_maxCameraCorrection = 0f;
                s_lastApproachSpeed = float.PositiveInfinity;
                s_observedApproachDeceleration = false;
                s_lastLaunchSpeed = 0f;
                s_observedLaunchAcceleration = false;
                s_minEncounterDistance = float.PositiveInfinity;
                s_minViewClearance = float.PositiveInfinity;
                s_closeDistances = new[] { float.PositiveInfinity, float.PositiveInfinity };
                s_closeCaptured = new bool[2];

                s_environmentCapture = 0;
                s_visibleOpponentSpeed = 0;
                s_downhillVerticalBoost = false;
                s_fallVerticalBoost = false;
                s_seen = new bool[2];
                s_passed = new bool[2];
                s_captures = new int[2];
                File.WriteAllText(Folder + "/telemetry.txt", "");
                EditorApplication.isPlaying = true;
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
                return;
            if (s_begin <= 0d)
                s_begin = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - s_begin > 100d)
            {
                Finish("FAIL Timed out");
                return;
            }
            var ride = UnityEngine.Object.FindFirstObjectByType<RollerCoasterTrialRide>();
            if (ride == null || ride.Cart == null || ride.Progress <= 0f)
                return;
            try
            {
                if (!ride.RideCamera.enabled)
                    throw new InvalidOperationException("Trial camera is disabled");
                var profile = ride.GetComponent<RollerCoasterSpeedProfile>();
                if (s_environmentCapture == 0 && ride.Progress > .15f)
                {
                    ScreenCapture.CaptureScreenshot(Folder + "/presentation-descent.png");
                    s_environmentCapture = 1;
                }
                if (s_environmentCapture == 1 && ride.Progress > .57f)
                {
                    ScreenCapture.CaptureScreenshot(Folder + "/presentation-fall.png");
                    s_environmentCapture = 2;
                }
                var encounters = ride.Course.GetComponentsInChildren<RollerCoasterEncounter>(true).OrderBy(x => x.Number).ToArray();
                if (encounters.Length != 2)
                    throw new InvalidOperationException("Expected two encounters");
                foreach (var encounter in encounters)
                {
                    int index = encounter.Number - 1;
                    bool captureQueued = false;
                    s_seen[index] |= encounter.Seen;
                    s_passed[index] |= encounter.Passed;
                    if (!encounter.BranchesVisible)
                        throw new InvalidOperationException("A branch rail became hidden");
                    s_visibleOpponentSpeed = Mathf.Max(s_visibleOpponentSpeed, encounter.CurrentSpeed);
                    float captureAt = s_captures[index] == 0 ? encounter.Begin - .015f : s_captures[index] == 1 ? encounter.Begin + .002f : encounter.End;
                    if (s_captures[index] < 3 && ride.Progress >= captureAt)
                    {
                        ScreenCapture.CaptureScreenshot(Folder + $"/encounter-{encounter.Number}-{s_captures[index]}.png");
                        s_captures[index]++;
                        captureQueued = true;
                    }
                    if (encounter.OncomingCart.gameObject.activeSelf)
                    {
                        s_minEncounterDistance = Mathf.Min(s_minEncounterDistance, Vector3.Distance(ride.Cart.transform.position, encounter.OncomingCart.position));
                        float rootDistance = Vector3.Distance(ride.Cart.transform.position, encounter.OncomingCart.position);
                        if (rootDistance < s_closeDistances[index])
                        {
                            s_closeDistances[index] = rootDistance;
                            if (!captureQueued && !s_closeCaptured[index] && rootDistance < 2.8f)
                            {
                                ScreenCapture.CaptureScreenshot(Folder + $"/close-pass-{encounter.Number}.png");
                                s_closeCaptured[index] = true;
                            }
                        }
                        Vector3 eye = encounter.OncomingCart.InverseTransformPoint(ride.RideCamera.transform.position) - new Vector3(0, 1.1f, 0);
                        Vector3 outside = new Vector3(Mathf.Max(0, Mathf.Abs(eye.x) - 1f), Mathf.Max(0, Mathf.Abs(eye.y) - 1.2f), Mathf.Max(0, Mathf.Abs(eye.z) - 1.2f));
                        s_minViewClearance = Mathf.Min(s_minViewClearance, outside.magnitude);
                        if (outside.magnitude < .2f)
                            throw new InvalidOperationException("Oncoming cart intersects the ride viewpoint");
                    }
                }
                float crestParameter = SplineUtility.ConvertIndexUnit(ride.Course.Spline.Spline,
                    ride.Course.CrestKnot, PathIndexUnit.Knot, PathIndexUnit.Normalized);
                if (profile != null && !ride.Paused && ride.Progress < crestParameter && s_pauseStage == 2)
                {
                    if (ride.Cart.Speed > s_lastApproachSpeed + 0.02f)
                        throw new InvalidOperationException("Crest approach accelerated instead of slowing");
                    if (ride.Cart.Speed < 1f)
                        s_observedApproachDeceleration = true;
                    s_lastApproachSpeed = ride.Cart.Speed;
                }
                if (profile != null && profile.LaunchingAfterLanding && !profile.HoldingAfterLanding && !ride.Paused)
                {
                    if (ride.Cart.Speed + 0.02f < s_lastLaunchSpeed)
                        throw new InvalidOperationException("Landing launch speed decreased");
                    if (ride.Cart.Speed > profile.LandingStartSpeed + 3f)
                        s_observedLaunchAcceleration = true;
                    s_lastLaunchSpeed = ride.Cart.Speed;
                }
                var motion = ride.GetComponent<RollerCoasterCameraMotion>();
                if (motion != null)
                {
                    var vertical = motion.VirtualCamera.GetComponent<RollerCoasterVerticalNoiseExtension>();
                    if (vertical != null && vertical.isActiveAndEnabled)
                    {
                        if (!s_visitedGap && vertical.VerticalScale > 1.4f)
                            s_downhillVerticalBoost = true;
                        if (s_previousWasInGap && vertical.VerticalScale > 1.2f)
                            s_fallVerticalBoost = true;
                    }
                    s_maxRollingRate = Mathf.Max(s_maxRollingRate, motion.CurrentRollingRate);
                    s_maxCameraCorrection = Mathf.Max(s_maxCameraCorrection,
                        Quaternion.Angle(Quaternion.identity, motion.VirtualCamera.State.OrientationCorrection));
                }
                if (profile != null && profile.HoldingAtCrest)
                {
                    s_observedCrestStop = true;
                    if (ride.Cart.Speed != 0f)
                        throw new InvalidOperationException("Crest hold did not stop the cart");
                }
                if (profile != null && profile.HoldingAfterLanding)
                {
                    if (!s_observedLandingStop)
                        s_landingHoldStart = EditorApplication.timeSinceStartup;
                    s_observedLandingStop = true;
                    if (ride.Cart.Speed != 0f)
                        throw new InvalidOperationException("Landing hold did not stop the cart");
                    if (s_landingPauseStage == 0)
                    {
                        ride.TogglePause();
                        s_landingPausedProgress = ride.Progress;
                        s_landingPauseTime = EditorApplication.timeSinceStartup;
                        s_landingPauseStage = 1;
                    }
                }
                if (s_landingPauseStage == 1 && EditorApplication.timeSinceStartup - s_landingPauseTime > 0.25d)
                {
                    if (Mathf.Abs(ride.Progress - s_landingPausedProgress) > 0.00001f)
                        throw new InvalidOperationException("Pause moved the landed cart");
                    ride.TogglePause();
                    s_landingPauseStage = 2;
                    File.AppendAllText(Folder + "/result.txt", "PASS Pause/resume during landing stop holds position\n");
                }
                if (profile != null && s_observedLandingStop && !s_observedSlowLaunch && profile.LaunchingAfterLanding &&
                    !profile.HoldingAfterLanding && ride.Cart.Speed > 0.01f)
                {
                    if (ride.Cart.Speed > profile.LandingStartSpeed + 1.5f)
                        throw new InvalidOperationException("Landing launch started too fast");
                    if (EditorApplication.timeSinceStartup - s_landingHoldStart < profile.LandingStopSeconds - 0.15f)
                        throw new InvalidOperationException("Landing stop was too short");
                    s_observedSlowLaunch = true;
                    File.AppendAllText(Folder + "/result.txt", $"PASS Landing stop then slow launch at speed {ride.Cart.Speed:F3}\n");
                }
                if (ride.Cart.Speed > 0.01f && !ride.Paused)
                {
                    s_minPositiveSpeed = Mathf.Min(s_minPositiveSpeed, ride.Cart.Speed);
                    s_maxSpeed = Mathf.Max(s_maxSpeed, ride.Cart.Speed);
                }
                if (s_pauseStage == 0 && ride.Progress > 0.005f)
                {
                    ride.TogglePause();
                    s_pausedProgress = ride.Progress;
                    s_pauseTime = EditorApplication.timeSinceStartup;
                    s_pauseStage = 1;
                }
                else if (s_pauseStage == 1 && EditorApplication.timeSinceStartup - s_pauseTime > 0.3d)
                {
                    if (Mathf.Abs(ride.Progress - s_pausedProgress) > 0.00001f)
                        throw new InvalidOperationException("Pause did not stop the cart");
                    File.AppendAllText(Folder + "/result.txt", "PASS Pause holds position; resume continues\n");
                    ride.TogglePause();
                    s_pauseStage = 2;
                }
                var spline = ride.Course.Spline;
                float gapStart = SplineUtility.ConvertIndexUnit(spline.Spline, ride.Course.FallStartKnot, PathIndexUnit.Knot, PathIndexUnit.Normalized);
                float gapEnd = SplineUtility.ConvertIndexUnit(spline.Spline, ride.Course.FallEndKnot, PathIndexUnit.Knot, PathIndexUnit.Normalized);
                bool inGap = ride.Progress > gapStart && ride.Progress < gapEnd;
                if (inGap)
                {
                    s_visitedGap = true;
                    if (s_previousWasInGap)
                        s_maxFallCameraStep = Mathf.Max(s_maxFallCameraStep,
                        Quaternion.Angle(s_previousCameraRotation, ride.RideCamera.transform.rotation));
                    spline.Evaluate(0, gapStart, out _, out var takeoff, out _);
                    Vector3 expectedHeading = Vector3.ProjectOnPlane(takeoff, Vector3.up).normalized;
                    Vector3 cameraHeading = Vector3.ProjectOnPlane(ride.RideCamera.transform.forward, Vector3.up).normalized;
                    if (Vector3.Dot(expectedHeading, cameraHeading) < 0.95f)
                        throw new InvalidOperationException("Fall camera reversed its heading");
                }
                s_previousWasInGap = inGap;
                s_previousCameraRotation = ride.RideCamera.transform.rotation;
                if (ride.Progress > gapEnd + 0.02f)
                    s_returned = true;
                if (EditorApplication.timeSinceStartup >= s_next)
                {
                    s_next = EditorApplication.timeSinceStartup + 1d;
                    var position = ride.Cart.transform.position;
                    File.AppendAllText(Folder + "/telemetry.txt", $"{ride.Progress:F4}: {position}, speed={ride.Cart.Speed:F3}\n");
                }
                if (ride.Finished)
                {
                    File.AppendAllText(Folder + "/result.txt", $"Close pass roots {s_closeDistances[0]:F3}/{s_closeDistances[1]:F3}m, view clearance {s_minViewClearance:F3}m\n");
                    if (s_seen.Any(x => !x) || s_passed.Any(x => !x))
                        throw new InvalidOperationException("Both encounters must complete");
                    if (s_visibleOpponentSpeed > encounters.Max(x => x.ApproachSpeed) + .5f)
                        throw new InvalidOperationException("Oncoming cart exceeded its approach speed");
                    File.AppendAllText(Folder + "/result.txt", $"PASS Both encounters completed with permanently visible branches; clearance {s_minEncounterDistance:F3}m, opponent speed peak {s_visibleOpponentSpeed:F3}m/s\n");
                    if (motion != null)
                    {
                        if (!s_downhillVerticalBoost || !s_fallVerticalBoost)
                            throw new InvalidOperationException("Phase-specific vertical shake boosts were not observed");
                        File.AppendAllText(Folder + "/result.txt", "PASS Vertical-only shake boost observed on downhill and during fall\n");
                        if (motion.LandingImpactCount != 1)
                            throw new InvalidOperationException("Landing shake did not fire exactly once");
                        if (s_maxRollingRate < 0.2f || s_maxCameraCorrection < 0.01f)
                            throw new InvalidOperationException("Camera shake did not reach the Cinemachine camera");
                        File.AppendAllText(Folder + "/result.txt", $"PASS Hamada rolling shake (rate peak {s_maxRollingRate:F3}, correction {s_maxCameraCorrection:F3} degrees), one landing impact\n");
                    }
                    if (!s_visitedGap || !s_returned)
                        throw new InvalidOperationException("The full route was not visited");
                    if (s_errors != 0)
                        throw new InvalidOperationException("Runtime errors were logged: " + s_errors);
                    if (profile != null && profile.UseSectionSpeeds)
                    {
                        if (!s_observedApproachDeceleration || !s_observedLaunchAcceleration)
                            throw new InvalidOperationException("Progressive crest braking and landing acceleration were not observed");
                        File.AppendAllText(Folder + "/result.txt", "PASS Crest approach progressively slows below 1; landing launch gradually accelerates\n");
                        if (profile.CrestStopSeconds > 0f && !s_observedCrestStop)
                            throw new InvalidOperationException("Crest stop was not observed");
                        if (profile.LandingStopSeconds > 0f && (!s_observedLandingStop || !s_observedSlowLaunch))
                            throw new InvalidOperationException("Landing stop and slow launch were not observed");
                        if (s_maxSpeed - s_minPositiveSpeed < 5f)
                            throw new InvalidOperationException("Section speeds did not vary");
                        File.AppendAllText(Folder + "/result.txt", $"PASS Crest stop and varying section speeds ({s_minPositiveSpeed:F3}..{s_maxSpeed:F3})\n");
                    }
                    if (s_maxFallCameraStep > 10f)
                        throw new InvalidOperationException("Fall camera angular jump: " + s_maxFallCameraStep);
                    File.AppendAllText(Folder + "/result.txt", $"PASS Stable fall camera heading; max observed angular step {s_maxFallCameraStep:F4} degrees\n");
                    Vector3 end = spline.EvaluatePosition(0, 1f);
                    if (Vector3.Distance(ride.Cart.transform.position, end) > 0.01f)
                        throw new InvalidOperationException("Cart did not stop at the exit");
                    File.AppendAllText(Folder + "/result.txt", "PASS Entry, spiral, trackless fall, returning hills and exit reached\nPASS Exit position and automatic stop\nPASS No runtime errors\n");
                    ride.Restart();
                    if (ride.Progress > 0.0001f)
                        throw new InvalidOperationException("Restart did not return to entry");
                    Finish("PASS Restart returns to entry\nTrial Ride COMPLETE");
                }
            }
            catch (Exception exception) { Finish("FAIL " + exception.Message); }
        }


        private static void Finish(string message)
        {
            File.AppendAllText(Folder + "/result.txt", message + "\n");
            SessionState.SetBool(Active, false);
            EditorApplication.isPlaying = false;
        }
    }
}
