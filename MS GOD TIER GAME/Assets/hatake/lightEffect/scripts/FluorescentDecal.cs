using System.Collections.Generic;
using UnityEngine;

namespace LightShaftLab
{
    /// <summary>
    /// 既存の不透明な面に蛍光塗料・蛍光インクを投影するデカール。ブラックライトの紫外線を受けたときだけ光る。
    /// 箱（ローカル±0.5×Size）の中にある面へローカルZ方向から投影する。描画は BlackLightFeature が行う。
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("LightShaft/蛍光デカール（FluorescentDecal）")]
    public sealed class FluorescentDecal : MonoBehaviour
    {
        public enum SHAPE
        {
            [InspectorName("テクスチャ")] TEXTURE = 0,
            [InspectorName("楕円")] ELLIPSE = 1,
            [InspectorName("長方形")] RECTANGLE = 2,
        }

        public enum MASK_CHANNEL
        {
            [InspectorName("A（RGBは色として乗算）")] ALPHA = 0,
            [InspectorName("R")] RED = 1,
            [InspectorName("輝度")] LUMINANCE = 2,
        }

        static readonly List<FluorescentDecal> g_active = new List<FluorescentDecal>();
        /// <summary>有効なデカールの一覧（描画順は追加順）。</summary>
        public static IReadOnlyList<FluorescentDecal> Active => g_active;

        [Tooltip("投影する箱の大きさ。X・Yが模様の幅と高さ、Zが投影の奥行き（この範囲にある面だけに付きます）。")]
        public Vector3 m_size = new Vector3(1, 1, 0.5f);

        [Header("形")]
        [Tooltip("模様の形。テクスチャなら任意の形を使えます。")]
        public SHAPE m_shape = SHAPE.TEXTURE;
        [Tooltip("模様のテクスチャ。マスクのチャンネルで形を決め、Aを使う場合はRGBを蛍光色に乗算します。")]
        public Texture2D m_mask;
        public MASK_CHANNEL m_maskChannel = MASK_CHANNEL.ALPHA;
        public Vector2 m_tiling = Vector2.one;
        public Vector2 m_offset = Vector2.zero;
        [Range(0, 0.5f), Tooltip("楕円・長方形の縁のぼかし（大きさに対する割合）。")]
        public float m_edgeSoftness = 0.05f;

        [Header("蛍光（紫外線を受けたときの発光）")]
        [ColorUsage(false, true), Tooltip("紫外線を受けたときに放出する色。ライトの色とは無関係です。")]
        public Color m_fluorescentColor = new Color(0.25f, 1, 0.35f);
        [Range(0, 10), Tooltip("紫外線をどれだけ光に変えるか。0で蛍光なし。")]
        public float m_efficiency = 1.5f;
        [Range(0, 64), Tooltip("蛍光の輝度をこの値に向けて滑らかに頭打ちにする。0で上限なし。")]
        public float m_maxLuminance = 8;
        [Range(0, 1), Tooltip("ライトに背を向けた面にも紫外線を回り込ませる量。")]
        public float m_wrap = 0;
        [Range(0, 1), Tooltip("塗料の濃さ（模様全体の不透明度）。")]
        public float m_opacity = 1;

        [Header("通常光での見た目")]
        [Tooltip("塗料そのものの色（下地に乗算）。白なら通常光では見えない透明インクになります。")]
        public Color m_daylightTint = Color.white;
        [Range(0, 1), Tooltip("通常光で塗料が見える量。0=透明インク（紫外線でだけ見える）。")]
        public float m_daylightOpacity = 0;

        [Header("投影の範囲")]
        [Range(0, 90), Tooltip("投影方向に対して面がこの角度より傾くと薄くなり始める。")]
        public float m_angleFadeStart = 60;
        [Range(0, 90), Tooltip("この角度で完全に消える（側面への引き伸ばしを防ぐ）。")]
        public float m_angleFadeEnd = 85;
        [Range(0, 1), Tooltip("奥行きの両端で薄くする範囲（Zに対する割合）。")]
        public float m_depthFade = 0.2f;
        [Min(0), Tooltip("カメラからこの距離より遠いと描画しない。")]
        public float m_drawDistance = 60;

        void OnEnable() { if (!g_active.Contains(this)) g_active.Add(this); }
        void OnDisable() { g_active.Remove(this); }

        void OnValidate()
        {
            m_size = Vector3.Max(m_size, new Vector3(0.001f, 0.001f, 0.001f));
            m_angleFadeEnd = Mathf.Max(m_angleFadeEnd, m_angleFadeStart);
        }

        /// <summary>単位立方体（±0.5）を投影の箱へ変換する行列。</summary>
        public Matrix4x4 BoxMatrix => transform.localToWorldMatrix * Matrix4x4.Scale(m_size);

        public bool HasFluorescence => m_efficiency > 0 && m_opacity > 0 && m_fluorescentColor.maxColorComponent > 0;
        public bool HasDaylightTint => m_daylightOpacity > 0 && m_opacity > 0 && m_daylightTint != Color.white;

        /// <summary>ワールド空間の外接ボックス。</summary>
        public Bounds WorldBounds
        {
            get
            {
                Matrix4x4 matrix = BoxMatrix;
                Vector3 center = matrix.MultiplyPoint3x4(Vector3.zero);
                Vector3 extents = new Vector3(
                    Mathf.Abs(matrix.m00) + Mathf.Abs(matrix.m01) + Mathf.Abs(matrix.m02),
                    Mathf.Abs(matrix.m10) + Mathf.Abs(matrix.m11) + Mathf.Abs(matrix.m12),
                    Mathf.Abs(matrix.m20) + Mathf.Abs(matrix.m21) + Mathf.Abs(matrix.m22)) * 0.5f;
                return new Bounds(center, extents * 2);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = BoxMatrix;
            Gizmos.color = new Color(0.55f, 0.3f, 1, 0.25f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = BoxMatrix;
            Gizmos.color = new Color(0.65f, 0.4f, 1, 1);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            // 投影方向（ローカル+Z）。
            Gizmos.DrawLine(new Vector3(0, 0, -0.5f), new Vector3(0, 0, 0.5f));
            Gizmos.DrawLine(new Vector3(0, 0, 0.5f), new Vector3(0.1f, 0, 0.35f));
            Gizmos.DrawLine(new Vector3(0, 0, 0.5f), new Vector3(-0.1f, 0, 0.35f));
        }
    }
}
