using UnityEngine;

public class GimmickSpeedChanger : GimmickSpline
{
    [SerializeField] private GimmickSplineRange m_normalToTarget;
    [SerializeField] private GimmickSplineRange m_TargetToNormal;



    [SerializeField] private bool m_useBaseSpeed = true;
    [SerializeField] private float m_normalSpeed;

    [SerializeField] private float m_targetSpeed;


    private void Awake()
    {
        if (m_normalToTarget != null)
            m_normalToTarget.OnInRange += SpeedChangeNT;
        if (m_TargetToNormal != null)
            m_TargetToNormal.OnInRange += SpeedChangeTN;

    }
    private void OnDestroy()
    {
        if (m_normalToTarget != null)
            m_normalToTarget.OnInRange -= SpeedChangeNT;
        if (m_TargetToNormal != null)
            m_TargetToNormal.OnInRange -= SpeedChangeTN;

    }


    public void SpeedChangeNT(CartController cart , float rate)
    {
        float sign = Mathf.Sign(cart.Speed);

        float from = (m_useBaseSpeed ? cart.BaseSpeed : m_normalSpeed )* sign;
        float to = m_targetSpeed * sign;
        float spd = Mathf.Lerp(from, to, rate);
        cart.SetSpeed(spd);
    }
    public void SpeedChangeTN(CartController cart , float rate)
    {
        float sign = Mathf.Sign(cart.Speed);

        float from = m_targetSpeed * sign;
        float to = (m_useBaseSpeed ? cart.BaseSpeed : m_normalSpeed) * sign;
        float spd = Mathf.Lerp(from, to, rate);
        cart.SetSpeed(spd);
    }

}