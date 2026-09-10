//-----------------------------------------------
// FadeTransition.cs
// 制作日：2026/09/10
// 制作者：渡辺開斗
// 概要：フェード遷移のクラス
//-----------------------------------------------
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FadeTransition : SceneTransition
{
    [Header("フェード用Image")]
    [SerializeField] private Image m_fadeImage;

    [Header("フェードイン時間")]
    [SerializeField] private float m_fadeInTime = 0.5f;

    [Header("フェードアウト時間")]
    [SerializeField] private float m_fadeOutTime = 0.5f;

    // 遷移の種類を取得
    public override TRANSITION_TYPE Type
    {
        get { return TRANSITION_TYPE.FADE; }
    }

    // シーン遷移開始前
    public override IEnumerator PlayOut()
    {
        float time = 0.0f;

        while (time < m_fadeOutTime)
        {
            time += Time.deltaTime;

            float rate = time / m_fadeOutTime;

            SetFadeAlpha(rate);

            yield return null;
        }

        SetFadeAlpha(1.0f);
    }

    // シーン遷移終了後
    public override IEnumerator PlayIn()
    {
        float time = 0.0f;

        while (time < m_fadeInTime)
        {
            time += Time.deltaTime;

            float rate = time / m_fadeInTime;

            SetFadeAlpha(1.0f - rate);

            yield return null;
        }

        SetFadeAlpha(0.0f);
    }

    // α値を設定する
    public void SetFadeAlpha(float alpha)
    {
        Color color = m_fadeImage.color;
        color.a = alpha;
        m_fadeImage.color = color;
    }
}
