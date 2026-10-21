using UnityEngine;

[System.Serializable]
public class BattleEnemyData
{

    [SerializeField] private string m_enemyId;
    [SerializeField] private string m_splineId;
    [SerializeField] private string m_spawnId;



    public string EnemyId => m_enemyId;
    public string SplineId => m_splineId;
    public string SpawnId => m_spawnId;


}
