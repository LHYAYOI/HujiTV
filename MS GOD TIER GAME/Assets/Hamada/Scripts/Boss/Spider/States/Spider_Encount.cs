using UnityEngine;

[CreateAssetMenu(fileName = "Spider_Encount", menuName = "Scriptable Objects/Spider_Encount")]
public class Spider_Encount : StateBase
{
    SpiderController m_controller;

    public override void Awake()
    {

    }

    public override void Start()
    {
        m_controller = m_stateOwner.GetComponent<SpiderController>();
    }

    public override void EnterState()
    {
        
    }

    public override void UpdateState()
    {
        //アニメーション終わったらアイドルに戻る
        if (m_controller.IsAnimationFinished("Encount"))
        {
            Debug.Log("エンカウント終わり");

            m_controller.ChangeState("Idle_State");
        }
    }

    public override void ExitState()
    {

    }
}
