using UnityEngine;
using UnityEngine.InputSystem;

public class ConductorGhostManager : EnemyBase
{
    [Header("指揮者お化け")]
    [SerializeField] private ConductorGhost m_conductorGhost;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    public override void Initialize()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
       
    }

    public void StartBattle()
    {
        m_conductorGhost.StartBattle();
    }

    public void EndBattle()
    {
        m_conductorGhost.SetUpperDrawerLockedFlag(true);
        m_conductorGhost.SetLowerDrawerLockedFlag(true);

        m_conductorGhost.RemoveAllSwarmGhosts();
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.CompareTag("MagicBullet"))
        {
            if(m_conductorGhost.GetIsVisibleFlag())
            {
                return;　// 指揮者お化けが出ている状態では閉めれない
            }

            m_currentHealth--;

            if (m_currentHealth == 1)
            {
                // 上の引き出しから閉める
                m_conductorGhost.SetUpperDrawerLockedFlag(true);
            }
            else if (m_currentHealth <= 0)
            {
                // 下の引き出しを閉める
                m_conductorGhost.SetLowerDrawerLockedFlag(true);
            }
        }
    }
}
