using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TeamToon
{
    public sealed class TeamToonRendererFeature : ScriptableRendererFeature
    {
        [Tooltip("Materialで輪郭線をONにしたオブジェクトを描画します。")]
        public bool outlines = true;
        [Tooltip("Materialで指定した髪の奥にある眉・瞳を描画します。")]
        public bool reveal = true;
        [Tooltip("Team Toonの追加描画対象。通常はEverything。")]
        public LayerMask layerMask = -1;
        ToonExtraPass pass;
        public override void Create() => pass = new ToonExtraPass { renderPassEvent = RenderPassEvent.AfterRenderingSkybox };
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if ((!outlines && !reveal) || renderingData.cameraData.isPreviewCamera) return;
            pass.outlines = outlines; pass.reveal = reveal; pass.layers = layerMask;
            renderer.EnqueuePass(pass);
        }
        sealed class ToonExtraPass : ScriptableRenderPass
        {
            public bool outlines, reveal;
            public LayerMask layers;
            class PassData { public RendererListHandle list; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                var camera = frame.Get<UniversalCameraData>();
                var rendering = frame.Get<UniversalRenderingData>();
                var lighting = frame.Get<UniversalLightData>();
                TextureHandle hair = default;
                if (reveal)
                {
                    var descriptor = graph.GetTextureDesc(resources.activeColorTexture);
                    descriptor.name = "Team Toon Visible Hair";
                    descriptor.colorFormat = GraphicsFormat.R32G32_SFloat;
                    descriptor.depthBufferBits = DepthBits.None;
                    // Match the depth attachment sample count (ZTest Equal).
                    descriptor.clearBuffer = true; descriptor.clearColor = Color.clear;
                    hair = graph.CreateTexture(descriptor);
                    Draw("TeamToonHairMask", hair, false, true);
                }
                if (outlines) Draw("TeamToonOutline", resources.activeColorTexture, false, false);
                if (reveal) Draw("TeamToonReveal", resources.activeColorTexture, true, false);

                void Draw(string tag, TextureHandle target, bool readsHair, bool writesHair)
                {
                    var drawing = RenderingUtils.CreateDrawingSettings(new ShaderTagId(tag), rendering, camera, lighting, SortingCriteria.CommonOpaque);
                    var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, new FilteringSettings(RenderQueueRange.opaque, layers)));
                    using var builder = graph.AddRasterRenderPass<PassData>(tag, out var data);
                    data.list = list;
                    builder.UseRendererList(list);
                    builder.SetRenderAttachment(target, 0, writesHair ? AccessFlags.Write : AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    if (readsHair) { builder.UseTexture(hair, AccessFlags.Read); builder.UseAllGlobalTextures(true); }
                    if (writesHair) builder.SetGlobalTextureAfterPass(hair, Shader.PropertyToID("_TeamToonHairMask"));
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) => context.cmd.DrawRendererList(d.list));
                }
            }
        }
    }
}
