

#ifndef TEAM_TOON_FORWARD_INCLUDED
#define TEAM_TOON_FORWARD_INCLUDED

#include "TeamToonInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct ToonAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ToonVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float2 uv : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 4);
#ifdef DYNAMICLIGHTMAP_ON
    float2 dynamicLightmapUV : TEXCOORD5;
#endif
    half4 fogAndVertexLight : TEXCOORD6;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};





ToonVaryings ToonVertex(ToonAttributes input)
{
    ToonVaryings output = (ToonVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = p.positionCS;
    output.positionWS = p.positionWS;
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.normalWS = n.normalWS;
    output.tangentWS = half4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
    OUTPUT_SH(n.normalWS, output.vertexSH);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    output.fogAndVertexLight = half4(ComputeFogFactor(p.positionCS.z), VertexLighting(p.positionWS, n.normalWS));
    return output;
}





half ToonBand(half value, half threshold, half softness)
{
    // ハイライトやシャドウの細い帯状の部分において、微分幅の制限がちらつき（シマー）として現れる
    half width = max(max(softness, fwidth(value) * 0.5), 0.0001);
    return smoothstep(threshold - width, threshold + width, value);
}

half3 ToonLightColor(half3 color)
{
    half intensity = max(max(color.r, color.g), color.b);
    // ライトが存在しない場合、影響が減少してもゼロのままになる。
    half3 hue = color / max(intensity, 0.0001);
    return lerp(half3(1, 1, 1), hue, _LightColorInfluence)
         * lerp(min(intensity, 1.0h), intensity, _LightIntensityInfluence);
}


half ToonShadow(half attenuation)
{
#if defined(_TOON_RECEIVE_SHADOWS_OFF)
    return 1;
#else
    return lerp(1, attenuation, _ReceiveShadowStrength);
#endif
}



half3 ToonDiffuseTint(half ndl, half4 control, float2 uv, half3 lightDir)
{
    half offset = (control.a * 2 - 1) * _ThresholdMaskStrength;
    half lit = ToonBand(ndl, _ShadowThreshold + offset, _ShadowSoftness);
#if defined(_TOON_FACE)
    if (_FaceMode > 1.5 && _HasFaceSDFMap > 0.5)
    {
        float3 f = SafeNormalize(_FaceForward.xyz);
        float3 r = SafeNormalize(_FaceRight.xyz - f * dot(f, _FaceRight.xyz));
        float3 up = SafeNormalize(cross(f, r));
        float3 l = SafeNormalize(lightDir - up * dot(lightDir, up));
        float2 faceUV = uv;
        if (dot(l, r) < 0) faceUV.x = 1 - faceUV.x;
        half sdf = SAMPLE_TEXTURE2D(_FaceSDFMap, sampler_FaceSDFMap, faceUV).r;
        // White admits more light, black admits less. Threshold is the light's angle.
        half lightAngle = 1 - saturate(dot(l, f) * 0.5 + 0.5);
        lit = ToonBand(sdf, lightAngle + _ShadowThreshold + offset, _ShadowSoftness);
    }
#endif
#if defined(_TOON_SHADE_SMOOTH)
    return lerp(_ShadowColor.rgb, half3(1, 1, 1), saturate(ndl * 0.5 + 0.5));
#elif defined(_TOON_SHADE_THREE)
    half middle = ToonBand(ndl, min(_DeepThreshold, _ShadowThreshold - 0.001) + offset, _DeepSoftness);
    return lerp(lerp(_DeepShadowColor.rgb, _ShadowColor.rgb, middle), half3(1, 1, 1), lit);
#elif defined(_TOON_SHADE_RAMP)
    return SAMPLE_TEXTURE2D(_RampMap, sampler_RampMap, float2(saturate(ndl * 0.5 + 0.5 + offset), 0.5)).rgb;
#else
    return lerp(_ShadowColor.rgb, half3(1, 1, 1), lit);
#endif
}


half3 ToonHighlight(ToonVaryings input, half3 n, half3 v, Light light, half3 albedo, half metallic, half roughness, half mask)
{
#if defined(_TOON_SPECULAR) || defined(_TOON_SPECULAR_HAIR)
    half3 h = SafeNormalize(v + light.direction);
    half highlight;
    half width = max(_SpecularWidth * lerp(0.4, 1.6, roughness), 0.001);
#if defined(_TOON_SPECULAR_HAIR)
    half3 t = SafeNormalize(input.tangentWS.xyz);
    half3 b = SafeNormalize(cross(n, t) * input.tangentWS.w);
    half3 strand = b;
    if (_HasHairFlowMap > 0.5)
    {
        half2 flow = SAMPLE_TEXTURE2D(_HairFlowMap, sampler_HairFlowMap, input.uv).rg * 2 - 1;
        if (dot(flow, flow) > 0.0001) strand = SafeNormalize(t * flow.x + b * flow.y);
    }
    half3 shifted = SafeNormalize(strand + n * _HairShift);
    half th = dot(shifted, h);
    half primary = sqrt(saturate(1 - th * th));
    highlight = ToonBand(primary, 1 - width, _SpecularSoftness);
    half secondaryTH = dot(SafeNormalize(strand - n * (_HairShift + 0.2)), h);
    highlight += _HairSecondaryStrength * ToonBand(sqrt(saturate(1 - secondaryTH * secondaryTH)), 1 - width * 0.6, _SpecularSoftness);
    half band = 1 - abs(dot(n, h) - 0.7) / max(width, 0.001);
    highlight += _HairBandStrength * ToonBand(band, 0.5, _SpecularSoftness);
#else
    highlight = ToonBand(saturate(dot(n, h)), 1 - width, _SpecularSoftness);
#endif
    return lerp(_SpecularColor.rgb, albedo, metallic) * highlight * mask * _SpecularStrength
         * saturate(dot(n, light.direction)) * ToonLightColor(light.color)
         * light.distanceAttenuation * ToonShadow(light.shadowAttenuation);
#else
    return 0;
#endif
}



half3 ToonAdditional(Light light, half3 normalWS, half3 albedo)
{
    return albedo * saturate(dot(normalWS, light.direction)) * ToonLightColor(light.color)
         * light.distanceAttenuation * ToonShadow(light.shadowAttenuation) * _AdditionalStrength;
}



half4 ToonFragment(ToonVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half4 base = ToonBase(input.uv);
    half3 n = ToonNormal(input.uv, input.normalWS, input.tangentWS, IS_FRONT_VFACE(face, true, false));
    half3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 color = base.rgb;
#if !defined(_TOON_SHADE_UNLIT)
    half4 control = ToonControl(input.uv);
    half3 surface = ToonSurface(input.uv);
    half ao = surface.b * control.g;
    InputData inputData = (InputData) 0; // LIGHT_LOOP_BEGINには、この変数が必要です。
    inputData.positionWS = input.positionWS;
    inputData.normalWS = n;
    inputData.viewDirectionWS = v;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    inputData.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
#else
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
    half4 shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
    Light mainLight = GetMainLight(inputData.shadowCoord, input.positionWS, shadowMask);
    half3 lightingNormal = n;
#if defined(_TOON_FACE)
    lightingNormal = SafeNormalize(lerp(n, SafeNormalize(_FaceForward.xyz), _FaceBlend));
#endif
    half3 bakedGI;
#ifdef DYNAMICLIGHTMAP_ON
    bakedGI = SAMPLE_GI(input.lightmapUV, input.dynamicLightmapUV, input.vertexSH, n);
#else
    bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, n);
#endif
    MixRealtimeAndBakedGI(mainLight, n, bakedGI, shadowMask);
    AmbientOcclusionFactor screenAO = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    half directAO = lerp(1, screenAO.directAmbientOcclusion, _SSAOReceive);
    half indirectAO = lerp(1, screenAO.indirectAmbientOcclusion, _SSAOReceive);
    half diffuseWeight = lerp(1, 0.25, surface.r); // 意図的に変更しています、BRDFではないです
    half3 tint = ToonDiffuseTint(dot(lightingNormal, mainLight.direction), control, input.uv, mainLight.direction);
    half3 shadowTint = lerp(_ShadowColor.rgb, tint, ToonShadow(mainLight.shadowAttenuation));
    color = base.rgb * shadowTint * ToonLightColor(mainLight.color) * mainLight.distanceAttenuation * directAO;
#if USE_CLUSTER_LIGHT_LOOP
    UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
        color += ToonAdditional(light, n, base.rgb) * directAO;
    }
