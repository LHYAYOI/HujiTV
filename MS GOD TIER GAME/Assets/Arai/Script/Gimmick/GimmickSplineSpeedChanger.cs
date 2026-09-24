using System.Collections.Generic;
using UnityEngine;

public class GimmickSplineSpeedChanger : GimmickSpline
{

    [SerializeField] private float m_speed;
    [SerializeField] private float m_time;


    public override void Execute(GimmickContext context)
    {
        context.SetCartSpeed(m_speed, m_time);

        // 切る
        ExecuteFlag = false;
    }


    private void OnDrawGizmos()
    {
        OnGizmosOnOff();
        OnGizmosMyPosition();        
    }
}
