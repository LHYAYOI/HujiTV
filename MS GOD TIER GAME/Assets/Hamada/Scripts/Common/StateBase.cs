//
//ステート基底クラス
//濱田ルイス
//

using System.Collections.Generic;
using UnityEngine;

public class StateBase : ScriptableObject
{
    [SerializeField] protected string m_stateName;
    [SerializeReference] List<StateBase> m_subStateList;
    protected StateBase m_parentState = null;
    protected StateMachine m_stateMachine = null;
    protected GameObject m_stateOwner = null;
    protected float m_stateTime = 0.0f;
    protected StateBase m_previousState;
    protected StateBase m_currentState;

    public void SetStateMachine(StateMachine stateMachine) { m_stateMachine = stateMachine; }
    public void SetStateOwner(GameObject gameObject) { m_stateOwner = gameObject; }
    public void SetParentState(StateBase state) { m_parentState = state; }
    public string GetStateName() { return m_stateName; }

    public virtual void Awake() 
    {
        foreach (StateBase subState in m_subStateList)
        {
            if (subState == null)
            {
                continue;
            }

            subState.SetStateMachine(m_stateMachine);
            subState.SetStateOwner(m_stateOwner);
            subState.Awake();
        }
    }

    public virtual void Start() 
    {
        foreach (StateBase subState in m_subStateList)
        {
            if (subState != null)
            {
                subState.Start();
            }
        }
    }

    public virtual void EnterState() { }

    public virtual void UpdateState() 
    {
        if (m_currentState != null)
        {
            m_currentState.UpdateState();
        }
    }

    public virtual void ExitState() 
    {
        if (m_currentState != null) 
        {
            m_currentState.ExitState();
            m_currentState = null;
        }
    }

    //ステート切り替え
    public void ChangeSubState(string stateName)
    {
        if (m_currentState != null)
        {
            m_currentState.ExitState();
        }

        //名前と一致したものと入れ替え
        foreach (StateBase state in m_subStateList)
        {
            if (state != null)
            {
                if (state.GetStateName() == stateName)
                {
                    SwapState(state);

                    break;
                }
            }
        }

        if (m_currentState != null)
        {
            m_currentState.EnterState();
        }
    }

    //ステート切り替え（要素番号で指定）
    public void ChangeSubState(int stateIndex)
    {
        if (stateIndex >= m_subStateList.Count || stateIndex < 0)
        {
            return;
        }

        if (m_currentState != null)
        {
            m_currentState.ExitState();
        }

        SwapState(m_subStateList[stateIndex]);

        if (m_currentState != null)
        {
            m_currentState.EnterState();
        }
    }

    //前のステートに移行
    public void ChangeToPreviousSubState()
    {
        if (m_currentState != null)
        {
            m_currentState.ExitState();
        }

        SwapState(m_previousState);

        //今（前の）ステートを開始
        if (m_currentState != null)
        {
            m_currentState.EnterState();
        }
    }

    //ステートを切り替える
    private void SwapState(StateBase state)
    {
        if (m_currentState == state)
        {
            return;
        }

        StateBase tempStateHolder = m_currentState;

        m_currentState = state;

        m_previousState = tempStateHolder;
    }
}
