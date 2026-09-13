using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class SplineController : MonoBehaviour
{

    [SerializeField] private SplineContainer m_spline;
    [SerializeField] private List<SplineSwitchPointData> m_switchPoints;



    private void OnDrawGizmos()
    {
        if(m_spline == null)
        {
            return;
        }

        foreach( var point in m_switchPoints)
        {
            // start end
            float length = m_spline.CalculateLength();
            if(length <= 0.01f)
            {
                continue;
            }

            Vector2 startEnd = point.SwitchStartEnd;
            float distance = Mathf.Abs(startEnd.x - startEnd.y);
            Vector2 startEnd01 = startEnd / length;
            float distance01 = Mathf.Abs(startEnd01.x - startEnd01.y);

            Vector3 startPosition = m_spline.EvaluatePosition(startEnd01.x);
            Vector3 endPosition = m_spline.EvaluatePosition(startEnd01.y);


            Gizmos.color = Color.yellow;
            
            // start end の描画
            Gizmos.DrawSphere(startPosition, 0.5f);
            Gizmos.DrawSphere(endPosition, 0.5f);



            // start - end の line
            Gizmos.color = Color.orange;

            float delta = 0.05f;
            for(float t = 0.0f; t <= 1.0f - delta; t += delta)
            {
                Vector3 start = m_spline.EvaluatePosition(startEnd01.x + distance01 * t);
                start += Vector3.up * 0.1f;
                Vector3 end = m_spline.EvaluatePosition(startEnd01.x + distance01 * ( t + delta));
                end += Vector3.up * 0.1f;

                Gizmos.DrawLine(start, end);
            }



            // another の描画
            SplineContainer anotherSpline = point.Spline;
            if(anotherSpline == null)
            {
                continue;
            }
            float anotherLength = anotherSpline.CalculateLength();
            float switchStartOffset01 = point.SwitchStartOffset / length;
            Vector2 anotherStartEnd01 = point.SwitchStartEnd / anotherLength;
            float anotherDistance01 = distance / anotherLength;

            // start - another spline start の描画
            Vector3 anotherSplineStartPosition = anotherSpline.EvaluatePosition(switchStartOffset01);
            Gizmos.DrawLine(startPosition , anotherSplineStartPosition);
            Gizmos.DrawSphere(anotherSplineStartPosition, 0.5f);
            
            // end - another spline end の描画
            Vector3 anotherSplineEndPosition = anotherSpline.EvaluatePosition(switchStartOffset01 + anotherDistance01);
            Gizmos.DrawLine(endPosition, anotherSplineEndPosition);
            Gizmos.DrawSphere(anotherSplineEndPosition, 0.5f);

        }



    }
}
