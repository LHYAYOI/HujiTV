using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BookImageDataは、BookPageData内で表示する画像の情報を管理するクラスです

[Serializable]
public sealed class BookImageData
{
    [SerializeField]
    private Sprite m_sprite;

    [Tooltip("画像に適用するUI用Material。未設定なら標準表示")]
    [SerializeField]
    private Material m_material;

    [Tooltip("ページ左上からの位置。Xは右向き、Yは下向き")]
    [SerializeField]
    private Vector2 m_position = Vector2.zero;

    [Tooltip("ページ内での表示サイズ。")]
    [SerializeField]
    private Vector2 m_size = new Vector2(100f, 100f);

    public Sprite Sprite => m_sprite;
    public Material Material => m_material;
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

    [Header("画像レイアウト")]

    [Tooltip("後ろの要素ほど手前に表示します。")]
    [SerializeField]
    private List<BookImageData> m_images =
        new List<BookImageData>();

    public string PageName => m_pageName;
    public Color BackgroundColor => m_backgroundColor;
    public IReadOnlyList<BookImageData> Images => m_images;

    [Tooltip("ページの基準サイズ。UI表示や3Dモデル表示で使用します")]
    public static Vector2 PageSize => new Vector2(310f, 360f);
}