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

    private bool m_isBattleFlag;
    public static bool IsBattleFlag => Instance?.m_isBattleFlag ?? false;



    private void Awake()
    {
        m_spawnManager = FindAnyObjectByType<EnemySpawnManager>();
        Instance = this; 
    }

    public static void StartSpawn(BattleData battleData)
    {
        if (Instance.m_spawnManager == null)
        {
            return;
        }

        Instance.m_currentBattleData = battleData;

        Instance.StartCoroutine(Instance.SpawnEnemyCoroutine());
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
            if(data.SplineId != "")
            {
                m_spawnManager.SpawnSplineMoveEnemy(data.EnemyId, data.SplineId, data.SpawnId);
            }
            else
            {
                m_spawnManager.SpawnPopUpEnemy(data.EnemyId, data.SpawnId);
            }
            
        }
    }

    public static void StartBattle(UnityEvent onFinishEvent)
    {
        if(Instance.m_spawnManager == null)
        {
            return; 
        }

        Instance.m_isBattleFlag = true;
        Instance.m_onFinishEvent = onFinishEvent;

        Instance.StartCoroutine(Instance.BattleCoroutine());
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


        // エネミーをバトル開始状態にする
        m_activeEnemyManager.SetAllActiveEnemiesToBattleStart();




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
        m_isBattleFlag = false;

        yield return null;
    }
}
