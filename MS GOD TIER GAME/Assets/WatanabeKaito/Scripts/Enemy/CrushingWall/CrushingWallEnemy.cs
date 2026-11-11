
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines;

public class CrushingWallEnemy : SplineMoveEnemy
{
    [Header("移動速度グラフ")]
    [SerializeField]
    private AnimationCurve m_speedCurve =
        new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 3f)
        );

    [Header("加速時間")]
    [SerializeField] private float m_speedRampDuration = 10f;

    [Header("被弾時の減速")]
    [SerializeField] private float m_damageSpeedRate = 0.7f;
    [SerializeField] private float m_minSpeedRate = 0.3f;

    [Header("プレイヤーへの攻撃")]
    [SerializeField] private float m_attackDistance = 1.5f;
    [SerializeField] private float m_attackDuration = 1f;

    // 移動速度グラフの経過時間
    private float m_speedElapsedTime = 0f;

    // 被弾による速度倍率
    private float m_damageSpeedMultiplier = 1f;

    // 攻撃中フラグ
    private bool m_isAttackingFlag = false;

    // 攻撃済みフラグ
    private bool m_hasAttackedFlag = false;

    private Coroutine m_attackCoroutine;


    public override void Initialize()
    {
        base.Initialize();
        m_speedElapsedTime = 0f;
        m_damageSpeedMultiplier = 1f;
        m_isAttackingFlag = false;
        m_hasAttackedFlag = false;
        ResetMoveSpeed();

    }

    // 移動処理
    protected override void UpdateMovement()
    {
        // 攻撃中は移動しない
        if (m_isAttackingFlag)
        {
            return;
        }

        // プレイヤーに追いついたか確認
        if (!m_hasAttackedFlag && m_playerPosition != null)
        {
            float distance = Vector3.Distance(transform.position, m_playerPosition.position);

            if (distance <= m_attackDistance)
            {
                m_hasAttackedFlag = true;
                m_attackCoroutine = StartCoroutine(AttackPlayer());

                return;
            }
        }

        // 移動速度を更新
        UpdateMoveSpeed();

        // 基底クラスの移動処理
        base.UpdateMovement();
    }


    // 移動速度の更新
    private void UpdateMoveSpeed()
    {
        m_speedElapsedTime += Time.deltaTime;

        float normalizedTime = 1f;

        if (m_speedRampDuration > 0f)
        {
            normalizedTime = Mathf.Clamp01(m_speedElapsedTime / m_speedRampDuration);
        }

        float graphSpeed = EvaluateSpeed(normalizedTime);

        m_moveSpeed = graphSpeed * m_damageSpeedMultiplier;
    }


    // 速度グラフの評価
    private float EvaluateSpeed(float normalizedTime)
    {
        if (m_speedCurve == null || m_speedCurve.length == 0)
        {
            return 1f;
        }

        return Mathf.Max(0f, m_speedCurve.Evaluate(normalizedTime));
    }


    // 攻撃を受けたときに呼び出す
    public void ApplyDamageSlow()
    {
        m_damageSpeedMultiplier *= m_damageSpeedRate;

        m_damageSpeedMultiplier = Mathf.Max(m_damageSpeedMultiplier, m_minSpeedRate);

        float normalizedTime = 1f;

        if (m_speedRampDuration > 0f)
        {
            normalizedTime =
                Mathf.Clamp01(m_speedElapsedTime / m_speedRampDuration);
        }

        m_moveSpeed =  EvaluateSpeed(normalizedTime) * m_damageSpeedMultiplier;
    }


    // プレイヤーへの攻撃
    private IEnumerator AttackPlayer()
    {
        m_isAttackingFlag = true;

        // 攻撃

        // 攻撃時間
        yield return new WaitForSeconds(m_attackDuration);

        // 初速に戻す
        ResetMoveSpeed();

        // 移動再開
        m_isAttackingFlag = false;

        m_attackCoroutine = null;
    }


    // 移動速度を初期状態に戻す
    private void ResetMoveSpeed()
    {
        m_speedElapsedTime = 0f;
        m_damageSpeedMultiplier = 1f;

        // グラフの最初の速度に戻す
        m_moveSpeed = EvaluateSpeed(0f);
    }
}
