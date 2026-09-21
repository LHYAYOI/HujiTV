using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace LightShaftLab
{
    public sealed class LightShaftFeature : ScriptableRendererFeature
    {
        public static event System.Action<Camera, int, int, int, int> SelectionUpdated;
        [UnityEngine.Serialization.FormerlySerializedAs("shader")]
        public Shader m_shader;
        public void ResetHistory(Camera camera = null) => m_pass?.ResetHistory(camera);
        public static event System.Action<Camera, string, int, int> HistoryUpdated;
        public static event System.Action<Camera, UnityEngine.Object, float> SelectedSource;
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
            if (volume == null || !volume.m_enableFlag.value || (!volume.IsActive() && !m_pass.HasFading(camera.camera)))
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
            bool m_useFogFlag, m_useGodRaysFlag, m_temporal, m_tiles;
            float m_historyWeight;
            int m_historyHash;
            public bool HasActiveEffects() => m_useFogFlag || m_useGodRaysFlag;
            public bool HasFading(Camera camera)
            {
                if (!m_cameras.TryGetValue(camera.GetInstanceID(), out var state)) return false;
                foreach (var slot in state.fog.slots) if (slot.source && slot.weight > 0) return true;
                return false;
            }

            sealed class CameraSelection
            {
                public Camera camera;
                public double time;
                public readonly ShaftHistory history = new ShaftHistory();
                public readonly ShaftSelection<LightShaftEmitter> spots = new ShaftSelection<LightShaftEmitter>(4);
                public readonly ShaftSelection<LocalFogVolume> fog = new ShaftSelection<LocalFogVolume>(16);
            }
            readonly Dictionary<int, CameraSelection> m_cameras = new Dictionary<int, CameraSelection>();
            readonly List<int> m_expired = new List<int>();
            readonly List<ShaftSelection<LightShaftEmitter>.Candidate> m_spotCandidates = new List<ShaftSelection<LightShaftEmitter>.Candidate>();
            readonly List<ShaftSelection<LocalFogVolume>.Candidate> m_fogCandidates = new List<ShaftSelection<LocalFogVolume>.Candidate>();
            readonly Dictionary<LightShaftEmitter, int> m_shadowIndices = new Dictionary<LightShaftEmitter, int>();
            readonly Dictionary<LightShaftEmitter, Color> m_lightColors = new Dictionary<LightShaftEmitter, Color>();
            readonly Vector4[] m_fogSunBoost = new Vector4[16];
            readonly Vector4[] m_detailRegions = new Vector4[4];
            readonly Vector4[] m_spotRects = new Vector4[4], m_fogRects = new Vector4[16];
            CameraSelection m_selection;
            float m_delta;
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
                var camera = data.cameraData.camera;
                double now = Time.realtimeSinceStartupAsDouble;
                m_expired.Clear();
                foreach (var pair in m_cameras)
                    if (!pair.Value.camera || now - pair.Value.time > 60) m_expired.Add(pair.Key);
                foreach (int id in m_expired) { m_cameras[id].history.Dispose(); m_cameras.Remove(id); }
                if (!m_cameras.TryGetValue(camera.GetInstanceID(), out m_selection))
                {
                    m_selection = new CameraSelection { camera = camera, time = now - 1.0 / 60 };
                    m_cameras.Add(camera.GetInstanceID(), m_selection);
                }
                m_delta = Mathf.Clamp((float)(now - m_selection.time), 0, 0.1f);
                m_selection.time = now;
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
                m_material.SetInt("_ShaftSpotSamples", settings.m_spotMinimumSamples.value);
                m_material.SetInt("_ShaftLocalSamples", settings.m_localFogMinimumSamples.value);
                m_material.SetFloat("_ShaftJitter", settings.m_sampleJitter.value);
                m_material.SetInt("_ShaftRefineRoots", settings.m_refineRootsFlag.value && m_divisor > 1 ? 1 : 0);
                m_material.SetInt("_ShaftRefineEdges", settings.m_refineDepthEdgesFlag.value && m_divisor > 1 ? 1 : 0);
                m_tiles = (settings.m_tileCullingFlag.value || ShaftTileDebug.ForCamera(camera)!=ShaftTileDebugMode.Off) && !camera.stereoEnabled && SystemInfo.IsFormatSupported(UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32_UInt, UnityEngine.Experimental.Rendering.GraphicsFormatUsage.Render);
                CollectLocalFog(settings, ref data);
                CollectLights(settings, ref data);
                m_material.SetInt("_ShaftTilesEnabled",m_tiles?1:0);
                m_material.SetInt("_ShaftTileDebugMode",m_tiles?(int)ShaftTileDebug.ForCamera(camera):0);
                m_material.SetFloat("_ShaftTileDebugOpacity",Mathf.Clamp01(ShaftTileDebug.Opacity));
                m_temporal = settings.m_temporalFlag.value && !camera.stereoEnabled;
                m_material.SetInt("_ShaftTemporalEnabled",m_temporal?1:0);
                m_historyWeight = settings.m_historyWeight.value;
                if(m_temporal) unchecked
                {
                    m_historyHash = settings.GetHashCode();
                    foreach (var v in m_positions) m_historyHash = m_historyHash * 31 + v.GetHashCode();
                    foreach (var v in m_directions) m_historyHash = m_historyHash * 31 + v.GetHashCode();
                    foreach (var v in m_colors) m_historyHash = m_historyHash * 31 + v.GetHashCode();
                    foreach (var v in m_shadowData) m_historyHash = m_historyHash * 31 + v.GetHashCode();
                    foreach (var v in m_fogTransforms) m_historyHash = m_historyHash * 31 + v.GetHashCode();
                    foreach (var v in m_fogSettings) m_historyHash = m_historyHash * 31 + v.GetHashCode();
                    foreach (var v in m_fogColors) m_historyHash = m_historyHash * 31 + v.GetHashCode();
                    foreach (var v in m_fogSunBoost) m_historyHash = m_historyHash * 31 + v.GetHashCode();
                    m_historyHash = m_historyHash * 31 + m_material.GetInt("_ShaftSpotCount");
                    m_historyHash = m_historyHash * 31 + m_material.GetInt("_LocalFogCount");
                    m_historyHash = m_historyHash * 31 + m_material.GetVector("_ShaftSun").GetHashCode();
                    m_historyHash = m_historyHash * 31 + m_material.GetVector("_ShaftSunColor").GetHashCode();
                }
                if (!m_temporal) m_selection.history.Dispose();
            }

            void CollectLocalFog(LightShaftVolume settings, ref RenderingData data)
            {
                m_fogCandidates.Clear();
                var camera = data.cameraData.camera;
                foreach (var fog in LocalFogVolume.g_active)
                {
                    if (!fog || !fog.isActiveAndEnabled || (fog.m_density <= 0 && fog.m_sunBoost <= 0)) continue;
                    var scale = fog.transform.lossyScale;
                    if (Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z))) < 0.0001f) continue;
                    var matrix = fog.transform.localToWorldMatrix;
                    Vector3 extents = (Abs(matrix.MultiplyVector(Vector3.right)) + Abs(matrix.MultiplyVector(Vector3.up)) + Abs(matrix.MultiplyVector(Vector3.forward))) * 0.5f;
                    var bounds = new Bounds(fog.transform.position, extents * 2);
                    if (!GeometryUtility.TestPlanesAABB(m_planes, bounds) || bounds.SqrDistance(camera.transform.position) > settings.m_maxDistance.value * settings.m_maxDistance.value) continue;
                    float contribution = fog.m_density + (settings.m_enableSpatialGodRaysFlag.value && !fog.m_subtractFlag ? fog.m_sunBoost * 0.035f : 0);
                    m_fogCandidates.Add(new ShaftSelection<LocalFogVolume>.Candidate(fog, ShaftSelection<LocalFogVolume>.Score(camera, bounds, contribution, fog.m_priority)));
                }
                m_selection.fog.Update(m_fogCandidates, m_delta, settings.m_selectionFadeSeconds.value, settings.m_selectionHysteresis.value);
                int localCount = 0;
                foreach (var slot in m_selection.fog.slots)
                {
                    var fog = slot.source;
                    if (!fog || slot.weight <= 0) continue;
                    var scale = fog.transform.lossyScale;
                    if (Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z))) < 0.0001f) continue;
                    if(m_tiles)
                    {
                    var matrix = fog.transform.localToWorldMatrix;
                    var extent = (Abs(matrix.MultiplyVector(Vector3.right))+Abs(matrix.MultiplyVector(Vector3.up))+Abs(matrix.MultiplyVector(Vector3.forward)))*.5f;
                    m_fogRects[localCount] = ProjectBounds(camera,new Bounds(fog.transform.position,extent*2));
                    }
                    m_fogTransforms[localCount] = fog.transform.worldToLocalMatrix;
                    m_fogSettings[localCount] = new Vector4((int)fog.m_shape, fog.m_density * slot.weight, fog.m_edgeFade, fog.m_subtractFlag ? 1 : 0);
                    m_fogColors[localCount] = fog.m_tint.linear;
                    m_fogSunBoost[localCount] = new Vector4(settings.m_enableSpatialGodRaysFlag.value && !fog.m_subtractFlag ? fog.m_sunBoost * slot.weight : 0, 0, 0, 0);
                    localCount++;
                }
                m_material.SetVectorArray("_ShaftFogRects",m_fogRects);
                m_material.SetVectorArray("_LocalFogSunBoost", m_fogSunBoost);
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
                m_spotCandidates.Clear(); m_shadowIndices.Clear(); m_lightColors.Clear();
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
                    else if (light.type == LightType.Spot && shadowIndex < data.lightData.additionalLightsCount)
                    {
                        float radius = light.range * Mathf.Tan(light.spotAngle * Mathf.Deg2Rad * 0.5f);
                        Vector3 axis = light.transform.forward;
                        Vector3 end = light.transform.position + axis * light.range;
                        Vector3 disk = new Vector3(Mathf.Sqrt(Mathf.Max(0, 1-axis.x*axis.x)), Mathf.Sqrt(Mathf.Max(0, 1-axis.y*axis.y)), Mathf.Sqrt(Mathf.Max(0, 1-axis.z*axis.z))) * radius;
                        var bounds = new Bounds(light.transform.position, Vector3.zero);
                        bounds.Encapsulate(end - disk); bounds.Encapsulate(end + disk);
                        if (!GeometryUtility.TestPlanesAABB(m_planes, bounds) || bounds.SqrDistance(data.cameraData.camera.transform.position) > settings.m_maxDistance.value * settings.m_maxDistance.value) continue;
                        float brightness = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
                        m_spotCandidates.Add(new ShaftSelection<LightShaftEmitter>.Candidate(emitter, ShaftSelection<LightShaftEmitter>.Score(data.cameraData.camera, bounds, brightness, emitter.m_priority)));
                        m_shadowIndices[emitter] = shadowIndex;
                        m_lightColors[emitter] = color;
                    }
                }
                m_selection.spots.Update(m_spotCandidates, m_delta, settings.m_selectionFadeSeconds.value, settings.m_selectionHysteresis.value);
                foreach (var slot in m_selection.spots.slots)
                {
                    var emitter = slot.source;
                    if (!emitter || slot.weight <= 0 || !emitter.TryGetComponent<Light>(out var light)) continue;
                    // Never reuse a previous frame's shadow atlas index.
                    bool hasShadow = m_shadowIndices.TryGetValue(emitter, out int shadowIndex);
                    Color color = m_lightColors.TryGetValue(emitter, out var visibleColor) ? visibleColor : light.color.linear * light.intensity * emitter.m_scattering;
                    color *= slot.weight;
                    Vector3 position = light.transform.position;
                    Vector3 direction = light.transform.forward;
                    m_positions[count] = new Vector4(position.x, position.y, position.z, light.range);
                    m_directions[count] = new Vector4(direction.x, direction.y, direction.z, Mathf.Cos(light.spotAngle * Mathf.Deg2Rad * 0.5f));
                    m_colors[count] = new Vector4(color.r, color.g, color.b, Mathf.Cos(light.innerSpotAngle * Mathf.Deg2Rad * 0.5f));
                    m_shadowData[count] = new Vector4(shadowIndex, hasShadow && emitter.m_castShadowsFlag && light.shadows != LightShadows.None ? 1 : 0, 1f / Mathf.Max(light.range * light.range, 0.0001f), 1f / Mathf.Max(0.001f, m_colors[count].w - m_directions[count].w));
                    m_detailRegions[count] = RootRegion(data.cameraData.camera, light);
                    if(m_tiles)
                    {
                    float radius=light.range*Mathf.Tan(light.spotAngle*Mathf.Deg2Rad*.5f);
                    Vector3 end=position+direction*light.range;
                    Vector3 disk=new Vector3(Mathf.Sqrt(Mathf.Max(0,1-direction.x*direction.x)),Mathf.Sqrt(Mathf.Max(0,1-direction.y*direction.y)),Mathf.Sqrt(Mathf.Max(0,1-direction.z*direction.z)))*radius;
                    var bounds=new Bounds(position,Vector3.zero);bounds.Encapsulate(end-disk);bounds.Encapsulate(end+disk);
                    m_spotRects[count]=ProjectBounds(data.cameraData.camera,bounds);
                    }
                    count++;
                }
                m_material.SetInt("_ShaftSpotCount", count);
                int fogCount = 0;
                foreach (var slot in m_selection.fog.slots) if (slot.source && slot.weight > 0) fogCount++;
                SelectionUpdated?.Invoke(data.cameraData.camera, m_spotCandidates.Count, count, m_fogCandidates.Count, fogCount);
                if(SelectedSource!=null)
                {
                    foreach(var slot in m_selection.spots.slots)if(slot.source&&slot.weight>0)SelectedSource.Invoke(data.cameraData.camera,slot.source,slot.weight);
                    foreach(var slot in m_selection.fog.slots)if(slot.source&&slot.weight>0)SelectedSource.Invoke(data.cameraData.camera,slot.source,slot.weight);
                }
                m_material.SetVectorArray("_ShaftSpotPosition", m_positions);
                m_material.SetVectorArray("_ShaftSpotDirection", m_directions);
                m_material.SetVectorArray("_ShaftSpotColor", m_colors);
                m_material.SetVectorArray("_ShaftSpotShadow", m_shadowData);
                m_material.SetVectorArray("_ShaftSpotRects",m_spotRects);
                m_material.SetVectorArray("_ShaftDetailRegions", m_detailRegions);
                m_material.SetInt("_ShaftUseFog", m_useFogFlag ? 1 : 0);
                m_material.SetInt("_ShaftUseGodRay", m_useGodRaysFlag ? 1 : 0);
            }

            static Vector4 ProjectBounds(Camera camera,Bounds bounds)
            {
                Vector2 lo=Vector2.one,hi=Vector2.zero;
                for(int i=0;i<8;i++)
                {
                    Vector3 offset=Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Vector3 uv=camera.WorldToViewportPoint(bounds.center+offset);
                    // Clipped or enclosing volumes must remain conservative.
                    if(uv.z<=camera.nearClipPlane)return new Vector4(0,0,1,1);
                    lo=Vector2.Min(lo,uv);hi=Vector2.Max(hi,uv);
                }
                return new Vector4(lo.x,lo.y,hi.x,hi.y);
            }
            static Vector3 Abs(Vector3 settings) => new Vector3(Mathf.Abs(settings.x), Mathf.Abs(settings.y), Mathf.Abs(settings.z));
            Vector4 RootRegion(Camera camera, Light light)
            {
                Vector3 apex = camera.WorldToViewportPoint(light.transform.position);
                if (apex.z <= camera.nearClipPlane) return new Vector4(2, 2, -1, -1);
                float tangent = Mathf.Tan(light.spotAngle * Mathf.Deg2Rad * 0.5f);
                float span = camera.orthographic ? camera.orthographicSize : apex.z * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
                float unitsPerPixel = 2 * span / Mathf.Max(1, camera.pixelHeight);
                float length = Mathf.Min(light.range * 0.25f, 4 * m_divisor * unitsPerPixel / Mathf.Max(0.01f, 2 * tangent));
                float radius = length * tangent;
                Vector3 end = light.transform.position + light.transform.forward * length;
                Vector2 lo = new Vector2(apex.x, apex.y), hi = lo;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = end + new Vector3((i & 1) == 0 ? -radius : radius, (i & 2) == 0 ? -radius : radius, (i & 4) == 0 ? -radius : radius);
                    Vector3 uv = camera.WorldToViewportPoint(corner);
                    if (uv.z <= camera.nearClipPlane) return new Vector4(2, 2, -1, -1);
                    lo = Vector2.Min(lo, uv); hi = Vector2.Max(hi, uv);
                }
                Vector2 padding = new Vector2(2f * m_divisor / Mathf.Max(1, camera.pixelWidth), 2f * m_divisor / Mathf.Max(1, camera.pixelHeight));
                lo -= padding; hi += padding;
                // Large regions already have sufficient screen-space coverage.
                if ((hi.x-lo.x)*(hi.y-lo.y) > 0.05f) return new Vector4(2, 2, -1, -1);
                return new Vector4(lo.x, lo.y, hi.x, hi.y);
            }
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
                m_material.SetVector("_ShaftDebugSize",new Vector4(fullDesc.width,fullDesc.height,0,0));
                textureDescriptor.width = Mathf.Max(1, (textureDescriptor.width + m_divisor - 1) / m_divisor);
                textureDescriptor.height = Mathf.Max(1, (textureDescriptor.height + m_divisor - 1) / m_divisor);
                textureDescriptor.msaaSamples = MSAASamples.None;
                m_material.SetVector("_ShaftLowSize", new Vector4(1f / textureDescriptor.width, 1f / textureDescriptor.height, textureDescriptor.width, textureDescriptor.height));
                textureDescriptor.name = "Light Shaft Scattering";
                textureDescriptor.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
                var fogTexture = graph.CreateTexture(textureDescriptor);
                textureDescriptor.name = "Light Shaft Sample Depth";
                textureDescriptor.colorFormat = m_temporal ? UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32_SFloat : UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
                var fogDepth = graph.CreateTexture(textureDescriptor);
                var cameraData = frameData.Get<UniversalCameraData>();
                var history = m_selection.history;
                if (m_temporal && m_useFogFlag)
                {
                    history.Ensure(textureDescriptor.width, textureDescriptor.height);
                    var camera = cameraData.camera;
                    if (Time.realtimeSinceStartupAsDouble-history.time > 0.25) history.Reset("Frame gap");
                    else if (history.hash != m_historyHash) history.Reset("Lighting / settings / selection changed");
                    else if (Vector3.Distance(history.position,camera.transform.position)>2 || Quaternion.Angle(history.rotation,camera.transform.rotation)>20 || history.projection != camera.projectionMatrix) history.Reset("Camera cut / projection changed");
                    m_material.SetFloat("_ShaftFramePhase", history.valid ? (history.frame * 0.61803398875f) % 1 : 0);
                }
                else m_material.SetFloat("_ShaftFramePhase",0);
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
                    using (var builder = graph.AddRasterRenderPass<PassData>("God Ray Sky Occlusion", out var data, ShaftProfiling.Get(ShaftProfiling.Pass.SkyMask)))
                    {
                        data.m_source = source;
                        data.m_material = m_material;
                        builder.UseTexture(source);
                        builder.UseTexture(resources.cameraDepthTexture);
                        builder.UseAllGlobalTextures(true);
                        builder.SetRenderAttachment(mask, 0);
                        builder.SetRenderFunc((PassData passData, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, passData.m_source, new Vector4(1, 1, 0, 0), passData.m_material, 2));
                    }

                    using (var builder = graph.AddRasterRenderPass<PassData>("God Ray Radial Scattering", out var data, ShaftProfiling.Get(ShaftProfiling.Pass.Radial)))
                    {
                        data.m_source = mask;
                        data.m_material = m_material;
                        builder.UseTexture(mask);
                        builder.SetRenderAttachment(radial, 0);
                        builder.SetRenderFunc((PassData passData, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, passData.m_source, new Vector4(1, 1, 0, 0), passData.m_material, 3));
                    }

                    using (var builder = graph.AddRasterRenderPass<PassData>("God Ray Low Resolution Filter", out var data, ShaftProfiling.Get(ShaftProfiling.Pass.RadialFilter)))
                    {
                        data.m_source = radial;
                        data.m_material = m_material;
                        builder.UseTexture(radial);
                        builder.SetRenderAttachment(godRay, 0);
                        builder.SetGlobalTextureAfterPass(godRay, g_godRayId);
                        builder.SetRenderFunc((PassData passData, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, passData.m_source, new Vector4(1, 1, 0, 0), passData.m_material, 4));
                    }
                }

                TextureHandle tileMasks=default;
                if(m_tiles && m_useFogFlag)
                {
                    var tileDesc=textureDescriptor;
                    tileDesc.width=(textureDescriptor.width+7)/8;tileDesc.height=(textureDescriptor.height+7)/8;
                    tileDesc.colorFormat=UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32_UInt;
                    tileDesc.name="Shaft Tile Masks";
                    tileMasks=graph.CreateTexture(tileDesc);
                    using(var builder=graph.AddRasterRenderPass<PassData>("Light Shaft Tile Candidates",out var data,ShaftProfiling.Get(ShaftProfiling.Pass.Tiles)))
                    {
                        data.m_source=source;data.m_material=m_material;builder.UseTexture(source);
                        builder.SetRenderAttachment(tileMasks,0);
                        builder.SetGlobalTextureAfterPass(tileMasks,Shader.PropertyToID("_ShaftTileMasks"));
                        builder.SetRenderFunc((PassData d,RasterGraphContext context)=>Blitter.BlitTexture(context.cmd,d.m_source,new Vector4(1,1,0,0),d.m_material,6));
                    }
                }
                if (m_useFogFlag)
                    using (var builder = graph.AddRasterRenderPass<PassData>("Volumetric Light Shaft", out var data, ShaftProfiling.Get(ShaftProfiling.Pass.Atmosphere)))
                    {
                        data.m_source = source;
                        data.m_material = m_material;
                        builder.UseTexture(source);
                        if(tileMasks.IsValid())builder.UseTexture(tileMasks);
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

                if (m_temporal && m_useFogFlag)
                {
                    int previous = history.index, next = 1 - previous;
                    var previousColor = graph.ImportTexture(history.color[previous]);
                    var previousDepth = graph.ImportTexture(history.depth[previous]);
                    var temporalColor = graph.ImportTexture(history.color[next]);
                    var temporalDepth = graph.ImportTexture(history.depth[next]);
                    m_material.SetTexture("_ShaftHistoryColor", history.color[previous].rt);
                    m_material.SetTexture("_ShaftHistoryDepth", history.depth[previous].rt);
                    m_material.SetMatrix("_ShaftPreviousVP", history.viewProjection);
                    m_material.SetMatrix("_ShaftPreviousView", history.view);
                    m_material.SetFloat("_ShaftHistoryWeight",history.valid ? m_historyWeight : 0);
                    using (var builder = graph.AddRasterRenderPass<PassData>("Light Shaft Temporal", out var data, ShaftProfiling.Get(ShaftProfiling.Pass.Temporal)))
                    {
                        data.m_source = fogTexture; data.m_material = m_material;
                        builder.UseTexture(fogTexture); builder.UseTexture(fogDepth);
                        builder.UseTexture(previousColor); builder.UseTexture(previousDepth);
                        builder.UseTexture(resources.cameraDepthTexture); builder.UseAllGlobalTextures(true);
                        builder.SetRenderAttachment(temporalColor,0); builder.SetRenderAttachment(temporalDepth,1);
                        builder.SetGlobalTextureAfterPass(temporalColor,g_fogId);
                        builder.SetRenderFunc((PassData passData,RasterGraphContext context) => Blitter.BlitTexture(context.cmd,passData.m_source,new Vector4(1,1,0,0),passData.m_material,5));
                    }
                    HistoryUpdated?.Invoke(cameraData.camera,history.valid ? "Accumulating" : history.reason,history.width,history.height);
                    history.index=next; history.valid=true; history.frame++;
                    history.hash=m_historyHash; history.time=Time.realtimeSinceStartupAsDouble;
                    history.position=cameraData.camera.transform.position; history.rotation=cameraData.camera.transform.rotation;
                    history.projection=cameraData.camera.projectionMatrix; history.view=cameraData.GetViewMatrix();
                    history.viewProjection=cameraData.GetProjectionMatrix()*history.view;
                    fogTexture=temporalColor;
                }

                using (var builder = graph.AddRasterRenderPass<PassData>("Light Shaft Depth Aware Composite", out var data, ShaftProfiling.Get(ShaftProfiling.Pass.Composite)))
                {
                    data.m_source = source;
                    data.m_material = m_material;
                    builder.UseTexture(source);
                    if (m_useFogFlag)
                    {
                        builder.UseTexture(fogTexture);
                        builder.UseTexture(fogDepth);
                    }

                    if(tileMasks.IsValid())builder.UseTexture(tileMasks);
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

            public void ResetHistory(Camera camera)
            {
                foreach(var state in m_cameras.Values) if(!camera || state.camera==camera) state.history.Reset("Explicit reset");
            }
            public void Dispose()
            {
                foreach(var state in m_cameras.Values) state.history.Dispose();
                m_cameras.Clear();
            } // 一時テクスチャはRender Graphが解放する。
        }
    }
}
