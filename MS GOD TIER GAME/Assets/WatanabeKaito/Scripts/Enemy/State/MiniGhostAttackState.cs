using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class MiniGhostAttackState : MiniGhostState
{

    [Header("弾のプレハブリスト")]
    [SerializeField] private List<GameObject> m_bulletPrefabs = new List<GameObject>();

    [Header("弾の速度")]
    [SerializeField] private float m_bulletSpeed = 10f;

    [Header("弾の発射角度（度数）")]
    [SerializeField] private float m_bulletAngle = 45f;

    [Header("照準位置の補正")]
    [SerializeField] private float m_targetHeightOffset = -0.1f;

    [Header("高さの差に応じた補正率")]
    [SerializeField] private float m_heightCorrectionRate = 0.1f;

    [Header("蹴りの弾を生成してから発射するまでの時間（秒）")]
    [SerializeField] private float m_kickBulletDelay = 2f;

    [Header("投げの弾を生成してから発射するまでの時間（秒）")]
    [SerializeField] private float m_throwBulletDelay = 1.7f;

    [Header("発射後から移動までの待ち時間（秒）")]
    [SerializeField] private float m_postShootDelay = 2.0f;

    [Header("振り向きにかかる時間（秒）")]
    [SerializeField] private float m_turnDuration = 1f;

    private Animator m_animator;    // Animatorコンポーネント
    private Quaternion m_originalRotation;  // 元の向きを保持する変数
    private Coroutine m_attackCoroutine;    // 実行中の攻撃コルーチン
    private readonly List<GameObject> m_spawnedBullets = new List<GameObject>();    // 生成された弾のリスト

    public override void InitializeState(MiniGhostEnemy owner)
    {
        base.InitializeState(owner);
        m_animator = owner.GetComponentInChildren<Animator>();
    }

    public override void EnterState()
    {
        if (m_owner.GetIsDying())
        {
            return;
        }

        if (m_bulletPrefabs == null || m_bulletPrefabs.Count == 0)
        {
            m_owner.ChangeState(MINI_GHOST_STATE_TYPE.MOVE);
            return;
        }

        m_attackCoroutine = StartCoroutine(AttackRoutine());
    }

    public override void ExitState()
    {
        // 状態を抜けた場合は攻撃のコルーチンを停止
        if (m_attackCoroutine != null)
        {
            StopCoroutine(m_attackCoroutine);
            m_attackCoroutine = null;
        }

        // 死亡した場合は生成した弾を削除
        if (m_owner != null && m_owner.GetIsDying())
        {
            DestroySpawnedBullets();
        }
    }

    // 攻撃のコルーチン
    private IEnumerator AttackRoutine()
    {
        bool isKick = Random.Range(0, 2) == 0;
        m_originalRotation = m_owner.transform.rotation;

        yield return TurnRoutine(true);
        if (m_owner.GetIsDying())
        {
            yield break;
        }

        if (m_animator != null)
        {
            if (isKick)
            {
                m_animator.SetTrigger("tKick");
            }
            else
            {
                m_animator.SetTrigger("tThrow");
            }
        }

        Transform firePoint;

        if (isKick)
        {
            firePoint = m_owner.GetKickFirePoint();
        }
        else
        {
            firePoint = m_owner.GetThrowFirePoint();
        }

        if (firePoint == null)
        {
            FinishAttack();
            yield break;
        }

        GameObject bulletPrefab = GetRandomBulletPrefab();
        if (bulletPrefab == null)
        {
            FinishAttack();
            yield break;
        }

        GameObject bulletObject = null;

        if (isKick)
        {
            // 蹴りでは先に弾を生成し、指定時間後に発射方向を設定する。
            bulletObject = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
            m_spawnedBullets.Add(bulletObject);
            yield return new WaitForSeconds(m_kickBulletDelay);
        }
        else
        {
            // 投げでは指定時間待ってから弾を生成する。
            yield return new WaitForSeconds(m_throwBulletDelay);
            if (m_owner.GetIsDying())
            {
                yield break;
            }

            bulletObject = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
            m_spawnedBullets.Add(bulletObject);
        }

        if (m_owner.GetIsDying() || bulletObject == null)
        {
            yield break;
        }

        Vector3 spawnPosition = firePoint.position;
        Vector3 targetPosition = m_owner.GetPlayerPosition();

        MiniGhostEnemyBullet bullet = bulletObject.GetComponent<MiniGhostEnemyBullet>();
        if (bullet != null)
        {
            Vector3 correctedTargetPosition = GetCorrectedTargetPosition(targetPosition, spawnPosition);

            bullet.SetTarget(correctedTargetPosition, m_bulletSpeed, m_bulletAngle);
        }

        yield return new WaitForSeconds(m_postShootDelay);
        if (m_owner.GetIsDying())
        {
            yield break;
        }

        yield return TurnRoutine(false);
        if (m_owner.GetIsDying())
        {
            yield break;
        }

        FinishAttack();
    }

    // 攻撃終了処理
    private void FinishAttack()
    {
        m_attackCoroutine = null;

        if (m_owner != null && !m_owner.GetIsDying())
        {
            m_owner.ChangeState(MINI_GHOST_STATE_TYPE.MOVE);
        }
    }

    // ランダムに弾のプレハブを取得する
    private GameObject GetRandomBulletPrefab()
    {
        if (m_bulletPrefabs == null || m_bulletPrefabs.Count == 0)
        {
            return null;
        }

        int randomIndex = Random.Range(0, m_bulletPrefabs.Count);
        return m_bulletPrefabs[randomIndex];
    }

    // プレイヤーの方向に振り向くコルーチン
    private IEnumerator TurnRoutine(bool turnToPlayer)
    {
        Vector3 playerPosition = m_owner.GetPlayerPosition();
        Quaternion startRotation = m_owner.transform.rotation;
        float elapsed = 0f;

        while (elapsed < m_turnDuration)
        {
            if (m_owner.GetIsDying())
            {
                yield break;
            }

            float t;
            if (m_turnDuration <= 0f)
            {
                t = 1f;
            }
            else
            {
                t = Mathf.Clamp01(elapsed / m_turnDuration);
            }

            float easedT;
            if (turnToPlayer)
            {
                easedT = Easing.EaseOutSine(t);
            }
            else
            {
                easedT = Easing.EaseInSine(t);
            }

            Quaternion targetRotation;
            if (turnToPlayer)
            {
                Vector3 direction = playerPosition - m_owner.transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    targetRotation = Quaternion.LookRotation(-direction.normalized);
                }
                else
                {
                    targetRotation = m_owner.transform.rotation;
                }
            }
            else
            {
                targetRotation = m_originalRotation;
            }

            m_owner.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, easedT);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (turnToPlayer)
        {
            Vector3 direction = playerPosition - m_owner.transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                m_owner.transform.rotation = Quaternion.LookRotation(-direction.normalized);
            }
        }
        else
        {
            m_owner.transform.rotation = m_originalRotation;
        }
    }

    // 目標位置の高さを補正する
    private Vector3 GetCorrectedTargetPosition(Vector3 targetPosition, Vector3 spawnPosition)
    {
        // 発射位置から見た目標位置の高さの差
        float heightDifference = targetPosition.y - spawnPosition.y;

        // 高さの差に応じて補正する
        float correction = m_targetHeightOffset + heightDifference * m_heightCorrectionRate;

        targetPosition.y += correction;

        return targetPosition;
    }

    // 生成した弾を削除する
    private void DestroySpawnedBullets()
    {
        foreach (GameObject bullet in m_spawnedBullets)
        {
            if (bullet != null)
            {
                Destroy(bullet);
            }
        }

        m_spawnedBullets.Clear();
    }

}
