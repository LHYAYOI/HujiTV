
#include "Assets/hatake/lightEffect/shaders/ShaftRaymarch.hlsl"
#include "Assets/hatake/lightEffect/shaders/ShaftTileDebug.hlsl"


TEXTURE2D_X(_ShaftFog);
TEXTURE2D_X(_ShaftFogDepth);
TEXTURE2D_X(_ShaftGodRay);


float4 _GodRaySun,_GodRayColor,_GodRayTexelSize;
int _ShaftUseFog,_ShaftUseGodRay;
int _ShaftRefineRoots, _ShaftRefineEdges;
float4 _ShaftDetailRegions[4];


// 画面の深度（線形）
float ShaftCenterDepth(float2 uv)
{
    float raw=SampleSceneDepth(uv);
    return unity_OrthoParams.w>0.5 ? LinearDepthToEyeDepth(raw):LinearEyeDepth(raw,_ZBufferParams);
}


// 低解像度の霧を、深度の近い 2x2 サンプルで重み付けしてフル解像度へ広げる。
// nearest には 4 サンプルのうち最も近い深度差を返す（精細化の判定に使う）
float4 ShaftUpsampleFog(float2 uv, float center, out float nearest)
{
    float2 pixel=uv*_ShaftLowSize.zw-0.5, fraction=frac(pixel);
    float4 sum=0, nearestFog=0; float total=0;
    nearest=1e20;
    
    
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
    
    return total>0.0001 ? sum/total:nearestFog;
}


// フル解像度で計算し直す割合（0〜1）。スポットの根元の領域と、対応する低解像度サンプルがない深度の境界
float ShaftDetailWeight(float2 uv, float center, float nearest)
{
    float detailWeight=0;
    
    if(_ShaftRefineRoots!=0)
    {
        [loop] for(int light=0;light<min(_ShaftSpotCount,4);light++)
        {
            float4 region=_ShaftDetailRegions[light];
            float2 edge=min(uv-region.xy,region.zw-uv)*_ScaledScreenParams.xy;
            detailWeight=max(detailWeight,saturate(min(edge.x,edge.y)*0.5));
        }
    }
    
    if(_ShaftRefineEdges!=0 && nearest>max(0.1,center*0.01)) detailWeight=1;
    return detailWeight;
}


// 霧・ゴッドレイ・デバッグ表示を元の色へ重ねる
float3 ShaftApply(float3 source, float4 fog, float2 uv)
{
    float3 color=source*fog.a+fog.rgb;
    
    
    if(_ShaftUseGodRay!=0)
    {
        float rays=SAMPLE_TEXTURE2D_X(_ShaftGodRay,sampler_LinearClamp,uv).r;
        color+=rays*_GodRayColor.rgb*_GodRaySun.w;
    }
    
    
    if(_ShaftTileDebugMode!=0 && _ShaftTilesEnabled!=0 && _ShaftUseFog!=0)color=ShaftDebugOverlay(color,uv);
    return color;
}


half4 Composite(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv=input.texcoord;
    half4 source=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
    float4 fog=float4(0,0,0,1);
    
    
    if(_ShaftUseFog!=0)
    {
        float nearest;
        fog=ShaftUpsampleFog(uv,ShaftCenterDepth(uv),nearest);
    }
    
    
    return half4(ShaftApply(source.rgb,fog,uv),source.a);
}


// 精細化パス。合成の結果の上に SrcAlpha / OneMinusSrcAlpha で重ねるので、
// 出力 = lerp(合成の色, 精細な霧で求めた色, 重み) となり、元の 1 パスの式と同じになる。
// 重みが 0 の画素はすぐ捨てるので、重いレイマーチは境界と根元の画素だけで動く。
half4 Refine(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv=input.texcoord;
    float center=ShaftCenterDepth(uv);
    float nearest=1e20;
    
    
    // 根元の判定だけで決まる画素は、深度差を調べる必要がない
    if(_ShaftRefineEdges!=0)
    {
        float2 pixel=uv*_ShaftLowSize.zw-0.5;
        [unroll] for(int y=0;y<2;y++) [unroll] for(int x=0;x<2;x++)
        {
            float2 sampleUV=(floor(pixel)+float2(x,y)+0.5)*_ShaftLowSize.xy;
            nearest=min(nearest,abs(SAMPLE_TEXTURE2D_X(_ShaftFogDepth,sampler_PointClamp,sampleUV).r-center));
        }
    }
    
    
    float detailWeight=ShaftDetailWeight(uv,center,nearest);
    if(detailWeight<=0) discard;
    
    
    half4 source=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
    float4 fog=ShaftEvaluate(input,true).fog;
    return half4(ShaftApply(source.rgb,fog,uv),detailWeight);
}
