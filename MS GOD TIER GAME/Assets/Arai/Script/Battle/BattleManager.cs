using System.Collections;
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
        yield return null;
    }
}
