Shader "YasudaHaruto/Page Pickup Paper"
{
    Properties
    {
        [MainTexture] _BaseMap ("Paper", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _PaperWaveOrigin ("Wave Origin and Amplitude", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PaperWaveDirection ("Wave Direction and Phase", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float4 _PaperWaveOrigin;
                float4 _PaperWaveDirection;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 position = TransformObjectToWorld(input.positionOS.xyz);
                float3 direction = _PaperWaveDirection.xyz;
                float amplitude = _PaperWaveOrigin.w;
                float phase = dot(position - _PaperWaveOrigin.xyz, direction) * 23.0 - _PaperWaveDirection.w;
                position.y += sin(phase) * amplitude;
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                normal -= direction * (normal.y * cos(phase) * amplitude * 23.0);
                output.normalWS = normalize(normal);
                output.positionCS = TransformWorldToHClip(position);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half3 normal = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                Light sun = GetMainLight();
                half3 key = sun.color * saturate(dot(normal, sun.direction)) * 0.28;
                half fill = saturate(dot(normal, normalize(half3(-0.4, 0.85, 0.35)))) * 0.24;
                half3 light = 0.46 + fill + key + max(0, SampleSH(normal)) * 0.20;
                half4 paper = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                return half4(paper.rgb * light, paper.a);
            }
            ENDHLSL
        }
    }
}
