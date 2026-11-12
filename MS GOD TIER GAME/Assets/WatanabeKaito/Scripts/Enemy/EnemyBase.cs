//-----------------------------------------------
// EnemyBase.cs
// 制作日：2026/09/30
// 制作者：渡辺開斗
// 概要：エネミーの基底クラス
//-----------------------------------------------
using UnityEngine;

public abstract class EnemyBase : MonoBehaviour
{
    [Header("エネミーデータ")]
    [SerializeField] protected EnemyData m_enemyData;

    protected int m_currentHealth;  // 現在の体力

    protected Transform m_playerPosition; // プレイヤーの位置

    protected ActiveEnemyManager m_activeEnemyManager; // アクティブなエネミーを管理するマネージャー

    protected virtual void Awake()
    {
        if (m_enemyData != null)
        {
            m_currentHealth = m_enemyData.m_maxHealth;
        }
    }

    // スポナーからの呼び出し用初期化関数
    public abstract void Initialize();

    // バトル開始時の処理を行う関数
    public abstract void BattleStart();

    // バトル終了時の処理を行う関数
    public abstract void BattleEnd();

    // エネミーデータを取得する関数
    public EnemyData GetEnemyData()
    {
        return m_enemyData;
    }

    // プレイヤーの位置を設定する関数
    public void SetPlayerPosition(Transform playerTransform)
    {
        m_playerPosition = playerTransform;
    }

    // スポーンマネージャーの参照を設定する関数
    public void SetActiveEnemyManager(ActiveEnemyManager activeEnemyManager)
    {
        m_activeEnemyManager = activeEnemyManager;
    }
}
