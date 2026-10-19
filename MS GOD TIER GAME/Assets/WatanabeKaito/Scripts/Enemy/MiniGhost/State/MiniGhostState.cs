using UnityEngine;

public abstract class MiniGhostState : MonoBehaviour
{
    protected MiniGhostEnemy m_owner;   // ステートを持つミニお化けの参照

    public virtual void InitializeState(MiniGhostEnemy owner)   // ステートの初期化処理
    {
        m_owner = owner;
    }

    public virtual void EnterState() { }    // ステートに入るときの処理
    public virtual void UpdateState() { }   // ステートの更新処理
    public virtual void ExitState() { }     // ステートから出るときの処理
}
