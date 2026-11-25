Shader "Custom/RadialBlur"
{
   SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_BlitTexture);

            float _BlurStrength;
            float2 _BlurCenter;
            float _maskStart;
            float _maskEnd;


            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                float2 toCenter = _BlurCenter - uv;

                float distance = length(toCenter);

                float mask = smoothstep(_maskStart,_maskEnd,distance);

                float strength = _BlurStrength * mask;

                const int SAMPLE_COUNT = 10;

                half4 result = 0;

                [unroll]
                for (int i = 0; i < SAMPLE_COUNT; i++)
                {
                    float t = (float)i / (SAMPLE_COUNT - 1);

                    float2 sampleUV = uv + toCenter * (strength * t);

                    result += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV);
                }

                return result / SAMPLE_COUNT;
            }

            ENDHLSL
        }
    }
}
