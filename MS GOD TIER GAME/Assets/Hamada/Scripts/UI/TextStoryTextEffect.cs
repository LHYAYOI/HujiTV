using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "TextStoryTextEffect", menuName = "Scriptable Objects/TextStoryTextEffect")]
public class TextStoryTextEffect : TextEffect
{
   [SerializeField] float m_speed = 10f;
   [SerializeField] float m_radius = 0.03f;

    public override void Apply(TMP_TextInfo textInfo, int characterIndex)
    {
        TMP_CharacterInfo charInfo =
            textInfo.characterInfo[characterIndex];

        charInfo.color = Color.red;

        if (!charInfo.isVisible)
            return;

        int materialIndex = charInfo.materialReferenceIndex;
        int vertexIndex = charInfo.vertexIndex;

        Vector3[] vertices =
            textInfo.meshInfo[materialIndex].vertices;

        // ŠÔ‚ğ‹æØ‚Á‚ÄU“®•ûŒü‚ğ•Ï‚¦‚é
        float step = Mathf.Floor(Time.time * m_speed);

        float angle = Mathf.PerlinNoise(
            characterIndex * 13.7f,
            step * 0.37f
        ) * Mathf.PI * 2f;

        Vector3 offset = new Vector3(
            Mathf.Cos(angle),
            Mathf.Sin(angle),
            0f
        ) * m_radius;

        for (int i = 0; i < 4; i++)
        {
            vertices[vertexIndex + i] += offset;
        }
    }
}
