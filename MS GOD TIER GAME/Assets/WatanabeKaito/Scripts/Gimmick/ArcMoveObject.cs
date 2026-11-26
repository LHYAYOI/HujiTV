using System.Collections;
using UnityEngine;

public class ArcMoveObject : MonoBehaviour
{
    // 移動方向
    public enum ARC_DIRECTION_TYPE
    {
        RIGHT,
        LEFT
    }

    [Header("移動先")]
    [SerializeField] private Transform m_target;

    [Header("移動方向")]
    [SerializeField] private ARC_DIRECTION_TYPE m_arcDirection = ARC_DIRECTION_TYPE.RIGHT;

    [Header("移動設定")]
    [SerializeField] private float m_moveDuration = 2.0f;
    [SerializeField] private float m_arcWidth = 3.0f;

    private Coroutine m_moveCoroutine;

    public void StartArcMove()
    {
        if (m_target == null)
        {
            return;
        }

        if (m_moveCoroutine != null)
        {
            StopCoroutine(m_moveCoroutine);
        }

        m_moveCoroutine = StartCoroutine(MoveAlongArc());
    }

    private IEnumerator MoveAlongArc()
    {
        Vector3 startPosition = transform.position;
        Vector3 endPosition = m_target.position;

        // 移動方向を取得（XZ平面）
        Vector3 direction = endPosition - startPosition;
        direction.y = 0.0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            transform.position = endPosition;
            m_moveCoroutine = null;
            yield break;
        }

        direction.Normalize();

        // 移動方向に対する右方向を取得
        Vector3 sideDirection = Vector3.Cross(
            Vector3.up,
            direction
        ).normalized;

        // 左方向の場合は反転
        if (m_arcDirection == ARC_DIRECTION_TYPE.RIGHT)
        {
            sideDirection *= -1.0f;
        }

        float elapsedTime = 0.0f;

        while (elapsedTime < m_moveDuration)
        {
            elapsedTime += Time.deltaTime;

            // 移動割合
            float t = Mathf.Clamp01(
                elapsedTime / m_moveDuration
            );

            // 直線移動の位置
            Vector3 position = Vector3.Lerp(
                startPosition,
                endPosition,
                t
            );

            // 弧の膨らみ
            float arc = Mathf.Sin(Mathf.PI * t) * m_arcWidth;

            // 右または左に膨らませる
            position += sideDirection * arc;

            transform.position = position;

            yield return null;
        }

        transform.position = endPosition;
        m_moveCoroutine = null;
    }
}