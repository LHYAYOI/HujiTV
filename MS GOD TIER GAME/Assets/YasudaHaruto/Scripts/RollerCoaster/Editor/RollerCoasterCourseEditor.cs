using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace YasudaHaruto.RollerCoaster.Editor
{
    internal static class RollerCoasterCourseBuilder
    {
        internal const string ScenePath = "Assets/YasudaHaruto/Scenes/RollerCoaster.unity";
        private struct Point
        {
            public Vector3 Position;
            public Vector3 Direction;
            public Point(Vector3 position, Vector3 direction) { Position = position; Direction = direction.normalized; }
        }

        internal sealed class Layout
        {
            public Spline Spline;
            public int Crest, SpiralStart, SpiralEnd, FallStart, FallEnd, HillStart;
            public int[] ReturnSections;
        }

        internal static Layout MakeLayout(RollerCoasterCourse course)
        {
            if (course.EntryLength < 4f || course.EntryRise <= 0f || course.DescentLength < 12f || course.DescentDrop <= 0f ||
                course.Turns < 1 || course.Turns > 5 || course.StartRadius < 5f || course.EndRadius < 5f || course.EndRadius > course.StartRadius ||
                course.SpiralDrop < 5f || course.PointsPerTurn < 12 || course.PointsPerTurn > 48 || course.FallApproachLength < 4f ||
                course.FallForwardLength < 2f || course.FallDrop < 2f ||
                course.FirstHillHeight <= 0f || course.SecondHillHeight <= 0f || course.ReturnWidth < 50f || course.ReturnDepth < 40f || course.ExitDrop <= 0f)
                throw new ArgumentException("長さ・高さ・半径をInspectorの範囲へ合わせ、終端半径を開始半径以下にしてください。");
            var points = new List<Point>();
            var layout = new Layout();
            Vector3 forward = Vector3.forward;
            points.Add(new Point(Vector3.zero, new Vector3(0f, course.EntryRise / course.EntryLength, 1f)));
            points.Add(new Point(new Vector3(0f, course.EntryRise, course.EntryLength), forward));
            layout.Crest = 1;
            float descentStart = course.EntryLength;
            float bottom = course.EntryRise - course.DescentDrop;
            float grade = course.DescentDrop / course.DescentLength;
            points.Add(new Point(new Vector3(0f, course.EntryRise - course.DescentDrop * 0.2f, descentStart + course.DescentLength * 0.23f), new Vector3(0f, -grade, 1f)));
            points.Add(new Point(new Vector3(0f, course.EntryRise - course.DescentDrop * 0.76f, descentStart + course.DescentLength * 0.78f), new Vector3(0f, -grade, 1f)));

            float spiralZ = descentStart + course.DescentLength;
            float omega = 2f * Mathf.PI * course.Turns;
            int spiralSteps = course.Turns * course.PointsPerTurn;
            layout.SpiralStart = points.Count;
            for (int i = 0; i <= spiralSteps; i++)
            {
                float t = (float)i / spiralSteps;
                float angle = omega * t;
                float radius = Mathf.Lerp(course.StartRadius, course.EndRadius, t * t * (3f - 2f * t));
                float radiusDerivative = (course.EndRadius - course.StartRadius) * 6f * t * (1f - t);
                // 上から見て時計回り。開始／終了では半径変化の傾きを0にする。
                var position = new Vector3(course.StartRadius - radius * Mathf.Cos(angle), bottom - course.SpiralDrop * t, spiralZ + radius * Mathf.Sin(angle));
                var direction = new Vector3(-radiusDerivative * Mathf.Cos(angle) + radius * omega * Mathf.Sin(angle), -course.SpiralDrop,
                    radiusDerivative * Mathf.Sin(angle) + radius * omega * Mathf.Cos(angle));
                points.Add(new Point(position, direction));
            }
            layout.SpiralEnd = points.Count - 1;
            Vector3 spiralEnd = points[layout.SpiralEnd].Position;
            float finalGrade = course.SpiralDrop / (omega * course.EndRadius);
            Vector3 fallStart = spiralEnd + new Vector3(0f, -finalGrade * course.FallApproachLength, course.FallApproachLength);
            points.Add(new Point(fallStart, new Vector3(0f, -finalGrade, 1f)));
            layout.FallStart = points.Count - 1;
            points.Add(new Point(fallStart + new Vector3(0f, -course.FallDrop * 0.18f, course.FallForwardLength * 0.46f), new Vector3(0f, -1.2f, 1f)));
            points.Add(new Point(fallStart + new Vector3(0f, -course.FallDrop * 0.76f, course.FallForwardLength * 0.78f), new Vector3(0f, -1.8f, 1f)));
            Vector3 landing = fallStart + new Vector3(0f, -course.FallDrop, course.FallForwardLength);
            points.Add(new Point(landing, forward));
            layout.FallEnd = points.Count - 1;
            // ユーザーの拡大図の中心線。右下の黒→大きな赤→青→左の小さな赤→最後の青。
            // 俯瞰形状と高低差を同じ区間で生成し、「平坦な折り返しの後に山」を作らない。
            Vector2[][] curves =
            {
                new[] { new Vector2(508,294), new Vector2(508,248), new Vector2(450,202), new Vector2(440,166) },
                new[] { new Vector2(440,166), new Vector2(430,130), new Vector2(352,40), new Vector2(298,40) },
                new[] { new Vector2(298,40), new Vector2(244,40), new Vector2(128,92), new Vector2(128,140) },
                new[] { new Vector2(128,140), new Vector2(128,178), new Vector2(50,188), new Vector2(50,226) },
                new[] { new Vector2(50,226), new Vector2(50,264), new Vector2(52,310), new Vector2(52,351) }
            };
            float[] heights = { 0f, 0f, course.FirstHillHeight, 0f, course.SecondHillHeight, -course.ExitDrop };
            int[] subdivisions = { 4, 8, 8, 6, 6 };
            layout.ReturnSections = new int[6];
            layout.ReturnSections[0] = layout.FallEnd;
            for (int section = 0; section < curves.Length; section++)
            {
                var curve = curves[section];
                for (int i = 1; i <= subdivisions[section]; i++)
                {
                    float t = (float)i / subdivisions[section];
                    float u = 1f - t;
                    Vector2 pixel = u*u*u*curve[0] + 3f*u*u*t*curve[1] + 3f*u*t*t*curve[2] + t*t*t*curve[3];
                    Vector2 derivative = 3f*u*u*(curve[1]-curve[0]) + 6f*u*t*(curve[2]-curve[1]) + 3f*t*t*(curve[3]-curve[2]);
                    bool finalDescent = section == curves.Length - 1;
                    float heightT = finalDescent ? t*t*(2f-t) : t*t*(3f-2f*t);
                    float heightDerivative = finalDescent ? 4f*t-3f*t*t : 6f*t*(1f-t);
                    var position = landing + new Vector3((pixel.x-508f)/458f*course.ReturnWidth,
                        Mathf.Lerp(heights[section], heights[section+1], heightT), (294f-pixel.y)/311f*course.ReturnDepth);
                    var direction = new Vector3(derivative.x/458f*course.ReturnWidth,
                        (heights[section+1]-heights[section])*heightDerivative, -derivative.y/311f*course.ReturnDepth);
                    points.Add(new Point(position, direction));
                }
                layout.ReturnSections[section+1] = points.Count - 1;
            }
            layout.HillStart = layout.ReturnSections[1];

            layout.Spline = new Spline();
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                Vector3 incoming = i == 0 ? points[1].Position - point.Position : point.Position - points[i - 1].Position;
                Vector3 outgoing = i == points.Count - 1 ? incoming : points[i + 1].Position - point.Position;
                float inLength = incoming.magnitude / 3f * Mathf.Clamp01(Vector3.Dot(point.Direction, incoming.normalized));
                float outLength = outgoing.magnitude / 3f * Mathf.Clamp01(Vector3.Dot(point.Direction, outgoing.normalized));
                Quaternion rotation = Quaternion.LookRotation(point.Direction, Vector3.up);
                layout.Spline.Add(new BezierKnot(point.Position, new Vector3(0f, 0f, -inLength), new Vector3(0f, 0f, outLength), rotation), TangentMode.Broken);
            }
            return layout;
        }

        internal static float KnotParameter(Spline spline, int index) => SplineUtility.ConvertIndexUnit(spline, index, PathIndexUnit.Knot, PathIndexUnit.Normalized);

        internal static void Rebuild(RollerCoasterCourse course)
        {
            ApplyLayout(course, MakeLayout(course));
        }

        internal static void ApplyPreparedLayout(RollerCoasterCourse course, Layout layout) { ApplyLayout(course, layout); }

        internal static void RebuildRails(RollerCoasterCourse course)
        {
            if (course.Spline == null) throw new InvalidOperationException("Splineが未設定です。");
            ApplyLayout(course, new Layout
            {
                Spline = course.Spline.Spline, Crest = course.CrestKnot,
                SpiralStart = course.SpiralStartKnot, SpiralEnd = course.SpiralEndKnot,
                FallStart = course.FallStartKnot, FallEnd = course.FallEndKnot, HillStart = course.HillStartKnot, ReturnSections = course.ReturnSectionKnots
            });
        }

        private static void ApplyLayout(RollerCoasterCourse course, Layout layout)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("編集モードでコースを生成してください。");
            if (course.Spline == null || course.BeforeFall == null || course.AfterFall == null) throw new InvalidOperationException("経路・線路参照が未設定です。配置メニューから作成してください。");
            if (layout.FallStart < 0 || layout.FallEnd <= layout.FallStart || layout.FallEnd >= layout.Spline.Count)
                throw new InvalidOperationException("落下境界のノットが見つかりません。設計値からコースを再生成してください。");
            foreach (var rail in new[] { course.BeforeFall, course.AfterFall })
                if (rail.GeneratedRoot != null && rail.GeneratedRoot.transform.parent != rail.transform)
                    throw new InvalidOperationException("生成線路の親が変更されています。元へ戻してから再生成してください。");
            var spline = course.Spline;
            // 失敗した生成が以前の経路や線路を消さないように、先に一時Containerで両区間を計算する。
            var temporary = new GameObject("Course build staging") { hideFlags = HideFlags.HideAndDontSave };
            Mesh beforeMesh = null, afterMesh = null;
            try
            {
                temporary.transform.SetPositionAndRotation(spline.transform.position, spline.transform.rotation);
                temporary.transform.localScale = spline.transform.lossyScale;
                var stagingSpline = temporary.AddComponent<SplineContainer>();
                stagingSpline.Spline = layout.Spline;
                var table = new RailDistanceTable(stagingSpline);
                float gapStart = table.DistanceAtParameter(KnotParameter(layout.Spline, layout.FallStart));
                float gapEnd = table.DistanceAtParameter(KnotParameter(layout.Spline, layout.FallEnd));
                var first = CopySettings(course.BeforeFall, temporary, stagingSpline, 0f, gapStart);
                var second = CopySettings(course.AfterFall, temporary, stagingSpline, gapEnd, 0f);
                beforeMesh = RailSplineMeshGenerator.Build(first);
                afterMesh = RailSplineMeshGenerator.Build(second);

                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Build roller coaster course");
                Undo.RegisterCompleteObjectUndo(course, "Build roller coaster course");
                Undo.RegisterCompleteObjectUndo(spline, "Build roller coaster course");
                Undo.RegisterCompleteObjectUndo(course.BeforeFall, "Build roller coaster course");
                Undo.RegisterCompleteObjectUndo(course.AfterFall, "Build roller coaster course");
                spline.Spline = layout.Spline;
                course.SetGeneratedReferences(spline, course.BeforeFall, course.AfterFall, layout.Crest, layout.SpiralStart, layout.SpiralEnd, layout.FallStart, layout.FallEnd, layout.HillStart, layout.ReturnSections);
                SetRange(course.BeforeFall, 0f, gapStart);
                SetRange(course.AfterFall, gapEnd, 0f);
                RailSplineMeshGenerator.SaveBuiltMesh(course.BeforeFall, beforeMesh);
                beforeMesh = null;
                RailSplineMeshGenerator.SaveBuiltMesh(course.AfterFall, afterMesh);
                afterMesh = null;
                Undo.CollapseUndoOperations(group);
                EditorUtility.SetDirty(course);
                EditorSceneManager.MarkSceneDirty(course.gameObject.scene);
                SceneView.RepaintAll();
            }
            finally
            {
                if (beforeMesh != null && !AssetDatabase.Contains(beforeMesh)) UnityEngine.Object.DestroyImmediate(beforeMesh);
                if (afterMesh != null && !AssetDatabase.Contains(afterMesh)) UnityEngine.Object.DestroyImmediate(afterMesh);
                UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        private static RailSplineMesh CopySettings(RailSplineMesh source, GameObject parent, SplineContainer spline, float start, float end)
        {
            var go = new GameObject("Rail staging");
            go.transform.SetParent(parent.transform, false);
            // Buildの出力座標は最終的な線路rootに合わせる。
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            go.transform.localScale = Vector3.one;
            var copy = go.AddComponent<RailSplineMesh>();
            EditorUtility.CopySerialized(source, copy);
            copy.Configure(spline, source.SourceMesh, source.Materials);
            copy.SetGeneratedRoot(null);
            SetRange(copy, start, end);
            return copy;
        }

        internal static void SetRange(RailSplineMesh rail, float start, float end)
        {
            var serialized = new SerializedObject(rail);
            serialized.FindProperty("m_startDistance").floatValue = start;
            serialized.FindProperty("m_endDistance").floatValue = end;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/Roller Coaster/Create Course In RollerCoaster Scene")]
        internal static void CreateInScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<RollerCoasterCourse>(true)).Any())
                throw new InvalidOperationException("既にコースがあります。既存のRollerCoasterCourseから再生成してください。");
            var root = new GameObject("RollerCoaster Course");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var course = root.AddComponent<RollerCoasterCourse>();
                var route = new GameObject("Ride Spline");
                route.transform.SetParent(root.transform, false);
                var container = route.AddComponent<SplineContainer>();
                // 既存の走行システムへ、そのままContainerを渡せる参照を用意する。
                var controller = route.AddComponent<SplineController2>();
                var controllerData = new SerializedObject(controller);
                controllerData.FindProperty("m_spline").objectReferenceValue = container;
                controllerData.ApplyModifiedPropertiesWithoutUndo();
                var sampleAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RailSplineMeshSetup.SamplePath);
                if (sampleAsset == null) throw new InvalidOperationException("RailSplineSample.prefabがありません。先にCreate Rail Sampleを実行してください。");
                var sample = sampleAsset.GetComponent<RailSplineMesh>();
                var before = AddRail("Rails Before Fall");
                var after = AddRail("Rails After Fall");
                course.SetGeneratedReferences(container, before, after, 0, 0, 0, 0, 0);
                Rebuild(course);
                Undo.RegisterCreatedObjectUndo(root, "Create roller coaster course");
                Selection.activeGameObject = root;
                SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(18f, -29f, 72f), new Vector3(65f, 80f, 155f)), false);

                RailSplineMesh AddRail(string name)
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(root.transform, false);
                    var rail = go.AddComponent<RailSplineMesh>();
                    EditorUtility.CopySerialized(sample, rail);
                    rail.Configure(container, sample.SourceMesh, sample.Materials);
                    rail.SetGeneratedRoot(null);
                    return rail;
                }
            }
            catch { UnityEngine.Object.DestroyImmediate(root); throw; }
        }
    }

    [CustomEditor(typeof(RollerCoasterCourse))]
    public sealed class RollerCoasterCourseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("黒＝平坦、赤＝上り、青＝下り。形の再生成はSplineの手編集を置き換えます。速度と停止時間はTrial RideのSpeed Profileで調整します。入口・出口とワープは仮設定です。", MessageType.Info);
            if (GUILayout.Button("設計値からコースと線路を再生成"))
            {
                try { RollerCoasterCourseBuilder.Rebuild((RollerCoasterCourse)target); }
                catch (Exception exception) { Debug.LogException(exception, target); EditorUtility.DisplayDialog("コースを生成できません", exception.Message, "OK"); }
            }
            if (GUILayout.Button("現在のSplineから線路だけ再生成"))
            {
                try { RollerCoasterCourseBuilder.RebuildRails((RollerCoasterCourse)target); }
                catch (Exception exception) { Debug.LogException(exception, target); EditorUtility.DisplayDialog("線路を生成できません", exception.Message, "OK"); }
            }
            if (GUILayout.Button("走行用スプラインを選択")) Selection.activeGameObject = ((RollerCoasterCourse)target).Spline.gameObject;
        }

        private void OnSceneGUI()
        {
            var course = (RollerCoasterCourse)target;
            if (course.Spline == null || course.Spline.Spline.Count < 2) return;
            var spline = course.Spline.Spline;
            Label(0, "入口（仮）"); Label(course.CrestKnot, "下り直前／停止候補");
            Label(course.SpiralStartKnot, "らせん開始"); Label(course.SpiralEndKnot, "らせん終了");
            Label(course.FallStartKnot, "線路終了／落下開始"); Label(course.FallEndKnot, "線路再開"); Label(spline.Count - 1, "出口（仮）");
            Handles.color = new Color(1f, 0.5f, 0.1f);
            var gap = new Vector3[65];
            float start = RollerCoasterCourseBuilder.KnotParameter(spline, course.FallStartKnot);
            float end = RollerCoasterCourseBuilder.KnotParameter(spline, course.FallEndKnot);
            for (int i = 0; i < gap.Length; i++) gap[i] = course.Spline.EvaluatePosition(0, Mathf.Lerp(start, end, (float)i / (gap.Length - 1)));
            Handles.DrawAAPolyLine(4f, gap);
            if (course.ReturnSectionKnots.Length == 6)
            {
                Color[] colors = { Color.black, Color.red, Color.blue, Color.red, Color.blue };
                string[] names = { "平坦", "大きなカーブ：上り", "大きなカーブ：下り", "小さな曲がり：上り", "最後の下り" };
                for (int section = 0; section < 5; section++)
                {
                    int first = course.ReturnSectionKnots[section], last = course.ReturnSectionKnots[section+1];
                    if (first < 0 || last >= spline.Count) continue;
                    float firstT = RollerCoasterCourseBuilder.KnotParameter(spline, first);
                    float lastT = RollerCoasterCourseBuilder.KnotParameter(spline, last);
                    var line = new Vector3[97];
                    for (int i = 0; i < line.Length; i++) line[i] = course.Spline.EvaluatePosition(0, Mathf.Lerp(firstT, lastT, i/96f));
                    Handles.color = colors[section]; Handles.DrawAAPolyLine(5f, line);
                    Handles.Label(line[48] + Vector3.up, names[section]);
                }
            }
            void Label(int index, string label)
            {
                if (index < 0 || index >= spline.Count) return;
                Vector3 position = course.Spline.transform.TransformPoint(spline[index].Position);
                Handles.Label(position + Vector3.up, label);
            }
        }
    }
}
