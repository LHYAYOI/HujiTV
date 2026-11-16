//-----------------------------------------------
// EnemyData.cs
// 制作日：2026/09/29
// 制作者：渡辺開斗
// 概要：エネミーデータのScriptableObject
//-----------------------------------------------
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Enemy/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("エネミーの最大体力")]
    public int m_maxHealth;

    [Header("1回の攻撃で与える汚れの数")]
    public int m_dirtAmount;

    [Header("制限時間の有無(無しならチェックを外す)")]
    public bool m_hasTimeLimit = true;
}
