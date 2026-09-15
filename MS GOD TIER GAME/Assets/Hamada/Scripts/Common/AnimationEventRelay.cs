using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public class EVENT_DATA 
{
    [SerializeField] string m_eventName;
    [SerializeField] UnityEvent onAnimationEvent;

    public UnityEvent GetEvents() 
    {
        return onAnimationEvent;
    }

    public string GetEventName() 
    {
        return m_eventName;
    }
}

public class AnimationEventRelay : MonoBehaviour
{
    [SerializeField] List<EVENT_DATA> onAnimationEvent;

    //ここでイベントを名前で指定
    public void TriggerEvent(string eventName) 
    {
        EVENT_DATA processEvent = null;

        foreach (EVENT_DATA data in onAnimationEvent) 
        {
            if (data.GetEventName() == eventName) 
            {
                processEvent = data;
                break;
            }
        }

        if (processEvent == null) 
        {
            return;
        }

        processEvent.GetEvents()?.Invoke();
    }
}
