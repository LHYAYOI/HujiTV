using UnityEngine;


public class MiniGhostMoveState : MiniGhostState
{

    [Header("移動スピード")]
    [SerializeField] private float m_moveSpeed = 1f;

    [Header("スプラインに合わせて回転するか")]
    [SerializeField] private bool m_rotateWithSplineFlag = true;

    [Header("攻撃間隔（秒）")]
    [SerializeField] private float m_fireInterval = 2.0f;

    private float m_fireTimer;

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
