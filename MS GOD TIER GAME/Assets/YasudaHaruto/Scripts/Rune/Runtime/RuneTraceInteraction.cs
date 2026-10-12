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
    private readonly MAGIC_AIM_TYPE m_aimType;
    private readonly ICastMagicCommand m_successCommand;

    public bool IsActive => m_runeTraceController.IsTracing;

    public RuneTraceInteraction(RuneData runeData, RuneTraceController runeTraceController, BookInputController inputController, ICastMagicCommand successCommand, MAGIC_AIM_TYPE aimType)
    {
        m_runeData = runeData;
        m_runeTraceController = runeTraceController;
        m_inputController = inputController;
        m_successCommand = successCommand;
        m_aimType = aimType;
    }

    public void Begin()
    {
        m_runeTraceController.CastConfirmed += OnCastConfirmed;
        m_runeTraceController.TraceFailed += OnTraceFailed;
        m_runeTraceController.BeginTrace(m_runeData, m_aimType);
    }

    public void End()
    {
        m_runeTraceController.CastConfirmed -= OnCastConfirmed;
        m_runeTraceController.TraceFailed -= OnTraceFailed;

        m_runeTraceController.Cancel();

        if (m_inputController.State == BOOK_INPUT_STATE.RUNE_TRACING)
            m_inputController.EndRuneTrace();
    }

    private void OnCastConfirmed(MAGIC_CAST_DIRECTION direction)
    {
        m_successCommand?.Execute(direction);
    }

    private void OnTraceFailed(RuneTraceResult result)
    {
        Debug.Log($"Rune失敗 Before : {m_inputController.State}");

        m_inputController.EndRuneTrace();

        Debug.Log($"Rune失敗 After : {m_inputController.State}");
    }
}