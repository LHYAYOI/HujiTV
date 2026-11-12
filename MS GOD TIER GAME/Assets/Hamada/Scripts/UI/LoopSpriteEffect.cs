using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "LoopSpriteEffect", menuName = "Scriptable Objects/LoopSpriteEffect")]
public class LoopSpriteEffect : TextEffect
{
    [Header("Animation")]
    [SerializeField] int[] m_frameIndices;

    [SerializeField] float m_frameRate = 12f;

    public override void Apply(
        TMP_TextInfo textInfo,
        int characterIndex,
        int localIndex,
        float elapsedTime)
    {

        TMP_CharacterInfo charInfo =
            textInfo.characterInfo[characterIndex];

        // スプライト以外は処理しない
        if (charInfo.elementType != TMP_TextElementType.Sprite)
            return;

        if (m_frameIndices == null || m_frameIndices.Length == 0)
            return;

        TMP_SpriteCharacter baseSprite =
    charInfo.textElement as TMP_SpriteCharacter;

        if (baseSprite == null)
            return;

        TMP_SpriteAsset spriteAsset =
            baseSprite.textAsset as TMP_SpriteAsset;

        if (spriteAsset == null || spriteAsset.spriteSheet == null)
            return;

        int materialIndex = charInfo.materialReferenceIndex;
        int vertexIndex = charInfo.vertexIndex;

        TMP_MeshInfo meshInfo =
            textInfo.meshInfo[materialIndex];

        // 現在のフレームを計算
        int frameStep = Mathf.FloorToInt(
            elapsedTime * Mathf.Max(0.01f, m_frameRate)
        );

        int frameListIndex = frameStep % m_frameIndices.Length;

        // Sprite Asset内のフレーム番号
        int spriteIndex = m_frameIndices[frameListIndex];

        if (spriteIndex < 0 ||
            spriteIndex >= spriteAsset.spriteCharacterTable.Count)
            return;

        TMP_SpriteCharacter spriteCharacter =
            spriteAsset.spriteCharacterTable[spriteIndex];

        // 元のスプライトのスケール
        float baseSpriteScale =
            baseSprite.scale * baseSprite.glyph.scale;

        if (baseSpriteScale <= 0.0001f)
            return;

        // 現在のフレームのスケール
        float spriteScale =
            charInfo.scale / baseSpriteScale *
            spriteCharacter.scale *
            spriteCharacter.glyph.scale;

        // 文字の基準位置
        Vector2 origin = new Vector2(
            charInfo.origin,
            charInfo.baseLine
        );

        // 現在のフレームのMetrics
        var metrics = spriteCharacter.glyph.metrics;

        // 頂点を再計算
        Vector3 bl = new Vector3(
            origin.x + metrics.horizontalBearingX * spriteScale,
            origin.y + (metrics.horizontalBearingY - metrics.height) * spriteScale
        );

        Vector3 tl = new Vector3(
            bl.x,
            origin.y + metrics.horizontalBearingY * spriteScale
        );

        Vector3 tr = new Vector3(
            origin.x + (metrics.horizontalBearingX + metrics.width) * spriteScale,
            tl.y
        );

        Vector3 br = new Vector3(
            tr.x,
            bl.y
        );

        Vector3[] vertices = meshInfo.vertices;

        vertices[vertexIndex + 0] = bl;
        vertices[vertexIndex + 1] = tl;
        vertices[vertexIndex + 2] = tr;
        vertices[vertexIndex + 3] = br;
    }

    public override float GetDuration(int characterCount)
    {
        // ループするため終了時間はない
        return float.PositiveInfinity;
    }

    public override bool IsLooping => true;
}
