using System.Collections;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UIElements;


public class CartController : MonoBehaviour
{
    private enum CartState
    {
        Idle,
        Stop,
        Move,
        Switch
    }
    private CartState m_state;


    [SerializeField] private Transform m_targetCard;

    [SerializeField] private bool m_movingFlag;
    [SerializeField] private float m_speed;
    private float m_baseSpeed;
    [SerializeField] private float m_switchSpeed;
    [SerializeField] private float m_distance; // 全体の進んだ距離


    private SplineContainer m_spline;
    
    private float m_splineAnchor = 0.0f; // 進んだ距離 - アンカーポイント
    
    private bool m_loopFlag = false;
    private bool m_switchFlag = false;






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

    private void Awake()
    {
        m_state = CartState.Move;

        m_baseSpeed = m_speed;
    }

    private void Update()
    {
        if(m_targetCard == null)
        {
            return;
        }

        switch (m_state)
        {
            case CartState.Move:
                MoveUpdate();
                break;

            case CartState.Switch:
                SwitchMoveUpdate();
                break;
        }

    }

    private void MoveUpdate()
    {
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
            else
            {
                m_state = CartState.Stop;
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

    private void SwitchMoveUpdate()
    {
        if(m_spline == null)
        {
            return;
        }


        Vector3 start = m_targetCard.position;

        float length = m_spline.CalculateLength();
        float splineOffset = SplineOffset;
        float value = splineOffset / length;
        Vector3 end = m_spline.EvaluatePosition(value);

        Vector3 direction = end - start;
        float speed = m_switchSpeed * Time.deltaTime;
        if(direction.sqrMagnitude > speed * speed)
        {
            direction = direction.normalized * speed;
        }
        else
        {
            m_state = CartState.Move;
        }

        m_targetCard.position += direction;
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


        m_state = CartState.Switch;
    }
    
    public float SplineOffset => m_distance - m_splineAnchor;
    
    public IEnumerator SetSpeed(float speed, float time)
    {
        m_speed = speed;

        yield return new WaitForSeconds(time);

        ResetSpeed();
    }

    public void ResetSpeed()
    {
        m_speed = m_baseSpeed;
    }

}
