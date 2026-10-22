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
        [Tooltip("半透明Materialで『重なった裏側を隠す』をONにした部位を、キャラクター識別番号ごとに1つの体として描画します。")]
        public bool ghostMask = true;
        [Tooltip("半透明Materialで『外側の光（ハロー）』をONにしたオブジェクトを描画します。")]
        public bool ghostGlow = true;
        [Tooltip("Team Toonの追加描画対象。通常はEverything。")]
        public LayerMask layerMask = -1;
        ToonExtraPass pass;
        GhostGlowPass glowPass;
        GhostMaskPass maskPass;
        public override void Create()
        {
            pass = new ToonExtraPass { renderPassEvent = RenderPassEvent.AfterRenderingSkybox };
            maskPass = new GhostMaskPass { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
            // After transparents: the ghost bodies' depth prepass then hides the halo behind each body.
            glowPass = new GhostGlowPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.isPreviewCamera) return;
            if (outlines || reveal)
            {
                pass.outlines = outlines; pass.reveal = reveal; pass.layers = layerMask;
                renderer.EnqueuePass(pass);
            }
            if (ghostMask) { maskPass.layers = layerMask; renderer.EnqueuePass(maskPass); }
            if (ghostGlow) { glowPass.layers = layerMask; renderer.EnqueuePass(glowPass); }
        }
        // Nearest ghost fragment per pixel (linear depth, character ID). The regular transparent pass
        // then discards a ghost fragment when a nearer fragment of the same character exists, so separate
        // meshes of one character (body, hands) blend as a single surface while keeping URP lighting.
        sealed class GhostMaskPass : ScriptableRenderPass
        {
            public LayerMask layers;
            class PassData { public RendererListHandle list; }
            static readonly ShaderTagId Tag = new ShaderTagId("TeamToonGhostMask");
            static readonly int MaskId = Shader.PropertyToID("_TeamToonGhostMask");
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                var camera = frame.Get<UniversalCameraData>();
                var rendering = frame.Get<UniversalRenderingData>();
                var lighting = frame.Get<UniversalLightData>();
                var color = graph.GetTextureDesc(resources.activeColorTexture);
                color.name = "Team Toon Ghost Mask";
                color.colorFormat = GraphicsFormat.R32G32_SFloat;
                color.depthBufferBits = DepthBits.None;
                color.msaaSamples = MSAASamples.None; color.bindTextureMS = false;
                color.clearBuffer = true; color.clearColor = Color.clear;
                var mask = graph.CreateTexture(color);
                // Own depth: the nearest ghost wins regardless of opaque occluders, which the
                // transparent pass still tests against the camera depth.
                var depth = color;
                depth.name = "Team Toon Ghost Mask Depth";
                depth.depthBufferBits = DepthBits.Depth32;
                var maskDepth = graph.CreateTexture(depth);
                var drawing = RenderingUtils.CreateDrawingSettings(Tag, rendering, camera, lighting, SortingCriteria.CommonOpaque);
                var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, new FilteringSettings(RenderQueueRange.transparent, layers)));
                using var builder = graph.AddRasterRenderPass<PassData>("TeamToonGhostMask", out var data);
                data.list = list;
                builder.UseRendererList(list);
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(maskDepth, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(mask, MaskId);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((PassData d, RasterGraphContext context) => context.cmd.DrawRendererList(d.list));
            }
        }
        sealed class GhostGlowPass : ScriptableRenderPass
        {
            public LayerMask layers;
            class PassData { public RendererListHandle list; }
            static readonly ShaderTagId Tag = new ShaderTagId("TeamToonGhostGlow");
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                var camera = frame.Get<UniversalCameraData>();
                var rendering = frame.Get<UniversalRenderingData>();
                var lighting = frame.Get<UniversalLightData>();
                var drawing = RenderingUtils.CreateDrawingSettings(Tag, rendering, camera, lighting, SortingCriteria.CommonTransparent);
                var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, new FilteringSettings(RenderQueueRange.transparent, layers)));
                using var builder = graph.AddRasterRenderPass<PassData>("TeamToonGhostGlow", out var data);
                data.list = list;
                builder.UseRendererList(list);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                builder.UseAllGlobalTextures(true); // Reads _TeamToonGhostMask.
                builder.SetRenderFunc((PassData d, RasterGraphContext context) => context.cmd.DrawRendererList(d.list));
            }
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
