using System.Collections.Generic;
using System.Reflection;
using hatake;
using UnityEngine;
using UnityEngine.UI;

namespace YasudaHaruto.BookValidation
{
    internal sealed class BookPageTabs : MonoBehaviour
    {
        private static readonly Color s_spellPaper = new Color32(245, 216, 159, 255);
        private static readonly Color s_guidePaper = new Color32(166, 217, 205, 255);
        private static readonly Color s_currentPaper = new Color32(255, 250, 223, 255);
        // The shared renderer exposes turn progress, but keeps the fold geometry private.
        // Read the same fold state its shader uses so manual drags and automatic turns agree.
        private const BindingFlags FoldFields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo s_foldPoint = typeof(BookFlipView).GetField("m_foldPoint", FoldFields);
        private static readonly FieldInfo s_corner = typeof(BookFlipView).GetField("m_corner", FoldFields);
        private static readonly FieldInfo s_turnSide = typeof(BookFlipView).GetField("m_turnSide", FoldFields);
        private static readonly FieldInfo s_destinationCover = typeof(BookFlipView).GetField("m_destinationCover", FoldFields);
        private readonly List<Button> m_tabs = new List<Button>();
        private readonly List<Graphic> m_icons = new List<Graphic>();
        private BookGameController m_book;
        private BookGameDragInput m_input;
        private IReadOnlyList<PageInstance> m_pages;
        private RectTransform m_strip;
        private RectTransform m_coverClip;

