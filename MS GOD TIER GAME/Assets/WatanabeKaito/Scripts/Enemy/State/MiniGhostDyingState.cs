using System.Collections;
using UnityEngine;


public class MiniGhostDyingState : MiniGhostState
{
    [Header("èkè¨Ç…Ç©Ç©ÇÈéûä‘ÅiïbÅj")]
    [SerializeField] private float m_shrinkDuration = 2.0f;

    private Animator m_animator;
    private Coroutine m_shrinkCoroutine;

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
    }

    public override void ExitState()
    {
        if (m_shrinkCoroutine != null)
        {
            StopCoroutine(m_shrinkCoroutine);
            m_shrinkCoroutine = null;
        }
    }

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
        Destroy(m_owner.gameObject);
    }
}
