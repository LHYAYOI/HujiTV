using System.Collections;
using UnityEngine;
using UnityEngine.Splines;

public class CameraLookTarget : MonoBehaviour
{
    private Transform m_target;
    private Coroutine m_coroutine;


    private void Awake()
    {
        m_target = new GameObject("target").transform;
        m_target.transform.SetParent(transform);
        m_target.transform.position = transform.position;

    }
    private void Start()
    {
        CameraManager.Instance.SwitchCamera("FPSCamera");
    }


    // 引数座標を見る
    public void LookAt(Vector3 position, float time)
    {
        if(time == 0.0f)
        {
            var cameraData = CameraManager.Instance.GetCurrentCamera();
            var camera = cameraData.GetCamera();
            m_target.position = position;
            cameraData.GetCamera().LookAt = m_target;

            return;
        }

        if(m_coroutine != null)
        {
            StopCoroutine(m_coroutine);
        }


        m_coroutine = StartCoroutine(LookAtRoutine(position, time));

    }

    private IEnumerator LookAtRoutine(Vector3 position , float time)
    {
        var cameraData = CameraManager.Instance.GetCurrentCamera();        
        var camera = cameraData.GetCamera();
        

        Vector3 start = camera.transform.position + camera.transform.forward;
        Vector3 end = position;


        m_target.position = start;
        cameraData.GetCamera().LookAt = m_target;


        for(float t = 0.0f; t < time; t += Time.deltaTime)
        {
            float value = t / time;
            m_target.position = Vector3.Lerp(start,end,value);

            yield return null;
        }


        m_target.position = end;
    }
}
