using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class SwarmMiniGhostTest : MonoBehaviour
{
    private Animator m_animator;

    [SerializeField]
    private string[] m_chargeStates =
    {
        "Charge1",
        "Charge2",
        "Charge3",
        "Charge4"
    };

    [Header("突撃先")]
    [SerializeField] private Transform m_targetCamera;

    [Header("待ち時間（秒）")]
    [SerializeField] private float m_minDelay = 0f;
    [SerializeField] private float m_maxDelay = 4f;

    [Header("移動設定")]
    [SerializeField] private float m_moveSpeed = 5f;
    [SerializeField] private float m_stopDistance = 0.5f;

    // 初期位置と初期回転を保存
    private Vector3 m_initialPosition;
    private Quaternion m_initialRotation;

    // 実行中のコルーチン
    private Coroutine m_chargeCoroutine;

    private void Awake()
    {
        // シーン開始時の位置を保存
        m_initialPosition = transform.position;
        m_initialRotation = transform.rotation;

        if (m_animator == null)
        {
            m_animator = GetComponentInChildren<Animator>();
        }

        if (m_targetCamera == null && Camera.main != null)
        {
            m_targetCamera = Camera.main.transform;
        }
    }

    private void Start()
    {
        StartCharge();
    }

    private void Update()
    {
        // Spaceキーが押されたらリセット
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ResetAndStart();
        }
    }

    private void ResetAndStart()
    {
        // 実行中の移動・待機処理を停止
        if (m_chargeCoroutine != null)
        {
            StopCoroutine(m_chargeCoroutine);
            m_chargeCoroutine = null;
        }

        // 初期位置・初期回転に戻す
        transform.SetPositionAndRotation(
            m_initialPosition,
            m_initialRotation
        );

        // アニメーションを初期状態に戻す
        if (m_animator != null)
        {
            m_animator.Rebind();
            m_animator.Update(0f);
        }

        // ランダムな待ち時間で再スタート
        StartCharge();
    }

    private void StartCharge()
    {
        m_chargeCoroutine = StartCoroutine(ChargeRoutine());
    }

    private IEnumerator ChargeRoutine()
    {
        if (m_animator == null ||
            m_targetCamera == null ||
            m_chargeStates == null ||
            m_chargeStates.Length == 0)
        {
            yield break;
        }

        // 待ち時間とアニメーションを抽選
        float delay = Random.Range(m_minDelay, m_maxDelay);
        int index = Random.Range(0, m_chargeStates.Length);

        // 抽選した時間だけ待機
        yield return new WaitForSeconds(delay);

        if (m_targetCamera == null)
        {
            yield break;
        }

        // 選ばれた突撃アニメーションを再生
        m_animator.CrossFadeInFixedTime(
         $"Base Layer.{m_chargeStates[index]}",
         0.3f,
         0,
         0f
     );

        yield return new WaitForSeconds(0.5f); // アニメーションの再生を少し待つ

        // カメラに到達するまで移動
        while (m_targetCamera != null)
        {
            Vector3 direction =
                m_targetCamera.position - transform.position;

            float distance = direction.magnitude;

            if (distance <= m_stopDistance)
            {
                break;
            }

            direction.Normalize();

            transform.position +=
                direction * m_moveSpeed * Time.deltaTime;

            yield return null;
        }

        m_chargeCoroutine = null;
    }
}
