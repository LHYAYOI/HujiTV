using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UIElements;


public class CartController : MonoBehaviour
{
    [SerializeField] private Transform m_targetCard;

    [SerializeField] private bool m_movingFlag;
    [SerializeField] private float m_speed;
    [SerializeField] private float m_distance; // 全体の進んだ距離


    private SplineContainer m_spline;
    private float m_splineAnchor = 0.0f; // 進んだ距離 - アンカーポイント
    private bool m_loopFlag = false;


    // ------------------------------------------------------------------------
    // Unity Event
    // ------------------------------------------------------------------------
    private void OnValidate()
    {
        // レールにマイナスがないので distance は 0 ~ ∞
        // マイナス値になったら0に戻す
        if(m_distance < 0.0f)
        {
            m_distance = 0.0f;
        }
    }

    private void Update()
    {
        if(m_targetCard == null)
        {
            return;
        }

        MoveUpdate();
    }

    private void MoveUpdate()
    {

        if (m_movingFlag == false)
        {
            return;
        }

        if(m_spline == null)
        {
            return;
        }

        // max なら とまるかループ
        float length = m_spline.CalculateLength();
        float splineOffset = SplineOffset;

        if(splineOffset > length)
        {
            if(m_loopFlag)
            {
                m_splineAnchor += length;
            }

            return;
        }

        // 進む
        m_distance += m_speed * Time.deltaTime;


        // Transform位置更新
        float value = splineOffset / length;
        Vector3 position = m_spline.EvaluatePosition(value);

        if(m_targetCard == null)
        {
            return;
        }

        m_targetCard.position = position;

    }


    // ------------------------------------------------------------------------
    // Public Event
    // ------------------------------------------------------------------------
    public void SetSpline(SplineContainer spline , float offsetDistance,bool loopFlag)
    {
        // spline1 -------- distance 
        //                |
        // spline2 offset ---・anchor

        // m_distance - m_splineAnchor 
        // -> new spline offset = 0

        // m_distance - (m_splineAnchor - offset)
        // -> new spline offset = offset
        
        m_spline = spline;
        m_splineAnchor = m_distance - offsetDistance;
        m_loopFlag = loopFlag;
    }
    
    public float SplineOffset => m_distance - m_splineAnchor;
    
}
