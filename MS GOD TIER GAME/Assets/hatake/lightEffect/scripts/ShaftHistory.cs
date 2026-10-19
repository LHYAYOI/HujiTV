using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace LightShaftLab
{
    // カメラごとに1組持つ。Game ビューと Scene ビューで再投影データを共有してはいけない。
    internal sealed class ShaftHistory
    {
        public readonly RTHandle[] m_color = new RTHandle[2], m_depth = new RTHandle[2];
        public int m_index, m_frame, m_hash, m_width, m_height;
        public bool m_validFlag;
        public Matrix4x4 m_viewProjection, m_view, m_projection;
        public Vector3 m_position;
        public Quaternion m_rotation;
        public double m_time;
        public string m_reason = "Initial frame";
        // 解像度が変わったときだけ履歴テクスチャ（ダブルバッファ）を確保し直す。
        public void Ensure(int width, int height)
        {
            if (m_width == width && m_height == height && m_color[0] != null) return;
            Dispose(); m_width = width; m_height = height;
            for (int i = 0; i < 2; i++)
            {
                m_color[i] = RTHandles.Alloc(width, height, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, filterMode: FilterMode.Bilinear, name: "Shaft History Color");
                m_depth[i] = RTHandles.Alloc(width, height, colorFormat: GraphicsFormat.R32G32B32A32_SFloat, filterMode: FilterMode.Point, name: "Shaft History Depth");
            }
            m_reason = "Resolution changed";
        }
        public void Reset(string reason) { m_validFlag = false; m_frame = 0; m_reason = reason; }
        public void Dispose()
        {
            for (int i = 0; i < 2; i++) { m_color[i]?.Release(); m_depth[i]?.Release(); m_color[i] = null; m_depth[i] = null; }
            m_validFlag = false; m_frame = 0;
        }
    }
}
