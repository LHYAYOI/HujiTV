// 床を這う煙のパーティクル用シェーダー。
// テクスチャを使わず、煙の形はすべてこの中のノイズで作る（外部素材なし）。
// パーティクルの Custom Vertex Streams は Position, Color, UV, StableRandom.x, AgePercent の順にすること
// （DoorSmokeSetup のメニューで作ると自動で設定される）。
Shader "custom/hatake/SmokePuff"
{
    Properties
    {
        _Color("Tint", Color) = (0.86, 0.9, 1, 1)
        _Density("Density", Range(0, 3)) = 1
        _NoiseScale("Noise Scale", Range(0.5, 8)) = 2.6
        _Billow("Billow (edge breakup)", Range(0, 1)) = 0.75
        _Swirl("Swirl Speed", Range(0, 3)) = 0.8
        _Ambient("Ambient", Range(0, 2)) = 0.7
        _Diffuse("Direct Light", Range(0, 2)) = 0.35
        _BackLight("Back Light (forward scatter)", Range(0, 8)) = 2.5
        _Anisotropy("Forward Scatter Sharpness", Range(0, 0.95)) = 0.6
        _SoftDistance("Soft Intersection (m)", Float) = 0.35
        _NearFade("Near Fade Start / Length (m)", Vector) = (0.5, 1.2, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/hatake/lightEffect/shaders/DistanceFogCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Density, _NoiseScale, _Billow, _Swirl, _Ambient, _Diffuse, _BackLight, _Anisotropy, _SoftDistance;
            float4 _NearFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                // xy = UV、z = StableRandom.x、w = AgePercent
                float4 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 uv : TEXCOORD1;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color;
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

            // 2 オクターブのノイズ。もこもこした輪郭を作る
            float Billow(float2 p)
            {
                float value = 0.62 * Noise2(p);
                value += 0.38 * Noise2(p * 2.31 + 11.3);
                return value;
            }

            // Henyey-Greenstein の位相関数を、光源の真正面を向いたときに 1 になるよう割ったもの。
            // 光源の方を向いて煙を見たとき（逆光）に明るくなる
            float PhaseHG(float cosine, float g)
            {
                float denominator = max(0.0001, 1 + g * g - 2 * g * cosine);
                float peak = (1 + g) / max(0.0001, (1 - g) * (1 - g));
                return (1 - g * g) / (denominator * sqrt(denominator)) / peak;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float seed = input.uv.z;
                float age = input.uv.w;
                float2 centered = input.uv.xy * 2 - 1;

                // 先に安い判定で捨てる：輪郭の外、カメラに近すぎる粒、壁や床の奥
                float reach = length(centered);
                clip(1 + _Billow * 0.5 - reach);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float particleDepth = -TransformWorldToView(input.positionWS).z;
                float fade = input.color.a * _Color.a;
                // カメラに近すぎる粒を消す（トロッコが煙を突き抜けるとき用）
                fade *= saturate((particleDepth - _NearFade.x) / max(_NearFade.y, 0.001));
                clip(fade - 0.002);
                // 床や壁との交差をぼかす
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                fade *= saturate((sceneDepth - particleDepth) / max(_SoftDistance, 0.001));
                clip(fade - 0.002);

                // 粒ごとにずらして回転させたノイズで輪郭を崩す
                float angle = (seed - 0.5) * 6.2831 + age * _Swirl * (seed > 0.5 ? 1 : -1);
                float2x2 rotation = float2x2(cos(angle), -sin(angle), sin(angle), cos(angle));
                float2 swirled = mul(rotation, centered);
                float noise = Billow(swirled * _NoiseScale * 0.5 + seed * 37.0 + float2(0, age * 0.7));
                float radius = reach + (noise - 0.5) * _Billow;
                float shape = saturate(1 - radius);
                shape *= shape;
                float alpha = saturate(shape * (0.55 + noise) * _Density) * fade;
                clip(alpha - 0.002);

                // 視線と、縁ほど光が透ける量
                float3 viewRay = normalize(input.positionWS - GetCameraPositionWS());
                float thin = 1 - shape;
                // 上側ほど明るく、下側は自分の影で暗くする
                float selfShadow = lerp(0.55, 1, saturate(input.uv.y + (noise - 0.5) * 0.6));

                float3 lighting = SampleSH(float3(0, 1, 0)) * _Ambient * selfShadow;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float mainPhase = PhaseHG(dot(viewRay, mainLight.direction), _Anisotropy);
                lighting += mainLight.color * mainLight.shadowAttenuation * (_Diffuse * selfShadow + _BackLight * mainPhase * (0.35 + thin));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = screenUV;
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    float phase = PhaseHG(dot(viewRay, light.direction), _Anisotropy);
                    float3 incoming = light.color * light.distanceAttenuation * light.shadowAttenuation;
                    lighting += incoming * (_Diffuse * selfShadow + _BackLight * phase * (0.35 + thin));
                LIGHT_LOOP_END

                // 奥の粒は距離フォグに沈める
                float3 color = HatakeApplyFog(lighting * _Color.rgb * input.color.rgb, GetCameraPositionWS(), input.positionWS);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
