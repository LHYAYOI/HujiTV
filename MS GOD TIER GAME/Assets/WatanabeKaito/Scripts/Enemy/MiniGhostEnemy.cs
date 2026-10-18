using UnityEngine;
using UnityEngine.Splines;

public enum MINI_GHOST_STATE_TYPE
{
    NONE,
    SPAWN,
    MOVE,
    ATTACK,
    DYING
}

public class MiniGhostEnemy : SplineMoveEnemy
{
    [Header("制限時間")]
    [SerializeField] private float m_timeLimit = 15.0f;

    [Header("蹴りの弾の発射位置")]
    [SerializeField] private Transform m_kickFirePoint;

    [Header("投げの弾の発射位置")]
    [SerializeField] private Transform m_throwFirePoint;

    private MiniGhostSpawnState m_spawnState;
    private MiniGhostMoveState m_moveState;
    private MiniGhostAttackState m_attackState;
    private MiniGhostDyingState m_dyingState;

    private MINI_GHOST_STATE_TYPE m_currentStateType = MINI_GHOST_STATE_TYPE.NONE;
    private MiniGhostState m_currentState;

    private bool m_isInitialized;
    private bool m_timeLimitStarted;
    private bool m_isDying;

  

    public override void Initialize()
    {
        base.Initialize();

        if (m_isInitialized)
        {
            return;
        }

        m_isInitialized = true;
        ResolveStateComponents();
        InitializeStateComponents();
        ChangeState(MINI_GHOST_STATE_TYPE.SPAWN);
    }

    protected override void Update()
    {
        if (m_timeLimitStarted && !m_isDying)
        {
            m_timeLimit -= Time.deltaTime;
            if (m_timeLimit <= 0f)
            {
                BeginDying();
            }
        }

        // 現在の状態だけを更新する。
        if (m_currentState != null)
        {
            m_currentState.UpdateState();
        }
    }

    private void ResolveStateComponents()
    {
        m_spawnState = GetComponent<MiniGhostSpawnState>();
        m_moveState = GetComponent<MiniGhostMoveState>();
        m_attackState = GetComponent<MiniGhostAttackState>();
        m_dyingState = GetComponent<MiniGhostDyingState>();
    }

    private void InitializeStateComponents()
    {
        InitializeComponent(m_spawnState);
        InitializeComponent(m_moveState);
        InitializeComponent(m_attackState);
        InitializeComponent(m_dyingState);
    }

    private void InitializeComponent(MiniGhostState state)
    {
        if (state != null)
        {
            state.InitializeState(this);
        }
    }

    public void ChangeState(MINI_GHOST_STATE_TYPE nextStateType)
    {
        // 死亡後にほかの状態へ戻ることを防ぐ。
        if (m_isDying && nextStateType != MINI_GHOST_STATE_TYPE.DYING)
        {
            return;
        }

        MiniGhostState nextState = GetStateComponent(nextStateType);
        if (nextState == null)
        {
            return;
        }

        if (m_currentState == nextState)
        {
            return;
        }

        if (m_currentState != null)
        {
            m_currentState.ExitState();
        }

        m_currentState = nextState;
        m_currentStateType = nextStateType;
        m_currentState.EnterState();
    }

    private MiniGhostState GetStateComponent(MINI_GHOST_STATE_TYPE stateType)
    {
        switch (stateType)
        {
            case MINI_GHOST_STATE_TYPE.SPAWN: return m_spawnState;
            case MINI_GHOST_STATE_TYPE.MOVE: return m_moveState;
            case MINI_GHOST_STATE_TYPE.ATTACK: return m_attackState;
            case MINI_GHOST_STATE_TYPE.DYING: return m_dyingState;
            default: return null;
        }
    }

    /// <summary>制限時間や被弾から呼び出し、死亡処理を一度だけ開始する。</summary>
    public void BeginDying()
    {
        if (m_isDying)
        {
            return;
        }

        m_isDying = true;
        m_timeLimitStarted = false;

        if (m_spawnManager != null)
        {
            m_spawnManager.RemoveActiveEnemy(this);
        }

        ChangeState(MINI_GHOST_STATE_TYPE.DYING);
    }

    public MINI_GHOST_STATE_TYPE GetCurrentStateType()
    {
        return m_currentStateType;
    }

    public bool GetIsDying()
    {
        return m_isDying;
    }

    public Transform GetKickFirePoint()
    {
        return m_kickFirePoint;
    }

    public Transform GetThrowFirePoint()
    {
        return m_throwFirePoint;
    }

    // SplineMoveEnemyの移動更新をMove状態から呼び出す。
    public void UpdateSplineMove()
    {
        base.Update();
    }
    public SplineContainer GetTargetSpline()
    {
        return m_targetSpline;
    }

    public Vector3 GetSpawnPosition()
    {
        return m_spawnPosition;
    }

    public Vector3 GetPlayerPosition()
    {
        // プレイヤーの位置の少し手前の位置を返す
        return m_playerPosition.position;
    }

    public float GetDistanceTraveled()
    {
        return m_distanceTraveled;
    }

    public Vector3 GetKickFirePosition()
    {
        return m_kickFirePoint.position;
    }

    public Vector3 GetThrowFirePosition()
    {
        return m_throwFirePoint.position;
    }

    public void SetMoveSpeed(float speed)
    {
        m_moveSpeed = speed;
    }

    public void SetRotateWithSplineFlag(bool flag)
    {
        m_rotateWithSplineFlag = flag;
    }

    public void SetDistanceTraveled(float distance)
    {
        m_distanceTraveled = distance;
    }

    public void SetMoveDirection(float direction)
    {
        m_moveDirection = direction;
    }

    public void StartTimeLimit()
    {
        if (!m_timeLimitStarted && !m_isDying)
        {
            m_timeLimitStarted = true;
        }
    }

    // 旧コードとの互換用。新しいコードでは IsDying を使用する。
    public bool GgtIsDyingFlag() => m_isDying;

    private void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.CompareTag("MagicBullet"))
        {
            BeginDying();
        }
    }
}