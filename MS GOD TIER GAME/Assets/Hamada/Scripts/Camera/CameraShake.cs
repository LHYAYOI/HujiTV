using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraShake : MonoBehaviour
{
    [SerializeField] CinemachineRotationShake m_cinemachineRotationShake;

    void Update()
    {
        if(Keyboard.current.pKey.wasPressedThisFrame)
        {
            TorokkoVHShake(0.5f, 0.5f,1.5f);
        }
    }

    IEnumerator ShakeRoutine(float duration,float intensity)
    {
        float elapsedTime = 0.0f;

        // ƒ‰ƒ“ƒ_ƒ€ˆÊ‘Š
        float phaseX = Random.Range(0f, Mathf.PI * 2f);
        float phaseY = Random.Range(0f, Mathf.PI * 2f);
        float phaseZ = Random.Range(0f, Mathf.PI * 2f);

        // ƒ‰ƒ“ƒ_ƒ€ü”g”
        float freqX = Random.Range(25f, 40f);
        float freqY = Random.Range(25f, 40f);

        Quaternion m_startQuaternion = Quaternion.LookRotation(transform.forward, transform.up);

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float rate = elapsedTime / duration;

            // Œ¸Š
            float damper = 1.0f - Easing.EaseOutCubic(rate);

            // Še²‚Ì‰ñ“]—Ê‚ğŒvZ
            float xRotation = Mathf.Sin(elapsedTime * freqX + phaseX) * intensity * 2 * damper;
            float yRotation = Mathf.Cos(elapsedTime * freqY + phaseY) * intensity * damper;

            //‰ñ“]‚ğ“K—p
            m_cinemachineRotationShake.SetRotationOffset(new Vector3(xRotation, yRotation, 0));

            yield return null;
        }

        // Š®‘S‚ÉŒ³‚ÌŠp“x‚É–ß‚·
        m_cinemachineRotationShake.ResetOffset();
    }

    IEnumerator ShakeVHRoutine(float duration, float horizontalIntensity, float verticalIntensity)
    {
        float elapsedTime = 0.0f;

        // ƒ‰ƒ“ƒ_ƒ€ˆÊ‘Š
        float phaseX = Random.Range(0f, Mathf.PI * 2f);
        float phaseY = Random.Range(0f, Mathf.PI * 2f);
        float phaseZ = Random.Range(0f, Mathf.PI * 2f);

        // ƒ‰ƒ“ƒ_ƒ€ü”g”
        float freqX = Random.Range(25f, 80f);
        float freqY = Random.Range(25f, 80f);

        Quaternion m_startQuaternion = Quaternion.LookRotation(transform.forward, transform.up);

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float rate = elapsedTime / duration;

            // Œ¸Š
            float damper = 1.0f - Easing.EaseOutCubic(rate);

            // Še²‚Ì‰ñ“]—Ê‚ğŒvZ
            float xRotation = Mathf.Sin(elapsedTime * freqX + phaseX) * verticalIntensity * 2 * damper;
            float yRotation = Mathf.Cos(elapsedTime * freqY + phaseY) * horizontalIntensity * .5f * damper;

            //‰ñ“]‚ğ“K—p
            m_cinemachineRotationShake.SetRotationOffset(new Vector3(xRotation, yRotation, 0));

            yield return null;
        }

        // Š®‘S‚ÉŒ³‚ÌŠp“x‚É–ß‚·
        m_cinemachineRotationShake.ResetOffset();
    }

    public void TorokkoVHShake(float duration, float horizontalIntensity, float verticalIntensity) 
    {
        StartCoroutine(ShakeVHRoutine(duration, horizontalIntensity, verticalIntensity));
    }

    public void ShakeCamera(float intensity, float duration)
    {
        StartCoroutine(ShakeRoutine(duration, intensity));
    }
}
