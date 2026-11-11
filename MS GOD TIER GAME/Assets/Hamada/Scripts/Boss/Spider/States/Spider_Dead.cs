using UnityEngine;

[CreateAssetMenu(fileName = "Spider_Dead", menuName = "Scriptable Objects/Spider_Dead")]
public class Spider_Dead : StateBase
{
    public override void Awake()
    {

    }

    public override void Start()
    {
    }

    public override void EnterState()
    {
        m_stateOwner.GetComponent<SpiderController>().GetAnimator().SetTrigger("Dead");
    }

    public override void UpdateState()
    {

    }

    public override void ExitState()
    {

    }
}
