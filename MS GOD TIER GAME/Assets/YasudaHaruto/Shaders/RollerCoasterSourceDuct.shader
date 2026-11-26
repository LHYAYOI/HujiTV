Shader "YasudaHaruto/RollerCoaster Source Duct"
{
    Properties
    {
        _BaseMap("Duct Color",2D)="white"{}
        _BumpMap("Duct Normal",2D)="bump"{}
        _MetallicGlossMap("Duct Metalness",2D)="black"{}
        _RoughnessMap("Duct Roughness",2D)="white"{}
        _AtlasRect("Source Wall UV Region",Vector)=(0,0,.4515,1)
        _TileMeters("Source Length (m)",Float)=6.8
        _BumpScale("Normal Strength",Float)=1
        _BaseColor("Tint",Color)=(1,1,1,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull Off ZWrite On
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap);SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_RoughnessMap);SAMPLER(sampler_RoughnessMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _AtlasRect,_BaseColor;
                float _TileMeters,_BumpScale;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;float fog:TEXCOORD4;};
            Varyings Vert(Attributes input)
            {
                Varyings o;o.positionWS=TransformObjectToWorld(input.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(input.normalOS);o.tangentWS=float4(TransformObjectToWorldDir(input.tangentOS.xyz),input.tangentOS.w*GetOddNegativeScale());
                o.uv=input.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 tile=input.uv/max(.1,_TileMeters);
                float direction=frac(tile.x*.5)<.5?1:-1;
                float2 phase=float2(1-abs(frac(tile.x*.5)*2-1),frac(tile.y));
                // 周方向はTextureのRepeatで端同士を補間し、底面のUVシームを出さない。
                float2 uv=_AtlasRect.xy+float2(lerp(.001,.999,phase.x),phase.y)*_AtlasRect.zw;
                half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv),_BumpScale);normalTS.x*=direction;
                float3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 n=normalize(input.normalWS),t=normalize(input.tangentWS.xyz);if(dot(n,view)<0)n=-n;t=normalize(t-n*dot(t,n));
                float3 b=cross(n,t)*input.tangentWS.w;
                InputData data=(InputData)0;data.positionWS=input.positionWS;data.normalWS=normalize(t*normalTS.x+b*normalTS.y+n*normalTS.z);
                data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);data.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                data.bakedGI=SampleSH(data.normalWS);data.shadowMask=half4(1,1,1,1);data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                SurfaceData surface=(SurfaceData)0;surface.albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb*_BaseColor.rgb;
                surface.metallic=SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,uv).r;
                surface.smoothness=clamp(1-SAMPLE_TEXTURE2D(_RoughnessMap,sampler_RoughnessMap,uv).r,.05,.45);
                surface.normalTS=normalTS;surface.occlusion=1;surface.alpha=1;
                half4 color=UniversalFragmentPBR(data,surface);return half4(MixFog(color.rgb,input.fog),1);
            }
            ENDHLSL
        }
    }
}
