//-----------------------------------------------
// BookInputRouter.cs
// 制作日：2026/09/20
// 制作者：安田晴人
// 概要： 本のページめくりの入力をルーティングするクラス
//-----------------------------------------------
using UnityEngine;

public enum BOOK_POINTER_GESTURE
{
    NONE,
    PAGE_NAVIGATION,
    RUNE_TRACE
}


public class BookInputRouter : MonoBehaviour
{
    [SerializeField] private CastSwipeDetector m_castSwipeDetector;
    private Vector2 m_chargeStartScreenPosition;

    private IBookPointerInput m_pointerInput;
    private BookInputController m_inputController;
    private RuneTraceController m_runeTraceController;
    private BookPageAreaDetector m_areaDetector;
    private PageNavigationInput m_pageNavigationInput;

    private BOOK_POINTER_GESTURE m_currentGesture = BOOK_POINTER_GESTURE.NONE;
    private BOOK_AREA_TYPE m_navigationStartArea = BOOK_AREA_TYPE.NONE;

    public event System.Action<PAGE_NAVIGATION_DIRECTION> PageNavigationRequested;

    // 初期化
    public void Initialize(IBookPointerInput pointerInput, BookInputController inputController, RuneTraceController runeTraceController, BookPageAreaDetector areaDetector)
    {
        m_pointerInput = pointerInput;
        m_inputController = inputController;
        m_runeTraceController = runeTraceController;
        m_areaDetector = areaDetector;
        m_pageNavigationInput = new PageNavigationInput();

        m_currentGesture = BOOK_POINTER_GESTURE.NONE;
        m_navigationStartArea = BOOK_AREA_TYPE.NONE;
    }

    private void Update()
    {
        if (m_pointerInput == null || m_inputController == null || m_areaDetector == null)
        {// 初期化されていない場合は処理を行わない
            return;
        }

        if (m_pointerInput == null || m_inputController == null || m_areaDetector == null)
        {
            return;
        }

        m_runeTraceController?.Tick(Time.deltaTime);

        switch (m_inputController.State)
        {
            case BOOK_INPUT_STATE.NORMAL:
                UpdateNormalInput();
                break;

            case BOOK_INPUT_STATE.RUNE_TRACING:
                UpdateRuneTraceInput();
                break;

            case BOOK_INPUT_STATE.PAGE_TRANSITION:
            case BOOK_INPUT_STATE.DISABLED:
                break;
        }
    }

    // 通常入力の更新
    private void UpdateNormalInput()
    {
        if (m_currentGesture == BOOK_POINTER_GESTURE.PAGE_NAVIGATION)
        {// ページめくり中の入力処理
            if (m_pointerInput.TryGetPointerUp(out BookPointerData up))
            {// ページめくりの終了処理
                EndPageNavigationGesture(up);
            }

            return;
        }

        if (m_currentGesture != BOOK_POINTER_GESTURE.NONE)
        {// 他のジェスチャー中は処理を行わない
            return;
        }

        if (!m_pointerInput.TryGetPointerDown(out BookPointerData down))
        {// ポインターダウンがない場合は処理を行わない
            return;
        }

        bool runeEnabled = m_runeTraceController != null && m_runeTraceController.IsTracing;

        if (!m_areaDetector.TryDetect(down.ScreenPosition, out BOOK_AREA_TYPE area, out Vector2 position, runeEnabled))
        {// ポインターダウン位置が有効なエリアでない場合は処理を行わない
            return;
        }

        switch (area)
        {
            case BOOK_AREA_TYPE.RUNE:
                if (m_runeTraceController != null && m_runeTraceController.IsTracing)
                {// ルーントレース中の場合は処理を行わない
                    BeginRuneGesture(position);
                }
                break;

            case BOOK_AREA_TYPE.PREVIOUS_PAGE:
            case BOOK_AREA_TYPE.NEXT_PAGE:
                BeginPageNavigationGesture(down.Position, area);
                break;
        }
    }

    // ルーントレースジェスチャーの開始
    private void BeginRuneGesture(Vector2 runePosition)
    {
        if (m_runeTraceController == null || !m_runeTraceController.CanBeginStroke)
        {// ルーントレースが開始できない場合は処理を行わない
            return;
        }

        m_currentGesture = BOOK_POINTER_GESTURE.RUNE_TRACE;
        m_inputController.BeginRuneTrace();
        m_runeTraceController.BeginStroke(runePosition);
    }

