//-----------------------------------------------
// SoundData.cs
// 制作日：2026/09/16
// 制作者：渡辺開斗
// 概要：サウンドデータのScriptableObject
//-----------------------------------------------
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSoundData", menuName = "Audio/Sound Data")]
public class SoundData : ScriptableObject
{
    [Header("音の識別子")]
    public string m_id;

    [Header("音声クリップ")]
    public AudioClip m_clip;

    [Header("複数ランダムに鳴らす場合の音声クリップ")]
    public List<AudioClip> m_randomClips;

    [Header("音量")]
    [Range(0f, 1f)] 
    public float m_volume = 1f;

    [Header("ピッチ")]
    [Range(-3f, 3f)] 
    public float m_pitch = 1f;

    [Header("ピッチの揺らぎ幅")]
    [Range(0f, 0.3f)] 
    public float m_pitchVariance = 0f;

    [Header("ループ再生")]
    public bool m_loop = false;

    [Header("再生開始までの遅延時間（秒）")]
    public float m_delay = 0f;

    [Header("最小再生間隔（秒）")]
    public float m_cooldown = 0f;

    [Header("3Dサウンド設定 (SE専用)")]
    [Range(0f, 1f)]
    public float m_spatialBlend = 0f;

    // クリップ自動選択（単発 or ランダムリスト）
    public AudioClip GetClip()
    {
        if (m_randomClips != null && m_randomClips.Count > 0)
        {
            return m_randomClips[Random.Range(0, m_randomClips.Count)];
        }
        return m_clip;
    }
}
