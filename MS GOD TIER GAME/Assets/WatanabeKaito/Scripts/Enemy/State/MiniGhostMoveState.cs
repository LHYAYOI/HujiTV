using UnityEngine;

[CreateAssetMenu(fileName = "NewMiniGhostMoveState", menuName = "States/MiniGhostStates/MiniGhostMoveState")]

public class MiniGhostMoveState : StateBase
{
    [Header("移動スピード")]
    [SerializeField] private float m_moveSpeed = 1f;

    [Header("スプラインに合わせて回転するかしないか")]
    [SerializeField] private bool m_rotateWithSplineFlag = true;

    [Header("弾の発射間隔（秒）")]
    [SerializeField] private float m_fireInterval = 2.0f;

    private float m_fireTimer = 0f; // 弾の発射タイマー
    bool m_isShooting = false; // 射撃フラグ


    MiniGhostEnemy m_miniGhostEnemy;
    public override void Awake()
    {
       
    }

    public override void Start()
    {
        m_miniGhostEnemy = m_stateOwner.GetComponent<MiniGhostEnemy>();

        m_miniGhostEnemy.SetMoveSpeed(m_moveSpeed);
        m_miniGhostEnemy.SetRotateWithSplineFlag(m_rotateWithSplineFlag);
    }

    public override void EnterState()
    {
        m_fireTimer = 0f; // タイマーをリセット
        m_isShooting = false; // 射撃フラグをリセット
    }

    public override void UpdateState()
    {
        m_miniGhostEnemy.UpdateSplineMove();

        // 弾の発射タイマーを更新
        UpdateShooting();
    }

    public override void ExitState()
    {

    }

    // 射撃タイマー処理
    private void UpdateShooting()
    {
        m_fireTimer += Time.deltaTime;
        if (m_fireTimer >= m_fireInterval)
        {
            if (!m_isShooting)
            {
                m_stateMachine.ChangeState("MiniGhostAttackState");
                m_isShooting = true; // 射撃フラグをON
            }
        }
    }
}
