//-----------------------------------------------
// RuneTraceInteraction.cs
// 制作日：2026/09/19
// 制作者：安田晴人
// 概要： ルーンのなぞりインタラクション
//-----------------------------------------------
public class RuneTraceInteraction
    : IPageInteraction
{
    private readonly RuneData m_runeData;

    private readonly RuneTraceController m_runeTraceController;

    private readonly BookInputController m_inputController;

    public bool IsActive => m_runeTraceController.IsTracing;

    public RuneTraceInteraction(RuneData runeData, RuneTraceController runeTraceController, BookInputController inputController)
    {
        m_runeData = runeData;
        m_runeTraceController = runeTraceController;

        m_inputController = inputController;

        m_runeTraceController.TraceSucceeded += OnTraceSucceeded;
    }

    public void Begin()
    {
        m_inputController.BeginRuneTrace();

        m_runeTraceController.BeginTrace(m_runeData);
    }

    public void End()
    {
        m_runeTraceController.Cancel();

        m_inputController.EndRuneTrace();
    }

    private void OnTraceSucceeded(RuneTraceResult result)
    {
        m_inputController.EndRuneTrace();

        // 後でここからCommandへ繋ぐ
    }
}