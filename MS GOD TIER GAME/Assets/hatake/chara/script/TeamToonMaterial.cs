using UnityEngine;
using UnityEngine.Rendering;

namespace TeamToon
{
    /// <summary>実行時に機能のプロパティやテクスチャを変更した後は、Synchronizeを使用してください</summary>
    public static class TeamToonMaterial
    {
        public const string ShaderName = "custom/hatake/Toon";
        public enum Preset { Standard, Skin, Hair, Cloth, Metal, Eyes }
        public static bool IsSupported(Material material) => material && material.shader && material.shader.name == ShaderName;
        static bool On(Material m, string property) => m.GetFloat(property) > 0.5f;
        static bool Map(Material m, string enabled, string texture) => On(m, enabled) && m.GetTexture(texture);
        static void Keyword(Material m, string keyword, bool enabled)
        {
            var local = new LocalKeyword(m.shader, keyword);
            if (m.IsKeywordEnabled(local) != enabled) m.SetKeyword(local, enabled);
        }
        static void Number(Material m, string property, float value)
        {
            if (!Mathf.Approximately(m.GetFloat(property), value)) m.SetFloat(property, value);
        }
        static void Pass(Material m, string pass, bool enabled)
        {
            if (m.GetShaderPassEnabled(pass) != enabled) m.SetShaderPassEnabled(pass, enabled);
        }

        public static void Synchronize(Material m)
        {
            if (!IsSupported(m)) return;
            int shade = Mathf.Clamp(Mathf.RoundToInt(m.GetFloat("_ShadeMode")), 0, 4);
            bool unlit = shade == 3;
            Keyword(m, "_TOON_SHADE_THREE", shade == 1);
            Keyword(m, "_TOON_SHADE_SMOOTH", shade == 2);
            Keyword(m, "_TOON_SHADE_UNLIT", unlit);
            // ランプが指定されていない場合は、2色シェーディングにフォールバックします。
            Keyword(m, "_TOON_SHADE_RAMP", shade == 4 && m.GetTexture("_RampMap"));
            int specular = Mathf.Clamp(Mathf.RoundToInt(m.GetFloat("_SpecularMode")), 0, 2);
            Keyword(m, "_TOON_SPECULAR", !unlit && specular == 1);
            Keyword(m, "_TOON_SPECULAR_HAIR", !unlit && specular == 2);
            Keyword(m, "_TOON_NORMALMAP", !unlit && Map(m, "_NormalEnabled", "_NormalMap"));
            Keyword(m, "_TOON_CONTROL_MAP", !unlit && Map(m, "_ControlEnabled", "_ControlMap"));
            Keyword(m, "_TOON_SURFACE_MAP", !unlit && Map(m, "_SurfaceEnabled", "_SurfaceMap"));
            Keyword(m, "_TOON_ALPHA_CLIP", On(m, "_AlphaClip"));
            Keyword(m, "_TOON_RECEIVE_SHADOWS_OFF", !On(m, "_ReceiveShadows") || unlit);
            Keyword(m, "_TOON_RIM", !unlit && On(m, "_RimEnabled"));
            Keyword(m, "_TOON_REFLECTION", !unlit && On(m, "_ReflectionEnabled"));
            Keyword(m, "_TOON_EMISSION", On(m, "_EmissionEnabled"));
            Keyword(m, "_TOON_FACE", !unlit && m.GetFloat("_FaceMode") > 0.5f);
            // 共通分岐は機能バリエーションを再利用するものですが、それでも安全なフォールバックが必要
            Number(m, "_HasFaceSDFMap", Map(m, "_FaceSDFEnabled", "_FaceSDFMap") ? 1 : 0);
            Number(m, "_HasHairFlowMap", Map(m, "_HairFlowEnabled", "_HairFlowMap") ? 1 : 0);
            Number(m, "_HasEmissionMap", Map(m, "_EmissionMapEnabled", "_EmissionMap") ? 1 : 0);
            Number(m, "_HasOutlineMask", Map(m, "_OutlineMaskEnabled", "_OutlineMask") ? 1 : 0);
            Number(m, "_DeepThreshold", Mathf.Min(m.GetFloat("_DeepThreshold"), m.GetFloat("_ShadowThreshold") - 0.001f));
            Number(m, "_ShadowSoftness", Mathf.Max(0.001f, m.GetFloat("_ShadowSoftness")));
            Number(m, "_DeepSoftness", Mathf.Max(0.001f, m.GetFloat("_DeepSoftness")));
            Number(m, "_CharacterId", Mathf.Clamp(Mathf.RoundToInt(m.GetFloat("_CharacterId")), 1, 255));
            bool cutout = On(m, "_AlphaClip");
            string renderType = cutout ? "TransparentCutout" : "Opaque";
            if (m.GetTag("RenderType", false) != renderType) m.SetOverrideTag("RenderType", renderType);
            int queue = (int)(cutout ? RenderQueue.AlphaTest : RenderQueue.Geometry);
            if (m.renderQueue != queue) m.renderQueue = queue;
            Pass(m, "TeamToonOutline", On(m, "_OutlineEnabled"));
            Pass(m, "TeamToonHairMask", On(m, "_IsHair"));
            Pass(m, "TeamToonReveal", On(m, "_RevealEnabled"));
            bool doubleSided = m.GetFloat("_Cull") == 0;
            if (m.doubleSidedGI != doubleSided) m.doubleSidedGI = doubleSided;
            var gi = On(m, "_EmissionEnabled") && m.GetColor("_EmissionColor").maxColorComponent > 0
                ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (m.globalIlluminationFlags != gi) m.globalIlluminationFlags = gi;
        }

