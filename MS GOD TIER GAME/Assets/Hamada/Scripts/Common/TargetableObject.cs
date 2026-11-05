using UnityEngine;

public class TargetableObject : MonoBehaviour
{
    protected bool m_IsTargetableFlag = true;

    public void SetTargetable(bool value)
    {
        m_IsTargetableFlag = value;
    }

    public bool IsTargetable()
    {
        return m_IsTargetableFlag;
    }
}
