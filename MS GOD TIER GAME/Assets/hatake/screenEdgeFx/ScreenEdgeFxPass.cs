using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Hatake.ScreenFx
{
    /// <summary>
    /// 縁取り演出を画面の色へ直接重ねるパス(Unity 6 / URP 17 の RenderGraph 用)。
    /// 画面の色は読まないので、コピーや一時テクスチャは作らない。パスは 1 つだけ。
    /// </summary>
    internal sealed class ScreenEdgeFxPass : ScriptableRenderPass
    {
        private const string k_passName = "Hatake ScreenEdgeFx";
        private const int k_nebulaPass = 0;
        private const int k_grungePass = 1;

        private Mesh m_mesh;
        private Material m_material;
        private bool m_drawNebula;
        private bool m_drawGrunge;

        private sealed class PassData
        {
            public Mesh mesh;
            public Material material;
            public bool drawNebula;
            public bool drawGrunge;
        }

        public ScreenEdgeFxPass()
        {
            profilingSampler = new ProfilingSampler(k_passName);
        }

        public void Setup(Mesh mesh, Material material, bool drawNebula, bool drawGrunge)
        {
            m_mesh = mesh;
            m_material = material;
            m_drawNebula = drawNebula;
            m_drawGrunge = drawGrunge;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // 描く物が無ければ何もしない
            if (m_mesh == null || m_material == null || (!m_drawNebula && !m_drawGrunge))
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle target = resourceData.activeColorTexture;
            if (!target.IsValid())
            {
                return;
            }

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>(k_passName, out PassData passData, profilingSampler))
            {
                passData.mesh = m_mesh;
                passData.material = m_material;
                passData.drawNebula = m_drawNebula;
                passData.drawGrunge = m_drawGrunge;

                // 今ある絵を残したまま上に重ねる
                builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) => Execute(data, context));
            }
        }

        private static void Execute(PassData data, RasterGraphContext context)
        {
            // 星空を先に、ざらついた縁をその上に
            if (data.drawNebula)
            {
                context.cmd.DrawMesh(data.mesh, Matrix4x4.identity, data.material, 0, k_nebulaPass);
            }

            if (data.drawGrunge)
            {
                context.cmd.DrawMesh(data.mesh, Matrix4x4.identity, data.material, 0, k_grungePass);
            }
        }
    }
}
