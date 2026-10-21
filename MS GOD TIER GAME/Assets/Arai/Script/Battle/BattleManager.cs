using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class BattleManager : MonoBehaviour
{
    [SerializeField] private ActiveEnemyManager m_activeEnemyManager;

    private EnemySpawnManager m_spawnManager;
    private BattleData m_currentBattleData;
    private UnityEvent m_onFinishEvent;
    
    public static BattleManager Instance {  get; private set; }



    private void Awake()
    {
        m_spawnManager = FindAnyObjectByType<EnemySpawnManager>();
        Instance = this; 
    }

    public void StartSpawn(BattleData battleData)
    {
        if (m_spawnManager == null)
        {
            return;
        }

        m_currentBattleData = battleData;

        StartCoroutine(SpawnEnemyCoroutine());
    }


    private IEnumerator SpawnEnemyCoroutine()
    {
        if (m_currentBattleData == null)
        {
            yield break;
        }

        List<BattleEnemyData> enemyData = m_currentBattleData.EnemyData;

        foreach (BattleEnemyData data in enemyData)
        {
            m_spawnManager.SpawnSplineMoveEnemy(data.EnemyId, data.SplineId, data.SpawnId);
        }
    }

    public void StartBattle(UnityEvent onFinishEvent)
    {
        if(m_spawnManager == null)
        {
            return; 
        }

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


        // ミニオバケをバトル開始状態にする
        m_activeEnemyManager.SetAllMiniGhostsToBattleStart();




        // バトル始まります
        float time = 0.0f;
        time = Time.time;

        while(true)
        {
            // 全ての敵が倒された場合はバトル終了イベントを呼び出す
            if (m_activeEnemyManager.GetActiveEnemies().Count() == 0)
            {
                break;
            }

            // バトル時間が経過するまで待機する
            if ( Time.time - time > m_currentBattleData.BattleTime)
            {
                // アクティブな敵が残っている場合は全て削除する
                if (m_activeEnemyManager.GetActiveEnemies().Count() != 0)
                {
                    // バトル終了時に全ての敵を削除する
                    m_activeEnemyManager.RemoveAllActiveEnemies();
                }
                break;
            }

            yield return null;
        }
        

        m_onFinishEvent?.Invoke();

        yield return null;
    }
}
