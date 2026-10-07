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
    [Header("エネミーの識別子")]
    public string m_id;

    [Header("エネミーの名前")]
    public string m_name;

    [Header("エネミーの最大体力")]
    public int m_maxHealth;

    [Header("エネミーの攻撃力")]
    public int m_attack;

    [Header("エネミーの攻撃速度")]
    public float m_attackSpeed;
}
