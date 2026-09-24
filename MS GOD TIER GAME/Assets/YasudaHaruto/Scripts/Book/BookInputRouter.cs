using UnityEngine;

public class BookInputRouter : MonoBehaviour
{
    private IBookPointerInput m_pointerInput;

    private BookInputController m_inputController;

    private RuneTraceController m_runeTraceController;

    public void Initialize(IBookPointerInput pointerInput, BookInputController inputController, RuneTraceController runeTraceController)
    {
        m_pointerInput = pointerInput;
        m_inputController = inputController;
        m_runeTraceController = runeTraceController;
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
        // éüÇ…ÉyÅ[ÉWSwipeÇé¿ëïÇ∑ÇÈ
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