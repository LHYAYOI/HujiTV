//-----------------------------------------------
// PageData.cs
// 制作日：2026/09/19
// 制作者：安田晴人
// 概要： ページデータのScriptableObject
//-----------------------------------------------
using UnityEngine;

public enum PAGE_TYPE
{
    MAGIC,
    GUIDE,
    OBSTACLE
}

[CreateAssetMenu(fileName = "PageData", menuName = "Book/Page Data")]
public class PageData : ScriptableObject
{
    [SerializeField]
    private string m_pageId;

    [SerializeField]
    private string m_displayName;

    [SerializeField]
    private PAGE_TYPE m_pageType;

    [SerializeField]
    private PageInteractionData m_interaction;

    [SerializeField]
    private PageActionData m_action;

    public string PageId => m_pageId;
    public string DisplayName => m_displayName;
    public PAGE_TYPE PageType => m_pageType;

    public PageInteractionData Interaction => m_interaction;
    public PageActionData Action => m_action;
}