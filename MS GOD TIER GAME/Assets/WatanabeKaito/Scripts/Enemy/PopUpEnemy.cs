//-----------------------------------------------
// PopUpEnemy.cs
// 制作日：2026/09/30
// 制作者：渡辺開斗
// 概要：特定の位置に出現するエネミーのクラス
//-----------------------------------------------
using System.Collections;
using UnityEngine;

public class PopUpEnemy : EnemyBase
{
    [Header("出現時間")]
    [SerializeField] private float m_popUpDuration = 20.0f; // 出現している時間

    [Header("エネミーの弾")]
    [SerializeField] private GameObject m_enemyBullet; // エネミーの弾

    [Header("弾の発射位置")]
    [SerializeField] private Transform m_bulletShotPosition; // 弾の発射位置

    private EnemyBulletManager m_enemyBulletManager; // エネミーの弾の管理クラス

    private float m_time = 0.0f;

    protected override void Awake()
    {
        base.Awake();
    }

    public override void Initialize()
    {
        //m_enemyBulletManager = m_enemyBullet.GetComponent<EnemyBulletManager>();
    }

    void Update()
    {
        //// 出現している時間をカウントダウン
        //m_popUpDuration -= Time.deltaTime;

        //if (m_popUpDuration <= 0f)
        //{
        //    // 出現時間が終了したらエネミーを非表示にする
        //    gameObject.SetActive(false);
        //}

        //// 出現中にプレイヤーに向かって弾を撃つ
        //if (m_playerPosition != null && m_enemyBullet != null)
        //{
            
        //}

        //m_time += Time.deltaTime;

        //if (m_time >= m_enemyData.m_attackSpeed)
        //{
        //    // 弾を生成
        //    Instantiate(m_enemyBullet, m_bulletShotPosition.position, Quaternion.identity);

        //    // 弾を撃つ処理
        //    m_enemyBulletManager.Shot(m_playerPosition);

        //    Debug.Log(m_playerPosition);

        //    m_time = 0.0f;
        //}
    }

    private void OnCollisionEnter(Collision collision)
    {
       
        Debug.Log("当たった");
        Destroy(gameObject);
    }

}
