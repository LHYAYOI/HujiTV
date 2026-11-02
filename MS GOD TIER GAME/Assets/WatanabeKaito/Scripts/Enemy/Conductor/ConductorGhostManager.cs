using UnityEngine;
using UnityEngine.InputSystem;

public class ConductorGhostManager : EnemyBase
{
    [Header("指揮者お化け")]
    [SerializeField] private ConductorGhost m_conductorGhost;

    private bool m_battleStartFlag = false; // 戦闘開始フラグ

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
       // デバック
        //if (Keyboard.current != null &&
        //        Keyboard.current.bKey.wasPressedThisFrame)
        //{
        //    m_conductorGhost.SetUpperDrawerLockedFlag(true);
        //    StartCoroutine(m_conductorGhost.LockUpperChest());
        //}

        //if (Keyboard.current != null &&
        //       Keyboard.current.nKey.wasPressedThisFrame)
        //{
        //    m_conductorGhost.SetLowerDrawerLockedFlag(true);
        //    StartCoroutine(m_conductorGhost.LockLowerChest());
        //}
    }

    public void StartBattle()
    {
        m_conductorGhost.StartBattle();

        m_battleStartFlag = true;
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
            if(m_conductorGhost.GetIsVisibleFlag() || !m_battleStartFlag)
            {
                return;
            }

            HitReceiverController hitReceiver = collision.gameObject.GetComponent<HitReceiverController>();

            if (hitReceiver != null)
            {
                HitData hitData = new HitData();

                hitData.SetHitObject(this.gameObject);

                hitReceiver.OnHit(hitData);
            }

            m_currentHealth--;

            if (m_currentHealth == 1)
            {
                // 上の引き出しから閉める
                m_conductorGhost.SetUpperDrawerLockedFlag(true);
                StartCoroutine(m_conductorGhost.LockUpperChest());

            }
            else if (m_currentHealth <= 0)
            {
                // 下の引き出しを閉める
                m_conductorGhost.SetLowerDrawerLockedFlag(true);
                StartCoroutine(m_conductorGhost.LockLowerChest());
            }
        }
    }
}
