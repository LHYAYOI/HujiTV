using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "NoiseShakeEffect", menuName = "Scriptable Objects/NoiseShakeEffect")]
public class NoiseShakeEffect : TextEffect
{
    [Header("Noise Settings")]
    [SerializeField] Vector2 m_amplitude = new Vector2(3f, 3f);

    [SerializeField] float m_frequency = 2f;

    [SerializeField] float m_seed = 0f;

    public override void Apply(TMP_TextInfo textInfo, int characterIndex, int localIndex, float elapsedTime)
    {
        TMP_CharacterInfo charInfo = textInfo.characterInfo[characterIndex];

        if (!charInfo.isVisible)
            return;

        int materialIndex = charInfo.materialReferenceIndex;
        int vertexIndex = charInfo.vertexIndex;

        Vector3[] vertices =
            textInfo.meshInfo[materialIndex].vertices;

        // 文字ごとに異なるNoiseの座標を作る
        float seed = characterIndex * 17.17f + m_seed;

        float time = elapsedTime * m_frequency;

        // X方向のNoise
        float noiseX = Mathf.PerlinNoise(
            seed,
            time
        ) * 2f - 1f;

        // Y方向のNoise
        float noiseY = Mathf.PerlinNoise(
            seed + 53.71f,
            time + 31.29f
        ) * 2f - 1f;

        Vector3 offset = new Vector3(
            noiseX * m_amplitude.x,
            noiseY * m_amplitude.y,
            0f
        );

        // 文字の4頂点を同じ方向に移動
        for (int i = 0; i < 4; i++)
        {
            vertices[vertexIndex + i] += offset;
        }
    }

    public override float GetDuration(int characterCount)
    {
        return float.PositiveInfinity;
    }

    public override bool IsLooping => true;
}
