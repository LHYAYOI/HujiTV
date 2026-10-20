

#ifndef TEAM_TOON_INPUT_INCLUDED
#define TEAM_TOON_INPUT_INCLUDED


#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"



// すべてのパスおよびすべてのバリエーションにおいて、このレイアウトを同一に保ってください（SRP Batcher）

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor, _ShadowColor, _DeepShadowColor, _SpecularColor, _RimColor;
half4 _EmissionColor, _OutlineColor;
float4 _FaceForward, _FaceRight;
half _ShadeMode, _ShadowThreshold, _ShadowSoftness, _DeepThreshold, _DeepSoftness;
half _SpecularMode, _SpecularStrength, _SpecularWidth, _SpecularSoftness;
half _NormalEnabled, _NormalScale, _ControlEnabled, _ThresholdMaskStrength;
half _SurfaceEnabled, _Metallic, _Roughness, _Occlusion;
half _AlphaClip, _Cutoff, _Cull, _ReceiveShadows, _ReceiveShadowStrength;
half _AmbientStrength, _AdditionalStrength, _LightColorInfluence, _LightIntensityInfluence;
half _SSAOReceive, _ColorPreservation;
half _RimEnabled, _RimStrength, _RimPower, _RimLightAlignment;
half _ReflectionEnabled, _ReflectionStrength;
half _EmissionEnabled, _EmissionMapEnabled;
half _FaceMode, _FaceBlend, _FaceSDFEnabled;
half _HairFlowEnabled, _HairShift, _HairSecondaryStrength, _HairBandStrength;
half _OutlineEnabled, _OutlineWidth, _OutlineMaskEnabled;
half _IsHair, _RevealEnabled, _RevealOpacity, _RevealMaxDepth, _CharacterId;
half _HasFaceSDFMap, _HasHairFlowMap, _HasEmissionMap, _HasOutlineMask;
CBUFFER_END

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
TEXTURE2D(_ControlMap); SAMPLER(sampler_ControlMap);
TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
TEXTURE2D(_RampMap); SAMPLER(sampler_RampMap);
TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
TEXTURE2D(_FaceSDFMap); SAMPLER(sampler_FaceSDFMap);
TEXTURE2D(_HairFlowMap); SAMPLER(sampler_HairFlowMap);
TEXTURE2D(_OutlineMask); SAMPLER(sampler_OutlineMask);

half4 ToonBase(float2 uv)
{
    half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
#if defined(_TOON_ALPHA_CLIP)
    clip(base.a - _Cutoff);
#endif
    return base;
}

half4 ToonControl(float2 uv)
{
#if defined(_TOON_CONTROL_MAP)
    return SAMPLE_TEXTURE2D(_ControlMap, sampler_ControlMap, uv);
#else
    return half4(1, 1, 1, 0.5);
#endif
}

half3 ToonSurface(float2 uv)
{
    half3 surface = half3(_Metallic, _Roughness, _Occlusion);
#if defined(_TOON_SURFACE_MAP)
    surface *= SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, uv).rgb;
#endif
    return half3(saturate(surface.r), clamp(surface.g, 0.04, 1), saturate(surface.b));
}

half3 ToonNormal(float2 uv, half3 normalWS, half4 tangentWS, bool frontFace)
{
    half3 n = SafeNormalize(normalWS);
#if defined(_TOON_NORMALMAP)
    half3 t = SafeNormalize(tangentWS.xyz);
    half3 b = cross(n, t) * tangentWS.w;
    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalScale);
    n = SafeNormalize(TransformTangentToWorld(normalTS, half3x3(t, b, n)));
#endif
    return frontFace ? n : -n;
}
#endif
