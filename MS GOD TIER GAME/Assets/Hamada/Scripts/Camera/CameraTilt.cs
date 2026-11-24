using System.Collections;
using UnityEngine;

public class CameraTilt : MonoBehaviour
{
    [SerializeField] CinemachineRotationExtension m_cinemachineRotationExtension;

    Vector3 m_tiltDegree = new Vector3(0, 0, 0); // 傾きの量

    Coroutine m_tiltCoroutine;

    public void Tilt(float duration, Vector3 TiltAmount)
    {
        if (m_tiltCoroutine != null)
        {
            StopCoroutine(m_tiltCoroutine);
        }

        m_tiltCoroutine = StartCoroutine(TiltCoroutine(duration, TiltAmount));
    }

    IEnumerator TiltCoroutine(float duration, Vector3 TiltAmount)
    {
        float elapsedTime = 0.0f;

        Vector3 startTilt = m_tiltDegree;

        while (elapsedTime < duration) 
        {
            float rate = Easing.EaseOutCubic(elapsedTime / duration);

            elapsedTime += Time.deltaTime;

            Vector3 newTilt = Vector3.Lerp(startTilt, TiltAmount, rate);

            SetTilt(newTilt);

            yield return null;
        }
    }

    public void SetTilt(Vector3 tiltDegree)
    {
        if (m_cinemachineRotationExtension == null) 
        {
            return;
        }

        m_tiltDegree = tiltDegree;
        m_cinemachineRotationExtension.SetTiltRotationOffset(m_tiltDegree);
    }
}
