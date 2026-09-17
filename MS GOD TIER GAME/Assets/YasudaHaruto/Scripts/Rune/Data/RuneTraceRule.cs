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
    [Header("最大ストローク数と最大試行回数")]
    [SerializeField, Min(1)] private int m_maxStrokeCount = 1;
    [SerializeField, Min(1)] private int m_maxAttemptCount = 1;


    [Header("なぞり判定の条件")]
    [SerializeField, Min(0f)] private float m_distanceTolerance = 0.05f;
    [SerializeField, Range(0f, 1f)] private float m_requiredCoverage = 0.95f;
    [SerializeField, Range(0f, 1f)] private float m_requiredAccuracy = 0.90f;

    public int MaxStrokeCount => m_maxStrokeCount;

    public int MaxAttemptCount => m_maxAttemptCount;

    public float DistanceTolerance => m_distanceTolerance;

    public float RequiredCoverage => m_requiredCoverage;

    public float RequiredAccuracy => m_requiredAccuracy;
}