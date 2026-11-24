Shader "YasudaHaruto/RollerCoaster Obstacle"
{
    Properties
    {
        _BaseMap("Surface",2D)="white"{}
        _BaseColor("Color",Color)=(.6,.48,.34,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Cull Off ZWrite On
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            V Vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv*.15;return o;}
            half4 Frag(V i):SV_Target
            {
                half3 surfaceColor=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                return half4((.5+.5*surfaceColor)*_BaseColor.rgb,1);
            }
            ENDHLSL
        }
    }
}
