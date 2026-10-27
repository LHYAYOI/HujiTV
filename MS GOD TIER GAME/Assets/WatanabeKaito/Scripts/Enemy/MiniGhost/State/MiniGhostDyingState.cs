using System.Collections;
using UnityEngine;


public class MiniGhostDyingState : MiniGhostState
{
    [Header("縮小にかかる時間（秒）")]
    [SerializeField] private float m_shrinkDuration = 2.0f;

    [Header("パーティクル")]
    [SerializeField] private GameObject m_deathParticle;

    private GameObject m_spawnedDeathParticle;  // 生成されたパーティクルの参照

    private Animator m_animator;    // Animatorコンポーネント
    private Coroutine m_shrinkCoroutine;    // 実行中の縮小コルーチン

    public override void InitializeState(MiniGhostEnemy owner)
    {
        base.InitializeState(owner);
        m_animator = owner.GetComponentInChildren<Animator>();
    }

    public override void EnterState()
    {
        if (m_animator != null)
        {
            m_animator.SetBool("bDead", true);
        }

        if (m_shrinkCoroutine != null)
        {
            StopCoroutine(m_shrinkCoroutine);
        }

        m_shrinkCoroutine = StartCoroutine(ShrinkCoroutine());

        // パーティクルを再生
        PlayDeathEffect(m_owner.transform.position);

        // 元のマテリアルから透明マテリアルに切り替える
        StartCoroutine(m_owner.SwitchMaterialBlendCoroutine(0, 1));
    }

    public override void ExitState()
    {
        if (m_shrinkCoroutine != null)
        {
            StopCoroutine(m_shrinkCoroutine);
            m_shrinkCoroutine = null;   
        }
    }

    // 縮小のコルーチン
    private IEnumerator ShrinkCoroutine()
    {
        Vector3 originalScale = m_owner.transform.localScale;
        float elapsed = 0f;

        if (m_shrinkDuration <= 0f)
        {
            m_owner.transform.localScale = Vector3.zero;
            Destroy(m_owner.gameObject);
            yield break;
        }

        while (elapsed < m_shrinkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / m_shrinkDuration);
            float easedT = Easing.EaseInSine(t);
            m_owner.transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, easedT);
            yield return null;
        }

        m_owner.transform.localScale = Vector3.zero;

        Destroy(m_spawnedDeathParticle);
        Destroy(m_owner.gameObject);
    }

    // パーティクルを再生する
    private void PlayDeathEffect(Vector3 position)
    {
        m_spawnedDeathParticle = Instantiate(m_deathParticle, position, Quaternion.identity);
    }
}
