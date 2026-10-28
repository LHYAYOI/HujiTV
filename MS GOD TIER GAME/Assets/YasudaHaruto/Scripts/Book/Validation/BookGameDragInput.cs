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
    [Header("Cast Guide")]
    [SerializeField] private Vector2 m_castOrigin = new Vector2(0.5f, 0.25f);
    [SerializeField, Range(0.03f, 0.2f)] private float m_castRadius = 0.08f;
    [SerializeField, Range(0f, 0.85f)] private float m_castShade = 0.55f;
    [SerializeField, Min(0.1f)] private float m_castPulseSpeed = 1f;
    [SerializeField, Range(0f, 0.25f)] private float m_castPulseAmount = 0.08f;
    [SerializeField, Range(0.02f, 0.2f)] private float m_castArrowWidth = 0.1f;
    private RuneCastOverlay m_castOverlay;

    private void Update()
    {
        bool show = m_runeController != null &&
            (m_runeController.State == RUNE_TRACE_STATE.WAITING_CAST_START ||
             m_runeController.State == RUNE_TRACE_STATE.CHARGING);
        if (!show)
        {
            if (m_castOverlay != null) m_castOverlay.Hide();
            return;
        }
        if (m_castOverlay == null)
        {
            m_castOverlay = new GameObject("Rune Cast Guide", typeof(RectTransform)).AddComponent<RuneCastOverlay>();
            m_castOverlay.Configure(m_runeController, m_castSwipeDetector, GetBookScreenRect,
                m_castOrigin, m_castRadius, m_castShade, m_castPulseSpeed, m_castPulseAmount, m_castArrowWidth);
        }
        m_castOverlay.gameObject.SetActive(true);
    }

    public Rect? GetBookScreenRect()
    {
        if (m_flipView == null || m_displayRect == null) return null;
        Vector2 min = m_flipView.UvToBook(Vector2.zero);
        Vector2 max = m_flipView.UvToBook(Vector2.one);
        Vector2 extent = max - min;
        if (Mathf.Abs(extent.x) < 0.0001f || Mathf.Abs(extent.y) < 0.0001f) return null;
        Canvas canvas = m_displayRect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 lower = ProjectBookPoint(new Vector2(-1f, 0f), min, extent, camera);
        Vector2 upper = ProjectBookPoint(new Vector2(1f, m_flipView.PageAspect), min, extent, camera);
        return Rect.MinMaxRect(Mathf.Min(lower.x, upper.x), Mathf.Min(lower.y, upper.y),
            Mathf.Max(lower.x, upper.x), Mathf.Max(lower.y, upper.y));
    }

    private Vector2 ProjectBookPoint(Vector2 book, Vector2 min, Vector2 extent, Camera camera)
    {
        Vector2 uv = new Vector2((book.x - min.x) / extent.x, (book.y - min.y) / extent.y);
        Rect rect = m_displayRect.rect;
        return RectTransformUtility.WorldToScreenPoint(camera, m_displayRect.TransformPoint(new Vector3(
            Mathf.LerpUnclamped(rect.xMin, rect.xMax, uv.x), Mathf.LerpUnclamped(rect.yMin, rect.yMax, uv.y), 0f)));
    }

    private void OnDestroy()
    {
        if (m_castOverlay != null) Destroy(m_castOverlay.gameObject);
    }

    private void Awake()
    {
        m_displayRect = (RectTransform)transform;
    }

    public void Initialize(RuneTraceController runeController, BookInputController inputController)
    {
        m_runeController = runeController;
        m_inputController = inputController;
    }

    public void SetRuneInputAvailable(bool available)
    {
        m_runeController?.SetInputAvailable(available);
        if (available) return;
        if (m_castOverlay != null) m_castOverlay.Hide();
        if (m_gesture == GESTURE.RUNE)
        {
            m_gesture = GESTURE.NONE;
            m_runeStarted = false;
        }
        m_inputController?.EndRuneTrace();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (m_runeController != null && m_runeController.IsInputAvailable && m_runeController.IsAwaitingCast) return;
        if (m_inventoryOwner != null && m_inventoryOwner.IsInputBlocked) return;
        if (eventData.button != PointerEventData.InputButton.Left || m_gesture != GESTURE.NONE || m_areaDetector == null || m_runeController == null || m_inputController == null)
        {
            return;
        }

        if (!m_areaDetector.TryDetect(eventData.position, out BOOK_AREA_TYPE area, out Vector2 runePosition, m_runeController.IsTracing))
        {// エリア検出に失敗した場合は無効
            return;
        }

        if (!m_runeController.IsInputAvailable || area != BOOK_AREA_TYPE.RUNE)
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
        if (m_runeController != null && m_runeController.IsInputAvailable && m_runeController.IsAwaitingCast) return;
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

            m_runeController.EndStroke();

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


    }

    public Rect? GetRuneScreenRect()
    {
        return m_areaDetector != null && m_areaDetector.TryGetRuneScreenRect(out Rect rect)
            ? rect : (Rect?)null;
    }

    public Rect? GetGraphicScreenRect(RectTransform graphic)
    {
        return m_areaDetector != null && m_areaDetector.TryGetGraphicScreenRect(graphic, out Rect rect)
            ? rect : (Rect?)null;
    }

    private bool TryGetBookPoint(Vector2 screenPosition, Camera eventCamera, out Vector2 point)
    {
        return m_flipView.ScreenToBook(m_displayRect, screenPosition, eventCamera, out point);
    }

    private void OnDisable()
    {
        if (m_castOverlay != null) m_castOverlay.Hide();
        if (m_runeController != null && m_runeController.IsAwaitingCast) m_runeController.Cancel();
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
