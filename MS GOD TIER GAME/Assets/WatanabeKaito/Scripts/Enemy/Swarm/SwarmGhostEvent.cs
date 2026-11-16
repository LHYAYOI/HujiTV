
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

public class SwarmGhostEvent : SplineMoveEnemy
{

    [SerializeField] private GroupLeader m_groupLeader;

    [Header("移動設定")]
    [SerializeField] private bool m_loopFlag = true;

    [Header("回転補正")]
    [SerializeField] private Vector3 m_rotationOffset;

    // スプラインのサンプリング数
    private const int SAMPLE_COUNT = 200;

    // スプラインの位置・距離テーブル
    private Vector3[] m_samplePositions;
    private float[] m_distanceTable;

    // スプライン全体の長さ
    private float m_totalLength = 0.0f;

    // 現在の移動距離
    private float m_currentDistance = 0.0f;

    // 移動中かどうか
    private bool m_isMovingFlag = false;

    void Start()
    {
       
    }

    

    
    public override void Initialize()
    {
        // スプラインの始点に移動
        transform.position = m_targetSpline.EvaluatePosition(0f);
        m_groupLeader.SpawnAllSwarmGhost();
        StartSplineMovement();
    }

    public override void BattleStart()
    {
       Debug.Log("SwarmGhostEvent: BattleStart called.");
    }

    public override void BattleEnd()
    {
        Debug.Log("SwarmGhostEvent: BattleEnd called.");
        m_isMovingFlag = false;

        m_groupLeader.RemoveAllOyoguSwarmGhosts();

        Debug.Log("SwarmGhostEvent: BattleEnd called, all swarm ghosts removed.");
    }


    // スプライン移動を初期化
    public void StartSplineMovement()
    {
        if (m_targetSpline == null)
        {
            return;
        }

        // スプラインの距離テーブルを作成
        CreateDistanceTable();

        if (m_totalLength <= 0.001f)
        {
            return;
        }

        // 現在の位置に最も近いスプライン上の地点を探す
        float nearestDistance = float.MaxValue;
        int nearestIndex = 0;

        for (int i = 0; i <= SAMPLE_COUNT; i++)
        {
            float distance = (
                m_samplePositions[i] - transform.position
            ).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        // 最も近い位置から移動を開始
        m_currentDistance = m_distanceTable[nearestIndex];

        m_isMovingFlag = true;

        // 初期位置と回転を更新
        UpdateSplineTransform();
    }


    // スプラインの距離テーブルを作成
    private void CreateDistanceTable()
    {
        m_samplePositions = new Vector3[SAMPLE_COUNT + 1];
        m_distanceTable = new float[SAMPLE_COUNT + 1];

        m_totalLength = 0.0f;

        for (int i = 0; i <= SAMPLE_COUNT; i++)
        {
            float t = i / (float)SAMPLE_COUNT;

            m_samplePositions[i] =
                m_targetSpline.EvaluatePosition(t);

            if (i > 0)
            {
                float distance = Vector3.Distance(
                    m_samplePositions[i - 1],
                    m_samplePositions[i]
                );

                m_totalLength += distance;
            }

            m_distanceTable[i] = m_totalLength;
        }
    }


    

    // 移動距離からスプラインの補間値を取得
    private float GetInterpolationFromDistance(float distance)
    {
        int low = 0;
        int high = SAMPLE_COUNT;

        // 二分探索で距離に対応する区間を探す
        while (low < high)
        {
            int middle = (low + high) / 2;

            if (m_distanceTable[middle] < distance)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        int upperIndex = low;
        int lowerIndex = Mathf.Max(0, upperIndex - 1);

        float segmentLength =
            m_distanceTable[upperIndex] -
            m_distanceTable[lowerIndex];

        float ratio = 0.0f;

        if (segmentLength > 0.0001f)
        {
            ratio =
                (distance - m_distanceTable[lowerIndex]) /
                segmentLength;
        }

        float lowerT = lowerIndex / (float)SAMPLE_COUNT;
        float upperT = upperIndex / (float)SAMPLE_COUNT;

        return Mathf.Lerp(lowerT, upperT, ratio);
    }


    // 親オブジェクトの位置と回転を更新
    private void UpdateSplineTransform()
    {
        float t = GetInterpolationFromDistance(m_currentDistance);

        // スプライン上の位置に移動
        transform.position =
            m_targetSpline.EvaluatePosition(t);

        // スプラインの進行方向を取得
        Vector3 tangent =
            m_targetSpline.EvaluateTangent(t);

        Vector3 up =
            m_targetSpline.EvaluateUpVector(t);

        if (tangent.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (up.sqrMagnitude <= 0.0001f)
        {
            up = Vector3.up;
        }

        // 進行方向に回転
        Quaternion targetRotation = Quaternion.LookRotation(
            tangent.normalized,
            up.normalized
        );

        transform.rotation =
            targetRotation * Quaternion.Euler(m_rotationOffset);
    }

    public bool GetIsMoving()
    {
        return m_isMovingFlag;
    }

}
