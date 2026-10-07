//-----------------------------------------------
// MagicDatabase.cs
// 制作日：2026/10/07
// 制作者：安田晴人
// 概要：魔法データベースのスクリプタブルオブジェクト
//-----------------------------------------------

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MagicDatabase", menuName = "Book/Magic Database")]
public class MagicDatabase : ScriptableObject
{
    [SerializeField] private List<MagicData> m_magicList = new();

    public MagicData Find(byte magicId)
    {
        return m_magicList.Find(magic => magic.MagicId == magicId);
    }
}