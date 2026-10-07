//-----------------------------------------------
// MagicManager.cs
// 制作日：2026/10/07
// 制作者：安田晴人
// 概要：魔法の管理を行うマネージャークラス
//-----------------------------------------------
using System;
using UnityEngine;

public class MagicManager : MonoBehaviour
{
    [SerializeField] private MagicDatabase m_magicDatabase;

    public event Action<MagicData> OnMagicCastRequested;

    public void RequestCast(byte magicId)
    {
        MagicData magicData = m_magicDatabase.Find(magicId);

        if (magicData == null)
        {
            Debug.LogWarning($"Magic ID {magicId} が登録されていません");
            return;
        }

        Debug.Log($"Magic Cast : {magicData.DisplayName}");

        OnMagicCastRequested?.Invoke(magicData);
    }
}