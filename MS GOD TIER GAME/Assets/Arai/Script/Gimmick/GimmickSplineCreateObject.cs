using UnityEngine;
using UnityEngine.Splines;

public class GimmickSplineCreateObject : GimmickSpline
{

    // ―――――――――――――――――――――――――――――――――――――
    // Propaty
    // ―――――――――――――――――――――――――――――――――――――

    [Tooltip("生成オブジェクト")]
    [SerializeField] private GameObject m_prefab;

    [SerializeField] private Transform m_instantiateTransform;
    [SerializeField] private float m_destroyTimer = 100.0f;

    // ―――――――――――――――――――――――――――――――――――――
    // Public Propaty
    // ―――――――――――――――――――――――――――――――――――――
    public void Create(HitData hitdata)
    {
        Destroy(
            Instantiate(m_prefab,
            m_instantiateTransform.position,
            m_instantiateTransform.rotation),
            m_destroyTimer);
       
    }

    
    // ―――――――――――――――――――――――――――――――――――――
    // Gizmos
    // ―――――――――――――――――――――――――――――――――――――
    private void OnDrawGizmos()
    {
        OnGizmosMyPosition();


        SplineContainer mySpline = base.Spline;
        if (mySpline == null)
        {
            return;
        }



        

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position,m_instantiateTransform.position);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(m_instantiateTransform.position, 0.3f);
    }

}
