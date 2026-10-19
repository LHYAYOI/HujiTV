// 蛍光デカール。BlackLightFeature が不透明描画の後（光柱の合成より前）に箱メッシュとして描画する。
// 深度テクスチャから面の位置・法線を復元し、箱の中の面にだけ模様を投影する。
Shader "Hidden/hatake/BlackLight/FluorescentDecal"
{
    Properties
    {
        _DecalMask("Mask", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "BlackLightCommon.hlsl"

        TEXTURE2D(_DecalMask); SAMPLER(sampler_DecalMask);
        float4 _DecalMask_ST;   // xy=タイリング, zw=オフセット
        float4 _DecalShape;     // x=形(0テクスチャ/1楕円/2長方形), y=チャンネル(0A/1R/2輝度), z=縁のぼかし, w=不透明度
        float4 _DecalColor;     // rgb=蛍光色, w=変換効率
        float4 _DecalLighting;  // x=回り込み, y=cos(フェード開始角), z=cos(フェード終了角), w=奥行きフェード
        float4 _DecalTint;      // rgb=通常光での塗料の色, w=見える量
        float _DecalMaxLuminance;

        struct Attributes { float4 positionOS : POSITION; };
        struct Varyings { float4 positionCS : SV_POSITION; };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            return output;
        }

        // 深度テクスチャからスクリーンUV位置のワールド座標を復元する。
        float3 GetScenePosition(float2 uv)
        {
            float depth = SampleSceneDepth(uv);
            #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
            #endif
            return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
        }

        struct DecalSurface
        {
            float3 m_positionWS;
            float3 m_normalWS;
            float3 m_tint;   // テクスチャのRGB（A使用時）
            float m_mask;    // 0〜1
        };

        // 箱の外・空・急角度の面は false。
        bool EvaluateDecal(float4 positionCS, out DecalSurface surface)
        {
            surface = (DecalSurface)0;
            float2 uv = GetNormalizedScreenSpaceUV(positionCS);
            float rawDepth = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                if (rawDepth <= 0) return false;
            #else
                if (rawDepth >= 1) return false;
            #endif
            float3 positionWS = GetScenePosition(uv);
            float3 positionOS = TransformWorldToObject(positionWS);
            if (any(abs(positionOS) > 0.5)) return false;

            // 深度から法線を復元。左右・上下のうち差の小さい側を使い、物体の輪郭で法線が倒れるのを防ぐ。
            float2 texelSize = 1.0 / _ScaledScreenParams.xy;
            float3 left = GetScenePosition(uv - float2(texelSize.x, 0)), right = GetScenePosition(uv + float2(texelSize.x, 0));
            float3 down = GetScenePosition(uv - float2(0, texelSize.y)), up = GetScenePosition(uv + float2(0, texelSize.y));
            float3 deltaX = dot(right - positionWS, right - positionWS) < dot(positionWS - left, positionWS - left) ? right - positionWS : positionWS - left;
            float3 deltaY = dot(up - positionWS, up - positionWS) < dot(positionWS - down, positionWS - down) ? up - positionWS : positionWS - down;
            float3 normal = normalize(cross(deltaY, deltaX));
            if (dot(normal, GetWorldSpaceViewDir(positionWS)) < 0) normal = -normal;

            // 投影方向（ローカル+Z）に対する面の傾きでフェード。
            float3 projectionDirection = normalize(mul((float3x3)UNITY_MATRIX_M, float3(0, 0, 1)));
            float facing = dot(normal, -projectionDirection);
            float angleFade = saturate((facing - _DecalLighting.z) / max(_DecalLighting.y - _DecalLighting.z, 0.0001));
            float depthFade = saturate((0.5 - abs(positionOS.z)) / max(_DecalLighting.w * 0.5, 0.0001));

            float shapeMask = 1;
            float3 tint = 1;
            float2 decalUV = positionOS.xy + 0.5;
            if (_DecalShape.x < 0.5)
            {
                // 1ピクセル分の位置の差からUVの微分を求める（深度の境界で微分が暴れてミップが飛ぶのを防ぐ）。
                float2 maskUV = decalUV * _DecalMask_ST.xy + _DecalMask_ST.zw;
                float2 ddxUV = mul((float3x3)UNITY_MATRIX_I_M, deltaX).xy * _DecalMask_ST.xy;
                float2 ddyUV = mul((float3x3)UNITY_MATRIX_I_M, deltaY).xy * _DecalMask_ST.xy;
                float4 maskSample = SAMPLE_TEXTURE2D_GRAD(_DecalMask, sampler_DecalMask, maskUV, ddxUV, ddyUV);
                if (_DecalShape.y < 0.5) { shapeMask = maskSample.a; tint = maskSample.rgb; }
                else if (_DecalShape.y < 1.5) shapeMask = maskSample.r;
                else shapeMask = dot(maskSample.rgb, float3(0.2126, 0.7152, 0.0722));
            }
            else
            {
                float softness = max(_DecalShape.z * 2, 0.0001);
                float2 absPositionOS = abs(positionOS.xy * 2);
                if (_DecalShape.x < 1.5) shapeMask = saturate((1 - length(positionOS.xy * 2)) / softness);
                else shapeMask = saturate((1 - absPositionOS.x) / softness) * saturate((1 - absPositionOS.y) / softness);
            }

            surface.m_positionWS = positionWS;
            surface.m_normalWS = normal;
            surface.m_tint = tint;
            surface.m_mask = shapeMask * angleFade * depthFade * _DecalShape.w;
            return surface.m_mask > 0.0001;
        }
        ENDHLSL

        // 0: 通常光での塗料の色（下地に乗算）。
        Pass
        {
            Name "FluorescentDecalTint"
            Cull Front ZWrite Off ZTest Always
            Blend DstColor Zero
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragTint
            #pragma multi_compile_fog

            half4 FragTint(Varyings input) : SV_Target
            {
                DecalSurface surface;
                if (!EvaluateDecal(input.positionCS, surface)) discard;
                float3 tint = lerp(float3(1, 1, 1), _DecalTint.rgb * surface.m_tint, saturate(surface.m_mask * _DecalTint.w));
                float fogFactor = ComputeFogFactor(TransformWorldToHClip(surface.m_positionWS).z);
                return half4(MixFogColor(tint, float3(1, 1, 1), fogFactor), 1);
            }
            ENDHLSL
        }

        // 1: 紫外線を受けた蛍光の発光（加算）。
        Pass
        {
            Name "FluorescentDecalEmission"
            Cull Front ZWrite Off ZTest Always
            Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragEmission
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            half4 FragEmission(Varyings input) : SV_Target
            {
                if (_BlackLightCount < 0.5) discard;
                DecalSurface surface;
                if (!EvaluateDecal(input.positionCS, surface)) discard;
                float irradiance = BlackLightIrradiance(surface.m_positionWS, surface.m_normalWS, _DecalLighting.x, 1);
                float3 emission = FluorescenceEmission(_DecalColor.rgb * surface.m_tint, _DecalColor.w, _DecalMaxLuminance, irradiance) * surface.m_mask;
                // 加算なので霧の中では黒へ寄せる（URPのFog）。光柱の霧はこの後の合成で掛かる。
                float fogFactor = ComputeFogFactor(TransformWorldToHClip(surface.m_positionWS).z);
                return half4(MixFogColor(emission, float3(0, 0, 0), fogFactor), 0);
            }
            ENDHLSL
        }
    }
}
