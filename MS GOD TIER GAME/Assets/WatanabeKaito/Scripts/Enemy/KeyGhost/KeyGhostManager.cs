
using UnityEngine;

public class KeyGhostManager : EnemyBase, IEventMagicReceiver
{
    [Header("TargetableObject")]
    [SerializeField] private TargetableObject m_targetableObject;

    [Header("照準を有効にするタイミング")]
    [SerializeField, Range(0f, 1f)]
    private float m_targetableEnableRatio = 0.9f;

    [Header("動かすオブジェクト")]
    [SerializeField] private GameObject m_movableObject;

    [Header("移動先")]
    [SerializeField] private Transform m_moveTarget;

    [Header("ターゲットまでの移動時間")]
    [SerializeField] private float m_moveDuration = 1.5f;

    [Header("上昇後の待機時間")]
    [SerializeField] private float m_stayDuration = 1.0f;

    [Header("視界判定の余白")]
    [SerializeField, Range(0f, 0.49f)]
    private float m_viewMarginX = 0.2f;

    [SerializeField, Range(0f, 0.49f)]
    private float m_viewMarginY = 0.2f;

    [Header("奥行の判定範囲")]
    [SerializeField] private float m_minViewDepth = 1.0f;
    [SerializeField] private float m_maxViewDepth = 10.0f;

    [Header("トイレのふた")]
    [SerializeField] private ToiletLidController m_lidController;


    private Camera m_targetCamera;          // 視界判定用のカメラ
    private Renderer m_visibilityRenderer;  // 視界判定用のRenderer

    private Vector3 m_originalPosition;     // 元の位置
    private Vector3 m_moveStartPosition;    // 移動開始位置
    private Vector3 m_moveEndPosition;      // 移動終了位置
    private Vector3 m_returnStartPosition;  // 戻る際の開始位置

    private float m_moveTimer;  // 移動タイマー
    private float m_stayTimer;  // 待機タイマー

    private bool m_isMovingUp;      // 上昇中かどうか
    private bool m_isLidOpening;    // ふたを開くアニメーション中かどうか

    private bool m_startedBattle = false; // バトル開始フラグ

    private bool m_isTargetableEnabled = false; // ターゲットを有効化したかどうか

    // 現在の状態
    private enum KEY_GHOST_STATE
    {
        HIDE,   // 隠れている
        MOVE,   // 移動中
        STAY,   // 上の位置で待機中
        RETURN  // 元の位置に戻る
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

        // カメラを取得
        if (m_targetCamera == null)
        {
            m_targetCamera = Camera.main;
        }

        // Rendererを取得
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
        m_stayTimer = 0f;

        // 初期状態は隠れている状態
        m_currentState = KEY_GHOST_STATE.HIDE;

        // 上昇方向に設定
        m_isMovingUp = true;

        // ターゲット不可にする
        m_targetableObject.SetTargetable(false);
    }


    public override void BattleStart()
    {
        // バトル開始時の処理
        m_startedBattle = true;
    }


    public override void BattleEnd()
    {
        // バトル終了時の処理
    }


