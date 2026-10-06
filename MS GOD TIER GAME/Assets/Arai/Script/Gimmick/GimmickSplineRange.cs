using System;
using UnityEngine;
using UnityEngine.Splines;

public class GimmickSplineRange : GimmickSpline
{
    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――

    [SerializeField] private GimmickSplineRange m_pair;
    private CartController m_cart;
    private float m_startDistance;

    

    // ―――――――――――――――――――――――――――――――――――――
    // Public Propaty
    // ―――――――――――――――――――――――――――――――――――――

    public event Action<CartController,float> OnInRange;


    // ―――――――――――――――――――――――――――――――――――――
    // Unity Event
    // ―――――――――――――――――――――――――――――――――――――

    private void OnValidate()
    {
        if(m_pair == this)
        {
            m_pair = null;
        }
    }
    private void Update()
    {
        if(m_cart == null)
        {
            return;
        }

        // 0 - 1 rate
        float max = m_pair.GimmickPoint - m_startDistance;
        float rate = m_pair.GimmickPoint - m_cart.SplineOffset;
        float dist = m_cart.SplineOffset - m_startDistance;

        

        OnInRange?.Invoke(m_cart , Mathf.Clamp01(dist / max) );


        // 範囲外
        if (Mathf.Abs(max) < Mathf.Abs(rate) ||
            Mathf.Abs(dist) > Mathf.Abs(max))
        {
            m_cart = null;
        }

    }

    // ―――――――――――――――――――――――――――――――――――――
    // Public Event
    // ―――――――――――――――――――――――――――――――――――――    
    public void EnterRange(HitData hitdata)
    {
        var cart = hitdata.GetHitObjectComponent<CartController>();
        if(cart == null)
        {
            return;
        }

        m_cart = cart;
        m_startDistance = m_cart.SplineOffset;
    }



    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {


        SplineContainer mySpline = base.Spline;
        if (mySpline == null)
        {
            return;
        }



        float myLength = mySpline.CalculateLength();
        float gimmickPoint01 = GimmickPoint / myLength;
        float range01 = (GimmickPoint - m_pair.GimmickPoint)  / myLength;


        Gizmos.color = Color.green;
        int step = 20;
        float start = gimmickPoint01;
        float delta = range01 / step;
        for(int i = 0; i < step -1; i++)
        {
            Vector3 from = (Vector3)mySpline.EvaluatePosition(start - delta * i) + Vector3.right * 0.05f;
            Vector3 to = (Vector3)mySpline.EvaluatePosition(start - delta * (i + 1)) + Vector3.left * 0.05f;
            Gizmos.DrawLine(from, to);
        }


        Gizmos.color = Color.forestGreen;
        Gizmos.DrawSphere(mySpline.EvaluatePosition(start), 0.5f);
        Gizmos.DrawSphere(mySpline.EvaluatePosition(start - range01), 0.5f);

    }
}