//-----------------------------------------------
// BookInputRouter.cs
// 制作日：2026/09/20
// 制作者：安田晴人
// 概要： 本のページめくりの入力をルーティングするクラス
//-----------------------------------------------
using UnityEngine;

public class BookInputRouter : MonoBehaviour
{
    private IBookPointerInput m_pointerInput;
    private BookInputController m_inputController;
    private RuneTraceController m_runeTraceController;
    private PageNavigationInput m_pageNavigationInput;

    public event System.Action<PAGE_NAVIGATION_DIRECTION> PageNavigationRequested;

    public void Initialize(IBookPointerInput pointerInput, BookInputController inputController, RuneTraceController runeTraceController)
    {
        m_pointerInput = pointerInput;
        m_inputController = inputController;
        m_runeTraceController = runeTraceController;

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
        if (m_pointerInput.TryGetPointerDown(out BookPointerData down))
        {
            m_pageNavigationInput.Begin(down.Position);
        }

        if (m_pointerInput.TryGetPointerUp(out BookPointerData up))
        {
            if (m_pageNavigationInput.End(up.Position, out PAGE_NAVIGATION_DIRECTION direction))
            {
                PageNavigationRequested?.Invoke(direction);
            }
        }
    }

    private void UpdateRuneTraceInput()
    {
        if (m_runeTraceController == null)
        {
            return;
        }

        if (m_pointerInput.TryGetPointerDown(out BookPointerData down))
        {
            m_runeTraceController.BeginStroke(down.Position);
        }

        if (m_pointerInput.TryGetPointer(out BookPointerData move))
        {
            m_runeTraceController.AddPoint(move.Position);
        }

        if (m_pointerInput.TryGetPointerUp(out _))
        {
            m_runeTraceController.EndStroke();
        }
    }
}