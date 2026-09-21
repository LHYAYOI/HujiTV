
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


TEXTURE2D_X(_ShaftFogDepth);
TEXTURE2D(_ShaftHistoryColor);
TEXTURE2D(_ShaftHistoryDepth);


float4x4 _ShaftPreviousVP, _ShaftPreviousView;
float4 _ShaftLowSize;
float _ShaftHistoryWeight;


// Blit で使用される正規化されたビューポートに一致する、カメラ投影を保存します
// GetGPUProjectionMatrix() は、グラフの記録中に互換性レンダラーのターゲット状態に依存します
float2 PreviousViewport(float3 world)
{
    float4 clip=mul(_ShaftPreviousVP,float4(world,1));
    return clip.xy/clip.w*.5+.5;
}


struct TemporalOutput { half4 fog:SV_Target0; float2 depth:SV_Target1; };


TemporalOutput Temporal(Varyings input)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    
    float2 uv=input.texcoord;
    TemporalOutput o;
    o.fog=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv);
    o.depth=SAMPLE_TEXTURE2D_X(_ShaftFogDepth,sampler_PointClamp,uv).rg;
    
    
    if(_ShaftHistoryWeight<=0) return o;
    
    float rawDepth=SampleSceneDepth(uv);
    
    
    
    #if !UNITY_REVERSED_Z
        rawDepth=lerp(UNITY_NEAR_CLIP_VALUE,1,rawDepth);
    #endif // !UNITY_REVERSED_Z
    
    
    
    float3 end=ComputeWorldSpacePosition(uv,rawDepth,UNITY_MATRIX_I_VP);
    float3 origin=GetCameraPositionWS();
    
    
    if(unity_OrthoParams.w>0.5)
    {
        #if UNITY_REVERSED_Z
            float nearDepth=1;
        #else
            float nearDepth=UNITY_NEAR_CLIP_VALUE;
        #endif// UNITY_REVERSED_Z
        origin=ComputeWorldSpacePosition(uv,nearDepth,UNITY_MATRIX_I_VP);
    }
    
    
    float originDepth=-TransformWorldToView(origin).z;
    float3 anchor=lerp(origin,end,saturate((o.depth.y-originDepth)/max(0.0001,o.depth.x-originDepth)));
    float2 previousUV=PreviousViewport(anchor);
    float2 geometryUV=PreviousViewport(end);
    
    
    if(any(previousUV<_ShaftLowSize.xy*.5)||any(previousUV>1-_ShaftLowSize.xy*.5)||any(geometryUV<0)||any(geometryUV>1)) return o;
    
    
    float expected=-mul(_ShaftPreviousView,float4(end,1)).z;
    float anchorDepth=-mul(_ShaftPreviousView,float4(anchor,1)).z;
    
        
    if(expected<=0 || anchorDepth<=0) return o;
    
    
    float geometryDepth=SAMPLE_TEXTURE2D(_ShaftHistoryDepth,sampler_PointClamp,geometryUV).r;
    float2 historyDepth=SAMPLE_TEXTURE2D(_ShaftHistoryDepth,sampler_PointClamp,previousUV).rg;
    
    
    if(abs(geometryDepth-expected)>max(.15,expected*.015) || abs(historyDepth.y-anchorDepth)>max(.25,anchorDepth*.05)) return o;
    
    
    half4 lo=o.fog, hi=o.fog;
    
    
    [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
    {
        half4 sample=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv+float2(x,y)*_ShaftLowSize.xy);
        lo=min(lo,sample); hi=max(hi,sample);
    }
    
    
    half4 history=SAMPLE_TEXTURE2D(_ShaftHistoryColor,sampler_LinearClamp,previousUV);
    o.fog=lerp(o.fog,clamp(history,lo,hi),_ShaftHistoryWeight);
    
    return o;
}
