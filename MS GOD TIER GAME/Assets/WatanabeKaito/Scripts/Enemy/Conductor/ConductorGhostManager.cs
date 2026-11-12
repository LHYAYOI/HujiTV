using UnityEngine;
using UnityEngine.InputSystem;

public class ConductorGhostManager : EnemyBase, IEventMagicReceiver
{
    [Header("上の引き出しのTargetable")]
    [SerializeField] private TargetableObject m_upperDrawerTargetable;

    [Header("下の引き出しのTargetable")]
    [SerializeField] private TargetableObject m_lowerDrawerTargetable;

    [Header("指揮者お化け")]
    [SerializeField] private ConductorGhost m_conductorGhost;

    private bool m_battleStartFlag = false; // 戦闘開始フラグ

    private bool m_isUpperFlag = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_upperDrawerTargetable.SetTargetable(false);
        m_lowerDrawerTargetable.SetTargetable(false);
       
    }

    public override void Initialize()
    {
       
    }

    public override void BattleStart()
    {
        StartBattle();
    }

    public override void BattleEnd()
    {
        EndBattle();
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

        m_upperDrawerTargetable.SetTargetable(false);
        m_lowerDrawerTargetable.SetTargetable(false);

        m_conductorGhost.DeactivateAllSwarmGhosts();
    }

    public void OnChestHit()
    {

    }

    // MagicBulletが当たったときの処理
    private void HandleMagicBulletHit(HitData hitData)
    {
        if (!m_battleStartFlag)
        {
            return;
        }

        if (hitData != null)
        {
            MagicData magicData = hitData.GetMagicData;

            if (magicData != null)
            {
                TryReceiveEventMagic(magicData.EventMagicType);

                if (magicData.EventMagicType == EVENT_MAGIC_TYPE.CLOSE)
                {

                    if (m_isUpperFlag)
                    {
                        // 上の引き出しの処理
                        StartCoroutine(m_conductorGhost.LockUpperChest());
                    }
                    else
                    {
                        // 下の引き出しの処理
                        StartCoroutine(m_conductorGhost.LockLowerChest());
                    }
                }
            }
        }

        
    }

    public bool TryReceiveEventMagic(EVENT_MAGIC_TYPE eventMagicType)
    {
        if (eventMagicType == EVENT_MAGIC_TYPE.CLOSE)
        {
            
        }

        return false;
    }

    // 上の引き出しに当たった場合
    public void OnUpperDrawerHit(HitData hitData)
    {
        if(!m_conductorGhost.GetIsUpperVisibleFlag())
        {
            m_upperDrawerTargetable.SetTargetable(false);

            m_isUpperFlag = true;
            // 上の引き出しが開いている場合のみ処理を行う
            HandleMagicBulletHit(hitData);
        }
    }

    // 下の引き出しに当たった場合
    public void OnLowerDrawerHit(HitData hitData)
    {
        if(!m_conductorGhost.GetIsLowerVisibleFlag())
        {
            m_lowerDrawerTargetable.SetTargetable(false);

            m_isUpperFlag = false;
            // 下の引き出しが開いている場合のみ処理を行う
            HandleMagicBulletHit(hitData);
        }
    }

    
}
