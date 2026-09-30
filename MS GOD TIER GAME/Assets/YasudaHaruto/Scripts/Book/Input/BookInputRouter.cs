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
    private IBookPointerInput m_pointerInput;
    private BookInputController m_inputController;
    private RuneTraceController m_runeTraceController;
    private PageNavigationInput m_pageNavigationInput;
    private RuneInputArea m_runeInputArea;
    private BOOK_POINTER_GESTURE m_currentGesture = BOOK_POINTER_GESTURE.NONE;

    public event System.Action<PAGE_NAVIGATION_DIRECTION> PageNavigationRequested;

    public void Initialize(IBookPointerInput pointerInput, BookInputController inputController, RuneTraceController runeTraceController, RuneInputArea runeInputArea)
    {
        m_pointerInput = pointerInput;
        m_inputController = inputController;
        m_runeTraceController = runeTraceController;
        m_runeInputArea = runeInputArea;

        m_pageNavigationInput = new PageNavigationInput();
    }

    private void Update()
    {
        if (m_pointerInput == null || m_inputController == null)
        {
            return;
        }

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

    private void UpdateNormalInput()
    {
        if (!m_pointerInput.TryGetPointerDown(out BookPointerData down))
        {
            return;
        }

        if (CanStartRuneTrace(down))
        {
            BeginRuneGesture(down);
            return;
        }

        BeginPageNavigationGesture(down);
    }

    private bool CanStartRuneTrace(BookPointerData data)
    {
        if (m_runeTraceController == null)
        {
            return false;
        }

        if (!m_runeTraceController.IsTracing)
        {
            return false;
        }

        if (m_runeInputArea == null)
        {
            return false;
        }

        return m_runeInputArea.ContainsScreenPosition(data.ScreenPosition);
    }

    private void BeginRuneGesture(BookPointerData down)
    {
        if (!m_runeInputArea.TryConvertToNormalized(down.ScreenPosition, out Vector2 runePosition))
        {
            return;
        }

        m_currentGesture = BOOK_POINTER_GESTURE.RUNE_TRACE;

        m_inputController.BeginRuneTrace();

        m_runeTraceController.BeginStroke(runePosition);
    }

    private void BeginPageNavigationGesture(BookPointerData down)
    {
        m_currentGesture = BOOK_POINTER_GESTURE.PAGE_NAVIGATION;

        m_pageNavigationInput.Begin(down.Position);
    }

    private void UpdateRuneTraceInput()
    {
        if (m_currentGesture == BOOK_POINTER_GESTURE.NONE)
        {
            if (m_pointerInput.TryGetPointerDown(out BookPointerData down))
            {
                BeginRuneGesture(down);
            }

            return;
        }

        if (m_currentGesture != BOOK_POINTER_GESTURE.RUNE_TRACE)
        {
            return;
        }

        if (m_pointerInput.TryGetPointer(out BookPointerData move))
        {
            if (m_runeInputArea.TryConvertToNormalized(move.ScreenPosition, out Vector2 runePosition))
            {
                m_runeTraceController.AddPoint(runePosition);
            }
        }

        if (m_pointerInput.TryGetPointerUp(out _))
        {
            m_runeTraceController.EndStroke();

            m_currentGesture = BOOK_POINTER_GESTURE.NONE;
        }
    }
}