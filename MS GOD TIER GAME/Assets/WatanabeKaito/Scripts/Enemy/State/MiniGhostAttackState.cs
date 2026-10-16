using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMiniGhostAttackState", menuName = "States/MiniGhostStates/MiniGhostAttackState")]
public class MiniGhostAttackState : StateBase
{
    [Header("弾のプレハブリスト")]
    [SerializeField] private List<GameObject> m_bulletPrefabs;

    [Header("弾の速度")]
    [SerializeField] private float m_bulletSpeed = 10.0f;

    [Header("弾の発射角度（度数）")]
    [SerializeField] private float m_bulletAngle = 45.0f;

    [Header("弾が生成されてから発射されるまでの時間（秒）")]
    [SerializeField] private float m_bulletDelay = 1.0f;

    [Header("振りむくまでにかかる時間（秒）")]
    [SerializeField] private float m_turnDuration = 1.0f;

    private Quaternion m_originalRotation;  // 元の向きを保持する変数



    MiniGhostEnemy m_miniGhostEnemy;
    public override void Awake()
    {
       
    }

    public override void Start()
    {
        m_miniGhostEnemy = m_stateOwner.GetComponent<MiniGhostEnemy>();
        //CoroutineRunner.Instance.StartCoroutine(ShootCoroutine());
    }

    public override void EnterState()
    {
        //StartCoroutine(ShootCoroutine());
    }

    public override void UpdateState()
    {
    }

    public override void ExitState()
    {

    }

   

    // 弾の生成と発射処理
    private IEnumerator ShootCoroutine()
    {
        Vector3 playerPosition = m_miniGhostEnemy.GetPlayerPosition();

        Vector3 firePosition = m_miniGhostEnemy.GetFirePosition();

        m_originalRotation = m_stateOwner.transform.rotation;

        // プレイヤーの方向を向く
        //yield return StartCoroutine(TurnRoutine(true));

        // 発射口の位置を取得
        Vector3 spawnPosition = firePosition;

        // ランダムに弾のプレハブを選択
        GameObject bulletPrefab = GetRandomBulletPrefab();

        // 弾を生成
        GameObject bulletObj = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);

        // 少し待ってから方向を設定する
        yield return new WaitForSeconds(m_bulletDelay);

        if (bulletObj == null || firePosition == null) yield break;

        // ターゲットへの方向を計算（ターゲットが無い場合は進行方向へ飛ばす）
        Vector3 direction = m_stateOwner.transform.forward;
        Vector3 targetPosition = playerPosition;
        if (playerPosition != null)
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
        //yield return StartCoroutine(TurnRoutine(false));

        yield return new WaitForSeconds(m_bulletDelay);
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
        Vector3 playerPosition = m_miniGhostEnemy.GetPlayerPosition();

        if (turnToPlayer && m_miniGhostEnemy.GetPlayerPosition() == null) yield break;

        // 現在の向きをスタート地点にする（途中で切り替わっても滑らかに繋がる）
        Quaternion startRotation = m_stateOwner.transform.rotation;
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
                Vector3 directionToPlayer = (playerPosition - m_stateOwner.transform.position).normalized;
                targetRotation = Quaternion.LookRotation(-directionToPlayer);
            }
            else
            {
                // 元に戻る
                targetRotation = m_originalRotation;
            }

            // 回転を適用
            m_stateOwner.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, easedT);

            timeElapsed += Time.deltaTime;
            yield return null;
        }

        // 最後にズレを無くすための補正
        if (turnToPlayer)
        {
            Vector3 finalDirection = (playerPosition - m_stateOwner.transform.position).normalized;
            m_stateOwner.transform.rotation = Quaternion.LookRotation(-finalDirection);
        }
        else
        {
            m_stateOwner.transform.rotation = m_originalRotation;
        }
    }
}
