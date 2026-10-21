//-----------------------------------------------
// BookGameDragInput.cs
// 制作日：2026/10/21
// 制作者：安田晴人
// 概要： 本のページめくりとルーン入力のドラッグ操作を管理するスクリプト
//-----------------------------------------------
using UnityEngine;
using UnityEngine.EventSystems;

namespace YasudaHaruto.BookValidation
{
[RequireComponent(typeof(RectTransform))]
public class BookGameDragInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private enum GESTURE { NONE, RUNE, PAGE }

    [Header("Book")]
    [SerializeField] private YasudaBookViewController m_bookController;
    [SerializeField] private hatake.BookFlipView m_flipView;
    [SerializeField] private BookPageAreaDetector m_areaDetector;
    [SerializeField] private CastSwipeDetector m_castSwipeDetector;

    [Header("Page Flip")]
    [SerializeField] private hatake.BookFlipDragInput.GrabSide m_grabSide = hatake.BookFlipDragInput.GrabSide.Both;
    [SerializeField] private hatake.BookFlipDragInput.GrabCorner m_grabCorner = hatake.BookFlipDragInput.GrabCorner.Auto;
    [SerializeField] private bool m_closeAtEnds = true;
    [SerializeField, Range(0.05f, 1f)] private float m_grabZone = 1f;

    private RectTransform m_displayRect;
    private BookGameController m_inventoryOwner;
    public bool IsGestureActive => m_gesture != GESTURE.NONE;
    public void SetInventoryOwner(BookGameController owner) => m_inventoryOwner = owner;
    private RuneTraceController m_runeController;
    private BookInputController m_inputController;

    private GESTURE m_gesture;
    private int m_pointerId;
    private bool m_runeStarted;
    private Vector2 m_chargeStartPosition;

    private void Awake()
    {
        m_displayRect = (RectTransform)transform;
    }

