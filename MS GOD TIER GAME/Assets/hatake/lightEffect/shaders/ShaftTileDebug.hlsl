
int _ShaftTileDebugMode;
float _ShaftTileDebugOpacity;
float4 _ShaftDebugSize;


// 行優先、最下位ビットが左上に配置
uint ShaftDigit(uint digit)
{
    const uint glyphs[10]={31599u,29850u,29671u,31207u,18925u,31183u,31695u,18727u,31727u,31215u};
    return glyphs[min(digit,9u)];
}


float3 ShaftDebugOverlay(float3 color,float2 uv)
{
    uint2 tile=uint2(uv*_ShaftLowSize.zw)/8;
    uint2 mask=_ShaftTileMasks.Load(int3(tile,0));
    uint spots=countbits(mask.x), fog=countbits(mask.y);
    uint count=_ShaftTileDebugMode==2?spots:(_ShaftTileDebugMode==3?fog:spots+fog);
    float3 heat=count==0?float3(.035,.035,.05):count==1?float3(.45,.08,.8):count==2?float3(.05,.65,.95):count==3?float3(.05,.8,.4):count<=5?float3(.9,.85,.05):count<=9?float3(1,.4,.03):float3(.95,.05,.12);
    
    
    color=lerp(color,heat,_ShaftTileDebugOpacity);
    
    
    float2 cellSize=8*_ShaftLowSize.xy*_ShaftDebugSize.xy;
    float2 local=(uv*_ShaftLowSize.zw-float2(tile)*8)/8*cellSize;
    float edge=min(min(local.x,local.y),min(cellSize.x-local.x,cellSize.y-local.y));
    
    
    color=lerp(color,float3(.85,.85,.85),edge<.75?.35:0);
    //  ネイティブ解像度では、タイルの幅は8pxです、ごく小さなラベルは省略してください
    
        
    if(min(cellSize.x,cellSize.y)>=12)
    {
        float scale=max(1,floor(min(cellSize.x,cellSize.y)/8));
        float2 glyphPixel=floor((local-(cellSize-float2(7,5)*scale)*.5)/scale+.001);
        bool tens=count>=10;
        
        
        if(!tens)glyphPixel.x-=2;
        
        
        int digitIndex=tens&&glyphPixel.x>=4?1:0;
        int2 p=int2(glyphPixel)-int2(digitIndex*4,0);
        
        
        if(all(p>=0)&&p.x<3&&p.y<5)
        {
            // Blit UVは上向きに伸び、グリフの行は上から始まります
            uint digit=tens?(digitIndex==0?count/10:count%10):count;
            bool ink=(ShaftDigit(digit)&(1u<<((4-p.y)*3+p.x)))!=0;
            color=ink?float3(1,1,1):color*.65;
        }
    }
    return color;
}
