Shader "custom/hatake/Book/Page Flip Composite"
{
    // RenderTextureで撮影した見開きを合成し、ページめくりと本の装飾を描く。
    // BookFlipView が Graphics.Blit で出力RenderTextureへ描画する。
    // 値の意味と単位は BookFlip.md を参照。長さはページ幅 = 1。
    Properties
    {
        [Header(Paper)]
        _PaperTint ("Paper tint", Color) = (1, 0.96, 0.88, 1)
        _PaperTex ("Paper texture (optional)", 2D) = "white" {}
        _PaperTexStrength ("Paper texture strength", Range(0, 1)) = 0
        _PaperGrain ("Paper grain", Range(0, 0.5)) = 0.08
        _PaperAge ("Edge aging", Range(0, 1)) = 0.45
        _AgeColor ("Aged edge color", Color) = (0.78, 0.58, 0.36, 1)
        _PaperBump ("Paper fiber relief", Range(0, 2)) = 0.22
        _PaperBumpScale ("Paper fiber scale", Range(20, 600)) = 160
        [Normal] _PaperNormalTex ("Paper normal map (optional)", 2D) = "bump" {}
        _PaperNormalStrength ("Paper normal map strength", Range(0, 2)) = 0
        _InkBleed ("Ink bleed", Range(0, 1)) = 0.45
        _InkBleedRadius ("Ink bleed radius (page UV)", Range(0.001, 0.02)) = 0.006

        [Header(Gutter and Light)]
        _GutterWidth ("Gutter width", Range(0.01, 0.5)) = 0.14
        _GutterDark ("Gutter darkness", Range(0, 1)) = 0.35
        _LightDir ("Light direction", Vector) = (5, 3, 1, 0)
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
        [HideInInspector] _SpotCount ("Spot count", Float) = 0
        [HideInInspector] _Rigid ("Rigid cover turn", Float) = 0
        [HideInInspector] _RigidCover ("Rigid cover", Float) = 1
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
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "BookFlipCore.cginc"
            #include "BookFlipLook.cginc"
            #include "BookFlipEffects.cginc"

            struct VertexInput { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct VertexOutput { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            VertexOutput Vert(VertexInput input)
            {
                VertexOutput output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float3 ShadePage(BookFlipHit h, float3 c, float2 extraTilt)
            {
                // 表紙・裏表紙は硬い板：紙の質感・ノドの陰は付けない
                bool cover = (h.kind > 0.5 && h.kind < 2.5) || h.kind > 4.5;
                float3 normal = float3(0, 0, 1);
                if (h.kind > 4.5)
                {
                    // 表紙・裏表紙の内側（最初の左ページ・最後の右ページの位置）
                    c = BookBoardInside(h.kind < 5.5, h.flatPos);
                }
                else if (cover)
                {
                    float2 boxCenter, boxHalf;
                    BookCoverBox(h.kind < 1.5, boxCenter, boxHalf);
                    c = BookCoverSurface(c, h.flatPos - boxCenter);
                }
                else
                {
                    c = BookInkBleed(c, h);
                    c = BookPaper(c, h);
                    c = BookGutter(c, h.flatPos.x, normal);
                }

                bool turning = h.fold < 1e2;
                // めくり始めは角だけが小さく反るので、カールの幅を角の移動量に合わせる
                float curlWidth = max(_CurlWidth * saturate(h.foldLength * 1.5), 1e-4);
                float turnFade = saturate(h.foldLength * 4);

                if (h.layer == BOOK_LAYER_BACK)
                {
                    // 裏から透ける表面の印刷（紙の元の位置で読む）
                    float frontRight = h.sheetPos.x >= 0 ? 1 : 0;
                    if (!cover && BookSlotKind(0, frontRight) < 0.5)
                    {
                        float3 through = BookSamplePage(0, frontRight, saturate(BookPageUV(h.sheetPos))).rgb;
                        c *= lerp(1, saturate(through * 1.15), _ShowThrough);
                    }

                    float dd = h.rigid > 0.5 ? 1e3 : -h.fold;   // 硬い板は曲がらない
                    float bend = 1 - smoothstep(0, curlWidth, dd);
                    c *= lerp(1, _CurlDark, bend * bend * turnFade);
                    normal = normalize(float3(h.foldNormal * bend * 1.2, 1));
                    float band = (dd - curlWidth * 0.45) / (curlWidth * 0.22);
                    c += _CurlSpec * exp(-band * band) * turnFade;
                    if (h.rigid < 0.5) c *= lerp(0.85, 1, smoothstep(0, 0.012, h.flapInside));
                }
                else if (h.layer == BOOK_LAYER_FRONT && turning)
                {
                    // 折り目の手前で紙が持ち上がり始める
                    float bend = 1 - smoothstep(0, curlWidth * 0.6, -h.fold);
                    c *= lerp(1, 0.8, bend * turnFade);
                }
                else if (h.layer == BOOK_LAYER_UNDER && turning)
                {
                    c *= 1 - _FoldShadow * exp(-h.fold / _FoldShadowWidth) * turnFade;
                }

                // 紙の繊維の凹凸（と燃える縁の反り）で法線を傾け、光の向きで明暗を付ける
                float3 baseNormal = normal;
                float2 paperTilt = cover ? float2(0, 0) : BookPaperTilt(h.flatPos);
                normal = normalize(normal + float3(paperTilt + extraTilt, 0));
                c *= BookRelief(baseNormal, normal);

                c += min(1, BookSpecular(normal, float3(0, 0, 1)));
                return c;
            }

            float4 Frag(VertexOutput input) : SV_Target
            {
                float2 p = BookFromUV(input.uv);
                BookFlipHit h = BookFlipResolve(p);
                // 閉じた本の反対側（何も無い面）は本の外として描く
                h.kind = h.layer == BOOK_LAYER_NONE ? 0 : BookSlotKind(h.dest, h.right);
                if ((h.layer == BOOK_LAYER_FLAT || h.layer == BOOK_LAYER_UNDER) && abs(h.kind - BOOK_SLOT_EMPTY) < 0.5) h.layer = BOOK_LAYER_NONE;

                // 燃焼／復活：境界の内側は変化後の見開きを見せる
                bool fxPage = h.layer == BOOK_LAYER_FLAT && BookFxOnPage(p);
                float fxEdge = fxPage ? BookFxEdge(p) : 0;
                if (fxPage && fxEdge > 0) h.dest = 1;
                float2 curlShift = 0, curlTilt = 0;
                if (fxPage)
                {
                    // && は短絡しないので、対象ページ以外で勾配を計算しないよう分ける
                    if (BookFxCurl(p, fxEdge, curlShift, curlTilt) > 0) h.uv = saturate(h.uv + curlShift);
                }

                float4 col;
                if (h.layer == BOOK_LAYER_NONE) col = BookOutside(p);
                else if (h.rigidEdge > 0.5)
                {
                    // 回っている板の小口
                    col = float4(BookBoardEdgeColor(h.flatPos.y, 0.5), 1);
                }
                else if (h.rigidMargin > 0.5)
                {
                    // 硬い表紙の板のページより外：表側なら表紙の絵、内側なら表紙の内側の色
                    bool coverFace = h.kind > 0.5 && h.kind < 2.5;
                    float2 boxCenter, boxHalf;
                    BookCoverBox(h.kind < 1.5, boxCenter, boxHalf);
                    float3 picture = float3(-1, -1, -1);
                    if (coverFace && (h.kind < 1.5 ? _CoverFlags.x : _CoverFlags.y) > 0.5)
                    {
                        float2 coverUV = BookCoverUV(h.flatPos, h.kind < 1.5);
                        picture = h.kind < 1.5 ? tex2Dlod(_FrontCoverTex, float4(coverUV, 0, 0)).rgb : tex2Dlod(_BackCoverTex, float4(coverUV, 0, 0)).rgb;
                    }
                    col = float4(coverFace ? BookCoverSurface(picture, h.flatPos - boxCenter)
                                           : BookBoardInside(_RigidCover < 1.5, h.flatPos), 1);
                    // 外周の金の罫（絵が無い表紙）も板と一緒に回る
                    if (coverFace && picture.r < 0)
                        col.rgb = BookCoverTrim(col.rgb, h.flatPos, BookSdRoundBox(h.flatPos - boxCenter, boxHalf, _CoverRadius));
                }
                else col = float4(ShadePage(h, BookSamplePage(h.dest, h.right, h.uv).rgb, curlTilt), 1);
                // 角金具は表紙・裏表紙の板に付いて一緒に回る。表側（絵の面）は全体、内側は縁の部分だけ
                if (h.rigid > 0.5 && h.rigidEdge < 0.5)
                {
                    bool coverFace = h.kind > 0.5 && h.kind < 2.5;
                    if (coverFace || h.rigidMargin > 0.5)
                    {
                        float2 boxCenter, boxHalf;
                        BookCoverBox(h.flatPos.x >= 0, boxCenter, boxHalf);
                        float boxSd = BookSdRoundBox(h.flatPos - boxCenter, boxHalf, _CoverRadius);
                        col.rgb = BookCornerMetal(col.rgb, h.flatPos - boxCenter, boxHalf, boxSd);
                    }
                }
                if (h.rigid > 0.5) col.rgb *= h.rigidShade;
                if (fxPage) col.rgb = BookFxShadePage(col.rgb, p, fxEdge);
                // 閉じた表紙の上の角金具（板の縁はページの外で BookCover が描く）
                if (h.layer == BOOK_LAYER_FLAT && h.kind > 0.5 && h.kind < 2.5)
                {
                    float2 boxCenter, boxHalf;
                    BookCoverBox(h.kind < 1.5, boxCenter, boxHalf);
                    float boxSd = BookSdRoundBox(p - boxCenter, boxHalf, _CoverRadius);
                    col.rgb = BookCornerMetal(col.rgb, p - boxCenter, boxHalf, boxSd);
                }

                // めくれた紙が下の紙・表紙・背景に落とす影
                if (h.layer != BOOK_LAYER_BACK && h.flapOutside < 1e2)
                {
                    float shadow = (1 - smoothstep(0, _DropShadowWidth, h.flapOutside)) * _DropShadow * saturate(h.foldLength * 4);
                    col.rgb *= 1 - shadow;
                    col.a = max(col.a, shadow);
                }

                // 魔法の光：折り目とめくれた紙の縁
                if (_TurnGlow > 0 && h.fold < 1e2 && h.layer != BOOK_LAYER_NONE)
                {
                    float glow = exp(-abs(h.fold) / 0.012);
                    if (h.layer == BOOK_LAYER_BACK) glow = max(glow, exp(-h.flapInside / 0.008));
                    float along = dot(p, float2(-h.foldNormal.y, h.foldNormal.x));
                    float sparkle = BookLookNoise(float2(along * 38 - _Time.y * 5, _Time.y * 0.7));
                    col.rgb += _FoldGlowColor.rgb * _TurnGlow * glow * (0.35 + 0.65 * sparkle * sparkle);
                }

                col = BookApplyDim(col, p);

                // 燃えかすと粒は暗転の上に描く
                float4 ash = BookFxAsh(p);
                col.rgb = col.rgb * (1 - ash.a) + ash.rgb;
                col.a = max(col.a, ash.a);

                float3 particles = BookFxParticles(p);
                col.rgb += particles;
                col.a = max(col.a, saturate(max(particles.r, max(particles.g, particles.b))));
                return col;
            }
            ENDCG
        }
    }
}
