using UnityEngine.Rendering;

namespace LightShaftLab
{
    // Recorders publish completed frame data, not timings for the current Draw call.
    // A sample count of zero means unavailable/not executed, never a zero-ms result.
    public static class ShaftProfiling
    {
        public enum Pass { Atmosphere, Composite, SkyMask, Radial, RadialFilter, Temporal, Tiles }
        static readonly string[] g_names = { "Shaft.Atmosphere (fog + lights)", "Shaft.Composite", "Shaft.SkyMask", "Shaft.Radial", "Shaft.RadialFilter", "Shaft.Temporal", "Shaft.Tiles" };
        static readonly ProfilingSampler[] g_samplers = Create();
        public static bool Enabled { get; private set; }
        static ProfilingSampler[] Create()
        {
            var result = new ProfilingSampler[g_names.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new ProfilingSampler(g_names[i]);
            return result;
        }
        public static ProfilingSampler Get(Pass pass) => g_samplers[(int)pass];
        public static void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            foreach (var sampler in g_samplers) sampler.enableRecording = enabled;
        }
        public static float GpuMilliseconds(Pass pass)
        {
            var sampler = Get(pass);
            return Enabled && sampler.gpuSampleCount > 0 ? sampler.gpuElapsedTime / sampler.gpuSampleCount : float.NaN;
        }
        public static float CpuMilliseconds(Pass pass)
        {
            var sampler = Get(pass);
            return Enabled && sampler.cpuSampleCount > 0 ? sampler.cpuElapsedTime / sampler.cpuSampleCount : float.NaN;
        }
    }
}
