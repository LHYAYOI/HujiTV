using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster.Editor
{
    /// <summary>現在の配置を保って終端直線を延ばし、3枚の破壊板と交差のニアミスを組み込む。</summary>
    internal static class RollerCoasterBreakthroughSetup
    {
        private const string Folder = "Assets/YasudaHaruto/Data/RollerCoaster/Breakthrough";

        [MenuItem("Tools/Roller Coaster/Apply Breakthrough Finale")]
        internal static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("編集モードで実行してください。");
            var ride = UnityEngine.Object.FindFirstObjectByType<RollerCoasterTrialRide>();
            if (ride == null || ride.gameObject.scene.path != RollerCoasterCourseBuilder.ScenePath)
                throw new InvalidOperationException("RollerCoaster Sceneが必要です。");
            EditorSceneManager.SaveScene(ride.gameObject.scene, "Assets/YasudaHaruto/Backups~/Before-breakthrough-scene-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            var story = ride.GetComponent<RollerCoasterStoryboard>();
            var sequence = ride.GetComponent<RollerCoasterBreakthroughSequence>();
            if (sequence == null) sequence = ride.gameObject.AddComponent<RollerCoasterBreakthroughSequence>();
            RailSplineMeshGenerator.EnsureFolder(Folder);
            if (sequence.StraightExtension < 29.9f) ExtendStraight(ride, story);
            foreach (var actor in story.Actors.Where(a => a.Kind == RollerCoasterStoryboard.ACTOR_KIND.CROSSING))
            {
                actor.ReverseCrossing = actor.Section == 10;
                actor.Route.name = actor.ReverseCrossing ? "10 Crossing Left To Right" : "09 Crossing Right To Left";
                TuneClearance(ride, story, actor);
            }
            var sparks = ride.GetComponent<RollerCoasterBrakeSparks>();
            if (sparks != null) { sparks.enabled = false; EditorUtility.SetDirty(sparks); }
            ConfigureCamera(ride);
            BuildBoards(ride, story, sequence);
            EditorUtility.SetDirty(story);
            EditorUtility.SetDirty(sequence);
            RollerCoasterStoryboardBuilder.RebuildDuctEnclosure();
            EditorSceneManager.MarkSceneDirty(ride.gameObject.scene);
            EditorSceneManager.SaveScene(ride.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        private static void ExtendStraight(RollerCoasterTrialRide ride, RollerCoasterStoryboard story)
        {
            var course = ride.Course;
            var original = new Spline(course.Spline.Spline);
            float oldLength = course.Spline.CalculateLength();
            var markers = story.Markers.Select(t => SplineUtility.ConvertIndexUnit(original, t, PathIndexUnit.Normalized, PathIndexUnit.Knot)).ToArray();
            float fall = SplineUtility.ConvertIndexUnit(original, story.FallEnd, PathIndexUnit.Normalized, PathIndexUnit.Knot);
            var data = new SerializedObject(story);
            var oldCurve = data.FindProperty("m_speeds").animationCurveValue;
            var spline = course.Spline.Spline;
            int edge = course.FallStartKnot;
            Vector3 forward = ((Vector3)spline[edge].Position - (Vector3)spline[edge - 1].Position);
            forward.y = 0;
            forward.Normalize();
            Vector3 shift = forward * 30f;
            for (int i = edge; i < spline.Count; i++)
            {
                var knot = spline[i];
                knot.Position += (Unity.Mathematics.float3)shift;
                spline.SetKnot(i, knot);
            }
            // 前区間の接線を変えず、最後の長い直線側の接線だけを進行方向へ揃える。
            var approach = spline[edge - 1];
            approach.TangentOut = Quaternion.Inverse(approach.Rotation) * forward * 6f;
            approach.TangentIn = -((Vector3)approach.TangentOut).normalized * ((Vector3)approach.TangentIn).magnitude;
            spline.SetKnot(edge - 1, approach);
            var lip = spline[edge];
            lip.TangentIn = Quaternion.Inverse(lip.Rotation) * -forward * 8f;
            spline.SetKnot(edge, lip);
            float Remap(float t) => SplineUtility.ConvertIndexUnit(spline,
                SplineUtility.ConvertIndexUnit(original, t, PathIndexUnit.Normalized, PathIndexUnit.Knot), PathIndexUnit.Knot, PathIndexUnit.Normalized);
            float[] newMarkers = markers.Select(k => SplineUtility.ConvertIndexUnit(spline, k, PathIndexUnit.Knot, PathIndexUnit.Normalized)).ToArray();
            float newFall = SplineUtility.ConvertIndexUnit(spline, fall, PathIndexUnit.Knot, PathIndexUnit.Normalized);
            float newLength = course.Spline.CalculateLength();
            var keys = oldCurve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].time = Remap(keys[i].time);
                keys[i].inTangent *= newLength / oldLength;
                keys[i].outTangent *= newLength / oldLength;
            }
            foreach (var actor in story.Actors) actor.Meeting = Remap(actor.Meeting);
            story.Configure(ride, newMarkers, newFall, new AnimationCurve(keys), story.Actors, story.Ceilings);
            var table = new RailDistanceTable(course.Spline);
            RollerCoasterCourseBuilder.SetRange(course.BeforeFall, 0, table.DistanceAtParameter(newMarkers[10]));
            RollerCoasterCourseBuilder.SetRange(course.AfterFall,
                table.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(spline, course.FallEndKnot + 1)), 0);
            RailSplineMeshGenerator.Generate(course.BeforeFall);
            RailSplineMeshGenerator.Generate(course.AfterFall);
            var catchRail = course.transform.Find("Image Course 1-11/11 Landing Catch Rail");
            catchRail.position += course.Spline.transform.TransformVector(shift);
            EditorUtility.SetDirty(course.Spline);
            EditorUtility.SetDirty(course.BeforeFall);
            EditorUtility.SetDirty(course.AfterFall);
            Directory.CreateDirectory("Temp/RollerCoasterStoryboard");
            File.WriteAllText("Temp/RollerCoasterStoryboard/straight-extension.txt", $"Course length {oldLength:F3} -> {newLength:F3}m; lip shifted by 30m.\n");
        }

        [MenuItem("Tools/Roller Coaster/Refine Smooth Camera And Full Boards")]
        internal static void RefineCameraAndBoards()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("編集モードで実行してください。");
            var ride = UnityEngine.Object.FindFirstObjectByType<RollerCoasterTrialRide>();
            if (ride == null || ride.gameObject.scene.path != RollerCoasterCourseBuilder.ScenePath)
                throw new InvalidOperationException("RollerCoaster Sceneが必要です。");
            EditorSceneManager.SaveScene(ride.gameObject.scene, "Assets/YasudaHaruto/Backups~/Before-full-boards-scene-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            ConfigureCamera(ride);
            BuildBoards(ride, ride.GetComponent<RollerCoasterStoryboard>(), ride.GetComponent<RollerCoasterBreakthroughSequence>());
            EditorUtility.SetDirty(ride.GetComponent<RollerCoasterBreakthroughSequence>());
            EditorSceneManager.MarkSceneDirty(ride.gameObject.scene);
            EditorSceneManager.SaveScene(ride.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureCamera(RollerCoasterTrialRide ride)
        {
            var data = new SerializedObject(ride.GetComponent<RollerCoasterCameraMotion>());
            data.FindProperty("m_curveCameraMode").enumValueIndex = (int)RollerCoasterCameraMotion.CURVE_CAMERA_MODE.DELAYED_ROLL;
            data.FindProperty("m_bankDelaySeconds").floatValue = .55f;
            data.FindProperty("m_bankMaxDegreesPerSecond").floatValue = 45f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void RefineImpactAndCrossings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("編集モードで実行してください。");
            var story = UnityEngine.Object.FindFirstObjectByType<RollerCoasterStoryboard>();
            if (story == null || story.gameObject.scene.path != RollerCoasterCourseBuilder.ScenePath)
                throw new InvalidOperationException("RollerCoaster Sceneが必要です。");
            EditorSceneManager.SaveScene(story.gameObject.scene, "Assets/YasudaHaruto/Backups~/Before-impact-timing-scene-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            var ride = story.GetComponent<RollerCoasterTrialRide>();
            foreach (var actor in story.Actors.Where(a => a.Kind == RollerCoasterStoryboard.ACTOR_KIND.CROSSING))
                TuneClearance(ride, story, actor);
            EditorUtility.SetDirty(story);
            EditorSceneManager.MarkSceneDirty(story.gameObject.scene);
            EditorSceneManager.SaveScene(story.gameObject.scene);
        }

        internal static void SmoothEntranceAndStrengthenFall()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("編集モードで実行してください。");
            var story = UnityEngine.Object.FindFirstObjectByType<RollerCoasterStoryboard>();
            if (story == null || story.gameObject.scene.path != RollerCoasterCourseBuilder.ScenePath)
                throw new InvalidOperationException("RollerCoaster Sceneが必要です。");
            EditorSceneManager.SaveScene(story.gameObject.scene, "Assets/YasudaHaruto/Backups~/Before-entry-tangent-scene-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
            var ride = story.GetComponent<RollerCoasterTrialRide>();
            var course = ride.Course;
            var original = new Spline(course.Spline.Spline);
            float oldLength = course.Spline.CalculateLength();
            var data = new SerializedObject(story);
            var oldCurve = data.FindProperty("m_speeds").animationCurveValue;
            var spline = course.Spline.Spline;
            int entryIndex = course.FallStartKnot - 1;
            var entry = spline[entryIndex];
            float oldAngle = Vector3.Angle(-(Vector3)entry.TangentIn, entry.TangentOut);
            // 直線側だけでなくカーブ側のハンドルも揃え、入口の方向の段差をなくす。
            spline.SetTangentMode(entryIndex, TangentMode.Broken);
            entry.TangentIn = -((Vector3)entry.TangentOut).normalized * ((Vector3)entry.TangentIn).magnitude;
            spline.SetKnot(entryIndex, entry);
            float Remap(float t) => SplineUtility.ConvertIndexUnit(spline,
                SplineUtility.ConvertIndexUnit(original, t, PathIndexUnit.Normalized, PathIndexUnit.Knot), PathIndexUnit.Knot, PathIndexUnit.Normalized);
            float newLength = course.Spline.CalculateLength();
            var keys = oldCurve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].time = Remap(keys[i].time);
                keys[i].inTangent *= newLength / oldLength;
                keys[i].outTangent *= newLength / oldLength;
            }
            var curve = new AnimationCurve(keys) { preWrapMode = oldCurve.preWrapMode, postWrapMode = oldCurve.postWrapMode };
            float[] markers = story.Markers.Select(Remap).ToArray();
            float fall = Remap(story.FallEnd);
            foreach (var actor in story.Actors) actor.Meeting = Remap(actor.Meeting);
            story.Configure(ride, markers, fall, curve, story.Actors, story.Ceilings);
            var table = new RailDistanceTable(course.Spline);
            RollerCoasterCourseBuilder.SetRange(course.BeforeFall, 0, table.DistanceAtParameter(markers[10]));
            RollerCoasterCourseBuilder.SetRange(course.AfterFall,
                table.DistanceAtParameter(RollerCoasterCourseBuilder.KnotParameter(spline, course.FallEndKnot + 1)), 0);
            RailSplineMeshGenerator.Generate(course.BeforeFall);
            RailSplineMeshGenerator.Generate(course.AfterFall);
            foreach (var actor in story.Actors.Where(a => a.Kind == RollerCoasterStoryboard.ACTOR_KIND.CROSSING))
                TuneClearance(ride, story, actor);
            var camera = new SerializedObject(ride.GetComponent<RollerCoasterCameraMotion>());
            camera.FindProperty("m_airborneStrength").floatValue = .5f;
            camera.FindProperty("m_fallVerticalScale").floatValue = 1.8f;
            camera.FindProperty("m_fallShakeDegrees").floatValue = 1.8f;
            camera.FindProperty("m_landingPitch").floatValue = 8f;
            camera.FindProperty("m_landingYaw").floatValue = 1.4f;
            camera.FindProperty("m_landingRoll").floatValue = 2.2f;
            camera.FindProperty("m_landingDuration").floatValue = .7f;
            camera.FindProperty("m_landingSinkMeters").floatValue = .3f;
            camera.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(course.Spline);
            EditorUtility.SetDirty(course.BeforeFall);
            EditorUtility.SetDirty(course.AfterFall);
            EditorUtility.SetDirty(story);
            RollerCoasterStoryboardBuilder.RebuildDuctEnclosure();
            Directory.CreateDirectory("Temp/RollerCoasterStoryboard");
            File.WriteAllText("Temp/RollerCoasterStoryboard/entry-smoothing.txt", $"Entry handle angle {oldAngle:F4} -> {Vector3.Angle(-(Vector3)entry.TangentIn, entry.TangentOut):F4} degrees.\n");
        }

        private static void TuneClearance(RollerCoasterTrialRide ride, RollerCoasterStoryboard story, RollerCoasterStoryboard.ACTOR actor)
        {
            Bounds Measure(Transform t)
            {
                var bounds = RollerCoasterCartClearance.Measure(t);
                bounds.center = Vector3.Scale(bounds.center, t.lossyScale);
                bounds.size = Vector3.Scale(bounds.size, t.lossyScale);
                return bounds;
            }
            var player = Measure(ride.Cart.transform);
            var other = Measure(actor.Cart);
            float Gap(float lead)
            {
                actor.CrossingLeadMeters = lead;
                float result = float.PositiveInfinity;
                for (float delta = -12f; delta < 10f; delta += .01f)
                {
                    float progress = actor.Meeting + delta / story.Length;
                    actor.Route.Evaluate(0, story.CrossingParameterAt(actor, delta), out var p, out var tangent, out var up);
                    if (actor.ReverseCrossing) tangent = -tangent;
                    result = Mathf.Min(result, RollerCoasterCartClearance.Gap(player, ride.Course.Spline.EvaluatePosition(0, progress), story.RotationAt(progress),
                        other, p, Quaternion.LookRotation(tangent, up)));
                }
                return result;
            }
            float low = 2f, high = 8f;
            if (Gap(high) < .05f) throw new InvalidOperationException("交差車の安全な先行距離を確保できません。");
            for (int i = 0; i < 14; i++)
            {
                float middle = (low + high) * .5f;
                if (Gap(middle) >= .05f) high = middle; else low = middle;
            }
            actor.CrossingLeadMeters = high;
        }

        private static void BuildBoards(RollerCoasterTrialRide ride, RollerCoasterStoryboard story, RollerCoasterBreakthroughSequence sequence)
        {
            const float radius = 2.72f, centerY = 1.4f;
            var root = ride.Course.transform.Find("Image Course 1-11");
            var old = root.Find("11 Breakthrough Barriers");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var boardsRoot = new GameObject("11 Breakthrough Barriers").transform;
            boardsRoot.SetParent(root, false);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Warning Wood.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("YasudaHaruto/RollerCoaster/Warning Board"));
                AssetDatabase.CreateAsset(material, Folder + "/Warning Wood.mat");
            }
            string meshPath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Board Fragments.asset");
            var textMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Board Text.mat");
            if (textMaterial == null)
            {
                textMaterial = new Material(Shader.Find("YasudaHaruto/RollerCoaster/Board Text"));
                AssetDatabase.CreateAsset(textMaterial, Folder + "/Board Text.mat");
            }
            var supportMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Wood Supports.mat");
            if (supportMaterial == null)
            {
                supportMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                supportMaterial.SetColor("_BaseColor", new Color(.32f, .17f, .065f));
                supportMaterial.SetFloat("_Smoothness", .1f);
                AssetDatabase.CreateAsset(supportMaterial, Folder + "/Wood Supports.mat");
            }
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var source = primitive.GetComponent<MeshFilter>().sharedMesh;
            var pieces = new List<Mesh>();
            var centers = new List<Vector3>();
            var outline = Enumerable.Range(0, 16).Select(i => new Vector2(Mathf.Cos(i * Mathf.PI / 8f) * radius, centerY + Mathf.Sin(i * Mathf.PI / 8f) * radius)).ToList();

            Mesh Extrude(List<Vector2> polygon, Vector2 center, string name)
            {
                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var uv = new List<Vector2>();
                var triangles = new List<int>();
                void Vertex(Vector2 p, float z, Vector3 normal)
                {
                    vertices.Add(new Vector3(p.x - center.x, p.y - center.y, z));
                    normals.Add(normal);
                    uv.Add(new Vector2(p.x / radius * 1.2f, 1.275f + (p.y - centerY) / radius * .825f));
                }
                int count = polygon.Count;
                foreach (var p in polygon) Vertex(p, -.045f, Vector3.back);
                foreach (var p in polygon) Vertex(p, .045f, Vector3.forward);
                for (int i = 1; i < count - 1; i++)
                    triangles.AddRange(new[] { 0, i + 1, i, count, count + i, count + i + 1 });
                for (int i = 0; i < count; i++)
                {
                    Vector2 a = polygon[i], b = polygon[(i + 1) % count], edge = b - a;
                    Vector3 normal = new Vector3(edge.y, -edge.x, 0).normalized;
                    int n = vertices.Count;
                    Vertex(a, -.045f, normal); Vertex(b, -.045f, normal);
                    Vertex(b, .045f, normal); Vertex(a, .045f, normal);
                    triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
                }
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }

            // 衝突前はこの単一の閉じたメッシュだけを表示し、破片の境界を一切描かない。
            var intactMesh = Extrude(outline, Vector2.zero, "Seamless Full Duct Barrier");
            AssetDatabase.CreateAsset(intactMesh, meshPath);
            float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
            for (int row = 0; row < 8; row++)
                for (int col = 0; col < 8; col++)
                {
                    float x = -radius + col * radius / 4f, y = centerY - radius + row * radius / 4f, size = radius / 4f;
                    var polygon = new List<Vector2> { new Vector2(x, y), new Vector2(x + size, y), new Vector2(x + size, y + size), new Vector2(x, y + size) };
                    // 格子を板の外周で切り抜く。衝突後も全破片を合わせると同じ板形状になる。
                    for (int edge = 0; edge < outline.Count && polygon.Count > 0; edge++)
                    {
                        Vector2 a = outline[edge], b = outline[(edge + 1) % outline.Count];
                        var clipped = new List<Vector2>();
                        Vector2 previous = polygon[polygon.Count - 1];
                        float before = Cross(b - a, previous - a);
                        foreach (var current in polygon)
                        {
                            float after = Cross(b - a, current - a);
                            if ((before >= 0) != (after >= 0)) clipped.Add(Vector2.LerpUnclamped(previous, current, before / (before - after)));
                            if (after >= 0) clipped.Add(current);
                            previous = current; before = after;
                        }
                        polygon = clipped;
                    }
                    polygon = polygon.Distinct().ToList();
                    if (polygon.Count < 3 || Mathf.Abs(Enumerable.Range(0, polygon.Count).Sum(i => Cross(polygon[i], polygon[(i + 1) % polygon.Count]))) < .0002f) continue;
                    Vector2 center = polygon.Aggregate(Vector2.zero, (a, b) => a + b) / polygon.Count;
                    var mesh = Extrude(polygon, center, $"Clipped wood {row}-{col}");
                    AssetDatabase.AddObjectToAsset(mesh, meshPath);
                    pieces.Add(mesh);
                    centers.Add(new Vector3(center.x, center.y, 0));
                }
            // 板が空中に浮かないよう、床まで届く木の支柱も破片として用意する。
            for (int i = 0; i < 2; i++)
            {
                var mesh = UnityEngine.Object.Instantiate(source);
                mesh.name = "Wood support " + i;
                mesh.vertices = mesh.vertices.Select(v => Vector3.Scale(v, new Vector3(.12f, 2.1f, .12f))).ToArray();
                mesh.RecalculateBounds();
                AssetDatabase.AddObjectToAsset(mesh, meshPath);
                pieces.Add(mesh);
                centers.Add(new Vector3(i == 0 ? -1.05f : 1.05f, -.45f, .1f));
            }
            UnityEngine.Object.DestroyImmediate(primitive);
            var barriers = new List<RollerCoasterBreakthroughSequence.Barrier>();
            foreach (float distance in new[] { 30f, 20f, 10f })
            {
                var board = new GameObject($"No Entry Board {barriers.Count + 1}").transform;
                board.SetParent(boardsRoot, false);
                float t = story.Markers[10] - distance / story.Length;
                ride.Course.Spline.Evaluate(0, t, out var p, out var tangent, out var up);
                board.SetPositionAndRotation(p, Quaternion.LookRotation(tangent, up));
                var intact = new GameObject("Intact Seamless Plate");
                intact.transform.SetParent(board, false);
                intact.AddComponent<MeshFilter>().sharedMesh = intactMesh;
                intact.AddComponent<MeshRenderer>().sharedMaterial = material;
                for (int i = pieces.Count - 2; i < pieces.Count; i++)
                {
                    var support = new GameObject("Intact Wood Support");
                    support.transform.SetParent(intact.transform, false);
                    support.transform.localPosition = centers[i];
                    support.AddComponent<MeshFilter>().sharedMesh = pieces[i];
                    support.AddComponent<MeshRenderer>().sharedMaterial = supportMaterial;
                }
                var fragments = new List<Transform>();
                for (int i = 0; i < pieces.Count; i++)
                {
                    var piece = new GameObject("Breakable Wood " + i).transform;
                    piece.SetParent(board, false);
                    piece.localPosition = centers[i];
                    piece.gameObject.AddComponent<MeshFilter>().sharedMesh = pieces[i];
                    piece.gameObject.AddComponent<MeshRenderer>().sharedMaterial = i < pieces.Count - 2 ? material : supportMaterial;
                    piece.gameObject.SetActive(false);
                    fragments.Add(piece);
                }
                var lettering = new GameObject("NO ENTRY Lettering");
                lettering.transform.SetParent(board, false);
                lettering.transform.localPosition = new Vector3(0, centerY, -.052f);
                var text = lettering.AddComponent<TextMesh>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 128;
                text.characterSize = .2f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = Color.black;
                text.text = "NO ENTRY";
                var renderer = lettering.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = textMaterial;
                float width = renderer.localBounds.size.x;
                if (width > .01f) lettering.transform.localScale = Vector3.one * (3.6f / width);
                lettering.AddComponent<RollerCoasterBoardText>();
                barriers.Add(new RollerCoasterBreakthroughSequence.Barrier { Root = board, MetersBeforeEdge = distance, Lettering = lettering,
                    IntactVisual = intact, Fragments = fragments.ToArray() });
            }
            sequence.Configure(barriers.ToArray(), 30f);
        }
    }
}
