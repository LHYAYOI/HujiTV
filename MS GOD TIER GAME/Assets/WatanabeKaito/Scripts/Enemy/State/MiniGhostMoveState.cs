using UnityEngine;


public class MiniGhostMoveState : MiniGhostState
{

    [Header("ˆÚ“®ƒXƒs[ƒh")]
    [SerializeField] private float m_moveSpeed = 1f;

    [Header("ƒXƒvƒ‰ƒCƒ“‚É‡‚í‚¹‚Ä‰ñ“]‚·‚é‚©")]
    [SerializeField] private bool m_rotateWithSplineFlag = true;

    [Header("UŒ‚ŠÔŠui•bj")]
    [SerializeField] private float m_fireInterval = 2.0f;

    private float m_fireTimer;  // UŒ‚ŠÔŠu‚Ìƒ^ƒCƒ}[

    public override void EnterState()
    {
        m_fireTimer = 0f;
        m_owner.SetMoveSpeed(m_moveSpeed);
        m_owner.SetRotateWithSplineFlag(m_rotateWithSplineFlag);
    }

    public override void UpdateState()
    {
        if (m_owner.GetIsDying())
        {
            return;
        }

        m_owner.UpdateSplineMove();

        m_fireTimer += Time.deltaTime;
        if (m_fireTimer >= m_fireInterval)
        {
            m_owner.ChangeState(MINI_GHOST_STATE_TYPE.ATTACK);
        }
    }
}
