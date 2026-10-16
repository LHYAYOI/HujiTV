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

    public void RunRoutine(IEnumerator routine)
    {
        StartCoroutine(routine);
    }
}
