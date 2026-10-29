// 床の水たまり。床に寝かせた Quad に使う。テクスチャを使わず、形・波紋はこの中で作る。
// 映るもの：反射プローブ（部屋の景色）＋ ライトの照り返し ＋ ゴースト専用の平面反射（_GhostReflectionTex）。
// 水たまりの下の床を暗くし（濡れ）、その上に反射を足す。
Shader "custom/hatake/Puddle"
{
    Properties
    {
        [Header(Wet)]
        _WetColor("Wet Tint", Color) = (0.02, 0.025, 0.035, 1)
        _Darkening("Darkening", Range(0, 1)) = 0.55
        [Header(Shape)]
        _EdgeSoftness("Edge Softness", Range(0.01, 0.5)) = 0.12
        _Irregularity("Edge Irregularity", Range(0, 0.8)) = 0.35
        _ShapeScale("Edge Detail", Range(0.5, 8)) = 2.5
        [Header(Reflection)]
        _Reflectivity("Reflectivity", Range(0, 2)) = 1
        _MinReflection("Reflection at Steep View", Range(0, 1)) = 0.3
        _Roughness("Roughness", Range(0, 1)) = 0.04
        _Specular("Light Highlights", Range(0, 8)) = 2
        _SpecularPower("Highlight Sharpness", Range(16, 2048)) = 600
        [Header(Ghost)]
        _GhostStrength("Ghost Strength", Range(0, 4)) = 1.5
        _GhostDistortion("Ghost Ripple Distortion", Range(0, 0.1)) = 0.03
        [Header(Ripples)]
        _RippleStrength("Ripple Strength", Range(0, 2)) = 0.6
        _RippleWavelength("Ripple Width (m)", Range(0.01, 0.5)) = 0.06
        _RippleSpeed("Ripple Speed (m/s)", Range(0.05, 3)) = 0.45
        _RippleLife("Ripple Life (s)", Range(0.2, 5)) = 1.6
        [HideInInspector] _GhostReflectionTex("Ghost Reflection", 2D) = "black" {}
        [HideInInspector] _GhostEnabled("Ghost Enabled", Float) = 0
        [HideInInspector] _ShapeSeed("Shape Seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            // 色は事前に濃さを掛けてある：床を暗くして、反射を足す
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/hatake/lightEffect/shaders/DistanceFogCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _WetColor;
            float _Darkening, _EdgeSoftness, _Irregularity, _ShapeScale;
            float _Reflectivity, _MinReflection, _Roughness, _Specular, _SpecularPower;
            float _GhostStrength, _GhostDistortion;
            float _RippleStrength, _RippleWavelength, _RippleSpeed, _RippleLife;
            float _GhostEnabled, _ShapeSeed;
            CBUFFER_END

            TEXTURE2D(_GhostReflectionTex);
            SAMPLER(sampler_GhostReflectionTex);
            // x, y = 中心のワールド XZ、z = 強さ、w = 始まった時刻
            float4 _Ripples[16];

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

            // 波紋による水面の傾き（XZ）
            float2 RippleSlope(float3 positionWS)
            {
                float2 slope = 0;
                [unroll]
                for (int i = 0; i < 16; i++)
                {
                    float4 ripple = _Ripples[i];
                    float age = _Time.y - ripple.w;
                    if (ripple.z <= 0 || age < 0 || age > _RippleLife)
                    {
                        continue;
                    }
                    float2 delta = positionWS.xz - ripple.xy;
                    float reach = length(delta);
                    float x = (reach - age * _RippleSpeed) / _RippleWavelength;
                    float envelope = exp(-x * x * 0.35) * (1 - age / _RippleLife);
                    slope += delta / max(reach, 0.0001) * cos(x * 3.1416) * envelope * ripple.z;
                }
                return slope * _RippleStrength;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 縁がゆがんだ水たまりの形
                float2 centered = input.uv * 2 - 1;
                float edge = Noise2(input.uv * _ShapeScale + _ShapeSeed) * 0.65 + Noise2(input.uv * _ShapeScale * 2.3 + _ShapeSeed * 1.7) * 0.35;
                float reach = length(centered) + (edge - 0.5) * _Irregularity;
                float mask = saturate((1 - _Irregularity * 0.5 - reach) / _EdgeSoftness);
                clip(mask - 0.001);

                float2 slope = RippleSlope(input.positionWS);
                float3 up = normalize(input.normalWS);
                float3 normal = normalize(up + float3(-slope.x, 0, -slope.y));
                float3 view = normalize(GetCameraPositionWS() - input.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                // 見下ろすほど弱く、浅い角度ほど強く映る（水らしさ）。真上からでも少しは映す
                float facing = saturate(dot(normal, view));
                float fresnel = lerp(_MinReflection, 1, pow(1 - facing, 5)) * _Reflectivity;

                // 部屋の景色（反射プローブ）
                float3 reflected = reflect(-view, normal);
                float3 environment = GlossyEnvironmentReflection(reflected, input.positionWS, _Roughness, 1, screenUV);

                // ゴースト専用の平面反射。反射カメラの画像は左右が逆なので戻し、波紋で揺らす
                float2 ghostUV = float2(1 - screenUV.x, screenUV.y) + slope * _GhostDistortion;
                float3 ghost = SAMPLE_TEXTURE2D(_GhostReflectionTex, sampler_GhostReflectionTex, ghostUV).rgb * (_GhostStrength * _GhostEnabled);

                // ランプや月の照り返し
                float3 highlight = 0;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 halfway = normalize(mainLight.direction + view);
                highlight += mainLight.color * mainLight.shadowAttenuation * pow(saturate(dot(normal, halfway)), _SpecularPower);
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = screenUV;
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    float3 lightHalf = normalize(light.direction + view);
                    highlight += light.color * light.distanceAttenuation * light.shadowAttenuation * pow(saturate(dot(normal, lightHalf)), _SpecularPower);
                LIGHT_LOOP_END

                // 床を暗くする量（アルファ）と、その上に足す光（事前に濃さを掛ける）
                float darkening = mask * _Darkening;
                float3 color = _WetColor.rgb * darkening + (environment * fresnel + ghost * max(fresnel, 0.5) + highlight * _Specular) * mask;
                // 距離フォグ：暗くした床と足した光を、フォグの色へ寄せる
                float fog = HatakeFogAmount(GetCameraPositionWS(), input.positionWS);
                color = lerp(color, HatakeFogColor(GetCameraPositionWS(), input.positionWS) * darkening, fog);
                return half4(color, darkening);
            }
            ENDHLSL
        }
    }
}
