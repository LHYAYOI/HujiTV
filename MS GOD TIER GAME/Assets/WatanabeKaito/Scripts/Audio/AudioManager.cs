//-----------------------------------------------
// AudioManager.cs
// 制作日：2026/09/15
// 制作者：渡辺開斗
// 概要：音の管理クラス
//-----------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using static UnityEngine.Analytics.IAnalytic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("AudioMixer")]
    [SerializeField] private AudioMixer m_audioMixer;
    [SerializeField] private AudioMixerGroup m_bgmGroup;
    [SerializeField] private AudioMixerGroup m_seGroup;

    [Header("BGMのAudioSource")]
    [SerializeField] private AudioSource m_bgmSource;

    [Header("BGMのSoundData")]
    [SerializeField] private List<SoundData> m_bgmDataList = new List<SoundData>();

    [Header("SEのAudioSource")]
    [SerializeField] private AudioSource m_seSource;

    [Header("SEのSoundData")]
    [SerializeField] private List<SoundData> m_seDataList = new List<SoundData>();

    // BGMとSEの辞書
    private Dictionary<string, SoundData> m_bgmDictionary = new Dictionary<string, SoundData>();
    private Dictionary<string, SoundData> m_seDictionary = new Dictionary<string, SoundData>();

    // クールダウン管理用の辞書（SEの連打防止）
    private Dictionary<string, float> lastPlayTimeDict = new Dictionary<string, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeAutoLoad()
    {
        // インスタンスがまだ存在しない場合のみ生成
        if (Instance == null)
        {
            // Resourcesフォルダ内にあるAudioManagerプレハブを読み込む
            GameObject prefab = Resources.Load<GameObject>("AudioManager");
            if (prefab != null)
            {
                Instantiate(prefab);
            }
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSoundDictionaries();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // BGMとSEの辞書を初期化
    private void InitializeSoundDictionaries()
    {
        foreach (var bgm in m_bgmDataList)
        {
            if (bgm != null && !string.IsNullOrEmpty(bgm.m_id) && !m_bgmDictionary.ContainsKey(bgm.m_id))
            {
                m_bgmDictionary.Add(bgm.m_id, bgm);
            }
        }
        foreach (var se in m_seDataList)
        {
            if (se != null && !string.IsNullOrEmpty(se.m_id) && !m_seDictionary.ContainsKey(se.m_id))
            {
                m_seDictionary.Add(se.m_id, se);
            }
        }
    }

    // BGM再生
    public void PlayBGM(string id)
    {
        if (!m_bgmDictionary.TryGetValue(id, out SoundData data))
        {
            return;
        }

        AudioClip clipToPlay = data.GetClip();
        if (clipToPlay == null) return;

        if (m_bgmSource.clip == clipToPlay && m_bgmSource.isPlaying) return;

        m_bgmSource.clip = clipToPlay;
        m_bgmSource.volume = data.m_volume;
        m_bgmSource.pitch = data.m_pitch;
        m_bgmSource.loop = true; // BGMは強制ループ
        m_bgmSource.outputAudioMixerGroup = m_bgmGroup;
        m_bgmSource.Play();
    }

    // BGM停止
    public void StopBGM()
    {
        m_bgmSource.Stop();
    }


    // SE再生（位置指定なし）
    public void PlaySE(string id)
    {
        PlaySE(id, transform.position);
    }

    // SE再生（位置指定あり）
    public void PlaySE(string id, Vector3 position)
    {
        if (!m_seDictionary.TryGetValue(id, out SoundData data))
        {
            return;
        }

        // クールダウン（連打防止）チェック
        if (data.m_cooldown > 0f)
        {
            if (lastPlayTimeDict.TryGetValue(id, out float lastTime))
            {
                if (Time.time - lastTime < data.m_cooldown) return;
            }
            lastPlayTimeDict[id] = Time.time;
        }

        AudioClip clipToPlay = data.GetClip();
        if (clipToPlay == null) return;

        StartCoroutine(PlaySECoroutine(data, clipToPlay, position));
    }

    // SE再生のコルーチン
    private IEnumerator PlaySECoroutine(SoundData data, AudioClip clip, Vector3 position)
    {
        if (data.m_delay > 0f)
        {
            yield return new WaitForSeconds(data.m_delay);
        }

        GameObject seObj = new GameObject($"SE_{data.m_id}");
        seObj.transform.position = position;

        AudioSource source = seObj.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = data.m_volume;
        source.loop = data.m_loop;
        source.spatialBlend = data.m_spatialBlend;
        source.outputAudioMixerGroup = m_seGroup;

        float randomPitch = Random.Range(-data.m_pitchVariance, data.m_pitchVariance);
        source.pitch = Mathf.Clamp(data.m_pitch + randomPitch, 0.1f, 3f);

        source.Play();

        if (!data.m_loop)
        {
            float playDuration = clip.length / Mathf.Abs(source.pitch);
            Destroy(seObj, playDuration);
        }
    }

    // BGMの全体音量を設定する（0.0〜1.0）
    public void SetBgmVolume(float volume)
    {
        // 0.0〜1.0 の値を、AudioMixerのデシベル表記(-80dB〜0dB)に変換する
        float decibel = Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f;

        // Exposeしたパラメータ名 "BGM_Volume" を指定して適用
        m_audioMixer.SetFloat("BGM_Volume", decibel);
    }

    // SEの全体音量を設定する（0.0〜1.0）
    public void SetSeVolume(float volume)
    {
        // 0.0〜1.0 の値を、AudioMixerのデシベル表記(-80dB〜0dB)に変換する
        float decibel = Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20f;

        // Exposeしたパラメータ名 "SE_Volume" を指定して適用
        m_audioMixer.SetFloat("SE_Volume", decibel);
    }
}