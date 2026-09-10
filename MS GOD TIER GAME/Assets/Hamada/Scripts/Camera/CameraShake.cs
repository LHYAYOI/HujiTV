using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    private Quaternion m_startRotation;

    private void Start()
    {
        m_startRotation = gameObject.transform.localRotation;
    }

    void Update()
    {
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

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float rate = elapsedTime / duration;

            // Œ¸Š
            float damper = 1.0f - Easing.EaseOutCubic(rate);

            // Še²‚Ì‰ñ“]—Ê‚ğŒvZ
            float xRotation = Mathf.Sin(elapsedTime * freqX + phaseX) * intensity * damper;
            float yRotation = Mathf.Cos(elapsedTime * freqY + phaseY) * intensity * damper;

            //‰ñ“]‚ğ“K—p
            transform.rotation = m_startRotation * Quaternion.Euler(xRotation, yRotation, 0);

            yield return null;
        }

        // Š®‘S‚ÉŒ³‚ÌŠp“x‚É–ß‚·
        gameObject.transform.rotation = m_startRotation;
    }

    public void ShakeCamera(float intensity, float duration)
    {
        StartCoroutine(ShakeRoutine(duration, intensity));
    }
}
