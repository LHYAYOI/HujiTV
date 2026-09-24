using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class GimmickSplineOnOffReset : GimmickSpline
{

    [SerializeField] private bool m_onOff;
    [SerializeField] private List<Gimmick> m_targetGimmics = new();



    public override bool CanExecute(GimmickContext context)
    {
        float end = GimmickPoint;
        float start = end - 1.0f;

        // my distance
        float myPos = context.Cart.SplineOffset;


        return ExecuteFlag &&
            myPos < end &&
            myPos > start;
    }
    public override void Execute(GimmickContext context)
    {
        for(int i = 0; i < m_targetGimmics.Count; i++)
        {
            m_targetGimmics[i].ExecuteFlag = m_onOff;
        }
    }


    private void OnDrawGizmos()
    {
        OnGizmosOnOff();
        OnGizmosMyPosition();

        Gizmos.color = Color.skyBlue;
        Vector3 start = transform.position;

        foreach (var g in m_targetGimmics)
        {
            Vector3 direction = g.transform.position - start;
            Vector3 center = start + direction * 0.5f + Vector3.up * -10.0f;

            Gizmos.DrawLine(transform.position, center);
            Gizmos.DrawLine(center,g.transform.position);
            Gizmos.DrawSphere(g.transform.position, 0.3f);
        }
    }
}
