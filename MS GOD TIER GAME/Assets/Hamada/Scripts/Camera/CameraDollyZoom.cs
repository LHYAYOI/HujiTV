using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class CameraDollyZoom : MonoBehaviour
{
    [SerializeField] CinemachineCamera m_cinemachineCamera;
    [SerializeField] Transform m_positionAnchor;

    Coroutine m_coroutine;

    public void DollyCamera(float duration, float zoomValue, float pullBackAmount) 
    {
        if (m_coroutine != null) 
        {
            StopCoroutine(m_coroutine);
        }

        m_coroutine = StartCoroutine(DollyCameraRoutine(duration, zoomValue, pullBackAmount));
    }

    private IEnumerator DollyCameraRoutine(float duration, float zoomValue, float pullBackAmount) 
    {
        float elapsedTime = 0;

        float startZoomValue = m_cinemachineCamera.Lens.FieldOfView;
        Vector3 startPosition = m_cinemachineCamera.transform.position;

        while (elapsedTime < duration) 
        {
            elapsedTime += Time.deltaTime;

            float rate = Easing.EaseInOutCubic(elapsedTime / duration);

            Vector3 targetPosition = m_positionAnchor.position - m_cinemachineCamera.transform.forward * pullBackAmount;

            m_cinemachineCamera.Lens.FieldOfView = Mathf.Lerp(startZoomValue, zoomValue, rate);
            m_cinemachineCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, rate);

            yield return null;
        }
    }


}
