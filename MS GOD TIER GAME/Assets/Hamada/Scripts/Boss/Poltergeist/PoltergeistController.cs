using System.Collections.Generic;
using UnityEngine;

public class PoltergeistController : EnemyBase
{
    [SerializeField] StateMachine m_stateMachine;

    [SerializeField] List<TargetableObject> m_selectStateColliders;
    [SerializeField] List<TargetableObject> m_stunStateColliders;

    GameObject m_shotObject;

    int m_attackedCount = 0;

    // スポナーからの呼び出し用初期化関数
    public override void Initialize() 
    {
    }

    // バトル開始時の処理を行う関数
    public override void BattleStart() 
    {
    }

    // バトル終了時の処理を行う関数
    public override void BattleEnd() 
    {
    }

    private void Start()
    {
        ChangeState("Encounter");
    }

    public void ChangeState(string stateName) 
    {
        if (m_stateMachine == null) 
        {
            return;
        }

        m_stateMachine.ChangeState(stateName);
    }

    public void SetShotObject(GameObject shotObject) 
    {
        m_shotObject = shotObject;
    }

    public GameObject GetShotObject() 
    {
        return m_shotObject;
    }

    public void AddAttackedCount() 
    {
        m_attackedCount++;
    }

    public int GetAttackedCount() 
    {
        return m_attackedCount;
    }

    //額縁が全て攻撃されたか
    public void CheckIfIsAllPictureFrameAttacked() 
    {
        foreach (TargetableObject collider in m_selectStateColliders) 
        {
            if (collider == null) 
            {
                continue;
            }

            if(collider.IsTargetable())
            {
                return;
            }
        }

        //スタン状態へ
        ChangeState("Stun");
    }

    //額縁のコライダーをまた狙えるようにする
    public void ActiveAllPictureFrameCollider() 
    {
        foreach (TargetableObject collider in m_selectStateColliders)
        {
            if (collider == null)
            {
                continue;
            }

            collider.SetTargetable(true);
        }
    }
}
