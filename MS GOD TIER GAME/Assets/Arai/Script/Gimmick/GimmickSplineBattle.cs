using UnityEngine;
using UnityEngine.Events;

public class GimmickSplineBattle : GimmickSpline
{

    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――
    [SerializeField] private BattleData m_battleData;

    [SerializeField] private EnemySpawnManager m_enemyManager;

    [SerializeField] private UnityEvent m_onFinishBattle;

    // ―――――――――――――――――――――――――――――――――――――
    // Public Event
    // ―――――――――――――――――――――――――――――――――――――  

    public void SpawnStart()
    {
        foreach (int id in m_battleData.SpawnIds)
        {
            m_enemyManager.SpawnEnemy(id);
        }

        BattleManager.SetCurrentBattleData(m_battleData);
    }

    public void BattleStart()
    {
        BattleManager.StartBattle(m_onFinishBattle);
    }

    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {
        OnGizmosMyPosition();


    }
}
