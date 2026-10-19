using System.Collections.Generic;
using UnityEngine;

public class BattleData : MonoBehaviour
{
    [SerializeField] private float m_battleTime;
    [SerializeField] private List<BattleEnemyData> m_enemyData;

    public float BattleTime => m_battleTime;
    public List<BattleEnemyData> EnemyData => m_enemyData;



    private void OnValidate()
    {
        /*
        foreach (var data in m_enemyData)
        {
            float time = data.SpawnTime + data.DestroyTime;

            if(m_battleTime < time)
            {
                m_battleTime = time;
            }
        }
        */
    }
}
