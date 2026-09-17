using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class CameraZoom : MonoBehaviour
{
    [SerializeField] CinemachineCamera m_cinemachineCamera;
    [SerializeField] float m_defaultFOV = 60.0f;

    Coroutine m_FOVCoroutine;

    public void ChangeFOV(float duration, float FOVAmount)
    {
        if (m_FOVCoroutine != null)
        {
            StopCoroutine(m_FOVCoroutine);
        }

        m_FOVCoroutine = StartCoroutine(FOVCoroutine(duration, FOVAmount));
    }

    IEnumerator FOVCoroutine(float duration, float FOVAmount)
    {
        float elapsedTime = 0.0f;

        float startFOV = m_cinemachineCamera.Lens.FieldOfView;

        while (elapsedTime < duration)
        {
            float rate = Easing.EaseOutCubic(elapsedTime / duration);

            elapsedTime += Time.deltaTime;

            float newFOV = Mathf.Lerp(startFOV, FOVAmount, rate);

            SetFOV(newFOV);

            yield return null;
        }
    }

    public void SetFOV(float FOV)
    {
        if (m_cinemachineCamera == null) 
        {
            return;
        }

        m_cinemachineCamera.Lens.FieldOfView = FOV;
    }
}
