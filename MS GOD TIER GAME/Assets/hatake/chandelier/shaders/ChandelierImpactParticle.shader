Shader "hatake/Chandelier/Impact Particle"
{
    // シャンデリアの衝突演出の粒（URP、ライティング無し）。形は板の UV から手続きで描くので、テクスチャは要らない
    //   Soft  ：柔らかい丸（粉じん）
    //   Spark ：明るい芯の点（火花。Stretched Billboard で伸ばして線にする）
    //   Shard ：ひし形の欠片（結晶・ガラスの破片）。縁が明るく光る
    // 色は粒の色 × Intensity（1 を超えると Bloom で光る）。出力は不透明度を掛けた色（乗算済み）なので、
    // 粉じんは Blend One OneMinusSrcAlpha、光る粒は Blend One One で使う
    Properties
    {
        [Enum(Soft,0,Spark,1,Shard,2)] _Shape ("Shape", Float) = 0
        _Softness ("Softness (Soft)", Range(0.01, 1)) = 0.7
        _Intensity ("Intensity (HDR)", Range(0, 20)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _Shape;
            float _Softness;
            float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float a;
                float glow = 1;
                if (_Shape < 0.5)
                {
                    // 柔らかい丸：中心ほど濃く、縁へなめらかに消える
                    a = smoothstep(0, max(_Softness, 1e-3), saturate(1 - length(p)));
                }
                else if (_Shape < 1.5)
                {
                    // 火花：明るい芯の点。Stretched Billboard で動く向きに伸ばされて線になる（伸ばす向きの軸に依らない）
                    float r = saturate(1 - length(p));
                    a = r * r;
                }
                else
                {
                    // 欠片：ひし形。縁と中心の筋を明るくしてガラスのきらめきに
                    float d = abs(p.x) * 1.4 + abs(p.y);
                    a = 1 - smoothstep(0.9, 1, d);
                    glow = 0.6 + 0.8 * smoothstep(0.6, 0.95, d) + 0.5 * (1 - smoothstep(0, 0.12, abs(p.x)));
                }
                half4 c = input.color;
                c.a *= a;
                return half4(c.rgb * glow * _Intensity * c.a, c.a);
            }
            ENDHLSL
        }
    }
}
