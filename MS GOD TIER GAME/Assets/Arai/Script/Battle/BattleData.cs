using System.Collections.Generic;
using UnityEngine;

public class BattleData : MonoBehaviour
{
    [SerializeField] private float m_battleTime;
    [SerializeField] private List<BattleEnemyData> m_enemyData;

    public float BattleTime => m_battleTime;
    public List<BattleEnemyData> EnemyData => m_enemyData;



}
