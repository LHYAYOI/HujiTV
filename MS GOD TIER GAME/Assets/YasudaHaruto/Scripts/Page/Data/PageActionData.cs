//-----------------------------------------------
// PageActionData.cs
// 制作日：2026/09/30
// 制作者：安田晴人
// 概要： ページアクションデータのシリアライズ可能なクラス
//-----------------------------------------------
using System;
using UnityEngine;

public enum PAGE_ACTION_TYPE
{
    NONE,
    CAST_MAGIC,
    REMOVE_PAGE
}

[Serializable]
public class PageActionData
{
    [SerializeField]
    private PAGE_ACTION_TYPE m_type;

    [SerializeField]
    private MagicData m_magicData;

    public PAGE_ACTION_TYPE Type => m_type;
    public MagicData MagicData => m_magicData;
}