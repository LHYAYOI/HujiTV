using System.Collections;
using UnityEngine;

public class IrisTransition : SceneTransition
{
    [Header("マテリアル")]
    [SerializeField] private Material m_material;

    [Header("フェードイン時間")]
    [SerializeField] private float m_fadeInTime = 0.5f;

    [Header("フェードアウト時間")]
    [SerializeField] private float m_fadeOutTime = 0.5f;

    // 遷移の種類を取得
    public override TRANSITION_TYPE Type
    {
        get { return TRANSITION_TYPE.IRIS; }
    }

    // シーン遷移開始前
    public override IEnumerator PlayOut()
    {
        float time = 0.0f;

        while (time < m_fadeOutTime)
        {
            time += Time.deltaTime;

            float rate = Mathf.Clamp01(time / m_fadeOutTime);

            SeyFloatProgress(1.0f - rate);

            yield return null;
        }

        SeyFloatProgress(0.0f);

        yield return null;
    }

    // シーン遷移終了後
    public override IEnumerator PlayIn()
    {
        float time = 0.0f;

        while (time < m_fadeInTime)
        {
            time += Time.deltaTime;

            float rate = Mathf.Clamp01(time / m_fadeInTime);

            SeyFloatProgress(rate);

            yield return null;
        }

        SeyFloatProgress(1.0f);

        yield return null;
    }

    private void SeyFloatProgress(float progress)
    {
        progress = Mathf.Clamp(progress, 0.0f, 1.0f);

        m_material.SetFloat("_Progress", progress);
    }

   
}
