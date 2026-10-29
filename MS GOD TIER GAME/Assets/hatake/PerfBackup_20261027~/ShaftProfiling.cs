using UnityEngine.Rendering;

namespace LightShaftLab
{
    // レコーダーが公開するのは完了したフレームのデータで、現在の Draw 呼び出しの計測値ではない。
    // サンプル数0は「取得不可／未実行」を意味し、0ms という結果ではない。
    public static class ShaftProfiling
    {
        public enum PASS { ATMOSPHERE, COMPOSITE, SKY_MASK, RADIAL, RADIAL_FILTER, TEMPORAL, TILES, FOG_REACTIVE }
        static readonly string[] g_names = { "Shaft.Atmosphere (fog + lights)", "Shaft.Composite", "Shaft.SkyMask", "Shaft.Radial", "Shaft.RadialFilter", "Shaft.Temporal", "Shaft.Tiles", "Shaft.FogReactive" };
        static readonly ProfilingSampler[] g_samplers = Create();
        public static bool IsEnabled { get; private set; }
        static ProfilingSampler[] Create()
        {
            var result = new ProfilingSampler[g_names.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new ProfilingSampler(g_names[i]);
            return result;
        }
        public static ProfilingSampler Get(PASS pass) => g_samplers[(int)pass];
        public static void SetEnabled(bool enableFlag)
        {
            IsEnabled = enableFlag;
            foreach (var sampler in g_samplers) sampler.enableRecording = enableFlag;
        }
        public static float GpuMilliseconds(PASS pass)
        {
            var sampler = Get(pass);
            return IsEnabled && sampler.gpuSampleCount > 0 ? sampler.gpuElapsedTime / sampler.gpuSampleCount : float.NaN;
        }
        public static float CpuMilliseconds(PASS pass)
        {
            var sampler = Get(pass);
            return IsEnabled && sampler.cpuSampleCount > 0 ? sampler.cpuElapsedTime / sampler.cpuSampleCount : float.NaN;
        }
    }
}
