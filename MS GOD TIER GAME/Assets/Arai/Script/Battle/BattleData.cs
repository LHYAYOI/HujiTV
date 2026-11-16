using System.Collections.Generic;
using UnityEngine;

public class BattleData : MonoBehaviour
{
    [SerializeField] private float m_battleTime;
    [SerializeField] private List<int> m_spawnIds;

    public float BattleTime => m_battleTime;

    public List<int> SpawnIds => m_spawnIds;



}