        public static void ApplyPreset(Material m, Preset preset)
        {
            if (!IsSupported(m)) return;
            // 外観の値を復元します。その際、作成済みの画像、UV、カットアウト、カリングは保持されます
            usingDefaults(m, preset);
            Synchronize(m);
        }
        static void usingDefaults(Material m, Preset preset)
        {
            var defaults = new Material(m.shader);
            foreach (string p in new[] { "_ShadeMode", "_ShadowThreshold", "_ShadowSoftness", "_DeepThreshold", "_DeepSoftness",
                "_SpecularMode", "_SpecularStrength", "_SpecularWidth", "_SpecularSoftness", "_Metallic", "_Roughness", "_Occlusion",
                "_RimEnabled", "_RimStrength", "_RimPower", "_ReflectionEnabled", "_ReflectionStrength", "_EmissionEnabled",
                "_FaceMode", "_FaceBlend", "_HairShift", "_HairSecondaryStrength", "_HairBandStrength", "_IsHair", "_RevealEnabled",
                "_AmbientStrength", "_AdditionalStrength", "_LightColorInfluence", "_LightIntensityInfluence", "_ColorPreservation" })
                m.SetFloat(p, defaults.GetFloat(p));
            m.SetColor("_ShadowColor", defaults.GetColor("_ShadowColor"));
            m.SetColor("_DeepShadowColor", defaults.GetColor("_DeepShadowColor"));
            m.SetColor("_SpecularColor", defaults.GetColor("_SpecularColor"));
            if (Application.isPlaying) Object.Destroy(defaults); else Object.DestroyImmediate(defaults);
            switch (preset)
            {
                case Preset.Skin:
                    m.SetFloat("_ShadowSoftness", 0.07f); m.SetFloat("_SpecularStrength", 0.08f); m.SetFloat("_Roughness", 0.7f); break;
                case Preset.Hair:
                    m.SetFloat("_ShadeMode", 1); m.SetFloat("_SpecularMode", 2); m.SetFloat("_SpecularStrength", 0.6f);
                    m.SetFloat("_Roughness", 0.3f); m.SetFloat("_IsHair", 1); break;
                case Preset.Cloth:
                    m.SetFloat("_SpecularMode", 0); m.SetFloat("_Roughness", 0.85f); break;
                case Preset.Metal:
                    m.SetFloat("_Metallic", 1); m.SetFloat("_Roughness", 0.25f); m.SetFloat("_SpecularStrength", 0.9f);
                    m.SetFloat("_ReflectionEnabled", 1); break;
                case Preset.Eyes:
                    m.SetFloat("_ColorPreservation", 0.8f); m.SetFloat("_SpecularStrength", 0.4f); break;
            }
        }
    }
}
