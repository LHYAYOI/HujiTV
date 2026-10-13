using UnityEngine;

public class GimmickSplineEnemySpawn : GimmickSpline
{
    [SerializeField] private EnemySpawnManager m_enemyManager;
    [SerializeField] private string m_enemyId;
    [SerializeField] private string m_splineId;
    [SerializeField] private string m_spawnId;



    // ―――――――――――――――――――――――――――――――――――――
    // Public Event
    // ―――――――――――――――――――――――――――――――――――――  
    public void Spawn(HitData hitdata)
    {
        m_enemyManager?.SpawnSplineMoveEnemy(m_enemyId,m_splineId,m_spawnId);
    }



    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {
        OnGizmosMyPosition();


    }
}
