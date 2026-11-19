Shader "YasudaHaruto/RollerCoaster Enclosure"
{
    Properties
    {
        _BaseMap("Pattern Atlas", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1,1,1,1)
        _AtlasRect("Atlas Region (XY / Width Height)", Vector) = (0,0,1,1)
        _TileMeters("Pattern Repeat Height (m)", Float) = 3
        _ScrollVelocity("UV Scroll (cycles/sec)", Vector) = (0.012,0.004,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Off ZWrite On
        Pass
        {
            Name "Enclosure"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _AtlasRect;
                float4 _ScrollVelocity;
                float _TileMeters;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; float3 positionWS:TEXCOORD2; float fog:TEXCOORD3; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.fog=ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 period=max(0.1,_TileMeters)*float2(_AtlasRect.z/max(0.001,_AtlasRect.w),1);
                float2 phase=1-abs(frac((input.uv/period+_Time.y*_ScrollVelocity.xy)*0.5)*2-1);
                // Scrollを色領域の中で折り返し、隣のAtlas領域へ流れ出さない。
                float2 atlasUV=_AtlasRect.xy+lerp(0.003,1-0.003,phase)*_AtlasRect.zw;
                half3 pattern=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,atlasUV).rgb*_BaseColor.rgb;
                half shade=0.8+0.2*saturate(dot(normalize(input.normalWS),normalize(float3(0.3,0.8,-0.4))));
                float3 normal=normalize(input.normalWS);
                half3 illumination=SampleSH(normal);
                Light main=GetMainLight();
                illumination+=main.color*(.25+.75*abs(dot(normal,main.direction)))*main.distanceAttenuation;
                uint count=GetAdditionalLightsCount();
                InputData inputData=(InputData)0;
                inputData.positionWS=input.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                LIGHT_LOOP_BEGIN(count)
                    Light light=GetAdditionalLight(lightIndex,input.positionWS);
                    illumination+=light.color*light.distanceAttenuation*(.3+.7*abs(dot(normal,light.direction)));
                LIGHT_LOOP_END
                return half4(MixFog(pattern*shade*illumination,input.fog),1);
            }
            ENDHLSL
        }
    }
}
