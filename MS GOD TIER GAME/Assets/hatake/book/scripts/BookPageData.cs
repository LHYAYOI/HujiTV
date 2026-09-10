using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BookImageData
{
    [SerializeField]
    private Texture2D m_texture;

    [Tooltip("ページ左上からの位置。Xは右向き、Yは下向き。")]
    [SerializeField]
    private Vector2 m_position = Vector2.zero;

    [Tooltip("ページ内での表示サイズ。")]
    [SerializeField]
    private Vector2 m_size = new Vector2(100f, 100f);

    public Texture2D Texture => m_texture;
    public Vector2 Position => m_position;

    public Vector2 Size => new Vector2(
        Mathf.Max(1f, m_size.x),
        Mathf.Max(1f, m_size.y));
}

[CreateAssetMenu(
    fileName = "Page_New",
    menuName = "Book/Page Data")]
public sealed class BookPageData : ScriptableObject
{
    [Header("管理用")]

    [SerializeField]
    private string m_pageName = "";

    [Header("ページ背景")]

    [SerializeField]
    private Color m_backgroundColor = Color.white;

    public Color BackgroundColor => m_backgroundColor;

    [Header("画像レイアウト")]

    [Tooltip("後ろの要素ほど手前に表示します。")]
    [SerializeField]
    private List<BookImageData> m_images =
        new List<BookImageData>();

    public string PageName => m_pageName;
    public IReadOnlyList<BookImageData> Images => m_images;

    // 全ページ共通の基準サイズ。
    public static Vector2 PageSize => new Vector2(310f, 360f);
}