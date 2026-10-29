// 雨粒と水しぶきのパーティクル用シェーダー。テクスチャを使わず、粒の形はこの中で作る。
// 伸ばした板（Stretched Billboard）に使うと、丸い形が縦に伸びて雨粒の筋になる。
Shader "custom/hatake/RainDrop"
{
    Properties
    {
        _Color("Tint", Color) = (0.8, 0.88, 1, 0.7)
        _Brightness("Brightness", Range(0, 8)) = 1.6
        _Ambient("Ambient", Range(0, 2)) = 0.6
        _Softness("Edge Softness", Range(0.05, 1)) = 0.6
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/hatake/lightEffect/shaders/DistanceFogCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Brightness, _Ambient, _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // 丸い粒。伸ばした板では縦長の筋になる
                float reach = length(input.uv * 2 - 1);
                float alpha = pow(saturate((1 - reach) / _Softness), 1.5) * input.color.a * _Color.a;
                clip(alpha - 0.003);

                // 周りの光を拾って光る（暗い部屋ではランプの色に光る）
                float3 lighting = SampleSH(float3(0, 1, 0)) * _Ambient;
                Light mainLight = GetMainLight();
                lighting += mainLight.color;
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                    lighting += light.color * light.distanceAttenuation;
                LIGHT_LOOP_END

                float3 color = lighting * _Color.rgb * input.color.rgb * _Brightness;
                color = HatakeApplyFog(color, GetCameraPositionWS(), input.positionWS);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
