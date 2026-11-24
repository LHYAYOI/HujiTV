
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines;

public class CrushingWallEnemy : SplineMoveEnemy
{
    [Header("アニメーター")]
    [SerializeField] private Animator m_animator;

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

    [Header("攻撃終了からプレイヤー移動開始までの時間")]
    [SerializeField] private float m_attackToPlayerMoveDelay = 2f;

    [Header("プレイヤー移動開始から自身の移動開始までの時間")]
    [SerializeField] private float m_playerMoveToSelfMoveDelay = 1f;

    [Header("プレイヤーの移動再開時の速度")]
    [SerializeField] private float m_playerMoveSpeed = 5f;

    [Header("Targetable")]
    [SerializeField] private List<TargetableObject> m_targetableObjects = new List<TargetableObject>();

    [Header("LookTarget")]
    [SerializeField] private GameObject m_lookTarget;

    private float m_speedElapsedTime = 0f; // 移動速度グラフの経過時間

    
    private float m_damageSpeedMultiplier = 1f; // 被弾による速度倍率

    
    private bool m_isAttackingFlag = false; // 攻撃中フラグ

    private bool m_battleStartFlag = false; // 戦闘開始フラグ
    private bool m_battleEndFlag = false; // 戦闘終了フラグ


    public override void Initialize()
    {
        base.Initialize();
        m_speedElapsedTime = 0f;
        m_damageSpeedMultiplier = 1f;
        m_isAttackingFlag = false;

        m_animator.SetFloat("fSpeed", 0f);

        // スプラインの始点に移動
        if (m_targetSpline != null)
        {
            m_distanceTraveled = 0f;
            Vector3 startPosition = m_targetSpline.EvaluatePosition(0f);
            transform.position = startPosition;
        }

        m_lookTarget.SetActive(false);

        DeactivateAllTargetableObjects();

        ResetMoveSpeed();

    }

    public override void BattleStart()
    {
        base.BattleStart();
        m_speedElapsedTime = 0f;
        m_damageSpeedMultiplier = 1f;
        m_isAttackingFlag = false;

        m_animator.SetFloat("fSpeed", 1f);

        m_lookTarget.SetActive(true);

        ActivateAllTargetableObjects();

        ResetMoveSpeed();

        m_battleStartFlag = true;
    }

    public override void BattleEnd()
    {
        base.BattleEnd();

        DeactivateAllTargetableObjects();

        m_battleEndFlag = true;
    }

    // 移動処理
    protected override void UpdateMovement()
    {
        // 攻撃中とバトル開始前は移動しない
        if (m_isAttackingFlag || !m_battleStartFlag)
        {
            return;
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
            normalizedTime = Mathf.Clamp01(m_speedElapsedTime / m_speedRampDuration);
        }

        m_moveSpeed =  EvaluateSpeed(normalizedTime) * m_damageSpeedMultiplier;
    }

    // 移動速度を初期状態に戻す
    private void ResetMoveSpeed()
    {
        m_speedElapsedTime = 0f;
        m_damageSpeedMultiplier = 1f;

        // グラフの最初の速度に戻す
        m_moveSpeed = EvaluateSpeed(0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if(m_battleEndFlag || !m_battleStartFlag)
        {
            return;
        }

        CartController cartController = other.GetComponent<CartController>();

        if (cartController != null)
        {
            StartCoroutine(AttackCoroutine(cartController));
        }

    }


    // 攻撃のコルーチン
    public IEnumerator AttackCoroutine(CartController cartController)
    {
        // TargetableObject全てを非アクティブにする
        DeactivateAllTargetableObjects();

        // お互いに移動を止める
        cartController.SetSpeed(0f);
        m_isAttackingFlag = true;

        // 攻撃アニメーション
        m_animator.SetTrigger("tAttack");

        // 攻撃終了後、プレイヤーの移動を再開するまでの待機
        yield return new WaitForSeconds(m_attackToPlayerMoveDelay);

        // プレイヤーの移動を再開
        cartController.SetSpeed(m_playerMoveSpeed);

        // プレイヤーの移動再開後、自身の移動を再開するまでの待機
        yield return new WaitForSeconds(m_playerMoveToSelfMoveDelay);

        // 自身の移動速度を初期状態に戻す
        ResetMoveSpeed();

        // 自身の移動を再開
        m_isAttackingFlag = false;

        // TargetableObject全てをアクティブにする
        ActivateAllTargetableObjects();
    }

    // TargetableObject全てをアクティブにする
    public void ActivateAllTargetableObjects()
    {
        foreach (var targetable in m_targetableObjects)
        {
            targetable.SetTargetable(true);
        }
    }

    // TargetableObject全てを非アクティブにする
    public void DeactivateAllTargetableObjects()
    {
        foreach (var targetable in m_targetableObjects)
        {
            targetable.SetTargetable(false);
        }
    }
}
