using UnityEngine;

public class TargetableObject : MonoBehaviour
{
    protected bool m_IsTargetableFlag = true;

    private void OnEnable()
    {
        if (TargetSelector.Instance != null) 
        {
            TargetSelector.Instance.RegisterTarget(this);
        }
    }

    private void OnDisable()
    {
        if (TargetSelector.Instance != null)
        {
            TargetSelector.Instance.UnregisterTarget(this);
        }
    }

    public void SetTargetable(bool value)
    {
        m_IsTargetableFlag = value;
    }

    public bool IsTargetable()
    {
        return m_IsTargetableFlag;
    }
}
