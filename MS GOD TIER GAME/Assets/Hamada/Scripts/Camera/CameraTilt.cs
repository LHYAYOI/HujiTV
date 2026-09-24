using System.Collections;
using UnityEngine;

public class CameraTilt : MonoBehaviour
{
    [SerializeField] CinemachineRotationExtension m_cinemachineRotationExtension;

    float m_tiltDegree = 0.0f; // ŒX‚«‚Ì—Ê

    Coroutine m_tiltCoroutine;

    public void Tilt(float duration, float TiltAmount)
    {
        if (m_tiltCoroutine != null)
        {
            StopCoroutine(m_tiltCoroutine);
        }

        m_tiltCoroutine = StartCoroutine(TiltCoroutine(duration, TiltAmount));
    }

    IEnumerator TiltCoroutine(float duration, float TiltAmount)
    {
        float elapsedTime = 0.0f;

        float startTilt = m_tiltDegree;

        while (elapsedTime < duration) 
        {
            float rate = Easing.EaseOutCubic(elapsedTime / duration);

            elapsedTime += Time.deltaTime;

            float newTilt = Mathf.Lerp(startTilt, TiltAmount, rate);

            SetTilt(newTilt);

            yield return null;
        }
    }

    public void SetTilt(float tiltDegree)
    {
        if (m_cinemachineRotationExtension == null) 
        {
            return;
        }

        m_tiltDegree = tiltDegree;
        m_cinemachineRotationExtension.SetTiltRotationOffset(new Vector3(0, 0, m_tiltDegree));
    }
}
