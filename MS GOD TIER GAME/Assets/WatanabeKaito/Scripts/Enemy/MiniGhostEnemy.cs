using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class MiniGhostEnemy : SplineMoveEnemy
{
    [Header("制限時間")]
    [SerializeField] private float m_timeLimit = 15.0f;

    [Header("弾の発射位置")]
    [SerializeField] private Transform m_firePoint;

    private StateMachine m_stateMachine;

    public override void Initialize()
    {
        base.Initialize();

        m_stateMachine = GetComponent<StateMachine>();
        m_stateMachine.ChangeState("MiniGhostSpawnState");
    }

    protected override void Update()
    {
      
    }

    // スプライン上の移動処理を更新する
    public void UpdateSplineMove()
    {
        base.Update();
    }

    // スプラインを取得する
    public SplineContainer GetTargetSpline()
    {
        return m_targetSpline;
    }

    // スポーン位置を取得する
    public Vector3 GetSpawnPosition()
    {
        return m_spawnPosition;
    }

    // プレイヤーの位置を取得する
    public Vector3 GetPlayerPosition()
    {
        return m_playerPosition.position;
    }

    // スプライン上の移動距離を取得する

    public float GetDistanceTraveled()
    {
        return m_distanceTraveled;
    }

    // 弾の発射位置を取得する
    public Vector3 GetFirePosition()
    {
        return m_firePoint.position;
    }

    // 移動スピードを設定する
    public void SetMoveSpeed(float speed)
    {
        m_moveSpeed = speed;
    }

    // スプラインに合わせて回転するかどうかを設定する
    public void SetRotateWithSplineFlag(bool flag)
    {
        m_rotateWithSplineFlag = flag;
    }


    // スプライン上の移動距離を設定する
    public void SetDistanceTraveled(float distance)
    {
        m_distanceTraveled = distance;
    }

    // スプライン上の移動方向を取得する
    public void SetMoveDirection(float direction)
    {
        m_moveDirection = direction;
    }


    private void OnCollisionEnter(Collision collision)
    {

        if (collision.gameObject.CompareTag("MagicBullet"))
        {
            Destroy(gameObject);
        }
    }
}
