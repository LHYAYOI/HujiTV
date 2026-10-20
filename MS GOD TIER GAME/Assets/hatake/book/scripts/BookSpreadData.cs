using UnityEngine;

/// <summary>
/// 見開きセット（新規格）。左右2ページを1組として扱う。
/// 本の中では必ず見開きの左右にそろって並び、燃焼・復活も2ページ同時に行う。
/// 1ページずつ扱う従来の BookPageData（旧方式）と同じ本に混ぜられる。
/// </summary>
[CreateAssetMenu(
    fileName = "Spread_New",
    menuName = "Book/Spread Data")]
public sealed class BookSpreadData : ScriptableObject
{
    [Header("管理用")]
    [SerializeField] private string m_spreadName = "";

    [Header("ページ")]
    [SerializeField] private BookPageData m_leftPage;
    [SerializeField] private BookPageData m_rightPage;

    public string SpreadName => m_spreadName;
    public BookPageData LeftPage => m_leftPage;
    public BookPageData RightPage => m_rightPage;
}
