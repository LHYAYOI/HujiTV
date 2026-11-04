using System.Collections.Generic;
using UnityEngine;

public class GroupLeader_Ara : MonoBehaviour
{
    [SerializeField] private List<Transform> m_children;

    [Header("リーダーとの距離")]
    [SerializeField] private Vector2 m_distanceMinMax = new Vector2(2f, 5f);

    [Header("移動速度")]
    [SerializeField] private Vector2 m_speedMinMax = new Vector2(2f, 5f);

    [Header("角度の回転速度")]
    [SerializeField] private Vector2 m_rotateSpeedMinMax = new Vector2(10f, 60f);
    [Header("目的地の手前で停止する距離")]
    [SerializeField] private float m_stopDistance = 0.1f;
    private class ChildData
    {
        public Transform transform;

        // リーダーとの距離
        public float distance;

        // リーダーからの方向
        public Vector3 direction;

        // 回転軸
        public Vector3 rotationAxis;

        // 回転速度
        public float rotateSpeed;

        // 移動速度
        public float moveSpeed;
    }

    private List<ChildData> m_childData = new List<ChildData>();

    private void Start()
    {
        foreach (Transform child in m_children)
        {
            ChildData data = new ChildData();

            data.transform = child;

            // リーダーからの距離
            data.distance = Random.Range(
                m_distanceMinMax.x,
                m_distanceMinMax.y
            );

            // 初期方向をランダムに設定
            data.direction = Random.onUnitSphere;

            // 回転軸をランダムに設定
            data.rotationAxis = Random.onUnitSphere;

            // 回転速度をランダムに設定
            float direction = Random.value < 0.5f ? -1f : 1f;

            data.rotateSpeed = Random.Range(
                m_rotateSpeedMinMax.x,
                m_rotateSpeedMinMax.y
            ) * direction;

            // 移動速度をランダムに設定
            data.moveSpeed = Random.Range(
                m_speedMinMax.x,
                m_speedMinMax.y
            );

            m_childData.Add(data);
        }
    }

    private void Update()
    {
        foreach (ChildData child in m_childData)
        {
            UpdateChild(child);
        }
    }
    private void UpdateChild(ChildData child)
    {
        // 3D方向を回転させる
        child.direction = Quaternion.AngleAxis(
            child.rotateSpeed * Time.deltaTime,
            child.rotationAxis
        ) * child.direction;

        // 目的地
        Vector3 targetPosition =
            transform.position + child.direction * child.distance;

        // 子供から目的地への方向
        Vector3 moveDirection =
            targetPosition - child.transform.position;

        float distance = moveDirection.magnitude;

        // 目的地の手前まで移動
        if (distance > m_stopDistance)
        {
            child.transform.position = Vector3.MoveTowards(
                child.transform.position,
                targetPosition,
                child.moveSpeed * Time.deltaTime
            );
        }

        // 目的地への方向を求めてRotationを設定
        if (moveDirection.sqrMagnitude > 0.001f)
        {
            child.transform.rotation =
                Quaternion.LookRotation(moveDirection.normalized);
        }
    }
}