//-----------------------------------------------
// RuneData.cs
// 制作日：2026/09/17
// 制作者：安田晴人
// 概要：ルーンのデータを表すクラス
//-----------------------------------------------
using UnityEngine;

[CreateAssetMenu(fileName = "RuneData", menuName = "Rune/Rune Data")]
public class RuneData : ScriptableObject
{

    [SerializeField] private string m_runeId;
    [SerializeField] private string m_displayName;

    [SerializeField] private RuneAuthoringData m_authoringData = new();

    [SerializeField] private RuneTraceData m_traceData = new();

    [SerializeField] private RuneTraceRule m_rule = new();


    public string RuneId => m_runeId;

    public string DisplayName => m_displayName;

    public RuneAuthoringData AuthoringData => m_authoringData;
    public RuneTraceData TraceData => m_traceData;

    public RuneTraceRule Rule => m_rule;
}