    private void Update()
    {
        if (m_targetCamera == null ||
            m_visibilityRenderer == null ||
            m_moveTarget == null ||
            m_movableObject == null ||
            m_targetableObject == null ||
            !m_startedBattle)
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
                UpdateStaying();
                break;


            case KEY_GHOST_STATE.RETURN:
                UpdateReturning();
                break;
        }
    }


    // カメラの視界内に入っているか判定
    private bool IsInCameraView()
    {
        // ワールド座標をビューポート座標に変換
        Vector3 viewportPosition = m_targetCamera.WorldToViewportPoint(
            m_visibilityRenderer.bounds.center
        );

        // 奥行の判定
        if (viewportPosition.z < m_minViewDepth ||
            viewportPosition.z > m_maxViewDepth)
        {
            return false;
        }

        // カメラの描画距離外なら判定しない
        if (viewportPosition.z < m_targetCamera.nearClipPlane ||
            viewportPosition.z > m_targetCamera.farClipPlane)
        {
            return false;
        }

        // X軸の判定範囲
        float minX = m_viewMarginX;
        float maxX = 1.0f - m_viewMarginX;

        // Y軸の判定範囲
        float minY = m_viewMarginY;
        float maxY = 1.0f - m_viewMarginY;

        // X軸・Y軸それぞれの範囲内にあるか判定
        return viewportPosition.x >= minX &&
               viewportPosition.x <= maxX &&
               viewportPosition.y >= minY &&
               viewportPosition.y <= maxY;
    }


    // 移動開始
    private void StartMoving()
    {
        // 現在位置から移動を開始
        m_moveStartPosition = m_movableObject.transform.localPosition;

        // 上の位置を移動先に設定
        m_moveEndPosition = m_moveTarget.localPosition;

        m_moveTimer = 0f;

        // 上昇方向に設定
        m_isMovingUp = true;

        // ふたを開く
        if (m_lidController != null)
        {
            m_lidController.StartOpening();
        }

        // ふたの開閉アニメーションを有効にする
        m_isLidOpening = true;

        // ターゲット不可にする
        m_targetableObject.SetTargetable(false);

        m_isTargetableEnabled = false;

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

        // ふたを開く
        if (m_lidController != null && m_isLidOpening)
        {
            m_lidController.UpdateRotation(easeT);
        }

        // 移動
        m_movableObject.transform.localPosition = Vector3.Lerp(
            m_moveStartPosition,
            m_moveEndPosition,
            easeT
        );

        // 上昇中、指定した割合に達したらターゲット可能にする
        if (m_isMovingUp && !m_isTargetableEnabled && t >= m_targetableEnableRatio)
        {
            m_targetableObject.SetTargetable(true);
            m_isTargetableEnabled = true;
        }

        // 移動完了
        if (t >= 1f)
        {
            m_movableObject.transform.localPosition = m_moveEndPosition;

            // 上昇が完了した場合
            if (m_isMovingUp)
            {
                // 待機タイマーをリセット
                m_stayTimer = 0f;

                // 待機状態にする
                m_currentState = KEY_GHOST_STATE.STAY;

                m_isLidOpening = false;
            }
            else
            {
                // 下降が完了した場合

                m_isTargetableEnabled = false;

                // 次は上昇する
                m_isMovingUp = true;

                // 現在位置を移動開始位置にする
                m_moveStartPosition = m_movableObject.transform.localPosition;

                // 上の位置を移動先にする
                m_moveEndPosition = m_moveTarget.localPosition;

                // 移動タイマーをリセット
                m_moveTimer = 0f;
            }
        }
    }


    // 上の位置で待機する処理
    private void UpdateStaying()
    {
        m_stayTimer += Time.deltaTime;

        // 待機時間が終了したら下降を開始
        if (m_stayTimer >= m_stayDuration)
        {
            // 現在位置を移動開始位置にする
            m_moveStartPosition = m_movableObject.transform.localPosition;

            // 元の位置を移動先にする
            m_moveEndPosition = m_originalPosition;

            // 移動タイマーをリセット
            m_moveTimer = 0f;

            // 下降方向に設定
            m_isMovingUp = false;

            // ターゲット不可にする
            m_targetableObject.SetTargetable(false);

            // 移動状態にする
            m_currentState = KEY_GHOST_STATE.MOVE;
        }
    }


    // 元の位置へ戻る処理を開始
    private void StartReturning()
    {
        m_returnStartPosition = m_movableObject.transform.localPosition;

        m_moveTimer = 0f;

        // ふたを閉じる
        if (m_lidController != null)
        {
            m_lidController.StartClosing();
        }

        // ふたの開閉アニメーションを終了
        m_isLidOpening = false;

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

        // ふたを閉じる
        if (m_lidController != null)
        {
            m_lidController.UpdateRotation(easeT);
        }

        // 元の位置へ移動
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
            m_stayTimer = 0f;

            // 再び隠れている状態にする
            m_currentState = KEY_GHOST_STATE.HIDE;
        }
    }


    public void OnKeyGhostHit(HitData hitData)
    {
        if (hitData != null)
        {
            MagicData magicData = hitData.GetMagicData;

            if (magicData != null)
            {
                TryReceiveEventMagic(EVENT_MAGIC_TYPE.NONE);
            }
        }
    }


    public bool TryReceiveEventMagic(EVENT_MAGIC_TYPE eventMagicType)
    {
        //if (eventMagicType == EVENT_MAGIC_TYPE.CLOSE)
        {
            Destroy(m_movableObject);
        }

        return false;
    }
}
