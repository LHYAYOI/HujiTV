using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace LightShaftLab
{
    [AddComponentMenu(""), ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(1000)]
    public sealed class FogSimulationManager : MonoBehaviour
    {
        // 一時的に停止中。古いシーンの参照が切れないよう、シリアライズされる型は残している。
        public static bool IsAvailable => false;
        public Camera m_referenceCamera;
        [Range(1, 2)] public int m_maxDomains = 1;
        [Range(1, 4)] public int m_maxSources = 4;
        [Range(1, 16)] public int m_maxInteractors = 16;
        [Range(30, 60)] public int m_updateHz = 30;
        [Min(0)] public float m_fadeSeconds = .35f;
        [Min(0)] public float m_minimumHold = 1;
        [UnityEngine.Serialization.FormerlySerializedAs("m_debugBounds")]
        public bool m_drawDebugBoundsFlag;
        public static FogSimulationManager Instance { get; private set; }
        public string Status { get; private set; } = "固定Fog（動的Fogは停止中）";
        public int ActiveDomains { get; private set; }
        public int ActiveSources { get; private set; }
        public long AllocatedBytes { get; private set; }
        public uint UpdateCount { get; private set; }
        public int LastInteractorCount { get; private set; }
        public float Interpolation => Mathf.Clamp01(m_accumulator * Mathf.Clamp(m_updateHz, 30, 60));
        static readonly ProfilerMarker g_cpu = new ProfilerMarker("FogSimulationManager.Control");
        readonly List<ShaftSelection<FogSimulationDomain>.Candidate> m_domainCandidates = new List<ShaftSelection<FogSimulationDomain>.Candidate>();
        readonly List<ShaftSelection<LocalFogVolume>.Candidate> m_sourceCandidates = new List<ShaftSelection<LocalFogVolume>.Candidate>();
        readonly List<FogInteractor> m_interactors = new List<FogInteractor>();
        readonly Plane[] m_planes = new Plane[6];
        readonly FogField[] m_fields = new FogField[2];
        readonly float[] m_age = new float[2];
        readonly Matrix4x4[] m_worldToUV = new Matrix4x4[2];
        readonly Vector4[] m_blend = new Vector4[2];
        readonly Vector4[] m_pointA = new Vector4[16], m_pointB = new Vector4[16], m_sweep = new Vector4[16], m_velocity = new Vector4[16], m_angular = new Vector4[16];
        readonly Matrix4x4[] m_box = new Matrix4x4[16];
        ShaftSelection<FogSimulationDomain> m_domains;
        ShaftSelection<LocalFogVolume> m_sources;
        ComputeShader m_shader;
        CommandBuffer m_commands;
        float m_accumulator, m_captureElapsed;
        bool m_allocationFailedFlag;
        void OnEnable()
        {
            if (!IsAvailable) { DisposeFields(); return; }
            if (Instance && Instance != this) { Debug.LogWarning("FogSimulationManagerはシーン全体に1個だけ配置してください。", this); enabled = false; return; }
            Instance = this;
            m_allocationFailedFlag = false;
        }
        void OnDisable()
        {
            DisposeFields(); if (Instance == this) Instance = null;
            m_commands?.Release(); m_commands = null; CoreUtils.Destroy(m_shader); m_shader = null;
            m_sources = null; m_domains = null; m_accumulator = m_captureElapsed = 0;
        }
        void LateUpdate()
        {
            if (IsAvailable && Application.isPlaying) Simulate(Time.deltaTime, m_referenceCamera ? m_referenceCamera : Camera.main);
        }
        // 単体検証用ハーネスからも呼ばれる。描画処理からカメラごとに呼ばれることはない。
        // 領域・Fog の選別、インタラクターの取り込み、Compute による固定周期のステップ更新を行う。
        public void Simulate(float deltaTime, Camera camera)
        {
            if (!IsAvailable || !isActiveAndEnabled || Instance != this || m_allocationFailedFlag) return;
            using (g_cpu.Auto())
            {
                float delta = Mathf.Clamp(deltaTime, 0, .1f);
                int domainCapacity = Mathf.Clamp(m_maxDomains, 1, 2), sourceCapacity = Mathf.Clamp(m_maxSources, 1, 4);
                if (m_domains == null || m_domains.m_slots.Length != domainCapacity || m_sources == null || m_sources.m_slots.Length != sourceCapacity)
                {
                    DisposeFields(); m_domains = new ShaftSelection<FogSimulationDomain>(domainCapacity);
                    m_sources = new ShaftSelection<LocalFogVolume>(sourceCapacity);
                }
                if (camera) GeometryUtility.CalculateFrustumPlanes(camera, m_planes);
                m_sourceCandidates.Clear(); m_domainCandidates.Clear();
                foreach (var fog in LocalFogVolume.g_active)
                {
                    if (!fog || !fog.isActiveAndEnabled || !fog.m_reactiveFlag || fog.m_subtractFlag || fog.m_density <= 0 || fog.m_motionStrength <= 0 || !fog.m_motionDomain || !fog.m_motionDomain.isActiveAndEnabled) continue;
                    var bounds = fog.RenderBounds;
                    if (camera && !GeometryUtility.TestPlanesAABB(m_planes, bounds) && !fog.m_motionDomain.m_pinFlag) continue;
                    float score = camera ? ShaftSelection<LocalFogVolume>.Score(camera, bounds, fog.m_density, fog.m_priority) : fog.m_density * fog.m_priority;
                    m_sourceCandidates.Add(new ShaftSelection<LocalFogVolume>.Candidate(fog, score));
                }
                foreach (var domain in FogSimulationDomain.g_active)
                {
                    if (!domain || !domain.isActiveAndEnabled || domain.m_priority <= 0) continue;
                    float score = 0;
                    foreach (var source in m_sourceCandidates) if (source.m_source.m_motionDomain == domain) score = Mathf.Max(score, source.m_score);
                    if (score <= 0) continue;
                    int contacts = 0;
                    foreach (var interactor in FogInteractor.g_active) if (interactor && interactor.IsAvailable && domain.Bounds.Intersects(interactor.Bounds)) contacts++;
                    score *= domain.m_priority * (1 + Mathf.Min(contacts, 4) * .25f);
                    if (domain.m_pinFlag) score *= 1000;
                    for (int i = 0; i < m_fields.Length; i++) if (m_fields[i] != null && m_fields[i].Domain == domain && m_age[i] < m_minimumHold) score *= 1000;
                    m_domainCandidates.Add(new ShaftSelection<FogSimulationDomain>.Candidate(domain, score));
                }
                m_domains.Update(m_domainCandidates, delta, m_fadeSeconds, .2f);
                // 採用されなくなった Fog ソースも、古いフィールドを使ってフェードアウトできる。
                for (int i = m_sourceCandidates.Count - 1; i >= 0; i--)
                    if (FindDomainSlot(m_sourceCandidates[i].m_source.m_motionDomain) < 0) m_sourceCandidates.RemoveAt(i);
                m_sources.Update(m_sourceCandidates, delta, m_fadeSeconds, .2f);
                bool wantedFlag = false;
                foreach (var slot in m_sources.m_slots) if (slot.m_source && slot.m_weight > 0 && FindDomainSlot(slot.m_source.m_motionDomain) >= 0) { wantedFlag = true; break; }
                if (!wantedFlag)
                {
                    DisposeFields(); m_accumulator = m_captureElapsed = 0; Status = "固定Fog（動的Fogは停止中）"; return;
                }
                if (!SystemInfo.supportsComputeShaders || !SystemInfo.supports3DTextures || !SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.LoadStore))
                { DisposeFields(); Status = "3D Compute非対応：固定Fogで表示"; return; }
                if (!m_shader)
                {
                    var asset = Resources.Load<ComputeShader>("LightShaftFogReactive");
                    if (!asset) { Status = "Compute Shaderが見つかりません：固定Fogで表示"; return; }
                    m_shader = Instantiate(asset); m_commands = new CommandBuffer { name = "Reactive Fog" };
                }
                m_accumulator += delta; m_captureElapsed += delta;
                float period = 1f / Mathf.Clamp(m_updateHz, 30, 60);
                bool stepFlag = m_accumulator >= period;
                if (stepFlag)
                {
                    foreach (var interactor in FogInteractor.g_active) if (interactor && interactor.isActiveAndEnabled) interactor.Capture(m_captureElapsed);
                    m_captureElapsed = 0; m_accumulator %= period;
                }
                m_commands.Clear();
                bool recordedWorkFlag = false;
                using (new ProfilingScope(m_commands, ShaftProfiling.Get(ShaftProfiling.PASS.FOG_REACTIVE)))
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var domain = i < m_domains.m_slots.Length ? m_domains.m_slots[i].m_source : null;
                        bool hasSourceFlag = false;
                        foreach (var source in m_sources.m_slots) if (source.m_source && source.m_weight > 0 && source.m_source.m_motionDomain == domain) { hasSourceFlag = true; break; }
                        if (m_fields[i] != null && (!domain || !hasSourceFlag || m_fields[i].Domain != domain || m_fields[i].Bounds != domain.Bounds || m_fields[i].Resolution != domain.Resolution || m_fields[i].MaxDisplacement != Mathf.Max(0, domain.m_maxDisplacement)))
                        { m_fields[i].Dispose(); m_fields[i] = null; m_age[i] = 0; }
                        if (!domain || !hasSourceFlag) continue;
                        if (m_fields[i] == null)
                        {
                            try { m_fields[i] = new FogField(domain, m_shader, m_commands); recordedWorkFlag = true; }
                            catch (System.Exception error)
                            {
                                DisposeFields(); m_allocationFailedFlag = true; Status = "3Dバッファ確保失敗：固定Fogで表示";
                                Debug.LogWarning(Status + " / " + error.Message, this); return;
                            }
                        }
                        m_age[i] += delta;
                        if (stepFlag) { UploadInteractors(domain); m_fields[i].Step(m_shader, m_commands, period, m_interactors.Count); recordedWorkFlag = true; }
                    }
                }
                // 描画と同じグラフィックスキューで実行する。Dispatch の記録後に、完成したバッファを公開する。
                if (recordedWorkFlag) Graphics.ExecuteCommandBuffer(m_commands);
                if (stepFlag) UpdateCount++;
                ActiveDomains = ActiveSources = 0; AllocatedBytes = 0;
                foreach (var field in m_fields) if (field != null) { ActiveDomains++; AllocatedBytes += field.Bytes; }
                foreach (var source in m_sources.m_slots) if (source.m_source && GetMotion(source.m_source, out _, out _, out _)) ActiveSources++;
                if (stepFlag) Status = $"Reactive：領域 {ActiveDomains}/{domainCapacity}・Fog {ActiveSources}/{sourceCapacity}・{m_updateHz}Hz";
            }
        }
        // 領域に接触するインタラクターを優先度順に上限数まで集め、Compute Shader の配列パラメータへ書き込む。
        void UploadInteractors(FogSimulationDomain domain)
        {
            m_interactors.Clear();
            foreach (var interactor in FogInteractor.g_active)
                if (interactor && interactor.IsAvailable && domain.Bounds.Intersects(interactor.Bounds)) m_interactors.Add(interactor);
            m_interactors.Sort(CompareInteractors);
            if (m_interactors.Count > Mathf.Clamp(m_maxInteractors, 1, 16)) m_interactors.RemoveRange(Mathf.Clamp(m_maxInteractors, 1, 16), m_interactors.Count - Mathf.Clamp(m_maxInteractors, 1, 16));
            LastInteractorCount = m_interactors.Count;
            for (int i = 0; i < m_interactors.Count; i++)
            {
                var item = m_interactors[i]; var pointA = item.A; var pointB = item.B; var velocity = item.Velocity; var angularVelocity = item.AngularVelocity;
                m_pointA[i] = new Vector4(pointA.x, pointA.y, pointA.z, item.Radius); m_pointB[i] = new Vector4(pointB.x, pointB.y, pointB.z, item.m_influence);
                m_sweep[i] = new Vector4(item.SweepStart.x, item.SweepStart.y, item.SweepStart.z, Mathf.Max(.01f, item.BoxMinimumSide));
                m_velocity[i] = new Vector4(velocity.x, velocity.y, velocity.z, item.m_strength); m_angular[i] = new Vector4(angularVelocity.x, angularVelocity.y, angularVelocity.z, item.IsBox ? 1 : 0); m_box[i] = item.BoxTransform;
            }
            m_commands.SetComputeVectorArrayParam(m_shader, "_InteractorA", m_pointA); m_commands.SetComputeVectorArrayParam(m_shader, "_InteractorB", m_pointB);
            m_commands.SetComputeVectorArrayParam(m_shader, "_InteractorSweep", m_sweep); m_commands.SetComputeVectorArrayParam(m_shader, "_InteractorVelocity", m_velocity);
            m_commands.SetComputeVectorArrayParam(m_shader, "_InteractorAngular", m_angular); m_commands.SetComputeMatrixArrayParam(m_shader, "_InteractorBox", m_box);
        }
        // 強さ×速度が大きいものを優先し、同点ならインスタンスIDで順序を安定させる。
        static int CompareInteractors(FogInteractor left, FogInteractor right)
        {
            int priority = (right.m_strength * (1 + right.Velocity.sqrMagnitude)).CompareTo(left.m_strength * (1 + left.Velocity.sqrMagnitude));
            return priority != 0 ? priority : left.GetInstanceID().CompareTo(right.GetInstanceID());
        }
        // 領域が割り当てられている有効なスロット番号を返す。無ければ -1。
        int FindDomainSlot(FogSimulationDomain domain)
        {
            if (!domain || m_domains == null) return -1;
            for (int i = 0; i < m_domains.m_slots.Length; i++) if (m_domains.m_slots[i].m_source == domain && m_domains.m_slots[i].m_weight > 0) return i;
            return -1;
        }
        public bool GetMotion(LocalFogVolume fog, out int fieldIndex, out float weight, out Bounds bounds)
        {
            fieldIndex = -1; weight = 0; bounds = fog ? fog.RenderBounds : default;
            if (!IsAvailable || !fog) return false;
            if (fog.m_subtractFlag || !fog.m_motionDomain || m_sources == null) return false;
            int index = FindDomainSlot(fog.m_motionDomain);
            if (index < 0 || m_fields[index] == null || m_fields[index].Domain != fog.m_motionDomain) return false;
            foreach (var source in m_sources.m_slots)
                if (source.m_source == fog) { weight = source.m_weight * m_domains.m_slots[index].m_weight * Mathf.Clamp01(fog.m_motionStrength); break; }
            if (weight <= 0) return false;
            fieldIndex = index; return true;
        }
        public FogField GetField(int index) => IsAvailable && index >= 0 && index < m_fields.Length ? m_fields[index] : null;
        public bool HasMotionState(LocalFogVolume fog)
        {
            if (!IsAvailable || !fog) return false;
            int index = FindDomainSlot(fog.m_motionDomain);
            if (index < 0 || m_sources == null || m_fields[index] == null) return false;
            foreach (var source in m_sources.m_slots) if (source.m_source == fog && source.m_weight > 0) return true;
            return false;
        }
        public void Bind(Material material)
        {
            for (int i = 0; i < 2; i++)
            {
                var field = m_fields[i]; m_worldToUV[i] = field != null ? field.WorldToUVW : Matrix4x4.identity;
                m_blend[i] = new Vector4(Interpolation, 0, 0, 0);
                material.SetTexture(i == 0 ? "_FogMotionCurrent0" : "_FogMotionCurrent1", field?.Current.rt);
                material.SetTexture(i == 0 ? "_FogMotionPrevious0" : "_FogMotionPrevious1", field?.Previous.rt);
            }
            material.SetMatrixArray("_FogFieldWorldToUV", m_worldToUV); material.SetVectorArray("_FogFieldBlend", m_blend);
        }
        void DisposeFields()
        {
            for (int i = 0; i < m_fields.Length; i++) { m_fields[i]?.Dispose(); m_fields[i] = null; m_age[i] = 0; }
            ActiveDomains = ActiveSources = LastInteractorCount = 0; AllocatedBytes = 0;
        }
        void OnDrawGizmos()
        {
            if (!m_drawDebugBoundsFlag) return;
            for (int i = 0; i < m_fields.Length; i++)
                if (m_fields[i] != null) { Gizmos.color = Color.Lerp(Color.gray, Color.cyan, m_domains.m_slots[i].m_weight); Gizmos.DrawWireCube(m_fields[i].Bounds.center, m_fields[i].Bounds.size); }
        }
    }
}
