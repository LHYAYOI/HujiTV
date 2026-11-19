using System.Collections;
using UnityEngine;

public class Odoriba : MonoBehaviour
{
    [SerializeField] private GimmickSplineStop m_stop;
    [SerializeField] private float m_time;

    


    
    public void OdoribaStart ()
    {
        StartCoroutine(OdoribaCoroutine());
    }

    private void OdoribaEnd()
    {
        m_stop.Move();
    }

    private  IEnumerator OdoribaCoroutine()
    {
        yield return new WaitForSeconds(m_time);

        OdoribaEnd();
    }
}
