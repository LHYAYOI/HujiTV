using UnityEngine;

public class KeyGhostManager : EnemyBase
{
    [Header("TargetableObject")]
    [SerializeField] private TargetableObject m_targetableObject;

    [Header("動かすオブジェクト")]
    [SerializeField] private GameObject m_movableObject;

    [Header("移動先")]
    [SerializeField] private Transform m_moveTarget;

    [Header("隠れている時間")]
    [SerializeField] private float m_hiddenDuration = 3.0f;

    [Header("ターゲットまでの移動時間")]
    [SerializeField] private float m_moveDuration = 2.0f;

    [Header("ターゲット位置での待機時間")]
    [SerializeField] private float m_stayDuration = 3.0f;


    private Vector3 m_originalPosition;    // 元の位置
    private Vector3 m_moveStartPosition;   // 移動開始位置
    private Vector3 m_moveEndPosition;     // 移動終了位置
    private Vector3 m_returnStartPosition; // 戻るときの開始位置

    private float m_timer;      // タイマー
    private float m_moveTimer;  // 移動タイマー


    // 現在の状態
    private enum KEY_GHOST_STATE
    {
        HIDE,  // 隠れている
        MOVE,  // 移動中
        STAY,  // ターゲット位置で待機中
        RETURN // 元の位置に戻る
    }

    private KEY_GHOST_STATE m_currentState;

    private void Start()
    {
        Initialize();
    }

    public override void Initialize()
    {
        if (m_movableObject == null ||
            m_moveTarget == null ||
            m_targetableObject == null)
        {
            return;
        }

        // 元の位置を保存
        m_originalPosition = m_movableObject.transform.localPosition;

        // タイマーを初期化
        m_timer = 0f;
        m_moveTimer = 0f;

        // 初期状態を隠れている状態にする
        m_currentState = KEY_GHOST_STATE.HIDE;

        // ターゲット不可にする
        m_targetableObject.SetTargetable(false);
    }

    private void Update()
    {
        if (m_moveTarget == null ||
            m_movableObject == null ||
            m_targetableObject == null)
        {
            return;
        }

        switch (m_currentState)
        {
            case KEY_GHOST_STATE.HIDE:
                UpdateHidden();
                break;

            case KEY_GHOST_STATE.MOVE:
                UpdateMoving();
                break;

            case KEY_GHOST_STATE.STAY:
                UpdateStaying();
                break;

            case KEY_GHOST_STATE.RETURN:
                UpdateReturning();
                break;
        }
    }

    // 隠れている状態の処理
    private void UpdateHidden()
    {
        m_timer += Time.deltaTime;

        if (m_timer >= m_hiddenDuration)
        {
            m_timer = 0f;
            m_moveTimer = 0f;

            // オブジェクトの初期位置から移動する
            m_moveStartPosition = m_originalPosition;

            // 移動開始時のターゲット位置を保存
            m_moveEndPosition = m_moveTarget.localPosition;

            m_currentState = KEY_GHOST_STATE.MOVE;
        }
    }

    // 移動中の処理
    private void UpdateMoving()
    {
        m_moveTimer += Time.deltaTime;

        float t;

        if (m_moveDuration <= 0f)
        {
            t = 1f;
        }
        else
        {
            t = Mathf.Clamp01(m_moveTimer / m_moveDuration);
        }

        float easeT = Easing.EaseOutSine(t);

        m_movableObject.transform.localPosition = Vector3.Lerp(
            m_moveStartPosition,
            m_moveEndPosition,
            easeT
        );

        // 移動完了
        if (t >= 1f)
        {
            m_movableObject.transform.localPosition = m_moveEndPosition;

            m_timer = 0f;

            // ターゲット可能にする
            m_targetableObject.SetTargetable(true);

            m_currentState = KEY_GHOST_STATE.STAY;
        }
    }

    // ターゲット位置で待機しているときの処理
    private void UpdateStaying()
    {
        m_timer += Time.deltaTime;

        if (m_timer >= m_stayDuration)
        {
            // 戻り始めの位置を保存
            m_returnStartPosition = m_movableObject.transform.localPosition;

            // タイマーをリセット
            m_timer = 0f;
            m_moveTimer = 0f;

            // ターゲット不可にする
            m_targetableObject.SetTargetable(false);

            // 戻る状態に変更
            m_currentState = KEY_GHOST_STATE.RETURN;
        }
    }

    // 元の位置へ戻る処理
    private void UpdateReturning()
    {
        m_moveTimer += Time.deltaTime;

        float t;

        if (m_moveDuration <= 0f)
        {
            t = 1f;
        }
        else
        {
            t = Mathf.Clamp01(m_moveTimer / m_moveDuration);
        }

        float easeT = Easing.EaseOutSine(t);

        m_movableObject.transform.localPosition = Vector3.Lerp(
            m_returnStartPosition,
            m_originalPosition,
            easeT
        );

        // 戻り完了
        if (t >= 1f)
        {
            m_movableObject.transform.localPosition = m_originalPosition;

            m_timer = 0f;
            m_moveTimer = 0f;

            // 再び隠れている状態にする
            m_currentState = KEY_GHOST_STATE.HIDE;
        }
    }
}