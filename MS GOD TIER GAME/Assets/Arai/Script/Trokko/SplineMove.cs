using UnityEngine;
using UnityEngine.Splines;


public class SplineMove : MonoBehaviour
{
    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――

    [SerializeField] private SplineContainer m_spline;
    [SerializeField] private Transform m_targetObject;


    [Header("パラメーター")]
    [SerializeField] private bool m_distanceMoveFlag;
    [Range(0.0f, 1.0f)]
    [SerializeField] private float m_value01;
    [SerializeField] private float m_valueDistance;


    // ―――――――――――――――――――――――――――――――――――――
    // Unity Event
    // ―――――――――――――――――――――――――――――――――――――

    private void OnValidate()
    {
        m_valueDistance = Mathf.Abs(m_valueDistance);


        if (m_distanceMoveFlag)
        {
            MoveByDistance(m_valueDistance);
        }

        MoveByValue(m_value01);
    }

    private void Update()
    {

        if(m_distanceMoveFlag)
        {
            MoveByDistance(m_valueDistance);
        }

        MoveByValue(m_value01);
    }

    // ―――――――――――――――――――――――――――――――――――――
    // Private Event
    // ―――――――――――――――――――――――――――――――――――――
    
    private void MoveByDistance (float distance)
    {
        float length = m_spline.CalculateLength();

        m_value01 = Mathf.Repeat(distance / length,1f);        
    }

    /// <summary>
    /// スプラインの最初から最後までを 0 - 1 で指定、
    /// m_targetObject を動かす
    /// </summary>
    /// <param name="value">　0～1 </param>
    private void MoveByValue(float value)
    {
        value = Mathf.Clamp01(value);


        Vector3 position = m_spline?.EvaluatePosition(m_value01) ?? Vector3.zero;
        Vector3 tangent = m_spline?.EvaluateTangent(m_value01) ?? Vector3.forward;

        if (m_targetObject != null)
        {
            m_targetObject.position = position;
            m_targetObject.rotation = Quaternion.LookRotation(tangent);
        }
    }
}
