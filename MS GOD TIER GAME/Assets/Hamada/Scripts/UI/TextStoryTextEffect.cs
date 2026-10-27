using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "TextStoryTextEffect", menuName = "Scriptable Objects/TextStoryTextEffect")]
public class TextStoryTextEffect : TextEffect
{
    [SerializeField] float m_appearDurationOfOneChar = 0.5f;
    [SerializeField] float m_delayPerCharacter = 0.1f;
    [SerializeField] float m_startScale;
    [SerializeField] float m_targetScale;

    public override void Apply(TMP_TextInfo textInfo, int characterIndex, int localIndex, float elapsedTime)
    {
        

        TMP_CharacterInfo charInfo = textInfo.characterInfo[characterIndex];

        if (!charInfo.isVisible)
            return;

        int materialIndex = charInfo.materialReferenceIndex;
        int vertexIndex = charInfo.vertexIndex;

        TMP_MeshInfo meshInfo = textInfo.meshInfo[materialIndex];

        Vector3[] vertices = meshInfo.vertices;
        Color32[] colors = meshInfo.colors32;

        // 文字ごとの開始タイミング
        float startTime = localIndex * m_delayPerCharacter;

        // 文字の中心
        Vector3 center = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;

        // 0～1の進行度
        float t = Mathf.Clamp01((elapsedTime - startTime) / Mathf.Max(0.001f, m_appearDurationOfOneChar));

        // 滑らかな補間
        t = Easing.EaseOutCubic(t);

        float scale = Mathf.Lerp(m_startScale, m_targetScale, t);
        float alpha = t;

        for (int i = 0; i < 4; i++)
        {
            int index = vertexIndex + i;

            // 中心を基準に縮小
            vertices[index] = center + (vertices[index] - center) * scale;

            // アルファ値を変更
            Color32 color = colors[index];
            color.a = (byte)(color.a * alpha);
            colors[index] = color;
        }
    }

    public override float GetDuration(int characterCount) 
    {
        if (characterCount <= 0) 
        {
            return 0;
        }

        return m_appearDurationOfOneChar + (characterCount - 1) * m_delayPerCharacter;
    }
}
