using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Encounter", menuName = "Scriptable Objects/Poltergeist/Encounter")]
public class Encount : StateBase
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
        RoutineRunner.instance.RunRoutine(EncounterRoutine());
    }

    public override void UpdateState()
    {

    }

    public override void ExitState()
    {

    }

    IEnumerator EncounterRoutine() 
    {
        //下から移動
        Vector3 objectPosition = m_stateOwner.transform.position;

        yield return m_controller.MoveAllPartsRoutine(new Vector3(0, 5, 0), 2);

        //アニメーション再生

        //アニメーション終わったら選択に移行
        m_controller.ChangeState("PreSelect");
    }
}
