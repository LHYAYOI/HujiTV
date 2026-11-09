using LightShaftLab;
using System.Collections.Generic;
using UnityEngine;

public class GimmickSplineFogSetter : GimmickSpline
{
    [SerializeField] private List<LocalFogVolume> m_targetFog;

    [SerializeField] private GimmickSplineRange m_range;   
    [SerializeField] private Vector2 m_density;

    private void Awake()
    {
        if (m_range != null)
        {
            m_range.OnInRange += SetDensity;
            m_range.OnOutRange += OnOutRange;

        }
    }
    private void OnDestroy()
    {
        if (m_range != null)
        {
            m_range.OnInRange  -= SetDensity;
            m_range.OnOutRange -= OnOutRange;

        }
    }


    public void SetDensity(CartController cart, float rate)
    {
        foreach(var fog in m_targetFog)
        {
            fog.m_density = Mathf.Lerp(m_density.x,m_density.y, rate);
        }
    }

    public void OnOutRange()
    {
        foreach(var fog in m_targetFog)
        {
            fog.gameObject.SetActive(false);
        }
    }
}