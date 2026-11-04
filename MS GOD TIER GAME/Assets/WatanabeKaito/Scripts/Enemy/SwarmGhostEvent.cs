
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

public class SwarmGhostEvent : MonoBehaviour
{
    [Header("移動させるスプライン")]
    [SerializeField] private SplineContainer m_splineContainer;

    [Header("移動設定")]
    [SerializeField] private float m_moveSpeed = 2.0f;
    [SerializeField] private bool m_loopFlag = true;

    [Header("回転補正")]
    [SerializeField] private Vector3 m_rotationOffset;

    // スプラインのサンプリング数
    private const int SAMPLE_COUNT = 200;

    // 現在出現している群れお化け
    private List<GameObject> m_activeSwarmGhostList =
        new List<GameObject>();

    // 現在出現している群れお化けの位置
    private List<Transform> m_activeSpawnPositions =
        new List<Transform>();

    // まだ出現に使用していない位置
    private List<Transform> m_availableSpawnPositions =
        new List<Transform>();

    // 出現処理が重複して実行されるのを防ぐ
    private bool m_isSpawningFlag = false;

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

    private void Update()
    {
      
        // スプライン移動
        if (m_isMovingFlag)
        {
            MoveAlongSpline();
        }
    }

    

   

    // スプライン移動を初期化
    public void StartSplineMovement()
    {
        if (m_splineContainer == null)
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
                m_splineContainer.EvaluatePosition(t);

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


    // スプラインに沿って移動
    private void MoveAlongSpline()
    {
        // 移動距離を更新
        m_currentDistance += m_moveSpeed * Time.deltaTime;

        if (m_loopFlag)
        {
            // 終点に到達したら先頭に戻る
            m_currentDistance = Mathf.Repeat(
                m_currentDistance,
                m_totalLength
            );
        }
        else
        {
            // ループしない場合は終点で停止
            if (m_currentDistance >= m_totalLength)
            {
                m_currentDistance = m_totalLength;
                m_isMovingFlag = false;
            }
        }

        UpdateSplineTransform();
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
            m_splineContainer.EvaluatePosition(t);

        // スプラインの進行方向を取得
        Vector3 tangent =
            m_splineContainer.EvaluateTangent(t);

        Vector3 up =
            m_splineContainer.EvaluateUpVector(t);

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


    
}
