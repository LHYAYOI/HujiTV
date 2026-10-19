using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace LightShaftLab
{
    // 符号付きのワールド空間速度（m/s）と変位（m）を保持する 3D フィールド。圧力の解法は行わない。
    public sealed class FogField : System.IDisposable
    {
        public FogSimulationDomain Domain { get; private set; }
        public Bounds Bounds { get; private set; }
        public int Resolution { get; private set; }
        public float MaxDisplacement { get; private set; }
        readonly RTHandle[] m_velocity = new RTHandle[2], m_displacement = new RTHandle[3];
        int m_velocityIndex, m_currentIndex = 1, m_previousIndex, m_nextIndex = 2;
        public RTHandle Current => m_displacement[m_currentIndex];
        public RTHandle Previous => m_displacement[m_previousIndex];
        public RTHandle Velocity => m_velocity[m_velocityIndex];
        public Matrix4x4 WorldToUVW => Matrix4x4.Scale(new Vector3(1 / Bounds.size.x, 1 / Bounds.size.y, 1 / Bounds.size.z)) * Matrix4x4.Translate(-Bounds.min);
        public long Bytes => (long)Resolution * Resolution * Resolution * 8 * 5;
        public uint Ticks { get; private set; }
        public FogField(FogSimulationDomain domain, ComputeShader shader, CommandBuffer commandBuffer)
        {
            Domain = domain; Bounds = domain.Bounds; Resolution = domain.Resolution; MaxDisplacement = Mathf.Max(0, domain.m_maxDisplacement);
            try
            {
                for (int i = 0; i < 2; i++) m_velocity[i] = Allocate($"Fog velocity {i}");
                for (int i = 0; i < 3; i++) m_displacement[i] = Allocate($"Fog displacement {i}");
            }
            catch { Dispose(); throw; }
            int kernel = shader.FindKernel("Clear"); commandBuffer.SetComputeIntParam(shader, "_Resolution", Resolution);
            foreach (var handle in m_velocity) Clear(handle);
            foreach (var handle in m_displacement) Clear(handle);
            void Clear(RTHandle handle) { commandBuffer.SetComputeTextureParam(shader, kernel, "_ClearTarget", handle); commandBuffer.DispatchCompute(shader, kernel, Resolution / 4, Resolution / 4, Resolution / 4); }
        }
        // ランダム書き込み可能な 3D RenderTexture を確保し、RTHandle に所有権ごと渡す。
        RTHandle Allocate(string name)
        {
            var renderTexture = new RenderTexture(Resolution, Resolution, 0)
            {
                name = name, dimension = TextureDimension.Tex3D, volumeDepth = Resolution,
                graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat, enableRandomWrite = true,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, useMipMap = false
            };
            if (!renderTexture.Create()) { CoreUtils.Destroy(renderTexture); throw new System.InvalidOperationException("3D Fog RenderTexture allocation failed"); }
            return RTHandles.Alloc(renderTexture, transferOwnership: true);
        }
        // 1 ステップ分のシミュレーションを Dispatch し、速度（ダブルバッファ）と変位（トリプルバッファ）を入れ替える。
        public void Step(ComputeShader shader, CommandBuffer commandBuffer, float deltaTime, int interactorCount)
        {
            int kernel = shader.FindKernel("Step");
            commandBuffer.SetComputeIntParam(shader, "_Resolution", Resolution); commandBuffer.SetComputeIntParam(shader, "_InteractorCount", interactorCount);
            commandBuffer.SetComputeVectorParam(shader, "_DomainMin", Bounds.min); commandBuffer.SetComputeVectorParam(shader, "_DomainSize", Bounds.size);
            commandBuffer.SetComputeVectorParam(shader, "_DomainWind", Domain.m_wind);
            commandBuffer.SetComputeVectorParam(shader, "_Parameters", new Vector4(deltaTime, Mathf.Max(0, Domain.m_maxDisplacement), Mathf.Max(.01f, Domain.m_restoration), Mathf.Max(.01f, Domain.m_velocityDamping)));
            commandBuffer.SetComputeTextureParam(shader, kernel, "_VelocityRead", m_velocity[m_velocityIndex]);
            commandBuffer.SetComputeTextureParam(shader, kernel, "_DisplacementRead", Current);
            commandBuffer.SetComputeTextureParam(shader, kernel, "_VelocityWrite", m_velocity[1 - m_velocityIndex]);
            commandBuffer.SetComputeTextureParam(shader, kernel, "_DisplacementWrite", m_displacement[m_nextIndex]);
            commandBuffer.DispatchCompute(shader, kernel, Resolution / 4, Resolution / 4, Resolution / 4);
            m_velocityIndex = 1 - m_velocityIndex;
            int oldPreviousIndex = m_previousIndex; m_previousIndex = m_currentIndex; m_currentIndex = m_nextIndex; m_nextIndex = oldPreviousIndex; Ticks++;
        }
        public void Dispose()
        {
            // RTHandles.Alloc(RenderTexture) で確保したハンドルは元のテクスチャも所有しているため、Release だけで解放される。
            foreach (var handle in m_velocity) handle?.Release();
            foreach (var handle in m_displacement) handle?.Release();
        }
    }
}
