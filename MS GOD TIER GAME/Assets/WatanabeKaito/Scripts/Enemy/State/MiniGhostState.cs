using UnityEngine;

public abstract class MiniGhostState : MonoBehaviour
{
    protected MiniGhostEnemy m_owner;

    public virtual void InitializeState(MiniGhostEnemy owner)
    {
        m_owner = owner;
    }

    public virtual void EnterState() { }
    public virtual void UpdateState() { }
    public virtual void ExitState() { }
}
