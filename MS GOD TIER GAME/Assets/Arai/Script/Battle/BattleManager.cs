using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    private BattleData m_currentBattleData;
    



    public void StartBattle(BattleData battleData)
    {
        m_currentBattleData = battleData;

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

        yield return null;
    }
}
