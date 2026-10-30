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
        private RuneTraceController m_controller;
        private CastSwipeDetector m_detector;
        private Func<Rect?> m_bookRect;
        private RuneCastGuideGraphic m_graphic;
        private Vector2 m_origin;
        private float m_radiusRatio, m_shade, m_pulseSpeed, m_pulseAmount;
        private Vector2 m_center, m_start;
        private float m_radius;
        private bool m_pressed;
        private int m_pointerId;
        private RunePageView m_spellView;
        private RuneSpellSettings m_settings;
        private readonly Image[] m_arrows = new Image[3];

        public Vector2 Center => m_center;
        public float Radius => m_radius;

        public void Configure(RuneTraceController controller, CastSwipeDetector detector, Func<Rect?> bookRect,
            Vector2 origin, float radiusRatio, float shade, float pulseSpeed, float pulseAmount, float arrowWidth = 0.1f,
            RunePageView spellView = null, RuneSpellSettings settings = null)
        {
            m_controller = controller;
            m_detector = detector;
            m_bookRect = bookRect;
            m_origin = origin;
            m_radiusRatio = radiusRatio;
            m_shade = shade;
            m_pulseSpeed = pulseSpeed;
            m_pulseAmount = pulseAmount;
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
            m_graphic.ArrowWidth = Mathf.Clamp(arrowWidth, 0.02f, 0.2f);
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

        private bool RefreshGeometry()
        {
            Rect? book = m_bookRect?.Invoke();
            if (!book.HasValue || book.Value.height <= 0f) return false;
            Rect rect = book.Value;
            m_center = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, Mathf.Clamp01(m_origin.x)),
                Mathf.Lerp(rect.yMin, rect.yMax, Mathf.Clamp01(m_origin.y)));
            m_radius = rect.height * Mathf.Clamp(m_radiusRatio, 0.03f, 0.2f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(m_graphic.rectTransform, m_center, null, out Vector2 local);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * m_pulseSpeed) * m_pulseAmount;
            m_graphic.SetGuide(local, m_radius * pulse, rect.height * 0.24f, m_shade,
                m_detector != null ? m_detector.GuideSideAngle : 45f);
            RefreshArrows(rect.height);
            return true;
        }

        private void RefreshArrows(float bookHeight)
        {
            float length = bookHeight * m_settings.ArrowLength;
            float sideAngle = m_detector != null ? m_detector.GuideSideAngle : 45f;
            for (int i = 0; i < m_arrows.Length; i++)
            {
                var arrow = m_arrows[i];
                if (arrow == null) continue;
                bool visible = m_pressed && arrow.sprite != null;
                arrow.gameObject.SetActive(visible);
                if (!visible) continue;
                float angle = (i - 1) * sideAngle;
                Vector2 direction = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));
                Vector2 point = m_center + direction * (m_radius * 1.25f + length * 0.5f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(m_graphic.rectTransform, point, null, out Vector2 local);
                arrow.rectTransform.anchoredPosition = local;
                arrow.rectTransform.sizeDelta = Vector2.one * length;
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
                Color tint = m_spellView != null && m_spellView.SpellEffect != null
                    ? Color.Lerp(m_spellView.SpellEffect.SpellColor, Color.white, 0.6f) : Color.white;
                tint.a = m_graphic.SelectedDirection == i ? 1f : m_settings.ArrowIdleAlpha;
                arrow.color = tint;
            }
        }

        private void LateUpdate()
        {
            if (m_controller == null || (m_controller.State != RUNE_TRACE_STATE.WAITING_CAST_RELEASE &&
                m_controller.State != RUNE_TRACE_STATE.WAITING_CAST_START &&
                m_controller.State != RUNE_TRACE_STATE.CHARGING)) { Hide(); return; }
            if (!RefreshGeometry())
            {
                m_controller.CancelCastGesture();
                m_pressed = false;
                m_spellView?.SetSpellDrag(Vector2.zero, m_radius);
                m_graphic.enabled = false;
            }
            else m_graphic.enabled = true;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (m_pressed || data.button != PointerEventData.InputButton.Left || !RefreshGeometry() ||
                Vector2.Distance(data.position, m_center) > m_radius || m_detector == null ||
                m_controller == null || !m_controller.TryBeginCast()) return;
            m_pressed = true;
            m_pointerId = data.pointerId;
            m_start = data.position;
            m_spellView?.SetSpellDrag(Vector2.zero, m_radius);
            m_graphic.SelectedDirection = -1;
            RefreshGeometry();
        }

        public void OnBeginDrag(PointerEventData data) { }

        public void OnDrag(PointerEventData data)
        {
            if (!m_pressed || data.pointerId != m_pointerId) return;
            m_spellView?.SetSpellDrag(data.position - m_start, m_radius);
            m_graphic.SelectedDirection = m_detector.TryEvaluate(m_start, data.position, out MAGIC_CAST_DIRECTION direction)
                ? (int)direction : -1;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (!m_pressed || data.pointerId != m_pointerId) return;
            m_pressed = false;
            if (m_detector.TryEvaluate(m_start, data.position, out MAGIC_CAST_DIRECTION direction))
            {
                m_spellView?.SetSpellDrag(data.position - m_start, m_radius);
                m_controller.ConfirmCast(direction);
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
            m_spellView?.SetSpellDrag(Vector2.zero, m_radius);
            if (m_graphic != null) m_graphic.SelectedDirection = -1;
        }

        public void Hide()
        {
            m_pressed = false;
            m_spellView?.SetSpellDrag(Vector2.zero, m_radius);
            if (m_graphic != null) m_graphic.SelectedDirection = -1;
            foreach (var arrow in m_arrows) if (arrow != null) arrow.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }
    }
}
