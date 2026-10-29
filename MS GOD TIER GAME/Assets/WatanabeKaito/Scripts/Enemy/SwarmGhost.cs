using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class SwarmGhost : MonoBehaviour
{
    [Header("カメラを向く高さの補正")]
    [SerializeField] private float m_cameraHeightOffset = 1.5f;

    [Header("毎フレームプレイヤーに向く角度")]
    [SerializeField] private float m_turnSpeed = 60f;

    [SerializeField]
    private string[] m_chargeStates =
    {
        "Charge1",
        "Charge2",
        "Charge3",
        "Charge4"
    };

    [Header("突撃先")]
    [SerializeField] private Transform m_target;

    [Header("待ち時間（秒）")]
    [SerializeField] private float m_minDelay = 0f;
    [SerializeField] private float m_maxDelay = 4f;

    [Header("移動設定")]
    [SerializeField] private float m_moveSpeed = 5f;

    [Tooltip("カメラの位置を通り過ぎる距離")]
    [SerializeField] private float m_passDistance = 3f;

    [Header("帰還設定")]
    [Tooltip("元の位置まで戻る時間")]
    [SerializeField] private float m_returnDuration = 1.5f;

    [Tooltip("帰還時の弧の大きさ")]
    [SerializeField] private float m_returnArcHeight = 3f;

    [Header("帰還中のアニメーション速度")]
    [SerializeField] private float m_returnAnimationSpeed = 0.5f;

    [Header("縮小にかかる時間（秒）")]
    [SerializeField] private float m_shrinkDuration = 2.0f;

    [Header("パーティクル")]
    [SerializeField] private GameObject m_deathParticle;

    private float m_defaultAnimatorSpeed = 1f; // Animatorの初期再生速度
    private GameObject m_spawnedDeathParticle;  // 生成されたパーティクルの参照

    private Coroutine m_shrinkCoroutine;    // 縮小コルーチンの参照
    private Coroutine m_chargeCoroutine;    // 突撃コルーチンの参照
    private Coroutine m_returnCoroutine;    // 帰還コルーチンの参照

    private Animator m_animator;    // Animatorの参照

    private bool m_isDying = false; // 死亡中かどうかのフラグ

    private Vector3 m_initialPosition; // 初期位置を保存

    // 突撃中フラグ
    public bool m_isChargingFlag = false;

    private void Awake()
    {
        m_initialPosition = transform.position;

        m_animator = GetComponentInChildren<Animator>();

        if (m_target == null && Camera.main != null)
        {
            m_target = Camera.main.transform;
        }

        if (m_animator != null)
        {
            m_defaultAnimatorSpeed = m_animator.speed;
        }

        TurnToPlayerImmediately();
    }

    private void Update()
    {
        // Eキーで突撃
        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            StartCharge();
        }

        if(!m_isChargingFlag)
        {
           TurnToPlayer();
        }
    }

    // プレイヤーの方向へ即座に向く
    private void TurnToPlayerImmediately()
    {
        Vector3 playerPosition = m_target.position;

        // プレイヤーの狙う位置を調整
        playerPosition.y -= m_cameraHeightOffset;

        Vector3 direction = playerPosition - transform.position;

        // 方向ベクトルがほぼゼロなら回転しない
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        // プレイヤーの方向へ即座に回転
        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    // プレイヤーの方向を向く
    private void TurnToPlayer()
    {
        Vector3 playerPosition = m_target.position;

        // プレイヤーの狙う位置を調整
        playerPosition.y -= m_cameraHeightOffset;

        Vector3 direction = playerPosition - transform.position;

        // 方向ベクトルがほぼゼロなら回転しない
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        // プレイヤーの方向を向く回転
        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        // 毎フレーム少しずつ回転する
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            m_turnSpeed * Time.deltaTime
        );
    }

    // 突撃を開始する
    private void StartCharge()
    {
        // 死亡中や突撃中の重複呼び出しを防ぐ
        if (m_isDying || m_chargeCoroutine != null)
        {
            return;
        }

        if (m_animator == null ||
            m_target == null ||
            m_chargeStates == null ||
            m_chargeStates.Length == 0 ||
            m_moveSpeed <= 0f)
        {
            return;
        }

        m_chargeCoroutine = StartCoroutine(ChargeRoutine());
    }

    // 突撃から帰還までの処理
    private IEnumerator ChargeRoutine()
    {
        m_isChargingFlag = true;

        // 待ち時間とアニメーションをランダムに選ぶ
        float delay = Random.Range(m_minDelay, m_maxDelay);
        int index = Random.Range(0, m_chargeStates.Length);

        yield return new WaitForSeconds(delay);

        if (m_isDying || m_target == null)
        {
            m_chargeCoroutine = null;
            yield break;
        }

        // ターゲットの位置を取得
        Vector3 targetPosition = m_target.position;

        // お化けからターゲットに向かう方向
        Vector3 direction = targetPosition - transform.position;

        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();
        }
        else
        {
            direction = transform.forward;
        }

        // ターゲットを通り過ぎた位置
        Vector3 passPosition = targetPosition + direction * m_passDistance;

        // 突撃アニメーションを再生
        m_animator.CrossFadeInFixedTime(
            $"Base Layer.{m_chargeStates[index]}",
            0.3f,
            0,
            0f
        );

        // アニメーションの再生を少し待つ
        yield return new WaitForSeconds(0.5f);

        if (m_isDying)
        {
            m_chargeCoroutine = null;
            yield break;
        }

        // ターゲットを通り過ぎるまで直線移動
        while (Vector3.Distance(transform.position, passPosition) > 0.01f)
        {
            if (m_isDying)
            {
                m_chargeCoroutine = null;
                yield break;
            }

            transform.position = Vector3.MoveTowards(transform.position, passPosition, m_moveSpeed * Time.deltaTime);

            yield return null;
        }

        // 通過地点に正確に合わせる
        transform.position = passPosition;

        // 帰還開始時にアニメーション速度を変更
        m_animator.speed = m_returnAnimationSpeed;

        // 選択したアニメーションに応じた方向へ帰還
        m_returnCoroutine = StartCoroutine(ReturnToInitialPositionCoroutine(passPosition, m_initialPosition, index));

        yield return m_returnCoroutine;

        m_returnCoroutine = null;

        // 死亡していなければ速度を戻してIdleへ移行
        if (!m_isDying && m_animator != null)
        {
            m_animator.speed = m_defaultAnimatorSpeed;

            m_animator.CrossFadeInFixedTime(
                "Base Layer.idle",
                0.6f,
                0,
                0f
            );
        }

        m_chargeCoroutine = null;
    }

    // 放物線を描きながら一回転して元の位置に戻る
    private IEnumerator ReturnToInitialPositionCoroutine(Vector3 startPosition, Vector3 targetPosition, int chargeIndex)
    {
        if (m_returnDuration <= 0f)
        {
            transform.position = targetPosition;
            yield break;
        }

        // アニメーションに応じた帰還方向
        Vector3 arcDirection = GetReturnArcDirection(chargeIndex);

        // アニメーションに応じた回転軸と回転方向
        GetReturnRotationSettings(chargeIndex, out Vector3 rotationAxis, out float rotationSign);

        // 回転開始時の向きを保存
        Quaternion startRotation = transform.rotation;

        float elapsedTime = 0f;

        while (elapsedTime < m_returnDuration)
        {
            if (m_isDying)
            {
                yield break;
            }

            elapsedTime += Time.deltaTime;

            float t = Mathf.Clamp01(elapsedTime / m_returnDuration);

            // 移動を滑らかにするイージング
            float easedT = Easing.EaseInOutSine(t);

            // 始点から終点までの基本位置
            Vector3 position = Vector3.Lerp(
                startPosition,
                targetPosition,
                easedT
            );

            // 放物線の弧を計算
            float arcHeight = 4f * m_returnArcHeight * easedT * (1f - easedT);

            // 指定された方向に放物線を描く
            position += arcDirection * arcHeight;

            transform.position = position;

            // 帰還中に360度、一回転させる
            float rotationAngle = 360f * rotationSign * t;

            transform.rotation = Quaternion.AngleAxis(rotationAngle, rotationAxis) * startRotation;

            yield return null;
        }

        // 最終位置と回転を正確に戻す
        transform.position = targetPosition;
        transform.rotation = startRotation;

        m_isChargingFlag = false;
    }

    // アニメーション番号に対応する帰還方向を取得
    private Vector3 GetReturnArcDirection(int chargeIndex)
    {
        // 画面の上下左右をカメラの向きから取得
        Transform viewTransform = null;

        if (Camera.main != null)
        {
            viewTransform = Camera.main.transform;
        }
        else if (m_target != null)
        {
            viewTransform = m_target;
        }

        Vector3 up = Vector3.up;
        Vector3 right = Vector3.right;

        if (viewTransform != null)
        {
            up = viewTransform.up;
            right = viewTransform.right;
        }

        // m_chargeStatesの配列順に帰還方向を設定
        switch (chargeIndex)
        {
            // Charge1：上方向
            case 0:
                return up.normalized;

            // Charge2：下方向
            case 1:
                return -up.normalized;

            // Charge3：右斜め上方向
            case 2:
                return (-right + up).normalized;

            // Charge4：左斜め上方向
            case 3:
                
                return (right + up).normalized;

            // その他：上方向
            default:
                return up.normalized;
        }
    }

    // アニメーションに応じた回転軸と回転方向を取得
    private void GetReturnRotationSettings(int chargeIndex, out Vector3 rotationAxis, out float rotationSign)
    {
        Transform viewTransform = null;

        if (Camera.main != null)
        {
            viewTransform = Camera.main.transform;
        }
        else if (m_target != null)
        {
            viewTransform = m_target;
        }

        Vector3 cameraRight = Vector3.right;
        Vector3 cameraUp = Vector3.up;

        if (viewTransform != null)
        {
            cameraRight = viewTransform.right;
            cameraUp = viewTransform.up;
        }

        switch (chargeIndex)
        {
            // Charge1：上方向の軌道
            case 0:
                rotationAxis = cameraRight;
                rotationSign = 1f;
                break;

            // Charge2：下方向の軌道
            case 1:
                rotationAxis = cameraRight;
                rotationSign = -1f;
                break;

            // Charge3：右斜め上方向の軌道
            case 2:
                rotationAxis = cameraUp;
                rotationSign = 1f;
                break;

            // Charge4：左斜め上方向の軌道
            case 3:
                rotationAxis = cameraUp;
                rotationSign = -1f;
                break;

            default:
                rotationAxis = cameraRight;
                rotationSign = 1f;
                break;
        }

        rotationAxis.Normalize();
    }

    public void SetTarget(Transform target)
    {
        m_target = target;
    }

    public void Die()
    {
        if (m_isDying)
        {
            return;
        }

        m_isDying = true;

        // 移動中や帰還中のコルーチンを停止
        if (m_chargeCoroutine != null)
        {
            StopCoroutine(m_chargeCoroutine);
            m_chargeCoroutine = null;
        }

        if (m_returnCoroutine != null)
        {
            StopCoroutine(m_returnCoroutine);
            m_returnCoroutine = null;
        }

        // Animatorの速度を戻してから死亡アニメーションを再生
        if (m_animator != null)
        {
            m_animator.speed = m_defaultAnimatorSpeed;
            m_animator.SetTrigger("bDead");
        }

        // 縮小コルーチンを開始
        if (m_shrinkCoroutine != null)
        {
            StopCoroutine(m_shrinkCoroutine);
        }

        m_shrinkCoroutine = StartCoroutine(ShrinkCoroutine());

        // パーティクルを再生
        PlayDeathEffect(transform.position);
    }

    // 縮小して消滅する
    private IEnumerator ShrinkCoroutine()
    {
        Vector3 originalScale = transform.localScale;
        float elapsed = 0f;

        if (m_shrinkDuration <= 0f)
        {
            transform.localScale = Vector3.zero;

            Destroy(m_spawnedDeathParticle);
            Destroy(gameObject);

            yield break;
        }

        while (elapsed < m_shrinkDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / m_shrinkDuration
            );

            float easedT = Easing.EaseInSine(t);

            transform.localScale = Vector3.Lerp(
                originalScale,
                Vector3.zero,
                easedT
            );

            yield return null;
        }

        transform.localScale = Vector3.zero;

        Destroy(m_spawnedDeathParticle);
        Destroy(gameObject);
    }

    // パーティクルを再生する
    private void PlayDeathEffect(Vector3 position)
    {
        if (m_deathParticle == null)
        {
            return;
        }

        m_spawnedDeathParticle = Instantiate(
            m_deathParticle,
            position,
            Quaternion.identity
        );

        // パーティクルのサイズを半分にする
        m_spawnedDeathParticle.transform.localScale *= 0.5f;
    }
}