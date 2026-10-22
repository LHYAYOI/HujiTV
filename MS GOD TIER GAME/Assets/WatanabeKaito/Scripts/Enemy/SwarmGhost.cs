using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class SwarmGhost : MonoBehaviour
{
    [SerializeField]
    private string[] m_chargeStates =
    {
        "Charge1",
        "Charge2",
        "Charge3",
        "Charge4"
    };

    [Header("突撃先")]
    [SerializeField] private Transform m_target;

    [Header("待ち時間（秒）")]
    [SerializeField] private float m_minDelay = 0f;
    [SerializeField] private float m_maxDelay = 4f;

    [Header("移動設定")]
    [SerializeField] private float m_moveSpeed = 5f;
    [SerializeField] private float m_stopDistance = 0.5f;

    [Header("縮小にかかる時間（秒）")]
    [SerializeField] private float m_shrinkDuration = 2.0f;

    [Header("パーティクル")]
    [SerializeField] private GameObject m_deathParticle;

    private GameObject m_spawnedDeathParticle;  // 生成されたパーティクルの参照

    private Coroutine m_shrinkCoroutine;    // 実行中の縮小コルーチン

    private Animator m_animator;

    private bool m_isDying = false;

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

        if (m_target == null && Camera.main != null)
        {
            m_target = Camera.main.transform;
        }
    }

    private void Start()
    {
        
    }

    private void Update()
    {
        // Spaceキーが押されたらリセット
        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            StartCharge();
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
            m_target == null ||
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

        if (m_target == null)
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

        // ターゲットに到達するまで移動
        while (m_target != null)
        {
            Vector3 direction = m_target.position - transform.position;

            float distance = direction.magnitude;

            if (distance <= m_stopDistance)
            {
                break;
            }

            direction.Normalize();

            transform.position += direction * m_moveSpeed * Time.deltaTime;

            yield return null;
        }

        m_chargeCoroutine = null;
    }

    public void SetTarget(Transform target)
    {
        m_target = target;
    }

    public void Die()
    {
        if (m_isDying)
        {
            return;
        }
        m_isDying = true;

        m_animator.SetTrigger("bDead");

        // 実行中の移動・待機処理を停止
        if (m_chargeCoroutine != null)
        {
            StopCoroutine(m_chargeCoroutine);
            m_chargeCoroutine = null;
        }
        // 縮小コルーチンを開始
        if (m_shrinkCoroutine != null)
        {
            StopCoroutine(m_shrinkCoroutine);
        }
        m_shrinkCoroutine = StartCoroutine(ShrinkCoroutine());
        // パーティクルを再生
        PlayDeathEffect(transform.position);
    }

    // 縮小のコルーチン
    private IEnumerator ShrinkCoroutine()
    {
        Vector3 originalScale = transform.localScale;
        float elapsed = 0f;

        if (m_shrinkDuration <= 0f)
        {
            transform.localScale = Vector3.zero;
            Destroy(gameObject);
            yield break;
        }

        while (elapsed < m_shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / m_shrinkDuration);
            float easedT = Easing.EaseInSine(t);
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, easedT);
            yield return null;
        }

        transform.localScale = Vector3.zero;

        Destroy(m_spawnedDeathParticle);
        Destroy(gameObject);
    }

    // パーティクルを再生する
    private void PlayDeathEffect(Vector3 position)
    {
        m_spawnedDeathParticle = Instantiate(m_deathParticle, position, Quaternion.identity);

        // パーティクルのサイズを半分にする
        m_spawnedDeathParticle.transform.localScale = m_spawnedDeathParticle.transform.localScale / 2;
    }
}
