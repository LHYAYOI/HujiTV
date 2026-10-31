using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace LightShaftLab
{
    /// <summary>
    /// 距離フォグ。不透明物と空を描いた直後（LightShaft の霧を重ねる前）に、画面全体へフォグを重ねる。
    /// 半透明のシェーダーが同じフォグをかけられるよう、値をグローバルにも設定する（DistanceFogCommon.hlsl）。
    /// </summary>
    public sealed class DistanceFogFeature : ScriptableRendererFeature
    {
        public const string SHADER_NAME = "Hidden/hatake/DistanceFog";

        [Tooltip("Hidden/hatake/DistanceFog。Player ビルドに含めるため明示的に参照する。")]
        public Shader m_shader;

        static readonly int g_paramsId = Shader.PropertyToID("_HatakeFogParams");
        static readonly int g_heightId = Shader.PropertyToID("_HatakeFogHeight");
        static readonly int g_nearColorId = Shader.PropertyToID("_HatakeFogNearColor");
        static readonly int g_farColorId = Shader.PropertyToID("_HatakeFogFarColor");

        Material m_material;
        DistanceFogPass m_pass;

        public override void Create()
        {
            CoreUtils.Destroy(m_material);
            if (!m_shader)
            {
                m_shader = Shader.Find(SHADER_NAME);
            }
            m_material = m_shader ? CoreUtils.CreateEngineMaterial(m_shader) : null;
            m_pass = new DistanceFogPass(m_material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            var fog = VolumeManager.instance.stack.GetComponent<DistanceFogVolume>();
            bool activeFlag = m_material && fog != null && fog.IsActive() && !renderingData.cameraData.isPreviewCamera &&
                (camera.cameraType != CameraType.Reflection || camera.TryGetComponent<DistanceFogCameraOptIn>(out _)) && renderingData.cameraData.renderType == CameraRenderType.Base;
            // 半透明のシェーダーも同じ値を読むので、無効なときも「無効」を設定する
            if (!activeFlag)
            {
                Shader.SetGlobalVector(g_paramsId, Vector4.zero);
                return;
            }
            SetGlobals(fog);
            renderer.EnqueuePass(m_pass);
        }

        // フォグの値をグローバルに設定する
        static void SetGlobals(DistanceFogVolume fog)
        {
            float start = fog.m_startDistance.value;
            if (fog.m_linkToLightShaftFlag.value)
            {
                // LightShaft の霧が届く距離の少し手前から重ね始める
                var shaft = VolumeManager.instance.stack.GetComponent<LightShaftVolume>();
                if (shaft != null && shaft.IsFogActive())
                {
                    start = Mathf.Max(0, shaft.m_maxDistance.value - fog.m_overlap.value);
                }
            }
            Shader.SetGlobalVector(g_paramsId, new Vector4(start, fog.m_density.value, fog.m_maxOpacity.value, 1));
            Shader.SetGlobalVector(g_heightId, new Vector4(fog.m_baseHeight.value, fog.m_heightFalloff.value, fog.m_colorDistance.value, fog.m_applyToSkyFlag.value ? 1 : 0));
            Shader.SetGlobalColor(g_nearColorId, fog.m_nearColor.value.linear);
            Shader.SetGlobalColor(g_farColorId, fog.m_farColor.value.linear);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(m_material);

        sealed class DistanceFogPass : ScriptableRenderPass
        {
            readonly Material m_material;

            sealed class PassData
            {
                public Material m_material;
            }

            public DistanceFogPass(Material material)
            {
                m_material = material;
                // LightShaft の霧（BeforeRenderingTransparents）より前。奥の距離フォグの上に手前の光る霧が重なる
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer)
                {
                    return;
                }
                m_material.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                using (var builder = graph.AddRasterRenderPass<PassData>("Distance Fog", out var data))
                {
                    data.m_material = m_material;
                    if (resources.cameraDepthTexture.IsValid())
                    {
                        builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    }
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((PassData passData, RasterGraphContext context) =>
                    {
                        context.cmd.DrawProcedural(Matrix4x4.identity, passData.m_material, 0, MeshTopology.Triangles, 3, 1);
                    });
                }
            }
        }
    }
}
