using UnityEngine;

public class BattleEnemyData : MonoBehaviour
{
    public enum SPAWN_TYPE
    {
        // 時間で自動スポーン
        TIME_SPLINE,
        TIME_PUPUP,
        // これ以前の敵を全部倒したらスポーン
        WAIT_TIME_SPLINE,
        WAIT_TIME_PUPUP,
    }
    public enum DESTROY_TYPE
    {
        HIT,
        HIT_TIME,
    }




    [Header("----------")]
    [SerializeField] private SPAWN_TYPE m_spawnType;
    // 前の敵がスポーンしてからスポーンするまでの時間
    [SerializeField] private float m_spawnTime;

    [SerializeField] private string m_enemyId;
    [SerializeField] private string m_splineId;
    [SerializeField] private string m_spawnId;

    [Header("----------")]
    [SerializeField] private DESTROY_TYPE m_destroyType;
    [SerializeField] private float m_destroyTime;

    // 生まれた時間を Time.timeでいれとく
    // これをもとにdestroytime spawntimeを求める
    private float m_time;

    // -----------------------------------------

    public SPAWN_TYPE SpawnType => m_spawnType;
    public float SpawnTime => m_spawnTime;


    public string EnemyId => m_enemyId;
    public string SplineId => m_splineId;
    public string SpawnId => m_spawnId;


    public DESTROY_TYPE DestroyType => m_destroyType;
    public float DestroyTime => m_destroyTime;


    public float Time { set { m_time = value; } get { return m_time; } }

}
