using UnityEngine;

public class TargetableObject : MonoBehaviour
{
    [SerializeField] protected bool m_IsTargetableFlag = true;

    protected bool m_wasHit = true;

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

    private void LateUpdate()
    {
        SetWasHitFlag(false);
    }

    public void SetTargetable(bool value)
    {
        m_IsTargetableFlag = value;
    }

    public bool IsTargetable()
    {
        return m_IsTargetableFlag;
    }

    public void SetWasHitFlag(bool flag) 
    {
        m_wasHit = flag;
    }

    public bool WasHit() 
    {
        return m_wasHit;
    }
}
