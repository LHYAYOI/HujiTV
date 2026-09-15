//-----------------------------------------------
// MagicSystem.cs
// 制作日：2026/09/11
// 制作者：安田晴人
// 概要： 魔法システムを管理するクラス
//-----------------------------------------------

using UnityEngine;

public class MagicSystem : MonoBehaviour
{
    [SerializeField]
    private BookInputState m_bookInputState;

    [SerializeField]
    private MAGIC_TYPE[] m_pageMagics =
    {
        MAGIC_TYPE.FIRE,
        MAGIC_TYPE.ICE,
        MAGIC_TYPE.THUNDER,
        MAGIC_TYPE.HEAL
    };

    private void OnEnable()
    {
        if (m_bookInputState != null)
        {
            m_bookInputState.OnCastRequested += OnCastRequested;
        }
    }

    private void OnDisable()
    {
        if (m_bookInputState != null)
        {
            m_bookInputState.OnCastRequested -= OnCastRequested;
        }
    }

    private void OnCastRequested(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= m_pageMagics.Length)
        {
            Debug.LogWarning($"魔法が設定されていないPageです : {pageIndex}");

            return;
        }

        MAGIC_TYPE magic = m_pageMagics[pageIndex];

        Cast(magic);
    }

    private void Cast(MAGIC_TYPE magic)
    {
        Debug.Log($"===== {magic} 発動 =====");
    }
}