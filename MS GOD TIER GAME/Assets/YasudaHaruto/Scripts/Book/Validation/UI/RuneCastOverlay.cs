using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YasudaHaruto.BookValidation
{
    // Captures all pointer input while aiming; only a press inside the circle may start a cast.
    public sealed class RuneCastOverlay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float ReleaseFadeDuration = 0.18f;
        private RuneTraceController m_controller;
        private CastSwipeDetector m_detector;
        private Func<Rect?> m_bookRect;
        private RuneCastGuideGraphic m_graphic;
        private Vector2 m_origin;
        private float m_radiusRatio, m_shade;
        private Vector2 m_center, m_start;
        private float m_radius, m_resetRadius;
        private bool m_aimReachedOutsideGlow, m_aimReturnedToGlow;
        private bool m_pressed;
        private int m_pointerId;
        private RunePageView m_spellView;
        private RuneSpellSettings m_settings;
        private readonly Image[] m_arrows = new Image[3];
        private readonly Vector2[] m_arrowAxes = new Vector2[3];
        private Sprite[] m_frames;
        private RawImage m_glow, m_prompt;
        private Material m_glowMaterial, m_shadeMaterial;
        private RectTransform m_bookDisplay;
        private Func<Texture> m_bookTexture;
        private float m_arrowStart;
        private bool m_arrowsVisible;

        private float m_releaseFadeStart;

        public bool IsFadingOut { get; private set; }
        public Vector2 Center => m_center;
        public float Radius => m_radius;

        public void Configure(RuneTraceController controller, CastSwipeDetector detector, Func<Rect?> bookRect,
            Vector2 origin, float radiusRatio, float shade, float pulseSpeed, float pulseAmount, float arrowWidth = 0.1f,
            RunePageView spellView = null, RuneSpellSettings settings = null, RectTransform bookDisplay = null,
            Func<Texture> bookTexture = null)
        {
            m_controller = controller;
            m_detector = detector;
            m_bookRect = bookRect;
            m_origin = origin;
            m_radiusRatio = radiusRatio;
            m_shade = shade;
            m_bookDisplay = bookDisplay;
            m_bookTexture = bookTexture;
            m_spellView = spellView;
            m_settings = settings ?? new RuneSpellSettings();
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            gameObject.AddComponent<GraphicRaycaster>();
            m_graphic = new GameObject("Spotlight and Direction Guides", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RuneCastGuideGraphic)).GetComponent<RuneCastGuideGraphic>();
            m_graphic.transform.SetParent(transform, false);
            m_graphic.rectTransform.anchorMin = Vector2.zero;
            m_graphic.rectTransform.anchorMax = Vector2.one;
            m_graphic.rectTransform.offsetMin = m_graphic.rectTransform.offsetMax = Vector2.zero;
            m_graphic.raycastTarget = true;
            m_graphic.canvasRenderer.cullTransparentMesh = false;
            m_glow = CreateArtwork("Zyumon_Z");
            m_prompt = CreateArtwork("Text_Hanatsu");
            Shader shader = Resources.Load<Shader>("RuneSpell/RuneCastComposite");
            if (shader != null)
            {
                m_shadeMaterial = new Material(shader);
                m_shadeMaterial.EnableKeyword("RUNE_VIGNETTE");
                m_graphic.material = m_shadeMaterial;
                m_glowMaterial = new Material(shader);

                m_glowMaterial.SetFloat("_Premultiply", 1f);
                m_glow.material = m_glowMaterial;
            }
            if (m_settings.ArrowSprite == null)
            {
                m_frames = Resources.LoadAll<Sprite>("RuneSpell/DirectionFrames");
                Array.Sort(m_frames, (a, b) => StringComparer.Ordinal.Compare(a.name, b.name));
            }
            Sprite arrow = m_settings.ArrowSprite != null ? m_settings.ArrowSprite : Resources.Load<Sprite>("RuneSpell/MagicAimArrow");
            for (int i = 0; i < m_arrows.Length; i++)
            {
                var go = new GameObject("Magic Direction " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(m_graphic.transform, false);
                m_arrows[i] = go.GetComponent<Image>();
                m_arrows[i].sprite = arrow;
                m_arrows[i].preserveAspect = true;
                m_arrows[i].raycastTarget = false;
                go.SetActive(false);
            }
            Canvas.ForceUpdateCanvases();
            RefreshGeometry();
        }

        private RawImage CreateArtwork(string name)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
            image.transform.SetParent(m_graphic.transform, false);
            image.texture = Resources.Load<Texture2D>("RuneSpell/" + name);
            image.raycastTarget = false;
            image.enabled = image.texture != null;
            return image;
        }

        private static void PlaceArtwork(RawImage image, Vector2 point, float height, float opacity)
        {
            if (image.texture == null) return;
            float aspect = image.texture.width / (float)image.texture.height;
            image.rectTransform.anchoredPosition = point;
            image.rectTransform.sizeDelta = new Vector2(height * aspect, height);
            image.color = new Color(1f, 1f, 1f, opacity);
        }

        private bool RefreshGeometry(float visibility = 1f)
        {
            Rect? book = m_bookRect?.Invoke();
            if (!book.HasValue || book.Value.height <= 0f) return false;
            Rect rect = book.Value;
            m_center = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, Mathf.Clamp01(m_origin.x)),
                Mathf.Lerp(rect.yMin, rect.yMax, Mathf.Clamp01(m_origin.y)));
            m_radius = rect.height * Mathf.Clamp(m_radiusRatio, 0.03f, 0.2f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_graphic.rectTransform, m_center, null, out Vector2 local);
            var effect = m_spellView != null ? m_spellView.SpellEffect : null;
            float progress = effect != null && effect.IsVisible ? effect.GatherProgress : 1f;
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.12f) / 0.65f));
            UpdateShade(rect, fade * visibility);
            float glowHeight = rect.height * RuneSpellSettings.GlowHeightRatio;
            m_resetRadius = glowHeight * 0.40f;
            PlaceArtwork(m_glow, local, glowHeight, fade * visibility);
            UpdateGlowComposite(glowHeight);
            float promptFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.72f) / 0.28f));
            PlaceArtwork(m_prompt, local - Vector2.up * rect.height * 0.275f, rect.height * 0.10f, promptFade * visibility);
            RefreshArrows(rect.size);
            return true;
        }

        private void UpdateShade(Rect book, float fade)
        {
            // Stack the configured shade twice to match the darker paper in the reference.
            float transmission = 1f - m_shade;
            m_graphic.SetShade((1f - transmission * transmission) * fade);
            if (m_shadeMaterial == null) return;

            RectTransform graphic = m_graphic.rectTransform;
            Rect bounds = graphic.rect;
            if (bounds.width <= 0f || bounds.height <= 0f) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(graphic, book.center, null, out Vector2 center);
            m_shadeMaterial.SetVector("_Vignette", new Vector4(
                (center.x - bounds.xMin) / bounds.width, (center.y - bounds.yMin) / bounds.height,
                book.width * 0.53f / bounds.width, book.height * 0.60f / bounds.height));
        }

        private void UpdateGlowComposite(float height)
        {
            if (m_glowMaterial == null || m_glow.texture == null) return;
            Texture book = m_bookTexture?.Invoke();
            if (m_bookDisplay == null || book == null) return;

            Canvas canvas = m_bookDisplay.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 halfSize = new Vector2(height * m_glow.texture.width / m_glow.texture.height, height) * 0.5f;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_bookDisplay, m_center - halfSize, camera, out Vector2 lower) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(m_bookDisplay, m_center + halfSize, camera, out Vector2 upper)) return;
            Rect display = m_bookDisplay.rect;
            if (display.width <= 0f || display.height <= 0f) return;

            Vector2 origin = new Vector2((lower.x - display.xMin) / display.width, (lower.y - display.yMin) / display.height);
            Vector2 size = new Vector2((upper.x - lower.x) / display.width, (upper.y - lower.y) / display.height);
            m_glowMaterial.EnableKeyword("RUNE_BOOK_OVERLAY");
            m_glowMaterial.SetTexture("_BookTex", book);
            m_glowMaterial.SetVector("_BookUV", new Vector4(origin.x, origin.y, size.x, size.y));
        }

        private void RefreshArrows(Vector2 bookSize)
        {
            float bookHeight = bookSize.y;
            bool ready = m_controller != null && m_spellView != null && m_spellView.IsCastReady &&
                (m_controller.State == RUNE_TRACE_STATE.WAITING_CAST_START || m_controller.State == RUNE_TRACE_STATE.CHARGING);
            bool show = ready || m_pressed;
            if (show && !m_arrowsVisible) m_arrowStart = Time.unscaledTime;
            m_arrowsVisible = show;
            Sprite frame = null;
            if (show && m_frames != null && m_frames.Length > 0)
            {
                int index = Mathf.FloorToInt((Time.unscaledTime - m_arrowStart) * 24f) % m_frames.Length;
                frame = m_frames[index];
            }
            float length = bookHeight * m_settings.ArrowLength * 1.8f;
            float sideAngle = m_detector != null ? m_detector.GuideSideAngle : 45f;
            for (int i = 0; i < m_arrows.Length; i++)
            {
                var arrow = m_arrows[i];
                if (arrow == null) continue;
                if (frame != null) arrow.sprite = frame;
                bool visible = show && arrow.sprite != null &&
                    (m_controller.UsesThreeDirections || i == (int)MAGIC_CAST_DIRECTION.CENTER);
                arrow.gameObject.SetActive(visible);
                float angle = (i - 1) * sideAngle;
                Vector2 direction = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));
                // Keep the tail clear even on the frame with the longest dotted arrow.
                bool centerArrow = i == (int)MAGIC_CAST_DIRECTION.CENTER;
                float distance = bookHeight * (centerArrow ? 0.385f : 0.455f);
                Vector2 point = m_center + direction * distance;
                if (!centerArrow)
                    point += new Vector2((i - 1) * bookSize.x * 0.042f, -bookHeight * 0.045f);
                Vector2 axis = (point - m_center).normalized;
                m_arrowAxes[i] = axis;
                if (!visible) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(m_graphic.rectTransform, point, null, out Vector2 local);
                arrow.rectTransform.anchoredPosition = local;
                arrow.rectTransform.sizeDelta = Vector2.one * length;
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(axis.x, axis.y) * Mathf.Rad2Deg);
                Color tint = m_graphic.SelectedDirection == i
                    ? new Color(1f, 0.9f, 0.28f)
                    : new Color(1f, 1f, 1f, Mathf.Lerp(0.45f, 1f, m_settings.ArrowIdleAlpha));
                arrow.color = tint;
            }
        }

        private void LateUpdate()
        {
            if (IsFadingOut)
            {
                float elapsed = Time.unscaledTime - m_releaseFadeStart;
                float visibility = 1f - Mathf.SmoothStep(0f, 1f, elapsed / ReleaseFadeDuration);
                if (visibility <= 0f || !RefreshGeometry(visibility)) Hide();
                return;
            }
            if (m_controller == null || (m_controller.State != RUNE_TRACE_STATE.WAITING_CAST_RELEASE &&
                m_controller.State != RUNE_TRACE_STATE.WAITING_CAST_START &&
                m_controller.State != RUNE_TRACE_STATE.CHARGING)) { Hide(); return; }
            if (!RefreshGeometry())
            {
                m_controller.CancelCastGesture();
                m_pressed = false;
                m_spellView?.CancelSpellDrag();
                m_graphic.gameObject.SetActive(false);
            }
            else m_graphic.gameObject.SetActive(true);
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (m_pressed || data.button != PointerEventData.InputButton.Left || !RefreshGeometry() ||
                Vector2.Distance(data.position, m_center) > m_radius || m_detector == null ||
                m_controller == null || !m_controller.TryBeginCast()) return;
            IsFadingOut = false;
            m_graphic.raycastTarget = true;
            m_pressed = true;
            m_pointerId = data.pointerId;
            m_start = data.position;
            m_aimReachedOutsideGlow = false;
            m_aimReturnedToGlow = false;
            m_spellView?.CancelSpellDrag();
            m_graphic.SelectedDirection = -1;
            RefreshGeometry();
        }

        public void OnBeginDrag(PointerEventData data) { }

        public void OnDrag(PointerEventData data)
        {
            if (!m_pressed || data.pointerId != m_pointerId) return;
            m_spellView?.SetSpellDrag(GetAimOffset(data.position));
        }

        private Vector2 GetAimOffset(Vector2 pointer)
        {
            Vector2 delta = pointer - m_start;
            // Release a locked arrow when the finger comes back inside the visible glow.
            float distanceSquared = (pointer - m_center).sqrMagnitude;
            bool insideGlow = distanceSquared <= m_resetRadius * m_resetRadius;
            m_aimReturnedToGlow = insideGlow && (m_aimReachedOutsideGlow || m_aimReturnedToGlow);
            m_aimReachedOutsideGlow = !insideGlow;
            if (m_aimReturnedToGlow || distanceSquared <= m_radius * m_radius * 0.25f)
            {
                m_graphic.SelectedDirection = -1;
                return Vector2.zero;
            }

            if (m_graphic.SelectedDirection < 0 &&
                m_detector.TryPreviewDirection(m_start, pointer, out MAGIC_CAST_DIRECTION direction) &&
                (m_controller.UsesThreeDirections || direction == MAGIC_CAST_DIRECTION.CENTER))
                m_graphic.SelectedDirection = (int)direction;
            if (m_graphic.SelectedDirection < 0) return Vector2.zero;

            Vector2 axis = m_arrowAxes[m_graphic.SelectedDirection];
            return axis * Mathf.Max(0f, Vector2.Dot(delta, axis));
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (!m_pressed || data.pointerId != m_pointerId) return;
            m_pressed = false;
            Vector2 offset = GetAimOffset(data.position);
            if (m_graphic.SelectedDirection >= 0 &&
                m_detector.TryEvaluate(m_start, m_start + offset, out _))
            {
                m_spellView?.SetSpellDrag(offset);
                m_controller.ConfirmCast((MAGIC_CAST_DIRECTION)m_graphic.SelectedDirection);
                m_graphic.raycastTarget = false;
                m_releaseFadeStart = Time.unscaledTime;
                IsFadingOut = true;
                return;
            }
            else m_controller.EndStroke(); // Preserve the existing invalid-swipe failure rules.
            Hide();
        }

        public void OnEndDrag(PointerEventData data) => OnPointerUp(data);

        private void OnApplicationFocus(bool focus)
        {
            if (focus) return;
            m_pressed = false;
            m_controller?.CancelCastGesture();
            m_spellView?.CancelSpellDrag();
            if (m_graphic != null) m_graphic.SelectedDirection = -1;
        }

        private void OnDestroy()
        {
            if (m_glowMaterial != null) Destroy(m_glowMaterial);
            if (m_shadeMaterial != null) Destroy(m_shadeMaterial);
        }

        public void Hide()
        {
            IsFadingOut = false;
            m_pressed = false;
            m_spellView?.CancelSpellDrag();
            if (m_graphic != null)
            {
                m_graphic.SelectedDirection = -1;
                m_graphic.raycastTarget = true;
            }
            m_arrowsVisible = false;
            foreach (var arrow in m_arrows) if (arrow != null) arrow.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }
    }
}
