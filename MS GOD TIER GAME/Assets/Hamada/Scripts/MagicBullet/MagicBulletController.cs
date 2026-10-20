using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class MagicBulletController : MonoBehaviour
{
    [Serializable]
    private struct BezierControlPoint
    {
        public Vector2 m_curveHeight;
        public Vector2 m_curveWidth;
    }

    [SerializeField] MagicData m_magicData;

    [SerializeField] float m_moveSpeed;

    [SerializeField] List<BezierControlPoint> m_bezierControlPoints;

    GameObject m_target;

    Vector3 m_controlPoint;

    public MagicData GetMagicData()
    {
        return m_magicData;
    }

    public void FireBullet(GameObject target, MAGIC_CAST_DIRECTION direction)
    {
        m_target = target;

        Vector3 centerPoint = Vector3.Lerp(transform.position, m_target.transform.position, 0.5f);

        CalculateControlPoint(centerPoint, direction);

        float distance = (m_target.transform.position - transform.position).magnitude;

        float duration = distance / m_moveSpeed;

        StartCoroutine(HomingRoutine(duration));
    }

    private void CalculateControlPoint(Vector3 centerPoint,MAGIC_CAST_DIRECTION direction) 
    {
        BezierControlPoint controlPoint = m_bezierControlPoints[(int)direction];

        float curveHeight = UnityEngine.Random.Range(controlPoint.m_curveHeight.x, controlPoint.m_curveHeight.y);
        float curveWidth = UnityEngine.Random.Range(controlPoint.m_curveWidth.x, controlPoint.m_curveWidth.y);

        m_controlPoint = centerPoint + Vector3.up * curveHeight + Vector3.right * curveWidth;
    }

    private IEnumerator HomingRoutine(float duration)
    {
        Vector3 startPosition = transform.position;

        float deltaTime = 0;

        while (deltaTime < duration)
        {
            deltaTime += Time.deltaTime;

            float rate = deltaTime / duration;

            rate = Mathf.Clamp01(rate);

            Vector3 position = Vector3.Lerp(startPosition, m_target.transform.position, rate);

            transform.position = CalculateBezier(startPosition, m_controlPoint, m_target.transform.position, rate);

            yield return null;
        }
    }

    private Vector3 CalculateBezier(Vector3 start, Vector3 controlPoint, Vector3 end, float rate) 
    {
        float u = 1.0f - rate;

        return u * u * start + 2f * u * rate * controlPoint + rate * rate * end;
    }

    //発射音再生 

    //---何か当たった時に呼び出してほしい処理---//

    //エフェクト再生

    //ヒット時の音再生
}
