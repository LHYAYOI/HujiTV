using System.Collections.Generic;
using UnityEngine;

public class BattleData : MonoBehaviour
{
    [SerializeField] private float m_battleTime;
    [SerializeField] private List<int> m_spawnIds;

    [Header("制限時間の有無（無しの場合はチェックを外す）")]
    [SerializeField] private bool m_hasTimeLimit = true;

    public float BattleTime => m_battleTime;

    public List<int> SpawnIds => m_spawnIds;

    public bool HasTimeLimit => m_hasTimeLimit;
}
