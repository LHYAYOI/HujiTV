using UnityEngine;

public class GimmickSpeedChanger : GimmickSpline
{
    [SerializeField] private GimmickSplineRange m_normalToTarget;
    [SerializeField] private GimmickSplineRange m_TargetToNormal;



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
        float from = cart.BaseSpeed;
        float to = m_targetSpeed;
        float spd = Mathf.Lerp(from, to, rate);
        cart.SetSpeed(spd);
    }
    public void SpeedChangeTN(CartController cart , float rate)
    {
        float from = m_targetSpeed;
        float to = cart.BaseSpeed;
        float spd = Mathf.Lerp(from, to, rate);
        cart.SetSpeed(spd);
    }

}