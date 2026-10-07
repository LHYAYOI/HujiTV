//-----------------------------------------------
// BookInputController.cs
// 制作日：2026/09/20
// 制作者：安田晴人
// 概要： 本のページの入力状態を管理するクラス
//-----------------------------------------------

public class BookInputController
{
    private BOOK_INPUT_STATE m_state = BOOK_INPUT_STATE.NORMAL;

    public BOOK_INPUT_STATE State => m_state;

    public bool CanNavigatePage => m_state == BOOK_INPUT_STATE.NORMAL;

    public bool CanTraceRune => m_state == BOOK_INPUT_STATE.RUNE_TRACING;

    public void BeginRuneTrace()
    {
        m_state = BOOK_INPUT_STATE.RUNE_TRACING;
    }

    public void EndRuneTrace()
    {
        if (m_state != BOOK_INPUT_STATE.RUNE_TRACING)
        {
            return;
        }

        m_state = BOOK_INPUT_STATE.NORMAL;
    }

    public void BeginPageTransition()
    {
        m_state = BOOK_INPUT_STATE.PAGE_TRANSITION;
    }

    public void EndPageTransition()
    {
        if (m_state != BOOK_INPUT_STATE.PAGE_TRANSITION)
        {
            return;
        }

        m_state = BOOK_INPUT_STATE.NORMAL;
    }

    public void Disable()
    {
        m_state = BOOK_INPUT_STATE.DISABLED;
    }

    public void Enable()
    {
        if (m_state != BOOK_INPUT_STATE.DISABLED)
        {
            return;
        }

        m_state = BOOK_INPUT_STATE.NORMAL;
    }
}