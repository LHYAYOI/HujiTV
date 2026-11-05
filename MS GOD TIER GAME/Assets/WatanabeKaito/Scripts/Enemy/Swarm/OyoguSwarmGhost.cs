using System.Collections;
using UnityEngine;

public class OyoguSwarmGhost : MonoBehaviour
{
    [Header("親のアニメーター")]
    [SerializeField] private Animator m_parentAnimator;

    [Header("死亡用アニメーター")]
    [SerializeField] private Animator m_deadAnimator;

    [Header("縮小にかかる時間（秒）")]
    [SerializeField] private float m_shrinkDuration = 1.0f;


    private void Awake()
    {
        m_deadAnimator.enabled = false;
    }

    public void DeadAnimation()
    {
        m_parentAnimator.enabled = false;
        m_deadAnimator.enabled = true;

        if (m_deadAnimator != null)
        {
            StartCoroutine(ShrinkCoroutine());
            m_deadAnimator.SetTrigger("tDead");
        }
    }

    // 縮小して消滅する
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

            float t = Mathf.Clamp01(
                elapsed / m_shrinkDuration
            );

            float easedT = Easing.EaseInSine(t);

            transform.localScale = Vector3.Lerp(
                originalScale,
                Vector3.zero,
                easedT
            );

            yield return null;
        }

        transform.localScale = Vector3.zero;

        Destroy(gameObject);
    }
}
