using System.Collections;
using UnityEngine;

public class MiniGhostEnemy : SplineMoveEnemy
{

    [Header("弾のプレハブ")]
    [SerializeField] private GameObject m_bulletPrefab;

    [Header("弾の発射間隔（秒）")]
    [SerializeField] private float m_fireInterval = 2.0f;

    [Header("弾の速度")]
    [SerializeField] private float m_bulletSpeed = 10.0f;

    [Header("弾の発射位置")]
    [SerializeField] private Transform m_firePoint;

    [Header("弾が生成されてから発射されるまでの時間（秒）")]
    [SerializeField] private float m_bulletDelay = 1.0f;

    public float turnDuration = 1.0f; // 振り向くのにかける秒数

    private Quaternion m_originalRotation;
    private Coroutine m_turnCoroutine;

    private float m_fireTimer = 0f; // 弾の発射タイマー

    public override void Initialize()
    {
        base.Initialize();
        m_fireTimer = 0f;
    }

    protected override void Update()
    {
        // 親クラスの移動処理を更新
        base.Update();

        // 弾の発射タイマーを更新
        UpdateShooting();
    }

    // 射撃タイマー処理
    private void UpdateShooting()
    {
        if (m_bulletPrefab == null) return;

        m_fireTimer += Time.deltaTime;
        if (m_fireTimer >= m_fireInterval)
        {
            StartCoroutine(ShootCoroutine());
            m_fireTimer = 0f;
        }
    }

    // 弾の生成と発射処理
    private IEnumerator ShootCoroutine()
    {
        m_originalRotation = transform.rotation;

        // 発射する時は停止する
        float originalMoveSpeed = m_moveSpeed;
        m_moveSpeed = 0f;

        // 発射口が指定されていればその位置、なければエネミー自身の位置を使用
        Vector3 spawnPosition = m_firePoint.position;

        // プレイヤーの方向を向く
        yield return StartCoroutine(TurnRoutine(true));

        // 弾を生成
        GameObject bulletObj = Instantiate(m_bulletPrefab, spawnPosition, Quaternion.identity);

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
            bullet.SetDirection(direction, m_bulletSpeed);
        }

        // 元の向きに戻る
        yield return StartCoroutine(TurnRoutine(false));

        // 移動速度を元に戻す
        m_moveSpeed = originalMoveSpeed;
    }

    private IEnumerator TurnRoutine(bool turnToPlayer)
    {
        if (turnToPlayer && m_playerPosition == null) yield break;

        // 現在の向きをスタート地点にする（途中で切り替わっても滑らかに繋がる）
        Quaternion startRotation = transform.rotation;
        float timeElapsed = 0f;

        while (timeElapsed < turnDuration)
        {
            float t = timeElapsed / turnDuration;

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
                targetRotation = Quaternion.LookRotation(directionToPlayer);
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
            transform.rotation = Quaternion.LookRotation(finalDirection);
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
