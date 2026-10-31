
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
            // ソフトシャドウのキーワード (_SHADOWS_SOFT*) はあえて宣言しない。
            // 1 ステップごとに High (16 タップ) のフィルタをかけるのは重く、
            // 光線方向の積分とサンプル位置のゆらぎで縁は十分ぼける。ハードウェア PCF の 1 タップで引く。


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
            // フル解像度の全画素で動くので、レイマーチは含めない（精細化は下の "Light Shaft Detail Refine" パス）


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
            // God Ray を遮るもの（LightMode "GodRayOccluder" のパスを持つ半透明など）。深度に入らないものの分。
            TEXTURE2D_X(_GodRayOccluder);
            float _GodRayOccluderEnabled;
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
                if (_GodRayOccluderEnabled>0.5) sky*=1-saturate(LOAD_TEXTURE2D_X(_GodRayOccluder,uint2(input.positionCS.xy)).r);
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
        Pass
        {
            // 深度の境界・スポットの根元だけフル解像度でレイマーチし、合成結果へ重みで混ぜる。
            // 色は lerp(合成, 精細, w) と同じになる。アルファ (元の source.a) は書き換えない
            Name "Light Shaft Detail Refine"
            ZWrite Off ZTest Always Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, Zero One
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Refine
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


            #include "Assets/hatake/lightEffect/shaders/ShaftComposite.hlsl"
            ENDHLSL
        }
    }
}
