
Shader "Hidden/hatake/Volumetric"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "VolumetricLightShaft"
            ZWrite Off ZTest Always Cull Off


            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"


            #include "Assets/hatake/lightEffect/shaders/ShaftRaymarch.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Depth Aware Composite"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Composite
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


            #include "Assets/hatake/lightEffect/shaders/ShaftComposite.hlsl"
            ENDHLSL
        }


        Pass
        {
            Name "GodRaySkyMask"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Mask


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


            float4 _GodRaySun, _GodRayTexelSize;
            half4 Mask(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float sky=0;
                [unroll] for(int y=0;y<2;y++) [unroll] for(int x=0;x<2;x++)
                {
                    float depth=SampleSceneDepth(input.texcoord+(float2(x,y)-0.5)*0.5*_GodRayTexelSize.xy);
                    #if UNITY_REVERSED_Z
                        sky+=step(depth,0.00001);
                    #else
                        sky+=step(0.99999,depth);
                    #endif
                }
                sky*=0.25;
                float2 offset=input.texcoord-_GodRaySun.xy; offset.x*=_ScreenParams.x/_ScreenParams.y;
                return sky*exp(-dot(offset,offset)/max(0.0001,_GodRaySun.z*_GodRaySun.z));
            }
            ENDHLSL
        }


        Pass
        {
            Name "God Ray Radial Integration"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM

            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Radial

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _GodRaySun; int _GodRaySamples;
            half4 Radial(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 origin=input.texcoord, delta=(_GodRaySun.xy-origin)*0.95/max(_GodRaySamples,1);
                float jitter=frac(52.9829189*frac(dot(input.positionCS.xy,float2(0.06711056,0.00583715))));
                float result=0, weight=1, total=0;

                // 設定でサンプル数を増減させても、ゴッドレイの長さや見た目の濃さが変わらないように、1サンプルあたりの光の減衰率（decay）を自動調整してしまいましょう
                float decay=exp2(-1.8846/max(_GodRaySamples,1)); // 0.96^32


                [loop] for(int i=0;i<_GodRaySamples;i++)
                {
                    float2 uv=origin+delta*(i+frac(jitter+i*0.61803398875));
                    if(all(uv>=0) && all(uv<=1)) result+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv).r*weight;
                    total+=weight; weight*=decay;
                }
                return result/max(total,0.0001);
            }
            ENDHLSL
        }


        Pass
        {
            Name "God Ray Low Resolution Filter"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM


            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Filter


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _GodRayTexelSize;
            half4 Filter(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float sum=0;
                [unroll] for(int y=0;y<2;y++) [unroll] for(int x=0;x<2;x++)
                    sum+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,input.texcoord+(float2(x,y)-0.5)*_GodRayTexelSize.xy).r;
                return sum*0.25;
            }

            ENDHLSL
        }
        Pass
        {
            Name "Light Shaft Temporal"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Temporal
            #include "Assets/hatake/lightEffect/shaders/ShaftTemporal.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Light Shaft Tile Candidates"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Tiles
            #include "Assets/hatake/lightEffect/shaders/ShaftTiles.hlsl"
            ENDHLSL
        }
    }
}
