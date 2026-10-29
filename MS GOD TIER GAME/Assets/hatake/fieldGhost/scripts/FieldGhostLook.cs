using UnityEngine;

namespace FieldGhost
{
    /// <summary>
    /// お化け（custom/hatake/ToonMaster のゴースト設定）の濃さと切り口を MaterialPropertyBlock で書く小道具。
    /// TeamToonGhostDeformer も同じ PropertyBlock を使うので、必ず読んでから書き戻す。
    /// </summary>
    public static class FieldGhostLook
    {
        static readonly int VisibilityId = Shader.PropertyToID("_Visibility");
        static readonly int SliceEnabledId = Shader.PropertyToID("_SliceEnabled");
        static readonly int SlicePlaneId = Shader.PropertyToID("_SlicePlane");
        static readonly int SliceGlowColorId = Shader.PropertyToID("_SliceGlowColor");
        static readonly int SliceGlowWidthId = Shader.PropertyToID("_SliceGlowWidth");
        static MaterialPropertyBlock block;

        static MaterialPropertyBlock Block => block ??= new MaterialPropertyBlock();

        /// <summary>全体の濃さ（0 で見えない、1 でマテリアルどおり）。</summary>
        public static void SetVisibility(Renderer[] renderers, float visibility)
        {
            if (renderers == null)
            {
                return;
            }
            foreach (Renderer r in renderers)
            {
                if (!r)
                {
                    continue;
                }
                r.GetPropertyBlock(Block);
                Block.SetFloat(VisibilityId, Mathf.Clamp01(visibility) * MaterialVisibility(r));
                r.SetPropertyBlock(Block);
            }
        }

        /// <summary>平面の裏側を切る。法線の向いている側だけが残る。</summary>
        public static void SetSlice(Renderer[] renderers, Vector3 planePoint, Vector3 planeNormal, Color glowColor, float glowWidth)
        {
            if (renderers == null)
            {
                return;
            }
            Vector3 n = planeNormal.normalized;
            Vector4 plane = new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, planePoint));
            foreach (Renderer r in renderers)
            {
                if (!r)
                {
                    continue;
                }
                r.GetPropertyBlock(Block);
                Block.SetFloat(SliceEnabledId, 1);
                Block.SetVector(SlicePlaneId, plane);
                Block.SetColor(SliceGlowColorId, glowColor);
                Block.SetFloat(SliceGlowWidthId, Mathf.Max(glowWidth, 0.001f));
                r.SetPropertyBlock(Block);
            }
        }

        public static void ClearSlice(Renderer[] renderers)
        {
            if (renderers == null)
            {
                return;
            }
            foreach (Renderer r in renderers)
            {
                if (!r)
                {
                    continue;
                }
                r.GetPropertyBlock(Block);
                Block.SetFloat(SliceEnabledId, 0);
                r.SetPropertyBlock(Block);
            }
        }

        /// <summary>見つかったレンダラーのうち、ゴースト用シェーダでないものがあれば名前を返す（設定ミスの知らせ用）。</summary>
        public static string FindUnsupported(Renderer[] renderers)
        {
            if (renderers == null)
            {
                return null;
            }
            foreach (Renderer r in renderers)
            {
                if (r && !r.sharedMaterial)
                {
                    return r.name;
                }
                if (r && !r.sharedMaterial.HasProperty(VisibilityId))
                {
                    return r.name;
                }
            }
            return null;
        }

        static float MaterialVisibility(Renderer r)
        {
            Material m = r.sharedMaterial;
            return m && m.HasProperty(VisibilityId) ? m.GetFloat(VisibilityId) : 1;
        }
    }
}
