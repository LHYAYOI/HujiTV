using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace LightShaftLab
{
    public sealed class LightShaftFeature : ScriptableRendererFeature
    {
        public static event System.Action<Camera, int, int, int, int> g_selectionUpdated;
        [UnityEngine.Serialization.FormerlySerializedAs("shader")]
        public Shader m_shader;
        public void ResetHistory(Camera camera = null) => m_pass?.ResetHistory(camera);
        public static event System.Action<Camera, string, int, int> g_historyUpdated;
        public static event System.Action<Camera, UnityEngine.Object, float> g_selectedSource;
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
            bool m_useFogFlag, m_useGodRaysFlag, m_temporalFlag, m_tilesFlag;
            float m_historyWeight;
            int m_historyHash;
            public bool HasActiveEffects() => m_useFogFlag || m_useGodRaysFlag;
            public bool HasFading(Camera camera)
            {
                if (!m_cameras.TryGetValue(camera.GetInstanceID(), out var state)) return false;
                foreach (var slot in state.m_fog.m_slots) if (slot.m_source && slot.m_weight > 0) return true;
                return false;
            }

            // カメラごとの選択状態と時間方向の履歴。
            sealed class CameraSelection
            {
                public Camera m_camera;
                public double m_time;
                public readonly ShaftHistory m_history = new ShaftHistory();
                public readonly ShaftSelection<LightShaftEmitter> m_spots = new ShaftSelection<LightShaftEmitter>(4);
                public readonly ShaftSelection<LocalFogVolume> m_fog = new ShaftSelection<LocalFogVolume>(16);
            }
            readonly Dictionary<int, CameraSelection> m_cameras = new Dictionary<int, CameraSelection>();
            readonly List<int> m_expired = new List<int>();
            readonly List<ShaftSelection<LightShaftEmitter>.Candidate> m_spotCandidates = new List<ShaftSelection<LightShaftEmitter>.Candidate>();
            readonly List<ShaftSelection<LocalFogVolume>.Candidate> m_fogCandidates = new List<ShaftSelection<LocalFogVolume>.Candidate>();
            readonly Dictionary<LightShaftEmitter, int> m_shadowIndices = new Dictionary<LightShaftEmitter, int>();
            readonly Dictionary<LightShaftEmitter, Color> m_lightColors = new Dictionary<LightShaftEmitter, Color>();
            readonly Vector4[] m_fogSunBoost = new Vector4[16];
            readonly Vector4[] m_spotFluorescence = new Vector4[4];
            readonly Vector4[] m_detailRegions = new Vector4[4];
            readonly Vector4[] m_spotRects = new Vector4[4], m_fogRects = new Vector4[16];
            CameraSelection m_selection;
            float m_delta;
            readonly Plane[] m_planes = new Plane[6];
            readonly Vector4[] m_positions = new Vector4[4], m_directions = new Vector4[4], m_colors = new Vector4[4], m_shadowData = new Vector4[4];
            Texture2D m_beamNoise;
            readonly Vector4[] m_beamStyle = new Vector4[4], m_beamRight = new Vector4[4], m_beamUp = new Vector4[4], m_beamAxes = new Vector4[4], m_beamSettings = new Vector4[4];
            readonly Matrix4x4[] m_fogTransforms = new Matrix4x4[16];
            readonly Matrix4x4[] m_fogMotionBounds = new Matrix4x4[16];
            readonly Vector4[] m_fogMotion = new Vector4[16], m_motionRects = new Vector4[8];
            int m_motionRectCount;
            readonly TextureHandle[] m_motionTextures = new TextureHandle[4];
            readonly Vector4[] m_fogSettings = new Vector4[16], m_fogColors = new Vector4[16];
            public ShaftPass(Material material)
            {
                this.m_material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
                requiresIntermediateTexture = true;
            }

            // フレームごとに Volume 設定・ライト・局所フォグを集めてマテリアルへ渡し、時間方向の履歴用ハッシュを更新する。
            public void Prepare(LightShaftVolume settings, ref RenderingData data)
            {
                var camera = data.cameraData.camera;
                double now = Time.realtimeSinceStartupAsDouble;
                m_expired.Clear();
                foreach (var pair in m_cameras)
                    if (!pair.Value.m_camera || now - pair.Value.m_time > 60) m_expired.Add(pair.Key);
                foreach (int id in m_expired) { m_cameras[id].m_history.Dispose(); m_cameras.Remove(id); }
                if (!m_cameras.TryGetValue(camera.GetInstanceID(), out m_selection))
                {
                    m_selection = new CameraSelection { m_camera = camera, m_time = now - 1.0 / 60 };
                    m_cameras.Add(camera.GetInstanceID(), m_selection);
                }
                m_delta = Mathf.Clamp((float)(now - m_selection.m_time), 0, 0.1f);
                m_selection.m_time = now;
                m_divisor = settings.m_resolutionDivisor.value;
                m_useFogFlag = settings.IsFogActive();
                m_useGodRaysFlag = false;
                GeometryUtility.CalculateFrustumPlanes(data.cameraData.camera, m_planes);
                var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                m_material.SetInt("_ShaftSunCascadeCount", pipeline ? pipeline.shadowCascadeCount : 1);
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
                m_tilesFlag = (settings.m_tileCullingFlag.value || ShaftTileDebug.ForCamera(camera) != SHAFT_TILE_DEBUG_MODE.OFF) && !camera.stereoEnabled && SystemInfo.IsFormatSupported(UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32_UInt, UnityEngine.Experimental.Rendering.GraphicsFormatUsage.Render);
                CollectLocalFog(settings, ref data);
                CollectLights(settings, ref data);
                m_material.SetInt("_ShaftTilesEnabled", m_tilesFlag ? 1 : 0);
                m_material.SetInt("_ShaftTileDebugMode", m_tilesFlag ? (int)ShaftTileDebug.ForCamera(camera) : 0);
                m_material.SetFloat("_ShaftTileDebugOpacity", Mathf.Clamp01(ShaftTileDebug.g_opacity));
                m_material.SetInt("_ShaftDetailDebug", ShaftTileDebug.g_camera == camera && ShaftTileDebug.g_showRefinementCoverageFlag ? 1 : 0);
                m_temporalFlag = settings.m_temporalFlag.value && !camera.stereoEnabled;
                m_material.SetInt("_ShaftTemporalEnabled", m_temporalFlag ? 1 : 0);
                m_historyWeight = settings.m_historyWeight.value;
                if (m_temporalFlag) unchecked
                    {
                        m_historyHash = settings.GetHashCode();
                        foreach (var value in m_positions) m_historyHash = m_historyHash * 31 + value.GetHashCode();
                        foreach (var value in m_directions) m_historyHash = m_historyHash * 31 + value.GetHashCode();
                        foreach (var value in m_colors) m_historyHash = m_historyHash * 31 + value.GetHashCode();
                        foreach (var value in m_shadowData) m_historyHash = m_historyHash * 31 + value.GetHashCode();
                        foreach (var value in m_spotFluorescence) m_historyHash = m_historyHash * 31 + value.GetHashCode();
                        foreach (var value in m_beamStyle) m_historyHash = m_historyHash * 31 + value.GetHashCode();
                        foreach (var value in m_beamRight) m_historyHash = m_historyHash * 31 + value.GetHashCode();
                        foreach (var value in m_beamUp) m_historyHash = m_historyHash * 31 + value.GetHashCode();
                        foreach (var value in m_beamSettings) m_historyHash = m_historyHash * 31 + value.GetHashCode();
                        foreach (var value in m_fogTransforms) m_historyHash = m_historyHash * 31 + value.GetHashCode();
                        foreach (var value in m_fogSettings) m_historyHash = m_historyHash * 31 + value.GetHashCode();
                        foreach (var value in m_fogColors) m_historyHash = m_historyHash * 31 + value.GetHashCode();
                        foreach (var value in m_fogSunBoost) m_historyHash = m_historyHash * 31 + value.GetHashCode();
                        m_historyHash = m_historyHash * 31 + m_material.GetInt("_ShaftSpotCount");
                        m_historyHash = m_historyHash * 31 + m_material.GetInt("_LocalFogCount");
                        m_historyHash = m_historyHash * 31 + m_material.GetVector("_ShaftSun").GetHashCode();
                        m_historyHash = m_historyHash * 31 + m_material.GetVector("_ShaftSunColor").GetHashCode();
                    }
                if (!m_temporalFlag) m_selection.m_history.Dispose();
            }

            // 視錐台内の局所フォグを候補として選び、選ばれたスロットの情報を配列に詰める。
            void CollectLocalFog(LightShaftVolume settings, ref RenderingData data)
            {
                m_fogCandidates.Clear();
                var camera = data.cameraData.camera;
                var motion = FogSimulationManager.IsAvailable ? FogSimulationManager.Instance : null;
                if (motion) motion.Bind(m_material);
                foreach (var fog in LocalFogVolume.g_active)
                {
                    if (!fog || !fog.isActiveAndEnabled || (fog.m_density <= 0 && fog.m_sunBoost <= 0)) continue;
                    var scale = fog.transform.lossyScale;
                    if (Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z))) < 0.0001f) continue;
                    var bounds = fog.RenderBounds;
                    if (!GeometryUtility.TestPlanesAABB(m_planes, bounds) || bounds.SqrDistance(camera.transform.position) > settings.m_maxDistance.value * settings.m_maxDistance.value) continue;
                    float contribution = fog.m_density + (settings.m_enableSpatialGodRaysFlag.value && !fog.m_subtractFlag ? fog.m_sunBoost * 0.035f : 0);
                    m_fogCandidates.Add(new ShaftSelection<LocalFogVolume>.Candidate(fog, ShaftSelection<LocalFogVolume>.Score(camera, bounds, contribution, fog.m_priority)));
                }
                m_selection.m_fog.Update(m_fogCandidates, m_delta, settings.m_selectionFadeSeconds.value, settings.m_selectionHysteresis.value);
                int localCount = 0;
                m_motionRectCount = 0;
                foreach (var slot in m_selection.m_fog.m_slots)
                {
                    var fog = slot.m_source;
                    if (!fog || slot.m_weight <= 0) continue;
                    var scale = fog.transform.lossyScale;
                    if (Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z))) < 0.0001f) continue;
                    var renderBounds = fog.RenderBounds;
                    m_fogMotion[localCount] = Vector4.zero;
                    m_fogMotionBounds[localCount] = fog.transform.worldToLocalMatrix;
                    if (motion && motion.GetMotion(fog, out int fieldIndex, out float motionWeight, out renderBounds))
                    {
                        m_fogMotion[localCount] = new Vector4(fieldIndex + 1, motionWeight, 0, 0);
                        m_fogMotionBounds[localCount] = Matrix4x4.TRS(renderBounds.center, Quaternion.identity, renderBounds.size).inverse;
                        if (m_motionRectCount < m_motionRects.Length) m_motionRects[m_motionRectCount++] = ProjectBounds(camera, renderBounds);
                    }
                    if (m_tilesFlag)
                    {
                        m_fogRects[localCount] = ProjectBounds(camera, renderBounds);
                    }
                    m_fogTransforms[localCount] = fog.transform.worldToLocalMatrix;
                    m_fogSettings[localCount] = new Vector4((int)fog.m_shape, fog.m_density * slot.m_weight, fog.m_edgeFade, fog.m_subtractFlag ? 1 : 0);
                    m_fogColors[localCount] = fog.m_tint.linear;
                    m_fogSunBoost[localCount] = new Vector4(settings.m_enableSpatialGodRaysFlag.value && !fog.m_subtractFlag ? fog.m_sunBoost * slot.m_weight : 0, 0, 0, 0);
                    localCount++;
                }
                m_material.SetVectorArray("_ShaftFogRects", m_fogRects);
                m_material.SetVectorArray("_LocalFogMotion", m_fogMotion);
                m_material.SetMatrixArray("_LocalFogMotionBounds", m_fogMotionBounds);
                m_material.SetVectorArray("_LocalFogSunBoost", m_fogSunBoost);
                m_material.SetInt("_LocalFogCount", localCount);
                m_material.SetMatrixArray("_LocalFogTransforms", m_fogTransforms);
                m_useFogFlag = settings.m_intensity.value > 0 && (settings.m_density.value > 0 || localCount > 0);
                m_material.SetVectorArray("_LocalFogSettings", m_fogSettings);
                m_material.SetVectorArray("_LocalFogColors", m_fogColors);
            }

            // 太陽（ゴッドレイ）とスポットライトを集め、スポットは上位のみを選んでシェーダーへ渡す。
            void CollectLights(LightShaftVolume settings, ref RenderingData data)
            {
                m_material.SetVector("_ShaftSun", Vector4.zero);
                m_material.SetVector("_ShaftSunColor", Vector4.zero);
                int count = 0, additionalIndex = 0;
                bool hasBeamStyleFlag = false, hasVariationFlag = false;
                m_spotCandidates.Clear(); m_shadowIndices.Clear(); m_lightColors.Clear();
                var blackLight = VolumeManager.instance.stack.GetComponent<BlackLightVolume>();
                var lights = data.lightData.visibleLights;
                for (int i = 0; i < lights.Length; i++)
                {
                    var visible = lights[i];
                    var light = visible.light;
                    bool isMainLightFlag = i == data.lightData.mainLightIndex;
                    int shadowIndex = isMainLightFlag ? -1 : additionalIndex++;
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
                        m_material.SetVector("_ShaftSun", new Vector4(direction.x, direction.y, direction.z, light.shadows != LightShadows.None && light.shadowStrength > 0 ? 1 : 0));
                        m_material.SetVector("_ShaftSunColor", visible.finalColor * settings.m_spatialGodRayIntensity.value);
                        continue;
                    }

                    if (!light || !light.TryGetComponent<LightShaftEmitter>(out var emitter) || !emitter.isActiveAndEnabled || emitter.m_scattering <= 0)
                        continue;
                    Color color = visible.finalColor * emitter.m_scattering;
                    if (isMainLightFlag && light.type == LightType.Directional)
                    {
                        Vector3 direction = -light.transform.forward;
                        m_material.SetVector("_ShaftSun", new Vector4(direction.x, direction.y, direction.z, emitter.m_castShadowsFlag && light.shadows != LightShadows.None && light.shadowStrength > 0 ? 1 : 0));
                        m_material.SetVector("_ShaftSunColor", color);
                    }
                    else if (light.type == LightType.Spot && shadowIndex < data.lightData.additionalLightsCount)
                    {
                        float radius = light.range * Mathf.Tan(light.spotAngle * Mathf.Deg2Rad * 0.5f);
                        Vector3 axis = light.transform.forward;
                        Vector3 end = light.transform.position + axis * light.range;
                        Vector3 disk = new Vector3(Mathf.Sqrt(Mathf.Max(0, 1 - axis.x * axis.x)), Mathf.Sqrt(Mathf.Max(0, 1 - axis.y * axis.y)), Mathf.Sqrt(Mathf.Max(0, 1 - axis.z * axis.z))) * radius;
                        var bounds = new Bounds(light.transform.position, Vector3.zero);
                        bounds.Encapsulate(end - disk); bounds.Encapsulate(end + disk);
                        if (!GeometryUtility.TestPlanesAABB(m_planes, bounds) || bounds.SqrDistance(data.cameraData.camera.transform.position) > settings.m_maxDistance.value * settings.m_maxDistance.value) continue;
                        float brightness = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
                        m_spotCandidates.Add(new ShaftSelection<LightShaftEmitter>.Candidate(emitter, ShaftSelection<LightShaftEmitter>.Score(data.cameraData.camera, bounds, brightness, emitter.m_priority)));
                        m_shadowIndices[emitter] = shadowIndex;
                        m_lightColors[emitter] = color;
                    }
                }
                m_selection.m_spots.Update(m_spotCandidates, m_delta, settings.m_selectionFadeSeconds.value, settings.m_selectionHysteresis.value);
                foreach (var slot in m_selection.m_spots.m_slots)
                {
                    var emitter = slot.m_source;
                    if (!emitter || slot.m_weight <= 0 || !emitter.TryGetComponent<Light>(out var light)) continue;
                    // 前フレームのシャドウアトラスのインデックスは決して再利用しない。
                    bool hasShadowFlag = m_shadowIndices.TryGetValue(emitter, out int shadowIndex);
                    Color color = m_lightColors.TryGetValue(emitter, out var visibleColor) ? visibleColor : light.color.linear * light.intensity * emitter.m_scattering;
                    color *= slot.m_weight;
                    Vector3 position = light.transform.position;
                    Vector3 direction = light.transform.forward;
                    m_positions[count] = new Vector4(position.x, position.y, position.z, light.range);
                    m_directions[count] = new Vector4(direction.x, direction.y, direction.z, Mathf.Cos(light.spotAngle * Mathf.Deg2Rad * 0.5f));
                    m_colors[count] = new Vector4(color.r, color.g, color.b, Mathf.Cos(light.innerSpotAngle * Mathf.Deg2Rad * 0.5f));
                    m_shadowData[count] = new Vector4(hasShadowFlag ? shadowIndex : -1, emitter.m_castShadowsFlag && light.shadows != LightShadows.None && light.shadowStrength > 0 ? 1 : 0, 1f / Mathf.Max(light.range * light.range, 0.0001f), 1f / Mathf.Max(0.001f, m_colors[count].w - m_directions[count].w));
                    // ブラックライト：空気中の埃が紫外線で光る分（等方）を可視の散乱とは別に渡す。
                    m_spotFluorescence[count] = Vector4.zero;
                    if (blackLight != null && blackLight.m_fogFluorescence.value > 0 && emitter.TryGetComponent<BlackLightEmitter>(out var ultraviolet) && ultraviolet.UltravioletIntensity > 0)
                    {
                        Color fluorescence = blackLight.m_fogFluorescenceColor.value.linear * (ultraviolet.UltravioletIntensity * blackLight.m_fogFluorescence.value * blackLight.m_fluorescenceGain.value * emitter.m_scattering * slot.m_weight);
                        m_spotFluorescence[count] = new Vector4(fluorescence.r, fluorescence.g, fluorescence.b, 0);
                    }
                    m_beamStyle[count] = emitter.ComputeBeamParameters(light);
                    m_beamSettings[count] = emitter.PatternSettings;
                    var right = light.transform.right; var up = light.transform.up;
                    m_beamRight[count] = new Vector4(right.x, right.y, right.z, 1f / Mathf.Max(.001f, Mathf.Tan(light.spotAngle * Mathf.Deg2Rad * .5f)));
                    m_beamUp[count] = new Vector4(up.x, up.y, up.z, m_beamSettings[count].x);
                    m_beamAxes[count] = emitter.ComputePatternAxes(Application.isPlaying ? Time.time : 0);
                    hasVariationFlag |= m_beamStyle[count].z > 0;
                    hasBeamStyleFlag |= m_beamStyle[count].x > 0 || m_beamStyle[count].y > 0 || m_beamStyle[count].z > 0;
                    m_detailRegions[count] = RootRegion(data.cameraData.camera, light);
                    bool animatedFlag = m_beamStyle[count].z > 0 && m_beamSettings[count].z > 0;
                    if (m_tilesFlag || animatedFlag)
                    {
                        float radius = light.range * Mathf.Tan(light.spotAngle * Mathf.Deg2Rad * .5f);
                        Vector3 end = position + direction * light.range;
                        Vector3 disk = new Vector3(Mathf.Sqrt(Mathf.Max(0, 1 - direction.x * direction.x)), Mathf.Sqrt(Mathf.Max(0, 1 - direction.y * direction.y)), Mathf.Sqrt(Mathf.Max(0, 1 - direction.z * direction.z))) * radius;
                        var bounds = new Bounds(position, Vector3.zero); bounds.Encapsulate(end - disk); bounds.Encapsulate(end + disk);
                        m_spotRects[count] = ProjectBounds(data.cameraData.camera, bounds);
                        if (animatedFlag && m_motionRectCount < m_motionRects.Length) m_motionRects[m_motionRectCount++] = m_spotRects[count];
                    }
                    count++;
                }
                m_material.SetInt("_ShaftSpotCount", count);
                int fogCount = 0;
                foreach (var slot in m_selection.m_fog.m_slots) if (slot.m_source && slot.m_weight > 0) fogCount++;
                g_selectionUpdated?.Invoke(data.cameraData.camera, m_spotCandidates.Count, count, m_fogCandidates.Count, fogCount);
                if (g_selectedSource != null)
                {
                    foreach (var slot in m_selection.m_spots.m_slots) if (slot.m_source && slot.m_weight > 0) g_selectedSource.Invoke(data.cameraData.camera, slot.m_source, slot.m_weight);
                    foreach (var slot in m_selection.m_fog.m_slots) if (slot.m_source && slot.m_weight > 0) g_selectedSource.Invoke(data.cameraData.camera, slot.m_source, slot.m_weight);
                }
                m_material.SetVectorArray("_ShaftSpotPosition", m_positions);
                m_material.SetVectorArray("_ShaftSpotDirection", m_directions);
                m_material.SetVectorArray("_ShaftSpotColor", m_colors);
                m_material.SetVectorArray("_ShaftSpotShadow", m_shadowData);
                m_material.SetVectorArray("_ShaftSpotFluorescence", m_spotFluorescence);
                if (hasVariationFlag && !m_beamNoise) m_beamNoise = ShaftBeamPattern.CreateTexture();
                if (m_beamNoise) m_material.SetTexture("_ShaftBeamNoise", m_beamNoise);
                m_material.SetInt("_ShaftHasBeamStyle", hasBeamStyleFlag ? 1 : 0);
                m_material.SetVectorArray("_ShaftBeamStyle", m_beamStyle);
                m_material.SetVectorArray("_ShaftBeamRight", m_beamRight);
                m_material.SetVectorArray("_ShaftBeamUp", m_beamUp);
                m_material.SetVectorArray("_ShaftBeamAxes", m_beamAxes);
                m_material.SetVectorArray("_ShaftMotionRects", m_motionRects);
                m_material.SetInt("_ShaftMotionRectCount", m_motionRectCount);
                m_material.SetVectorArray("_ShaftSpotRects", m_spotRects);
                m_material.SetVectorArray("_ShaftDetailRegions", m_detailRegions);
                m_material.SetInt("_ShaftUseFog", m_useFogFlag ? 1 : 0);
                m_material.SetInt("_ShaftUseGodRay", m_useGodRaysFlag ? 1 : 0);
            }

            // バウンディングボックスを画面へ投影し、ビューポート上の矩形 (最小xy, 最大xy) を返す。
            static Vector4 ProjectBounds(Camera camera, Bounds bounds)
            {
                Vector2 lower = Vector2.one, upper = Vector2.zero;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 offset = Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 uv = camera.WorldToViewportPoint(bounds.center + offset);
                    // ニアクリップで切れる、またはカメラを包むボリュームは保守的に全画面扱いにする。
                    if (uv.z <= camera.nearClipPlane) return new Vector4(0, 0, 1, 1);
                    lower = Vector2.Min(lower, uv); upper = Vector2.Max(upper, uv);
                }
                return new Vector4(lower.x, lower.y, upper.x, upper.y);
            }
            static Vector3 Abs(Vector3 vector) => new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));
            // スポットライトの根元付近の画面矩形を求める（高解像度で詳細計算する領域）。該当なしは (2,2,-1,-1)。
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
                Vector2 lower = new Vector2(apex.x, apex.y), upper = lower;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = end + new Vector3((i & 1) == 0 ? -radius : radius, (i & 2) == 0 ? -radius : radius, (i & 4) == 0 ? -radius : radius);
                    Vector3 uv = camera.WorldToViewportPoint(corner);
                    if (uv.z <= camera.nearClipPlane) return new Vector4(2, 2, -1, -1);
                    lower = Vector2.Min(lower, uv); upper = Vector2.Max(upper, uv);
                }
                Vector2 padding = new Vector2(2f * m_divisor / Mathf.Max(1, camera.pixelWidth), 2f * m_divisor / Mathf.Max(1, camera.pixelHeight));
                lower -= padding; upper += padding;
                // 大きな領域は既に画面上で十分なサンプル数があるため、詳細領域として扱わない。
                if ((upper.x - lower.x) * (upper.y - lower.y) > 0.05f) return new Vector4(2, 2, -1, -1);
                return new Vector4(lower.x, lower.y, upper.x, upper.y);
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
                var fullDescriptor = textureDescriptor;
                m_material.SetVector("_ShaftDebugSize", new Vector4(fullDescriptor.width, fullDescriptor.height, 0, 0));
                textureDescriptor.width = Mathf.Max(1, (textureDescriptor.width + m_divisor - 1) / m_divisor);
                textureDescriptor.height = Mathf.Max(1, (textureDescriptor.height + m_divisor - 1) / m_divisor);
                textureDescriptor.msaaSamples = MSAASamples.None;
                m_material.SetVector("_ShaftLowSize", new Vector4(1f / textureDescriptor.width, 1f / textureDescriptor.height, textureDescriptor.width, textureDescriptor.height));
                textureDescriptor.name = "Light Shaft Scattering";
                textureDescriptor.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
                var fogTexture = graph.CreateTexture(textureDescriptor);
                textureDescriptor.name = "Light Shaft Sample Depth";
                textureDescriptor.colorFormat = m_temporalFlag ? UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat : UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
                var fogDepth = graph.CreateTexture(textureDescriptor);
                var cameraData = frameData.Get<UniversalCameraData>();
                for (int i = 0; i < 2; i++)
                {
                    var field = FogSimulationManager.Instance ? FogSimulationManager.Instance.GetField(i) : null;
                    m_motionTextures[i * 2] = field != null ? graph.ImportTexture(field.Current) : default;
                    m_motionTextures[i * 2 + 1] = field != null ? graph.ImportTexture(field.Previous) : default;
                }
                var history = m_selection.m_history;
                if (m_temporalFlag && m_useFogFlag)
                {
                    history.Ensure(textureDescriptor.width, textureDescriptor.height);
                    var camera = cameraData.camera;
                    if (Time.realtimeSinceStartupAsDouble - history.m_time > 0.25) history.Reset("Frame gap");
                    else if (history.m_hash != m_historyHash) history.Reset("Lighting / settings / selection changed");
                    else if (Vector3.Distance(history.m_position, camera.transform.position) > 2 || Quaternion.Angle(history.m_rotation, camera.transform.rotation) > 20 || history.m_projection != camera.projectionMatrix) history.Reset("Camera cut / projection changed");
                    m_material.SetFloat("_ShaftFramePhase", history.m_validFlag ? (history.m_frame * 0.61803398875f) % 1 : 0);
                }
                else m_material.SetFloat("_ShaftFramePhase", 0);
                TextureHandle godRay = default;
                if (m_useGodRaysFlag)
                {
                    var godRayDescriptor = fullDescriptor;
                    godRayDescriptor.width = Mathf.Max(1, (godRayDescriptor.width + 3) / 4);
                    godRayDescriptor.height = Mathf.Max(1, (godRayDescriptor.height + 3) / 4);
                    m_material.SetVector("_GodRayTexelSize", new Vector4(1f / godRayDescriptor.width, 1f / godRayDescriptor.height, godRayDescriptor.width, godRayDescriptor.height));
                    godRayDescriptor.msaaSamples = MSAASamples.None;
                    godRayDescriptor.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat;
                    godRayDescriptor.name = "God Ray Occlusion";
                    var mask = graph.CreateTexture(godRayDescriptor);
                    godRayDescriptor.name = "God Ray Radial";
                    var radial = graph.CreateTexture(godRayDescriptor);
                    godRayDescriptor.name = "God Ray Filtered";
                    godRay = graph.CreateTexture(godRayDescriptor);
                    using (var builder = graph.AddRasterRenderPass<PassData>("God Ray Sky Occlusion", out var data, ShaftProfiling.Get(ShaftProfiling.PASS.SKY_MASK)))
                    {
                        data.m_source = source;
                        data.m_material = m_material;
                        builder.UseTexture(source);
                        builder.UseTexture(resources.cameraDepthTexture);
                        builder.UseAllGlobalTextures(true);
                        builder.SetRenderAttachment(mask, 0);
                        builder.SetRenderFunc((PassData passData, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, passData.m_source, new Vector4(1, 1, 0, 0), passData.m_material, 2));
                    }

                    using (var builder = graph.AddRasterRenderPass<PassData>("God Ray Radial Scattering", out var data, ShaftProfiling.Get(ShaftProfiling.PASS.RADIAL)))
                    {
                        data.m_source = mask;
                        data.m_material = m_material;
                        builder.UseTexture(mask);
                        builder.SetRenderAttachment(radial, 0);
                        builder.SetRenderFunc((PassData passData, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, passData.m_source, new Vector4(1, 1, 0, 0), passData.m_material, 3));
                    }

                    using (var builder = graph.AddRasterRenderPass<PassData>("God Ray Low Resolution Filter", out var data, ShaftProfiling.Get(ShaftProfiling.PASS.RADIAL_FILTER)))
                    {
                        data.m_source = radial;
                        data.m_material = m_material;
                        builder.UseTexture(radial);
                        builder.SetRenderAttachment(godRay, 0);
                        builder.SetGlobalTextureAfterPass(godRay, g_godRayId);
                        builder.SetRenderFunc((PassData passData, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, passData.m_source, new Vector4(1, 1, 0, 0), passData.m_material, 4));
                    }
                }

                TextureHandle tileMasks = default;
                if (m_tilesFlag && m_useFogFlag)
                {
                    var tileDescriptor = textureDescriptor;
                    tileDescriptor.width = (textureDescriptor.width + 7) / 8; tileDescriptor.height = (textureDescriptor.height + 7) / 8;
                    tileDescriptor.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32_UInt;
                    tileDescriptor.name = "Shaft Tile Masks";
                    tileMasks = graph.CreateTexture(tileDescriptor);
                    using (var builder = graph.AddRasterRenderPass<PassData>("Light Shaft Tile Candidates", out var data, ShaftProfiling.Get(ShaftProfiling.PASS.TILES)))
                    {
                        data.m_source = source; data.m_material = m_material; builder.UseTexture(source);
                        builder.SetRenderAttachment(tileMasks, 0);
                        builder.SetGlobalTextureAfterPass(tileMasks, Shader.PropertyToID("_ShaftTileMasks"));
                        builder.SetRenderFunc((PassData passData, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, passData.m_source, new Vector4(1, 1, 0, 0), passData.m_material, 6));
                    }
                }
                if (m_useFogFlag)
                    using (var builder = graph.AddRasterRenderPass<PassData>("Volumetric Light Shaft", out var data, ShaftProfiling.Get(ShaftProfiling.PASS.ATMOSPHERE)))
                    {
                        foreach (var motionTexture in m_motionTextures) if (motionTexture.IsValid()) builder.UseTexture(motionTexture);
                        data.m_source = source;
                        data.m_material = m_material;
                        builder.UseTexture(source);
                        if (tileMasks.IsValid()) builder.UseTexture(tileMasks);
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

                if (m_temporalFlag && m_useFogFlag)
                {
                    int previous = history.m_index, next = 1 - previous;
                    var previousColor = graph.ImportTexture(history.m_color[previous]);
                    var previousDepth = graph.ImportTexture(history.m_depth[previous]);
                    var temporalColor = graph.ImportTexture(history.m_color[next]);
                    var temporalDepth = graph.ImportTexture(history.m_depth[next]);
                    m_material.SetTexture("_ShaftHistoryColor", history.m_color[previous].rt);
                    m_material.SetTexture("_ShaftHistoryDepth", history.m_depth[previous].rt);
                    m_material.SetMatrix("_ShaftPreviousVP", history.m_viewProjection);
                    m_material.SetMatrix("_ShaftPreviousView", history.m_view);
                    m_material.SetFloat("_ShaftHistoryWeight", history.m_validFlag ? m_historyWeight : 0);
                    using (var builder = graph.AddRasterRenderPass<PassData>("Light Shaft Temporal", out var data, ShaftProfiling.Get(ShaftProfiling.PASS.TEMPORAL)))
                    {
                        data.m_source = fogTexture; data.m_material = m_material;
                        builder.UseTexture(fogTexture); builder.UseTexture(fogDepth);
                        builder.UseTexture(previousColor); builder.UseTexture(previousDepth);
                        builder.UseTexture(resources.cameraDepthTexture); builder.UseAllGlobalTextures(true);
                        builder.SetRenderAttachment(temporalColor, 0); builder.SetRenderAttachment(temporalDepth, 1);
                        builder.SetGlobalTextureAfterPass(temporalColor, g_fogId);
                        builder.SetRenderFunc((PassData passData, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, passData.m_source, new Vector4(1, 1, 0, 0), passData.m_material, 5));
                    }
                    g_historyUpdated?.Invoke(cameraData.camera, history.m_validFlag ? "Accumulating" : history.m_reason, history.m_width, history.m_height);
                    history.m_index = next; history.m_validFlag = true; history.m_frame++;
                    history.m_hash = m_historyHash; history.m_time = Time.realtimeSinceStartupAsDouble;
                    history.m_position = cameraData.camera.transform.position; history.m_rotation = cameraData.camera.transform.rotation;
                    history.m_projection = cameraData.camera.projectionMatrix; history.m_view = cameraData.GetViewMatrix();
                    history.m_viewProjection = cameraData.GetProjectionMatrix() * history.m_view;
                    fogTexture = temporalColor;
                }

                using (var builder = graph.AddRasterRenderPass<PassData>("Light Shaft Depth Aware Composite", out var data, ShaftProfiling.Get(ShaftProfiling.PASS.COMPOSITE)))
                {
                    foreach (var motionTexture in m_motionTextures) if (motionTexture.IsValid()) builder.UseTexture(motionTexture);
                    data.m_source = source;
                    data.m_material = m_material;
                    builder.UseTexture(source);
                    if (m_useFogFlag)
                    {
                        builder.UseTexture(fogTexture);
                        builder.UseTexture(fogDepth);
                    }

                    if (tileMasks.IsValid()) builder.UseTexture(tileMasks);
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
                foreach (var state in m_cameras.Values) if (!camera || state.m_camera == camera) state.m_history.Reset("Explicit reset");
            }
            public void Dispose()
            {
                CoreUtils.Destroy(m_beamNoise); m_beamNoise = null;
                foreach (var state in m_cameras.Values) state.m_history.Dispose();
                m_cameras.Clear();
            } // 一時テクスチャはRender Graphが解放する。
        }
    }
}
