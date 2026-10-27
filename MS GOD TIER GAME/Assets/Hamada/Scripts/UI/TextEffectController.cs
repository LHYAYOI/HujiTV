using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum TEXT_EFFECT_STATE
{
    Standby,
    Playing,
    Completed
}

[Serializable]
public class TextEffectSetting
{
    public bool m_applyToAllTextFlag = false;

    [Tooltip("エフェクトを適用したい文字列")]
    public string m_targetText;

    [Tooltip("適用するエフェクト")]
    public TextEffect m_effect;
}

public class TextEffectController : MonoBehaviour
{
    [SerializeField] TMP_Text m_text;
    [SerializeField]　List<TextEffectSetting> m_effectSettings = new();

    Dictionary<TextEffectSetting, bool> m_completedEffects = new();

    float m_elapsedTime;

    TEXT_EFFECT_STATE m_state;

    private void Start()
    {
        m_state = TEXT_EFFECT_STATE.Standby;
    }

    public void Play() 
    {
        m_state = TEXT_EFFECT_STATE.Playing;
        m_elapsedTime = 0;

        m_completedEffects.Clear();

        foreach (TextEffectSetting setting in m_effectSettings) 
        {
            if (setting.m_effect == null) 
            {
                continue;
            }

            m_completedEffects.Add(setting, false);
        }
    }

    public TEXT_EFFECT_STATE GetEffectState()
    {
        return m_state;
    }

    void LateUpdate()
    {
        if (m_state != TEXT_EFFECT_STATE.Playing) 
        {
            return;
        }

        if (m_effectSettings.Count == 0) 
        {
            return;
        }

        m_elapsedTime += Time.deltaTime;

        // 毎回元のメッシュを再生成する
        m_text.ForceMeshUpdate();

        TMP_TextInfo textInfo = m_text.textInfo;

        ModifyMesh(textInfo);

        // 頂点と色の変更を反映
        m_text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);

        //終了処理とか
        CheckEffectCompletion(textInfo);
    }

    private void ModifyMesh(TMP_TextInfo textInfo) 
    {
        foreach (TextEffectSetting setting in m_effectSettings)
        {
            //エフェクトが設定されてるか
            if (setting.m_effect == null)
            {
                continue;
            }

            if (setting.m_applyToAllTextFlag)
            {
                for (int i = 0; i < textInfo.characterCount; i++) 
                {
                    setting.m_effect.Apply(textInfo, i, i, m_elapsedTime);
                }
            }
            else 
            {
                //文字の指定があるか
                if (string.IsNullOrEmpty(setting.m_targetText))
                {
                    continue;
                }

                //指定した文字にエフェクトを実行
                ApplyToMatchingText(textInfo, setting);
            }
        }
    }

    private void ApplyToMatchingText(TMP_TextInfo textInfo, TextEffectSetting setting)
    {
        string target = setting.m_targetText;
         
        int count = textInfo.characterCount;

        // 対象文字列を検索する
        for (int i = 0; i <= count - target.Length; i++)
        {
            bool isMatch = true;

            for (int j = 0; j < target.Length; j++)
            {
                if (textInfo.characterInfo[i + j].character != target[j])
                {
                    isMatch = false;
                    break;
                }
            }

            if (!isMatch) 
            {
                continue;
            }

            // 一致した範囲の全ての文字に適用
            for (int j = 0; j < target.Length; j++)
            {
                setting.m_effect.Apply(textInfo, i + j, j, m_elapsedTime);
            }

            // 一致範囲の最後まで進める
            i += target.Length - 1;
        }
    }

    private int GetTargetCharacterCount(TMP_TextInfo textInfo, TextEffectSetting setting)
    {
        if (setting.m_applyToAllTextFlag)
            return textInfo.characterCount;

        if (string.IsNullOrEmpty(setting.m_targetText))
            return 0;

        string target = setting.m_targetText;
        int count = textInfo.characterCount;
        int matchCount = 0;

        for (int i = 0; i <= count - target.Length; i++)
        {
            bool isMatch = true;

            for (int j = 0; j < target.Length; j++)
            {
                if (textInfo.characterInfo[i + j].character != target[j])
                {
                    isMatch = false;
                    break;
                }
            }

            if (!isMatch)
                continue;

            matchCount++;
            i += target.Length - 1;
        }

        // 現在のApplyToMatchingTextでは、
        // 一致した範囲ごとにlocalIndexが0から始まる
        return matchCount > 0 ? target.Length : 0;
    }

    private void CheckEffectCompletion(TMP_TextInfo textInfo)
    {
        bool allCompleted = true;

        foreach (TextEffectSetting setting in m_effectSettings)
        {
            if (setting.m_effect == null)
                continue;

            if (!m_completedEffects.TryGetValue(setting, out bool completed))
                continue;

            if (completed)
                continue;

            int characterCount = GetTargetCharacterCount(textInfo, setting);

            float duration = setting.m_effect.GetDuration(characterCount);

            if (m_elapsedTime >= duration)
            {
                m_completedEffects[setting] = true;
            }
            else
            {
                allCompleted = false;
            }
        }

        if (allCompleted)
        {
            m_state = TEXT_EFFECT_STATE.Completed;
        }
    }
}
