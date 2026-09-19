namespace LightShaftLab
{
    public enum ShaftQualityPreset { Performance, Balanced, NearDetail }
    public static class ShaftQualityPresets
    {
        // Shared by runtime controls and editor. Preserve artistic atmosphere settings.
        public static void Apply(LightShaftVolume volume, ShaftQualityPreset preset)
        {
            bool fast = preset == ShaftQualityPreset.Performance, detail = preset == ShaftQualityPreset.NearDetail;
            volume.m_resolutionDivisor.Override(fast ? 4 : detail ? 1 : 2);
            volume.m_steps.Override(fast ? 32 : detail ? 64 : 48);
            volume.m_spotMinimumSamples.Override(fast ? 4 : detail ? 12 : 8);
            volume.m_localFogMinimumSamples.Override(fast ? 2 : detail ? 4 : 3);
            volume.m_godRaySamples.Override(fast ? 16 : detail ? 48 : 32);
            volume.m_refineRootsFlag.Override(true);
            volume.m_refineDepthEdgesFlag.Override(true);
            // Optional techniques need scene-specific comparisons; presets remain predictable.
            volume.m_temporalFlag.Override(false);
            volume.m_tileCullingFlag.Override(false);
        }
    }
}
