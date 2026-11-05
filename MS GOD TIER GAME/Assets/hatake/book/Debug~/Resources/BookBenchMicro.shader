Shader "Hidden/Book/Bench Micro"
{
    // 計測用（BookPerfProbe）：BookFlipComposite の関数を1つずつ全画面で描き、どの関数が重いかを測る。製品では使わない
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_local_fragment MB_HASH MB_NOISE MB_FBM MB_SIN MB_OUTSIDE MB_STACK MB_COVERCLOSED MB_COVERSURFACE MB_BOARDFINISH MB_METAL MB_PAPERTILT MB_PAPERBASE MB_SHADEPAGE MB_RESOLVE MB_PREPARE MB_DIM
            #define _PAPERFINISH_MATTE
            #include "UnityCG.cginc"
            #include "../../shader/BookFlipCore.cginc"
            #include "../../shader/BookFlipLook.cginc"
            #include "../../shader/BookFlipEffects.cginc"

            struct VertexInput { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct VertexOutput { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            VertexOutput Vert(VertexInput input)
            {
                VertexOutput output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float4 Frag(VertexOutput input) : SV_Target
            {
                float2 p = BookFromUV(input.uv);
                s_bookCoverBaseColor = _CoverColor.rgb;
            #if defined(MB_HASH)
                // ハッシュ 8 回
                float v = 0;
                for (int i = 0; i < 8; i++) v += BookLookHash(p * 37 + i * 1.7);
                return v / 8;
            #elif defined(MB_SIN)
                // sin 8 回（ハッシュの中身。大きな引数）
                float v = 0;
                for (int i = 0; i < 8; i++) v += frac(sin(dot(p * 37 + i * 1.7, float2(127.1, 311.7))) * 43758.5453);
                return v / 8;
            #elif defined(MB_NOISE)
                return BookLookNoise(p * 40);
            #elif defined(MB_FBM)
                return BookLookFbm(p * 40);
            #elif defined(MB_OUTSIDE)
                return BookOutside(p);
            #elif defined(MB_STACK)
                return BookStack(p);
            #elif defined(MB_COVERCLOSED)
                return float4(BookClosedBoardColor(p, true), 1);
            #elif defined(MB_COVERSURFACE)
                return float4(BookCoverSurface(float3(-1, -1, -1), p), 1);
            #elif defined(MB_BOARDFINISH)
                float2 center, halfSize;
                BookCoverBox(true, center, halfSize);
                return float4(BookBoardFinish(float3(0.5, 0.4, 0.3), p, center, halfSize, true, 1), 1);
            #elif defined(MB_METAL)
                float2 center, halfSize;
                BookCoverBox(true, center, halfSize);
                float sd = BookSdRoundBox(p - center, halfSize, _CoverRadius);
                return float4(BookCornerMetal(float3(0.5, 0.4, 0.3), p - center, halfSize, sd), 1);
            #elif defined(MB_PAPERTILT)
                return float4(BookPaperTilt(p), 0, 1);
            #elif defined(MB_PAPERBASE)
                return float4(BookPaperBase(float3(0.8, 0.8, 0.8), p, false), 1);
            #elif defined(MB_SHADEPAGE)
                // 紙のページ1枚分の陰影（ShadePage の本文の処理と同じ順）
                BookFlipHit h = (BookFlipHit)0;
                h.flatPos = p;
                h.uv = saturate(BookPageUV(p));
                h.right = p.x >= 0 ? 1 : 0;
                float3 c = BookPaper(float3(0.8, 0.8, 0.8), h);
                float3 normal;
                c = BookGutter(c, p.x, normal);
                float3 n2 = normalize(normal + float3(BookPaperTilt(p), 0));
                c *= BookRelief(normal, n2);
                c += min(1, BookPaperSheen(n2, p, true));
                return float4(c, 1);
            #elif defined(MB_RESOLVE)
                BookFlipHit h = BookFlipResolve(p);
                return float4(h.uv, h.layer * 0.2, 1);
            #elif defined(MB_PREPARE)
                BookPrepareCover();
                return float4(BookCoverBaseColor(), 1);
            #elif defined(MB_DIM)
                return BookApplyDim(float4(0.5, 0.5, 0.5, 1), p);
            #else
                return 0;
            #endif
            }
            ENDCG
        }
    }
}
