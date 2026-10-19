#ifndef FLUORESCENT_UNLIT_FORWARD_PASS_INCLUDED
#define FLUORESCENT_UNLIT_FORWARD_PASS_INCLUDED

// URP の UnlitPassFragment の結果に蛍光の放出を加える。
// Unlit は法線・影を使わず「紫外線強度 × 距離/円錐減衰」だけで光る。
#include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitForwardPass.hlsl"
#include "BlackLightCommon.hlsl"

void FluorescentUnlitPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UnlitPassFragment(input, outColor
#ifdef _WRITE_RENDERING_LAYERS
        , outRenderingLayers
#endif
    );

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    // テクスチャ参照は分岐の外で行う（ミップ選択の微分を分岐内に入れない）。
    half3 fluorColor = _FluorColor.rgb * SAMPLE_TEXTURE2D(_FluorMap, sampler_BaseMap, input.uv).rgb;
    fluorColor *= lerp(half3(1, 1, 1), SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb, _FluorAlbedoTint);
    if (_FluorEfficiency > 0 && _BlackLightCount > 0.5)
    {
        // Unlit の Varyings はワールド座標を持たないため、画面位置と深度から復元する。
        float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
        float deviceDepth = input.positionCS.z;
        #if !UNITY_REVERSED_Z
            deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, deviceDepth);
        #endif
        float3 positionWS = ComputeWorldSpacePosition(screenUV, deviceDepth, UNITY_MATRIX_I_VP);
        float irradiance = BlackLightIrradiance(positionWS, float3(0, 1, 0), 0, 0);
        outColor.rgb += FluorescenceEmission(fluorColor, _FluorEfficiency, _FluorSaturation, irradiance);
    }
}

#endif
