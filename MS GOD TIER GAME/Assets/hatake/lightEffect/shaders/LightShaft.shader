
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


            float4 _ShaftMedium, _ShaftHeightNoise, _ShaftWind, _ShaftTint, _ShaftSun, _ShaftSunColor;
            float4 _ShaftSpotPosition[4], _ShaftSpotDirection[4], _ShaftSpotColor[4], _ShaftSpotShadow[4];
            float _ShaftAmbient;
            int _ShaftSteps, _ShaftSpotCount;
            int _LocalFogCount;
            float4x4 _LocalFogTransforms[16];
            float4 _LocalFogSettings[16], _LocalFogColors[16];


            float Hash(float3 p) { p=frac(p*0.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z); }

            float Noise(float3 p)
            {
                float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(i),Hash(i+float3(1,0,0)),f.x),lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),lerp(Hash(i+float3(0,1,1)),Hash(i+1),f.x),f.y),f.z);
            }

            float Phase(float cosine)
            {
                float g=_ShaftMedium.w;
                float inverse=rsqrt(max(0.05,1+g*g-2*g*cosine));
                return (1-g*g)*inverse*inverse*inverse;
            }



            struct FogOutput {
                half4 fog:SV_Target0;
                float depth:SV_Target1;
            };

            FogOutput Frag(Varyings input)
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                float depth=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 end=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                float3 origin=GetCameraPositionWS();

                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                        float nearDepth=1;
                    #else
                        float nearDepth=UNITY_NEAR_CLIP_VALUE;
                    #endif
                    origin=ComputeWorldSpacePosition(uv,nearDepth,UNITY_MATRIX_I_VP);
                }

                float3 delta=end-origin;
                float distance=min(length(delta),_ShaftMedium.z);
                float3 ray=delta/max(length(delta),0.0001);
                float stepSize=distance/max(_ShaftSteps,1);
                float jitter=Hash(float3(input.positionCS.xy,0));
                float transmittance=1; float3 scatter=0;
                float sunPhase=Phase(dot(ray,_ShaftSun.xyz));


                [loop] for (int s=0;s<_ShaftSteps;s++)
                {
                    float3 p=origin+ray*((s+jitter)*stepSize);
                    float density=_ShaftMedium.x*exp(-max(0,p.y-_ShaftHeightNoise.x)*_ShaftHeightNoise.y);
                    if(_ShaftHeightNoise.z>0.001 && density>0.00001)
                        density*=lerp(1,0.25+1.5*Noise(p*_ShaftHeightNoise.w-_Time.y*_ShaftWind.xyz),_ShaftHeightNoise.z);
                    float3 tintedDensity=density*_ShaftTint.rgb; float subtraction=0;


                    [loop] for(int volume=0;volume<_LocalFogCount;volume++)
                    {
                        float3 local=mul(_LocalFogTransforms[volume],float4(p,1)).xyz*2;
                        float boundary=_LocalFogSettings[volume].x<0.5 ? max(abs(local.x),max(abs(local.y),abs(local.z))) : length(local);
                        float amount=smoothstep(0,_LocalFogSettings[volume].z,1-boundary)*_LocalFogSettings[volume].y;
                        if(_LocalFogSettings[volume].w>0.5) subtraction+=amount;
                        else { density+=amount; tintedDensity+=amount*_LocalFogColors[volume].rgb; }
                    }


                    float3 mediumTint=tintedDensity/max(density,0.00001);
                    density=max(0,density-subtraction);
                    if(density<0.00001) continue;
                    float extinction=exp(-density*stepSize);
                    float3 illumination=_ShaftAmbient.xxx;


                    if (any(_ShaftSunColor.rgb > 0))
                    {
                        float shadow=1;
                        if (_ShaftSun.w>0.5)
                        {
                            float4 shadowCoord=TransformWorldToShadowCoord(p);
                            shadowCoord.w=1; 
                            shadow=MainLightRealtimeShadow(shadowCoord);
                        }
                        illumination+=_ShaftSunColor.rgb*shadow*sunPhase;
                    }


                    [loop] for(int l=0;l<_ShaftSpotCount;l++)
                    {
                        float3 fromLight=p-_ShaftSpotPosition[l].xyz;
                        float d2=dot(fromLight,fromLight);
                        if(d2>=_ShaftSpotPosition[l].w*_ShaftSpotPosition[l].w) continue;
                        float3 direction=fromLight*rsqrt(max(d2,0.000001));
                        float cone=saturate((dot(direction,_ShaftSpotDirection[l].xyz)-_ShaftSpotDirection[l].w)*_ShaftSpotShadow[l].w);
                        float normalizedD2=d2*_ShaftSpotShadow[l].z;
                        float range=saturate(1-normalizedD2*normalizedD2);
                        if (cone*range>0.001)
                        {
                            float shadow=1;
                            if (_ShaftSpotShadow[l].y>0.5) shadow=AdditionalLightRealtimeShadow((int)_ShaftSpotShadow[l].x,p,-direction);
                            illumination+=_ShaftSpotColor[l].rgb*(cone*cone*range*range/max(1,d2))*shadow*Phase(dot(ray,-direction));
                        }
                    }
                    scatter+=transmittance*(1-extinction)*illumination*mediumTint*_ShaftMedium.y;
                    transmittance*=extinction;
                    if (transmittance<0.01) break;
                }
                FogOutput output; output.fog=half4(scatter,transmittance);
                output.depth=-TransformWorldToView(end).z; return output;
            }
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


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


            TEXTURE2D_X(_ShaftFog);
            TEXTURE2D_X(_ShaftFogDepth);
            TEXTURE2D_X(_ShaftGodRay);
            float4 _ShaftLowSize,_GodRaySun,_GodRayColor,_GodRayTexelSize;
            int _ShaftUseFog,_ShaftUseGodRay;


            half4 Composite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                half4 source=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
                float4 fog=float4(0,0,0,1);

                if(_ShaftUseFog!=0)
                {
                    float raw=SampleSceneDepth(uv);
                    float center=unity_OrthoParams.w>0.5 ? LinearDepthToEyeDepth(raw):LinearEyeDepth(raw,_ZBufferParams);
                    float2 pixel=uv*_ShaftLowSize.zw-0.5, fraction=frac(pixel);
                    float4 sum=0, nearestFog=0; float total=0, nearest=1e20;

                    [unroll] for(int y=0;y<2;y++) [unroll] for(int x=0;x<2;x++)
                    {
                        float2 sampleUV=(floor(pixel)+float2(x,y)+0.5)*_ShaftLowSize.xy;
                        float depth=SAMPLE_TEXTURE2D_X(_ShaftFogDepth,sampler_PointClamp,sampleUV).r;
                        float difference=abs(depth-center);
                        float weight=exp(-difference/max(0.05,center*0.005))*(x==0 ? 1-fraction.x:fraction.x)*(y==0 ? 1-fraction.y:fraction.y);
                        float4 value=SAMPLE_TEXTURE2D_X(_ShaftFog,sampler_PointClamp,sampleUV);
                        if(difference<nearest) { nearest=difference; nearestFog=value; }
                        sum+=value*weight; total+=weight;
                    }

                    
                    fog=total>0.0001 ? sum/total:nearestFog;
                }

                float3 color=source.rgb*fog.a+fog.rgb;

                if(_ShaftUseGodRay!=0)
                {
                    float rays=SAMPLE_TEXTURE2D_X(_ShaftGodRay,sampler_LinearClamp,uv).r;
                    color+=rays*_GodRayColor.rgb*_GodRaySun.w;
                }
                return half4(color,source.a);
            }
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


            float4 _GodRaySun;
            half4 Mask(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float depth=SampleSceneDepth(input.texcoord);
                #if UNITY_REVERSED_Z
                    float sky=step(depth,0.00001);
                #else
                    float sky=step(0.99999,depth);
                #endif
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


                [loop] for(int i=0;i<_GodRaySamples;i++)
                {
                    float2 uv=origin+delta*(i+frac(jitter+i*0.61803398875));
                    if(all(uv>=0) && all(uv<=1)) result+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv).r*weight;
                    total+=weight; weight*=0.96;
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
    }
}
