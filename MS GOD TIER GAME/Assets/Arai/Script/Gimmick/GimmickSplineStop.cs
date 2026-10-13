using UnityEngine;

public class GimmickSplineStop : GimmickSpline
{

    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――
    private float m_enterSpeed = 0.0f;
    private CartController m_cart = null;

    private float m_recastTime = 0.5f;
    private float m_lastTime = 0.0f;

    // ―――――――――――――――――――――――――――――――――――――
    // Public Event
    // ―――――――――――――――――――――――――――――――――――――  
    public void StopHit(HitData hitdata)
    {
        if(Time.time - m_lastTime < m_recastTime)
        {
            return;
        }


        CartController cart = hitdata.GetHitObjectComponent<CartController>();


        if(cart == null)
        {
            return;
        }

        m_cart = cart;
        m_enterSpeed = m_cart.Speed;

        m_cart.SetSpeed(0.0f);
    }

    public void MoveHit(HitData hitdata)
    {
        Move();
    }

    public void Move()
    {
        m_cart.SetSpeed(m_enterSpeed);

        m_lastTime = Time.time;
    }



    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {
        OnGizmosMyPosition();


    }
}
