
using UnityEngine;
using UnityEngine.Splines;

public class SplineController2 : MonoBehaviour
{
    [SerializeField] private SplineContainer m_spline;
    [SerializeField] private bool m_loopFlag;
    


    public SplineContainer Spline => m_spline;
    public bool LoopFlag => m_loopFlag;



}
