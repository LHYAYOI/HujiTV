using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace LightShaftLab
{
    public sealed class LightShaftFeature : ScriptableRendererFeature
    {
        [UnityEngine.Serialization.FormerlySerializedAs("shader")]
        public Shader m_shader;
        Material m_material;
        ShaftPass m_pass;
        public override void Create()
        {
            m_pass?.Dispose();
            CoreUtils.Destroy(m_material);
            if (m_shader)
                m_material = CoreUtils.CreateEngineMaterial(m_shader);
            m_pass = new ShaftPass(m_material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData;
            if (!m_material || camera.isPreviewCamera || camera.cameraType == CameraType.Reflection || camera.renderType == CameraRenderType.Overlay)
                return;
            var volume = VolumeManager.instance.stack.GetComponent<LightShaftVolume>();
            if (volume == null || !volume.IsActive())
                return;
            m_pass.Prepare(volume, ref renderingData);
            if (m_pass.HasActiveEffects())
                renderer.EnqueuePass(m_pass);
        }

        protected override void Dispose(bool disposingFlag)
        {
            m_pass?.Dispose();
            CoreUtils.Destroy(m_material);
        }

        sealed class ShaftPass : ScriptableRenderPass
        {
            readonly Material m_material;
            static readonly int g_fogId = Shader.PropertyToID("_ShaftFog");
            static readonly int g_fogDepthId = Shader.PropertyToID("_ShaftFogDepth"), g_godRayId = Shader.PropertyToID("_ShaftGodRay");
            int m_divisor;
            bool m_useFogFlag, m_useGodRaysFlag;
            public bool HasActiveEffects() => m_useFogFlag || m_useGodRaysFlag;

            readonly Plane[] m_planes = new Plane[6];
            readonly Vector4[] m_positions = new Vector4[4], m_directions = new Vector4[4], m_colors = new Vector4[4], m_shadowData = new Vector4[4];
            readonly Matrix4x4[] m_fogTransforms = new Matrix4x4[16];
            readonly Vector4[] m_fogSettings = new Vector4[16], m_fogColors = new Vector4[16];
            public ShaftPass(Material material)
            {
                this.m_material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            public void Prepare(LightShaftVolume settings, ref RenderingData data)
            {
                m_divisor = settings.m_resolutionDivisor.value;
                m_useFogFlag = settings.IsFogActive();
                m_useGodRaysFlag = false;
                GeometryUtility.CalculateFrustumPlanes(data.cameraData.camera, m_planes);
                m_material.SetVector("_ShaftMedium", new Vector4(settings.m_density.value, settings.m_intensity.value, settings.m_maxDistance.value, settings.m_anisotropy.value));
                m_material.SetVector("_ShaftHeightNoise", new Vector4(settings.m_baseHeight.value, settings.m_heightFalloff.value, settings.m_noiseAmount.value, settings.m_noiseScale.value));
                m_material.SetVector("_ShaftWind", settings.m_wind.value);
                m_material.SetColor("_ShaftTint", settings.m_scatteringTint.value);
                m_material.SetFloat("_ShaftAmbient", settings.m_ambient.value);
                m_material.SetInt("_ShaftSteps", settings.m_steps.value);
                CollectLocalFog(settings, ref data);
                CollectLights(settings, ref data);
            }

            void CollectLocalFog(LightShaftVolume settings, ref RenderingData data)
            {
                int localCount = 0;
                foreach (var fog in LocalFogVolume.g_active)
                {
                    if (!fog || !fog.isActiveAndEnabled || fog.m_density <= 0 || localCount == 16)
                        continue;
                    var scale = fog.transform.lossyScale;
                    if (Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z))) < 0.0001f)
                        continue;
                    var matrix = fog.transform.localToWorldMatrix;
                    Vector3 extents = (Abs(matrix.MultiplyVector(Vector3.right)) + Abs(matrix.MultiplyVector(Vector3.up)) + Abs(matrix.MultiplyVector(Vector3.forward))) * 0.5f;
                    var bounds = new Bounds(fog.transform.position, extents * 2);
                    if (!GeometryUtility.TestPlanesAABB(m_planes, bounds) || bounds.SqrDistance(data.cameraData.camera.transform.position) > settings.m_maxDistance.value * settings.m_maxDistance.value)
                        continue;
                    m_fogTransforms[localCount] = fog.transform.worldToLocalMatrix;
                    m_fogSettings[localCount] = new Vector4((int)fog.m_shape, fog.m_density, fog.m_edgeFade, fog.m_subtractFlag ? 1 : 0);
                    m_fogColors[localCount] = fog.m_tint.linear;
                    localCount++;
                }

                m_material.SetInt("_LocalFogCount", localCount);
                m_material.SetMatrixArray("_LocalFogTransforms", m_fogTransforms);
                m_useFogFlag = settings.m_intensity.value > 0 && (settings.m_density.value > 0 || localCount > 0);
                m_material.SetVectorArray("_LocalFogSettings", m_fogSettings);
                m_material.SetVectorArray("_LocalFogColors", m_fogColors);
            }

            void CollectLights(LightShaftVolume settings, ref RenderingData data)
            {
                m_material.SetVector("_ShaftSun", Vector4.zero);
                m_material.SetVector("_ShaftSunColor", Vector4.zero);
                int count = 0, additional = 0;
                var lights = data.lightData.visibleLights;
                for (int i = 0; i < lights.Length; i++)
                {
                    var visible = lights[i];
                    var light = visible.light;
                    bool isMainLightFlag = i == data.lightData.mainLightIndex;
                    int shadowIndex = isMainLightFlag ? -1 : additional++;
                    if (isMainLightFlag && light && light.type == LightType.Directional && settings.m_enableGodRaysFlag.value)
                    {
                        var camera = data.cameraData.camera;
                        Vector3 viewport = camera.WorldToViewportPoint(camera.transform.position - light.transform.forward * camera.farClipPlane * 0.9f);
                        float edge = Mathf.Max(Mathf.Abs(viewport.x - 0.5f), Mathf.Abs(viewport.y - 0.5f));
                        float fade = viewport.z > 0 ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.45f, 0.8f, edge)) : 0;
                        m_useGodRaysFlag = fade > 0 && settings.m_godRayIntensity.value > 0;
                        m_material.SetVector("_GodRaySun", new Vector4(viewport.x, viewport.y, settings.m_godRayRadius.value, settings.m_godRayIntensity.value * fade));
                        m_material.SetColor("_GodRayColor", visible.finalColor);
                        m_material.SetInt("_GodRaySamples", settings.m_godRaySamples.value);
                    }

                    // 空間方式と旧Emitterで太陽の散乱を二重加算しない
                    if (isMainLightFlag && light && light.type == LightType.Directional && settings.m_enableSpatialGodRaysFlag.value)
                    {
                        Vector3 direction = -light.transform.forward;
                        m_material.SetVector("_ShaftSun", new Vector4(direction.x, direction.y, direction.z, light.shadows != LightShadows.None ? 1 : 0));
                        m_material.SetVector("_ShaftSunColor", visible.finalColor * settings.m_spatialGodRayIntensity.value);
                        continue;
                    }

                    if (!light || !light.TryGetComponent<LightShaftEmitter>(out var emitter) || !emitter.isActiveAndEnabled || emitter.m_scattering <= 0)
                        continue;
                    Color color = visible.finalColor * emitter.m_scattering;
                    if (isMainLightFlag && light.type == LightType.Directional)
                    {
                        Vector3 direction = -light.transform.forward;
                        m_material.SetVector("_ShaftSun", new Vector4(direction.x, direction.y, direction.z, emitter.m_castShadowsFlag ? 1 : 0));
                        m_material.SetVector("_ShaftSunColor", color);
                    }
                    else if (light.type == LightType.Spot && count < 4 && shadowIndex < data.lightData.additionalLightsCount)
                    {
                        Vector3 position = light.transform.position;
                        Vector3 direction = light.transform.forward;
                        m_positions[count] = new Vector4(position.x, position.y, position.z, light.range);
                        m_directions[count] = new Vector4(direction.x, direction.y, direction.z, Mathf.Cos(light.spotAngle * Mathf.Deg2Rad * 0.5f));
                        m_colors[count] = new Vector4(color.r, color.g, color.b, Mathf.Cos(light.innerSpotAngle * Mathf.Deg2Rad * 0.5f));
                        m_shadowData[count] = new Vector4(shadowIndex, emitter.m_castShadowsFlag && light.shadows != LightShadows.None ? 1 : 0, 1f / Mathf.Max(light.range * light.range, 0.0001f), 1f / Mathf.Max(0.001f, m_colors[count].w - m_directions[count].w));
                        count++;
                    }
                }

                m_material.SetInt("_ShaftSpotCount", count);
                m_material.SetVectorArray("_ShaftSpotPosition", m_positions);
                m_material.SetVectorArray("_ShaftSpotDirection", m_directions);
                m_material.SetVectorArray("_ShaftSpotColor", m_colors);
                m_material.SetVectorArray("_ShaftSpotShadow", m_shadowData);
                m_material.SetInt("_ShaftUseFog", m_useFogFlag ? 1 : 0);
                m_material.SetInt("_ShaftUseGodRay", m_useGodRaysFlag ? 1 : 0);
            }

            static Vector3 Abs(Vector3 settings) => new Vector3(Mathf.Abs(settings.x), Mathf.Abs(settings.y), Mathf.Abs(settings.z));
            class PassData
            {
                public TextureHandle m_source;
                public Material m_material;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer)
                    return;
                var source = resources.activeColorTexture;
                var textureDescriptor = graph.GetTextureDesc(source);
                textureDescriptor.name = "Light Shaft Color";
                textureDescriptor.clearBuffer = false;
                var output = graph.CreateTexture(textureDescriptor);
                var fullDesc = textureDescriptor;
                textureDescriptor.width = Mathf.Max(1, (textureDescriptor.width + m_divisor - 1) / m_divisor);
                textureDescriptor.height = Mathf.Max(1, (textureDescriptor.height + m_divisor - 1) / m_divisor);
                textureDescriptor.msaaSamples = MSAASamples.None;
                m_material.SetVector("_ShaftLowSize", new Vector4(1f / textureDescriptor.width, 1f / textureDescriptor.height, textureDescriptor.width, textureDescriptor.height));
                textureDescriptor.name = "Light Shaft Scattering";
                textureDescriptor.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
                var fogTexture = graph.CreateTexture(textureDescriptor);
                textureDescriptor.name = "Light Shaft Sample Depth";
                textureDescriptor.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
                var fogDepth = graph.CreateTexture(textureDescriptor);
                TextureHandle godRay = default;
                if (m_useGodRaysFlag)
                {
                    var godDesc = fullDesc;
                    godDesc.width = Mathf.Max(1, (godDesc.width + 3) / 4);
                    godDesc.height = Mathf.Max(1, (godDesc.height + 3) / 4);
                    m_material.SetVector("_GodRayTexelSize", new Vector4(1f / godDesc.width, 1f / godDesc.height, godDesc.width, godDesc.height));
                    godDesc.msaaSamples = MSAASamples.None;
                    godDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat;
                    godDesc.name = "God Ray Occlusion";
                    var mask = graph.CreateTexture(godDesc);
                    godDesc.name = "God Ray Radial";
                    var radial = graph.CreateTexture(godDesc);
                    godDesc.name = "God Ray Filtered";
                    godRay = graph.CreateTexture(godDesc);
                    using (var builder = graph.AddRasterRenderPass<PassData>("God Ray Sky Occlusion", out var data))
                    {
                        data.m_source = source;
                        data.m_material = m_material;
                        builder.UseTexture(source);
                        builder.UseTexture(resources.cameraDepthTexture);
                        builder.UseAllGlobalTextures(true);
                        builder.SetRenderAttachment(mask, 0);
                        builder.SetRenderFunc((PassData passData, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, passData.m_source, new Vector4(1, 1, 0, 0), passData.m_material, 2));
                    }

                    using (var builder = graph.AddRasterRenderPass<PassData>("God Ray Radial Scattering", out var data))
                    {
                        data.m_source = mask;
                        data.m_material = m_material;
                        builder.UseTexture(mask);
                        builder.SetRenderAttachment(radial, 0);
                        builder.SetRenderFunc((PassData passData, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, passData.m_source, new Vector4(1, 1, 0, 0), passData.m_material, 3));
                    }

                    using (var builder = graph.AddRasterRenderPass<PassData>("God Ray Low Resolution Filter", out var data))
                    {
                        data.m_source = radial;
                        data.m_material = m_material;
                        builder.UseTexture(radial);
                        builder.SetRenderAttachment(godRay, 0);
                        builder.SetGlobalTextureAfterPass(godRay, g_godRayId);
                        builder.SetRenderFunc((PassData passData, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, passData.m_source, new Vector4(1, 1, 0, 0), passData.m_material, 4));
                    }
                }

                if (m_useFogFlag)
                    using (var builder = graph.AddRasterRenderPass<PassData>("Volumetric Light Shaft", out var data))
                    {
                        data.m_source = source;
                        data.m_material = m_material;
                        builder.UseTexture(source);
                        if (resources.cameraDepthTexture.IsValid())
                            builder.UseTexture(resources.cameraDepthTexture);
                        if (resources.mainShadowsTexture.IsValid())
                            builder.UseTexture(resources.mainShadowsTexture);
                        if (resources.additionalShadowsTexture.IsValid())
                            builder.UseTexture(resources.additionalShadowsTexture);
                        builder.UseAllGlobalTextures(true);
                        builder.SetRenderAttachment(fogTexture, 0);
                        builder.SetRenderAttachment(fogDepth, 1);
                        builder.SetGlobalTextureAfterPass(fogTexture, g_fogId);
                        builder.SetGlobalTextureAfterPass(fogDepth, g_fogDepthId);
                        builder.SetRenderFunc((PassData passData, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, passData.m_source, new Vector4(1, 1, 0, 0), passData.m_material, 0));
                    }

                using (var builder = graph.AddRasterRenderPass<PassData>("Light Shaft Depth Aware Composite", out var data))
                {
                    data.m_source = source;
                    data.m_material = m_material;
                    builder.UseTexture(source);
                    if (m_useFogFlag)
                    {
                        builder.UseTexture(fogTexture);
                        builder.UseTexture(fogDepth);
                    }

                    if (m_useGodRaysFlag)
                        builder.UseTexture(godRay);
                    if (resources.cameraDepthTexture.IsValid())
                        builder.UseTexture(resources.cameraDepthTexture);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(output, 0);
                    builder.SetRenderFunc((PassData passData, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, passData.m_source, new Vector4(1, 1, 0, 0), passData.m_material, 1));
                }

                resources.cameraColor = output;
            }

            public void Dispose()
            {
            } // 一時テクスチャはRender Graphが解放する。
        }
    }
}