    public void Initialize(RuneTraceController runeController, BookInputController inputController)
    {
        m_runeController = runeController;
        m_inputController = inputController;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (m_inventoryOwner != null && m_inventoryOwner.IsInputBlocked) return;
        if (eventData.button != PointerEventData.InputButton.Left || m_gesture != GESTURE.NONE || m_areaDetector == null || m_runeController == null || m_inputController == null)
        {
            return;
        }

        if (!m_areaDetector.TryDetect(eventData.position, out BOOK_AREA_TYPE area, out Vector2 runePosition, m_runeController.IsTracing))
        {// エリア検出に失敗した場合は無効
            return;
        }

        if (area != BOOK_AREA_TYPE.RUNE)
        {// ルーン領域以外の入力はページめくりに渡す
            return;
        }

        // クールダウン中もルーン領域の入力をページめくりに渡さない
        m_gesture = GESTURE.RUNE;
        m_pointerId = eventData.pointerId;
        m_runeStarted = false;

        if (!m_runeController.CanBeginStroke || m_inputController.State != BOOK_INPUT_STATE.NORMAL)
        {// ルーン入力が開始できない場合は無効
            return;
        }

        // ルーン入力を開始する
        m_inputController.BeginRuneTrace();
        m_runeController.BeginStroke(runePosition);
        m_runeStarted = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (m_inventoryOwner != null && m_inventoryOwner.IsInputBlocked) return;
        if (m_gesture != GESTURE.NONE || eventData.button != PointerEventData.InputButton.Left ||
            m_bookController == null || m_flipView == null || m_bookController.IsBusy || m_flipView.IsTurning)
        {
            return;
        }

        if (!TryGetBookPoint(eventData.pressPosition, eventData.pressEventCamera, out Vector2 point))
        {// 本の座標に変換できない場合は無効
            return;
        }

        if (!m_flipView.ContainsPage(point) || Mathf.Abs(point.x) < 1f - m_grabZone)
        {// ページ内でつかめる範囲外の場合は無効
            return;
        }

        // 右ページか左ページかを判定
        bool rightPage = point.x >= 0f;

        if ((rightPage && m_grabSide == hatake.BookFlipDragInput.GrabSide.LeftOnly) || (!rightPage && m_grabSide == hatake.BookFlipDragInput.GrabSide.RightOnly))
        {// つかめない側のページの場合は無効
            return;
        }

        // 右をつかむと次へ、左は前へ。閉じた表紙は開き、端の見開きでは閉じる（Close At Ends）
        bool canTurn = m_closeAtEnds ? m_bookController.CanFlick(rightPage) : rightPage ? m_bookController.CanTurnForward : m_bookController.CanTurnBackward;

        if (!canTurn)
        {
            return;
        }

        // つかむ角を決定
        hatake.BookFlipCorner? corner = m_grabCorner switch
        {
            hatake.BookFlipDragInput.GrabCorner.Bottom => hatake.BookFlipCorner.Bottom,
            hatake.BookFlipDragInput.GrabCorner.Top => hatake.BookFlipCorner.Top,
            _ => null
        };

        // ドラッグを開始
        m_flipView.BeginDrag(point, corner);

        if (TryGetBookPoint(eventData.position, eventData.pressEventCamera, out Vector2 current))
        {// ドラッグ中の座標を更新
            m_flipView.UpdateDrag(current);
        }

        if (m_closeAtEnds)
        {// 端の見開きで閉じる場合はフリックを開始
            m_bookController.Flick(rightPage);
        }
        else if (rightPage)
        {// 右ページをつかんだ場合は次の見開きを表示
            m_bookController.ShowNextSpread();
        }
        else
        {// 左ページをつかんだ場合は前の見開きを表示
            m_bookController.ShowPreviousSpread();
        }

        if (!m_bookController.IsBusy)
        {// ページめくりが受け付けられなかった場合はドラッグをキャンセル
            m_flipView.CancelDrag();
            return;
        }

        // ドラッグ中のポインタIDを記録
        m_gesture = GESTURE.PAGE;
        m_pointerId = eventData.pointerId;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (m_gesture == GESTURE.NONE || eventData.pointerId != m_pointerId)
        {// ドラッグ中でない場合は無効
            return;
        }

        if (m_gesture == GESTURE.RUNE)
        {// ルーン入力中の場合はルーンの座標を更新
            UpdateRune(eventData.position);
            return;
        }

        if (TryGetBookPoint(eventData.position, eventData.pressEventCamera, out Vector2 point))
        {// ページめくり中の場合はドラッグの座標を更新
            m_flipView.UpdateDrag(point);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (m_gesture != GESTURE.RUNE || eventData.pointerId != m_pointerId)
        {// ルーン入力中でない場合は無効
            return;
        }

        if (m_runeStarted)
        {// ルーン入力中の場合はルーンの座標を更新して入力を終了

            // ルーン入力中の座標を更新
            UpdateRune(eventData.position);

            if (m_runeController.State == RUNE_TRACE_STATE.CHARGING && m_castSwipeDetector != null &&
                m_castSwipeDetector.TryEvaluate(m_chargeStartPosition, eventData.position, out MAGIC_CAST_DIRECTION direction))
            {// ルーン入力がチャージ中で、スワイプ方向が判定できた場合はキャストを確定
                m_runeController.ConfirmCast(direction);
            }
            else
            {// ルーン入力を終了
                m_runeController.EndStroke();
            }

            // ルーン入力を終了
            m_inputController.EndRuneTrace();
        }

        // ジェスチャーをリセット
        m_gesture = GESTURE.NONE;
        m_runeStarted = false;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (m_gesture != GESTURE.PAGE || eventData.pointerId != m_pointerId)
        {// ページめくり中でない場合は無効
            return;
        }

        if (TryGetBookPoint(eventData.position, eventData.pressEventCamera, out Vector2 point))
        {// ページめくり中の場合はドラッグの座標を更新
            m_flipView.UpdateDrag(point);
        }

        m_flipView.EndDrag();
        m_gesture = GESTURE.NONE;
    }

    private void UpdateRune(Vector2 screenPosition)
    {
        if (!m_runeStarted || m_runeController.State != RUNE_TRACE_STATE.DRAWING)
        {// ルーン入力が開始されていない場合や、描画中でない場合は無効
            return;
        }

        if (!m_areaDetector.TryGetRunePosition(screenPosition, out Vector2 runePosition))
        {// ルーン座標に変換できない場合は無効
            return;
        }

        // ルーン入力中の座標を更新
        m_runeController.AddPoint(runePosition);

        if (m_runeController.State == RUNE_TRACE_STATE.CHARGING)
        {// ルーン入力がチャージ中の場合はスワイプ開始位置を記録 
            m_chargeStartPosition = screenPosition;
        }
    }

    private bool TryGetBookPoint(Vector2 screenPosition, Camera eventCamera, out Vector2 point)
    {
        return m_flipView.ScreenToBook(m_displayRect, screenPosition, eventCamera, out point);
    }

    private void OnDisable()
    {
        if (m_gesture == GESTURE.PAGE && m_flipView != null)
        {// ページめくり中の場合はドラッグを終了
            m_flipView.EndDrag();
        }

        if (m_gesture == GESTURE.RUNE && m_runeStarted)
        {// ルーン入力中の場合はルーン入力を終了
            m_runeController?.EndStroke();
            m_inputController?.EndRuneTrace();
        }

        m_gesture = GESTURE.NONE;
        m_runeStarted = false;
    }
}

}
