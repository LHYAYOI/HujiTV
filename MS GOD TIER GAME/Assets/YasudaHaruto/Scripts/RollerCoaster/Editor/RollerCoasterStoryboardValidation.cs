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
        private static int s_cameraModeTest;
        private static readonly bool[] s_earlyCrossing = new bool[2];
        private static double s_sparksPreviewStart;
        private static bool s_sparksPreviewJumped;
        private static int s_sparksPreviewFrame;
        private static readonly bool[] s_barrierCaptured = new bool[3];
        private static readonly bool[] s_debrisCaptured = new bool[3];
        private static bool s_boardImpactSeen;
        private static bool s_boardApproachCaptured, s_boardPauseDone;
        private static double s_boardPauseAt;
        private static Vector3 s_boardPausePosition, s_boardPauseImpact;
        private static float s_maxCameraBankSpeed;
        private static float s_maxBoardImpact, s_maxBoardDisplacement;
        private static float s_maxFallShake, s_maxLandingShake;

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
                Require(!collider.Raycast(new Ray(eye, sight.normalized), out _, sight.magnitude - .25f), "Continuous forward rail is visible before the right turn");
                var forwardRail = wrong.GetComponent<RailSplineMesh>();
                Require(forwardRail.StartDistance == 0 && forwardRail.EndDistance == 0 &&
                    ride.Course.transform.Find("Image Course 1-11/08 Broken Rail Far Side") == null,
                    "Apparent straight rail is continuous with no overlapping far-side duplicate");
                foreach (var actor in story.Actors.Where(a => a.Kind == RollerCoasterStoryboard.ACTOR_KIND.CROSSING))
                {
                    float meeting = distanceTable.DistanceAtParameter(actor.Meeting);
                    distanceTable.Frame(meeting - 20f, out var ahead, out var heading);
                    Vector3 from = ahead + Vector3.up * 1.1f;
                    Vector3 target = (Vector3)actor.Route.EvaluatePosition(0, story.CrossingParameterAt(actor, -20f)) + Vector3.up * .8f;
                    Vector3 direction = target - from;
                    Require(!collider.Raycast(new Ray(from, direction.normalized), out _, direction.magnitude - .3f),
                        $"Crossing {actor.Section} is unobstructed from 20m before the meeting");
                    Require(story.CrossingParameterAt(actor, -20f) > 0 &&
                        (actor.ReverseCrossing ? story.CrossingParameterAt(actor, -19f) < story.CrossingParameterAt(actor, -20f) : story.CrossingParameterAt(actor, -19f) > story.CrossingParameterAt(actor, -20f)),
                        $"Crossing {actor.Section} is already moving during the early reveal");
                    var playerBounds = RollerCoasterCartClearance.Measure(ride.Cart.transform);
                    playerBounds.center = Vector3.Scale(playerBounds.center, ride.Cart.transform.lossyScale);
                    playerBounds.size = Vector3.Scale(playerBounds.size, ride.Cart.transform.lossyScale);
                    var actorBounds = RollerCoasterCartClearance.Measure(actor.Cart);
                    actorBounds.center = Vector3.Scale(actorBounds.center, actor.Cart.lossyScale);
                    actorBounds.size = Vector3.Scale(actorBounds.size, actor.Cart.lossyScale);
                    float minimum = float.PositiveInfinity;
                    for (float delta = -28f; delta <= 12f; delta += .05f)
                    {
                        float progress = actor.Meeting + delta / story.Length;
                        Vector3 playerPosition = ride.Course.Spline.EvaluatePosition(0, progress);
                        actor.Route.Evaluate(0, story.CrossingParameterAt(actor, delta), out var actorPosition, out var tangent, out var up);
                        if (actor.ReverseCrossing) tangent = -tangent;
                        float gap = RollerCoasterCartClearance.Gap(playerBounds, playerPosition, story.RotationAt(progress), actorBounds,
                            actorPosition, Quaternion.LookRotation(tangent, up));
                        minimum = Mathf.Min(minimum, gap);
                    }
                    Require(minimum > .02f, $"Crossing {actor.Section} keeps the entire wagons apart ({minimum:F3}m minimum separating-axis gap)");
                    Require(minimum < .09f && actor.ReverseCrossing == (actor.Section == 10), $"Crossing {actor.Section} uses the requested direction and a close non-contact pass");
                    float minimumSpeed = float.PositiveInfinity, maximumSpeed = 0;
                    for (float delta = -22f; delta <= 8f; delta += .5f)
                    {
                        Vector3 a = actor.Route.EvaluatePosition(0, story.CrossingParameterAt(actor, delta - .05f));
                        Vector3 b = actor.Route.EvaluatePosition(0, story.CrossingParameterAt(actor, delta + .05f));
                        float speed = Vector3.Distance(a, b) / .1f * story.SpeedAt(actor.Meeting + delta / story.Length);
                        minimumSpeed = Mathf.Min(minimumSpeed, speed);
                        maximumSpeed = Mathf.Max(maximumSpeed, speed);
                    }
                    Require(minimumSpeed > 0 && maximumSpeed / minimumSpeed < 1.1f,
                        $"Crossing {actor.Section} remains approximately uniform from early reveal through passage ({minimumSpeed:F2}-{maximumSpeed:F2}m/s)");
                }
            }
            finally { Physics.queriesHitBackfaces = backfaces; UnityEngine.Object.DestroyImmediate(probe); }
            Require(story.SpeedAt(0) > 0 && story.SpeedAt(story.Markers[0]) > 0 && story.SpeedAt(story.FallEnd + .001f) < 4, "No crest stop; low landing target speed");
            Require(story.SpeedAt(story.Markers[10] - .001f) > 0 && story.SpeedAt(story.Markers[10] - .001f) < 10 &&
                story.SpeedAt(Mathf.Lerp(story.Markers[10], story.FallEnd, .6f)) > 20, "Barrier brake slows without stopping, then the fall accelerates");
            var sequence = ride.GetComponent<RollerCoasterBreakthroughSequence>();
            Require(sequence != null && sequence.Barriers.Length == 3 && sequence.StraightExtension >= 30f &&
                !ride.GetComponent<RollerCoasterBrakeSparks>().enabled, "Three breakable boards replace sparks on the extended straight");
            Require(!ShaderUtil.ShaderHasError(Shader.Find("YasudaHaruto/RollerCoaster/Warning Board")), "Yellow and black board shader compiles");
            Require(!ShaderUtil.ShaderHasError(Shader.Find("YasudaHaruto/RollerCoaster/Board Text")), "Board lettering uses a depth-tested shader");
            foreach (var barrier in sequence.Barriers)
            {
                Require(barrier.IntactVisual != null && barrier.IntactVisual.activeSelf && barrier.Fragments.All(p => !p.gameObject.activeSelf),
                    "Unbroken board uses one seamless surface and hides all fragments");
                var mesh = barrier.IntactVisual.GetComponent<MeshFilter>().sharedMesh;
                Require(mesh.bounds.size.x > 5.3f && mesh.bounds.size.y > 5.3f, "Board nearly fills the six-metre duct cross section");
                var boardProbe = new GameObject("Temporary seamless board probe");
                boardProbe.transform.SetPositionAndRotation(barrier.Root.position, barrier.Root.rotation);
                try
                {
                    var collider = boardProbe.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                    for (float sampleY = -2.4f; sampleY <= 2.4f; sampleY += .2f)
                        for (float sampleX = -2.4f; sampleX <= 2.4f; sampleX += .2f)
                            if (sampleX * sampleX + sampleY * sampleY < 2.4f * 2.4f && !collider.Raycast(new Ray(boardProbe.transform.TransformPoint(new Vector3(sampleX, sampleY + 1.4f, -.2f)), boardProbe.transform.forward), out _, .4f))
                                throw new InvalidOperationException("Hole in intact board");
                }
                finally { UnityEngine.Object.DestroyImmediate(boardProbe); }
            }
            float previousBrakeSpeed = float.PositiveInfinity;
            bool progressiveBraking = true;
            for (float remaining = sequence.BrakeDistance; remaining >= 0; remaining -= .25f)
            {
                float speed = story.SpeedAt(story.Markers[10] - remaining / story.Length);
                progressiveBraking &= speed > 0 && speed <= previousBrakeSpeed + .05f;
                previousBrakeSpeed = speed;
            }
            Require(progressiveBraking, "Brake profile remains positive and decelerates through the barriers");
            float entryParameter = RollerCoasterCourseBuilder.KnotParameter(ride.Course.Spline.Spline, ride.Course.FallStartKnot - 1);
            Vector3 entryBefore = ride.Course.Spline.EvaluateTangent(0, entryParameter - .000001f);
            Vector3 entryAfter = ride.Course.Spline.EvaluateTangent(0, entryParameter + .000001f);
            float entryAngle = Vector3.Angle(entryBefore, entryAfter);
            Require(entryAngle < .1f, $"Curve enters the barrier straight with a continuous tangent ({entryAngle:F4}deg jump)");
            float approach = story.Markers[10] - 35f / story.Length;
            Require(Mathf.Abs(sequence.ModifySpeed(approach, 23f) - 23f) < .001f && sequence.ContactLeanAt(approach) == 0,
                "There is no voluntary slowdown or forward lean before the first contact");
            for (int i = 0; i < sequence.Barriers.Length; i++)
            {
                float distance = sequence.Barriers[i].MetersBeforeEdge;
                float before = story.SpeedAt(story.Markers[10] - (distance + 1.2f) / story.Length);
                float after = story.SpeedAt(story.Markers[10] - (distance - .5f) / story.Length);
                float expected = i == 0 ? 18f : i == 1 ? 13f : 8f;
                Require(before - after > 3f && Mathf.Abs(after - expected) < .01f,
                    $"Board {i + 1} causes its own speed loss ({before:F2} -> {after:F2}m/s)");
                float middle = story.SpeedAt(story.Markers[10] - (distance - 4f) / story.Length);
                Require(Mathf.Abs(middle - after) < .01f, "Speed holds between board collisions");
            }
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
                else if (command == "presentation")
                {
                    try { RollerCoasterStoryboardBuilder.ApplyPresentationRefinement(); Begin(); }
                    catch (Exception error) { File.WriteAllText(Folder + "/result.txt", "FAIL " + error); Debug.LogException(error); }
                }
                else if (command == "entry-fall")
                {
                    try { RollerCoasterBreakthroughSetup.SmoothEntranceAndStrengthenFall(); Begin(); }
                    catch (Exception error) { File.WriteAllText(Folder + "/result.txt", "FAIL " + error); Debug.LogException(error); }
                }
                else if (command == "impact-timing")
                {
                    try { RollerCoasterBreakthroughSetup.RefineImpactAndCrossings(); Begin(); }
                    catch (Exception error) { File.WriteAllText(Folder + "/result.txt", "FAIL " + error); Debug.LogException(error); }
                }
                else if (command == "smooth-boards")
                {
                    try { RollerCoasterBreakthroughSetup.RefineCameraAndBoards(); Begin(); }
                    catch (Exception error) { File.WriteAllText(Folder + "/result.txt", "FAIL " + error); Debug.LogException(error); }
                }
                else if (command == "breakthrough")
                {
                    try { RollerCoasterBreakthroughSetup.Apply(); Begin(); }
                    catch (Exception error) { File.WriteAllText(Folder + "/result.txt", "FAIL " + error); Debug.LogException(error); }
                }
                else if (command == "sparks-preview" || command == "entry-preview")
                {
                    bool entryPreview = command == "entry-preview";
                    File.WriteAllText(Folder + (entryPreview ? "/entry-preview.txt" : "/sparks-preview.txt"), entryPreview ? "Curve to straight preview\n" : "Brake contact preview\n");
                    SessionState.SetBool("Yasuda.EntryPreview", entryPreview);
                    SessionState.SetBool("Yasuda.SparksPreview", true);
                    EditorApplication.isPlaying = true;
                }
                else if (command == "presentation-effects")
                {
                    try { RollerCoasterStoryboardBuilder.ApplyPresentationEffects(); Begin(); }
                    catch (Exception error) { File.WriteAllText(Folder + "/result.txt", "FAIL " + error); Debug.LogException(error); }
                }
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
            if (SessionState.GetBool("Yasuda.SparksPreview", false) && EditorApplication.isPlaying && !EditorApplication.isCompiling)
            {
                TickSparksPreview();
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
                ValidatePresentation(ride, story);
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
                    if (!s_actorCaptured[index] && actor.Kind == RollerCoasterStoryboard.ACTOR_KIND.CROSSING && Mathf.Abs(before + story.CrossingLeadFor(actor)) < .7f)
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
                Require(s_cameraModeTest == 3, "Level and delayed camera bank modes were exercised during the curve");
                Require(s_maxCameraBankSpeed > 1 && s_maxCameraBankSpeed <= 45.1f, $"Camera bank stays within the smooth angular speed limit ({s_maxCameraBankSpeed:F2}deg/s peak)");
                Require(s_earlyCrossing.All(x => x), "Both crossing carts are visible at least 16m before the meeting");
                var finalSequence = ride.GetComponent<RollerCoasterBreakthroughSequence>();
                Require(finalSequence.BrokenCount == 3 && s_barrierCaptured.All(x => x) && s_boardImpactSeen,
                    "All three boards break into visible fragments and produce camera impacts");
                Require(s_debrisCaptured.All(x => x), "Debris spread was captured after each impact");
                Require(s_boardPauseDone, "Pause freezes flying wood and impact state, then resumes");
                Require(s_maxBoardImpact > 7.5f && s_maxBoardDisplacement > .1f,
                    $"Collision shake is pronounced ({s_maxBoardImpact:F2}deg and {s_maxBoardDisplacement:F3}m peak)");
                Require(s_maxFallShake > 1.5f && s_maxLandingShake > 5f,
                    $"Falling and landing shakes are stronger ({s_maxFallShake:F2}deg airborne, {s_maxLandingShake:F2}deg landing)");
                Require(s_held && s_pauseDone && s_recovered, "Landing recovers the view, waits, and launches after pause/resume");
                Require(ride.Cart.Speed < .01f, "Automatic goal stop");
                File.AppendAllText(Folder + "/result.txt", $"Camera clearance {s_minEye:F3}m, frames={s_frame}\n");
                ride.Restart();
                Require(ride.Progress < .001f && story.Actors.All(x => !x.Passed), "Restart resets course events");
                Require(finalSequence.BrokenCount == 0 && finalSequence.FlyingFragmentCount == 0 && finalSequence.ImpactRotation == Vector3.zero &&
                    finalSequence.Barriers.All(b => b.Lettering.activeSelf && b.IntactVisual.activeSelf && b.Fragments.All(p => !p.gameObject.activeSelf)), "Restart restores intact boards, hides fragments and resets impact clocks");
                Finish("COMPLETE", 0);
            }
            catch (Exception error) { Finish("FAIL " + error, 1); }
        }

        // Sceneの開始位置を編集せず、接続部または旧火花の区間だけをPlay中に短く確認する。
        private static void TickSparksPreview()
        {
            var ride = UnityEngine.Object.FindFirstObjectByType<RollerCoasterTrialRide>();
            if (ride == null || Time.frameCount < 5)
                return;
            bool entryPreview = SessionState.GetBool("Yasuda.EntryPreview", false);
            if (!s_sparksPreviewJumped)
            {
                var story = ride.GetComponent<RollerCoasterStoryboard>();
                float progress = entryPreview ? RollerCoasterCourseBuilder.KnotParameter(ride.Course.Spline.Spline, ride.Course.FallStartKnot - 1) - 10f / story.Length : story.Markers[10] - 16f / story.Length;
                ride.Restart();
                ride.Cart.SetSpline(ride.Course.Spline, progress * story.Length, false, true);
                story.RestartSequence();
                ride.ApplyPose(progress);
                ride.GetComponent<RollerCoasterCameraMotion>().BeginRide(progress);
                s_sparksPreviewJumped = true;
                s_sparksPreviewStart = EditorApplication.timeSinceStartup;
            }
            if (entryPreview && s_sparksPreviewFrame++ % 10 == 0)
            {
                Capture(ride.RideCamera, Folder + $"/straight-entry-{s_sparksPreviewFrame:000}.png");
                File.AppendAllText(Folder + "/entry-preview.txt", $"frame {s_sparksPreviewFrame}: progress={ride.Progress:F6}\n");
            }
            if (!entryPreview && ride.GetComponent<RollerCoasterStoryboard>().BrakeFailing && s_sparksPreviewFrame++ % 10 == 0)
            {
                int[] pixels = VisibleSparkPixels(ride);
                File.AppendAllText(Folder + "/sparks-preview.txt", $"{s_sparksPreviewFrame}: left={pixels[0]}, right={pixels[1]}\n");
                Capture(ride.RideCamera, Folder + $"/sparks-contact-{s_sparksPreviewFrame:000}.png");
            }
            if (EditorApplication.timeSinceStartup - s_sparksPreviewStart > (entryPreview ? 1.1f : 2f))
            {
                SessionState.SetBool("Yasuda.SparksPreview", false);
                SessionState.SetBool("Yasuda.EntryPreview", false);
                EditorApplication.isPlaying = false;
            }
        }

        private static void ValidatePresentation(RollerCoasterTrialRide ride, RollerCoasterStoryboard story)
        {
            var motion = ride.GetComponent<RollerCoasterCameraMotion>();
            s_maxFallShake = Mathf.Max(s_maxFallShake, motion.FallShakeRotation.magnitude);
            s_maxLandingShake = Mathf.Max(s_maxLandingShake, motion.LandingShakeRotation.magnitude);
            float bank = story.RideBankDegreesAt(ride.Progress);
            if (motion.CurveCameraMode == RollerCoasterCameraMotion.CURVE_CAMERA_MODE.DELAYED_ROLL)
                s_maxCameraBankSpeed = Mathf.Max(s_maxCameraBankSpeed, Mathf.Abs(motion.CameraBankAngularSpeed));
            if (ride.Progress > story.Markers[1] + 8f / story.Length && ride.Progress < story.Markers[3] - 8f / story.Length && Mathf.Abs(bank) > 30)
            {
                if (s_cameraModeTest == 0)
                {
                    motion.CurveCameraMode = RollerCoasterCameraMotion.CURVE_CAMERA_MODE.FIXED_ROLL;
                    s_cameraModeTest = 1;
                }
                else if (s_cameraModeTest == 1)
                {
                    Require(Mathf.Abs(motion.CameraBankDegrees) < .01f, "Camera stays level while the cart is banked");
                    Capture(ride.RideCamera, Folder + "/curve-camera-level.png");
                    motion.CurveCameraMode = RollerCoasterCameraMotion.CURVE_CAMERA_MODE.DELAYED_ROLL;
                    s_cameraModeTest = 2;
                }
                else if (s_cameraModeTest == 2)
                {
                    Require(Mathf.Abs(Mathf.DeltaAngle(motion.CameraBankDegrees, bank)) > 5 && Mathf.Abs(motion.CameraBankDegrees) > 0,
                        "Delayed camera follows after the cart rather than sharing its immediate bank");
                    Capture(ride.RideCamera, Folder + "/curve-camera-delayed.png");
                    s_cameraModeTest = 3;
                }
            }
            foreach (var actor in story.Actors.Where(a => a.Kind == RollerCoasterStoryboard.ACTOR_KIND.CROSSING))
            {
                int index = actor.Section - 9;
                float delta = (ride.Progress - actor.Meeting) * story.Length;
                Vector3 viewport = ride.RideCamera.WorldToViewportPoint(actor.Cart.position + Vector3.up);
                if (!s_earlyCrossing[index] && delta >= -22f && delta <= -16f && actor.Cart.gameObject.activeSelf &&
                    viewport.z > 0 && viewport.x > .02f && viewport.x < .98f && viewport.y > .02f && viewport.y < .98f)
                {
                    s_earlyCrossing[index] = true;
                    Require(true, $"Crossing {actor.Section} is in the early peripheral view at {delta:F2}m, viewport={viewport}");
                    Capture(ride.RideCamera, Folder + $"/crossing-early-{actor.Section:00}.png");
                }
            }
            var sequence = ride.GetComponent<RollerCoasterBreakthroughSequence>();
            if (sequence == null) return;
            s_maxBoardImpact = Mathf.Max(s_maxBoardImpact, sequence.ImpactRotation.magnitude);
            s_maxBoardDisplacement = Mathf.Max(s_maxBoardDisplacement, sequence.ImpactPosition.magnitude);
            if (story.BrakeFailing && !s_boardApproachCaptured)
            {
                s_boardApproachCaptured = true;
                Capture(ride.RideCamera, Folder + "/no-entry-approach.png");
            }
            for (int i = 0; i < 3; i++)
                if (!s_barrierCaptured[i] && sequence.BrokenCount > i && sequence.ImpactRotation.magnitude > .5f)
                {
                    int visible = sequence.Barriers[i].Fragments.Count(piece =>
                    {
                        Vector3 p = ride.RideCamera.WorldToViewportPoint(piece.position);
                        return piece.gameObject.activeSelf && p.z > .1f && p.x > 0 && p.x < 1 && p.y > 0 && p.y < 1;
                    });
                    Require(visible >= 3 && sequence.FlyingFragmentCount >= 18, $"Board {i + 1} breaks with visible wood ({visible} pieces), speed={ride.Cart.Speed:F2}m/s");
                    s_barrierCaptured[i] = true;
                    s_boardImpactSeen = true;
                    Capture(ride.RideCamera, Folder + $"/board-break-{i + 1}.png");
                }
            if (!s_boardPauseDone && s_boardPauseAt == 0 && sequence.BrokenCount == 1 && s_barrierCaptured[0])
            {
                ride.TogglePause();
                s_boardPauseAt = EditorApplication.timeSinceStartup;
                s_boardPausePosition = sequence.Barriers[0].Fragments[0].position;
                s_boardPauseImpact = sequence.ImpactRotation;
            }
            int lastBoard = sequence.BrokenCount - 1;
            if (lastBoard >= 0 && !s_debrisCaptured[lastBoard] && sequence.ImpactElapsed > .18f)
            {
                s_debrisCaptured[lastBoard] = true;
                Capture(ride.RideCamera, Folder + $"/board-debris-{lastBoard + 1}.png");
            }
            if (!s_boardPauseDone && s_boardPauseAt > 0 && EditorApplication.timeSinceStartup - s_boardPauseAt > .15)
            {
                Require(Vector3.Distance(sequence.Barriers[0].Fragments[0].position, s_boardPausePosition) < .0001f &&
                    Vector3.Distance(sequence.ImpactRotation, s_boardPauseImpact) < .0001f, "Wood fragments and impact do not advance while paused");
                ride.TogglePause();
                s_boardPauseDone = true;
            }
        }

        // 同一フレームで粒子を非表示/片側表示して差分を取る。車体に隠れている粒子は数えない。
        private static int[] VisibleSparkPixels(RollerCoasterTrialRide ride)
        {
            var renderers = new[] { "Left Brake Sparks", "Right Brake Sparks" }
                .Select(n => ride.Cart.transform.Find(n).GetComponent<ParticleSystemRenderer>()).ToArray();
            var enabled = renderers.Select(r => r.enabled).ToArray();
            var target = RenderTexture.GetTemporary(320, 180, 24);
            var previous = ride.RideCamera.targetTexture;
            var active = RenderTexture.active;
            var texture = new Texture2D(320, 180, TextureFormat.RGB24, false);
            Color32[] Read()
            {
                ride.RideCamera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 320, 180), 0, 0);
                texture.Apply();
                return texture.GetPixels32();
            }
            try
            {
                ride.RideCamera.targetTexture = target;
                foreach (var renderer in renderers) renderer.enabled = false;
                var baseline = Read();
                var result = new int[2];
                for (int side = 0; side < 2; side++)
                {
                    renderers[side].enabled = true;
                    var visible = Read();
                    result[side] = Enumerable.Range(0, visible.Length).Count(i => visible[i].r - baseline[i].r > 20 && visible[i].g - baseline[i].g > 10);
                    renderers[side].enabled = false;
                }
                return result;
            }
            finally
            {
                for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = enabled[i];
                ride.RideCamera.targetTexture = previous;
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
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
