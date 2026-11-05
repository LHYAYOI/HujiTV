// BookFlipComposite の各パスで共有するコード。パスごとに BOOK_PASS_PAGE / BOOK_PASS_COVER / BOOK_FX を
// 定義してから include する（定義によって使わないコードを書き出さない）
#ifndef BOOK_FLIP_COMPOSITE_COMMON_INCLUDED
#define BOOK_FLIP_COMPOSITE_COMMON_INCLUDED
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
                // 表紙・裏表紙の表側（絵の面）は硬い板：紙の質感・ノドの陰は付けない。
                // 表紙・裏表紙は片面印刷なので、内側（最初の左ページ・最後の右ページの位置）は
                // 絵を出さず、見返しの紙（印刷の無い色紙）を貼った面として描く
                // 分割パスでは面の種類が決まっているので、使わない方のコードを書き出さない
            #if defined(BOOK_PASS_PAGE)
                bool printedCover = false;
                bool endpaper = h.kind > 4.5;
            #elif defined(BOOK_PASS_COVER)
                bool printedCover = true;
                bool endpaper = false;
            #else
                bool printedCover = h.kind > 0.5 && h.kind < 2.5;
                bool endpaper = h.kind > 4.5;
            #endif
                bool cover = printedCover || endpaper;
                // 絵を貼った表紙（c が負でない）はマットPP貼りとして弱いつやにする。強い照り返しは革だけ
                bool coverPicture = printedCover && c.r >= 0;
                float3 normal = float3(0, 0, 1);
                if (endpaper)
                {
                    c = BookPaperBase(c, h.flatPos, true);
                    c = BookGutter(c, h.flatPos.x, normal);
                }
                else if (printedCover)
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
                float2 paperTilt = float2(0, 0);
                if (!printedCover) paperTilt = BookPaperTilt(h.flatPos);
                normal = normalize(normal + float3(paperTilt + extraTilt, 0));
                c *= BookRelief(baseNormal, normal);

                // つや：革の表紙は板の照り返し、絵を貼った表紙と見返しはマットの弱いつや、本文の紙は紙の仕上げ（マット／グロス）のつや
                // ?: は両辺を計算するので if で分ける
                if (printedCover && !coverPicture) c += min(1, BookSpecular(normal, BookViewDir()));
                else c += min(1, BookPaperSheen(normal, h.flatPos, !cover));
                return c;
            }


            // 画素の面を決める（めくり・燃焼の境界を含む）。kind は燃焼で dest を変える前の面
            BookFlipHit BookResolveHit(float2 p, out bool fxPage, out float fxEdge, out float2 curlTilt)
            {
                BookFlipHit h = BookFlipResolve(p);
                // 閉じた本の反対側（何も無い面）は本の外として描く
                h.kind = h.layer == BOOK_LAYER_NONE ? 0 : BookSlotKind(h.dest, h.right);
                if ((h.layer == BOOK_LAYER_FLAT || h.layer == BOOK_LAYER_UNDER) && abs(h.kind - BOOK_SLOT_EMPTY) < 0.5) h.layer = BOOK_LAYER_NONE;
                fxPage = false;
                fxEdge = 0;
                curlTilt = 0;
            #if defined(BOOK_FX)
                // 燃焼／復活：境界の内側は変化後の見開きを見せる
                fxPage = h.layer == BOOK_LAYER_FLAT && BookFxOnPage(p);
                fxEdge = fxPage ? BookFxEdge(p) : 0;
                if (fxPage && fxEdge > 0) h.dest = 1;
                if (fxPage)
                {
                    // && は短絡しないので、対象ページ以外で勾配を計算しないよう分ける
                    float2 curlShift;
                    if (BookFxCurl(p, fxEdge, curlShift, curlTilt) > 0) h.uv = saturate(h.uv + curlShift);
                }
            #endif
                return h;
            }

            // 表紙の面か（印刷した表紙、回る板の小口・縁）。分割パスの振り分けに使う
            bool BookIsCoverPixel(BookFlipHit h)
            {
                return h.rigidEdge > 0.5 || h.rigidMargin > 0.5 || (h.kind > 0.5 && h.kind < 2.5);
            }

            // 面の色（角金具・板の明るさ・燃焼の色まで）。この後に BookFinish を掛ける
            float4 BookBaseColor(BookFlipHit h, float2 p, bool fxPage, float fxEdge, float2 curlTilt)
            {
                float4 col;
            #if defined(BOOK_PASS_PAGE)
                col = float4(ShadePage(h, BookSamplePage(h.dest, h.right, h.uv).rgb, curlTilt), 1);
            #else
            #if !defined(BOOK_PASS_COVER)
                if (h.layer == BOOK_LAYER_NONE) col = BookOutside(p);
                else
            #endif
                if (h.rigidEdge > 0.5)
                {
                    // 回っている板の小口
                    col = float4(BookBoardEdgeColor(h.sheetPos.x + h.sheetPos.y, saturate(h.flapInside)), 1);
                }
                else if (h.rigidMargin > 0.5)
                {
                    // 硬い表紙の板のページより外：表側なら閉じた表紙の縁、内側なら開いた本の縁（着地した位置で）。
                    // 置いてある板と同じ関数で描くので、金の罫・角金具・革の模様が着地の前後で変わらない
                    // 内側の縁には、その側のページの束（小口）も板に載って一緒に回る
                    bool coverFace = h.kind > 0.5 && h.kind < 2.5;
                    float4 carried = coverFace ? float4(0, 0, 0, 0) : BookStackCarried(h.flatPos);
                    if (carried.a > 0) col = carried;
                    else if (coverFace) col = float4(BookClosedBoardColor(h.flatPos, h.kind < 1.5), 1);
                    else col = float4(BookOpenBoardColor(h.flatPos), 1);
                }
                else col = float4(ShadePage(h, BookSamplePage(h.dest, h.right, h.uv).rgb, curlTilt), 1);
                // 角金具：回っている板の表側（全体）と、閉じた表紙の上（板の縁はページの外で BookCover が描く）。
                // 2つは同時に起きないので、位置だけ選んで1回だけ描く（同じ大きな処理を2回書き出さない）
                bool coverFaceKind = h.kind > 0.5 && h.kind < 2.5;
                bool rigidMetal = h.rigid > 0.5 && h.rigidEdge < 0.5 && h.rigidMargin < 0.5 && coverFaceKind;
                bool flatMetal = h.layer == BOOK_LAYER_FLAT && coverFaceKind;
                if (rigidMetal || flatMetal)
                {
                    float2 metalPos = rigidMetal ? h.flatPos : p;
                    float2 boxCenter, boxHalf;
                    BookCoverBox(rigidMetal ? h.flatPos.x >= 0 : h.kind < 1.5, boxCenter, boxHalf);
                    float boxSd = BookSdRoundBox(metalPos - boxCenter, boxHalf, _CoverRadius);
                    col.rgb = BookCornerMetal(col.rgb, metalPos - boxCenter, boxHalf, boxSd);
                }
            #endif
                if (h.rigid > 0.5) col.rgb *= h.rigidShade;
            #if defined(BOOK_FX)
                if (fxPage) col.rgb = BookFxShadePage(col.rgb, p, fxEdge);
            #endif
                return col;
            }

            // めくりの落ち影と魔法の光（姿勢の明暗の後、暗転の前に重ねる）
            float4 BookTurnOverlay(float4 col, BookFlipHit h, float2 p)
            {
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
                #if defined(BOOK_SPLIT)
                    // 折り目に沿った1次元のきらめき（2次元のノイズは高いので、ハッシュ2回の値ノイズ）
                    float x = along * 38 - _Time.y * 5;
                    float i = floor(x), f = frac(x);
                    float t = _Time.y * 0.7;
                    float a = frac(sin(i * 127.1 + floor(t) * 311.7) * 43758.5453);
                    float b = frac(sin((i + 1) * 127.1 + floor(t) * 311.7) * 43758.5453);
                    float sparkle = lerp(a, b, f * f * (3 - 2 * f));
                #else
                    float sparkle = BookLookNoise(float2(along * 38 - _Time.y * 5, _Time.y * 0.7));
                #endif
                    col.rgb += _FoldGlowColor.rgb * _TurnGlow * glow * (0.35 + 0.65 * sparkle * sparkle);
                }

                return col;
            }

            // 面の色に重ねる仕上げ：姿勢の明暗・落ち影・魔法の光・暗転・燃えかす・画面フェード
            float4 BookFinish(float4 col, BookFlipHit h, float2 p)
            {
                // 本の姿勢による明暗（光の向きは一定、本が光へ向く・背ける）。魔法の光・暗転・燃えかすはこの後に重ねる（自ら光るので暗くしない）
                col.rgb *= BookPoseShade();

                col = BookTurnOverlay(col, h, p);

                col = BookApplyDim(col, p);

            #if defined(BOOK_FX)
                // 燃えかすと粒は暗転の上に描く
                float4 ash = BookFxAsh(p);
                col.rgb = col.rgb * (1 - ash.a) + ash.rgb;
                col.a = max(col.a, ash.a);

                float3 particles = BookFxParticles(p);
                col.rgb += particles;
                col.a = max(col.a, saturate(max(particles.r, max(particles.g, particles.b))));
            #endif

                // 画面フェード（後処理を使うときは BookFlipView が 0 にし、後処理の最後で掛ける）
                return BookApplyScreenFade(col);
            }

            // 分割パスの面の色（1〜3）を読む
            BOOK_DECLARE_TEX(_BookBaseTex)
#endif
