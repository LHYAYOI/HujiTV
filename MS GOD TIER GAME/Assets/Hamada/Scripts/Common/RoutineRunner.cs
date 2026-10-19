using System.Collections;
using UnityEngine;

public class RoutineRunner : MonoBehaviour
{

    public static RoutineRunner instance { get; set; }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        instance = this;
    }

    public Coroutine RunRoutine(IEnumerator routine)
    {
        return StartCoroutine(routine);
    }

    public void StopRoutine(Coroutine routine)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }
    }
}
