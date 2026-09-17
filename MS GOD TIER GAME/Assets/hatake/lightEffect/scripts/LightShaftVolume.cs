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
        public ClampedIntParameter m_steps = new ClampedIntParameter(48, 16, 96);
        [UnityEngine.Serialization.FormerlySerializedAs("resolutionDivisor")]
        public ClampedIntParameter m_resolutionDivisor = new ClampedIntParameter(2, 1, 4);
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
        public bool IsFogActive() => (m_density.value > 0 || LocalFogVolume.g_active.Count > 0) && m_intensity.value > 0;
        public bool IsActive() => m_enableFlag.value && (IsFogActive() || (m_enableGodRaysFlag.value && m_godRayIntensity.value > 0));
        public bool IsTileCompatible() => false;
    }
}
