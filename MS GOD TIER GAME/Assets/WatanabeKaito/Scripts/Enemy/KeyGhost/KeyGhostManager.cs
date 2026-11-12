using UnityEngine;

public class KeyGhostManager : EnemyBase
{
    [Header("TargetableObject")]
    [SerializeField] private TargetableObject m_targetableObject;

    [Header("動かすオブジェクト")]
    [SerializeField] private GameObject m_movableObject;

    [Header("移動先")]
    [SerializeField] private Transform m_moveTarget;

    [Header("ターゲットまでの移動時間")]
    [SerializeField] private float m_moveDuration = 2.0f;

    private Camera m_targetCamera;  // 視界判定用のカメラ

    private Renderer m_visibilityRenderer;  // 視界判定用のRenderer

    private Vector3 m_originalPosition;     // 元の位置を保存する変数
    private Vector3 m_moveStartPosition;    // 移動開始位置
    private Vector3 m_moveEndPosition;      // 移動終了位置
    private Vector3 m_returnStartPosition;  // 元の位置に戻る際の開始位置

    private float m_moveTimer;  // 移動時間の計測用タイマー

    private Plane[] m_frustumPlanes = new Plane[6]; // カメラの視界判定用


    // 現在の状態
    private enum KEY_GHOST_STATE
    {
        HIDE,  // 隠れている
        MOVE,  // 移動中
        STAY,  // ターゲット位置で待機中
        RETURN // 元の位置に戻る
    }

    private KEY_GHOST_STATE m_currentState; // 現在の状態を保持する変数


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

        // カメラを取得
        if (m_targetCamera == null)
        {
            m_targetCamera = Camera.main;
        }

        // Rendererが未設定なら動かすオブジェクトから取得
        if (m_visibilityRenderer == null)
        {
            m_visibilityRenderer = m_movableObject.GetComponentInChildren<Renderer>();
        }

        if (m_targetCamera == null || m_visibilityRenderer == null)
        {
            return;
        }

        // 元の位置を保存
        m_originalPosition = m_movableObject.transform.localPosition;

        m_moveTimer = 0f;

        // 初期状態は隠れている状態
        m_currentState = KEY_GHOST_STATE.HIDE;

        // ターゲット不可にする
        m_targetableObject.SetTargetable(false);
    }


    private void Update()
    {
        if (m_targetCamera == null ||
            m_visibilityRenderer == null ||
            m_moveTarget == null ||
            m_movableObject == null ||
            m_targetableObject == null)
        {
            return;
        }

        // カメラの視界内に入っているか判定
        bool isInCameraView = IsInCameraView();

        // 状態の切り替え
        switch (m_currentState)
        {
            case KEY_GHOST_STATE.HIDE:

                // 視界に入ったら出現
                if (isInCameraView)
                {
                    StartMoving();
                }

                break;

            case KEY_GHOST_STATE.MOVE:

            case KEY_GHOST_STATE.STAY:

                // 視界から外れたら隠れる
                if (!isInCameraView)
                {
                    StartReturning();
                }

                break;

            case KEY_GHOST_STATE.RETURN:

                // 戻っている途中で再び視界に入ったら出現
                if (isInCameraView)
                {
                    StartMoving();
                }

                break;
        }

        // 現在の状態に応じた処理
        switch (m_currentState)
        {
            case KEY_GHOST_STATE.MOVE:
                UpdateMoving();
                break;

            case KEY_GHOST_STATE.STAY:
                // 視界から外れるまで待機
                break;

            case KEY_GHOST_STATE.RETURN:
                UpdateReturning();
                break;
        }
    }


    // カメラの視界内に入っているか判定
    private bool IsInCameraView()
    {
        GeometryUtility.CalculateFrustumPlanes(
            m_targetCamera,
            m_frustumPlanes
        );

        return GeometryUtility.TestPlanesAABB(
            m_frustumPlanes,
            m_visibilityRenderer.bounds
        );
    }


    // 移動開始
    private void StartMoving()
    {
        // 現在位置から移動を開始
        m_moveStartPosition = m_movableObject.transform.localPosition;

        // ターゲット位置を保存
        m_moveEndPosition = m_moveTarget.localPosition;

        m_moveTimer = 0f;

        // ターゲット不可にする
        m_targetableObject.SetTargetable(false);

        m_currentState = KEY_GHOST_STATE.MOVE;
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

            // ターゲット可能にする
            m_targetableObject.SetTargetable(true);

            m_currentState = KEY_GHOST_STATE.STAY;
        }
    }


    // 元の位置へ戻る処理を開始
    private void StartReturning()
    {
        m_returnStartPosition = m_movableObject.transform.localPosition;

        m_moveTimer = 0f;

        // ターゲット不可にする
        m_targetableObject.SetTargetable(false);

        m_currentState = KEY_GHOST_STATE.RETURN;
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

            m_moveTimer = 0f;

            // 再び隠れている状態にする
            m_currentState = KEY_GHOST_STATE.HIDE;
        }
    }
}