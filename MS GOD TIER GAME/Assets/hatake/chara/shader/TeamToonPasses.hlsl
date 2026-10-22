#ifndef TEAM_TOON_PASSES_INCLUDED
#define TEAM_TOON_PASSES_INCLUDED
#include "TeamToonInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

float3 _LightDirection;
float3 _LightPosition;
TEXTURE2D_X(_TeamToonHairMask); SAMPLER(sampler_TeamToonHairMask);
struct PassAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct PassVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    half4 tangentWS : TEXCOORD2;
    float3 positionWS : TEXCOORD3;
    half fog : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
PassVaryings ToonPassVertex(PassAttributes input)
{
    PassVaryings output = (PassVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexNormalInputs n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    ToonDeform(positionWS, n.normalWS, n.tangentWS);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.positionWS = positionWS;
    output.normalWS = n.normalWS;
    output.tangentWS = half4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.fog = ComputeFogFactor(output.positionCS.z);
    return output;
}
PassVaryings ToonShadowVertex(PassAttributes input)
{
    PassVaryings output = ToonPassVertex(input);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirection = SafeNormalize(_LightPosition - output.positionWS);
#else
    float3 lightDirection = _LightDirection;
#endif
    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, lightDirection)));
    return output;
}
half4 ToonDepthFragment(PassVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    ToonBase(input.uv);
    return 0;
}
half4 ToonDepthNormalsFragment(PassVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    ToonBase(input.uv);
    half3 normal = ToonNormal(input.uv, input.normalWS, input.tangentWS, IS_FRONT_VFACE(face, true, false));
#if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = PackNormalOctQuadEncode(normal);
    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
#else
    return half4(normal, 0);
#endif
}
PassVaryings ToonMetaVertex(PassAttributes input)
{
    PassVaryings output = ToonPassVertex(input);
    output.positionCS = MetaVertexPosition(input.positionOS, input.lightmapUV, input.dynamicLightmapUV, unity_LightmapST, unity_DynamicLightmapST);
    return output;
}
half4 ToonMetaFragment(PassVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    MetaInput meta = (MetaInput)0;
    meta.Albedo = ToonBase(input.uv).rgb;
#if defined(_TOON_EMISSION)
    meta.Emission = _EmissionColor.rgb;
    if (_HasEmissionMap > 0.5) meta.Emission *= SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb;
#endif
    return MetaFragment(meta);
}
PassVaryings ToonOutlineVertex(PassAttributes input)
{
    PassVaryings output = ToonPassVertex(input);
    float3 normalVS = TransformWorldToViewDir(output.normalWS, true);
    float2 projectedNormal = mul((float3x3)UNITY_MATRIX_P, normalVS).xy;
    float lengthSquared = dot(projectedNormal, projectedNormal);
    projectedNormal *= rsqrt(max(lengthSquared, 0.0001));
    float mask = 1;
    if (_HasOutlineMask > 0.5) mask = SAMPLE_TEXTURE2D_LOD(_OutlineMask, sampler_OutlineMask, output.uv, 0).r;
    output.positionCS.xy += projectedNormal * _OutlineWidth * mask * (2 / _ScaledScreenParams.xy) * output.positionCS.w;
    return output;
}
half4 ToonOutlineFragment(PassVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    clip(_OutlineEnabled - 0.5);
    ToonBase(input.uv);
    return half4(MixFog(_OutlineColor.rgb, input.fog), 1);
}
float4 ToonHairMaskFragment(PassVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    clip(_IsHair - 0.5);
    ToonBase(input.uv);
    return float4(-TransformWorldToView(input.positionWS).z, _CharacterId, 0, 1);
}
half4 ToonRevealFragment(PassVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    clip(_RevealEnabled - 0.5);
    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
    float2 hair = SAMPLE_TEXTURE2D_X(_TeamToonHairMask, sampler_TeamToonHairMask, screenUV).rg;
    // The mask contains visible hair only. IDs prevent revealing another character's eyes.
    clip(0.25 - abs(hair.y - _CharacterId));
    float featureDepth = -TransformWorldToView(input.positionWS).z;
    float difference = featureDepth - hair.x;
    clip(difference - 0.0001);
    clip(_RevealMaxDepth - difference);
    half4 base = ToonBase(input.uv);
    return half4(MixFog(base.rgb, input.fog), _RevealOpacity);
}
PassVaryings ToonGhostMaskVertex(PassAttributes input)
{
    PassVaryings output = ToonPassVertex(input);
    // Materials saved before Synchronize ran may still have this pass enabled; collapse it.
    if (_SurfaceType < 0.5 || _DepthPrepass < 0.5) output.positionCS = float4(0, 0, 0, 1);
    return output;
}
float4 ToonGhostMaskFragment(PassVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        //消えてるなら遮蔽しない
        if (_MulAlpha < 0.001)
            discard;
        
    ToonBase(input.uv);
    return float4(-TransformWorldToView(input.positionWS).z, _CharacterId, 0, 1);
}
PassVaryings ToonGhostGlowVertex(PassAttributes input)
{
    PassVaryings output = ToonPassVertex(input);
    output.positionWS += SafeNormalize(float3(output.normalWS)) * _GhostGlowWidth;
    output.positionCS = TransformWorldToHClip(output.positionWS);
    if (_SurfaceType < 0.5 || _GhostGlowEnabled < 0.5) output.positionCS = float4(0, 0, 0, 1);
    return output;
}
half4 ToonGhostGlowFragment(PassVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    ToonBase(input.uv);
    // Back faces of the inflated shell: bright next to the body's silhouette, fading outward.
    // The ghost mask hides the shell inside the character's combined silhouette.
    if (_DepthPrepass > 0.5 && ToonGhostSameCharacter(ToonGhostMask(input.positionCS))) discard;
    half3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half facing = saturate(dot(-SafeNormalize(input.normalWS), v) * 2);
    half3 glow = _GhostGlowColor.rgb * pow(facing, max(_GhostGlowFalloff, 0.1)) * _Visibility;
    glow *= saturate(_MulAlpha);
    return half4(MixFogColor(glow, half3(0, 0, 0), input.fog), 0);
}
#endif
