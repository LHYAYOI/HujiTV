using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraShake : MonoBehaviour
{
    [SerializeField] CinemachineRotationShake m_cinemachineRotationShake;

    private Vector3 m_totalShakeOffset;

    void Update()
    {
        if(Keyboard.current.pKey.wasPressedThisFrame)
        {
            VHShake(0.5f, 0.5f,3.5f);
        }

        if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            ShakeCamera(3, 1.5f);
        }
    }

    IEnumerator ShakeRoutine(float duration, float horizontalIntensity, float verticalIntensity, Vector2 frequencyRange)
    {
        float elapsedTime = 0.0f;

        // ランダム位相
        float phaseX = Random.Range(0f, Mathf.PI * 2f);
        float phaseY = Random.Range(0f, Mathf.PI * 2f);
        float phaseZ = Random.Range(0f, Mathf.PI * 2f);

        // ランダム周波数
         float frequency = Random.Range(frequencyRange.x, frequencyRange.y);

        Vector3 previousShake = Vector3.zero;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float rate = elapsedTime / duration;

            // 減衰
            float damper = 1.0f - Easing.EaseOutCubic(rate);

            // 各軸の回転量を計算
            float xRotation = Mathf.Sin(elapsedTime * frequency + phaseX) * verticalIntensity * damper;
            float yRotation = Mathf.Cos(elapsedTime * frequency + phaseY) * horizontalIntensity * damper;

            Vector3 currentOffset = new Vector3(xRotation, yRotation, 0);

            m_totalShakeOffset -= previousShake; // 前回のオフセットを打ち消す

            m_totalShakeOffset += currentOffset; // 新しいオフセットを加える

            //回転を適用
            m_cinemachineRotationShake.SetRotationOffset(m_totalShakeOffset);

            previousShake = currentOffset; // 今回のオフセットを保存

            yield return null;
        }

        m_totalShakeOffset -= previousShake; // 最後のオフセットを打ち消す

        m_cinemachineRotationShake.SetRotationOffset(m_totalShakeOffset);
    }

    public void VHShake(float duration, float horizontalIntensity, float verticalIntensity) 
    {
        StartCoroutine(ShakeRoutine(duration, horizontalIntensity, verticalIntensity, new Vector2(25f, 80f)));
    }

    public void ShakeCamera(float intensity, float duration)
    {
        StartCoroutine(ShakeRoutine(duration, intensity, intensity, new Vector2(25f, 40f)));
    }
}
