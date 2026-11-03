using UnityEngine;

public class GimmickSplineSetBattleCamera : GimmickSpline
{
    static private GimmickSplineSetBattleCamera m_settingScript;


    [SerializeField] private GimmickSplineRange m_range;
    
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
        m_settingScript = this;

        CameraManager.Instance.SwitchCamera("BattleCamera");
    }

    private void OnOutRange()
    {
        if(!m_isLookFlag)
        {
            return;
        }

        m_isLookFlag = false;


        if(m_settingScript == this)
        {
            m_settingScript = null;
            CameraManager.Instance.SwitchCamera("FPSCamera");
        }
    }




    private void OnDrawGizmos()
    {
    }
}