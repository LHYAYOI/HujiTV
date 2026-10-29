using System.Collections.Generic;
using UnityEngine;

namespace TeamToon
{
    [DisallowMultipleComponent, AddComponentMenu("Team Toon/モデル単位のマテリアルブレンド")]
    public sealed class TeamToonMaterialBlend : MonoBehaviour
    {
        public Material materialA;
        public Material materialB;
        [Range(0, 1), Tooltip("0: ghost_mtl_blue / 1: charactorSample_001")]
        public float blend;
        [Tooltip("空なら子階層を自動検索。AまたはBが入っているスロットだけが対象です。")]
        public Renderer[] renderers = new Renderer[0];
        readonly List<Slot> slots = new List<Slot>();
        Material instance;
        Material previousA, previousB;
        // 端（blend 0 / 1）でαが 1 になる側。そのときは不透明として描き、後ろが透けないようにする
        bool sourceOpaque, targetOpaque;
        int renderState = -1;
        float depthPrepass;
        sealed class Slot { public Renderer renderer; public int index; public Material original; }

        void Reset()
        {
#if UNITY_EDITOR
            materialA = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/WatanabeKaito/Materials/Enemy/ghost_mtl_blue.mat");
            materialB = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/hatake/chara/sample/charactorSample_001.mat");
#endif
        }
        void OnEnable() => Rebuild();
        void LateUpdate()
        {
            if (previousA != materialA || previousB != materialB) Rebuild();
            if (!instance) return;
            float weight = Mathf.Clamp01(blend);
            instance.SetFloat("_MaterialBlend", weight);
            bool fullyVisible = instance.GetFloat("_MulAlpha") >= 0.999f && instance.GetFloat("_Visibility") >= 0.999f;
            bool opaque = (weight <= 0.001f && sourceOpaque) || (weight >= 0.999f && targetOpaque && fullyVisible);
            ApplyRenderState(opaque);
        }

        // αが 1 の間は不透明の描き方（深度を書き、影を落とし、不透明の順番で描く）にする。
        // 透明のままだと深度を書かないため、自分の奥の部分や後ろの物が描く順番しだいで透けて見える。
        void ApplyRenderState(bool opaque)
        {
            int state = opaque ? 1 : 0;
            if (state == renderState) return;
            renderState = state;
            instance.SetFloat("_ZWrite", opaque ? 1 : 0);
            instance.SetOverrideTag("RenderType", opaque ? "Opaque" : "Transparent");
            instance.renderQueue = (int)(opaque ? UnityEngine.Rendering.RenderQueue.Geometry : UnityEngine.Rendering.RenderQueue.Transparent);
            instance.SetShaderPassEnabled("ShadowCaster", opaque);
            instance.SetShaderPassEnabled("DepthOnly", opaque);
            instance.SetShaderPassEnabled("DepthNormals", opaque);
            // 不透明の間は深度で隠れるので、重なりを隠すマスクは使わない（前のフレームのマスクで自分が消えないように）
            instance.SetFloat("_DepthPrepass", opaque ? 0 : depthPrepass);
            instance.SetShaderPassEnabled("TeamToonGhostMask", !opaque && depthPrepass > 0.5f);
            instance.SetShaderPassEnabled("TeamToonGhostGlow", !opaque && instance.GetFloat("_GhostGlowEnabled") > 0.5f);
        }

        // マテリアルが不透明か（α = 1 で描かれるか）
        static bool IsOpaque(Material material)
        {
            if (!material) return false;
            if (material.HasProperty("_SurfaceType") && material.GetFloat("_SurfaceType") > 0.5f) return false;
            if (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f) return false;
            if (material.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.GeometryLast) return false;
            string color = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            return !material.HasProperty(color) || material.GetColor(color).a >= 0.999f;
        }
        void OnDidApplyAnimationProperties() => LateUpdate();
        [ContextMenu("対象を再取得 / マテリアル設定を再読み込み")]
        public void Rebuild()
        {
            Restore();
            previousA = materialA;
            previousB = materialB;
            // Keep generated materials out of saved scenes and prefabs.
            if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled) return;
            if (!materialA || !TeamToonMaterial.IsSupported(materialB)) return;
            sourceOpaque = IsOpaque(materialA);
            targetOpaque = IsOpaque(materialB);
            renderState = -1;
            instance = new Material(materialB) { name = name + " (Model Blend)", hideFlags = HideFlags.HideAndDontSave };
            instance.SetFloat("_BlendTargetOpaque", targetOpaque ? 1 : 0);
            depthPrepass = instance.GetFloat("_DepthPrepass");
            instance.SetFloat("_SurfaceType", 1);
            instance.SetFloat("_AlphaClip", 0);
            instance.SetFloat("_MaterialBlendEnabled", 1);
            instance.SetFloat("_MaterialBlend", Mathf.Clamp01(blend));
            string map = materialA.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            if (materialA.HasProperty(map))
            {
                instance.SetTexture("_BlendSourceMap", materialA.GetTexture(map));
                instance.SetTextureScale("_BlendSourceMap", materialA.GetTextureScale(map));
                instance.SetTextureOffset("_BlendSourceMap", materialA.GetTextureOffset(map));
            }
            string color = materialA.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            instance.SetColor("_BlendSourceColor", materialA.HasProperty(color) ? materialA.GetColor(color) : Color.white);
            instance.SetFloat("_BlendSourceMetallic", materialA.HasProperty("_Metallic") ? materialA.GetFloat("_Metallic") : 0);
            instance.SetFloat("_BlendSourceSmoothness", materialA.HasProperty("_Smoothness") ? materialA.GetFloat("_Smoothness") : 0.5f);
            instance.SetColor("_BlendSourceEmission", materialA.IsKeywordEnabled("_EMISSION") && materialA.HasProperty("_EmissionColor") ? materialA.GetColor("_EmissionColor") : Color.black);
            TeamToonMaterial.Synchronize(instance);
            // Transparent blended bodies must not leave opaque shadows or depth behind.
            instance.SetShaderPassEnabled("ShadowCaster", false);
            instance.SetShaderPassEnabled("DepthOnly", false);
            instance.SetShaderPassEnabled("DepthNormals", false);
            LateUpdate();
            var targets = renderers != null && renderers.Length > 0 ? renderers : GetComponentsInChildren<Renderer>(true);
            foreach (var target in targets)
            {
                if (!target || (target.GetComponentInParent<TeamToonMaterialBlend>() != this)) continue;
                var materials = target.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != materialA && materials[i] != materialB) continue;
                    slots.Add(new Slot { renderer = target, index = i, original = materials[i] });
                    materials[i] = instance;
                    changed = true;
                }
                if (changed) target.sharedMaterials = materials;
            }
        }
        void OnDisable() => Restore();
        void OnDestroy() => Restore();
        void Restore()
        {
            foreach (var slot in slots)
            {
                if (!slot.renderer) continue;
                var materials = slot.renderer.sharedMaterials;
                if (slot.index >= materials.Length || materials[slot.index] != instance) continue;
                materials[slot.index] = slot.original;
                slot.renderer.sharedMaterials = materials;
            }
            slots.Clear();
            if (instance) { if (Application.isPlaying) Destroy(instance); else DestroyImmediate(instance); }
            instance = null;
        }
    }
}
