using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster.Editor
{
    // 起動済みEditorでの作成／検証用。任意コードや任意パスは受け付けない。
    [InitializeOnLoad]
    public static class RollerCoasterCourseValidation
    {
        private const string Folder = "Temp/RollerCoasterCourseValidation";
        private const string Request = Folder + "/request.txt";
        private const string Report = Folder + "/result.txt";
        private static double s_nextPoll;


        static RollerCoasterCourseValidation()
        {
            EditorApplication.update += Poll;
        }


        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < s_nextPoll)
                return;
            s_nextPoll = EditorApplication.timeSinceStartup + 1d;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request))
                return;
            string request = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Report, "Course request: " + request + "\n");
            try
            {
                switch (request)
                {
                    case "build":
                        RollerCoasterCourseBuilder.CreateInScene();
                        Verify();
                        Scene scene = SceneManager.GetSceneByPath(RollerCoasterCourseBuilder.ScenePath);
                        EditorSceneManager.SaveScene(scene);
                        File.AppendAllText(Report, "Saved RollerCoaster scene\n");
                        break;
                    case "verify":
                        Verify();
                        break;
                    case "inspect":
                        var inspectionCourse = FindCourse();
                        File.WriteAllText(Folder + "/live-course.json", EditorJsonUtility.ToJson(inspectionCourse, true));
                        File.WriteAllText(Folder + "/live-spline.json", EditorJsonUtility.ToJson(inspectionCourse.Spline, true));
                        File.AppendAllText(Report, $"Live fall: {inspectionCourse.FallForwardLength}, {inspectionCourse.FallDrop}; actual start={inspectionCourse.Spline.Spline[inspectionCourse.FallStartKnot].Position}, end={inspectionCourse.Spline.Spline[inspectionCourse.FallEndKnot].Position}\n");
                        break;
                    case "regression":
                        VerifyEditing();
                        break;
                    case "frame":
                        Frame();
                        break;
                    case "return-and-trial":
                        PrepareReturnAndTrial();
                        break;
                    case "gentle-return":
                        var gentleCourse = FindCourse();
                        File.WriteAllText(Folder + "/before-gentle-live-course.json", EditorJsonUtility.ToJson(gentleCourse, true));
                        File.WriteAllText(Folder + "/before-gentle-live-spline.json", EditorJsonUtility.ToJson(gentleCourse.Spline, true));
                        Undo.RegisterCompleteObjectUndo(gentleCourse, "Widen return curve");
                        var gentleData = new SerializedObject(gentleCourse);
                        gentleData.FindProperty("m_returnRadius").floatValue = 26f;
                        gentleData.FindProperty("m_returnSway").floatValue = 1f;
                        gentleData.ApplyModifiedPropertiesWithoutUndo();
                        PrepareReturnAndTrial();
                        VerifyFallView();
                        break;
                    case "fall-view":
                        VerifyFallView();
                        break;
                    case "diagram-route":
                        PrepareReturnAndTrial();
                        var diagramCourse = FindCourse();
                        var diagramRide = RollerCoasterTrialSetup.Create(diagramCourse);
                        var speeds = RollerCoasterTrialSetup.AddSectionSpeeds(diagramRide);
                        speeds.Initialize(diagramCourse, diagramRide.Cart, 0f);
                        var markers = diagramCourse.ReturnSectionKnots;
                        float SpeedAtKnot(int index) => speeds.SpeedAt(RollerCoasterCourseBuilder.KnotParameter(diagramCourse.Spline.Spline, index), 12f);
                        Require(SpeedAtKnot(markers[2]) < SpeedAtKnot(markers[3]) && SpeedAtKnot(markers[4]) < SpeedAtKnot(markers[5]),
                            "Section speeds slow on both climbs and increase on both descents");
                        EditorSceneManager.SaveScene(diagramCourse.gameObject.scene);
                        File.AppendAllText(Report, "Saved diagram route and editable section speeds\n");
                        break;
                    case "ride-tuning":
                        TuneRide();
                        break;
                    case "two-encounters":
                    case "smooth-and-encounter":
                        PrepareSmoothAndEncounter();
                        break;
                    case "presentation-rework":
                        RollerCoasterTrialSetup.StrengthenVerticalShake(RollerCoasterTrialSetup.Create(FindCourse()));
                        PrepareSmoothAndEncounter();
                        break;
                    case "compact-shaft":
                        RollerCoasterTrialSetup.StrengthenVerticalShake(RollerCoasterTrialSetup.Create(FindCourse()), 2.4f, 1.5f, .3f);
                        PrepareSmoothAndEncounter();
                        break;
                    case "enclosures-and-easing":
                        PrepareEnclosuresAndEasing();
                        break;
                    case "inspect-enclosure":
                        var model = AssetDatabase.LoadAssetAtPath<GameObject>(RailSplineMeshSetup.ModelPath);
                        foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                        {
                            var matrix = filter.transform.localToWorldMatrix;
                            matrix.m03 = matrix.m13 = matrix.m23 = 0f;
                            var source = RailMeshBuilder.Read(filter.sharedMesh, matrix);
                            File.AppendAllText(Report, $"{filter.name}: bounds={source.Bounds}, materials={string.Join(",", filter.GetComponent<MeshRenderer>().sharedMaterials.Select(x => x.name + ":" + AssetDatabase.GetAssetPath(x)))}\n");
                        }
                        break;
                    case "camera-motion":
                        var shakeCourse = FindCourse();
                        var shakeRide = RollerCoasterTrialSetup.Create(shakeCourse);
                        var motion = RollerCoasterTrialSetup.AddCameraMotion(shakeRide);
                        Require(motion.Shake != null && motion.Shake.GetType() == typeof(CameraShake), "Original Hamada CameraShake is referenced directly");
                        Require(motion.VirtualCamera.GetComponent<Unity.Cinemachine.CinemachineBasicMultiChannelPerlin>().NoiseProfile != null,
                            "Existing trolley noise profile is connected");
                        Require(motion.RollingRateFor(28f, 0f, 0f, false) > motion.RollingRateFor(3f, 0f, 0f, false), "Rolling shake increases with speed");
                        Require(motion.RollingRateFor(16f, -0.6f, 0f, false) > motion.RollingRateFor(16f, 0.6f, 0f, false), "Downhill shake differs from uphill shake");
                        Require(motion.RollingRateFor(16f, 0f, 0f, true) < motion.RollingRateFor(16f, 0f, 0f, false), "Track rumble is reduced in the air");
                        Require(motion.RollingRateFor(0f, -0.6f, 1f, false) == 0f, "Stationary cart has no rolling shake target");
                        EditorSceneManager.SaveScene(shakeCourse.gameObject.scene);
                        File.AppendAllText(Report, "Saved Hamada-based camera motion\n");
                        break;
                    default:
                        throw new ArgumentException("Unknown course request");
                }
                File.AppendAllText(Report, "RollerCoaster Course COMPLETE\n");
            }
            catch (Exception exception)
            {
                File.AppendAllText(Report, "FAIL " + exception + "\n");
                Debug.LogException(exception);
            }
        }


        private static void PrepareSmoothAndEncounter()
        {
            var course = FindCourse();
            var sequence = RollerCoasterTwoEncounterSetup.Build(course);
            var encounters = sequence.Encounters;
            Require(encounters.Length == 2 && encounters[0].AvoidRight && !encounters[1].AvoidRight, "Two encounters: left threat/right escape, then right threat/left escape");
            var settings = course.GetComponentInChildren<RollerCoasterEnclosures>(true);
            if (settings == null)
                settings = RollerCoasterEnclosureBuilder.Create(course);
            var data = new SerializedObject(settings);
            data.FindProperty("m_smooth").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            RollerCoasterEnclosureBuilder.Generate(settings);
            foreach (var encounter in encounters)
            {
                Require(encounter.BranchesVisible, "Both branch rails are permanently enabled");
                Require(Vector3.Distance(course.Spline.EvaluatePosition(0, encounter.Begin), encounter.OncomingRoute.EvaluatePosition(0, 0)) < .01f, "Branch starts at the main rail junction");
                Vector3 incoming = course.Spline.EvaluateTangent(0, encounter.Begin);
                Vector3 outgoing = encounter.OncomingRoute.EvaluateTangent(0, 0);
                Require(Vector3.Dot(incoming.normalized, outgoing.normalized) > .999f, "Branch shares the main rail tangent without crossing backward");
                float minimum = float.PositiveInfinity;
                for (int i = 0; i <= 200; i++)
                {
                    float t = Mathf.Lerp(encounter.Begin - .015f, encounter.End, i / 200f);
                    Vector3 player = course.Spline.EvaluatePosition(0, t);
                    for (int j = 0; j <= 40; j++)
                    {
                        Vector3 opponent = encounter.OncomingRoute.EvaluatePosition(0, encounter.ParameterAtDistance(Mathf.Lerp(encounter.StopDistance, encounter.StartDistance, j / 40f)));
                        minimum = Mathf.Min(minimum, Vector3.Distance(player, opponent));
                    }
                }
                File.AppendAllText(Report, $"INFO Encounter {encounter.Number} untimed root envelope {minimum:F3}m; actual camera clearance is checked in Play Mode\n");
            }
            var mesh = settings.GeneratedRoot.GetComponent<MeshFilter>().sharedMesh;
            Require(mesh.vertexCount > 10000, "Enclosure mesh is subdivided along the route");
            Require(settings.GeneratedRoot.GetComponent<MeshRenderer>().sharedMaterials.Length == settings.Palette.Length, "Scattered color palette is preserved");
            Require(!ShaderUtil.ShaderHasError(settings.Palette[0].shader), "Enclosure shader compiles without errors");
            var probe = new GameObject("Temporary enclosure verification");
            bool oldBackfaces = Physics.queriesHitBackfaces;
            try
            {
                probe.transform.SetPositionAndRotation(settings.transform.position, settings.transform.rotation);
                var collider = probe.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                Physics.queriesHitBackfaces = true;
                Physics.SyncTransforms();
                var distanceTable = new RailDistanceTable(course.Spline);
                foreach (var encounter in encounters)
                    for (int i = 1; i < 100; i++)
                    {
                        float distance = Mathf.Lerp(encounter.RoomEntry, encounter.RoomExit, i / 100f);
                        Vector3 rail = course.Spline.EvaluatePosition(0, distanceTable.Parameter(distance));
                        Require(collider.Raycast(new Ray(rail, Vector3.up), out var roof, 100f) && roof.distance > 2.5f, $"Junction {encounter.Number} rail is not covered by its floor at {i}");
                    }
                foreach (var encounter in encounters)
                {
                    float forkDistance = distanceTable.DistanceAtParameter(encounter.Begin);
                    Vector3 eye = (Vector3)course.Spline.EvaluatePosition(0, distanceTable.Parameter(forkDistance - 5f)) + Vector3.up * 1.1f;
                    var wrongTable = new RailDistanceTable(encounter.OncomingRoute);
                    foreach (Vector3 target in new[]{(Vector3)encounter.OncomingRoute.EvaluatePosition(0,wrongTable.Parameter(wrongTable.Length-2f))+Vector3.up*1.1f,
                        (Vector3)course.Spline.EvaluatePosition(0,distanceTable.Parameter(forkDistance+22f))+Vector3.up*1.1f})
                    {
                        Vector3 ray = target - eye;
                        Require(collider.Raycast(new Ray(eye, ray.normalized), out _, ray.magnitude - .1f), $"Junction {encounter.Number} far branch is hidden by its curved enclosure");
                    }
                }
                var firstEncounter = encounters[0];
                float firstFork = distanceTable.DistanceAtParameter(firstEncounter.Begin);
                Vector3 approachEye = (Vector3)course.Spline.EvaluatePosition(0, distanceTable.Parameter(firstFork - 5f)) + Vector3.up * 1.1f;
                Vector3 rightAhead = (Vector3)course.Spline.EvaluatePosition(0, distanceTable.Parameter(firstFork + 12f)) + Vector3.up * .15f;
                Vector3 sight = rightAhead - approachEye;
                Require(collider.Raycast(new Ray(approachEye, sight.normalized), out _, sight.magnitude - .1f), "Right rail 12m beyond the fork is hidden on approach");
                float start = RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, course.FallStartKnot);
                float end = RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, course.FallEndKnot);
                int rays = 0;
                for (int i = 12; i < 988; i++)
                {
                    float t = i / 1000f;
                    course.Spline.Evaluate(0, t, out var p, out var tangent, out var up);
                    Vector3 origin = (Vector3)p + Vector3.up * 1.1f;
                    bool fall = t > start && t < end;
                    Vector3 right = fall ? Vector3.right : Vector3.Cross((Vector3)up, (Vector3)tangent).normalized;
                    Vector3 vertical = fall ? Vector3.forward : (Vector3)up;
                    foreach (Vector3 direction in new[] { right, -right, vertical, -vertical })
                    {
                        if (!collider.Raycast(new Ray(origin, direction), out var hit, 1000f))
                            throw new InvalidOperationException($"Open enclosure at t={t:F3}, direction={direction}");
                        if (hit.distance < .15f)
                            throw new InvalidOperationException($"Enclosure intersects ride view at {t:F3}");
                        rays++;
                    }
                }
                File.AppendAllText(Report, $"PASS {rays} wall/ceiling/floor rays enclosed, including trackless fall\n");
                float middle = (start + end) * .5f;
                Vector3 falling = (Vector3)course.Spline.EvaluatePosition(0, middle) + Vector3.up * 1.1f;
                foreach (Vector3 direction in new[] { Vector3.right, Vector3.left })
                {
                    if (!collider.Raycast(new Ray(falling, direction), out var clearance, 1000f) || clearance.distance < 2.5f || clearance.distance > 4.5f)
                        throw new InvalidOperationException("Fall shaft must stay enclosed at a moderate width");
                }
                Require(collider.Raycast(new Ray(falling, Vector3.forward), out var front, 1000f) && front.distance > 2f && front.distance < 8f, "Fall is enclosed by a compact shaft");
                Vector3 roofTarget = (Vector3)course.Spline.EvaluatePosition(0, end) + new Vector3(0, 4.25f, 6f);
                Vector3 roofRay = roofTarget - falling;
                Require(collider.Raycast(new Ray(falling, roofRay.normalized), out _, roofRay.magnitude), "Lower corridor ceiling top is hidden by the shaft exit wall");
            }
            finally { Physics.queriesHitBackfaces = oldBackfaces; UnityEngine.Object.DestroyImmediate(probe); }
            EditorSceneManager.SaveScene(course.gameObject.scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = sequence.gameObject;
            File.AppendAllText(Report, $"Saved smooth colored enclosure and two permanent-branch encounters; vertices={mesh.vertexCount}\n");
        }


        private static void PrepareEnclosuresAndEasing()
        {
            var course = FindCourse();
            var ride = RollerCoasterTrialSetup.Create(course);
            var speeds = RollerCoasterTrialSetup.AddSectionSpeeds(ride);
            Undo.RecordObject(speeds, "Ease crest and landing speeds");
            var data = new SerializedObject(speeds);
            data.FindProperty("m_landingStartSpeed").floatValue = 0.6f;
            data.FindProperty("m_landingAccelerationSeconds").floatValue = 4.5f;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(speeds);
            speeds.Initialize(course, ride.Cart, 0f);
            float crest = RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, course.CrestKnot);
            Require(speeds.SpeedAt(crest * .1f, 12f) > speeds.SpeedAt(crest * .5f, 12f) &&
                speeds.SpeedAt(crest * .5f, 12f) > speeds.SpeedAt(crest * .95f, 12f), "Entry progressively slows toward the crest");
            var settings = RollerCoasterEnclosureBuilder.Create(course);
            var enclosureData = new SerializedObject(settings);
            enclosureData.FindProperty("m_overlap").floatValue = 1.4f;
            enclosureData.ApplyModifiedPropertiesWithoutUndo();
            RollerCoasterEnclosureBuilder.ApplyAtlasColors(settings);
            string Signature() => string.Join(";", settings.GeneratedRoot.GetComponentsInChildren<MeshRenderer>().Select(x => x.sharedMaterial.name));
            string first = Signature();
            RollerCoasterEnclosureBuilder.Generate(settings);
            Require(first == Signature(), "Enclosure color placement is deterministic");
            var filters = settings.GeneratedRoot.GetComponentsInChildren<MeshFilter>();
            Require(filters.All(x => x.sharedMesh == settings.SourceMesh), "All enclosures reuse the unbent straight source mesh");
            Require(filters.Select(x => x.GetComponent<MeshRenderer>().sharedMaterial).Distinct().Count() == settings.Palette.Length,
                "All palette colors are distributed along the track");
            var bounds = settings.SourceMesh.bounds;
            float gapStart = RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, course.FallStartKnot);
            float gapEnd = RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, course.FallEndKnot);
            for (int i = 0; i <= 3000; i++)
            {
                float t = i / 3000f;
                if (t > gapStart && t < gapEnd)
                    continue;
                course.Spline.Evaluate(0, t, out var point, out var tangent, out var up);
                var frame = Quaternion.LookRotation(tangent, up);
                foreach (var offset in new[] { new Vector3(-1.27f, 0f, 0f), new Vector3(1.27f, 0f, 0f), new Vector3(0f, 1.3f, 0f) })
                {
                    Vector3 world = (Vector3)point + frame * offset;
                    bool inside = filters.Any(x =>
                    {
                        Vector3 local = x.transform.InverseTransformPoint(world);
                        var expanded = bounds;
                        expanded.Expand(0.002f);
                        return expanded.Contains(local);
                    });
                    if (!inside)
                        throw new InvalidOperationException($"Enclosure coverage at {t:F4}, offset {offset}");
                }
            }
            File.AppendAllText(Report, $"PASS Rail edges and ride view fit inside enclosures at 3001 route samples; {filters.Length} unbent pieces\n");
            Require(filters.All(x =>
            {
                Vector3 center = x.transform.TransformPoint(bounds.center);
                return float.IsFinite(center.x) && float.IsFinite(center.y) && float.IsFinite(center.z);
            }), "Enclosure positions are finite");
            EditorSceneManager.SaveScene(course.gameObject.scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = settings.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
            File.AppendAllText(Report, "Saved enclosures and smooth crest/landing speeds\n");
        }


        private static void TuneRide()
        {
            var course = FindCourse();
            var oldTable = new RailDistanceTable(course.Spline);
            float OldDistance(int index) => oldTable.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, index));
            float oldDescentLength = OldDistance(course.SpiralStartKnot) - OldDistance(course.CrestKnot);
            float oldSpiralLength = OldDistance(course.SpiralEndKnot) - OldDistance(course.SpiralStartKnot);
            float oldInnerRadius = course.EndRadius;
            File.WriteAllText(Folder + "/before-tuning-course.json", EditorJsonUtility.ToJson(course, true));
            File.WriteAllText(Folder + "/before-tuning-spline.json", EditorJsonUtility.ToJson(course.Spline, true));
            Undo.RegisterCompleteObjectUndo(course, "Tune coaster intensity");
            var data = new SerializedObject(course);
            data.FindProperty("m_descentLength").floatValue = 48f;
            data.FindProperty("m_descentDrop").floatValue = 36f;
            data.FindProperty("m_startRadius").floatValue = 11.5f;
            data.FindProperty("m_endRadius").floatValue = 8.5f;
            data.FindProperty("m_firstHillHeight").floatValue = 7.5f;
            data.FindProperty("m_secondHillHeight").floatValue = 5.5f;
            data.FindProperty("m_exitDrop").floatValue = 7.5f;
            data.ApplyModifiedPropertiesWithoutUndo();
            RollerCoasterCourseBuilder.Rebuild(course);
            var ride = RollerCoasterTrialSetup.Create(course);
            var profile = RollerCoasterTrialSetup.AddSectionSpeeds(ride);
            File.WriteAllText(Folder + "/before-tuning-speed.json", EditorJsonUtility.ToJson(profile, true));
            Undo.RegisterCompleteObjectUndo(profile, "Tune landing stop and launch");
            var speeds = new SerializedObject(profile);
            speeds.FindProperty("m_descentSpeed").floatValue = 28f;
            speeds.FindProperty("m_landingStopSeconds").floatValue = 1.2f;
            speeds.FindProperty("m_landingStartSpeed").floatValue = 2.5f;
            speeds.FindProperty("m_landingAccelerationSeconds").floatValue = 3f;
            speeds.ApplyModifiedPropertiesWithoutUndo();
            Verify();
            VerifyFallView();
            var newTable = new RailDistanceTable(course.Spline);
            float NewDistance(int index) => newTable.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, index));
            float newDescentLength = NewDistance(course.SpiralStartKnot) - NewDistance(course.CrestKnot);
            float newSpiralLength = NewDistance(course.SpiralEndKnot) - NewDistance(course.SpiralStartKnot);
            Require(newDescentLength > oldDescentLength, "Initial descent is longer");
            Require(course.StartRadius <= oldInnerRadius && newSpiralLength < oldSpiralLength,
                "First spiral is no larger than the old inner ring and the two turns are shorter");
            File.AppendAllText(Report, $"Descent length {oldDescentLength:F3}->{newDescentLength:F3}m; spiral length {oldSpiralLength:F3}->{newSpiralLength:F3}m\n");
            EditorSceneManager.SaveScene(course.gameObject.scene);
            File.AppendAllText(Report, "Saved intensity and landing tuning\n");
        }


        private static void VerifyFallView()
        {
            var course = FindCourse();
            var ride = course.gameObject.scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<RollerCoasterTrialRide>(true)).Single();
            ride.RefreshRouteFrames();
            float start = RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, course.FallStartKnot);
            float end = RollerCoasterCourseBuilder.KnotParameter(course.Spline.Spline, course.FallEndKnot);
            course.Spline.Evaluate(0, start, out _, out var takeoff, out _);
            Vector3 entryHeading = Vector3.ProjectOnPlane(takeoff, Vector3.up).normalized;
            Quaternion previous = ride.EvaluateRideRotation(Mathf.Max(0f, start - 0.002f));
            float maxStep = 0f;
            int rawReverseSamples = 0;
            for (int i = 0; i <= 2000; i++)
            {
                float t = Mathf.Lerp(start - 0.002f, end + 0.002f, i / 2000f);
                var rotation = ride.EvaluateRideRotation(t);
                Quaternion difference = Quaternion.Inverse(previous) * rotation;
                float step = 2f * Mathf.Atan2(new Vector3(difference.x, difference.y, difference.z).magnitude, Mathf.Abs(difference.w)) * Mathf.Rad2Deg;
                maxStep = Mathf.Max(maxStep, step);
                previous = rotation;
                if (t <= start || t >= end)
                    continue;
                course.Spline.Evaluate(0, t, out _, out var rawTangent, out _);
                if (Vector3.Dot(Vector3.ProjectOnPlane(rawTangent, Vector3.up), entryHeading) < -0.00001f)
                    rawReverseSamples++;
                Vector3 facing = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up).normalized;
                if (Vector3.Dot(facing, entryHeading) < 0.95f)
                    throw new InvalidOperationException("Fall view turns away from entry heading");
            }
            Require(maxStep < 1f, "2001 fall-view samples including entry/landing have no angular jumps");
            Require(true, "Fall view keeps the takeoff heading even when the spatial tangent reverses");
            File.AppendAllText(Report, $"Raw horizontal reverse samples: {rawReverseSamples}; max corrected angular step: {maxStep:F4} degrees\n");
        }


        private static void PrepareReturnAndTrial()
        {
            var course = FindCourse();
            File.WriteAllText(Folder + "/before-return-live-course.json", EditorJsonUtility.ToJson(course, true));
            File.WriteAllText(Folder + "/before-return-live-spline.json", EditorJsonUtility.ToJson(course.Spline, true));
            var oldSpline = new Spline(course.Spline.Spline);
            var planned = RollerCoasterCourseBuilder.MakeLayout(course);
            // 前半の手編集は保持。落下も現在の実経路を正本として残す。
            for (int i = 0; i <= course.FallEndKnot; i++)
                planned.Spline.SetKnot(i, oldSpline[i]);
            Vector3 oldLanding = oldSpline[course.FallEndKnot].Position;
            // 上でprefixをコピーする前の設計上の着地点を求め、既存の着地点へtail全体を合わせる。
            var design = RollerCoasterCourseBuilder.MakeLayout(course);
            Vector3 delta = oldLanding - (Vector3)design.Spline[design.FallEnd].Position;
            for (int i = planned.FallEnd + 1; i < planned.Spline.Count; i++)
            {
                var knot = planned.Spline[i];
                knot.Position += (Unity.Mathematics.float3)delta;
                planned.Spline.SetKnot(i, knot);
            }
            RollerCoasterCourseBuilder.ApplyPreparedLayout(course, planned);
            for (int i = 0; i <= course.FallEndKnot; i++)
            {
                if (!course.Spline.Spline[i].Equals(oldSpline[i]))
                    throw new InvalidOperationException("Existing front-route knot changed: " + i);
            }
            Require(true, "Existing entry, spiral, and trackless fall knots are preserved");
            var trial = RollerCoasterTrialSetup.Create(course);
            Verify();
            Require(course.Spline.Spline[course.Spline.Spline.Count - 1].Position.z < oldLanding.z, "Exit returns toward the entrance side after landing");
            Require(trial.Cart != null && trial.RideCamera != null, "Existing CartController and trial camera are connected");
            EditorSceneManager.SaveScene(course.gameObject.scene);
            File.AppendAllText(Report, "Saved returning course and trial ride; Play starts the ride\n");
        }


        private static void VerifyEditing()
        {
            var original = FindCourse();
            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject copy = UnityEngine.Object.Instantiate(original.gameObject);
            SceneManager.MoveGameObjectToScene(copy, previewScene);
            string[] originalAssets = AssetDatabase.FindAssets("t:Mesh", new[] { RailSplineMeshGenerator.OutputFolder }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            try
            {
                var course = copy.GetComponent<RollerCoasterCourse>();
                var spline = course.Spline.Spline;
                var sourceBefore = course.BeforeFall.GeneratedRoot.GetComponent<MeshFilter>().sharedMesh;
                var sourceAfter = course.AfterFall.GeneratedRoot.GetComponent<MeshFilter>().sharedMesh;
                var knot = spline[course.CrestKnot];
                knot.Position.y += 0.25f;
                spline[course.CrestKnot] = knot;
                float editedHeight = knot.Position.y;
                RollerCoasterCourseBuilder.RebuildRails(course);
                Undo.FlushUndoRecordObjects();
                RequireNear(course.Spline.Spline[course.CrestKnot].Position.y, editedHeight, 0.0001f, "Rail-only rebuild preserves hand-edited knots");
                Mesh rebuiltBefore = course.BeforeFall.GeneratedRoot.GetComponent<MeshFilter>().sharedMesh;
                Require(rebuiltBefore != sourceBefore, "Rail-only rebuild creates a new mesh");
                Undo.PerformUndo();
                Require(course.BeforeFall.GeneratedRoot != null && course.AfterFall.GeneratedRoot != null &&
                    course.BeforeFall.GeneratedRoot.GetComponent<MeshFilter>().sharedMesh == sourceBefore &&
                    course.AfterFall.GeneratedRoot.GetComponent<MeshFilter>().sharedMesh == sourceAfter,
                    "One Undo restores both rail sections together");
                Undo.PerformRedo();
                Require(course.BeforeFall.GeneratedRoot != null && course.BeforeFall.GeneratedRoot.GetComponent<MeshFilter>().sharedMesh == rebuiltBefore,
                    "Redo restores the rebuilt course output");
                var design = new SerializedObject(course);
                design.FindProperty("m_startRadius").floatValue = 2f;
                design.ApplyModifiedPropertiesWithoutUndo();
                bool rejected = false;
                try
                {
                    RollerCoasterCourseBuilder.Rebuild(course);
                }
                catch (ArgumentException) { rejected = true; }
                Require(rejected && course.BeforeFall.GeneratedRoot.GetComponent<MeshFilter>().sharedMesh == rebuiltBefore,
                    "Invalid design leaves the previously generated course intact");
                Require(original.BeforeFall.GeneratedRoot.GetComponent<MeshFilter>().sharedMesh == sourceBefore &&
                    original.AfterFall.GeneratedRoot.GetComponent<MeshFilter>().sharedMesh == sourceAfter,
                    "Editing a cloned course does not alter original saved meshes");
            }
            finally
            {
                foreach (var component in copy.GetComponentsInChildren<Component>(true))
                    if (component != null)
                        Undo.ClearUndo(component);
                foreach (var transform in copy.GetComponentsInChildren<Transform>(true))
                    Undo.ClearUndo(transform.gameObject);
                UnityEngine.Object.DestroyImmediate(copy);
                EditorSceneManager.ClosePreviewScene(previewScene);
                foreach (string path in AssetDatabase.FindAssets("t:Mesh", new[] { RailSplineMeshGenerator.OutputFolder }).Select(AssetDatabase.GUIDToAssetPath).Except(originalAssets))
                    AssetDatabase.DeleteAsset(path);
            }
        }


        internal static RollerCoasterCourse FindCourse()
        {
            var scene = SceneManager.GetSceneByPath(RollerCoasterCourseBuilder.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("RollerCoasterシーンを開いてください。");
            return scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<RollerCoasterCourse>(true)).Single();
        }


        internal static void Verify()
        {
            var course = FindCourse();
            var spline = course.Spline.Spline;
            var table = new RailDistanceTable(course.Spline);
            Require(course.Spline.Splines.Count == 1 && !spline.Closed, "One open spline is shared by the whole ride");
            Require(course.BeforeFall.Spline == course.Spline && course.AfterFall.Spline == course.Spline, "Both rail sections use the same ride spline");
            Require(spline[course.CrestKnot].Position.y > spline[0].Position.y, "Entry rises to a crest before descending");
            Require(spline[course.SpiralStartKnot].Position.y < spline[course.CrestKnot].Position.y, "Straight descent reaches the spiral below the crest");
            int spiralCount = course.SpiralEndKnot - course.SpiralStartKnot;
            Require(spiralCount == course.Turns * course.PointsPerTurn, "Spiral contains the requested turn count");
            float accumulatedAngle = 0f;
            float previousAngle = 0f;
            for (int i = 0; i <= spiralCount; i++)
            {
                Vector3 p = spline[course.SpiralStartKnot + i].Position;
                float radius = new Vector2(p.x - course.StartRadius, p.z - (course.EntryLength + course.DescentLength)).magnitude;
                float expected = Mathf.Lerp(course.StartRadius, course.EndRadius, Mathf.SmoothStep(0f, 1f, (float)i / spiralCount));
                RequireNear(radius, expected, 0.001f, "Spiral radius");
                float angle = Mathf.Atan2(p.z - (course.EntryLength + course.DescentLength), course.StartRadius - p.x) * Mathf.Rad2Deg;
                if (i > 0)
                {
                    float delta = Mathf.DeltaAngle(previousAngle, angle);
                    if (delta <= 0f)
                        throw new InvalidOperationException("Spiral direction is not clockwise");
                    accumulatedAngle += delta;
                    if (p.y >= spline[course.SpiralStartKnot + i - 1].Position.y)
                        throw new InvalidOperationException("Spiral must descend continuously");
                }
                previousAngle = angle;
            }
            Require(Mathf.Abs(accumulatedAngle - 360f * course.Turns) < 0.01f, "Clockwise spiral descends for exactly two turns at the default settings");
            Require(course.EndRadius < course.StartRadius, "Spiral shrinks toward the bottom");
            for (int i = 1; i < spline.Count - 1; i++)
            {
                var knot = spline[i];
                Vector3 incoming = -(Vector3)knot.TangentIn;
                Vector3 outgoing = knot.TangentOut;
                if (incoming.sqrMagnitude < 1e-8f || outgoing.sqrMagnitude < 1e-8f || Vector3.Dot(incoming.normalized, outgoing.normalized) < 0.9999f)
                    throw new InvalidOperationException("A knot has a zero or discontinuous heading");
            }
            Require(true, "All joins have nonzero, continuous tangent directions");
            float gapStart = table.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(spline, course.FallStartKnot));
            float gapEnd = table.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(spline, course.FallEndKnot));
            RequireNear(course.BeforeFall.EndDistance, gapStart, 0.001f, "Rail end before fall");
            RequireNear(course.AfterFall.StartDistance, gapEnd, 0.001f, "Rail restart after fall");
            float actualFallDrop = spline[course.FallStartKnot].Position.y - spline[course.FallEndKnot].Position.y;
            Require(gapEnd > gapStart + actualFallDrop, "Trackless fall has a continuous path and forward travel");
            Require(spline[course.FallEndKnot].Position.y < spline[course.FallStartKnot].Position.y, "Trackless fall lands below takeoff");
            Vector3 gapMiddle = course.Spline.EvaluatePosition(0, table.Parameter((gapStart + gapEnd) * 0.5f));
            foreach (var rail in new[] { course.BeforeFall, course.AfterFall })
            {
                Mesh mesh = rail.GeneratedRoot.GetComponent<MeshFilter>().sharedMesh;
                Require(AssetDatabase.Contains(mesh) && mesh.vertexCount > 0, "Rail mesh is saved as an asset");
                var vertices = mesh.vertices;
                if (vertices.Any(p => !Finite(p)))
                    throw new InvalidOperationException("Mesh has non-finite positions");
                float nearest = vertices.Min(p => Vector3.Distance(rail.transform.TransformPoint(p), gapMiddle));
                Require(nearest > 2f, "No rail mesh bridges the middle of the fall");
                File.AppendAllText(Report, $"Mesh: {mesh.vertexCount} vertices, range {rail.StartDistance:F3}..{rail.EndDistance:F3}\n");
            }
            for (int i = 0; i <= 3000; i++)
            {
                float t = (float)i / 3000;
                course.Spline.Evaluate(0, t, out var position, out var tangent, out var up);
                if (!Finite(position) || !Finite(tangent) || ((Vector3)tangent).sqrMagnitude < 1e-8f || Vector3.Cross(tangent, up).sqrMagnitude < 1e-8f)
                    throw new InvalidOperationException("Spline frame is invalid at " + t);
            }
            Require(true, "3001 full-route samples have valid position, tangent, and up frames");
            int landing = course.FallEndKnot;
            RequireNear(spline[landing].Position.y, spline[landing + 1].Position.y, 0.0001f, "Flat section after landing");
            if (course.ReturnSectionKnots.Length == 6)
            {
                int[] markers = course.ReturnSectionKnots;
                Vector2[] diagramPoints = { new Vector2(508,294), new Vector2(440,166), new Vector2(298,40),
                    new Vector2(128,140), new Vector2(50,226), new Vector2(52,351) };
                Vector3 origin = spline[landing].Position;
                for (int marker = 0; marker < markers.Length; marker++)
                {
                    Vector3 point = spline[markers[marker]].Position;
                    RequireNear(point.x - origin.x, (diagramPoints[marker].x - 508f) / 458f * course.ReturnWidth, 0.001f, "Diagram horizontal landmark");
                    RequireNear(point.z - origin.z, (294f - diagramPoints[marker].y) / 311f * course.ReturnDepth, 0.001f, "Diagram depth landmark");
                }
                float[] signs = { 0f, 1f, -1f, 1f, -1f };
                for (int section = 0; section < signs.Length; section++)
                {
                    float firstT = RollerCoasterCourseBuilder.KnotParameter(spline, markers[section]);
                    float lastT = RollerCoasterCourseBuilder.KnotParameter(spline, markers[section + 1]);
                    float previousHeight = ((Vector3)course.Spline.EvaluatePosition(0, firstT)).y;
                    for (int sample = 1; sample <= 200; sample++)
                    {
                        float height = ((Vector3)course.Spline.EvaluatePosition(0, Mathf.Lerp(firstT, lastT, sample / 200f))).y;
                        float delta = height - previousHeight;
                        if (signs[section] == 0f ? Mathf.Abs(height - origin.y) > 0.001f : delta * signs[section] < -0.0001f)
                            throw new InvalidOperationException("Wrong elevation direction in diagram section " + section);
                        previousHeight = height;
                    }
                }
                Require(true, "Diagram landmarks and black/red/blue/red/blue elevation directions match");
                Require(spline[markers[2]].Position.y > origin.y && spline[markers[4]].Position.y > spline[markers[3]].Position.y &&
                    spline[markers[5]].Position.y < origin.y, "Big curve climbs then descends; small bend climbs; final segment descends below landing");
            }
            else
                throw new InvalidOperationException("Generate the diagram route before verifying its sections");
            var originalRoots = course.gameObject.scene.GetRootGameObjects();
            Require(originalRoots.Any(x => x.name == "Cube") && originalRoots.Any(x => x.name == "Main Camera"), "Existing user scene roots are preserved");
            File.AppendAllText(Report, $"Course length {table.Length:F3}m; fall gap {gapStart:F3}..{gapEnd:F3}m; knots {spline.Count}\n");
            WriteRoutePreview(course);
        }


        private static void WriteRoutePreview(RollerCoasterCourse course)
        {
            var table = new RailDistanceTable(course.Spline);
            var rows = new System.Collections.Generic.List<string>();
            for (int i = 0; i <= 800; i++)
            {
                float distance = table.Length * i / 800;
                Vector3 p = course.Spline.EvaluatePosition(0, table.Parameter(distance));
                rows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{{\"x\":{0:F4},\"y\":{1:F4},\"z\":{2:F4},\"rail\":{3}}}", p.x, p.y, p.z,
                    distance < course.BeforeFall.EndDistance || distance > course.AfterFall.StartDistance ? "true" : "false"));
            }
            File.WriteAllText(Folder + "/route.json", "[" + string.Join(",", rows) + "]");
        }


        private static void Frame()
        {
            var course = FindCourse();
            Selection.activeGameObject = course.gameObject;
            SceneView.lastActiveSceneView?.LookAt(new Vector3(15f, -29f, 72f), Quaternion.Euler(27f, -40f, 0f), 100f, false, true);
            SceneView.RepaintAll();
        }


        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            File.AppendAllText(Report, "PASS " + message + "\n");
        }

        private static void RequireNear(float value, float expected, float tolerance, string message)
        {
            if (Mathf.Abs(value - expected) > tolerance)
                throw new InvalidOperationException(message + $": {value} != {expected}");
        }
    }
}
