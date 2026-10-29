using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ChestShaker : MonoBehaviour
{
    [Header("")]
    [SerializeField] private float m_shakeDuration = 0.3f;

    [Header("揺れの強さ")]
    [SerializeField] private float m_shakeStrength = 0.04f;

    [Header("揺れの速さ")]
    [SerializeField] private float m_shakeSpeed = 45f;

    [Header("タンスの傾き")]
    [SerializeField] private float m_tiltAngle = 5.0f;

   
    private Quaternion m_chestOriginalLocalRotation;  // タンスの元の回転
                                                      // 
    private Vector3 m_chestOriginalLocalPosition; // タンスの元のローカル座標

    private Coroutine m_chestShakeCoroutine; // 揺れのコルーチン


    void Update()
    {
        if (Keyboard.current != null &&
           Keyboard.current.tKey.wasPressedThisFrame)
        {
            StartChestShake();
        }
    }

    // タンスの揺れを開始する
    public void StartChestShake()
    {
        // 実行中の揺れを停止する
        if (m_chestShakeCoroutine != null)
        {
            StopCoroutine(m_chestShakeCoroutine);

            // 元の位置と回転に戻す
            transform.localPosition = m_chestOriginalLocalPosition;

            transform.localRotation = m_chestOriginalLocalRotation;

            m_chestShakeCoroutine = null;
        }

        // 揺れ始める位置と回転を保存する
        m_chestOriginalLocalPosition = transform.localPosition;

        m_chestOriginalLocalRotation = transform.localRotation;

        // 揺れを開始する
        m_chestShakeCoroutine = StartCoroutine(ChestShakeCoroutine());
    }


    // タンスを揺らしながら傾けるコルーチン
    private IEnumerator ChestShakeCoroutine()
    {
        float elapsed = 0f;

        while (elapsed < m_shakeDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / m_shakeDuration);

            // 時間経過とともに揺れを弱くする
            float strength = 1f - t;

            // 左右に揺れる動き
            float shakeX =
                Mathf.Sin(elapsed * m_shakeSpeed)
                * m_shakeStrength
                * strength;

            // 前後に少し揺れる動き
            float shakeZ =
                Mathf.Sin(elapsed * m_shakeSpeed * 1.3f)
                * m_shakeStrength
                * 0.35f
                * strength;

            // 左右に傾く角度
            float tilt =
                Mathf.Sin(elapsed * m_shakeSpeed)
                * m_tiltAngle
                * strength;

            // タンスの位置を変更する
            transform.localPosition =
                m_chestOriginalLocalPosition
                + new Vector3(shakeX, 0f, shakeZ);

            // タンスの回転を変更する
            transform.localRotation =
                m_chestOriginalLocalRotation
                * Quaternion.Euler(tilt, tilt, tilt);

            yield return null;
        }

        // 最後に元の位置と回転へ戻す
        transform.localPosition = m_chestOriginalLocalPosition;

        transform.localRotation = m_chestOriginalLocalRotation;

        m_chestShakeCoroutine = null;
    }

}
