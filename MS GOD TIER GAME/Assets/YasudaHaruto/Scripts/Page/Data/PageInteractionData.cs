//-----------------------------------------------
// PageInteractionData.cs
// 制作日：2026/09/30
// 制作者：安田晴人
// 概要： ページのインタラクションデータを保持するクラス
//-----------------------------------------------
using System;
using UnityEngine;

public enum PAGE_INTERACTION_TYPE
{
    NONE,
    RUNE_TRACE,
    TAP
}

[Serializable]
public class PageInteractionData
{
    [SerializeField]
    private PAGE_INTERACTION_TYPE m_type;

    [SerializeField]
    private RuneData m_runeData;

    public PAGE_INTERACTION_TYPE Type => m_type;
    public RuneData RuneData => m_runeData;
}