        public void Initialize(BookGameController book, BookGameDragInput input)
        {
            m_book = book;
            m_input = input;
            var canvas = new GameObject("Book Page Tabs", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 65;
            m_coverClip = new GameObject("Cover Clip", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            m_coverClip.SetParent(canvas.transform, false);
            m_coverClip.anchorMin = Vector2.zero;
            m_coverClip.anchorMax = Vector2.one;
            m_coverClip.offsetMax = Vector2.zero;
            m_strip = new GameObject("Tabs", typeof(RectTransform)).GetComponent<RectTransform>();
            m_strip.SetParent(m_coverClip, false);
            m_strip.anchorMin = m_strip.anchorMax = m_strip.pivot = Vector2.zero;
        }

        private void LateUpdate()
        {
            if (m_strip == null)
                return;
            Rect? bookRect = m_input.GetBookScreenRect();
            var pages = m_book.Pages;
            bool visible = m_book.IsInitialized && pages != null && pages.Count > 0 && bookRect.HasValue &&
                m_book.BookView.DisplayImage.isActiveAndEnabled && bookRect.Value.width > 0f;
            m_strip.gameObject.SetActive(visible);
            if (!visible)
                return;

            if (!ReferenceEquals(pages, m_pages))
                Rebuild(pages);

            Rect rect = bookRect.Value;
            float left = Mathf.Clamp(rect.xMin, 6f, Screen.width - 6f);
            float right = Mathf.Clamp(rect.xMax, left, Screen.width - 6f);
            float spine = Mathf.Clamp(rect.center.x, left, right);
            float size = Mathf.Clamp(Screen.height * 0.062f, 32f, 76f);
            float spineGap = Mathf.Min(size * 0.15f, (right - left) * 0.05f);
            float height = size;
            float bottom = rect.yMax - size * 0.16f;
            var view = m_book.BookView;
            var flip = view.FlipView;
            // The current spread's left page belongs to the stack of pages already turned.
            int leftCount = view.IsClosed
                ? (view.CoverState == BookCoverState.BackCover ? m_tabs.Count : 0)
                : Mathf.Clamp(view.CurrentSpread, 0, m_tabs.Count);
            // A page keeps the same distance from the spine on either side.
            float width = Mathf.Min(size, Mathf.Max(0f, Mathf.Min(spine - left, right - spine) - spineGap) / m_tabs.Count);
            float coverAmount = view.IsClosed ? 1f : 0f;
            var nextCover = s_destinationCover?.GetValue(flip) is BookCoverState cover ? cover : flip.CoverState;
            if (flip.IsTurning && flip.CoverState != nextCover)
                coverAmount = flip.CoverState == BookCoverState.Open ? flip.TurnAmount : 1f - flip.TurnAmount;
            float coverEdge = rect.yMax + rect.width * 0.5f * 0.06f;
            float clipBottom = coverAmount > 0f
                ? Mathf.Clamp(Mathf.Lerp(bottom, Mathf.Min(coverEdge, bottom + height - 5f), coverAmount), 0f, Screen.height)
                : 0f;
            m_coverClip.offsetMin = new Vector2(0f, clipBottom);
            m_strip.anchoredPosition = new Vector2(spine, bottom - clipBottom);
            bool canSelect = m_book.CanSelectTab;

            int turningIndex = -1;
            float turnSide = 0f;
            Vector2 foldMiddle = default, foldNormal = default;
            if (flip.IsTurning && flip.CoverState == BookCoverState.Open && nextCover == BookCoverState.Open &&
                s_foldPoint?.GetValue(flip) is Vector2 fold && s_corner?.GetValue(flip) is BookFlipCorner corner &&
                s_turnSide?.GetValue(flip) is float side)
            {
                var origin = new Vector2(1f, corner == BookFlipCorner.Top ? flip.PageAspect : 0f);
                turnSide = side;
                foldMiddle = (origin + fold) * 0.5f;
                foldNormal = (origin - fold).normalized;
                turningIndex = side > 0f ? view.CurrentSpread : view.CurrentSpread - 1;
            }

            for (int i = 0; i < m_tabs.Count; i++)
            {
                var button = m_tabs[i];
                var tab = (RectTransform)button.transform;
                bool onLeft = i < leftCount;
                float distance = spineGap + (i + 0.5f) * width;
                float x = onLeft ? -distance : distance;
                float y = 0f;
                float angle = 0f;
                if (i == turningIndex && foldNormal != Vector2.zero)
                {
                    float pixelsPerPage = Mathf.Max(1f, rect.width * 0.5f);
                    var anchor = new Vector2(distance / pixelsPerPage, flip.PageAspect);
                    // The shader reflects the folded half about the crease. Use
                    // nearby points to orient this tab along the sheet's top edge.
                    Vector2 folded = FoldTabPoint(anchor, foldMiddle, foldNormal);
                    Vector2 tangent = FoldTabPoint(anchor + Vector2.right * 0.005f, foldMiddle, foldNormal) -
                        FoldTabPoint(anchor - Vector2.right * 0.005f, foldMiddle, foldNormal);
                    tangent.x *= turnSide;
                    if (tangent.x < 0f) tangent = -tangent;
                    angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
                    x = folded.x * turnSide * pixelsPerPage;
                    y = (folded.y - anchor.y) * pixelsPerPage;
                }
                tab.anchoredPosition = new Vector2(x, y);
                tab.localRotation = Quaternion.Euler(0f, 0f, angle);
                tab.sizeDelta = new Vector2(Mathf.Max(1f, width - 2f), height - 2f);
                // RuneLineGraphic uses a custom material, so hide the art
                // explicitly while the cover conceals the tab body.
                m_icons[i].gameObject.SetActive(coverAmount < 0.15f);
                bool current = !view.IsClosed && view.CurrentSpread == i + 1;
                button.interactable = canSelect && !current;
                button.image.color = current ? s_currentPaper :
                    m_pages[i].Data.PageType == PAGE_TYPE.MAGIC ? s_spellPaper : s_guidePaper;
            }
        }

        private static Vector2 FoldTabPoint(Vector2 point, Vector2 middle, Vector2 normal)
        {
            float distance = Vector2.Dot(point - middle, normal);
            return distance > 0f ? point - 2f * distance * normal : point;
        }

        private void Rebuild(IReadOnlyList<PageInstance> pages)
        {
            foreach (var button in m_tabs)
            {
                button.gameObject.SetActive(false);
                Destroy(button.gameObject);
            }
            m_tabs.Clear();
            m_icons.Clear();
            m_pages = pages;
            for (int i = 0; i < pages.Count; i++)
            {
                PageData page = pages[i].Data;
                int spread = i + 1;
                var tab = new GameObject(page.DisplayName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
                tab.transform.SetParent(m_strip, false);
                var rect = (RectTransform)tab.transform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0f);
                var button = tab.GetComponent<Button>();
                button.targetGraphic = tab.GetComponent<Image>();
                var colors = button.colors;
                colors.disabledColor = Color.white;
                colors.highlightedColor = new Color32(255, 255, 236, 255);
                colors.pressedColor = new Color32(211, 193, 161, 255);
                button.colors = colors;
                button.onClick.AddListener(() => m_book.NavigateToTab(spread));
                tab.GetComponent<Outline>().effectColor = new Color32(88, 60, 44, 235);
                m_tabs.Add(button);

                var rune = page.Interaction?.RuneData;
                Graphic icon;
                if (page.PageType == PAGE_TYPE.MAGIC && rune != null)
                {
                    var line = new GameObject("Rune", typeof(RectTransform), typeof(RuneLineGraphic)).GetComponent<RuneLineGraphic>();
                    line.SetStrokes(rune.TraceData.Strokes);
                    line.color = new Color32(89, 53, 43, 255);
                    line.LineWidth = 3f;
                    icon = line;
                }
                else
                {
                    var image = new GameObject("Page Illustration", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                    var illustrations = page.LeftVisualData.Images;
                    var sprite = illustrations != null && illustrations.Count > 0 ? illustrations[0]?.Sprite : null;
                    if (sprite != null)
                    {
                        // The guide illustrations occupy the center of the printed page art.
                        var source = sprite.rect;
                        var texture = sprite.texture;
                        image.texture = texture;
                        image.uvRect = new Rect((source.x + source.width * 0.15f) / texture.width,
                            (source.y + source.height * 0.26f) / texture.height,
                            source.width * 0.70f / texture.width, source.height * 0.47f / texture.height);
                    }
                    image.enabled = sprite != null;
                    icon = image;
                }
                icon.transform.SetParent(tab.transform, false);
                icon.raycastTarget = false;
                icon.rectTransform.anchorMin = new Vector2(0.12f, 0.1f);
                icon.rectTransform.anchorMax = new Vector2(0.88f, 0.9f);
                icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
                m_icons.Add(icon);
            }
        }
    }
}
