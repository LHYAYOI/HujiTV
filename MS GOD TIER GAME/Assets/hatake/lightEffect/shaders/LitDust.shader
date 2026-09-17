

Shader "custom/hatake/LitDust"
{
    Properties { 
        _Color("Tint",Color)=(1,0.9,0.75,0.35) 
        _Brightness("Brightness",Range(0,8))=2 
        _SoftDistance("Soft Intersection",Float)=0.15 
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One ZWrite Off Cull Off
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

            CBUFFER_START(UnityPerMaterial)
            
            float4 _Color; float _Brightness,_SoftDistance;
            CBUFFER_END
            
            struct A { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; float4 color:COLOR; };
            
            V Vert(A v) { V o; o.positionWS=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.uv=v.uv; o.color=v.color; return o; }
            
            
            half4 Frag(V i):SV_Target
            {
                float2 screenUV=GetNormalizedScreenSpaceUV(i.positionCS);
                float scene=LinearEyeDepth(SampleSceneDepth(screenUV),_ZBufferParams);
                float particle=-TransformWorldToView(i.positionWS).z;
                float alpha=pow(saturate(1-length(i.uv*2-1)),2)*i.color.a*_Color.a*saturate((scene-particle)/max(_SoftDistance,0.001));
                Light main=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 lighting=main.color*main.shadowAttenuation;
                InputData inputData=(InputData)0; inputData.positionWS=i.positionWS; inputData.normalizedScreenSpaceUV=screenUV;
                uint count=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light light=GetAdditionalLight(lightIndex,i.positionWS,half4(1,1,1,1));
                    lighting+=light.color*light.distanceAttenuation*light.shadowAttenuation;
                LIGHT_LOOP_END
                return half4(lighting*_Color.rgb*i.color.rgb*_Brightness,alpha);
            }
            ENDHLSL
        }
    }
}
