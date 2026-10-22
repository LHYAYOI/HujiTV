using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;


[Serializable]
public class TextEffectSetting
{
    [Tooltip("エフェクトを適用したい文字列")]
    public string m_targetText;

    [Tooltip("適用するエフェクト")]
    public TextEffect m_effect;
}

[RequireComponent(typeof(TMP_Text))]
public class TextEffectController : MonoBehaviour
{
    [SerializeField]
    List<TextEffectSetting> m_effectSettings = new();

    TMP_Text m_text;

    void Awake()
    {
        m_text = GetComponent<TMP_Text>();
    }

    void LateUpdate()
    {
        if (m_effectSettings.Count == 0) 
        {
            return;
        }

        // 毎回元のメッシュを再生成する
        m_text.ForceMeshUpdate();

        TMP_TextInfo textInfo = m_text.textInfo;

        foreach (TextEffectSetting setting in m_effectSettings)
        {
            if (setting.m_effect == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(setting.m_targetText)) 
            {
                continue;
            }

            //指定した文字にエフェクトを実行
            ApplyToMatchingText(textInfo, setting);
        }

        // 頂点の変更を反映
        m_text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
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
                setting.m_effect.Apply(textInfo, i + j);
            }

            // 一致範囲の最後まで進める
            i += target.Length - 1;
        }
    }
}
