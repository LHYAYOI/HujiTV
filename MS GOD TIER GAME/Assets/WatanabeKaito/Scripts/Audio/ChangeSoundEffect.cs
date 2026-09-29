using UnityEngine;
using UnityEngine.Audio;

public class ChangeSoundEffect : MonoBehaviour
{
    [Header("通常のスナップショット")]
    public AudioMixerSnapshot m_normalSnapshot;

    [Header("水中のスナップショット")]
    public AudioMixerSnapshot m_underwaterSnapshot;

    [Header("水中切り替えにかかる時間（秒）")]
    public float m_transitionInTime = 0.5f;

    [Header("通常切り替えにかかる時間（秒）")]
    public float m_transitionOutTime = 1.0f;


    // プレイヤーが水中から出た時のサウンドエフェクト切り替え
    public void ChangeNormalSoundEffect()
    {
        m_normalSnapshot.TransitionTo(m_transitionOutTime);
    }

    // プレイヤーが水中に入った時のサウンドエフェクト切り替え
    public void ChangeUnderwaterSoundEffect()
    {
        m_underwaterSnapshot.TransitionTo(m_transitionInTime);
    }
}