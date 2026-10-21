using UnityEngine;
using UnityEngine.Events;

public class GimmickSplineBattle : GimmickSpline
{

    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――
    [SerializeField] private BattleData m_battleData;

    [SerializeField] private UnityEvent m_onFinishBattle;

    // ―――――――――――――――――――――――――――――――――――――
    // Public Event
    // ―――――――――――――――――――――――――――――――――――――  

    public void SpawnStart()
    {
        BattleManager.StartSpawn(m_battleData);
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
