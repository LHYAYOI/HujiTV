using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class GimmickRangeShower : MonoBehaviour
{
    [SerializeField] SplineController2 m_splineController;

    [SerializeField] GimmickSpline m_EnterPoint;
    [SerializeField] GimmickSpline m_ExitPoint;
    [SerializeField] private Color m_rangeColor = Color.green;

    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {


        SplineContainer mySpline = m_splineController.Spline;
        if (mySpline == null)
        {
            return;
        }

        float myLength = mySpline.CalculateLength();
        float gimmickPoint01 = m_EnterPoint.GimmickPoint / myLength;
        float range01 = (m_EnterPoint.GimmickPoint - m_ExitPoint.GimmickPoint) / myLength;


        Gizmos.color = m_rangeColor;
        int step = 20;
        float start = gimmickPoint01;
        float delta = range01 / step;
        for (int i = 0; i < step - 1; i++)
        {
            Vector3 from = (Vector3)mySpline.EvaluatePosition(start - delta * i) + Vector3.right * 0.05f;
            Vector3 to = (Vector3)mySpline.EvaluatePosition(start - delta * (i + 1)) + Vector3.left * 0.05f;
            Gizmos.DrawLine(from, to);
        }


        Gizmos.color = m_rangeColor * 0.8f;
        Gizmos.DrawSphere(mySpline.EvaluatePosition(start), 0.1f);
        Gizmos.DrawSphere(mySpline.EvaluatePosition(start - range01), 0.1f);

    }
}
