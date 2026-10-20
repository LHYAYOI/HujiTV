using UnityEngine;
using UnityEngine.Splines;

public enum MINI_GHOST_STATE_TYPE // ミニお化けのステートタイプ
{
    NONE,
    IDLE,   // 待機
    SPAWN,  // 出現
    MOVE,   // 移動
    ATTACK, // 攻撃
    DYING   // 死亡
}

public class MiniGhostEnemy : SplineMoveEnemy
{
    [Header("制限時間")]
    [SerializeField] private float m_timeLimit = 15.0f;

    [Header("蹴りの弾の発射位置")]
    [SerializeField] private Transform m_kickFirePoint;

    [Header("投げの弾の発射位置")]
    [SerializeField] private Transform m_throwFirePoint;

    private MiniGhostIdleState m_idleState;     // 待機ステート
    private MiniGhostSpawnState m_spawnState;   // 出現ステート
    private MiniGhostMoveState m_moveState;     // 移動ステート
    private MiniGhostAttackState m_attackState; // 攻撃ステート
    private MiniGhostDyingState m_dyingState;   // 死亡ステート

    private MINI_GHOST_STATE_TYPE m_currentStateType = MINI_GHOST_STATE_TYPE.NONE; // 現在のステートタイプ
    private MiniGhostState m_currentState;  // 現在のステートコンポーネント

    private bool m_isInitialized;       // 初期化済みフラグ
    private bool m_timeLimitStarted;    // 制限時間開始フラグ
    private bool m_isDying;             // 死亡中フラグ


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

    // 各状態コンポーネントを取得する。
    private void ResolveStateComponents()
    {
        m_idleState = GetComponent<MiniGhostIdleState>();
        m_spawnState = GetComponent<MiniGhostSpawnState>();
        m_moveState = GetComponent<MiniGhostMoveState>();
        m_attackState = GetComponent<MiniGhostAttackState>();
        m_dyingState = GetComponent<MiniGhostDyingState>();
    }

    // 各状態コンポーネントを初期化する。
    private void InitializeStateComponents()
    {
        InitializeComponent(m_idleState);
        InitializeComponent(m_spawnState);
        InitializeComponent(m_moveState);
        InitializeComponent(m_attackState);
        InitializeComponent(m_dyingState);
    }

    // 各状態コンポーネントを初期化する。
    private void InitializeComponent(MiniGhostState state)
    {
        if (state != null)
        {
            state.InitializeState(this);
        }
    }

    // ステートを変更
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

    // 各ステートを取得
    private MiniGhostState GetStateComponent(MINI_GHOST_STATE_TYPE stateType)
    {
        switch (stateType)
        {
            case MINI_GHOST_STATE_TYPE.IDLE: return m_idleState;
            case MINI_GHOST_STATE_TYPE.SPAWN: return m_spawnState;
            case MINI_GHOST_STATE_TYPE.MOVE: return m_moveState;
            case MINI_GHOST_STATE_TYPE.ATTACK: return m_attackState;
            case MINI_GHOST_STATE_TYPE.DYING: return m_dyingState;
            default: return null;
        }
    }

    // 死亡処理を開始する
    public void BeginDying()
    {
        if (m_isDying)
        {
            return;
        }

        m_isDying = true;
        m_timeLimitStarted = false;

        if (m_activeEnemyManager != null)
        {
            m_activeEnemyManager.RemoveActiveEnemy(this);
        }

        ChangeState(MINI_GHOST_STATE_TYPE.DYING);
    }

    // 現在のステートタイプを取得する
    public MINI_GHOST_STATE_TYPE GetCurrentStateType()
    {
        return m_currentStateType;
    }

    // 死亡中かどうかを取得する
    public bool GetIsDying()
    {
        return m_isDying;
    }

    // 蹴りの弾の発射位置を取得する
    public Transform GetKickFirePoint()
    {
        return m_kickFirePoint;
    }

    // 投げの弾の発射位置を取得する
    public Transform GetThrowFirePoint()
    {
        return m_throwFirePoint;
    }

    // SplineMoveEnemyの移動更新
    public void UpdateSplineMove()
    {
        base.Update();
    }

    // スプラインのターゲットを取得する
    public SplineContainer GetTargetSpline()
    {
        return m_targetSpline;
    }

    // スポーン位置を取得する
    public Vector3 GetSpawnPosition()
    {
        return m_spawnPosition;
    }

    // プレイヤーの位置を取得する
    public Vector3 GetPlayerPosition()
    {
        return m_playerPosition.position;
    }


    // 移動速度を設定する
    public void SetMoveSpeed(float speed)
    {
        m_moveSpeed = speed;
    }

    // スプラインに合わせて回転するかのフラグを設定する
    public void SetRotateWithSplineFlag(bool flag)
    {
        m_rotateWithSplineFlag = flag;
    }

    // 移動距離を設定する
    public void SetDistanceTraveled(float distance)
    {
        m_distanceTraveled = distance;
    }


    // 制限時間を開始する
    public void StartTimeLimit()
    {
        if (!m_timeLimitStarted && !m_isDying)
        {
            m_timeLimitStarted = true;
        }
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.CompareTag("MagicBullet"))
        {
            BeginDying();
        }
    }
}