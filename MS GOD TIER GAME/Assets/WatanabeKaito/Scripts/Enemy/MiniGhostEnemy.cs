using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class MiniGhostEnemy : SplineMoveEnemy
{
    [Header("弾のプレハブリスト")]
    [SerializeField] private List<GameObject> m_bulletPrefabs;

    [Header("弾の発射位置")]
    [SerializeField] private Transform m_firePoint;

    [Header("弾の発射間隔（秒）")]
    [SerializeField] private float m_fireInterval = 2.0f;

    [Header("弾の速度")]
    [SerializeField] private float m_bulletSpeed = 10.0f;

    [Header("弾の発射角度（度数）")]
    [SerializeField] private float m_bulletAngle = 45.0f;

    [Header("弾が生成されてから発射されるまでの時間（秒）")]
    [SerializeField] private float m_bulletDelay = 1.0f;

    [Header("振りむくまでにかかる時間（秒）")]
    [SerializeField] private float m_turnDuration = 1.0f;

    [Header("スプラインまでの移動時間")]
    [SerializeField] private float m_spawnDuration = 3.0f;

    private Quaternion m_originalRotation;  // 元の向きを保持する変数
    bool m_isShooting = false; // 射撃中かどうかのフラグ

    private float m_fireTimer = 0f; // 弾の発射タイマー

    private bool m_isSpawningFlag = false; // 現在出現中かどうかのフラグ
    private Vector3 m_spawnTargetPos;  // 出現目標地点（スプラインの始点）
    private float m_spawnTimer = 0f;   // 出現用のタイマー

    public override void Initialize()
    {
        base.Initialize();
        StartSpawn();
        m_fireTimer = 0f;
    }

    protected override void Update()
    {
        // 弾の発射タイマーを更新
        UpdateShooting();

        // 出現中は射撃も移動も行わない
        if (m_isSpawningFlag)
        {
            UpdateSpawning();
            return;
        }

        // 射撃中、は移動処理を行わない
        if (m_isShooting) return;

        // 親クラスの移動処理を更新
        base.Update();
    }

    private void StartSpawn()
    {
        if (m_targetSpline != null)
        {
            // スプラインの始点座標を取得
            m_spawnTargetPos = m_targetSpline.EvaluatePosition(0f);
          
            // 向きの調整
            Vector3 initialTangent = m_targetSpline.EvaluateTangent(0f);
            if (initialTangent != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(initialTangent);
            }

            m_spawnTimer = 0f;
            m_isSpawningFlag = true; // 出現中フラグをON
        }
    }

    // 壁から出てくる処理
    private void UpdateSpawning()
    {
        m_spawnTimer += Time.deltaTime;

        // 1.0を超えないようにクランプ（安全対策）
        float t = Mathf.Clamp01(m_spawnTimer / m_spawnDuration);

        // 自作の EaseInSine を適用
        float easeT = Easing.EaseOutSine(t);

        // 線形補間で座標を更新
        transform.position = Vector3.Lerp(m_spawnPosition, m_spawnTargetPos, easeT);

        // 出現完了判定
        if (t >= 1.0f)
        {
            transform.position = m_spawnTargetPos;
            m_isSpawningFlag = false;

            // ここからスプライン移動を開始
            m_distanceTraveled = 0f;
            m_moveDirection = 1f;
        }
    }

    // 射撃タイマー処理
    private void UpdateShooting()
    {
        if (m_bulletPrefabs == null) return;

        m_fireTimer += Time.deltaTime;
        if (m_fireTimer >= m_fireInterval)
        {
            if (!m_isShooting)
            {
                StartCoroutine(ShootCoroutine());
            }
        }
    }

    // 弾の生成と発射処理
    private IEnumerator ShootCoroutine()
    {
        m_isShooting = true;

        m_originalRotation = transform.rotation;

        // プレイヤーの方向を向く
        yield return StartCoroutine(TurnRoutine(true));

        // 発射口の位置を取得
        Vector3 spawnPosition = m_firePoint.position;

        // ランダムに弾のプレハブを選択
        GameObject bulletPrefab = GetRandomBulletPrefab();

        // 弾を生成
        GameObject bulletObj = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);

        // 少し待ってから方向を設定する
        yield return new WaitForSeconds(m_bulletDelay);

        if (bulletObj == null || m_firePoint == null) yield break;

        // ターゲットへの方向を計算（ターゲットが無い場合は進行方向へ飛ばす）
        Vector3 direction = transform.forward;
        Vector3 targetPosition = m_playerPosition.transform.position;
        if (m_playerPosition != null)
        {
            direction = (targetPosition - spawnPosition).normalized;
        }

        // 弾スクリプトに方向と速度を適用
        MiniGhostEnemyBullet bullet = bulletObj.GetComponent<MiniGhostEnemyBullet>();
        if (bullet != null)
        {
            bullet.SetTarget(targetPosition, m_bulletSpeed, m_bulletAngle);
        }

        // 元の向きに戻る
        yield return StartCoroutine(TurnRoutine(false));

        yield return new WaitForSeconds(m_bulletDelay);

        m_fireTimer = 0f;

        m_isShooting = false;

    }

    // 弾を1つランダムに決める
    private GameObject GetRandomBulletPrefab()
    {
        if (m_bulletPrefabs == null || m_bulletPrefabs.Count == 0) return null;
        int randomIndex = Random.Range(0, m_bulletPrefabs.Count);
        return m_bulletPrefabs[randomIndex];
    }

    private IEnumerator TurnRoutine(bool turnToPlayer)
    {
        if (turnToPlayer && m_playerPosition == null) yield break;

        // 現在の向きをスタート地点にする（途中で切り替わっても滑らかに繋がる）
        Quaternion startRotation = transform.rotation;
        float timeElapsed = 0f;

        while (timeElapsed < m_turnDuration)
        {
            float t = timeElapsed / m_turnDuration;

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
                // プレイヤーを向く
                Vector3 directionToPlayer = (m_playerPosition.position - transform.position).normalized;
                targetRotation = Quaternion.LookRotation(-directionToPlayer);
            }
            else
            {
                // 元に戻る
                targetRotation = m_originalRotation;
            }

            // 回転を適用
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, easedT);

            timeElapsed += Time.deltaTime;
            yield return null;
        }

        // 最後にズレを無くすための補正
        if (turnToPlayer)
        {
            Vector3 finalDirection = (m_playerPosition.position - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(-finalDirection);
        }
        else
        {
            transform.rotation = m_originalRotation;
        }
    }




    private void OnCollisionEnter(Collision collision)
    {

        if (collision.gameObject.CompareTag("MagicBullet"))
        {
            Destroy(gameObject);
        }
    }
}
