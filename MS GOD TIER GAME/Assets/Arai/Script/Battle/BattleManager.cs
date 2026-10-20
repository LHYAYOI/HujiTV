using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BattleManager : MonoBehaviour
{


    private EnemySpawnManager m_spawnManager;
    private BattleData m_currentBattleData;
    private UnityEvent m_onFinishEvent;
    
    public static BattleManager Instance {  get; private set; }



    private void Awake()
    {
        m_spawnManager = FindAnyObjectByType<EnemySpawnManager>();
        Instance = this; 
    }

    public void StartBattle(BattleData battleData,UnityEvent onFinishEvent)
    {
        if(m_spawnManager == null)
        {
            return; 
        }

        m_currentBattleData = battleData;
        m_onFinishEvent = onFinishEvent;

        StartCoroutine(BattleCoroutine());
    }

    private IEnumerator BattleCoroutine()
    {
        if(m_currentBattleData == null)
        {
            yield break;
        }
        


        // バトルの情報
        float startTime = Time.time;
        float lastSpawnTime = startTime;
        List<BattleEnemyData> enemyData = m_currentBattleData.EnemyData;


        //        List<EnemyBase> enemyList = EnemySpawnManager.getEnemyList();
        /*
        foreach (BattleEnemyData data in enemyData)
        {
            switch (data.SpawnType)
            {
                case BattleEnemyData.SPAWN_TYPE.TIME_SPLINE:
                    break;
                case BattleEnemyData.SPAWN_TYPE.TIME_PUPUP:
                    break;
                case BattleEnemyData.SPAWN_TYPE.WAIT_TIME_SPLINE:
                    //                    yield return new WaitUntil();
                    break;
                case BattleEnemyData.SPAWN_TYPE.WAIT_TIME_PUPUP:
                    break;
            }

            //        yield return new WaitUntil(() => enemyList.Count == 0);

        }
        */


        foreach(BattleEnemyData data in  enemyData)
        {
            m_spawnManager.SpawnSplineMoveEnemy(data.EnemyId, data.SplineId, data.SpawnId);
        }


        yield return new WaitForSeconds(m_currentBattleData.BattleTime);

        m_onFinishEvent?.Invoke();

        yield return null;
    }
}
