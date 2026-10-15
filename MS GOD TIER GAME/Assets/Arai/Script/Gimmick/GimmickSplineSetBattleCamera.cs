using UnityEngine;

public class GimmickSplineSetBattleCamera : GimmickSpline
{
    [SerializeField] private GimmickSplineRange m_range;
    [SerializeField] private GameObject m_lookTarget;

    private bool m_isLookFlag;


    private void Awake()
    {
        if(m_range != null)
        {
            m_range.OnInRange += OnInRange;
            m_range.OnOutRange += OnOutRange;
        }
    }
    private void OnDestroy()
    {
        if(m_range != null)
        {
            m_range.OnInRange -= OnInRange;
            m_range.OnOutRange -= OnOutRange;
        }
    }

    private void OnInRange(CartController cart , float rate)
    {
        if(m_isLookFlag)
        {
            return;
        }

        m_isLookFlag = true;

        CameraManager.Instance.SetLookTarget(m_lookTarget);
        CameraManager.Instance.SwitchCamera("BattleCamera");
    }

    private void OnOutRange()
    {
        if(!m_isLookFlag)
        {
            return;
        }

        m_isLookFlag = false;

        CameraManager.Instance.SwitchCamera("FPSCamera");
    }

}