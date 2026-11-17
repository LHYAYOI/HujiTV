using System.Collections;
using System.Runtime.CompilerServices;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.PlayerLoop;

public class CameraShake : MonoBehaviour
{
    [SerializeField] CinemachineBasicMultiChannelPerlin m_multiChannelPerlin;
    [SerializeField] CinemachineRotationExtension m_cinemachineRotationExtension;

    [Header("トロッコの揺れ周り")]
    [SerializeField] float m_amplitudeGain;
    [SerializeField] float m_frequencyGain;

    [Header("キック揺れ周り")]
    [SerializeField]
    private AnimationCurve m_kickCurve =
    new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 5f),
        new Keyframe(0.2f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, 0f, 0f)
    );

    private Vector3 m_totalShakeOffset;
    private Vector3 m_kickRotationOffset;

    Coroutine m_kickRoutine;

    private void Start()
    {
        m_multiChannelPerlin.AmplitudeGain = m_amplitudeGain;
        m_multiChannelPerlin.FrequencyGain = m_frequencyGain;
    }

    public void KickRotation(Vector3 targetAngle,float rotateDuration)
    {
        if (m_kickRoutine != null) 
        {
            StopCoroutine(m_kickRoutine);
        }

        m_kickRoutine = StartCoroutine(KickRotationRoutine(targetAngle, rotateDuration));
    }

    public void VHShake(float duration, float horizontalIntensity = 0, float verticalIntensity = 0, float forwardIntensity = 0)
    {
        StartCoroutine(ShakeRoutine(duration, horizontalIntensity, verticalIntensity, forwardIntensity, new Vector2(25f, 80f)));
    }

    public void ShakeCamera(float intensity, float duration)
    {
        StartCoroutine(ShakeRoutine(duration, intensity, intensity, intensity, new Vector2(25f, 40f)));
    }

    public void SetRateOnTrolleyShake(float rate)
    {
        rate = Mathf.Clamp01(rate);
        m_multiChannelPerlin.AmplitudeGain = m_amplitudeGain * rate;
        m_multiChannelPerlin.FrequencyGain = m_frequencyGain * rate;
    }

    IEnumerator ShakeRoutine(float duration, float horizontalIntensity, float verticalIntensity, float forwardIntensity, Vector2 frequencyRange)
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
            float zRotation = Mathf.Cos(elapsedTime * frequency + phaseY) * forwardIntensity * damper;

            Vector3 currentOffset = new Vector3(xRotation, yRotation, zRotation);

            m_totalShakeOffset -= previousShake; // 前回のオフセットを打ち消す

            m_totalShakeOffset += currentOffset; // 新しいオフセットを加える

            //回転を適用
            m_cinemachineRotationExtension.SetShakeRotationOffset(m_totalShakeOffset);

            previousShake = currentOffset; // 今回のオフセットを保存

            yield return null;
        }

        m_totalShakeOffset -= previousShake; // 最後のオフセットを打ち消す

        m_cinemachineRotationExtension.SetShakeRotationOffset(m_totalShakeOffset);
    }

    IEnumerator KickRotationRoutine(Vector3 targetAngle, float duration) 
    {
        if (duration <= 0f)
        {
            yield break;
        }

        float elapsedTime = 0f;

        // 前回の揺れが残っていた場合の開始オフセット
        Vector3 startOffset = m_kickRotationOffset;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float rate = Mathf.Clamp01(elapsedTime / duration);

            float curve = m_kickCurve.Evaluate(rate);

            // 前回のオフセットも徐々に解消
            float returnWeight = 0.5f + 0.5f * Mathf.Cos(Mathf.PI * rate);

            m_kickRotationOffset = startOffset * returnWeight + targetAngle * curve;

            m_cinemachineRotationExtension.SetKickRotationOffset(m_kickRotationOffset);

            yield return null;
        }

        // 最終的に完全にリセット
        m_kickRotationOffset = Vector3.zero;

        m_cinemachineRotationExtension.SetKickRotationOffset(Vector3.zero);

        m_kickRoutine = null;
    }
}