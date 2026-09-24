using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Splines;

public class SplineController2 : MonoBehaviour
{
    [SerializeField] private SplineContainer m_spline;
    [SerializeField] private bool m_loopFlag;
    
    private Gimmick[] m_gimmicks;


    public SplineContainer Spline => m_spline;
    public bool LoopFlag => m_loopFlag;


    private void Awake()
    {
        m_gimmicks = GetComponentsInChildren<Gimmick>(true);
    }

    public Gimmick[] Gimmicks => m_gimmicks;

}
