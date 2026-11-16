Shader "YasudaHaruto/Rune Cast Composite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 10
        _Premultiply ("Premultiply RGB", Float) = 0
        _BookTex ("Book Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend [_SrcBlend] [_DstBlend]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local_fragment _ RUNE_BOOK_OVERLAY RUNE_VIGNETTE
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Premultiply;
            sampler2D _BookTex;
            float4 _BookUV;
            float4 _Vignette;

            struct Vertex
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            Varyings vert(Vertex input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                #if defined(RUNE_VIGNETTE)
                    float2 offset = (input.uv - _Vignette.xy) / _Vignette.zw;
                    float edge = smoothstep(0.12, 1.05, length(offset));
                    return fixed4(0, 0, 0, saturate(input.color.a * lerp(0.78, 1.12, edge)));
                #endif
                fixed4 sampleColor = tex2D(_MainTex, input.uv);
                #if defined(RUNE_BOOK_OVERLAY)
                    float3 paper = tex2D(_BookTex, _BookUV.xy + input.uv * _BookUV.zw).rgb;
                    float3 glow = sampleColor.rgb;
                    #if !defined(UNITY_COLORSPACE_GAMMA)
                        paper = LinearToGammaSpace(paper);
                        glow = LinearToGammaSpace(glow);
                    #endif
                    // Leave some local shading under the glow, then apply the Overlay blend.
                    paper = saturate(paper) * 0.68;
                    float3 low = 2 * paper * glow;
                    float3 high = 1 - 2 * (1 - paper) * (1 - glow);
                    float3 composite = lerp(low, high, step(0.5, paper));
                    #if !defined(UNITY_COLORSPACE_GAMMA)
                        composite = GammaToLinearSpace(composite);
                    #endif
                    sampleColor.rgb = composite;
                #endif
                sampleColor.rgb *= input.color.rgb * input.color.a * lerp(1, sampleColor.a, _Premultiply);
                sampleColor.a *= input.color.a;
                return sampleColor;
            }
            ENDCG
        }
    }
}