#endif
#if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
    uint pixelLightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
        color += ToonAdditional(light, n, base.rgb) * directAO;
    LIGHT_LOOP_END
#endif
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    color += base.rgb * input.fogAndVertexLight.yzw * _AdditionalStrength * directAO;
#endif
    color = (color + base.rgb * bakedGI * _AmbientStrength * indirectAO) * ao * diffuseWeight;
    color += ToonHighlight(input, n, v, mainLight, base.rgb, surface.r, surface.g, control.r);
#if defined(_TOON_REFLECTION)
    half3 reflection = GlossyEnvironmentReflection(reflect(-v, n), input.positionWS, surface.g, ao, inputData.normalizedScreenSpaceUV);
    half fresnel = pow(1 - saturate(dot(n, v)), 5);
    half3 f0 = lerp(half3(0.04, 0.04, 0.04), base.rgb, surface.r);
    color += reflection * lerp(f0, half3(1, 1, 1), fresnel) * _ReflectionStrength;
#endif
#if defined(_TOON_RIM)
    half rim = pow(1 - saturate(dot(n, v)), max(_RimPower, 0.1));
    half alignment = lerp(1, saturate(dot(n, mainLight.direction)), _RimLightAlignment);
    color += _RimColor.rgb * rim * alignment * _RimStrength * control.b;
#endif
    color = lerp(color, base.rgb, _ColorPreservation);
#endif
#if defined(_TOON_EMISSION)
    half3 emission = _EmissionColor.rgb;
    if (_HasEmissionMap > 0.5) emission *= SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb;
    color += emission;
#endif
    color = MixFog(color, input.fogAndVertexLight.x);
    return half4(max(color, 0), 1);
}
#endif
