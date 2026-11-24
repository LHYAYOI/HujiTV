using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "PreSelect", menuName = "Scriptable Objects/Poltergeist/PreSelect")]
public class PreSelect : StateBase
{
    PoltergeistController m_controller;

    public override void Awake()
    {

    }

    public override void Start()
    {
        m_controller = m_stateOwner.GetComponent<PoltergeistController>();
    }

    public override void EnterState()
    {
        RoutineRunner.instance.RunRoutine(PreSelectRoutine());
    }

    public override void UpdateState()
    {

    }

    public override void ExitState()
    {

    }

    IEnumerator PreSelectRoutine() 
    {
        //体全体を下に移動
        Vector3 objectPosition = m_stateOwner.transform.position;

        yield return m_controller.MoveAllPartsRoutine(new Vector3(0, -5, 0), 2);

        //３つの額縁を上に移動
        yield return m_controller.MoveUpperBody(new Vector3(0, 5, 0), 2);

        //アニメーション終わったら選択に移行
        m_controller.ChangeState("Select");
    }
}
