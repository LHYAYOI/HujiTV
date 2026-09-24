using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

public class GimmickSplineOnOffChanger : GimmickSpline
{

    [SerializeField] private bool m_once;
    [SerializeField] private float m_range;
    [SerializeField] private GimmickSpline m_target;


    public float Range { get { return m_range; } set { m_range = value; } }
    public GimmickSpline Target { get {return m_target;}}




    // ―――――――――――――――――――――――――――――――――――――
    // Public Event
    // ―――――――――――――――――――――――――――――――――――――    
    public override bool CanExecute(GimmickContext context)
    {
        float end = GimmickPoint;
        float start = end - m_range;

        // my distance
        float myPos = context.Cart.SplineOffset;


        return ExecuteFlag &&
            myPos < end &&
            myPos > start;

    }

    public override void Execute(GimmickContext context)    
    {
        if(m_target == null)
        {
            return;
        }

        if(Keyboard.current.spaceKey.wasPressedThisFrame)
        {

            bool b = m_target.ExecuteFlag;
            m_target.ExecuteFlag = !b;


            if (m_once)
            {
                ExecuteFlag = false;
            }
        }
    }


    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {
        OnGizmosOnOff();
        OnGizmosMyPosition();


        SplineContainer mySpline = base.Spline;
        if (mySpline == null)
        {
            return;
        }


        // gimmick Position
        float myLength = mySpline.CalculateLength();
        float gimmickPoint01 = GimmickPoint / myLength;
        Vector3 gimmickPosition = transform.position;

        Gizmos.color = Color.green;



        // gimmick position -- range の描画
        // 曲線に沿ってLine描画
        float rangeStart01 = (GimmickPoint - m_range) / myLength;
        float range01 = gimmickPoint01 - rangeStart01;
        int lineStep = 20;
        float rangeOneStep01 = range01 / lineStep;

        for (int i = 0; i < lineStep - 1; i++)
        {
            float a = rangeStart01 + rangeOneStep01 * i;
            Vector3 aPosition = (Vector3)mySpline.EvaluatePosition(a) + Vector3.right * -0.05f;

            float b = rangeStart01 + rangeOneStep01 * (i + 1);
            Vector3 bPosition = (Vector3)mySpline.EvaluatePosition(b) + Vector3.right * 0.05f;

            Gizmos.DrawLine(aPosition, bPosition);
        }

        // 範囲の始まりの丸
        Vector3 rangeStartPosition = mySpline.EvaluatePosition(rangeStart01);
        Gizmos.DrawSphere(rangeStartPosition, 0.3f);



        // target ---- gimmick position
        SplineContainer tarSpline = m_target?.Spline;
        if(tarSpline == null)
        {
            return;
        }

        float tarLength = tarSpline.CalculateLength();
        float tarOffset01 = m_target.GimmickPoint / tarLength;
        Vector3 tarPosition = tarSpline.EvaluatePosition(tarOffset01);

        Gizmos.color = Color.darkGreen;
        Gizmos.DrawLine(gimmickPosition+Vector3.right*-0.05f, tarPosition + Vector3.right * 0.05f);

    }
}
