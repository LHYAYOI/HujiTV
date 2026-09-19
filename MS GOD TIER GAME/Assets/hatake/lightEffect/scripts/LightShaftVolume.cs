using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LightShaftLab
{
    [Serializable, VolumeComponentMenu("Light Shaft/Volumetric Atmosphere")]
    public sealed class LightShaftVolume : VolumeComponent, IPostProcessComponent
    {
        [UnityEngine.Serialization.FormerlySerializedAs("enable")]
        public BoolParameter m_enableFlag = new BoolParameter(false);
        [UnityEngine.Serialization.FormerlySerializedAs("density")]
        public ClampedFloatParameter m_density = new ClampedFloatParameter(0.035f, 0, 0.3f);
        [UnityEngine.Serialization.FormerlySerializedAs("scatteringTint")]
        public ColorParameter m_scatteringTint = new ColorParameter(new Color(0.8f, 0.87f, 1), false, false, true);
        [UnityEngine.Serialization.FormerlySerializedAs("intensity")]
        public ClampedFloatParameter m_intensity = new ClampedFloatParameter(1, 0, 10);
        [UnityEngine.Serialization.FormerlySerializedAs("steps")]
        [Tooltip("全体の基本サンプル数。Spot・局所霧との交差区間には必要に応じて追加サンプルを配置します。")]
        public ClampedIntParameter m_steps = new ClampedIntParameter(48, 16, 96);
        [Tooltip("Spot円錐との交差区間に確保する最低サンプル数。低い全体Stepsでも根元を飛び越えないようにします。")]
        public ClampedIntParameter m_spotMinimumSamples = new ClampedIntParameter(8, 2, 16);
        [Tooltip("局所霧の交差区間に確保する最低サンプル数。薄い箱・球の欠けを抑えます。")]
        public ClampedIntParameter m_localFogMinimumSamples = new ClampedIntParameter(3, 1, 8);
        [Tooltip("区間内のサンプル位置のゆらぎ。0=中央、1=全幅。Temporal有効時は時間方向にも分散します。")]
        public ClampedFloatParameter m_sampleJitter = new ClampedFloatParameter(0.35f, 0, 1);
        [UnityEngine.Serialization.FormerlySerializedAs("resolutionDivisor")]
        public ClampedIntParameter m_resolutionDivisor = new ClampedIntParameter(2, 1, 4);
        [Tooltip("低解像度で細くなるSpot根元をフル解像度で再計算。大きく映る領域は対象外です。")]
        public BoolParameter m_refineRootsFlag = new BoolParameter(true);
        [Tooltip("深度境界で対応する低解像度サンプルがないピクセルを再計算し、にじみを抑えます。")]
        public BoolParameter m_refineDepthEdgesFlag = new BoolParameter(true);
        [Tooltip("8×8の低解像度ピクセルごとにSpot/霧候補を事前選別。効果が少ないシーンではオフにできます。")]
        public BoolParameter m_tileCullingFlag = new BoolParameter(false);
        [Tooltip("散乱履歴を再投影して低サンプル時のノイズを軽減します。カメラカット・照明変更時はリセットします。")]
        public BoolParameter m_temporalFlag = new BoolParameter(false);
        public ClampedFloatParameter m_historyWeight = new ClampedFloatParameter(0.85f, 0, 0.95f);
        [UnityEngine.Serialization.FormerlySerializedAs("godRays")]
        public BoolParameter m_enableGodRaysFlag = new BoolParameter(false);
        [UnityEngine.Serialization.FormerlySerializedAs("spatialGodRays")]
        public BoolParameter m_enableSpatialGodRaysFlag = new BoolParameter(false);
        [UnityEngine.Serialization.FormerlySerializedAs("spatialGodRayIntensity")]
        public ClampedFloatParameter m_spatialGodRayIntensity = new ClampedFloatParameter(1, 0, 10);
        [UnityEngine.Serialization.FormerlySerializedAs("godRayIntensity")]
        public ClampedFloatParameter m_godRayIntensity = new ClampedFloatParameter(0.6f, 0, 3);
        [UnityEngine.Serialization.FormerlySerializedAs("godRayRadius")]
        public ClampedFloatParameter m_godRayRadius = new ClampedFloatParameter(0.35f, 0.05f, 1);
        [UnityEngine.Serialization.FormerlySerializedAs("godRaySamples")]
        public ClampedIntParameter m_godRaySamples = new ClampedIntParameter(32, 8, 64);
        [UnityEngine.Serialization.FormerlySerializedAs("maxDistance")]
        public ClampedFloatParameter m_maxDistance = new ClampedFloatParameter(35, 1, 100);
        [UnityEngine.Serialization.FormerlySerializedAs("anisotropy")]
        public ClampedFloatParameter m_anisotropy = new ClampedFloatParameter(0.35f, -0.5f, 0.8f);
        [UnityEngine.Serialization.FormerlySerializedAs("baseHeight")]
        public FloatParameter m_baseHeight = new FloatParameter(0);
        [UnityEngine.Serialization.FormerlySerializedAs("heightFalloff")]
        public ClampedFloatParameter m_heightFalloff = new ClampedFloatParameter(0.15f, 0, 2);
        [UnityEngine.Serialization.FormerlySerializedAs("noiseAmount")]
        public ClampedFloatParameter m_noiseAmount = new ClampedFloatParameter(0.3f, 0, 1);
        [UnityEngine.Serialization.FormerlySerializedAs("noiseScale")]
        public ClampedFloatParameter m_noiseScale = new ClampedFloatParameter(0.45f, 0.01f, 5);
        [UnityEngine.Serialization.FormerlySerializedAs("wind")]
        public Vector3Parameter m_wind = new Vector3Parameter(new Vector3(0.08f, 0, 0.03f));
        [UnityEngine.Serialization.FormerlySerializedAs("ambient")]
        public ClampedFloatParameter m_ambient = new ClampedFloatParameter(0.04f, 0, 1);
        [Tooltip("選別された光柱・局所霧の片側フェード秒数。退場後に同じ枠で次の演出が登場します。")]
        public ClampedFloatParameter m_selectionFadeSeconds = new ClampedFloatParameter(0.35f, 0, 3);
        [Tooltip("表示中の演出への優先加算率。順位が僅差のときの頻繁な切替を抑えます。")]
        public ClampedFloatParameter m_selectionHysteresis = new ClampedFloatParameter(0.2f, 0, 1);
        public bool IsFogActive() => (m_density.value > 0 || LocalFogVolume.g_active.Count > 0) && m_intensity.value > 0;
        public bool IsActive() => m_enableFlag.value && (IsFogActive() || (m_enableGodRaysFlag.value && m_godRayIntensity.value > 0));
        public bool IsTileCompatible() => false;
    }
}
