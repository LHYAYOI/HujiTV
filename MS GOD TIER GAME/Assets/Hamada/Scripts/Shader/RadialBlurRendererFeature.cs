using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class RadialBlurRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader m_shader;

    private Material m_material;
    private RadialBlurPass m_pass;

    public override void Create()
    {

        CoreUtils.Destroy(m_material);

        if (m_shader == null)
        {
            m_shader = Shader.Find("Custom/RadialBlur");
        }

        m_material = m_shader != null
            ? CoreUtils.CreateEngineMaterial(m_shader)
            : null;

        m_pass = m_material != null
            ? new RadialBlurPass(m_material)
            : null;
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        RadialBlurController controller = RadialBlurController.Instance;

        if (controller == null)
        {
            return;
        }

        if (controller.Intensity <= 0.0001f)
        {
            return;
        }

        m_pass.Setup(controller.Intensity, controller.Center);

        renderer.EnqueuePass(m_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_material);
        m_material = null;
        m_pass = null;
    }

    private class RadialBlurPass : ScriptableRenderPass
    {
        private readonly Material m_material;

        private float m_intensity;
        private Vector2 m_center;

        private class PassData
        {
            public TextureHandle source;
            public Material material;
        }

        public RadialBlurPass(Material material)
        {
            m_material = material;

            // 透明物まで描画した後にブラーを実行
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

            requiresIntermediateTexture = true;
        }

        public void Setup(float intensity, Vector2 center)
        {
            m_intensity = intensity;
            m_center = center;
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();

            if (resources.isActiveTargetBackBuffer)
            {
                return;
            }

            if (!resources.activeColorTexture.IsValid())
            {
                return;
            }

            if (resources.isActiveTargetBackBuffer ||
                !resources.activeColorTexture.IsValid())
            {
                return;
            }

            // 元の画面テクスチャ
            var source = resources.activeColorTexture;

            // 出力用テクスチャ
            var descriptor = renderGraph.GetTextureDesc(source);

            descriptor.name = "Radial Blur";
            descriptor.clearBuffer = false;

            var destination = renderGraph.CreateTexture(descriptor);

            // シェーダーにパラメータを渡す
            m_material.SetFloat("_BlurStrength", m_intensity);

            m_material.SetVector(
                "_BlurCenter",
                new Vector4(m_center.x, m_center.y, 0, 0)
            );

            using (var builder =
                renderGraph.AddRasterRenderPass<PassData>(
                    "Radial Blur Pass",
                    out var passData))
            {
                passData.source = source;
                passData.material = m_material;

                builder.UseTexture(source);

                builder.SetRenderAttachment(destination, 0);

                builder.SetRenderFunc(
                    (PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(
                            context.cmd,
                            data.source,
                            new Vector4(1, 1, 0, 0),
                            data.material,
                            0
                        );
                    }
                );
            }

            // 描画結果を次の描画処理へ渡す
            resources.cameraColor = destination;
        }
    }
}
