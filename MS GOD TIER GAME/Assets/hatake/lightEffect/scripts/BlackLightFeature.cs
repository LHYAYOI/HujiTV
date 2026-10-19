using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace LightShaftLab
{
    /// <summary>
    /// 見えているブラックライト（最大8灯）の位置・円錐・紫外線強度をグローバル配列として渡す。
    /// 全画面パスは持たず、蛍光マテリアル（Fluorescent Lit / Unlit / Lit Dust）が各自で参照する。
    /// 蛍光デカール（FluorescentDecal）は不透明描画の後、光柱の合成の前に箱メッシュとして描く。
    /// </summary>
    public sealed class BlackLightFeature : ScriptableRendererFeature
    {
        public const int MAX_LIGHTS = 8;
        public const string DECAL_SHADER_NAME = "Hidden/hatake/BlackLight/FluorescentDecal";
        [Tooltip("蛍光デカールのシェーダー（Hidden/hatake/BlackLight/FluorescentDecal）。ビルドに含めるため参照を保持します。")]
        public Shader m_decalShader;
        BlackLightPass m_pass;
        FluorescentDecalPass m_decalPass;
        Material m_decalMaterial;

        public override void Create()
        {
            m_pass = new BlackLightPass();
            m_decalPass?.Dispose();
            CoreUtils.Destroy(m_decalMaterial);
            if (!m_decalShader) m_decalShader = Shader.Find(DECAL_SHADER_NAME);
            m_decalMaterial = m_decalShader ? CoreUtils.CreateEngineMaterial(m_decalShader) : null;
            m_decalPass = m_decalMaterial ? new FluorescentDecalPass(m_decalMaterial) : null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_pass == null) return;
            m_pass.Prepare(ref renderingData);
            renderer.EnqueuePass(m_pass);
            var cameraData = renderingData.cameraData;
            if (m_decalPass != null && !cameraData.isPreviewCamera && cameraData.cameraType != CameraType.Reflection && cameraData.renderType != CameraRenderType.Overlay && !cameraData.camera.stereoEnabled
                && m_decalPass.Prepare(cameraData.camera, m_pass.LightCount > 0))
                renderer.EnqueuePass(m_decalPass);
        }

        protected override void Dispose(bool disposing)
        {
            m_decalPass?.Dispose();
            m_decalPass = null;
            CoreUtils.Destroy(m_decalMaterial);
            m_decalMaterial = null;
        }

        sealed class BlackLightPass : ScriptableRenderPass
        {
            static readonly int g_positionId = Shader.PropertyToID("_BlackLightPosition");
            static readonly int g_directionId = Shader.PropertyToID("_BlackLightDirection");
            static readonly int g_paramsId = Shader.PropertyToID("_BlackLightParams");
            static readonly int g_countId = Shader.PropertyToID("_BlackLightCount");
            static readonly int g_gainId = Shader.PropertyToID("_BlackLightGain");

            struct Candidate
            {
                public float m_score;
                public Vector4 m_position, m_direction, m_parameters;
            }

            sealed class PassData
            {
                public Vector4[] m_positions, m_directions, m_parameters;
                public int m_count;
                public float m_gain;
            }

            readonly List<Candidate> m_candidates = new List<Candidate>();
            readonly Vector4[] m_positions = new Vector4[MAX_LIGHTS], m_directions = new Vector4[MAX_LIGHTS], m_parameters = new Vector4[MAX_LIGHTS];
            int m_count;
            float m_gain = 1;
            public int LightCount => m_count;

            public BlackLightPass()
            {
                // 影の描画後・不透明描画前。プリパスは蛍光を参照しない。
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            }

            public void Prepare(ref RenderingData data)
            {
                m_candidates.Clear();
                var volume = VolumeManager.instance.stack.GetComponent<BlackLightVolume>();
                m_gain = volume != null ? volume.m_fluorescenceGain.value : 1;
                Vector3 cameraPosition = data.cameraData.camera.transform.position;
                var lights = data.lightData.visibleLights;
                int additionalLightIndex = 0;
                for (int i = 0; i < lights.Length; i++)
                {
                    // LightShaftFeatureと同じ規則でURPの追加ライト番号（影の参照用）を数える。
                    int shadowIndex = i == data.lightData.mainLightIndex ? -1 : additionalLightIndex++;
                    var light = lights[i].light;
                    if (shadowIndex < 0 || !light || (light.type != LightType.Spot && light.type != LightType.Point)) continue;
                    if (!light.TryGetComponent<BlackLightEmitter>(out var emitter) || emitter.m_priority <= 0) continue;
                    float ultraviolet = emitter.UltravioletIntensity;
                    if (ultraviolet <= 0) continue;

                    Vector3 position = light.transform.position;
                    float range = Mathf.Max(light.range, 0.01f);
                    bool spotFlag = light.type == LightType.Spot;
                    Vector3 forward = light.transform.forward;
                    float outer = spotFlag ? Mathf.Cos(light.spotAngle * Mathf.Deg2Rad * 0.5f) : -2;
                    float inner = spotFlag ? Mathf.Cos(light.innerSpotAngle * Mathf.Deg2Rad * 0.5f) : -1;
                    bool shadowFlag = emitter.m_castShadowsFlag && light.shadows != LightShadows.None && shadowIndex < data.lightData.additionalLightsCount;
                    float distance = Mathf.Max(0, Vector3.Distance(cameraPosition, position) - range);
                    m_candidates.Add(new Candidate
                    {
                        m_score = emitter.m_priority * ultraviolet * range / (1 + distance),
                        m_position = new Vector4(position.x, position.y, position.z, 1f / (range * range)),
                        m_direction = new Vector4(forward.x, forward.y, forward.z, outer),
                        m_parameters = new Vector4(ultraviolet, 1f / Mathf.Max(0.001f, inner - outer), shadowIndex, shadowFlag ? 1 : 0)
                    });
                }
                if (m_candidates.Count > MAX_LIGHTS) m_candidates.Sort((left, right) => right.m_score.CompareTo(left.m_score));
                m_count = Mathf.Min(m_candidates.Count, MAX_LIGHTS);
                for (int i = 0; i < MAX_LIGHTS; i++)
                {
                    bool usedFlag = i < m_count;
                    m_positions[i] = usedFlag ? m_candidates[i].m_position : Vector4.zero;
                    m_directions[i] = usedFlag ? m_candidates[i].m_direction : Vector4.zero;
                    m_parameters[i] = usedFlag ? m_candidates[i].m_parameters : Vector4.zero;
                }
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                using (var builder = graph.AddUnsafePass<PassData>("Black Light Globals", out var data))
                {
                    data.m_positions = m_positions; data.m_directions = m_directions; data.m_parameters = m_parameters;
                    data.m_count = m_count; data.m_gain = m_gain;
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData passData, UnsafeGraphContext context) =>
                    {
                        context.cmd.SetGlobalVectorArray(g_positionId, passData.m_positions);
                        context.cmd.SetGlobalVectorArray(g_directionId, passData.m_directions);
                        context.cmd.SetGlobalVectorArray(g_paramsId, passData.m_parameters);
                        context.cmd.SetGlobalFloat(g_countId, passData.m_count);
                        context.cmd.SetGlobalFloat(g_gainId, passData.m_gain);
                    });
                }
            }
        }
        /// <summary>蛍光デカールの描画。通常光での塗料の色（乗算）→ 紫外線による蛍光（加算）の順に描く。</summary>
        sealed class FluorescentDecalPass : ScriptableRenderPass
        {
            const int MAX_DECALS = 256;
            static readonly int g_maskId = Shader.PropertyToID("_DecalMask");
            static readonly int g_maskScaleId = Shader.PropertyToID("_DecalMask_ST");
            static readonly int g_shapeId = Shader.PropertyToID("_DecalShape");
            static readonly int g_colorId = Shader.PropertyToID("_DecalColor");
            static readonly int g_lightingId = Shader.PropertyToID("_DecalLighting");
            static readonly int g_tintId = Shader.PropertyToID("_DecalTint");
            static readonly int g_maxLuminanceId = Shader.PropertyToID("_DecalMaxLuminance");

            struct DecalDraw
            {
                public Matrix4x4 m_matrix;
                public Texture m_texture;
                public Vector4 m_maskScale, m_shape, m_color, m_lighting, m_tint;
                public float m_maxLuminance;
                public bool m_tintFlag, m_emissionFlag;
            }

            sealed class PassData
            {
                public Material m_material;
                public Mesh m_mesh;
                public MaterialPropertyBlock m_properties;
                public List<DecalDraw> m_draws;
            }

            readonly Material m_material;
            readonly MaterialPropertyBlock m_properties = new MaterialPropertyBlock();
            readonly List<DecalDraw> m_draws = new List<DecalDraw>();
            readonly Plane[] m_planes = new Plane[6];
            Mesh m_cube;

            public FluorescentDecalPass(Material material)
            {
                m_material = material;
                // 深度コピーの後、光柱（BeforeRenderingTransparents）の合成より前。霧はデカールの上に掛かる。
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents - 1;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                requiresIntermediateTexture = true;
            }

            /// <summary>カメラから見えるデカールを集める。描くものが無ければ false。</summary>
            public bool Prepare(Camera camera, bool hasBlackLightFlag)
            {
                m_draws.Clear();
                var decals = FluorescentDecal.Active;
                if (decals.Count == 0) return false;
                GeometryUtility.CalculateFrustumPlanes(camera, m_planes);
                Vector3 cameraPosition = camera.transform.position;
                for (int i = 0; i < decals.Count && m_draws.Count < MAX_DECALS; i++)
                {
                    var decal = decals[i];
                    if (!decal || (camera.cullingMask & (1 << decal.gameObject.layer)) == 0) continue;
                    bool emissionFlag = hasBlackLightFlag && decal.HasFluorescence, tintFlag = decal.HasDaylightTint;
                    if (!emissionFlag && !tintFlag) continue;
                    var bounds = decal.WorldBounds;
                    if (!GeometryUtility.TestPlanesAABB(m_planes, bounds) || bounds.SqrDistance(cameraPosition) > decal.m_drawDistance * decal.m_drawDistance) continue;
                    bool texturedFlag = decal.m_shape == FluorescentDecal.SHAPE.TEXTURE;
                    Color fluorescent = decal.m_fluorescentColor.linear;
                    Color daylight = decal.m_daylightTint.linear;
                    m_draws.Add(new DecalDraw
                    {
                        m_matrix = decal.BoxMatrix,
                        m_texture = texturedFlag && decal.m_mask ? decal.m_mask : Texture2D.whiteTexture,
                        m_maskScale = new Vector4(decal.m_tiling.x, decal.m_tiling.y, decal.m_offset.x, decal.m_offset.y),
                        m_shape = new Vector4((int)decal.m_shape, (int)decal.m_maskChannel, decal.m_edgeSoftness, decal.m_opacity),
                        m_color = new Vector4(fluorescent.r, fluorescent.g, fluorescent.b, decal.m_efficiency),
                        m_lighting = new Vector4(decal.m_wrap, Mathf.Cos(decal.m_angleFadeStart * Mathf.Deg2Rad), Mathf.Cos(decal.m_angleFadeEnd * Mathf.Deg2Rad), decal.m_depthFade),
                        m_tint = new Vector4(daylight.r, daylight.g, daylight.b, decal.m_daylightOpacity),
                        m_maxLuminance = decal.m_maxLuminance,
                        m_tintFlag = tintFlag, m_emissionFlag = emissionFlag,
                    });
                }
                return m_draws.Count > 0;
            }

            /// <summary>投影の箱となる単位立方体メッシュを取得する（初回のみ生成）。</summary>
            Mesh GetCubeMesh()
            {
                if (m_cube) return m_cube;
                var vertices = new Vector3[8];
                for (int i = 0; i < 8; i++) vertices[i] = new Vector3((i & 1) - 0.5f, ((i >> 1) & 1) - 0.5f, ((i >> 2) & 1) - 0.5f);
                // 外向きの面（Cull Front で裏面＝奥側を描く）。
                int[] indices =
                {
                    0, 2, 3, 0, 3, 1, // -Z
                    4, 5, 7, 4, 7, 6, // +Z
                    0, 4, 6, 0, 6, 2, // -X
                    1, 3, 7, 1, 7, 5, // +X
                    0, 1, 5, 0, 5, 4, // -Y
                    2, 6, 7, 2, 7, 3, // +Y
                };
                m_cube = new Mesh { name = "Fluorescent Decal Box", hideFlags = HideFlags.HideAndDontSave, vertices = vertices, triangles = indices };
                m_cube.bounds = new Bounds(Vector3.zero, Vector3.one);
                return m_cube;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid()) return;
                using (var builder = graph.AddRasterRenderPass<PassData>("Fluorescent Decals", out var data))
                {
                    data.m_material = m_material; data.m_mesh = GetCubeMesh(); data.m_properties = m_properties; data.m_draws = m_draws;
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.UseTexture(resources.cameraDepthTexture);
                    if (resources.mainShadowsTexture.IsValid()) builder.UseTexture(resources.mainShadowsTexture);
                    if (resources.additionalShadowsTexture.IsValid()) builder.UseTexture(resources.additionalShadowsTexture);
                    builder.UseAllGlobalTextures(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (PassData passData, RasterGraphContext context) =>
                    {
                        // 乗算（塗料の色）を先に、加算（蛍光）を後に描く。
                        for (int pass = 0; pass < 2; pass++)
                        {
                            foreach (var draw in passData.m_draws)
                            {
                                if (pass == 0 ? !draw.m_tintFlag : !draw.m_emissionFlag) continue;
                                var properties = passData.m_properties;
                                properties.Clear();
                                properties.SetTexture(g_maskId, draw.m_texture);
                                properties.SetVector(g_maskScaleId, draw.m_maskScale);
                                properties.SetVector(g_shapeId, draw.m_shape);
                                properties.SetVector(g_colorId, draw.m_color);
                                properties.SetVector(g_lightingId, draw.m_lighting);
                                properties.SetVector(g_tintId, draw.m_tint);
                                properties.SetFloat(g_maxLuminanceId, draw.m_maxLuminance);
                                context.cmd.DrawMesh(passData.m_mesh, draw.m_matrix, passData.m_material, 0, pass, properties);
                            }
                        }
                    });
                }
            }

            public void Dispose()
            {
                CoreUtils.Destroy(m_cube);
                m_cube = null;
            }
        }
    }
}
