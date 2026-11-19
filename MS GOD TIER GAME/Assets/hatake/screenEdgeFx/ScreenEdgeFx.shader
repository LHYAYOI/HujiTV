// 画面の縁取り演出(全画面)。
// パス 0: 紫の星空ともや(Nebula)
// パス 1: ざらついた縁の暗がり(Grunge)
// どちらも「乗算済みアルファ」で画面の色の上に重ねるだけで、画面の色は読まない(コピー不要)。
// 中央の何も描かない範囲はメッシュに穴を開けて、画素の計算そのものを省く(ScreenEdgeFx.cs)。
Shader "Hatake/ScreenEdgeFx"
{
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Noise (R:cloud G:grunge B:stars)", 2D) = "gray" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Overlay" }

        ZTest Always
        ZWrite Off
        Cull Off
        Blend One OneMinusSrcAlpha, Zero One

        HLSLINCLUDE
        #pragma target 3.0

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_NoiseTex);
        SAMPLER(sampler_NoiseTex);

        CBUFFER_START(UnityPerMaterial)
            // 共通: x = 画面の横/縦
            float4 _EdgeFxAspect;

            // Nebula
            // xy = 平らな中央の半分の大きさ、zw = 1 / (1 - xy)
            float4 _NebBox;
            // x = 始まり、y = 1 / (終わり - 始まり)、z = ノイズの効き、w = 全体の強さ
            float4 _NebMask;
            // xy = 倍率、zw = ずらし
            float4 _NebSt0;
            float4 _NebSt1;
            float4 _NebSt2;
            float4 _NebSt3;
            float4 _NebSt4;
            // rgb = 色、a = 濃さ
            half4 _NebColorA;
            half4 _NebColorB;
            half4 _NebBandColor;
            half4 _NebStarColor;
            // x = 帯の始まり(|y|)、y = 1 / 幅、z = しきい値、w = 縁の鋭さ
            float4 _NebBand;
            // x = 星の始まり、y = 1 / 幅、z = またたきの下限、w = またたきの幅
            float4 _NebStar;

            // Grunge
            float4 _GruBox;
            float4 _GruMask;
            float4 _GruSt0;
            float4 _GruSt1;
            half4 _GruColor;
            // x = 粒の強さ
            float4 _GruParams;
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
        };

        // 縁からの距離(中央の箱の中は 0、画面の端の中点で 1、角で約 1.41)
        half EdgeDistance(float2 p, float4 box)
        {
            half2 q = max(abs(p) - box.xy, 0.0) * box.zw;
            return length(q);
        }

        // 画面の縦を 1 としたノイズ用の座標
        float2 NoiseBase(float2 p)
        {
            return float2(p.x * _EdgeFxAspect.x, p.y) * 0.5;
        }
        ENDHLSL

        Pass
        {
            Name "Nebula"

            HLSLPROGRAM
            #pragma vertex VertNebula
            #pragma fragment FragNebula
            #pragma multi_compile_local_fragment _ _EDGEFX_LITE

            struct VaryingsNebula
            {
                float4 positionCS : SV_POSITION;
                float2 p : TEXCOORD0;
                float4 uv01 : TEXCOORD1;
                float4 uv23 : TEXCOORD2;
                float2 uv4 : TEXCOORD3;
            };

            VaryingsNebula VertNebula(Attributes v)
            {
                VaryingsNebula o;
                // メッシュの頂点はそのままクリップ空間(-1〜1)
                o.positionCS = float4(v.positionOS.xy, UNITY_NEAR_CLIP_VALUE, 1.0);
                o.p = v.positionOS.xy;
                float2 b = NoiseBase(v.positionOS.xy);
                o.uv01.xy = b * _NebSt0.xy + _NebSt0.zw;
                o.uv01.zw = b * _NebSt1.xy + _NebSt1.zw;
                o.uv23.xy = b * _NebSt2.xy + _NebSt2.zw;
                o.uv23.zw = b * _NebSt3.xy + _NebSt3.zw;
                o.uv4 = b * _NebSt4.xy + _NebSt4.zw;
                return o;
            }

            half4 FragNebula(VaryingsNebula i) : SV_Target
            {
                half e = EdgeDistance(i.p, _NebBox);

                // もや: 逆向きに流れる 2 枚を重ねる
                half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv01.xy).r;
            #if defined(_EDGEFX_LITE)
                half n2 = n1;
            #else
                half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv01.zw).r;
            #endif
                half n = (n1 + n2) * 0.5;

                half m = saturate((e + (n - 0.5) * _NebMask.z - _NebMask.x) * _NebMask.y);
                m = m * m * (3.0 - 2.0 * m);

                // 明るいむら
                half glow = saturate(n1 * n2 * 2.2);
                half3 col = lerp(_NebColorA.rgb, _NebColorB.rgb, glow);
                half alpha = m * lerp(_NebColorA.a, _NebColorB.a, glow);

                // 上下の横長の雲
                half bn = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv23.xy).r;
                half band = saturate((abs(i.p.y) - _NebBand.x) * _NebBand.y);
                half c = saturate((bn - _NebBand.z) * _NebBand.w) * band * _NebBandColor.a;
                col = lerp(col, _NebBandColor.rgb, c);
                alpha = alpha + c * (1.0 - alpha);

                // 星(加算)
                half s = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv23.zw).b;
            #if !defined(_EDGEFX_LITE)
                s += SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv4).b * 0.6;
            #endif
                half sm = saturate((e - _NebStar.x) * _NebStar.y);
                half twinkle = _NebStar.z + _NebStar.w * n2;
                half3 star = _NebStarColor.rgb * (s * sm * twinkle);

                return half4(col * alpha + star, alpha) * _NebMask.w;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Grunge"

            HLSLPROGRAM
            #pragma vertex VertGrunge
            #pragma fragment FragGrunge

            struct VaryingsGrunge
            {
                float4 positionCS : SV_POSITION;
                float2 p : TEXCOORD0;
                float4 uv01 : TEXCOORD1;
            };

            VaryingsGrunge VertGrunge(Attributes v)
            {
                VaryingsGrunge o;
                o.positionCS = float4(v.positionOS.xy, UNITY_NEAR_CLIP_VALUE, 1.0);
                o.p = v.positionOS.xy;
                float2 b = NoiseBase(v.positionOS.xy);
                o.uv01.xy = b * _GruSt0.xy + _GruSt0.zw;
                o.uv01.zw = b * _GruSt1.xy + _GruSt1.zw;
                return o;
            }

            half4 FragGrunge(VaryingsGrunge i) : SV_Target
            {
                half e = EdgeDistance(i.p, _GruBox);

                // 大きなむらと細かい粒
                half g1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv01.xy).g;
                half g2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv01.zw).g;
                half g = g1 * 0.6 + g2 * 0.4;

                half a = saturate((e + (g - 0.5) * _GruMask.z - _GruMask.x) * _GruMask.y);

                // 中間の濃さのところだけ粒を立てる
                a = saturate(a + (g2 - 0.5) * _GruParams.x * a * (1.0 - a) * 4.0);
                a *= _GruColor.a * _GruMask.w;

                return half4(_GruColor.rgb * a, a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
