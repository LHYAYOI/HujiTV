using UnityEngine;
using UnityEngine.Rendering;

namespace TeamToon
{
    /// <summary>Use Synchronize after changing feature properties or textures at runtime.</summary>
    public static class TeamToonMaterial
    {
        public const string ShaderName = "custom/hatake/ToonMaster";

        public enum Preset { Standard, Skin, Hair, Cloth, Metal, Eyes, Ghost }
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
            // A missing ramp falls back to two-color shading.
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
            bool transparent = Mathf.RoundToInt(m.GetFloat("_SurfaceType")) == 1;
            Keyword(m, "_TOON_TRANSPARENT", transparent);
            Keyword(m, "_TOON_DEFORM", On(m, "_DeformEnabled"));
            // Premultiplied alpha; opaque keeps the original One Zero / ZWrite On state.
            Number(m, "_SrcBlend", (float)BlendMode.One);
            Number(m, "_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            Number(m, "_ZWrite", transparent ? 0 : 1);
            // These uniform branches reuse the feature variant, but still need safe fallbacks.
            Number(m, "_HasFaceSDFMap", Map(m, "_FaceSDFEnabled", "_FaceSDFMap") ? 1 : 0);
            Number(m, "_HasHairFlowMap", Map(m, "_HairFlowEnabled", "_HairFlowMap") ? 1 : 0);
            Number(m, "_HasEmissionMap", Map(m, "_EmissionMapEnabled", "_EmissionMap") ? 1 : 0);
            Number(m, "_HasOutlineMask", Map(m, "_OutlineMaskEnabled", "_OutlineMask") ? 1 : 0);
            Number(m, "_DeepThreshold", Mathf.Min(m.GetFloat("_DeepThreshold"), m.GetFloat("_ShadowThreshold") - 0.001f));
            Number(m, "_ShadowSoftness", Mathf.Max(0.001f, m.GetFloat("_ShadowSoftness")));
            Number(m, "_DeepSoftness", Mathf.Max(0.001f, m.GetFloat("_DeepSoftness")));
            Number(m, "_CharacterId", Mathf.Clamp(Mathf.RoundToInt(m.GetFloat("_CharacterId")), 1, 255));
            bool cutout = On(m, "_AlphaClip");
            string renderType = transparent ? "Transparent" : cutout ? "TransparentCutout" : "Opaque";
            if (m.GetTag("RenderType", false) != renderType) m.SetOverrideTag("RenderType", renderType);
            int queue = (int)(transparent ? RenderQueue.Transparent : cutout ? RenderQueue.AlphaTest : RenderQueue.Geometry);
            if (m.renderQueue != queue) m.renderQueue = queue;
            Pass(m, "TeamToonOutline", On(m, "_OutlineEnabled"));
            Pass(m, "TeamToonHairMask", On(m, "_IsHair"));
            Pass(m, "TeamToonReveal", On(m, "_RevealEnabled"));
            Pass(m, "TeamToonGhostMask", transparent && On(m, "_DepthPrepass"));
            Pass(m, "TeamToonGhostGlow", transparent && On(m, "_GhostGlowEnabled"));
            bool doubleSided = m.GetFloat("_Cull") == 0;
            if (m.doubleSidedGI != doubleSided) m.doubleSidedGI = doubleSided;
            var gi = On(m, "_EmissionEnabled") && m.GetColor("_EmissionColor").maxColorComponent > 0
                ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (m.globalIlluminationFlags != gi) m.globalIlluminationFlags = gi;
        }

        public static void ApplyPreset(Material m, Preset preset)
        {
            if (!IsSupported(m)) return;
            // Restore appearance values, keeping authored images, UVs, cutout and culling.
            usingDefaults(m, preset);
            Synchronize(m);
        }
        static void usingDefaults(Material m, Preset preset)
        {
            var defaults = new Material(m.shader);
            foreach (string p in new[] { "_ShadeMode","_MulAlpha", "_ShadowThreshold", "_ShadowSoftness", "_DeepThreshold", "_DeepSoftness",
                "_SpecularMode", "_SpecularStrength", "_SpecularWidth", "_SpecularSoftness", "_Metallic", "_Roughness", "_Occlusion",
                "_RimEnabled", "_RimStrength", "_RimPower", "_ReflectionEnabled", "_ReflectionStrength", "_EmissionEnabled",
                "_FaceMode", "_FaceBlend", "_HairShift", "_HairSecondaryStrength", "_HairBandStrength", "_IsHair", "_RevealEnabled",
                "_AmbientStrength", "_AdditionalStrength", "_LightColorInfluence", "_LightIntensityInfluence", "_ColorPreservation",
                "_SurfaceType", "_CoreAlpha", "_EdgeAlpha", "_EdgePower", "_AdditiveBlend", "_GhostRimPower", "_GhostGlowEnabled",
                "_GhostGlowWidth", "_GhostGlowFalloff" })
                m.SetFloat(p, defaults.GetFloat(p));
            m.SetColor("_ShadowColor", defaults.GetColor("_ShadowColor"));
            m.SetColor("_DeepShadowColor", defaults.GetColor("_DeepShadowColor"));
            m.SetColor("_SpecularColor", defaults.GetColor("_SpecularColor"));
            m.SetColor("_GhostRimColor", defaults.GetColor("_GhostRimColor"));
            m.SetColor("_GhostGlowColor", defaults.GetColor("_GhostGlowColor"));
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
                case Preset.Ghost:
                    // Luigi's Mansion-like ghost: see-through body, glowing rim and halo in the body color.
                    m.SetFloat("_SurfaceType", 1); m.SetFloat("_ShadowSoftness", 0.2f); m.SetFloat("_SpecularStrength", 0.35f);
                    m.SetFloat("_Roughness", 0.35f); m.SetFloat("_GhostGlowEnabled", 1);
                    Color body = m.GetColor("_BaseColor");
                    m.SetColor("_ShadowColor", Color.Lerp(body, Color.black, 0.45f));
                    m.SetColor("_GhostRimColor", Color.Lerp(body, Color.white, 0.35f) * 2.5f);
                    m.SetColor("_GhostGlowColor", body * 1.5f); break;
            }
        }
    }
}
