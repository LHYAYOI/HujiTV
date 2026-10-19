using System.Collections.Generic;
using UnityEngine;

public class ActiveEnemyManager : MonoBehaviour
{
    private List<EnemyBase> m_activeEnemies = new List<EnemyBase>(); // 現在アクティブなエネミーのリスト

    // 現在アクティブなエネミーリストを取得
    public List<EnemyBase> GetActiveEnemies()
    {
        return m_activeEnemies;
    }

    // エネミーをアクティブリストに追加
    public void AddActiveEnemy(EnemyBase enemy)
    {
        if (!m_activeEnemies.Contains(enemy))
        {
            m_activeEnemies.Add(enemy);
        }
    }

    // エネミーをアクティブリストから削除
    public void RemoveActiveEnemy(EnemyBase enemy)
    {
        if (m_activeEnemies.Contains(enemy))
        {
            m_activeEnemies.Remove(enemy);
        }
    }

    // エネミーのデータのIDを指定してアクティブリストから削除
    public void RemoveActiveEnemyByData(string enemyId)
    {
        EnemyBase enemyToRemove = m_activeEnemies.Find(enemy => enemy.GetEnemyData().m_id == enemyId);
        if (enemyToRemove != null)
        {
            // MiniGhostEnemyの場合は状態をDYINGに変更
            MiniGhostEnemy miniGhostEnemy = enemyToRemove as MiniGhostEnemy;
            if (miniGhostEnemy != null)
            {
                miniGhostEnemy.ChangeState(MINI_GHOST_STATE_TYPE.DYING);
            }
          
            m_activeEnemies.Remove(enemyToRemove);
        }
    }

    // エネミーをすべて削除
    public void RemoveAllActiveEnemies()
    {
        foreach (var enemy in m_activeEnemies)
        {
            if (enemy != null)
            {
                // MiniGhostEnemyの場合は状態をDYINGに変更
                MiniGhostEnemy miniGhostEnemy = enemy as MiniGhostEnemy;
                if (miniGhostEnemy != null)
                {
                    miniGhostEnemy.ChangeState(MINI_GHOST_STATE_TYPE.DYING);
                }
               
            }
        }
        m_activeEnemies.Clear();
    }

    // ミニオバケのデータのIDを指定してバトル開始状態にする
    public void SetActiveEnemyToBattleStart(string enemyId)
    {
        EnemyBase enemy = m_activeEnemies.Find(e => e.GetEnemyData().m_id == enemyId);
        if (enemy != null)
        {
            MiniGhostEnemy miniGhostEnemy = enemy as MiniGhostEnemy;
            if (miniGhostEnemy != null)
            {
                miniGhostEnemy.StartTimeLimit();
                miniGhostEnemy.ChangeState(MINI_GHOST_STATE_TYPE.MOVE);
            }
        }
    }

    // アクティブなミニオバケをすべてバトル開始状態にする
    public void SetAllMiniGhostsToBattleStart()
    {
        foreach (var enemy in m_activeEnemies)
        {
            if (enemy != null)
            {
                MiniGhostEnemy miniGhostEnemy = enemy as MiniGhostEnemy;
                if (miniGhostEnemy != null)
                {
                    miniGhostEnemy.StartTimeLimit();
                    miniGhostEnemy.ChangeState(MINI_GHOST_STATE_TYPE.MOVE);
                }
            }
        }
    }
}
