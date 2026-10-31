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
                Require(Mathf.Approximately(fake.Cooldown, 1f), "Cooldown starts at full remaining time");
                controller.Tick(0.25f);
                Require(Mathf.Approximately(fake.Cooldown, 1f), "Automatic cast waits for finger release before advancing");
                controller.EndStroke();
                controller.SetCooldown(1f);
                controller.Tick(0.25f);
                Require(Mathf.Approximately(fake.Cooldown, 0.75f), "Cooldown feedback uses the controller remaining time");
                controller.Tick(1f);
                Require(controller.CanBeginStroke && Mathf.Approximately(fake.Cooldown, 0f), "Cooldown finishes and tracing becomes available again");
                controller.Cancel();
                CheckGraphics(rune);
                CheckCastGuide(rune, start, end);
                CheckCompletionTiming(start, end);
                CheckThickLineGeometry();
                CheckCooldownShader();
                CheckPageImageBinding();
                CheckCanvasCooldown();
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
                var image = new GameObject("Existing Colored Rune", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(root.transform, false);
                image.rectTransform.sizeDelta = new Vector2(200f, 200f);
                Material original = image.material;
                view.ConfigureCooldownImage(() => image);
                view.PlayCooldownRelease();
                view.ShowCooldown(1f);
                var cooldown = root.GetComponent<RuneCooldownImageFeedback>();
                Require(cooldown != null && cooldown.IsVisible && image.material != original,
                    "Cooldown operates on the existing colored image");
                Require(Mathf.Approximately(image.material.GetFloat("_Drain"), 0f), "Release begins without an abrupt dark jump");
                cooldown.Advance(0.05f);
                Require(image.material.GetFloat("_Drain") > 0.4f && image.material.GetFloat("_Drain") < 0.6f,
                    "One-second cooldown reaches half drain after 0.05 seconds");
                Require(image.material.GetFloat("_Flash") > 0f, "Release briefly brightens the rune");
                Image releaseEcho = root.transform.Find("Rune Release Light").GetComponent<Image>();
                Require(!releaseEcho.raycastTarget && releaseEcho.rectTransform.anchoredPosition.y > image.rectTransform.anchoredPosition.y &&
                    releaseEcho.color.a > 0f, "Release light rises, fades and never blocks input");
                cooldown.Advance(1f);
                Require(Mathf.Approximately(image.material.GetFloat("_Drain"), 1f) && !releaseEcho.gameObject.activeSelf,
                    "Release finishes with the rune drained and the releaseEcho hidden");
                view.ShowCooldown(0.75f);
                Require(Mathf.Approximately(image.material.GetFloat("_Recovery"), 0.25f), "Recovery follows the controller's remaining time");
                using (var mesh = new VertexHelper())
                {
                    typeof(RuneLineGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Invoke(line, new object[] { mesh });
                    Require(mesh.currentVertCount == 0, "Cast removes the drawn rune");
                }
                view.ShowCooldown(0f);
                Require(!cooldown.IsVisible && image.material == original, "Completion restores the exact original material");
                view.BeginTrace(rune);
                view.ShowCooldown(0.5f);
                Require(Mathf.Approximately(image.material.GetFloat("_Drain"), 1f) &&
                    Mathf.Approximately(image.material.GetFloat("_Flash"), 0f), "Resuming a page does not replay the release");
                view.Clear();
                Require(!cooldown.IsVisible && image.material == original, "Cancel restores the original image");
                view.BeginTrace(rune);
                view.ShowCooldown(0.5f);
                typeof(RunePageView).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, null);
                Require(!cooldown.IsVisible && image.material == original, "Disable restores the original image");
                view.ConfigureCooldownImage(() => null);
                view.ShowCooldown(0.5f);
                Require(!cooldown.IsVisible, "Missing image does not break cooldown timing");
            }
            finally
            {
                if (overlay != null) UnityEngine.Object.DestroyImmediate(overlay.gameObject);
                if (completion != null) UnityEngine.Object.DestroyImmediate(completion.gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CheckPageImageBinding()
        {
            foreach (string name in new[] { "07", "09", "11" })
            {
                var root = new GameObject("Page Image Binding Test", typeof(RectTransform), typeof(Canvas));
                try
                {
                    var pageObject = new GameObject("Page", typeof(RectTransform), typeof(BookPageView));
                    pageObject.transform.SetParent(root.transform, false);
                    var pageView = pageObject.GetComponent<BookPageView>();
                    var content = new GameObject("PageContent", typeof(RectTransform)).GetComponent<RectTransform>();
                    content.SetParent(pageObject.transform, false);
                    var settings = new SerializedObject(pageView);
                    settings.FindProperty("m_pageContent").objectReferenceValue = content;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    var page = AssetDatabase.LoadAssetAtPath<BookPageData>("Assets/YasudaHaruto/Data/PageView/PageView_" + name + ".asset");
                    pageView.ShowPage(page);
                    var owner = root.AddComponent<YasudaBookViewController>();
                    typeof(YasudaBookViewController).GetField("m_leftPageView", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, pageView);
                    var pages = (List<BookPageData>)typeof(YasudaBookViewController).GetField("m_runtimePages", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
                    pages.Add(page);
                    Image image = owner.GetCurrentRuneImage(-1);
                    Require(image != null && image.sprite == page.Images[page.Images.Count - 1].Sprite,
                        "PageView_" + name + " binds its existing colored rune without duplicating sprite data");
                    Require(owner.GetCurrentRuneImage(0).sprite == page.Images[0].Sprite && owner.GetCurrentRuneImage(999) == null,
                        "Explicit image index and invalid index are handled");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }

        private static void CheckCanvasCooldown()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Cooldown Canvas Test", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var cameraObject = new GameObject("Cooldown Test Camera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            var texture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.enabled = false;
                camera.scene = scene;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true; camera.orthographicSize = 128f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.targetTexture = texture; texture.Create();
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(256f, 256f);
                var image = new GameObject("Translated Scaled Rune", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(root.transform, false);
                image.rectTransform.pivot = new Vector2(0f, 1f);
                image.rectTransform.anchoredPosition = new Vector2(-40f, 90f);
                image.rectTransform.sizeDelta = new Vector2(80f, 120f);
                image.rectTransform.localScale = new Vector3(1.2f, 0.8f, 1f);
                var feedback = root.AddComponent<RuneCooldownImageFeedback>();
                feedback.Show(image, false, 0.1f, 0.28f, 0.12f);
                var corners = new Vector3[4]; image.rectTransform.GetWorldCorners(corners);
                Vector3 bottom = camera.WorldToScreenPoint(corners[0]);
                Vector3 top = camera.WorldToScreenPoint(corners[2]);
                for (int step = 1; step <= 3; step++)
                {
                    feedback.SetRemaining(1f - step * 0.25f);
                    Canvas.ForceUpdateCanvases(); camera.Render();
                    RenderTexture.active = texture;
                    pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); pixels.Apply();
                    int bright = 0;
                    int x = Mathf.RoundToInt((bottom.x + top.x) * 0.5f);
                    int first = Mathf.CeilToInt(bottom.y), last = Mathf.FloorToInt(top.y) - 1;
                    for (int y = first; y <= last; y++) if (pixels.GetPixel(x, y).r > 0.8f) bright++;
                    float actual = (float)bright / (last - first + 1);
                    Require(Mathf.Abs(actual - step * 0.25f) < 0.04f,
                        "Batched Canvas restores translated, scaled, top-pivot image at " + step + "/4; actual=" + actual);
                }
                feedback.Hide();
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(pixels);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void CheckCooldownShader()
        {
            Shader shader = Resources.Load<Shader>("RuneCooldownImage");
            Require(shader != null && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "Cooldown shader compiles for the current graphics API");
            var material = new Material(shader);
            var texture = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            bool pushed = false;
            try
            {
                texture.Create();
                RenderTexture.active = texture;
                GL.Clear(true, true, Color.black);
                material.SetTexture("_MainTex", Texture2D.whiteTexture);
                material.SetFloat("_Darkness", 0.28f);
                material.SetFloat("_Drain", 1f);
                material.SetFloat("_Recovery", 0.5f);
                GL.PushMatrix(); pushed = true;
                GL.LoadOrtho();
                material.SetPass(0);
                GL.Begin(GL.QUADS); GL.Color(Color.white);
                GL.MultiTexCoord2(0, 0f, 0f); GL.MultiTexCoord2(1, 0f, 0f); GL.Vertex3(0f, 0f, 0f);
                GL.MultiTexCoord2(0, 0f, 1f); GL.MultiTexCoord2(1, 1f, 0f); GL.Vertex3(0f, 1f, 0f);
                GL.MultiTexCoord2(0, 1f, 1f); GL.MultiTexCoord2(1, 1f, 0f); GL.Vertex3(1f, 1f, 0f);
                GL.MultiTexCoord2(0, 1f, 0f); GL.MultiTexCoord2(1, 0f, 0f); GL.Vertex3(1f, 0f, 0f);
                GL.End(); GL.PopMatrix(); pushed = false;
                readback.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); readback.Apply();
                Require(readback.GetPixel(16, 8).r > 0.9f && readback.GetPixel(16, 24).r < 0.65f,
                    "GPU output restores brightness from bottom while the top stays dark");
                for (int step = 1; step <= 3; step++)
                {
                    material.SetFloat("_Recovery", step * 0.25f);
                    DrawCooldownQuad(material);
                    readback.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); readback.Apply();
                    int brightRows = 0;
                    for (int row = 0; row < 32; row++) if (readback.GetPixel(16, row).r > 0.8f) brightRows++;
                    Require(Mathf.Abs(brightRows - step * 8) <= 1,
                        "Equal time steps restore equal height: " + step + "/4");
                }
                var green = new Texture2D(1, 1);
                try
                {
                    green.SetPixel(0, 0, new Color(0.2f, 0.6f, 0.1f, 1f)); green.Apply();
                    material.SetTexture("_MainTex", green);
                    material.SetFloat("_Recovery", 0.5f); material.SetFloat("_Drain", 0.5f); material.SetFloat("_Flash", 0.65f);
                    DrawCooldownQuad(material);
                    readback.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); readback.Apply();
                    Require(readback.GetPixel(16, 8).r < readback.GetPixel(16, 24).r - 0.1f,
                        "Release light no longer overlays the already recovered area: bottom=" + readback.GetPixel(16, 8) + ", top=" + readback.GetPixel(16, 24));
                }
                finally { UnityEngine.Object.DestroyImmediate(green); }
                material.SetFloat("_Flash", 0f);
                RenderCooldownPreview(material);
            }
            finally
            {
                if (pushed) GL.PopMatrix();
                RenderTexture.active = previous;
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(readback);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static void DrawCooldownQuad(Material material)
        {
            GL.Clear(true, true, Color.black);
            GL.PushMatrix();
            GL.LoadOrtho(); material.SetPass(0);
            GL.Begin(GL.QUADS); GL.Color(Color.white);
            GL.MultiTexCoord2(0, 0f, 0f); GL.MultiTexCoord2(1, 0f, 0f); GL.Vertex3(0f, 0f, 0f);
            GL.MultiTexCoord2(0, 0f, 1f); GL.MultiTexCoord2(1, 1f, 0f); GL.Vertex3(0f, 1f, 0f);
            GL.MultiTexCoord2(0, 1f, 1f); GL.MultiTexCoord2(1, 1f, 0f); GL.Vertex3(1f, 1f, 0f);
            GL.MultiTexCoord2(0, 1f, 0f); GL.MultiTexCoord2(1, 0f, 0f); GL.Vertex3(1f, 0f, 0f);
            GL.End(); GL.PopMatrix();
        }

        private static void RenderCooldownPreview(Material material)
        {
            var target = new RenderTexture(1280, 256, 0, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1280, 256, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create(); RenderTexture.active = target;
                GL.Clear(true, true, new Color(0.94f, 0.91f, 0.8f, 1f));
                material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/YasudaHaruto/Data/Runes/Images/Runa_Clean.png"));
                for (int i = 0; i < 5; i++)
                {
                    material.SetFloat("_Drain", i == 0 ? 0f : 1f);
                    material.SetFloat("_Recovery", i == 0 || i == 4 ? 1f : i == 3 ? 0.5f : 0f);
                    material.SetFloat("_Flash", i == 1 ? 0.65f : 0f);
                    GL.PushMatrix();
                    GL.LoadOrtho(); material.SetPass(0);
                    GL.Begin(GL.QUADS); GL.Color(Color.white);
                    float left = i / 5f, right = (i + 1) / 5f;
                    GL.MultiTexCoord2(0, 0f, 0f); GL.MultiTexCoord2(1, 0f, 0f); GL.Vertex3(left, 0f, 0f);
                    GL.MultiTexCoord2(0, 0f, 1f); GL.MultiTexCoord2(1, 1f, 0f); GL.Vertex3(left, 1f, 0f);
                    GL.MultiTexCoord2(0, 1f, 1f); GL.MultiTexCoord2(1, 1f, 0f); GL.Vertex3(right, 1f, 0f);
                    GL.MultiTexCoord2(0, 1f, 0f); GL.MultiTexCoord2(1, 0f, 0f); GL.Vertex3(right, 0f, 0f);
                    GL.End(); GL.PopMatrix();
                }
                pixels.ReadPixels(new Rect(0, 0, 1280, 256), 0, 0); pixels.Apply();
                System.IO.Directory.CreateDirectory("Temp");
                System.IO.File.WriteAllBytes("Temp/RuneCooldownPreview.png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
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
