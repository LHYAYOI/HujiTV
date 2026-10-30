// 鏡。Mirror コンポーネントが描いた反射画像（_MirrorTex）を貼る。描いていないときは反射プローブの景色を映す。
// 古い洋館の鏡らしく、縁の曇りと銀の剥げ、かすかな揺らぎを足せる。テクスチャは使わない。
Shader "custom/hatake/Mirror"
{
    Properties
    {
        _Tint("Tint", Color) = (0.86, 0.88, 0.92, 1)
        _Brightness("Brightness", Range(0, 2)) = 1
        _EdgeDarkening("Edge Darkening", Range(0, 1)) = 0.45
        _Spots("Silver Spots", Range(0, 1)) = 0.3
        _SpotScale("Spot Size", Range(1, 20)) = 7
        _Wobble("Wobble", Range(0, 0.02)) = 0.002
        _WobbleSpeed("Wobble Speed", Range(0, 3)) = 0.4
        _FallbackStrength("Probe Fallback", Range(0, 2)) = 0.7
        [HideInInspector] _MirrorTex("Mirror", 2D) = "black" {}
        [HideInInspector] _MirrorEnabled("Mirror Enabled", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/hatake/lightEffect/shaders/DistanceFogCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;
            float _Brightness, _EdgeDarkening, _Spots, _SpotScale, _Wobble, _WobbleSpeed, _FallbackStrength, _MirrorEnabled;
            CBUFFER_END

            TEXTURE2D(_MirrorTex);
            SAMPLER(sampler_MirrorTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                // Unity の Quad は -Z が表
                output.normalWS = TransformObjectToWorldNormal(float3(0, 0, -1));
                return output;
            }

            float Hash2(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float Noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash2(i), Hash2(i + float2(1, 0)), f.x), lerp(Hash2(i + float2(0, 1)), Hash2(i + float2(1, 1)), f.x), f.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                // かすかな揺らぎ（古いガラスのゆがみ）
                float time = _Time.y * _WobbleSpeed;
                float2 wobble = float2(Noise2(input.uv * 5 + time), Noise2(input.uv * 5 + 17.3 - time)) - 0.5;

                // 反射カメラの画像は左右が逆なので戻す
                float2 mirrorUV = float2(1 - screenUV.x, screenUV.y) + wobble * _Wobble;
                float3 mirrored = SAMPLE_TEXTURE2D(_MirrorTex, sampler_MirrorTex, mirrorUV).rgb;

                // 描いていないときは反射プローブで代わりに映す
                float3 view = normalize(GetCameraPositionWS() - input.positionWS);
                float3 reflected = reflect(-view, normalize(input.normalWS));
                float3 fallback = GlossyEnvironmentReflection(reflected, input.positionWS, 0.05, 1, screenUV) * _FallbackStrength;
                float3 color = lerp(fallback, mirrored, _MirrorEnabled) * _Tint.rgb * _Brightness;

                // 縁の曇りと銀の剥げ
                float2 centered = input.uv * 2 - 1;
                float edge = smoothstep(0.55, 1, max(abs(centered.x), abs(centered.y)));
                float spots = smoothstep(0.62, 0.85, Noise2(input.uv * _SpotScale) * 0.7 + Noise2(input.uv * _SpotScale * 2.7 + 5.1) * 0.3);
                color *= 1 - _EdgeDarkening * edge;
                color *= 1 - _Spots * spots * (0.4 + 0.6 * edge);

                color = HatakeApplyFog(color, GetCameraPositionWS(), input.positionWS);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // 深度テクスチャに鏡を書く（距離フォグや半透明のソフトパーティクルが正しく働くように）
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
