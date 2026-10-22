#ifndef TEAM_TOON_INPUT_INCLUDED
#define TEAM_TOON_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

// Keep this layout identical in every pass and every variant (SRP Batcher).
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor, _ShadowColor, _DeepShadowColor, _SpecularColor, _RimColor;
half4 _EmissionColor, _OutlineColor;
float4 _FaceForward, _FaceRight;
half _ShadeMode, _ShadowThreshold, _ShadowSoftness, _DeepThreshold, _DeepSoftness, _MulAlpha;
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
half4 _GhostRimColor, _GhostGlowColor;
float4 _DeformAnchor, _DeformAxis, _DeformCenter, _DeformStretch, _DeformDrag, _DeformWave;
half _SurfaceType, _CoreAlpha, _EdgeAlpha, _EdgePower, _AdditiveBlend, _GhostRimPower, _Visibility, _DepthPrepass;
half _GhostGlowEnabled, _GhostGlowWidth, _GhostGlowFalloff;
half _DeformEnabled, _DeformCurve, _DeformWaveCount;
half _SrcBlend, _DstBlend, _ZWrite;
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
TEXTURE2D_X_FLOAT(_TeamToonGhostMask);

// R: nearest ghost linear depth, G: its character ID (0 = none). positionCS is SV_Position.
float2 ToonGhostMask(float4 positionCS)
{
    return LOAD_TEXTURE2D_X(_TeamToonGhostMask, uint2(positionCS.xy)).rg;
}
bool ToonGhostSameCharacter(float2 mask)
{
    return abs(mask.y - _CharacterId) < 0.25;
}
// A nearer fragment of the same character covers this pixel (body over hands, front over back).
bool ToonGhostHidden(float4 positionCS, float3 positionWS)
{
    float2 mask = ToonGhostMask(positionCS);
    float depth = -TransformWorldToView(positionWS).z;
    return ToonGhostSameCharacter(mask) && depth > mask.x + max(0.002, depth * 0.002);
}

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

half MulAlpha(half colorA)
{
    return _MulAlpha * colorA;
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

// Procedural deformer in world space, applied after skinning. TeamToonGhostDeformer
// writes the _Deform* vectors every frame; the material defaults are an identity transform.
float3 ToonDeformPosition(float3 p)
{
    float3 fromAnchor = p - _DeformAnchor.xyz;
    float t = saturate(dot(fromAnchor, _DeformAxis.xyz) / max(_DeformAnchor.w, 0.001));
    float weight = pow(t, max(_DeformCurve, 0.01));
    // Volume-preserving squash & stretch around the pivot.
    float3 d = _DeformStretch.xyz;
    float s = max(_DeformStretch.w, 0.05);
    float3 r = p - _DeformCenter.xyz;
    float along = dot(r, d);
    p = _DeformCenter.xyz + d * (along * s) + (r - d * along) * rsqrt(s);
    // The tip trails behind the motion and waves along the anchor → tip axis.
    p += _DeformDrag.xyz * weight;
    p += _DeformWave.xyz * (sin(t * _DeformWaveCount * TWO_PI - _DeformWave.w) * weight);
    return p;
}

void ToonDeform(inout float3 positionWS, inout real3 normalWS, inout real3 tangentWS)
{
#if defined(_TOON_DEFORM)
    // Normals and tangents follow the deformation through finite differences.
    const float e = 0.01;
    float3 n = SafeNormalize(float3(normalWS));
    float3 a = SafeNormalize(cross(n, abs(n.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
    float3 b = cross(n, a);
    float3 p = ToonDeformPosition(positionWS);
    float3 deformedNormal = SafeNormalize(cross(ToonDeformPosition(positionWS + a * e) - p, ToonDeformPosition(positionWS + b * e) - p));
    float3 t = float3(tangentWS);
    if (dot(t, t) > 1e-6)
    {
        t = ToonDeformPosition(positionWS + SafeNormalize(t) * e) - p;
        tangentWS = (real3)SafeNormalize(t - deformedNormal * dot(t, deformedNormal));
    }
    positionWS = p + deformedNormal * _DeformCenter.w;
    normalWS = (real3)deformedNormal;
#endif
}
#endif
