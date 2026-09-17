//-----------------------------------------------
// VolumeController.cs
// 制作日：2026/09/16
// 制作者：渡辺開斗
// 概要：音量調整管理クラス
//-----------------------------------------------
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class VolumeController : MonoBehaviour
{
    [Header("AudioMixer")]
    [SerializeField] private AudioMixer m_audioMixer;

    [Header("BGMのスライダー")]
    [SerializeField] private Slider m_bgmSlider;

    [Header("SEのスライダー")]
    [SerializeField] private Slider m_seSlider;


    void Start()
    {
        //BGM
        m_audioMixer.GetFloat("BGM_Volume", out float bgmVolume);
        m_bgmSlider.value = bgmVolume;

        //SE
        m_audioMixer.GetFloat("SE_Volume", out float seVolume);
        m_seSlider.value = seVolume;
    }

    // スライダーの値が変更されたときに呼ばれるメソッド
    public void SetBGM(float volume)
    {
        m_audioMixer.SetFloat("BGM_Volume", volume);
    }

    public void SetSE(float volume)
    {
        m_audioMixer.SetFloat("SE_Volume", volume);
    }
}
