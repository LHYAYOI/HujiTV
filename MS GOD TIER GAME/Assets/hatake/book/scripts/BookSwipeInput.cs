
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public sealed class BookSwipeInput : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [SerializeField]
    private BookController m_bookController;

    [Tooltip("見開き幅に対する、ページ移動に必要な移動量の割合。")]
    [SerializeField, Range(0.05f, 0.5f)]
    private float m_swipeThresholdRatio = 0.15f;

    private RectTransform m_rectTransform;
    private Vector2 m_startPosition;
    private int m_pointerId;
    private bool m_dragFlag;

    private void Awake()
    {
        m_rectTransform = (RectTransform)transform;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (m_dragFlag ||
            eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        bool convertFlag =
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_rectTransform,
                eventData.pressPosition,
                eventData.pressEventCamera,
                out Vector2 startPosition);

        if (!convertFlag)
        {
            return;
        }

        m_startPosition = startPosition;
        m_pointerId = eventData.pointerId;
        m_dragFlag = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 今回はドラッグ終了時に判定します。
        // UIのドラッグイベントを受けるために実装。
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!m_dragFlag || eventData.pointerId != m_pointerId)
        {
            return;
        }

        m_dragFlag = false;

        if (m_bookController == null)
        {
            return;
        }

        bool convertFlag =
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                m_rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 endPosition);

        if (!convertFlag)
        {
            return;
        }

        Vector2 movement = endPosition - m_startPosition;

        float threshold =
            m_rectTransform.rect.width * m_swipeThresholdRatio;

        // 短い移動と、縦方向が強い移動は無視。
        if (Mathf.Abs(movement.x) < threshold ||
            Mathf.Abs(movement.x) <= Mathf.Abs(movement.y))
        {
            return;
        }

        if (movement.x < 0f)
        {
            m_bookController.ShowNextSpread();
        }
        else
        {
            m_bookController.ShowPreviousSpread();
        }
    }

    private void OnDisable()
    {
        m_dragFlag = false;
    }
}