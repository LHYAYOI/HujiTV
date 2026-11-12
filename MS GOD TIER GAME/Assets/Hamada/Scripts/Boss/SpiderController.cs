using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class SpiderController : MonoBehaviour
{
    [SerializeField] StateMachine m_stateMachine;

    [SerializeField] Animator m_animator;

    [SerializeField] List<SpiderHitColliderController> m_colliders;

    private void OnEnable()
    {
        foreach (SpiderHitColliderController collider in m_colliders)
        {
            collider.SetSpiderController(this);

            collider.SetTargetable(false);
        }

        ChangeState("Encount_State");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L)) 
        {
            EnableDamageableState();
        }
    }

    public void ChangeState(string stateName) 
    {
        m_stateMachine.ChangeState(stateName);
    }

    public void CheckIfDead() 
    {
        foreach (SpiderHitColliderController collider in m_colliders) 
        {
            if (collider.gameObject.activeSelf) 
            {
                return;
            }
        }

        //死亡ステートへ
        ChangeState("Dead_State");
    }

    //狙える状態にする
    public void EnableDamageableState() 
    {
        ChangeState("Damageable_State");
    }

    public void SetCollidersTargetableFlag(bool flag) 
    {
        //コライダーたちを狙えるように
        foreach (SpiderHitColliderController collider in m_colliders)
        {
            if (!collider.gameObject.activeSelf)
            {
                continue;
            }

            collider.SetTargetable(flag);
        }
    }

    public bool IsAnimationFinished(string animationName)
    {
        if (m_animator == null || m_animator.runtimeAnimatorController == null)
        {
            return false;
        }

        AnimatorStateInfo stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);

        if (stateInfo.IsName(animationName) && stateInfo.normalizedTime >= 1.0f)
        {
            return true;
        }

        return false;
    }

    public Animator GetAnimator() 
    {
        return m_animator;
    }
}
