Shader "Hidden/Hatakeyama/PainterlyPost"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Painterly"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ U_KUWAHARA_ON
            #pragma multi_compile_local _ U_STROKE_ON
            #pragma multi_compile_local _ U_CANVAS_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // xy: 1ピクセルのUV幅、z: 1080p基準の画素倍率、w: 元画像との合成率
            float4 _PainterlyTexel;

            float3 PainterlySource(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
            }

            #include "Assets/hatake/Shaders/Painterly.hlsl"

            float4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                float3 painted = PainterlyShade(uv, _PainterlyTexel.xy, _PainterlyTexel.z);
                float3 source = PainterlySource(uv);
                return float4(lerp(source, painted, _PainterlyTexel.w), 1);
            }
            ENDHLSL
        }
    }
}
