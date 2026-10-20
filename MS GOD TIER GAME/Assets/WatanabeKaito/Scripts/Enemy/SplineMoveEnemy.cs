//-----------------------------------------------
// SplineMoveEnemy.cs
// 制作日：2026/09/30
// 制作者：渡辺開斗
// 概要：スプライン上を移動するエネミーのクラス
//-----------------------------------------------
using UnityEngine;
using UnityEngine.Splines;

public class SplineMoveEnemy : EnemyBase
{
    [Header("進行方向(1 = 始点→終点, -1 = 終点→始点)")]
    [SerializeField] private float m_moveDirection = -1f;


    protected float m_moveSpeed = 1f; // 移動スピード
    protected bool m_rotateWithSplineFlag = true;// スプラインに合わせて回転するかどうか
    protected SplineContainer m_targetSpline; // 移動対象のスプライン
    protected float m_distanceTraveled = 0f;  // スプライン上の移動距離
    protected float m_splineLength;           // スプラインの全長
    protected bool m_isClosedSplineFlag = false; // スプラインがループClosedしているかどうか
    protected Vector3 m_spawnPosition;


    public override void Initialize()
    {
        // 初期化処理
    }

    public virtual void SetupSpline(SplineContainer spline)
    {
        m_targetSpline = spline;
        if (m_targetSpline != null)
        {
            m_splineLength = m_targetSpline.CalculateLength();
            m_distanceTraveled = 0f;

            // Closedかどうかを取得しておく
            m_isClosedSplineFlag = m_targetSpline.Spline.Closed;
        }
    }

    public void SetSpawnPosition(Vector3 spawnPosition)
    {
        m_spawnPosition = spawnPosition;
    }


    protected virtual void Update()
    {
        if (m_targetSpline == null || m_splineLength <= 0  || m_enemyData == null) return;

        UpdateMovement();
    }

    // 移動の更新処理
    protected virtual void UpdateMovement()
    {
        m_distanceTraveled += m_moveSpeed * m_moveDirection * Time.deltaTime;

        if (m_isClosedSplineFlag)
        {
            if (m_distanceTraveled >= m_splineLength)
            {
                m_distanceTraveled %= m_splineLength;
            }
            else if (m_distanceTraveled < 0f)
            {
                m_distanceTraveled = (m_distanceTraveled % m_splineLength) + m_splineLength;
            }
        }
        else
        {
            if (m_distanceTraveled >= m_splineLength)
            {
                m_distanceTraveled = m_splineLength;
            }
            else if (m_distanceTraveled <= 0f)
            {
                m_distanceTraveled = 0f;
            }
        }

        float progress = m_distanceTraveled / m_splineLength;
        Vector3 position = m_targetSpline.EvaluatePosition(progress);
        Vector3 tangent = m_targetSpline.EvaluateTangent(progress);

        // 座標の更新
        transform.position = position;

        //TurnToPlayer();

        if (m_rotateWithSplineFlag)
        {
            // 向きの更新
            UpdateRotation(tangent);
        }
    }

    // 向きの更新処理
    protected virtual void UpdateRotation(Vector3 tangent)
    {
        if (tangent != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(tangent);
        }
    }

    
}