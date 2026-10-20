using UnityEngine;
using UnityEngine.EventSystems;

namespace hatake
{
    /// <summary>
    /// 本の表示（RawImage など）上のドラッグで、ページを指に追従させてめくる。
    /// 表示している RectTransform に付ける。BookSwipeInput とは同じ領域に併用しない。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BookFlipDragInput : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        public enum GrabSide { Both, RightOnly, LeftOnly }
        public enum GrabCorner { Auto, Bottom, Top }

        [SerializeField] private BookController m_bookController;
        [SerializeField] private BookFlipView m_flipView;

        [Header("利き手・持ち方")]
        [Tooltip("つかめるページ。片手持ちのUIでは、指の届く側だけにする")]
        [SerializeField] private GrabSide m_grabSide = GrabSide.Both;
        [Tooltip("Autoはつかんだ高さで上下の角を選ぶ")]
        [SerializeField] private GrabCorner m_grabCorner = GrabCorner.Auto;
        [Tooltip("最初・最後の見開きで外へめくると本を閉じる")]
        [SerializeField] private bool m_closeAtEnds = true;
        [Tooltip("ページの外側から何割の範囲でつかめるか。1でページ全体")]
        [SerializeField, Range(0.05f, 1f)] private float m_grabZone = 1f;

        private RectTransform m_rectTransform;
        private int m_pointerId;
        private bool m_dragFlag;

        public GrabSide AllowedSide { get => m_grabSide; set => m_grabSide = value; }
        public GrabCorner Corner { get => m_grabCorner; set => m_grabCorner = value; }

        private void Awake()
        {
            m_rectTransform = (RectTransform)transform;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (m_dragFlag || m_bookController == null || m_flipView == null ||
                eventData.button != PointerEventData.InputButton.Left ||
                m_bookController.IsBusy || m_flipView.IsTurning)
                return;
            if (!TryGetBookPoint(eventData.pressPosition, eventData.pressEventCamera, out Vector2 point)) return;

            if (!m_flipView.ContainsPage(point) || Mathf.Abs(point.x) < 1f - m_grabZone) return;

            bool rightPage = point.x >= 0f;
            if ((rightPage && m_grabSide == GrabSide.LeftOnly) || (!rightPage && m_grabSide == GrabSide.RightOnly))
                return;

            // 右をつかむと次へ、左は前へ。閉じた表紙は開き、端の見開きでは閉じる（Close At Ends）
            bool canTurn = m_closeAtEnds
                ? m_bookController.CanFlick(rightPage)
                : (rightPage ? m_bookController.CanTurnForward : m_bookController.CanTurnBackward);
            if (!canTurn) return;

            BookFlipCorner? corner = m_grabCorner switch
            {
                GrabCorner.Bottom => BookFlipCorner.Bottom,
                GrabCorner.Top => BookFlipCorner.Top,
                _ => null
            };
            m_flipView.BeginDrag(point, corner);
            if (TryGetBookPoint(eventData.position, eventData.pressEventCamera, out Vector2 current))
                m_flipView.UpdateDrag(current);
            if (m_closeAtEnds) m_bookController.Flick(rightPage);
            else if (rightPage) m_bookController.ShowNextSpread();
            else m_bookController.ShowPreviousSpread();
            if (!m_bookController.IsBusy)
            {
                // 受け付けられなかった
                m_flipView.CancelDrag();
                return;
            }
            m_pointerId = eventData.pointerId;
            m_dragFlag = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!m_dragFlag || eventData.pointerId != m_pointerId) return;
            if (!TryGetBookPoint(eventData.position, eventData.pressEventCamera, out Vector2 point)) return;
            m_flipView.UpdateDrag(point);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!m_dragFlag || eventData.pointerId != m_pointerId) return;
            OnDrag(eventData);
            m_dragFlag = false;
            m_flipView.EndDrag();
        }

        private bool TryGetBookPoint(Vector2 screenPosition, Camera eventCamera, out Vector2 point) =>
            m_flipView.ScreenToBook(m_rectTransform, screenPosition, eventCamera, out point);

        private void OnDisable()
        {
            if (m_dragFlag && m_flipView != null) m_flipView.EndDrag();
            m_dragFlag = false;
        }
    }
}
