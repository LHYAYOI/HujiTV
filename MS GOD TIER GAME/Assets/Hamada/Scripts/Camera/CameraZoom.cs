using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class CameraZoom : MonoBehaviour
{
    [SerializeField] CinemachineCamera m_cinemachineCamera;

    Coroutine m_FOVCoroutine;

    public void ChangeFOV(float duration, float FOVAmount)
    {
        if (m_FOVCoroutine != null)
        {
            StopCoroutine(m_FOVCoroutine);
        }

        m_FOVCoroutine = StartCoroutine(FOVCoroutine(duration, FOVAmount));
    }

    public void ChangeFOV010(float duration, float maxFOV)
    {
        if (m_FOVCoroutine != null)
        {
            StopCoroutine(m_FOVCoroutine);
        }

        m_FOVCoroutine = StartCoroutine(FOV010Coroutine(duration, maxFOV));
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

    IEnumerator FOV010Coroutine(float duration, float maxFOV) 
    {
        float elapsedTime = 0.0f;

        float startFOV = m_cinemachineCamera.Lens.FieldOfView;

        while (elapsedTime < duration)
        {

            float normalizedRate = Easing.EaseOutQuint(elapsedTime / duration);

            float rate = (1 - Mathf.Cos(normalizedRate * 2 * Mathf.PI)) * 0.5f;

            elapsedTime += Time.deltaTime;

            float newFOV = Mathf.Lerp(startFOV, maxFOV, rate);

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
