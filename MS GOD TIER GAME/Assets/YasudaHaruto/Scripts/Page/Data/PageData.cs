//-----------------------------------------------
// PageData.cs
// 制作日：2026/09/19
// 制作者：安田晴人
// 概要： 1見開き分のページデータ
//-----------------------------------------------
using UnityEngine;

public enum PAGE_TYPE
{
    MAGIC,
    GUIDE,
    OBSTACLE
}

public enum PAGE_INSERT_TYPE
{
    END,
    AFTER_CURRENT,
    RANDOM
}

[CreateAssetMenu(fileName = "PageData", menuName = "Book/Page Data")]
public class PageData : ScriptableObject
{
    [SerializeField] private byte m_pageId;
    [SerializeField] private PAGE_INSERT_TYPE m_insertType;
    [SerializeField] private string m_displayName;
    [SerializeField] private PAGE_TYPE m_pageType;

    [Header("Interaction")]
    [SerializeField] private PageInteractionData m_interaction;
    [SerializeField] private PageActionData m_action;

    [Header("Page Visual")]
    [SerializeField] private BookPageData m_leftVisualData;
    [SerializeField] private BookPageData m_rightVisualData;

    public byte PageId => m_pageId;
    public PAGE_INSERT_TYPE InsertType => m_insertType;
    public string DisplayName => m_displayName;
    public PAGE_TYPE PageType => m_pageType;
    public PageInteractionData Interaction => m_interaction;
    public PageActionData Action => m_action;

    public BookPageData LeftVisualData => m_leftVisualData;
    public BookPageData RightVisualData => m_rightVisualData;
}