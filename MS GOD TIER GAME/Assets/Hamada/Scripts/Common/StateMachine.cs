//
//ステートマシン
//濱田ルイス
//

using System.Collections.Generic;
using UnityEngine;

public class StateMachine : MonoBehaviour
{
    [SerializeReference] List<StateBase> m_stateList;

    StateBase m_previousState;
    StateBase m_currentState;

    private void Awake()
    {
        foreach (StateBase state in m_stateList)
        {
            state.SetStateMachine(this);
            state.SetStateOwner(this.gameObject);
            state.Awake();
        }
    }

    private void Start()
    {
        foreach (StateBase state in m_stateList)
        {
            state.Start();
        }
    }

    public void Update() 
    {
        if (m_currentState != null)
        {
            m_currentState.UpdateState();
        }
    }

    //ステート切り替え
    public void ChangeState(string stateName)
    {
        if (m_currentState != null)
        {
            m_currentState.ExitState();
        }

        //名前と一致したものと入れ替え
        foreach (StateBase state in m_stateList)
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
    public void ChangeState(int stateIndex)
    {
        if (stateIndex >= m_stateList.Count || stateIndex < 0) 
        {
            return;
        }

        if (m_currentState != null)
        {
            m_currentState.ExitState();
        }

        SwapState(m_stateList[stateIndex]);

        if (m_currentState != null)
        {
            m_currentState.EnterState();
        }
    }

    //前のステートに移行
    public void ChangeToPreviousState()
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

    //次の要素番号のステートに移行
    public void ChangeToNextIndexState() 
    {
        int currentIndex = m_stateList.IndexOf(m_currentState);
        int nextIndex = currentIndex + 1;

        if (nextIndex >= m_stateList.Count || m_stateList[currentIndex + 1] == null) 
        {
            return;
        }

        if (m_currentState != null)
        {
            m_currentState.ExitState();
        }

        SwapState(m_stateList[nextIndex]);

        if (m_currentState != null)
        {
            m_currentState.EnterState();
        }
    }

    //前の要素番号のステートに移行
    public void ChangeToPreviousIndexState()
    {
        int currentIndex = m_stateList.IndexOf(m_currentState);
        int previousIndex = currentIndex - 1;

        if (m_stateList[currentIndex - 1] == null)
        {
            return;
        }

        if (m_currentState != null)
        {
            m_currentState.ExitState();
        }

        SwapState(m_stateList[previousIndex]);

        if (m_currentState != null)
        {
            m_currentState.EnterState();
        }
    }

    public StateBase GetCurrentState() { return m_currentState; }

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

    //指定したステートを取得

    public T GetState<T>() where T : StateBase
    {
        foreach (StateBase state in m_stateList)
        {
            if (state is T)
            {
                return state as T;
            }
        }
        return null;
    }
}
