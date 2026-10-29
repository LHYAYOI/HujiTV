using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YasudaHaruto.BookValidation.Editor
{
    [InitializeOnLoad]
    public static class RuneSpellValidation
    {
        private const string Running = "Yasuda.RuneSpellValidation";
        private const string Report = "Temp/RuneSpellValidation/runtime-result.txt";
        private static int s_step, s_spread, s_castCount;
        private static double s_next, s_deadline;
        private static BookGameController s_game;
        private static RuneTraceController s_controller;
        private static RunePageView s_view;
        private static RuneSpellEffect s_effect;
        private static RuneCastOverlay s_overlay;
        private static RuneData s_rune;
        private static RuneSpellSettings s_settings;
        private static Vector2 s_home, s_pointer;
        private static MAGIC_CAST_DIRECTION s_lastDirection;

        static RuneSpellValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Running, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    s_step = s_castCount = 0;
                    s_game = null;
                    s_next = EditorApplication.timeSinceStartup + 2;
                    s_deadline = EditorApplication.timeSinceStartup + 90;
                    EditorApplication.update += Tick;
                }
                else if (state == PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.update -= Tick;
                    SessionState.SetBool(Running, false);
                    s_game = null; s_view = null; s_effect = null; s_overlay = null;
                }
            };
        }

        [MenuItem("Tools/YasudaHaruto/Verify Rune Spell In Current Scene")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory("Temp/RuneSpellValidation");
            File.WriteAllText(Report, "Current scene; input handlers exercised with synthetic pointer events.\n");
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }

        private static object Get(object target, string name) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            File.AppendAllText(Report, "PASS: " + message + "\n");
        }
        private static void Capture(string name) => ScreenCapture.CaptureScreenshot("Temp/RuneSpellValidation/" + name + ".png");
        private static void OnCast(MAGIC_CAST_DIRECTION direction) { s_castCount++; s_lastDirection = direction; }

        private static void CheckPalette()
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var colors = new Color[64];
            for (int i = 0; i < colors.Length; i++) colors[i] = i < 32 ? Color.white : new Color(0.05f, 0.4f, 1f, 1f);
            texture.SetPixels(colors);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), Vector2.one * 0.5f);
            var go = new GameObject("Rune Palette Test", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                var image = go.GetComponent<Image>(); image.sprite = sprite;
                Color color = RuneSpellPalette.Sample(image, Color.magenta);
                Require(color.b > 0.8f && color.r < 0.2f && color.g > 0.2f,
                    "Color is sampled from non-readable rune pixels while ignoring white paper");
            }
            finally { UnityEngine.Object.Destroy(go); UnityEngine.Object.Destroy(sprite); UnityEngine.Object.Destroy(texture); }
        }

        private static void Complete(MAGIC_AIM_TYPE type = MAGIC_AIM_TYPE.THREE_DIRECTIONS)
        {
            s_controller.BeginTrace(s_rune, type);
            foreach (var stroke in s_rune.TraceData.Strokes)
            {
                s_controller.BeginStroke(stroke.Points[0]);
                for (int i = 1; i < stroke.Points.Count; i++) s_controller.AddPoint(stroke.Points[i]);
                if (s_controller.State == RUNE_TRACE_STATE.WAITING_RELEASE) s_controller.EndStroke();
            }
            if (type == MAGIC_AIM_TYPE.NONE) return;
            Require(s_controller.State == RUNE_TRACE_STATE.WAITING_CAST_RELEASE, "Completed trace waits for original finger release");
            s_effect = s_view.SpellEffect;
            Require(s_effect != null && s_effect.IsVisible && !s_effect.IsReady, "Completed player strokes become gathering particles");
            s_effect.enabled = false; // Deterministic phase advancement, actual uGUI rendering remains active.
        }

        private static PointerEventData Pointer(Vector2 point) => new PointerEventData(EventSystem.current)
        { pointerId = 17, button = PointerEventData.InputButton.Left, position = point };

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < s_next) return;
            try
            {
                if (EditorApplication.timeSinceStartup > s_deadline) throw new TimeoutException("Rune spell validation timed out.");
                if (s_game != null && !s_game.BookView.IsReady) return;
                s_next = EditorApplication.timeSinceStartup + 0.35;
                switch (s_step++)
                {
                    case 0:
                        CheckPalette();
                        s_game = UnityEngine.Object.FindFirstObjectByType<BookGameController>();
                        Require(s_game != null && s_game.IsInitialized, "Book initialized");
                        s_spread = -1;
                        for (int i = 0; i < s_game.Pages.Count; i++)
                        {
                            var page = s_game.Pages[i].Data;
                            if (page.Interaction != null && page.Interaction.RuneData != null && page.Action != null &&
                                page.Action.MagicData != null && page.Action.MagicData.AimType == MAGIC_AIM_TYPE.THREE_DIRECTIONS)
                            { s_spread = i + 1; s_rune = page.Interaction.RuneData; break; }
                        }
                        Require(s_spread > 0, "Found a directional rune in the current inventory");
                        var bootstrap = UnityEngine.Object.FindFirstObjectByType<BookGameBootstrap>();
                        s_controller = (RuneTraceController)Get(bootstrap, "m_runeTraceController");
                        s_view = (RunePageView)Get(bootstrap, "m_runeTraceView");
                        var input = (BookGameDragInput)Get(bootstrap, "m_gameDragInput");
                        s_settings = (RuneSpellSettings)Get(input, "m_spellSettings");
                        UnityEngine.Object.FindFirstObjectByType<BookObstacleController>().SetConnected(true);
                        s_controller.CastConfirmed += OnCast;
                        Require(s_game.BookView.OpenBook(s_spread), "Open directional spread");
                        break;
                    case 1:
                        Require(Resources.Load<Sprite>("RuneSpell/MagicAimArrow") != null, "Generated arrow imported as a Sprite");
                        Complete();
                        Require(s_effect.GetComponentsInChildren<ParticleSystem>().Length == 4, "Four independent ParticleSystems compose spell");
                        var player = (RuneLineGraphic)Get(s_view, "m_playerGraphic");
                        using (var mesh = new VertexHelper())
                        {
                            typeof(RuneLineGraphic).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                .Invoke(player, new object[] { mesh });
                            Require(mesh.currentVertCount == 0, "Player line disappears when it dissolves");
                        }
                        Require(s_game.GetCurrentRuneImage() != null, "Printed colored rune remains intact");
                        Capture("01-dissolve");
                        break;
                    case 2:
                        s_controller.EndStroke();
                        Require(!s_controller.TryBeginCast(), "Cannot aim before particles gather");
                        s_effect.Advance(s_settings.FloatDuration * 0.8f);
                        var trace = s_effect.transform.Find("Dissolving Rune").GetComponent<ParticleSystem>();
                        Require(trace.particleCount == Mathf.Clamp(s_settings.TraceParticleCount, 32, 512), "Player stroke samples populate ParticleSystem");
                        Capture("02-float");
                        break;
                    case 3:
                        s_effect.Advance(s_settings.FloatDuration + s_settings.GatherDuration);
                        Require(s_effect.IsReady && s_view.IsCastReady, "Gather finishes and unlocks aiming");
                        Require(s_effect.transform.Find("Spell Core").GetComponent<ParticleSystem>().particleCount == 12 &&
                            s_effect.transform.Find("Orbiting Motes").GetComponent<ParticleSystem>().particleCount > 0,
                            "Core and surrounding motes remain active while waiting");
                        s_overlay = UnityEngine.Object.FindFirstObjectByType<RuneCastOverlay>();
                        Require(s_overlay != null, "Spotlight and input overlay active");
                        foreach (var image in s_overlay.GetComponentsInChildren<Image>(true))
                            Require(!image.gameObject.activeSelf && !image.raycastTarget, "Arrows hidden before dragging and never intercept input");
                        s_home = s_effect.Center;
                        Capture("03-ready");
                        break;
                    case 4:
                        var core = s_effect.transform.Find("Spell Core").GetComponent<ParticleSystem>();
                        Require(core.particleCount == 12, "Core particles survive the next player-loop frame");
                        var graphic = core.GetComponent<RuneParticleGraphic>();
                        using (var mesh = new VertexHelper())
                        {
                            typeof(RuneParticleGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                                .Invoke(graphic, new object[] { mesh });
                            Require(mesh.currentVertCount == 48, "Core produces visible UI particle quads");
                            File.AppendAllText(Report, "Core graphic: " + graphic.rectTransform.rect + ", rotation=" + graphic.transform.localEulerAngles +
                                ", scale=" + graphic.transform.lossyScale + ", culled=" + graphic.canvasRenderer.cull + "\n");
                            var buffer = new ParticleSystem.Particle[12]; core.GetParticles(buffer);
                            File.AppendAllText(Report, "Core particle: position=" + buffer[0].position + ", size=" + buffer[0].startSize +
                                ", color=" + buffer[0].startColor + ", active=" + graphic.isActiveAndEnabled + ", canvas=" + graphic.canvas + "\n");
                        }
                        s_pointer = s_overlay.Center + Vector2.up * Screen.height * 0.35f;
                        s_overlay.OnPointerDown(Pointer(s_overlay.Center));
                        s_overlay.OnDrag(Pointer(s_pointer));
                        s_effect.Advance(0f);
                        Require(s_controller.State == RUNE_TRACE_STATE.CHARGING && s_effect.Center.y > s_home.y,
                            "Touch spell and drag moves the core toward the pointer");
                        Require(s_overlay.GetComponentsInChildren<Image>().Length == 3, "Only image sprites render the three direction arrows");
                        var hits = new List<RaycastResult>();
                        EventSystem.current.RaycastAll(Pointer(s_overlay.Center), hits);
                        Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<RuneCastOverlay>() == s_overlay,
                            "Spotlight input surface receives real UI raycasts");
                        Capture("04-aim");
                        break;
                    case 5:
                        s_overlay.OnPointerUp(Pointer(s_pointer));
                        Require(s_effect.IsFlying && s_controller.State == RUNE_TRACE_STATE.COOLDOWN && s_castCount == 1 &&
                            s_lastDirection == MAGIC_CAST_DIRECTION.CENTER, "Release launches once and starts cooldown immediately");
                        float remaining = s_controller.CooldownRemaining;
                        s_effect.Advance(s_settings.FlightDuration * 0.18f);
                        Require(s_effect.transform.Find("Spell Trail").GetComponent<ParticleSystem>().particleCount > 0 &&
                            Mathf.Approximately(s_controller.CooldownRemaining, remaining), "Visual flight has its own tail and never ticks cooldown");
                        Capture("05-flight");
                        break;
                    case 6:
                        s_effect.Advance(s_settings.FlightDuration);
                        Require(!s_effect.IsVisible, "Flight clears its particles at the end");
                        CheckSide(MAGIC_CAST_DIRECTION.LEFT);
                        CheckSide(MAGIC_CAST_DIRECTION.RIGHT);
                        Complete();
                        s_controller.EndStroke();
                        s_effect.Advance(s_settings.FloatDuration + s_settings.GatherDuration + 0.01f);
                        s_overlay.gameObject.SetActive(true);
                        s_overlay.OnPointerDown(Pointer(s_overlay.Center));
                        s_overlay.OnDrag(Pointer(s_overlay.Center + Vector2.up * Screen.height * 0.3f));
                        typeof(RuneCastOverlay).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(s_overlay, new object[] { false });
                        Require(s_controller.State == RUNE_TRACE_STATE.WAITING_CAST_START && s_effect.IsReady,
                            "Lost focus cancels the held gesture without firing");
                        s_controller.SetInputAvailable(false);
                        Require(!s_effect.IsVisible && s_controller.State == RUNE_TRACE_STATE.WAITING_STROKE,
                            "Connection or fourth-slot input block clears pending spell");
                        s_controller.SetInputAvailable(true);
                        Complete(MAGIC_AIM_TYPE.NONE);
                        Require(s_controller.State == RUNE_TRACE_STATE.COOLDOWN && !s_effect.IsVisible && s_lastDirection == MAGIC_CAST_DIRECTION.NONE,
                            "Spells without aiming keep immediate cast behavior");
                        File.AppendAllText(Report, "RuneSpell COMPLETE\n");
                        Finish();
                        break;
                }
            }
            catch (Exception exception)
            {
                File.AppendAllText(Report, "FAIL: " + exception + "\n");
                Debug.LogException(exception);
                Finish();
            }
        }

        private static void CheckSide(MAGIC_CAST_DIRECTION direction)
        {
            Complete();
            s_controller.EndStroke();
            s_effect.Advance(s_settings.FloatDuration + s_settings.GatherDuration + 0.01f);
            s_overlay.gameObject.SetActive(true);
            float side = direction == MAGIC_CAST_DIRECTION.LEFT ? -1f : 1f;
            float angle = 45f * Mathf.Deg2Rad;
            Vector2 end = s_overlay.Center + new Vector2(side * Mathf.Sin(angle), Mathf.Cos(angle)) * Screen.height * 0.4f;
            int previous = s_castCount;
            s_overlay.OnPointerDown(Pointer(s_overlay.Center));
            s_overlay.OnDrag(Pointer(end));
            s_effect.Advance(0f);
            Vector2 start = s_effect.Center;
            s_overlay.OnPointerUp(Pointer(end));
            s_effect.Advance(s_settings.FlightDuration * 0.1f);
            Require(s_lastDirection == direction && s_castCount == previous + 1 && s_effect.IsFlying &&
                Mathf.Sign(s_effect.Center.x - start.x) == side, direction + " gesture fires exactly once in the selected direction");
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            if (s_controller != null) s_controller.CastConfirmed -= OnCast;
            EditorApplication.ExitPlaymode();
        }
    }
}
