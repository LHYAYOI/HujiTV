using UnityEngine;

public class GimmickSplineEnemySpawn : GimmickSpline
{
    [SerializeField] private EnemySpawnManager m_enemyManager;
    [SerializeField] private int m_spawnId;



    // ―――――――――――――――――――――――――――――――――――――
    // Public Event
    // ―――――――――――――――――――――――――――――――――――――  
    public void Spawn(HitData hitdata)
    {
        m_enemyManager?.SpawnEnemy(m_spawnId);
    }



    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {
        OnGizmosMyPosition();


    }
}
