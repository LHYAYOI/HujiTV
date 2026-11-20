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

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _BlurStrength;
            float2 _BlurCenter;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // ブラー中心から現在のピクセルへの方向
                float2 toCenter = _BlurCenter - uv;

                const int SAMPLE_COUNT = 8;

                half4 result = 0;

                [unroll]
                for (int i = 0; i < SAMPLE_COUNT; i++)
                {
                    float t = (float)i / (SAMPLE_COUNT - 1);

                    // 中心方向にサンプリング位置を移動
                    float2 sampleUV =
                        uv + toCenter * (_BlurStrength * t);

                    result += SAMPLE_TEXTURE2D_X(
                        _BlitTexture,
                        sampler_LinearClamp,
                        sampleUV
                    );
                }

                return result / SAMPLE_COUNT;
            }

            ENDHLSL
        }
    }
}
