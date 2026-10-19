using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Hatake.Painterly
{
    // PainterlyCamera を持つカメラの画面にポストプロセスを適応する
    public sealed class PainterlyFeature : ScriptableRendererFeature
    {
        [Tooltip("Hidden/Hatakeyama/PainterlyPost。Playerビルドに含めるため明示的に参照する")]
        public Shader shader;
        Material material;
        PainterlyPass pass;
        public static int Executions;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (!shader) shader = Shader.Find("Hidden/Hatakeyama/PainterlyPost");
            if (shader) material = CoreUtils.CreateEngineMaterial(shader);
            pass = new PainterlyPass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            var camera = data.cameraData.camera;
            var settings = camera.GetComponent<PainterlyCamera>();
            if (!material || !settings || !settings.enabled || !settings.renderPainterly || settings.strength <= 0 ||
                camera.cameraType != CameraType.Game || camera.stereoEnabled || data.cameraData.renderType != CameraRenderType.Base) return;
            pass.settings = settings;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        sealed class PainterlyPass : ScriptableRenderPass
        {
            static readonly int TexelId = Shader.PropertyToID("_PainterlyTexel");
            readonly Material material;
            public PainterlyCamera settings;
            sealed class PassData { public Material material; public MaterialPropertyBlock props; public TextureHandle source; }

            public PainterlyPass(Material material)
            {
                this.material = material;
                // IllustrationFeature など同じタイミングの処理より後に積まれ、その結果を絵画風にする
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            void Apply(PainterlyCamera s)
            {
                CoreUtils.SetKeyword(material, "U_KUWAHARA_ON", s.kuwahara);
                CoreUtils.SetKeyword(material, "U_STROKE_ON", s.strokes);
                CoreUtils.SetKeyword(material, "U_CANVAS_ON", s.canvas);
                material.SetFloat("_Contrast", s.contrast);
                material.SetFloat("_Brightness", s.brightness);
                material.SetFloat("_uBrushRadius", s.brushRadius);
                material.SetFloat("_uBrushSharpness", s.brushSharpness);
                material.SetFloat("_uBrushAnisotropy", s.brushAnisotropy);
                material.SetFloat("_uStrokeScale", s.strokeScale);
                material.SetFloat("_uStrokeStrength", s.strokeStrength);
                material.SetFloat("_uCanvasScale", s.canvasScale);
                material.SetFloat("_uCanvasStrength", s.canvasStrength);
                material.SetFloat("_uImpasto", s.impasto);
                material.SetFloat("_uSaturation", s.saturation);
                material.SetFloat("_uWarmth", s.warmth);
                material.SetFloat("_uVignette", s.vignette);
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var desc = graph.GetTextureDesc(source);
                desc.name = "Painterly"; desc.clearBuffer = false;
                var output = graph.CreateTexture(desc);
                Apply(settings);
                using (var b = graph.AddRasterRenderPass<PassData>("Painterly", out var d))
                {
                    d.material = material; d.source = source; d.props = new MaterialPropertyBlock();
                    d.props.SetVector(TexelId, new Vector4(1f / desc.width, 1f / desc.height, desc.height / 1080f, settings.strength));
                    d.props.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                    b.UseTexture(source, AccessFlags.Read);
                    b.SetRenderAttachment(output, 0, AccessFlags.Write);
                    b.SetRenderFunc((PassData data, RasterGraphContext ctx) => {
                        data.props.SetTexture("_BlitTexture", data.source);
                        ctx.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, data.props);
                        Executions++;
                    });
                }
                resources.cameraColor = output;
            }
        }
    }
}
