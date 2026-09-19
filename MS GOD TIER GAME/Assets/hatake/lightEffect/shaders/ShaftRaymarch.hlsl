
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Assets/hatake/lightEffect/shaders/ShaftIntervals.hlsl"


float4 _ShaftMedium, _ShaftHeightNoise, _ShaftWind, _ShaftTint, _ShaftSun, _ShaftSunColor;
float4 _ShaftSpotPosition[4], _ShaftSpotDirection[4], _ShaftSpotColor[4], _ShaftSpotShadow[4];
float _ShaftAmbient;
int _ShaftTilesEnabled, _ShaftTemporalEnabled;
Texture2D<uint2> _ShaftTileMasks;
float4 _ShaftLowSize;
int _ShaftSteps, _ShaftSpotCount, _ShaftSpotSamples, _ShaftLocalSamples;
float _ShaftJitter, _ShaftFramePhase;
int _LocalFogCount;
float4x4 _LocalFogTransforms[16];
float4 _LocalFogSettings[16], _LocalFogColors[16], _LocalFogSunBoost[16];


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


struct FogOutput { half4 fog:SV_Target0; float2 depth:SV_Target1; };

FogOutput ShaftEvaluate(Varyings input, bool detail)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv=input.texcoord;
    float depth=SampleSceneDepth(uv);
    
    
    #if !UNITY_REVERSED_Z
        depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
    #endif// UNITY_REVERSED_Z
    
    
    float3 end=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
    float3 origin=GetCameraPositionWS();
    
    if (unity_OrthoParams.w > 0.5)
    {
        #if UNITY_REVERSED_Z
            float nearDepth=1;
        #else
            float nearDepth=UNITY_NEAR_CLIP_VALUE;
        #endif// UNITY_REVERSED_Z
        origin=ComputeWorldSpacePosition(uv,nearDepth,UNITY_MATRIX_I_VP);
    }
    
    
    float3 delta=end-origin;
    float distance=min(length(delta),_ShaftMedium.z);
    float3 ray=delta/max(length(delta),0.0001);
    float baseStep=distance/max(_ShaftSteps,1);
    float jitter=frac(52.9829189*frac(dot(input.positionCS.xy,float2(0.06711056,0.00583715))));
    jitter=frac(jitter+(detail?0:_ShaftFramePhase));
    float2 spotIntervals[4];
    uint spotMask=0, fogMask=0;
    uint2 candidates=uint2((1u<<_ShaftSpotCount)-1u,(1u<<_LocalFogCount)-1u);
    
    
    
    if(_ShaftTilesEnabled!=0) candidates=_ShaftTileMasks.Load(int3(uint2(uv*_ShaftLowSize.zw)/8,0));
    [loop] for(uint remainingCandidates=candidates.x;remainingCandidates!=0;remainingCandidates &= remainingCandidates-1)
    {
        int light=firstbitlow(remainingCandidates);
        spotIntervals[light]=ShaftConeInterval(origin-_ShaftSpotPosition[light].xyz,ray,_ShaftSpotDirection[light].xyz,_ShaftSpotDirection[light].w,_ShaftSpotPosition[light].w,distance);
        
        if(spotIntervals[light].y>spotIntervals[light].x) spotMask |= 1u<<light;
    }
    
    
    float2 fogIntervals[16];
    
    
    [loop] for(uint remainingVolumes=candidates.y;remainingVolumes!=0;remainingVolumes &= remainingVolumes-1)
    {
        int volume=firstbitlow(remainingVolumes);
        fogIntervals[volume]=ShaftFogInterval(origin,ray,_LocalFogTransforms[volume],_LocalFogSettings[volume].x,distance);
        
        if(fogIntervals[volume].y>fogIntervals[volume].x) fogMask |= 1u<<volume;
    }
    
    
    float position=0;
    // 交差点ごとの精細化許容値に上限を設ける
    int budget=_ShaftSteps+countbits(spotMask)*(_ShaftSpotSamples+2)+countbits(fogMask)*(_ShaftLocalSamples+2)+4;
    float transmittance=1; float3 scatter=0; float weightedDepth=0, scatteringWeight=0;
    float sunPhase=Phase(dot(ray,_ShaftSun.xyz));
    // 均一で照明のない空間では、ビール・ランベルトの消光法則が厳密に成り立つ
    // https://www.optics-words.com/kogaku_kiso/Lambert-Beers-law.html
    // スポットライトを離れた後、何十もの空のセルを並べてはならない
    bool analyticEmpty=fogMask==0 && _ShaftHeightNoise.y<=0 && _ShaftHeightNoise.z<=0 && _ShaftAmbient<=0 && !any(_ShaftSunColor.rgb>0);
    
    
    [loop] for (int s=0;s<budget && position<distance;s++)
    {
        if(analyticEmpty)
        {
            bool insideSpot=false; float nextSpot=distance;
            [loop] for(uint remaining=spotMask;remaining!=0;remaining &= remaining-1)
            {
                int light=firstbitlow(remaining);
                insideSpot=insideSpot || (position>=spotIntervals[light].x && position<spotIntervals[light].y);
                if(spotIntervals[light].x>position) nextSpot=min(nextSpot,spotIntervals[light].x);
            }
            if(!insideSpot)
            {
                transmittance*=exp(-_ShaftMedium.x*(nextSpot-position));
                position=nextSpot; continue;
            }
        }
        
        
        float stepSize=min(baseStep,distance-position);
        
        [loop] for(uint remainingSpots=spotMask;remainingSpots!=0;remainingSpots &= remainingSpots-1)
        {
            int light=firstbitlow(remainingSpots);
            stepSize=ShaftRefineStep(position,stepSize,spotIntervals[light],_ShaftSpotSamples);
        }
        
        
        bool insideFog=false; float nextFog=distance;
        
        [loop] for(uint remainingFog=fogMask;remainingFog!=0;remainingFog &= remainingFog-1)
            {
                int volume=firstbitlow(remainingFog);
                stepSize=ShaftRefineStep(position,stepSize,fogIntervals[volume],_ShaftLocalSamples);
                insideFog=insideFog || (position>=fogIntervals[volume].x && position<fogIntervals[volume].y);
                if(fogIntervals[volume].x>position) nextFog=min(nextFog,fogIntervals[volume].x);
            }
        
        
        if(_ShaftMedium.x<=0 && !insideFog) { position=nextFog; continue; }
        
        
        stepSize=min(max(stepSize,0.00001),distance-position);
        float samplePosition=position+stepSize*(0.5+(frac(jitter+s*0.61803398875)-0.5)*_ShaftJitter);
        float3 p=origin+ray*samplePosition;
        position+=stepSize;
        float density=_ShaftMedium.x*exp(-max(0,p.y-_ShaftHeightNoise.x)*_ShaftHeightNoise.y);
        if(_ShaftHeightNoise.z>0.001 && density>0.00001)
            density*=lerp(1,0.25+1.5*Noise(p*_ShaftHeightNoise.w-_Time.y*_ShaftWind.xyz),_ShaftHeightNoise.z);
        float3 tintedDensity=density*_ShaftTint.rgb; float subtraction=0; float sunBoost=0;
        
        
        [loop] for(uint remainingFogSamples=fogMask;remainingFogSamples!=0;remainingFogSamples &= remainingFogSamples-1)
        {
            int volume=firstbitlow(remainingFogSamples);
            if(samplePosition<fogIntervals[volume].x || samplePosition>fogIntervals[volume].y) continue;
            float3 local=mul(_LocalFogTransforms[volume],float4(p,1)).xyz*2;
            float boundary=_LocalFogSettings[volume].x<0.5 ? max(abs(local.x),max(abs(local.y),abs(local.z))) : length(local);
            float regionWeight=smoothstep(0,_LocalFogSettings[volume].z,1-boundary);
            float amount=regionWeight*_LocalFogSettings[volume].y;
            sunBoost=max(sunBoost,regionWeight*_LocalFogSunBoost[volume].x);
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
                shadowCoord.w = 1; // 方向性投影は正投影、デッドブランチの分割に関する警告が出ないようにして
                shadow=MainLightRealtimeShadow(shadowCoord);
            }
            illumination+=_ShaftSunColor.rgb*shadow*sunPhase*(1+sunBoost);
        }
        
        
        [loop] for(uint remainingSpotSamples=spotMask;remainingSpotSamples!=0;remainingSpotSamples &= remainingSpotSamples-1)
        {
            int l=firstbitlow(remainingSpotSamples);
            
            
            if(samplePosition<spotIntervals[l].x || samplePosition>spotIntervals[l].y) continue;
            
            
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
        
        
        float3 contribution=transmittance*(1-extinction)*illumination*mediumTint*_ShaftMedium.y;
        
        
        if(_ShaftTemporalEnabled!=0&&!detail)
        {
            float importance=dot(contribution,float3(0.2126,0.7152,0.0722));
            weightedDepth+=importance*(-TransformWorldToView(p).z); scatteringWeight+=importance;
        }
        
        
        scatter+=contribution;
        transmittance*=extinction;
        if (transmittance<0.01) break;
    }
    
    
    FogOutput output; output.fog=half4(scatter,transmittance);
    output.depth=float2(-TransformWorldToView(end).z,scatteringWeight>0.00001?weightedDepth/scatteringWeight:-TransformWorldToView(origin+ray*distance*0.5).z); return output;
}

FogOutput Frag(Varyings input) { return ShaftEvaluate(input, false); }
