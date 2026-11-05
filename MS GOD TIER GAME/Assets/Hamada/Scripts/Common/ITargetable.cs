using UnityEngine;

public interface ITargetable
{
    abstract bool m_IsTargetable { get; set; }

    public void SetTargetable(bool value)
    {
        m_IsTargetable = value;
    }

    public bool IsTargetable()
    {
        return m_IsTargetable;
    }
}
