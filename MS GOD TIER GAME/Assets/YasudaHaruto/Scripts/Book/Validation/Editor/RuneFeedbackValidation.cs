using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace YasudaHaruto.BookValidation.Editor
{
    public static class RuneFeedbackValidation
    {
        private sealed class TraceView : IRuneTraceView, IRuneTraceFeedbackView, IRuneCooldownView
        {
            public int Failures;
            public int Completions;
            public float Deviation;
            public float Cooldown;
            public void ShowCooldown(float remaining) => Cooldown = remaining;
            public void BeginTrace(RuneData rune) { }
            public void UpdateCurrentStroke(IReadOnlyList<Vector2> points) { }
            public void EndStroke() { }
            public void PlaySuccess(RuneTraceResult result) { }
            public void PlayFailure(RuneTraceResult result) => Failures++;
            public void Clear() { }
            public void SetTraceDeviation(float value) => Deviation = value;
            public void PlayCompletion() => Completions++;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Debug.Log("RuneFeedback PASS: " + message);
        }

        [MenuItem("Tools/YasudaHaruto/Verify Rune Feedback")]
        public static void Run()
        {
            Vector2 start = new Vector2(0.1f, 0.5f);
            Vector2 end = new Vector2(0.9f, 0.5f);
            var path = new List<Vector2> { start, end };
            var evaluator = new RuneTraceRealtimeEvaluator(path, 0.05f, 0.04f, 0.025f, 0.005f);
            Require(evaluator.Begin(start), "Valid start accepted");
            evaluator.EvaluateSegment(start, new Vector2(0.2f, 0.5f));
            Require(evaluator.NormalizedDeviation < 0.001f, "Exact line stays green");
            evaluator.EvaluateSegment(new Vector2(0.2f, 0.5f), new Vector2(0.3f, 0.525f));
            Require(evaluator.NormalizedDeviation > 0.45f && evaluator.NormalizedDeviation < 0.55f,
                "Half tolerance produces intermediate feedback");
            Require(evaluator.EvaluateSegment(new Vector2(0.3f, 0.525f), new Vector2(0.4f, 0.6f)) == RUNE_REALTIME_RESULT.FAILED,
                "Original distance threshold still fails");
            Require(evaluator.NormalizedDeviation == 1f, "Failed segment turns fully red");

            RuneData rune = ScriptableObject.CreateInstance<RuneData>();
            rune.TraceData.SetStrokes(new List<RuneStrokeData> { new RuneStrokeData(path) });
            var fake = new TraceView();
            var controller = new RuneTraceController(fake);
            try
            {
                controller.BeginTrace(rune);
                controller.BeginStroke(start);
                controller.AddPoint(new Vector2(0.2f, 0.6f));
                Require(fake.Failures == 1 && controller.State == RUNE_TRACE_STATE.FAILED_WAITING_RELEASE,
                    "Failure feedback starts immediately before release");
                controller.EndStroke();
                Require(fake.Failures == 1 && controller.CanBeginStroke,
                    "Release resets attempt without replaying failure");
                controller.BeginStroke(new Vector2(0.8f, 0.8f));
                Require(fake.Failures == 2, "Invalid start shows failure");
                controller.EndStroke();
                controller.BeginStroke(start);
                controller.AddPoint(new Vector2(0.2f, 0.5f));
                controller.EndStroke();
                Require(fake.Failures == 3, "Early release shows failure");
                controller.Cancel();
                Require(controller.State == RUNE_TRACE_STATE.INACTIVE, "Cancel ends tracing");
                controller.BeginTrace(rune, MAGIC_AIM_TYPE.NONE);
                controller.BeginStroke(start);
                controller.AddPoint(end);
                Require(controller.State == RUNE_TRACE_STATE.COOLDOWN && fake.Failures == 3,
                    "Accurate completed rune still reaches cast cooldown");
                Require(fake.Completions == 1, "Completion effect fires on full rune completion only");
                Require(Mathf.Approximately(fake.Cooldown, 1f), "Cooldown starts with a full circle");
                controller.Tick(0.25f);
                Require(Mathf.Approximately(fake.Cooldown, 1f), "Automatic cast waits for finger release before advancing");
                controller.EndStroke();
                controller.SetCooldown(1f);
                controller.Tick(0.25f);
                Require(Mathf.Approximately(fake.Cooldown, 0.75f), "Cooldown circle uses the controller remaining time");
                controller.Tick(1f);
                Require(controller.CanBeginStroke && Mathf.Approximately(fake.Cooldown, 0f), "Cooldown finishes and tracing becomes available again");
                controller.Cancel();
                CheckGraphics(rune);
                CheckCastGuide(rune, start, end);
                CheckCompletionTiming(start, end);
                CheckThickLineGeometry();
            }
            finally { UnityEngine.Object.DestroyImmediate(rune); }
            Debug.Log("RuneFeedback COMPLETE");
        }

        private static void CheckCompletionTiming(Vector2 start, Vector2 end)
        {
            RuneData rune = ScriptableObject.CreateInstance<RuneData>();
            rune.TraceData.SetStrokes(new List<RuneStrokeData>
            {
                new RuneStrokeData(new[] { start, end }),
                new RuneStrokeData(new[] { start, end })
            });
            var fake = new TraceView();
            var controller = new RuneTraceController(fake);
            try
            {
                controller.BeginTrace(rune);
                controller.BeginStroke(start);
                controller.AddPoint(end);
                Require(fake.Completions == 0, "Completing one stroke does not finish a multi-stroke rune");
                controller.EndStroke();
                controller.BeginStroke(start);
                controller.AddPoint(end);
                Require(fake.Completions == 1 && controller.State == RUNE_TRACE_STATE.WAITING_CAST_RELEASE,
                    "Last stroke triggers completion effect before finger release");
                controller.AddPoint(end);
                controller.EndStroke();
                Require(fake.Completions == 1 && controller.State == RUNE_TRACE_STATE.WAITING_CAST_START,
                    "Releasing finger does not replay completion effect");
                controller.Cancel();
            }
            finally { UnityEngine.Object.DestroyImmediate(rune); }
        }

        private static void CheckCastGuide(RuneData rune, Vector2 start, Vector2 end)
        {
            var fake = new TraceView();
            var controller = new RuneTraceController(fake);
            int casts = 0;
            MAGIC_CAST_DIRECTION lastDirection = MAGIC_CAST_DIRECTION.NONE;
            controller.CastConfirmed += direction => { casts++; lastDirection = direction; };
            GameObject display = new GameObject("Cast Guide Test Display", typeof(RectTransform), typeof(Canvas), typeof(CastSwipeDetector));
            display.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.ForceUpdateCanvases();
            var detector = display.GetComponent<CastSwipeDetector>();
            var settings = new SerializedObject(detector);
            settings.FindProperty("m_displayArea").objectReferenceValue = display.GetComponent<RectTransform>();
            settings.ApplyModifiedPropertiesWithoutUndo();
            var overlay = new GameObject("Cast Guide Test Overlay", typeof(RectTransform)).AddComponent<RuneCastOverlay>();
            overlay.Configure(controller, detector, () => new Rect(0f, 0f, 800f, 600f),
                new Vector2(0.5f, 0.25f), 0.08f, 0.55f, 1f, 0.08f);
            var events = new GameObject("Cast Guide Test Events", typeof(UnityEngine.EventSystems.EventSystem));
            var data = new UnityEngine.EventSystems.PointerEventData(events.GetComponent<UnityEngine.EventSystems.EventSystem>())
            {
                button = UnityEngine.EventSystems.PointerEventData.InputButton.Left,
                pointerId = -1
            };
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    controller.BeginTrace(rune);
                    controller.BeginStroke(start);
                    controller.AddPoint(end);
                    Require(controller.State == RUNE_TRACE_STATE.WAITING_CAST_RELEASE,
                        "Rune completion waits for original pointer release");
                    Require(!controller.TryBeginCast(), "Cannot begin aiming before release");
                    controller.ConfirmCast(MAGIC_CAST_DIRECTION.CENTER);
                    Require(casts == i, "Continuing original stroke cannot cast");
                    controller.EndStroke();
                    Require(controller.State == RUNE_TRACE_STATE.WAITING_CAST_START,
                        "Release enters guide wait without failing rune");
                    overlay.gameObject.SetActive(true);
                    data.position = overlay.Center + Vector2.right * overlay.Radius * 2f;
                    overlay.OnPointerDown(data);
                    data.position = overlay.Center;
                    overlay.OnDrag(data);
                    overlay.OnPointerUp(data);
                    Require(controller.State == RUNE_TRACE_STATE.WAITING_CAST_START && casts == i,
                        "Outside press and drag into circle are ignored");
                    overlay.OnPointerDown(data);
                    Require(controller.State == RUNE_TRACE_STATE.CHARGING, "Circle press begins aim");
                    float angle = (i - 1) * detector.GuideSideAngle * Mathf.Deg2Rad;
                    float length = display.GetComponent<RectTransform>().rect.height * 0.3f;
                    data.position = overlay.Center + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * length;
                    overlay.OnDrag(data);
                    overlay.OnPointerUp(data);
                    overlay.OnEndDrag(data);
                    Require(casts == i + 1 && (int)lastDirection == i && controller.State == RUNE_TRACE_STATE.COOLDOWN,
                        "Guide swipe casts correct direction exactly once: " + i);
                }
                controller.BeginTrace(rune);
                controller.BeginStroke(start);
                controller.AddPoint(end);
                controller.EndStroke();
                overlay.gameObject.SetActive(true);
                data.position = overlay.Center;
                overlay.OnPointerDown(data);
                controller.CancelCastGesture();
                Require(controller.State == RUNE_TRACE_STATE.WAITING_CAST_START, "Lost focus can safely retry aiming");
                overlay.Hide();
                overlay.gameObject.SetActive(true);
                overlay.OnPointerDown(data);
                overlay.OnPointerUp(data);
                Require(controller.CanBeginStroke && fake.Failures == 1 && casts == 3,
                    "Too short swipe keeps existing failure behavior without casting");
                controller.Cancel();
                Require(!controller.IsAwaitingCast, "Cancel clears aim wait");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overlay.gameObject);
                UnityEngine.Object.DestroyImmediate(display);
                UnityEngine.Object.DestroyImmediate(events);
            }
        }

        private static void CheckGraphics(RuneData rune)
        {
            GameObject root = new GameObject("Feedback Test", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            RunePageView view = root.AddComponent<RunePageView>();
            GameObject lineObject = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(RuneLineGraphic));
            lineObject.transform.SetParent(root.transform, false);
            RuneLineGraphic line = lineObject.GetComponent<RuneLineGraphic>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("m_playerGraphic").objectReferenceValue = line;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            RuneFailureOverlay overlay = null;
            RuneCompletionOverlay completion = null;
            try
            {
                view.ConfigureScreenProjection(() => new Rect(100f, 100f, 200f, 160f));
                view.BeginTrace(rune);
                var points = new List<Vector2> { new Vector2(0.1f, 0.5f), new Vector2(0.2f, 0.5f) };
                view.SetTraceDeviation(0f);
                view.UpdateCurrentStroke(points);
                view.SetTraceDeviation(1f);
                points.Add(new Vector2(0.3f, 0.6f));
                view.UpdateCurrentStroke(points);
                CheckMesh(line, false);
                view.EndStroke();
                CheckMesh(line, false);
                view.SetTraceDeviation(0f);
                CheckMesh(line, true);
                view.UpdateCurrentStroke(points);
                view.SetTraceDeviation(1f);
                CheckMesh(line, false);
                view.SetTraceDeviation(0f);
                CheckMesh(line, true);
                view.PlayCompletion();
                completion = UnityEngine.Object.FindFirstObjectByType<RuneCompletionOverlay>();
                Require(completion != null && completion.IsVisible, "Completed player strokes produce an effect");
                RuneLineGraphic echo = completion.GetComponentInChildren<RuneLineGraphic>();
                Require(!echo.raycastTarget && completion.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null,
                    "Completion effect never blocks the release or cast guide");
                typeof(RuneCompletionOverlay).GetMethod("ApplyProgress", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(completion, new object[] { 0.5f });
                Require(echo.rectTransform.localScale.x > 1f && echo.color.a > 0f && echo.color.a < line.color.a,
                    "Completion image expands and becomes transparent");
                typeof(RuneCompletionOverlay).GetMethod("ApplyProgress", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(completion, new object[] { 1f });
                Require(Mathf.Approximately(echo.color.a, 0f), "Completion image fades fully out");
                view.PlayFailure(new RuneTraceResult(0f, 0f, 0, false));
                Require(!completion.IsVisible, "Failure clears completion image");
                overlay = UnityEngine.Object.FindFirstObjectByType<RuneFailureOverlay>();
                Require(overlay != null && overlay.IsVisible, "Failure overlay is visible");
                Canvas canvas = overlay.GetComponent<Canvas>();
                Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.sortingOrder > 100,
                    "Failure overlay sits outside book capture on top of screen");
                RectTransform shade = overlay.transform.Find("Screen Shade").GetComponent<RectTransform>();
                Require(shade.anchorMin == Vector2.zero && shade.anchorMax == Vector2.one,
                    "Shade stretches over full screen");
                view.BeginTrace(rune);
                Require(overlay.IsVisible, "Retry does not erase failure animation");
                view.Clear();
                Require(!overlay.IsVisible, "Cancel clears failure overlay");
                view.BeginTrace(rune);
                view.UpdateCurrentStroke(points);
                view.EndStroke();
                view.ShowCooldown(1f);
                var cooldown = root.GetComponentInChildren<RuneCooldownGraphic>(true);
                Require(cooldown != null && cooldown.gameObject.activeSelf && !cooldown.raycastTarget,
                    "Cooldown ring is visible and does not capture input");
                using (var mesh = new VertexHelper())
                {
                    typeof(RuneLineGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Invoke(line, new object[] { mesh });
                    Require(mesh.currentVertCount == 0, "Cast removes the drawn rune");
                }
                cooldown.rectTransform.sizeDelta = new Vector2(200f, 200f);
                cooldown.SetRemaining(0.75f);
                using (var mesh = new VertexHelper())
                {
                    typeof(RuneCooldownGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Invoke(cooldown, new object[] { mesh });
                    UIVertex first = default;
                    mesh.PopulateUIVertex(ref first, 0);
                    Require(first.position.x > 0f && Mathf.Abs(first.position.y) < 0.01f,
                        "Quarter elapsed removes the clockwise top-right quarter");
                    cooldown.SetRemaining(0f);
                    typeof(RuneCooldownGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Invoke(cooldown, new object[] { mesh });
                    Require(mesh.currentVertCount == 0, "Finished cooldown has no ring geometry");
                }
                view.BeginTrace(rune);
                Require(!cooldown.gameObject.activeSelf, "New trace hides the timer");
                view.ShowCooldown(0.5f);
                view.Clear();
                Require(!cooldown.gameObject.activeSelf, "Cancel hides the timer");
                view.BeginTrace(rune);
                view.ShowCooldown(0.5f);
                // This validation runs in Edit Mode; dispatch the runtime lifecycle explicitly.
                typeof(RunePageView).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, null);
                Require(!cooldown.gameObject.activeSelf, "Disable notification hides the timer");
            }
            finally
            {
                if (overlay != null) UnityEngine.Object.DestroyImmediate(overlay.gameObject);
                if (completion != null) UnityEngine.Object.DestroyImmediate(completion.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CheckThickLineGeometry()
        {
            var root = new GameObject("Thick Line Validation", typeof(RectTransform), typeof(RuneLineGraphic));
            try
            {
                var line = root.GetComponent<RuneLineGraphic>();
                line.rectTransform.sizeDelta = new Vector2(400f, 300f);
                line.LineWidth = 28f;
                line.color = Color.green;
                var points = new List<Vector2>
                {
                    new Vector2(0.1f, 0.2f), new Vector2(0.1f, 0.2f),
                    new Vector2(0.5f, 0.8f), new Vector2(0.9f, 0.2f),
                    new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.2f)
                };
                line.SetCurrentStroke(points);
                CheckMesh(line, true);
                Require(points.Count == 6 && points[1] == points[0], "Rendering preserves raw input samples");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CheckMesh(RuneLineGraphic graphic, bool green)
        {
            using (var helper = new VertexHelper())
            {
                typeof(RuneLineGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance,
                    null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(graphic, new object[] { helper });
                UIVertex first = default, vertex = default;
                helper.PopulateUIVertex(ref first, 0);
                bool uniform = true, transparent = false, solid = false;
                for (int i = 0; i < helper.currentVertCount; i++)
                {
                    helper.PopulateUIVertex(ref vertex, i);
                    transparent |= vertex.color.a == 0;
                    solid |= vertex.color.a > 0;
                    uniform &= vertex.color.r == first.color.r && vertex.color.g == first.color.g && vertex.color.b == first.color.b;
                    if (float.IsNaN(vertex.position.x) || float.IsInfinity(vertex.position.x) ||
                        float.IsNaN(vertex.position.y) || float.IsInfinity(vertex.position.y))
                        throw new InvalidOperationException("Non-finite line geometry");
                }
                Require(uniform, "All drawn vertices use the same current RGB color");
                Require(transparent && solid, "Line has a solid core and transparent antialias fringe");
                Require(green ? first.color.g > first.color.r : first.color.r > first.color.g,
                    green ? "Whole drawing returns to green" : "Whole drawing turns red, including completed strokes");
            }
        }
    }
}
