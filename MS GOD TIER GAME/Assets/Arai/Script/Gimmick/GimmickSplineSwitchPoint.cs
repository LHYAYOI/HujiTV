using UnityEngine;
using UnityEngine.Splines;

public class GimmickSplineSwitchPoint : GimmickSpline
{
    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――

    [Tooltip("乗り換え先のスプライン")]
    [SerializeField] private SplineController2 m_targetSplineController;

    [SerializeField] private float m_switchOffset;

    // ―――――――――――――――――――――――――――――――――――――
    // Public Propaty
    // ―――――――――――――――――――――――――――――――――――――
    public SplineController2 TargetSplineController { get { return m_targetSplineController; } }    
    public SplineContainer TargetSpline { get { return m_targetSplineController?.Spline; } }
    public float SwitchOffset { get { return m_switchOffset; } }


    // ―――――――――――――――――――――――――――――――――――――
    // Public Event
    // ―――――――――――――――――――――――――――――――――――――    
    public void Switch(HitData hitdata)
    {

        Debug.Log("C");

        CartController cart = hitdata.GetHitObjectComponent<CartController>();


        if(cart == null)
        {
            Debug.Log("null");
            return;
        }
        cart.SetSpline(m_targetSplineController.Spline, m_switchOffset, m_targetSplineController.LoopFlag);
    }

    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {
        OnGizmosMyPosition();


        SplineContainer mySpline = base.Spline;
        if(mySpline == null)
        {
            return;
        }



        float myLength = mySpline.CalculateLength();
        float gimmickPoint01 = GimmickPoint / myLength;
        Vector3 gimmickPosition = mySpline.EvaluatePosition(gimmickPoint01);


        // gimmick position --- switch position の描画
        Gizmos.color = Color.yellow;
        SplineContainer tarSpline = TargetSpline;
        if(tarSpline == null)
        {
            return;
        }

        float tarLength = tarSpline.CalculateLength();
        float switchOffset01 = m_switchOffset / tarLength;
        Vector3 switchOffsetPosition = tarSpline.EvaluatePosition(switchOffset01);

        Gizmos.DrawLine(gimmickPosition, switchOffsetPosition);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(switchOffsetPosition, 0.3f);
    }
}
