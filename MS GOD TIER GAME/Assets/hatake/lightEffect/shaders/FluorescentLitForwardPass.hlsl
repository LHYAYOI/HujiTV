#ifndef FLUORESCENT_LIT_FORWARD_PASS_INCLUDED
#define FLUORESCENT_LIT_FORWARD_PASS_INCLUDED

// 頂点シェーダーと入力の初期化は URP の LitForwardPass.hlsl をそのまま使い、
// フラグメントだけ LitPassFragment と同じ手順に蛍光の放出を加える。
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
#include "BlackLightCommon.hlsl"

void FluorescentLitPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);

    // 蛍光：紫外線の吸収は面の向き・距離・影に従い、放出は視線に依存しない。
    // 放出色はライト色と無関係なマテリアル固有色。フォグの前に加算する。
    // テクスチャ参照は分岐の外で行う（ミップ選択の微分を分岐内に入れない）。
    half3 fluorColor = _FluorColor.rgb * SAMPLE_TEXTURE2D(_FluorMap, sampler_BaseMap, input.uv).rgb;
    fluorColor *= lerp(half3(1, 1, 1), surfaceData.albedo, _FluorAlbedoTint);
    if (_FluorEfficiency > 0 && _BlackLightCount > 0.5)
    {
        float irradiance = BlackLightIrradiance(inputData.positionWS, inputData.normalWS, _FluorWrap, 1);
        color.rgb += FluorescenceEmission(fluorColor, _FluorEfficiency, _FluorSaturation, irradiance);
    }

    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
