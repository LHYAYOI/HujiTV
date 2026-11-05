Shader "Book/Page Flip Composite"
{
    // RenderTextureで撮影した見開きを合成し、ページめくりと本の装飾を描く
    // BookFlipView が Graphics.Blit で出力RenderTextureへ描画する
    Properties
    {
        [Header(Paper)]
        _PaperTint ("Paper tint", Color) = (1, 0.96, 0.88, 1)
        _EndpaperColor ("Endpaper color (inside of covers)", Color) = (0.86, 0.8, 0.68, 1)
        // 紙の仕上げ：シェーダーのバリアント（_PAPERFINISH_MATTE / _PAPERFINISH_GLOSS）で切り分ける
        [KeywordEnum(Matte, Gloss)] _PaperFinish ("Paper finish (Matte coated / Gloss coated)", Float) = 0
        _GlossStrength ("Gloss reflection strength", Range(0, 1)) = 0.25
        _GlossSharpness ("Gloss reflection sharpness", Range(4, 256)) = 48
        _PaperTex ("Paper texture (optional)", 2D) = "white" {}
        _PaperTexStrength ("Paper texture strength", Range(0, 1)) = 0
        _PaperGrain ("Paper grain", Range(0, 0.5)) = 0.08
        _PaperAge ("Edge aging", Range(0, 1)) = 0.45
        _AgeColor ("Aged edge color", Color) = (0.78, 0.58, 0.36, 1)
        _PaperBump ("Paper fiber relief", Range(0, 2)) = 0.22
        _PaperBumpScale ("Paper fiber scale", Range(20, 600)) = 160
        [Normal] _PaperNormalTex ("Paper normal map (optional)", 2D) = "bump" {}
        _PaperNormalStrength ("Paper normal map strength", Range(0, 2)) = 0
        _InkBleed ("Ink bleed (matte coated: 0)", Range(0, 1)) = 0
        _InkBleedRadius ("Ink bleed radius (page UV)", Range(0.001, 0.02)) = 0.006

        [Header(Gutter and Light)]
        _GutterWidth ("Gutter width", Range(0.01, 0.5)) = 0.14
        _GutterDark ("Gutter darkness", Range(0, 1)) = 0.35
        _LightDir ("Light direction (world, constant)", Vector) = (5, 3, 1, 0)
        _PoseShading ("Pose shading (book facing the light)", Range(0, 1)) = 0.5
        _SpecPower ("Specular shininess", Range(1, 128)) = 16
        _SpecStrength ("Specular strength", Range(0, 2)) = 0.35

        [Header(Page Turn)]
        _CurlWidth ("Curl width", Range(0.01, 0.6)) = 0.22
        _CurlDark ("Curl crease darkness", Range(0, 1)) = 0.55
        _CurlSpec ("Curl highlight", Range(0, 1)) = 0.18
        _ShowThrough ("Show-through of front print", Range(0, 1)) = 0.12
        _FoldShadow ("Shadow under the sheet", Range(0, 1)) = 0.55
        _FoldShadowWidth ("Shadow under the sheet width", Range(0.005, 0.5)) = 0.12
        _DropShadow ("Sheet drop shadow", Range(0, 1)) = 0.45
        _DropShadowWidth ("Sheet drop shadow width", Range(0.005, 0.3)) = 0.06
        [HDR] _FoldGlowColor ("Magic glow on the fold", Color) = (1.6, 1.1, 0.45, 1)

        [Header(Page Stack)]
        _StackColor ("Page edge color", Color) = (0.93, 0.86, 0.72, 1)
        _StackLineDark ("Page edge lines", Range(0, 1)) = 0.35

        [Header(Cover)]
        _CoverColor ("Cover color", Color) = (0.32, 0.12, 0.08, 1)
        _CoverTex ("Cover texture (optional)", 2D) = "white" {}
        _CoverMargin ("Cover margin XY", Vector) = (0.045, 0.06, 0, 0)
        _CoverRadius ("Cover corner radius", Range(0, 0.2)) = 0.035
        _CoverLeather ("Leather grain", Range(0, 1)) = 0.45
        [HDR] _CoverTrimColor ("Cover trim (A = amount)", Color) = (1.2, 0.85, 0.35, 1)
        _CoverTrimInset ("Cover trim inset", Range(0, 0.1)) = 0.018
        _CoverTrimWidth ("Cover trim width", Range(0.0005, 0.02)) = 0.003
        _BookShadow ("Shadow on background", Range(0, 1)) = 0.5
        [HDR] _CornerColor ("Corner metal (A = show)", Color) = (1.05, 0.78, 0.4, 1)
        _CornerSize ("Corner metal size", Range(0.02, 0.4)) = 0.17
        _BoardThickness ("Cover board thickness", Range(0, 0.06)) = 0.02

        [Header(Magic)]
        [HDR] _MagicColor ("Magic border color", Color) = (1, 0.73, 0.28, 1)
        _MagicStrength ("Magic border", Range(0, 1)) = 0

        [Header(Dim and Spot)]
        _DimColor ("Dim color", Color) = (0.06, 0.05, 0.09, 1)
        [HDR] _SpotRimColor ("Spot rim glow", Color) = (0.6, 0.9, 1.2, 1)
        [HDR] _SpotRuneColor ("Spot rune ring (A = show)", Color) = (1.7, 1.2, 0.5, 1)
        _SpotRuneWidth ("Spot rune ring width", Range(0.01, 0.15)) = 0.07
        _SpotRuneSpeed ("Spot rune ring speed", Range(-1, 1)) = 0.04

        [Header(Screen Fade)]
        _FadeColor ("Fade color", Color) = (0, 0, 0, 1)

        [Header(Burn and Revive)]
        _BurnCharColor ("Burn char color", Color) = (0.07, 0.04, 0.03, 1)
        [HDR] _BurnEmberColor ("Burn ember color", Color) = (3.2, 1.1, 0.25, 1)
        _BurnEdgeWidth ("Burn edge width", Range(0.005, 0.15)) = 0.045
        [HDR] _ReviveColor ("Revive light color", Color) = (0.9, 1.1, 1.6, 1)
        _ReviveEdgeWidth ("Revive edge width", Range(0.005, 0.15)) = 0.04
        _FxParticles ("Particle density", Range(0, 1)) = 0.55
        _FxAsh ("Burnt flake density", Range(0, 1)) = 0.6
        _BurnCurl ("Burning edge curl", Range(0, 1)) = 0.8

        [Header(Output Blend)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend (Blit: One / UI: SrcAlpha)", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend (Blit: Zero / UI: OneMinusSrcAlpha)", Float) = 0

        // ここから下は BookFlipView が毎フレーム設定する
        [HideInInspector] _MainTex ("Unused", 2D) = "white" {}
        [HideInInspector] _CurLeftTex ("Current left", 2D) = "white" {}
        [HideInInspector] _CurRightTex ("Current right", 2D) = "white" {}
        [HideInInspector] _DstLeftTex ("Destination left", 2D) = "white" {}
        [HideInInspector] _DstRightTex ("Destination right", 2D) = "white" {}
        [HideInInspector] _CurLeftRect ("Current left crop", Vector) = (0, 0, 0.5, 1)
        [HideInInspector] _CurRightRect ("Current right crop", Vector) = (0.5, 0, 0.5, 1)
        [HideInInspector] _DstLeftRect ("Destination left crop", Vector) = (0, 0, 0.5, 1)
        [HideInInspector] _DstRightRect ("Destination right crop", Vector) = (0.5, 0, 0.5, 1)
        [HideInInspector] _BookRect ("Book rect", Vector) = (0.08, 0.1, 0.92, 0.9)
        [HideInInspector] _PageAspect ("Page aspect", Float) = 1.2
        [HideInInspector] _TurnSide ("Turn side", Float) = 1
        [HideInInspector] _Turning ("Turning", Float) = 0
        [HideInInspector] _Fold ("Fold", Vector) = (1, 0, 1, 0)
        [HideInInspector] _TurnGlow ("Turn glow", Float) = 0
        [HideInInspector] _Stack ("Stack", Vector) = (0.02, 0.02, 14, 0.6)
        [HideInInspector] _Dim ("Dim", Float) = 0
        [HideInInspector] _ScreenFade ("Screen fade", Float) = 0
        [HideInInspector] _SpotCount ("Spot count", Float) = 0
        [HideInInspector] _Rigid ("Rigid cover turn", Float) = 0
        [HideInInspector] _RigidCover ("Rigid cover", Float) = 1
        [HideInInspector] _BoardMode ("Board mode", Float) = 0
        [HideInInspector] _BoardOpen ("Board open amount", Float) = 1
        [HideInInspector] _FxMode ("Effect mode", Float) = 0
        [HideInInspector] _FxSide ("Effect side", Float) = 1
        [HideInInspector] _FxProgress ("Effect progress", Float) = 0
        [HideInInspector] _FxOrigin ("Effect origin", Vector) = (0.5, 0.5, 1, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest Always


        // 0：1パスで全部を描く（PC）
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            // 紙の仕上げのバリアント。実行時にも切り替えられるよう両方を必ず含める（shader_feature だと
            // ビルドでマテリアルが使っていない方が削られ、BookFlipView.PaperFinish で切り替えられない）
            #pragma multi_compile_local_fragment _PAPERFINISH_MATTE _PAPERFINISH_GLOSS
            #define BOOK_FX

            #include "BookFlipCompositeCommon.cginc"

            float4 Frag(VertexOutput input) : SV_Target
            {
                float2 p = BookFromUV(input.uv);
                BookPrepareCover();
                bool fxPage; float fxEdge; float2 curlTilt;
                BookFlipHit h = BookResolveHit(p, fxPage, fxEdge, curlTilt);
                return BookFinish(BookBaseColor(h, p, fxPage, fxEdge, curlTilt), h, p);
            }
            ENDCG
        }

        // 1〜4：モバイル用の分割パス。1本の大きなシェーダーはモバイルの GPU で極端に遅くなる
        // （レジスタが足りずに退避する）ので、面の種類ごとの小さなシェーダーに分けて描く。
        // 1〜3 は面の色を _BookBaseTex へ（担当外の画素は捨てる）、4 がそれを読んで仕上げを重ねて出力する
        // 1：本の外（小口・表紙の板・背景の影）
        Pass
        {
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragOutside
            #define BOOK_SPLIT
            #include "BookFlipCompositeCommon.cginc"

            float4 FragOutside(VertexOutput input) : SV_Target
            {
                float2 p = BookFromUV(input.uv);
                // 本より十分外は何も無い（透明）
                if (BookFarOutside(p)) return 0;
                BookFlipHit h = BookFlipResolve(p);
                h.kind = h.layer == BOOK_LAYER_NONE ? 0 : BookSlotKind(h.dest, h.right);
                if ((h.layer == BOOK_LAYER_FLAT || h.layer == BOOK_LAYER_UNDER) && abs(h.kind - BOOK_SLOT_EMPTY) < 0.5) h.layer = BOOK_LAYER_NONE;
                if (h.layer != BOOK_LAYER_NONE) discard;
                BookPrepareCover();
                return BookOutside(p);
            }
            ENDCG
        }

        // 2：ページ（本文・見返し）
        Pass
        {
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragPage
            #define BOOK_SPLIT
            #pragma multi_compile_local_fragment _PAPERFINISH_MATTE _PAPERFINISH_GLOSS
            #pragma multi_compile_local_fragment _ BOOK_FX
            #define BOOK_PASS_PAGE
            #define BOOK_PAPER_LUT
            #include "BookFlipCompositeCommon.cginc"

            float4 FragPage(VertexOutput input) : SV_Target
            {
                float2 p = BookFromUV(input.uv);
                if (BookFarOutside(p)) discard;
                bool fxPage; float fxEdge; float2 curlTilt;
                BookFlipHit h = BookResolveHit(p, fxPage, fxEdge, curlTilt);
                if (h.layer == BOOK_LAYER_NONE || BookIsCoverPixel(h)) discard;
                return BookBaseColor(h, p, fxPage, fxEdge, curlTilt);
            }
            ENDCG
        }

        // 3：表紙（印刷した表紙、回る板の小口・縁、角金具）
        Pass
        {
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCover
            #define BOOK_SPLIT
            #define BOOK_PASS_COVER
            #include "BookFlipCompositeCommon.cginc"

            float4 FragCover(VertexOutput input) : SV_Target
            {
                float2 p = BookFromUV(input.uv);
                if (BookFarOutside(p)) discard;
                bool fxPage; float fxEdge; float2 curlTilt;
                BookFlipHit h = BookResolveHit(p, fxPage, fxEdge, curlTilt);
                if (h.layer == BOOK_LAYER_NONE || !BookIsCoverPixel(h)) discard;
                BookPrepareCover();
                return BookBaseColor(h, p, fxPage, fxEdge, curlTilt);
            }
            ENDCG
        }

        // 4：仕上げ（面の色 + 姿勢の明暗・落ち影・魔法の光・暗転・燃えかす・フェード）
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragFinish
            #define BOOK_SPLIT
            #pragma multi_compile_local_fragment _ BOOK_FX
            #include "BookFlipCompositeCommon.cginc"

            float4 FragFinish(VertexOutput input) : SV_Target
            {
                float2 p = BookFromUV(input.uv);
            #if !defined(BOOK_FX)
                // 本より十分外：暗転と画面フェードだけ（燃焼の粒は本の外にも出るので、演出中は省かない）
                if (BookFarOutside(p)) return BookApplyScreenFade(BookApplyDim(float4(0, 0, 0, 0), p));
            #endif
                float4 col = BOOK_TEX_LOD(_BookBaseTex, float4(input.uv, 0, 0));
                BookFlipHit h = BookFlipResolve(p);
                h.kind = h.layer == BOOK_LAYER_NONE ? 0 : BookSlotKind(h.dest, h.right);
                if ((h.layer == BOOK_LAYER_FLAT || h.layer == BOOK_LAYER_UNDER) && abs(h.kind - BOOK_SLOT_EMPTY) < 0.5) h.layer = BOOK_LAYER_NONE;
                return BookFinish(col, h, p);
            }
            ENDCG
        }

        // 5：表紙の地の色（表紙の絵の外周の平均）を1画素に求める。分割パスはこれを読む（画素ごとに8回読まない）
        Pass
        {
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCoverBase
            #include "BookFlipCompositeCommon.cginc"

            float4 FragCoverBase(VertexOutput input) : SV_Target
            {
                return float4(BookCoverBaseColorCompute(), 1);
            }
            ENDCG
        }

        // 6・7：紙の模様の焼き込み（BookFlipView が紙の設定・出力の大きさが変わったときだけ描く）
        // 6：紙の地に掛ける色
        Pass
        {
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragPaperLut
            #pragma multi_compile_local_fragment _PAPERFINISH_MATTE _PAPERFINISH_GLOSS
            #include "BookFlipCompositeCommon.cginc"

            float4 FragPaperLut(VertexOutput input) : SV_Target
            {
                float2 p = float2(input.uv.x * 2 - 1, input.uv.y * _PageAspect);
                return float4(BookPaperMulCompute(p), 1);
            }
            ENDCG
        }

        // 7：紙の法線の傾き
        Pass
        {
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragPaperTiltLut
            #pragma multi_compile_local_fragment _PAPERFINISH_MATTE _PAPERFINISH_GLOSS
            #include "BookFlipCompositeCommon.cginc"

            float4 FragPaperTiltLut(VertexOutput input) : SV_Target
            {
                float2 p = float2(input.uv.x * 2 - 1, input.uv.y * _PageAspect);
                return float4(BookPaperTiltCompute(p), 0, 1);
            }
            ENDCG
        }

        // 8：本文のめくり（モバイル）。めくり始めに、今の見開きの静止画（_BookTurnCacheTex：仕上げ済み）と
        // 移動先の見開きのページ（_BookTurnDestTex：面の色）を1度だけ描いておき、毎フレームはそれを読んで
        // 折り目・めくった紙の裏・影だけを計算する（毎フレームの処理を小さく保つ）
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragTurn
            #define BOOK_SPLIT
            #define BOOK_NO_RIGID
            #include "BookFlipCompositeCommon.cginc"
            BOOK_DECLARE_TEX(_BookTurnCacheTex)
            BOOK_DECLARE_TEX(_BookTurnDestTex)

            // 本の座標 → 出力の UV（BookFromUV の逆）
            float2 BookToOutputUV(float2 flat)
            {
                float2 t = float2((flat.x + 1) * 0.5, flat.y / max(_PageAspect, 1e-5));
                return _BookRect.xy + t * (_BookRect.zw - _BookRect.xy);
            }

            float4 FragTurn(VertexOutput input) : SV_Target
            {
                float2 p = BookFromUV(input.uv);
                BookFlipHit h = BookFlipResolve(p);
                h.kind = h.layer == BOOK_LAYER_NONE ? 0 : BookSlotKind(h.dest, h.right);
                if ((h.layer == BOOK_LAYER_FLAT || h.layer == BOOK_LAYER_UNDER) && abs(h.kind - BOOK_SLOT_EMPTY) < 0.5) h.layer = BOOK_LAYER_NONE;
                float4 col;
                float turnFade = saturate(h.foldLength * 4);
                float curlWidth = max(_CurlWidth * saturate(h.foldLength * 1.5), 1e-4);
                if (h.layer == BOOK_LAYER_UNDER || h.layer == BOOK_LAYER_BACK)
                {
                    // 移動先のページ（裏面は、めくり終えたときの位置で読む）
                    float2 uv = h.layer == BOOK_LAYER_BACK ? BookToOutputUV(h.flatPos) : input.uv;
                    col = BOOK_TEX_LOD(_BookTurnDestTex, float4(uv, 0, 0));
                    // 移動先の絵はページの外が透明。ページの縁で混ざった分を戻す（半分の解像度の仮の絵で縁が暗くならない）
                    float3 c = col.rgb / max(col.a, 1e-3);
                    if (h.layer == BOOK_LAYER_BACK)
                    {
                        // ShadePage の裏面の処理と同じ：表の印刷の透け、カールの陰と照り返し、縁の陰
                        bool cover = h.kind > 0.5 && h.kind < 2.5 || h.kind > 4.5;
                        float frontRight = h.sheetPos.x >= 0 ? 1 : 0;
                        if (!cover && BookSlotKind(0, frontRight) < 0.5)
                        {
                            float3 through = BookSamplePage(0, frontRight, saturate(BookPageUV(h.sheetPos))).rgb;
                            c *= lerp(1, saturate(through * 1.15), _ShowThrough);
                        }
                        float dd = -h.fold;
                        float bend = 1 - smoothstep(0, curlWidth, dd);
                        c *= lerp(1, _CurlDark, bend * bend * turnFade);
                        float band = (dd - curlWidth * 0.45) / (curlWidth * 0.22);
                        c += _CurlSpec * exp(-band * band) * turnFade;
                        c *= lerp(0.85, 1, smoothstep(0, 0.012, h.flapInside));
                    }
                    else
                    {
                        c *= 1 - _FoldShadow * exp(-h.fold / _FoldShadowWidth) * turnFade;
                    }
                    col.rgb = c * BookPoseShade();
                    col.a = 1;
                }
                else
                {
                    col = BOOK_TEX_LOD(_BookTurnCacheTex, float4(input.uv, 0, 0));
                    if (h.layer == BOOK_LAYER_FRONT)
                    {
                        // 折り目の手前で紙が持ち上がり始める（ShadePage の表の処理と同じ）
                        float bend = 1 - smoothstep(0, curlWidth * 0.6, -h.fold);
                        col.rgb *= lerp(1, 0.8, bend * turnFade);
                    }
                }
                bool destination = h.layer == BOOK_LAYER_UNDER || h.layer == BOOK_LAYER_BACK;
                col = BookTurnOverlay(col, h, p);
                // 静止画には暗転が入っているので、移動先のページだけに掛ける
                if (destination) col = BookApplyDim(col, p);
                return col;
            }
            ENDCG
        }

        // 9：暗転だけが動いている間（モバイル）。暗転の前の静止画（_BookDimBaseTex）に暗転・スポット・ルーンだけを重ねる
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragDim
            #define BOOK_SPLIT
            #include "BookFlipCompositeCommon.cginc"
            BOOK_DECLARE_TEX(_BookDimBaseTex)

            float4 FragDim(VertexOutput input) : SV_Target
            {
                float2 p = BookFromUV(input.uv);
                float4 col = BOOK_TEX_LOD(_BookDimBaseTex, float4(input.uv, 0, 0));
                return BookApplyScreenFade(BookApplyDim(col, p));
            }
            ENDCG
        }

        // 10：表紙の開け閉め（モバイル）。めくる前と後の静止画（_BookTurnCacheTex：今、_BookTurnDestTex：移動先）を読み、
        // 回る板のめくり始めに上を向いていた面は今の静止画、反対の面は移動先の静止画の、板が寝た位置の絵を貼る。
        // 毎フレームは板の向き・影だけを計算する
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragCoverTurn
            #define BOOK_SPLIT
            #include "BookFlipCompositeCommon.cginc"
            // どの静止画を読むかは BookFlipView が選んで渡す（移動先を描き終えるまでは今の静止画で代用）
            BOOK_DECLARE_TEX(_BookTurnCacheTex) // 今
            BOOK_DECLARE_TEX(_BookTurnDestTex)  // 移動先
            float _BookOpening; // 1 = 閉じた本を開く（今の静止画が閉じた本）、0 = 閉じる

            float2 BookToOutputUV(float2 flat)
            {
                float2 t = float2((flat.x + 1) * 0.5, flat.y / max(_PageAspect, 1e-5));
                return _BookRect.xy + t * (_BookRect.zw - _BookRect.xy);
            }

            // 移動先の静止画のうち描き終えた範囲（出力の UV の x の範囲。z = 1 なら全体を描き終えてミップもある）。
            // 描き終えていない所は、半分の解像度の仮の絵（w = 1 で描き終えている）、それも無ければ今の静止画で代用する
            float4 _BookDestReady;
            BOOK_DECLARE_TEX(_BookTurnDestLowTex)
            bool DestReady(float2 uv) { return _BookDestReady.z > 0.5 || (uv.x >= _BookDestReady.x && uv.x <= _BookDestReady.y); }
            float4 Cur(float2 uv, float lod) { return BOOK_TEX_LOD(_BookTurnCacheTex, float4(uv, 0, lod)); }
            float4 Dst(float2 uv, float lod)
            {
                if (!DestReady(uv)) return _BookDestReady.w > 0.5 ? BOOK_TEX_LOD(_BookTurnDestLowTex, float4(uv, 0, 0)) : Cur(uv, lod);
                return BOOK_TEX_LOD(_BookTurnDestTex, float4(uv, 0, _BookDestReady.z > 0.5 ? lod : 0));
            }
            // dest：0 = めくる前の状態（今の静止画）、1 = めくった後の状態（移動先の静止画）
            float4 StateImage(float dest, float2 uv, float lod) { return dest < 0.5 ? Cur(uv, lod) : Dst(uv, lod); }

            float4 FragCoverTurn(VertexOutput input) : SV_Target
            {
                float2 p = BookFromUV(input.uv);
                // 本より十分外は変わらない
                if (BookFarOutside(p)) return Cur(input.uv, 0);
                BookFlipHit h = BookFlipResolve(p);

                h.kind = h.layer == BOOK_LAYER_NONE ? 0 : BookSlotKind(h.dest, h.right);
                if ((h.layer == BOOK_LAYER_FLAT || h.layer == BOOK_LAYER_UNDER) && abs(h.kind - BOOK_SLOT_EMPTY) < 0.5) h.layer = BOOK_LAYER_NONE;
                float4 col;
                if (h.rigid > 0.5)
                {
                    if (h.rigidEdge > 0.5)
                    {
                        // 回っている板の小口（BookBoardEdgeColor のノイズを省いた版。小口は細いので見分けがつかない）
                        float3 baseColor = BOOK_TEX_LOD(_BookCoverBaseTex, float4(0.5, 0.5, 0, 0)).rgb;
                        float across = saturate(h.flapInside);
                        float3 edgeColor = baseColor * 0.45 * 0.92 + baseColor * 0.35 * (1 - smoothstep(0, 0.25, across));
                        col = float4(edgeColor * BookPoseShade(), 1);
                    }
                    // 板の面：めくり始めに上を向いていた面（dest 0）は今の静止画、反対の面（dest 1）は移動先の静止画の、
                    // 板が寝たときの位置の絵を貼る（開くときは表紙→見返し、閉じるときは見返し→表紙）。
                    // 板が立つほど絵を横に縮めて読むので、縮む分だけ粗いミップで読む
                    else
                    {
                        float lod = clamp(log2(1 / max(abs(_Fold.z), 0.02)), 0, 6);
                        col = StateImage(h.dest, BookToOutputUV(h.flatPos), lod);
                    }
                    col.rgb *= h.rigidShade;
                    col.a = 1;
                }
                else if (h.layer == BOOK_LAYER_UNDER || h.layer == BOOK_LAYER_FLAT) col = StateImage(h.dest, input.uv, 0);
                else
                {
                    // 本の外：動かない側の板・小口は開き具合で入れ替え、着地する側は閉じた本（背景）のまま
                    float stillSide = _RigidCover < 1.5 ? 1 : -1;
                    float4 closedCol = _BookOpening > 0.5 ? Cur(input.uv, 0) : Dst(input.uv, 0);
                    if (p.x * stillSide >= 0)
                    {
                        float4 openCol = _BookOpening > 0.5 ? Dst(input.uv, 0) : Cur(input.uv, 0);
                        col = lerp(closedCol, openCol, saturate(_BoardOpen));
                    }
                    else col = closedCol;
                }
                return BookTurnOverlay(col, h, p);
            }
            ENDCG
        }

        // 11：燃焼・復活（モバイル）。演出の前の静止画（_BookTurnCacheTex）と変化後のページ（_BookTurnDestTex）を、
        // 焼き込んだ境界（_BookFxLut）で切り替える。燃えかす・粒はパス 13・14 で四角形として描く
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragFx
            #define BOOK_SPLIT
            #include "BookFlipCompositeCommon.cginc"
            BOOK_DECLARE_TEX(_BookTurnCacheTex)
            BOOK_DECLARE_TEX(_BookTurnDestTex)
            BOOK_DECLARE_TEX(_BookFxLut) // r 境界の値, gb 進む向き, a ノイズ

            float4 FragFx(VertexOutput input) : SV_Target
            {
                float2 p = BookFromUV(input.uv);
                if (!BookFxOnPage(p)) return BOOK_TEX_LOD(_BookTurnCacheTex, float4(input.uv, 0, 0));
                float4 lut = BOOK_TEX_LOD(_BookFxLut, float4(BookPaperLutUV(p), 0, 0));
                float e = BookFxThreshold() - lut.r;
                float2 shiftUV = 0;
                float2 tilt = 0;
                if (_FxMode < 1.5 && _BurnCurl > 0 && e <= 0)
                {
                    // 燃えている縁の反り返り（BookFxCurl と同じ）
                    float width = max(_BurnEdgeWidth * 3.5, 1e-4) * (0.7 + 0.6 * lut.a);
                    float lift = 1 - smoothstep(0, width, -e);
                    lift *= lift * _BurnCurl;
                    float2 dir = lut.gb / max(length(lut.gb), 1e-5);
                    float2 shift = -dir * lift * width * 0.6 * max(_FxOrigin.z, 1e-3);
                    shiftUV = shift * float2(0.5 * (_BookRect.z - _BookRect.x), (_BookRect.w - _BookRect.y) / max(_PageAspect, 1e-5));
                    tilt = -dir * lift * 1.4;
                }
                float4 col;
                if (e > 0)
                {
                    col = BOOK_TEX_LOD(_BookTurnDestTex, float4(input.uv, 0, 0));
                    col.rgb = col.rgb / max(col.a, 1e-3) * BookPoseShade();
                    col.a = 1;
                    // 静止画には暗転が入っているので、変化後のページにも掛ける
                    col = BookApplyDim(col, p);
                }
                else col = BOOK_TEX_LOD(_BookTurnCacheTex, float4(input.uv + shiftUV, 0, 0));
                float3 l = BookLightDir();
                col.rgb *= 1 + (dot(normalize(float3(tilt, 1)), l) - l.z) * 1.6;
                // 火の縁のゆらぎ（1次元の安いゆらぎ）
                float flicker = 0.6 + 0.4 * (0.5 + 0.5 * sin(p.x * 40 + _Time.y * 8) * sin(p.y * 37 - _Time.y * 6.3));
                col.rgb = BookFxShadePageN(col.rgb, e, flicker, lut.a);
                return col;
            }
            ENDCG
        }

        // 12：燃焼・復活の境界の焼き込み（演出の始めに1度）
        Pass
        {
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragFxLut
            #include "BookFlipCompositeCommon.cginc"

            float4 FragFxLut(VertexOutput input) : SV_Target
            {
                float2 p = float2(input.uv.x * 2 - 1, input.uv.y * _PageAspect);
                float2 dir = BookFxDirection(p);
                return float4(BookFxField(p), dir, BookLookNoise(p * 9 + _FxOrigin.w));
            }
            ENDCG
        }

        // 13・14：燃えかす・粒（モバイル）。BookFlipView が1枚ずつ四角形で描く（画面全体で毎画素ループしない）。
        // TEXCOORD0 本の座標での中心からのずれ, TEXCOORD1 (cos, sin) 回転・裏返り・大きさ, TEXCOORD2 濃さ・光・種類, TEXCOORD3 ノイズの種

        // 13：燃えかすの紙片（乗算済みの色で重ねる。BookFxAsh と同じ形）
        Pass
        {
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex FxVert
            #pragma fragment FragAsh
            #include "BookFlipFxQuads.cginc"
            float4 FragAsh(FxVertexOutput input) : SV_Target
            {
                float2 q = input.local.xy;
                float cs = input.shape.x, sn = input.shape.y;
                q = float2(q.x * cs - q.y * sn, q.x * sn + q.y * cs);
                q.x /= input.shape.z;
                float size = input.shape.w;
                float shape = length(q) / size + (FxNoise(q / size * 1.8 + input.seed.xy) - 0.5) * 0.9;
                float body = 1 - smoothstep(0.82, 1, shape);
                float edge = smoothstep(0.5, 0.95, shape) * body;
                float a = body * input.look.x;
                float3 color = _BurnCharColor.rgb * 1.5 + _BurnEmberColor.rgb * edge * input.look.y;
                return float4(color * a, a);
            }
            ENDCG
        }

        // 14：火の粉・光の粒（足して重ねる。BookFxParticles と同じ形）
        Pass
        {
            Blend One One, One One
            BlendOp Add, Max
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex FxVert
            #pragma fragment FragSpark
            #include "BookFlipFxQuads.cginc"
            float4 FragSpark(FxVertexOutput input) : SV_Target
            {
                float2 d = input.local.xy;
                float r = input.shape.w;
                float glow = exp(-dot(d, d) / (r * r));
                if (input.look.z > 0.5)
                {
                    // 十字のきらめき（復活）
                    float2 a = abs(d);
                    glow += (exp(-a.x / (r * 0.3)) * exp(-a.y / (r * 3.5)) + exp(-a.y / (r * 0.3)) * exp(-a.x / (r * 3.5))) * 0.6;
                }
                float3 c = input.seed.xyz * glow * input.look.x;
                return float4(c, saturate(max(c.r, max(c.g, c.b))));
            }
            ENDCG
        }
    }
}
