using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Splines;

public class SplineController2 : MonoBehaviour
{
    [SerializeField] private SplineContainer m_spline;
    [SerializeField] private List<SplineSwitchPointData2> m_switchPoints;


    private void OnDrawGizmos()
    {
        if (m_spline == null)
        {
            return;
        }

        foreach (var point in m_switchPoints)
        {
            // start end
            float length = m_spline.CalculateLength();
            if (length <= 0.01f)
            {
                continue;
            }


            // start point の描画
            float switchPoint = point.SwitchPoint / length;
            float rangeStart =
                Mathf.Max(switchPoint - point.SwitchChangeRange / length, 0.0f);
            float distance = Mathf.Abs(switchPoint - rangeStart);

            Vector3 pointPosition = m_spline.EvaluatePosition(switchPoint);
            Vector3 startPosition = m_spline.EvaluatePosition(rangeStart);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(pointPosition, 0.5f);
            Gizmos.DrawSphere(startPosition, 0.5f);


            // start - point の line
            Gizmos.color = Color.orange;

            float delta = 0.05f;
            for (float t = 0.0f; t <= 1.0f - delta; t += delta)
            {
                Vector3 start = m_spline.EvaluatePosition(rangeStart + distance * t);
                start += Vector3.up * delta;
                Vector3 end = m_spline.EvaluatePosition(rangeStart + distance * (t + delta));
                end += Vector3.up * delta;

                Gizmos.DrawLine(start, end);
            }


            // point - another point
            SplineContainer anotherSpline = point.Spline;
            if(anotherSpline == null)
            {
                continue;
            }
            float anotherLength = anotherSpline.CalculateLength();
            float switchOffset = point.SwitchOffset / anotherLength;
            Vector3 offsetPosition = anotherSpline.EvaluatePosition(switchOffset);

            Gizmos.DrawLine(pointPosition, offsetPosition);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(offsetPosition, 0.5f);

        }



    }
}
