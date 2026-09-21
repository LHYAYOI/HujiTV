
#include "Assets/hatake/lightEffect/shaders/ShaftRaymarch.hlsl"
#include "Assets/hatake/lightEffect/shaders/ShaftTileDebug.hlsl"


TEXTURE2D_X(_ShaftFog);
TEXTURE2D_X(_ShaftFogDepth);
TEXTURE2D_X(_ShaftGodRay);


float4 _GodRaySun,_GodRayColor,_GodRayTexelSize;
int _ShaftUseFog,_ShaftUseGodRay;
int _ShaftRefineRoots, _ShaftRefineEdges;
float4 _ShaftDetailRegions[4];


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
        
        float detailWeight=0;
        
        if(_ShaftRefineRoots!=0)
            [loop] for(int light=0;light<_ShaftSpotCount;light++)
            {
                float4 region=_ShaftDetailRegions[light];
                float2 edge=min(uv-region.xy,region.zw-uv)*_ScreenParams.xy;
                detailWeight=max(detailWeight,saturate(min(edge.x,edge.y)*0.5));
            }
        
        if(_ShaftRefineEdges!=0 && nearest>max(0.1,center*0.01)) detailWeight=1;
        if(detailWeight>0) fog=lerp(fog,ShaftEvaluate(input,true).fog,detailWeight);
    }
    
    
    float3 color=source.rgb*fog.a+fog.rgb;
    
    
    if(_ShaftUseGodRay!=0)
    {
        float rays=SAMPLE_TEXTURE2D_X(_ShaftGodRay,sampler_LinearClamp,uv).r;
        color+=rays*_GodRayColor.rgb*_GodRaySun.w;
    }
    
    
    if(_ShaftTileDebugMode!=0 && _ShaftTilesEnabled!=0 && _ShaftUseFog!=0)color=ShaftDebugOverlay(color,uv);
    
    
    return half4(color,source.a);
}
