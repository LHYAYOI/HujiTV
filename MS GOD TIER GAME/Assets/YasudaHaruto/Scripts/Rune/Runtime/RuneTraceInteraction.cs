//-----------------------------------------------
// RuneTraceInteraction.cs
// 制作日：2026/09/19
// 制作者：安田晴人
// 概要： ルーンのなぞりインタラクション
//-----------------------------------------------
using UnityEngine;

public class RuneTraceInteraction : IPageInteraction
{
    private readonly RuneData m_runeData;
    private readonly RuneTraceController m_runeTraceController;
    private readonly BookInputController m_inputController;
    private readonly IPageCommand m_successCommand;

    public bool IsActive => m_runeTraceController.IsTracing;

    public RuneTraceInteraction(RuneData runeData, RuneTraceController runeTraceController, BookInputController inputController, IPageCommand successCommand)
    {
        m_runeData = runeData;
        m_runeTraceController = runeTraceController;
        m_inputController = inputController;
        m_successCommand = successCommand;
    }

    public void Begin()
    {
        m_runeTraceController.TraceSucceeded += OnTraceSucceeded;
        m_runeTraceController.TraceFailed += OnTraceFailed;

        m_runeTraceController.BeginTrace(m_runeData);
    }

    public void End()
    {
        m_runeTraceController.TraceSucceeded -= OnTraceSucceeded;
        m_runeTraceController.TraceFailed -= OnTraceFailed;

        m_runeTraceController.Cancel();

        if (m_inputController.State == BOOK_INPUT_STATE.RUNE_TRACING)
        {
            m_inputController.EndRuneTrace();
        }
    }

    private void OnTraceSucceeded(RuneTraceResult result)
    {
        m_inputController.EndRuneTrace();
        m_successCommand?.Execute();
    }

    private void OnTraceFailed(RuneTraceResult result)
    {
        Debug.Log($"Rune失敗 Before : {m_inputController.State}");

        m_inputController.EndRuneTrace();

        Debug.Log($"Rune失敗 After : {m_inputController.State}");
    }
}