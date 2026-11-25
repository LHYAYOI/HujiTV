using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YasudaHaruto.RollerCoaster.Editor
{
    /// <summary>現行11区間の形状・遭遇・車体表示・停止再開をEditorとPlay Modeで検証する。</summary>
    [InitializeOnLoad]
    public static class RollerCoasterStoryboardValidation
    {
        private const string Folder = "Temp/RollerCoasterStoryboard";
        private const string Active = "Yasuda.Storyboard.Validate";
        private static double s_start;
        private static int s_frame;
        private static int s_errors;
        private static bool[] s_captured = new bool[11];
        private static float s_minEye = float.PositiveInfinity;
        private static bool[] s_visible = new bool[5];
        private static bool[] s_actorCaptured = new bool[5];
        private static bool[] s_roofCaptured = new bool[2];
        private static bool[] s_extra = new bool[7];
        private static bool s_held, s_pauseDone, s_recovered, s_bankJoltSeen, s_foregroundVisible;
        private static double s_pauseAt;
        private static float s_pauseProgress;

        static RollerCoasterStoryboardValidation()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetBool(Active, false) && (type == LogType.Error || type == LogType.Exception))
                {
                    s_errors++;
                    File.AppendAllText(Folder + "/result.txt", message + "\n" + stack + "\n");
                }
            };
        }

        public static void Begin()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "/result.txt", "Image course 1-11 validation\n");
            var story = UnityEngine.Object.FindFirstObjectByType<RollerCoasterStoryboard>();
            var ride = story.GetComponent<RollerCoasterTrialRide>();
            Require(story.Markers.Length == 11 && story.Markers.Zip(story.Markers.Skip(1), (a, b) => b > a).All(x => x), "All 11 markers occur in the requested order");
            Require(story.Actors.Length == 5 && story.Ceilings.Length == 2, "Two oncoming, one parallel, two crossing carts and two rising ceilings");
            Require(!ShaderUtil.ShaderHasError(Shader.Find("YasudaHaruto/RollerCoaster Obstacle")), "Two-sided ceiling shader compiles");
            float[] y = story.Markers.Select(t => ((Vector3)ride.Course.Spline.EvaluatePosition(0, t)).y).ToArray();
            Require(y[0] > 20 && y[1] < 0 && y[3] < y[2], "Steep opening drop and descending large curve");
            Require(y.Skip(3).Take(7).Max() - y.Skip(3).Take(7).Min() > 7, "Later sections contain varied elevation rather than a level route");
            Require(Mathf.Abs(Vector3.Dot(((Vector3)ride.Course.Spline.EvaluateTangent(0, 0)).normalized, Vector3.up)) < .05f, "Entry begins level before climbing steeply");
            float early = story.SpeedAt(Mathf.Lerp(story.Markers[0], story.Markers[1], .2f));
            float late = story.SpeedAt(Mathf.Lerp(story.Markers[0], story.Markers[1], .8f));
            Require(early < late - 8f && late < 36f, $"Opening drop gradually accelerates ({early:F2} to {late:F2})");
            float maxBank = 0, leanPeak = 0, maxStep = 0, maxJolt = 0, maxDip = 0, previousBank = 0;
            bool followsRail = false;
            for (int i = 0; i <= 4000; i++)
            {
                float t = i / 4000f;
                float bank = story.RideBankDegreesAt(t);
                float rail = story.RailBankDegreesAt(t);
                if (Mathf.Abs(bank) > maxBank)
                {
                    maxBank = Mathf.Abs(bank);
                    leanPeak = t;
                }
                if (Mathf.Abs(rail) > 25 && Mathf.Abs(bank - rail) < .1f)
                    followsRail = true;
                maxStep = Mathf.Max(maxStep, Mathf.Abs(Mathf.DeltaAngle(previousBank, bank)));
                previousBank = bank;
                maxJolt = Mathf.Max(maxJolt, story.BankReturnShakeAt(t).magnitude);
                maxDip = Mathf.Max(maxDip, Mathf.Abs(story.BankReturnDipAt(t)));
            }
            Require(followsRail && maxBank > 30, "Ride follows the actual rail bank during the curve");
            Require(story.BankReturnCount > 0 && maxStep > 3 && maxStep < 25 && maxJolt > 1, "Curve exit rapidly returns the view with a short impact");
            Require(maxJolt < 2f && maxDip < .036f,
                $"Curve return impact is softened (rotation {maxJolt:F3} deg, dip {maxDip:F4} m, bank step {maxStep:F3} deg/sample)");
            SessionState.SetFloat("Yasuda.Storyboard.LeanPeak", leanPeak);
            Require(ride.Course.transform.Find("Image Course 1-11/08 Dark Gap Under Broken Track") == null, "Black gap emphasis board is removed; the enclosure floor remains visible");
            Require(story.Actors.Where(x => x.Kind == RollerCoasterStoryboard.ACTOR_KIND.ONCOMING).All(x => x.Cart != null), "Both oncoming wagons are staged from the start rather than appearing in view");
            for (int i = 0; i <= 4000; i++)
            {
                float t = i / 4000f;
                ride.Course.Spline.Evaluate(0, t, out var p, out var forward, out var up);
                if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || !float.IsFinite(p.z) || Unity.Mathematics.math.lengthsq(forward) < 1e-8f || Unity.Mathematics.math.lengthsq(up) < .1f)
                    throw new InvalidOperationException("Invalid spline frame at " + t);
            }
            var enclosure = ride.Course.transform.Find("Image Course 1-11/Continuous Course Enclosure");
            var probe = new GameObject("Temporary story enclosure probe");
            bool backfaces = Physics.queriesHitBackfaces;
            try
            {
                var collider = probe.AddComponent<MeshCollider>();
                collider.sharedMesh = enclosure.GetComponent<MeshFilter>().sharedMesh;
                Physics.queriesHitBackfaces = true;
                Physics.SyncTransforms();
                int rays = 0;
                var ductTable = new RailDistanceTable(ride.Course.Spline);
                ductTable.Frame(10, out var ductPosition, out var ductRotation);
                ductRotation = Quaternion.LookRotation(ductRotation * Vector3.forward, Vector3.up);
                Vector3 ductCenter = ductPosition + ductRotation * Vector3.up * 1.4f;
                Vector3 diagonal = ductRotation * new Vector3(1, 1, 0).normalized;
                Require(collider.Raycast(new Ray(ductCenter, diagonal), out var roundHit, 8) && roundHit.distance > 2.2f && roundHit.distance < 3.6f,
                    "Main passage uses the source duct's round cross section rather than a square");
                for (int i = 5; i < 995; i++)
                {
                    float t = i / 1000f;
                    ride.Course.Spline.Evaluate(0, t, out var p, out var tangent, out var up);
                    Vector3 origin = (Vector3)p + Vector3.up * 1.1f;
                    bool fall = t > story.Markers[10] && t < story.FallEnd;
                    Vector3 right = Vector3.Cross(Vector3.up, (Vector3)tangent).normalized;
                    Vector3 forward = Vector3.ProjectOnPlane((Vector3)tangent, Vector3.up).normalized;
                    foreach (Vector3 direction in fall ? new[] { Vector3.right, Vector3.left } : new[]{right,-right,Vector3.down,
                        (right-Vector3.up*.7f+forward*.5f).normalized,(-right-Vector3.up*.7f+forward*.5f).normalized,
                        (right+Vector3.up*.7f+forward*.5f).normalized,(-right+Vector3.up*.7f+forward*.5f).normalized})
                    {
                        if (!collider.Raycast(new Ray(origin, direction), out var hit, 80) || hit.distance < .12f)
                            throw new InvalidOperationException($"Enclosure clearance at t={t:F3}, dir={direction}, hit={hit.distance:F4}");
                        rays++;
                    }
                }
                Require(rays > 2800, "Walls and floor enclose the full ride, including the final fall");
                var distanceTable = new RailDistanceTable(ride.Course.Spline);
                float fork = distanceTable.DistanceAtParameter(story.Markers[7]);
                Vector3 eye = (Vector3)ride.Course.Spline.EvaluatePosition(0, distanceTable.Parameter(fork - 8)) + Vector3.up * 1.1f;
                var wrong = ride.Course.transform.Find("Image Course 1-11/08 Apparent Forward Route").GetComponent<UnityEngine.Splines.SplineContainer>();
                var wrongDistance = new RailDistanceTable(wrong);
                Vector3 edge = (Vector3)wrong.EvaluatePosition(0, wrongDistance.Parameter(10)) + Vector3.up * .15f;
                Vector3 sight = edge - eye;
                Require(!collider.Raycast(new Ray(eye, sight.normalized), out _, sight.magnitude - .25f), "Broken forward rail is visible before the right turn");
            }
            finally { Physics.queriesHitBackfaces = backfaces; UnityEngine.Object.DestroyImmediate(probe); }
            Require(story.SpeedAt(0) > 0 && story.SpeedAt(story.Markers[0]) > 0 && story.SpeedAt(story.FallEnd + .001f) < 4, "No crest stop; low landing target speed");
            Require(story.SpeedAt(story.Markers[10] - .001f) > 20 && story.SpeedAt(Mathf.Lerp(story.Markers[10], story.FallEnd, .6f)) > 27, "Final brake does not stop the cart and the fall accelerates");
            SessionState.SetBool(Active, true);
            s_errors = 0;
            EditorApplication.isPlaying = true;
        }

        private static void Require(bool value, string text)
        {
            if (!value)
                throw new InvalidOperationException(text);
            File.AppendAllText(Folder + "/result.txt", "PASS " + text + "\n");
        }

        private static void Tick()
        {
            string request = Folder + "/request.txt";
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists(request))
            {
                string command;
                try
                {
                    command = File.ReadAllText(request).Trim();
                    File.Delete(request);
                }
                catch (IOException) { return; } // 外部の書き込み完了を次のEditor更新まで待つ。
                if (command == "build")
                    RollerCoasterStoryboardBuilder.BuildAndValidate();
                else if (command == "wagon-inspect")
                    RollerCoasterWagonSetup.Inspect();
                else if (command == "wagon")
                {
                    try
                    {
                        RollerCoasterWagonSetup.Apply();
                        Begin();
                    }
                    catch (Exception error) { File.WriteAllText(Folder + "/result.txt", "FAIL " + error); Debug.LogException(error); }
                }
                else if (command == "duct" || command == "polish" || command == "tune")
                {
                    try
                    {
                        var tuningStory = UnityEngine.Object.FindFirstObjectByType<RollerCoasterStoryboard>();
                        if (command != "duct")
                        {
                            tuningStory.ApplyRideRefinement();
                            EditorUtility.SetDirty(tuningStory);
                        }
                        if (command == "tune")
                            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(tuningStory.gameObject.scene);
                        else
                            RollerCoasterStoryboardBuilder.RebuildDuctEnclosure();
                        Begin();
                    }
                    catch (Exception error) { File.WriteAllText(Folder + "/result.txt", "FAIL " + error); Debug.LogException(error); }
                }
                else if (command == "play")
                    Begin();
                else if (command == "finalize")
                    RollerCoasterStoryboardBuilder.FinalizeOutput();
                return;
            }
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)
                return;
            if (s_start == 0)
                s_start = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - s_start > 240)
            {
                Finish("FAIL Play timeout", 1);
                return;
            }
            var ride = UnityEngine.Object.FindFirstObjectByType<RollerCoasterTrialRide>();
            if (ride == null)
                return;
            var story = ride.GetComponent<RollerCoasterStoryboard>();
            if (story == null)
                return;
            try
            {
                s_frame++;
                if (ride.ForegroundVisual != null)
                {
                    foreach (var renderer in ride.ForegroundVisual.GetComponentsInChildren<Renderer>())
                    {
                        if ((ride.RideCamera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                            throw new InvalidOperationException("Foreground wagon is hidden by the ride camera mask");
                        Vector3 top = ride.RideCamera.WorldToViewportPoint(renderer.bounds.center + Vector3.up * renderer.bounds.extents.y);
                        if (top.z > 0 && top.y > 0 && top.y < .45f && top.x > 0 && top.x < 1)
                            s_foregroundVisible = true;
                    }
                }
                if (story.BankReturnShakeAt(ride.Progress).magnitude > 1)
                    s_bankJoltSeen = true;
                if (story.HoldingAfterLanding)
                {
                    if (!s_held)
                    {
                        s_held = true;
                        Capture(ride.RideCamera, Folder + "/landing-hold.png");
                    }
                    if (!s_pauseDone && s_pauseAt == 0)
                    {
                        ride.TogglePause();
                        s_pauseAt = EditorApplication.timeSinceStartup;
                        s_pauseProgress = ride.Progress;
                    }
                    if (!s_recovered && story.LandingRecovery >= 1)
                    {
                        ride.Course.Spline.Evaluate(0, ride.Progress, out _, out var tangent, out var up);
                        Require(Quaternion.Angle(story.RotationAt(ride.Progress), Quaternion.LookRotation(tangent, up)) < .1f && ride.Cart.Speed < .01f, "View returns after landing while the cart is still stopped");
                        s_recovered = true;
                        Capture(ride.RideCamera, Folder + "/landing-recovered.png");
                    }
                }
                if (s_pauseAt > 0 && !s_pauseDone && EditorApplication.timeSinceStartup - s_pauseAt > .25)
                {
                    Require(Mathf.Abs(ride.Progress - s_pauseProgress) < .000001f && story.HoldingAfterLanding, "Landing hold remains still during pause");
                    s_pauseDone = true;
                    ride.TogglePause();
                }
                for (int i = 0; i < story.Ceilings.Length; i++)
                    if (!s_roofCaptured[i] && ride.Progress > story.Ceilings[i].Entry - (story.Ceilings[i].Lead - 1) / story.Length)
                    {
                        s_roofCaptured[i] = true;
                        Capture(ride.RideCamera, Folder + $"/ceiling-{story.Ceilings[i].Section}-approach.png");
                    }
                float[] extra = { story.Markers[0] * .55f, Mathf.Lerp(story.Markers[0], story.Markers[1], .25f), Mathf.Lerp(story.Markers[0], story.Markers[1], .8f), SessionState.GetFloat("Yasuda.Storyboard.LeanPeak", story.Markers[2]), story.Markers[10] - .008f, Mathf.Lerp(story.Markers[10], story.FallEnd, .8f), .69f };
                for (int i = 0; i < extra.Length; i++)
                    if (!s_extra[i] && ride.Progress >= extra[i])
                    {
                        s_extra[i] = true;
                        Capture(ride.RideCamera, Folder + $"/tuning-{i}.png");
                        if (i == 3)
                        {
                            var observer = new GameObject("Temporary wheel lift view");
                            var camera = observer.AddComponent<Camera>();
                            Vector3 target = ride.Cart.transform.position + Vector3.up * .6f;
                            Vector3 forward = Vector3.ProjectOnPlane(ride.Cart.transform.forward, Vector3.up).normalized;
                            observer.transform.position = target + forward * 4f + Vector3.Cross(Vector3.up, forward) * 2.4f + Vector3.up;
                            observer.transform.LookAt(target);
                            camera.fieldOfView = 55;
                            Capture(camera, Folder + "/wheel-lift-external.png");
                            UnityEngine.Object.DestroyImmediate(observer);
                        }
                        break;
                    }
                foreach (var actor in story.Actors.Where(x => x.Cart.gameObject.activeSelf))
                {
                    Vector3 eye = actor.Cart.InverseTransformPoint(ride.RideCamera.transform.position) - new Vector3(0, 1.1f, 0);
                    Vector3 outside = new Vector3(Mathf.Max(0, Mathf.Abs(eye.x) - .95f), Mathf.Max(0, Mathf.Abs(eye.y) - 1.1f), Mathf.Max(0, Mathf.Abs(eye.z) - 1.1f));
                    s_minEye = Mathf.Min(s_minEye, outside.magnitude);
                    if (outside.magnitude < .2f)
                        throw new InvalidOperationException("Camera intersects cart in section " + actor.Section);
                    int index = Array.IndexOf(story.Actors, actor);
                    var target = actor.Cart.position + Vector3.up;
                    Vector3 viewport = ride.RideCamera.WorldToViewportPoint(target);
                    if (viewport.z > 0 && viewport.x > .03f && viewport.x < .97f && viewport.y > .03f && viewport.y < .97f)
                        s_visible[index] = true;
                    float before = (ride.Progress - actor.Meeting) * story.Length;
                    if (!s_actorCaptured[index] && actor.Kind == RollerCoasterStoryboard.ACTOR_KIND.CROSSING && Mathf.Abs(before + story.CrossingLead) < .7f)
                    {
                        s_actorCaptured[index] = true;
                        Capture(ride.RideCamera, Folder + $"/crossing-{actor.Section:00}.png");
                    }
                }
                for (int i = 0; i < 11; i++)
                    if (!s_captured[i] && ride.Progress >= story.Markers[i])
                    {
                        s_captured[i] = true;
                        Capture(ride.RideCamera, Folder + $"/section-{i + 1:00}.png");
                        break;
                    }
                if (!ride.Finished)
                    return;
                Require(story.Actors.All(x => x.Seen && x.Passed), "All five cart events were seen and passed");
                Require(s_visible.All(x => x), "All five carts entered the camera view");
                Require(story.Ceilings.All(x => x.Opened), "Both ceilings opened before the cart entered");
                Require(s_captured.All(x => x), "All 11 sections visited and captured");
                Require(s_errors == 0, "No runtime errors");
                if (ride.ForegroundVisual != null)
                    Require(s_foregroundVisible, "Wagon foreground is visible in the lower ride view");
                Require(s_bankJoltSeen, "The curve return impact occurred during the actual ride");
                Require(s_held && s_pauseDone && s_recovered, "Landing recovers the view, waits, and launches after pause/resume");
                Require(ride.Cart.Speed < .01f, "Automatic goal stop");
                File.AppendAllText(Folder + "/result.txt", $"Camera clearance {s_minEye:F3}m, frames={s_frame}\n");
                ride.Restart();
                Require(ride.Progress < .001f && story.Actors.All(x => !x.Passed), "Restart resets course events");
                Finish("COMPLETE", 0);
            }
            catch (Exception error) { Finish("FAIL " + error, 1); }
        }

        private static void Capture(Camera camera, string path)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return;
            var target = RenderTexture.GetTemporary(960, 540, 24);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); }
        }

        private static void Finish(string message, int code)
        {
            File.AppendAllText(Folder + "/result.txt", message + "\n");
            SessionState.SetBool(Active, false);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode)
                EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }
}
