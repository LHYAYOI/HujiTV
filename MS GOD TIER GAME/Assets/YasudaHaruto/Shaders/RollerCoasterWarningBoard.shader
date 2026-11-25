Shader "YasudaHaruto/RollerCoaster/Warning Board"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; float paint:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv;
                // 進行側の裏面と破断面は木地を見せ、回転する破片を木の板として読めるようにする。
                output.paint=saturate(-input.normalOS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float border=step(.82,length(float2(input.uv.x/1.2,(input.uv.y-1.275)/.825)));
                float stripe=step(.5,frac((input.uv.x+input.uv.y)*2.7));
                float grain=.88+.12*sin(input.uv.x*65+sin(input.uv.y*11));
                half3 wood=half3(.43,.24,.09)*grain;
                half3 yellow=lerp(half3(1,.72,.025),half3(.025,.02,.015),border*stripe);
                half3 color=lerp(wood,yellow,step(.5,input.paint));
                Light light=GetMainLight();
                float lighting=.55+.45*abs(dot(normalize(input.normalWS),light.direction));
                return half4(color*lighting,1);
            }
            ENDHLSL
        }
    }
}
