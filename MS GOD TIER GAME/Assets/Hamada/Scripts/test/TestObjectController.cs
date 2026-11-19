using UnityEngine;

public class TestObjectController : EnemyBase
{
    [SerializeField] EnemyDeathTextManager manager;
    [SerializeField] Transform m_deathTextPosition;
    [SerializeField] Vector2 m_deathTextScale;

    public void HitProcess(HitData data) 
    {
        manager.PlayText(m_deathTextPosition.position, m_deathTextScale, m_enemyData.m_deathText);

        Destroy(gameObject);

    }

    public override void Initialize()
    {
    }

    public override void BattleStart() 
    {
    }

    // バトル終了時の処理を行う関数
    public override void BattleEnd() 
    {
    }
}
