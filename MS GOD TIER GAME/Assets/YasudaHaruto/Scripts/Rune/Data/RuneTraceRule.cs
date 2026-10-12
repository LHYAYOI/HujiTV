//-----------------------------------------------
// RuneTraceRule.cs
// 制作日：2026/09/17
// 制作者：安田晴人
// 概要：ルーンのなぞりルールを表すクラス
//-----------------------------------------------
using System;
using UnityEngine;

[Serializable]
public class RuneTraceRule
{
    [Header("なぞりの条件")]
    [SerializeField, Min(1)] private int m_requiredStrokeCount = 1;


    [Header("なぞり判定の条件")]
    [SerializeField, Min(0f)] private float m_distanceTolerance = 0.05f;
    [SerializeField, Range(0f, 1f)] private float m_requiredCoverage = 0.95f;
    [SerializeField, Range(0f, 1f)] private float m_requiredAccuracy = 0.90f;

    [SerializeField, Min(0f)] private float m_startTolerance = 0.04f;
    [SerializeField, Min(0f)] private float m_endTolerance = 0.025f;


    public int RequiredStrokeCount => m_requiredStrokeCount;

    public float DistanceTolerance => m_distanceTolerance;

    public float RequiredCoverage => m_requiredCoverage;

    public float RequiredAccuracy => m_requiredAccuracy;
    public float StartTolerance => m_startTolerance;
    public float EndTolerance => m_endTolerance;

    public void SetTolerances(float distance, float start, float end)
    {
        m_distanceTolerance = Mathf.Max(0f, distance);
        m_startTolerance = Mathf.Max(0f, start);
        m_endTolerance = Mathf.Max(0f, end);
    }
}