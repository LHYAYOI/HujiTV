using UnityEngine;

[System.Serializable]
public class BattleEnemyData
{
    public enum SPAWN_TYPE 
    {
        SPLINE = 0,
        POPUP,
    }




    [Header("----------")]
    [SerializeField]private SPAWN_TYPE m_spawnType;
    
    [SerializeField] private string m_enemyId;
    [SerializeField] private string m_splineId;
    [SerializeField] private string m_spawnId;


    // 生まれた時間を Time.timeでいれとく
    // これをもとにdestroytime spawntimeを求める
    private float m_time;

    // -----------------------------------------

    public SPAWN_TYPE SpawnType => m_spawnType;
    public string EnemyId => m_enemyId;
    public string SplineId => m_splineId;
    public string SpawnId => m_spawnId;

    public float Time { set { m_time = value; } get { return m_time; } }

}
