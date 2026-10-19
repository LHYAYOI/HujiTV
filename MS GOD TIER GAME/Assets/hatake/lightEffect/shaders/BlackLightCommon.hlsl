#ifndef BLACKLIGHT_COMMON_INCLUDED
#define BLACKLIGHT_COMMON_INCLUDED

// BlackLightFeature が設定するブラックライト（紫外線）の一覧。
// 先に URP の Shadows.hlsl（Lighting.hlsl 経由で可）を include しておくこと。
#define BLACKLIGHT_MAX_LIGHTS 8
float4 _BlackLightPosition[BLACKLIGHT_MAX_LIGHTS];  // xyz=位置, w=1/range^2
float4 _BlackLightDirection[BLACKLIGHT_MAX_LIGHTS]; // xyz=照射方向, w=cos(外側の角度)。Pointは-2
float4 _BlackLightParams[BLACKLIGHT_MAX_LIGHTS];    // x=紫外線強度, y=1/(cos内-cos外), z=URP追加ライト番号, w=影を使う
float _BlackLightCount;
float _BlackLightGain;

// 表面へ届く紫外線量。normalWeight=0 で法線を使わない（Unlit・粒子用）。
float BlackLightIrradiance(float3 positionWS, float3 normalWS, float wrap, float normalWeight)
{
    float totalIrradiance = 0;
    int lightCount = (int)min(_BlackLightCount, BLACKLIGHT_MAX_LIGHTS);
    [loop] for (int i = 0; i < lightCount; i++)
    {
        float3 toLight = _BlackLightPosition[i].xyz - positionWS;
        float distanceSq = max(dot(toLight, toLight), 0.0001);
        // URPのPunctual Lightと同じ距離・円錐減衰。
        float distanceFade = distanceSq * _BlackLightPosition[i].w;
        distanceFade = saturate(1 - distanceFade * distanceFade); distanceFade *= distanceFade;
        float3 direction = toLight * rsqrt(distanceSq);
        float coneFade = saturate((dot(-direction, _BlackLightDirection[i].xyz) - _BlackLightDirection[i].w) * _BlackLightParams[i].y);
        float attenuation = distanceFade * coneFade * coneFade / distanceSq;
        if (attenuation <= 0) continue;
        float facing = saturate((dot(normalWS, direction) + wrap) / (1 + wrap));
        facing = lerp(1, facing, normalWeight);
        float shadow = 1;
        if (_BlackLightParams[i].w > 0.5)
            shadow = AdditionalLightRealtimeShadow((int)_BlackLightParams[i].z, positionWS, direction);
        totalIrradiance += _BlackLightParams[i].x * attenuation * facing * shadow;
    }
    return totalIrradiance * _BlackLightGain;
}

// 蛍光の放出。色はライト色に依存せず、maxLuminance を上限に滑らかに飽和する（Bloomの白飛び対策）。
float3 FluorescenceEmission(float3 color, float efficiency, float maxLuminance, float irradiance)
{
    float3 emission = color * (efficiency * irradiance);
    if (maxLuminance > 0)
    {
        float luminance = dot(emission, float3(0.2126, 0.7152, 0.0722));
        emission *= maxLuminance / (maxLuminance + luminance);
    }
    return emission;
}

#endif