    // ページナビゲーションジェスチャーの開始
    private void BeginPageNavigationGesture(Vector2 position, BOOK_AREA_TYPE area)
    {
        m_currentGesture = BOOK_POINTER_GESTURE.PAGE_NAVIGATION;
        m_navigationStartArea = area;
        m_pageNavigationInput.Begin(position);
    }

    // ページナビゲーションジェスチャーの終了
    private void EndPageNavigationGesture(BookPointerData up)
    {
        // ページナビゲーションの終了処理
        bool success = m_pageNavigationInput.End(up.Position, out PAGE_NAVIGATION_DIRECTION direction);

        // ページナビゲーションの開始エリアを保持
        BOOK_AREA_TYPE startArea = m_navigationStartArea;

        // ジェスチャー状態をリセット
        m_currentGesture = BOOK_POINTER_GESTURE.NONE;
        m_navigationStartArea = BOOK_AREA_TYPE.NONE;

        if (!success)
        {// ページナビゲーションの判定に失敗した場合は処理を行わない
            return;
        }

        // ページナビゲーションの方向が正しいかを判定
        bool validDirection = startArea == BOOK_AREA_TYPE.PREVIOUS_PAGE && direction == PAGE_NAVIGATION_DIRECTION.PREVIOUS ||
            startArea == BOOK_AREA_TYPE.NEXT_PAGE && direction == PAGE_NAVIGATION_DIRECTION.NEXT;

        if (!validDirection)
        {// ページナビゲーションの方向が正しくない場合は処理を行わない
            return;
        }

        // ページナビゲーションのリクエストを通知
        PageNavigationRequested?.Invoke(direction);
        Debug.Log($"Page Navigation Requested : {direction}");
    }

    // ルーントレース入力の更新
    private void UpdateRuneTraceInput()
    {
        if (m_currentGesture == BOOK_POINTER_GESTURE.NONE)
        {// ルーントレースジェスチャーの開始判定

            if (m_pointerInput.TryGetPointerDown(out BookPointerData down) && m_runeTraceController.IsTracing &&
                m_areaDetector.TryDetect(down.ScreenPosition, out BOOK_AREA_TYPE area, out Vector2 position, true) && area == BOOK_AREA_TYPE.RUNE)
            {// ルーントレースジェスチャーの開始判定
                BeginRuneGesture(position);
            }

            return;
        }

        if (m_currentGesture != BOOK_POINTER_GESTURE.RUNE_TRACE)
        {// 他のジェスチャー中は処理を行わない
            return;
        }

        if (m_pointerInput.TryGetPointer(out BookPointerData move) && m_runeTraceController.State == RUNE_TRACE_STATE.DRAWING)
        {// ルーントレース中の入力処理
            if (m_areaDetector.TryGetRunePosition(move.ScreenPosition, out Vector2 runePosition))
            {// ルーントレースのポイントを追加

                m_runeTraceController.AddPoint(runePosition);

                if (m_runeTraceController.State == RUNE_TRACE_STATE.CHARGING)
                {// ルーントレースのチャージ開始位置を記録
                    m_chargeStartScreenPosition = move.ScreenPosition;
                }
            }
        }

        if (!m_pointerInput.TryGetPointerUp(out BookPointerData up))
        {// ポインターアップがない場合は処理を行わない
            return;
        }

        if (m_runeTraceController.State == RUNE_TRACE_STATE.DRAWING && m_areaDetector.TryGetRunePosition(up.ScreenPosition, out Vector2 upRunePosition))
        {// ルーントレースのポイントを追加
            m_runeTraceController.AddPoint(upRunePosition);

            if (m_runeTraceController.State == RUNE_TRACE_STATE.CHARGING)
            {// ルーントレースのチャージ開始位置を記録
                m_chargeStartScreenPosition = up.ScreenPosition;
            }
        }

        if (m_runeTraceController.State == RUNE_TRACE_STATE.CHARGING && m_castSwipeDetector != null &&
            m_castSwipeDetector.TryEvaluate(m_chargeStartScreenPosition, up.ScreenPosition, out MAGIC_CAST_DIRECTION direction))
        {// ルーントレースのチャージが完了し、キャスト方向が判定された場合はキャストを確定
            m_runeTraceController.ConfirmCast(direction);
        }
        else
        {// ルーントレースのチャージが完了しなかった場合はストロークを終了
            m_runeTraceController.EndStroke();
        }

        // ルーントレースの入力処理を終了
        m_inputController.EndRuneTrace();
        m_currentGesture = BOOK_POINTER_GESTURE.NONE;
    }

}
