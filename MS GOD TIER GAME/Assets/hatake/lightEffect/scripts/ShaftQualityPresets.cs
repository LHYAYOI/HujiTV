namespace LightShaftLab
{
    public enum SHAFT_QUALITY_PRESET { PERFORMANCE, BALANCED, NEAR_DETAIL }
    public static class ShaftQualityPresets
    {
        // ランタイムの操作 UI とエディタで共用する。見た目（大気の芸術的な設定）は変更しない。
        public static void Apply(LightShaftVolume volume, SHAFT_QUALITY_PRESET preset)
        {
            bool fastFlag = preset == SHAFT_QUALITY_PRESET.PERFORMANCE, detailFlag = preset == SHAFT_QUALITY_PRESET.NEAR_DETAIL;
            volume.m_resolutionDivisor.Override(fastFlag ? 4 : detailFlag ? 1 : 2);
            volume.m_steps.Override(fastFlag ? 32 : detailFlag ? 64 : 48);
            volume.m_spotMinimumSamples.Override(fastFlag ? 4 : detailFlag ? 12 : 8);
            volume.m_localFogMinimumSamples.Override(fastFlag ? 2 : detailFlag ? 4 : 3);
            volume.m_godRaySamples.Override(fastFlag ? 16 : detailFlag ? 48 : 32);
            volume.m_refineRootsFlag.Override(true);
            volume.m_refineDepthEdgesFlag.Override(true);
            // オプションの手法はシーンごとの比較検証が必要なため、プリセットでは無効にして結果を予測しやすくする。
            volume.m_temporalFlag.Override(false);
            volume.m_tileCullingFlag.Override(false);
        }
    }
}
