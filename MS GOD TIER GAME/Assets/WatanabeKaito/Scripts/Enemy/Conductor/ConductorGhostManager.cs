using UnityEngine;
using UnityEngine.InputSystem;

public class ConductorGhostManager : EnemyBase, IEventMagicReceiver
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

    public void OnChestHit()
    {

    }

    // MagicBulletが当たったときの処理
    private void HandleMagicBulletHit(Collider collision, bool isUpperDrawer)
    {
        if (!m_battleStartFlag)
        {
            return;
        }

        HitReceiverController hitReceiver = collision.gameObject.GetComponent<HitReceiverController>();

        if (hitReceiver != null)
        {
            HitData hitData = new HitData();

            hitData.SetHitObject(this.gameObject);

            MagicData magicData = collision.gameObject.GetComponent<MagicBulletController>()?.GetMagicData();

            hitData.SetMagicData(magicData);

            hitReceiver.OnHit(hitData);

            if (magicData != null)
            {
                TryReceiveEventMagic(magicData.EventMagicType);

                if (magicData.EventMagicType == EVENT_MAGIC_TYPE.CLOSE)
                {
                    // 引き出しごとに異なる処理を追加する場合
                    if (isUpperDrawer)
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
    public void OnUpperDrawerHit(Collider collision)
    {
        if(!m_conductorGhost.GetIsUpperVisibleFlag())
        {
            // 上の引き出しが開いている場合のみ処理を行う
            HandleMagicBulletHit(collision, true);
        }
    }

    // 下の引き出しに当たった場合
    public void OnLowerDrawerHit(Collider collision)
    {
        if(!m_conductorGhost.GetIsLowerVisibleFlag())
        {
            // 下の引き出しが開いている場合のみ処理を行う
            HandleMagicBulletHit(collision, false);
        }
    }

    
}
