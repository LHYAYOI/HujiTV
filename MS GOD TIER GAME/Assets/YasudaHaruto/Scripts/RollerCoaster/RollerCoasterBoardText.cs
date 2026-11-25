using UnityEngine;

namespace YasudaHaruto.RollerCoaster
{
    /// <summary>板の文字を深度判定付きで描画するため、動的フォントのAtlasを専用材質へ同期する。</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public sealed class RollerCoasterBoardText : MonoBehaviour
    {
        private TextMesh m_text;
        private MeshRenderer m_renderer;
        private MaterialPropertyBlock m_properties;
        private Texture m_atlas;
        private static readonly int s_mainTexId = Shader.PropertyToID("_MainTex");

        private void OnEnable()
        {
            m_text = GetComponent<TextMesh>();
            m_renderer = GetComponent<MeshRenderer>();
            m_properties = new MaterialPropertyBlock();
            Font.textureRebuilt += OnAtlasRebuilt;
            if (m_text.font != null) m_text.font.RequestCharactersInTexture(m_text.text, m_text.fontSize, m_text.fontStyle);
            UpdateAtlas();
        }

        private void OnAtlasRebuilt(Font font)
        {
            if (m_text != null && font == m_text.font) UpdateAtlas();
        }

        private void LateUpdate() => UpdateAtlas();

        private void UpdateAtlas()
        {
            if (m_text == null || m_text.font == null || m_renderer == null) return;
            var atlas = m_text.font.material.mainTexture;
            if (atlas == m_atlas && m_atlas != null) return;
            m_atlas = atlas;
            m_renderer.GetPropertyBlock(m_properties);
            m_properties.SetTexture(s_mainTexId, atlas);
            m_renderer.SetPropertyBlock(m_properties);
        }

        private void OnDisable() => Font.textureRebuilt -= OnAtlasRebuilt;
    }
}
