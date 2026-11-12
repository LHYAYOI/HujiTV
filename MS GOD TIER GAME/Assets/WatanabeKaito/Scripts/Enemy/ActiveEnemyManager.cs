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

    // エネミーをすべて削除
    public void RemoveAllActiveEnemies()
    {
        foreach (var enemy in m_activeEnemies)
        {
            if (enemy != null)
            {
               Destroy(enemy.gameObject);
            }
        }
        m_activeEnemies.Clear();
    }


    // アクティブな全てのエネミーをバトル開始状態にする
    public void SetAllActiveEnemiesToBattleStart()
    {
        foreach (var enemy in m_activeEnemies)
        {
            if (enemy != null)
            {
                enemy.BattleStart();
            }
        }
    }

    // アクティブな全てのエネミーをバトル終了状態にする
    public void SetAllActiveEnemiesToBattleEnd()
    {
        foreach (var enemy in m_activeEnemies)
        {
            if (enemy != null)
            {
                enemy.BattleEnd();
            }
        }
    }
}
