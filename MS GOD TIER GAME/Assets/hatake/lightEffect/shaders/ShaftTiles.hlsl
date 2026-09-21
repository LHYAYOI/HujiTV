
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"


float4 _ShaftSpotRects[4], _ShaftFogRects[16], _ShaftLowSize;
int _ShaftSpotCount, _LocalFogCount;


bool Overlap(float4 region,float2 lo,float2 hi)
{
    // Blit UV とカメラのビューポートは、同じ正規化された向きを使用
    return all(region.zw>=lo)&&all(region.xy<=hi);
}


uint2 Tiles(Varyings input):SV_Target
{
    float2 tile=floor(input.positionCS.xy);
    float2 lo=(tile*8-1)*_ShaftLowSize.xy,hi=((tile+1)*8+1)*_ShaftLowSize.xy;
    uint2 mask=0;
    [loop]for(int i=0;i<_ShaftSpotCount;i++)if(Overlap(_ShaftSpotRects[i],lo,hi))mask.x|=1u<<i;
    [loop]for(int j=0;j<_LocalFogCount;j++)if(Overlap(_ShaftFogRects[j],lo,hi))mask.y|=1u<<j;
    return mask;
}
