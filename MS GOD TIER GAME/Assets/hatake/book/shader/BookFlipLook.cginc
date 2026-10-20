#ifndef BOOK_FLIP_LOOK_INCLUDED
#define BOOK_FLIP_LOOK_INCLUDED

// =====================================================================
// 本の見た目：紙、ノドの陰影、めくりのカールと影、小口、表紙、魔法の光、
// 暗転とスポット。BookFlipCore.cginc の結果（BookFlipHit）に色を付ける。
// 長さの単位はすべてページ幅 = 1。
// =====================================================================

#define BOOK_MAX_SPOTS 8

// 紙
float4 _PaperTint;
sampler2D _PaperTex; float4 _PaperTex_ST;
float _PaperTexStrength, _PaperGrain, _PaperAge;
float4 _AgeColor;
sampler2D _PaperNormalTex; float4 _PaperNormalTex_ST;
float _PaperNormalStrength, _PaperBump, _PaperBumpScale;
float _InkBleed, _InkBleedRadius;
// 表紙の角金具
float4 _CornerColor;  // a = 表示
float _CornerSize;
// ノドと光
float _GutterWidth, _GutterDark;
float4 _LightDir;
float _SpecPower, _SpecStrength;
// めくり
float _CurlWidth, _CurlDark, _CurlSpec, _ShowThrough;
float _FoldShadow, _FoldShadowWidth, _DropShadow, _DropShadowWidth;
float4 _FoldGlowColor;
float _TurnGlow;
// 小口（ページの厚み）
float4 _Stack;       // x 左の厚み, y 右の厚み, z 枚数, w 下方向へのずれ比
float4 _StackColor;
float _StackLineDark;
// 表紙
float4 _CoverColor; sampler2D _CoverTex; float4 _CoverTex_ST;
float _BoardMode;    // 0 開いた本, 1 表紙側だけ（表紙を上に閉じている）, -1 裏表紙側だけ
float _CoverRadius, _CoverLeather, _CoverTrimInset, _CoverTrimWidth, _BookShadow;
float4 _CoverTrimColor;
// 魔法の装飾
float4 _MagicColor;
float _MagicStrength;
// 暗転とスポット
float _Dim;
float4 _DimColor, _SpotRimColor;
float4 _SpotRuneColor;           // a = 表示
float _SpotRuneWidth, _SpotRuneSpeed;
float _SpotCount;
float4 _Spots[BOOK_MAX_SPOTS];       // 本座標の中心 xy, 半サイズ zw
float4 _SpotParams[BOOK_MAX_SPOTS];  // x 形状(0楕円/1矩形), y 角丸, z ぼかし, w 強さ

float BookLookHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float BookLookNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3 - 2 * f);
    return lerp(lerp(BookLookHash(i), BookLookHash(i + float2(1, 0)), f.x),
                lerp(BookLookHash(i + float2(0, 1)), BookLookHash(i + 1), f.x), f.y);
}
float BookLookFbm(float2 p)
{
    return BookLookNoise(p) * 0.5 + BookLookNoise(p * 2.03) * 0.3 + BookLookNoise(p * 4.1) * 0.2;
}

float BookSdRoundBox(float2 p, float2 halfSize, float radius)
{
    float2 q = abs(p) - halfSize + radius;
    return length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
}

// ---------------------------------------------------------------------
// 紙の凹凸：法線テクスチャ（任意）と手続きの繊維。法線の傾き（xy）を返す
// ---------------------------------------------------------------------
float BookPaperHeight(float2 local)
{
    float2 q = local * _PaperBumpScale;
    // 縦に長い繊維と細かな粒
    return BookLookFbm(q * float2(0.35, 1.6)) * 0.6 + BookLookNoise(q * 1.7) * 0.4;
}

float2 BookPaperTilt(float2 flatPos)
{
    float2 local = float2(abs(flatPos.x), flatPos.y);
    float2 tilt = 0;
    if (_PaperBump > 0)
    {
        float e = 0.5 / max(_PaperBumpScale, 1);
        float h = BookPaperHeight(local);
        float2 grad = float2(BookPaperHeight(local + float2(e, 0)) - h,
                             BookPaperHeight(local + float2(0, e)) - h) / e;
        tilt -= grad * _PaperBump / max(_PaperBumpScale, 1);
    }
    if (_PaperNormalStrength > 0)
    {
        float3 n = UnpackNormal(tex2Dlod(_PaperNormalTex, float4(local * _PaperNormalTex_ST.xy + _PaperNormalTex_ST.zw, 0, 0)));
        tilt += n.xy * _PaperNormalStrength;
    }
    // 左ページは鏡像なので横の傾きを反転
    tilt.x *= flatPos.x >= 0 ? 1 : -1;
    return tilt;
}

// 光の向きに対する凹凸の明暗。base は凹凸を入れる前の法線
float BookRelief(float3 base, float3 bumped)
{
    float3 l = normalize(_LightDir.xyz);
    return 1 + (dot(bumped, l) - dot(base, l)) * 1.6;
}

// ---------------------------------------------------------------------
// インクのにじみ：周囲により暗い画素（インク）があれば、その色を毛細管状に滲ませる
// ---------------------------------------------------------------------
float BookLuma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

float3 BookInkBleed(float3 c, BookFlipHit h)
{
    if (_InkBleed <= 0) return c;
    float lum = BookLuma(c);
    float darkest = lum;
    float3 ink = c;
    float angle = BookLookHash(floor(h.uv * 512)) * 6.2832;
    [unroll] for (int i = 0; i < 6; i++)
    {
        float a = angle + i * 1.0472;
        float r = _InkBleedRadius * (0.55 + 0.45 * frac(i * 0.618 + angle));
        float2 offset = float2(cos(a), sin(a) / max(_PageAspect, 1e-3)) * r;
        float3 s = BookSamplePage(h.dest, h.right, saturate(h.uv + offset)).rgb;
        float l = BookLuma(s);
        if (l < darkest) { darkest = l; ink = s; }
    }
    float spread = saturate((lum - darkest) * 2.5);
    float fiber = BookLookFbm(h.uv * float2(70, 95) + h.right * 13);
    float amount = spread * _InkBleed * smoothstep(0.25, 0.75, fiber);
    return lerp(c, lerp(ink, c, 0.45), amount);
}

// ---------------------------------------------------------------------
// 紙の質感
// ---------------------------------------------------------------------
float3 BookPaper(float3 c, BookFlipHit h)
{
    float2 p = h.flatPos;
    float2 local = float2(abs(p.x), p.y);
    float3 tint = _PaperTint.rgb;
    float3 tex = tex2Dlod(_PaperTex, float4(local * _PaperTex_ST.xy + _PaperTex_ST.zw, 0, 0)).rgb;
    tint *= lerp(1, tex, _PaperTexStrength);
    float grain = BookLookNoise(local * 420) * 0.6 + BookLookFbm(local * float2(9, 60)) * 0.4;
    tint *= 1 - _PaperGrain * (grain - 0.5);
    c *= tint;

    // 外周の焼け：ページ端ほど古い紙の色に寄せる
    // 背側は焼けないので、外側・上下の端だけを見る
    float edge = min(1 - abs(p.x), min(p.y, _PageAspect - p.y));
    float age = (1 - smoothstep(0, 0.16, edge + (BookLookFbm(p * 7) - 0.5) * 0.08)) * _PaperAge;
    c = lerp(c, c * _AgeColor.rgb, saturate(age));

    // 魔導書の縁取り（BookModelAtlas と同じ意匠）
    if (_MagicStrength > 0)
    {
        float2 uv = h.uv;
        float2 e = min(uv, 1 - uv) * float2(1, _PageAspect);
        float rim = min(e.x, e.y);
        float border = 1 - smoothstep(0.0015, 0.004, abs(rim - 0.038));
        float inner = (1 - smoothstep(0.001, 0.0025, abs(rim - 0.052))) * 0.35;
        float corner = length(e - 0.075);
        float seal = (1 - smoothstep(0.001, 0.004, abs(corner - 0.027))) * step(rim, 0.11);
        float pulse = 0.75 + 0.25 * sin(_Time.y * 1.5 + uv.y * 8);
        c = lerp(c, _MagicColor.rgb * pulse, saturate(border + inner + seal) * _MagicStrength * 0.85);
    }
    return c;
}

// ---------------------------------------------------------------------
// ノドの陰影と疑似法線による光（元のShadertoyの fakeSpine / fakeNormal / specular）
// ---------------------------------------------------------------------
float3 BookSpecular(float3 normal, float3 viewDir)
{
    float3 halfV = normalize(normalize(_LightDir.xyz) + viewDir);
    return pow(max(dot(normal, halfV), 0), _SpecPower) * _SpecStrength;
}

float3 BookGutter(float3 c, float x, out float3 normal)
{
    float t = saturate(abs(x) / max(_GutterWidth, 1e-4));
    float bend = 1 - t;
    // ノドに向かって紙が沈み込む。左右で傾きが逆
    normal = normalize(float3(-sign(x) * bend * bend * 1.4, 0, 1));
    c *= lerp(_GutterDark, 1, pow(t, 0.5));
    return c;
}

// ---------------------------------------------------------------------
// 本の外側：小口（ページの厚み）と表紙、本の影
// ---------------------------------------------------------------------
float4 BookStack(float2 p)
{
    bool closed = abs(_BoardMode) > 0.5;
    float side = closed ? (_BoardMode > 0 ? 1 : -1) : (p.x >= 0 ? 1 : -1);
    float thick = side > 0 ? _Stack.y : _Stack.x;
    if (thick <= 1e-5) return 0;
    float2 q = float2(p.x * side, p.y);
    float layers = max(_Stack.z, 1);
    float dx = thick / layers;
    float dy = dx * _Stack.w;
    // ページの束は、ページ（閉じた本では表紙の板と板の厚み）の外側と下に沿った L 字の帯。
    // 左端（背の側）と上端は板とそろえ、ずらさない
    float inner = 0, outer = 1, bottom = 0, top = _PageAspect;
    if (closed)
    {
        inner = -BOOK_SPINE_HALF;
        outer = 1 + _CoverMargin.x + _BoardThickness;
        bottom = -_CoverMargin.y - _BoardThickness * 0.7;
        top = _PageAspect + _CoverMargin.y;
    }
    float k = max(max((q.x - outer) / dx, (bottom - q.y) / max(dy, 1e-5)), 0);
    if (q.x < inner || k <= 0 || k > layers || q.y > top) return 0;
    float edgeLine = smoothstep(0.55, 0.95, frac(k));
    float3 c = _StackColor.rgb * lerp(1, 0.55, k / layers) * (1 - _StackLineDark * edgeLine);
    c *= 0.9 + 0.1 * BookLookNoise(float2(k * 3.1, q.y * 30));
    return float4(c, 1);
}

// 表紙の四隅の角金具（三角の板、彫りの線、鋲）
float3 BookCornerMetal(float3 c, float2 local, float2 halfSize, float coverSd)
{
    if (_CornerColor.a <= 0) return c;
    float2 q = halfSize - abs(local);      // 最寄りの角から内側への距離
    float size = max(_CornerSize, 1e-3);
    float plate = size - (q.x + q.y) * 0.7071;
    // 板の外周に落ちる影
    c *= 1 - 0.45 * (1 - smoothstep(0, 0.012, -plate)) * step(plate, 0) * _CornerColor.a;
    if (plate <= 0) return c;

    float2 dirToCorner = sign(local);
    float3 normal = normalize(float3(dirToCorner * 0.35 * (1 - smoothstep(0, 0.02, plate)), 1));
    float3 l = normalize(_LightDir.xyz);
    float scratch = BookLookNoise(float2((q.x - q.y) * 260, (q.x + q.y) * 18)) * 0.25;
    float diffuse = 0.55 + 0.45 * saturate(dot(normal, l)) - scratch * 0.3;
    float sweep = pow(saturate(1 - abs(frac((q.x - q.y) * 3 + _Time.y * 0.15) - 0.5) * 2), 8);
    float3 metal = _CornerColor.rgb * diffuse + _CornerColor.rgb * sweep * 0.6;
    // 彫りの線と鋲
    float engrave = 1 - smoothstep(0, 0.002, abs(plate - 0.014));
    metal *= 1 - engrave * 0.55;
    float rivet = length(q - size * 0.22) - 0.009;
    if (rivet < 0) metal = _CornerColor.rgb * (1.1 + 0.6 * saturate(-rivet / 0.009) * saturate(dot(normalize(float3(-(q - size * 0.22) * 60, 1)), l)));
    float edgeFade = smoothstep(0, 0.004, plate) * smoothstep(0, 0.004, -coverSd + 0.001);
    return lerp(c, metal, edgeFade * _CornerColor.a);
}

// 表紙の面：絵（あれば）に革の質感を重ねる。絵が無ければ革の色
float3 BookCoverSurface(float3 picture, float2 local)
{
    float leather = BookLookFbm(local * 18) * 0.6 + BookLookNoise(local * 140) * 0.4;
    float3 c = picture.r < 0 ? _CoverColor.rgb * tex2Dlod(_CoverTex, float4(local * _CoverTex_ST.xy + _CoverTex_ST.zw, 0, 0)).rgb
                             : picture;
    float grain = picture.r < 0 ? _CoverLeather : _CoverLeather * 0.35;
    return c * (1 - grain * (leather - 0.5));
}

// 表紙の板の内側の色（表紙の絵の色をならしたもの。絵が無ければ革）
float3 BookBoardInside(bool frontCover, float2 p)
{
    float3 picture = float3(-1, -1, -1);
    float2 uv = BookCoverUV(float2(-p.x, p.y), frontCover);
    if ((frontCover ? _CoverFlags.x : _CoverFlags.y) > 0.5)
        picture = (frontCover ? tex2Dlod(_FrontCoverTex, float4(uv, 0, 7)).rgb
                              : tex2Dlod(_BackCoverTex, float4(uv, 0, 7)).rgb) * 0.8;
    return BookCoverSurface(picture, p);
}

// 表紙の板の範囲。開いた本は見開き全体、閉じた本は片側
void BookBoardBox(out float2 center, out float2 halfSize)
{
    if (abs(_BoardMode) > 0.5)
    {
        BookCoverBox(_BoardMode > 0, center, halfSize);
        return;
    }
    center = float2(0, _PageAspect * 0.5);
    halfSize = float2(1 + _CoverMargin.x, _PageAspect * 0.5 + _CoverMargin.y);
}

// 表紙の外周の金の飾り罫。sd は板の外形からの距離（内側が負）
float3 BookCoverTrim(float3 c, float2 p, float sd)
{
    float trim = 1 - smoothstep(0, _CoverTrimWidth, abs(sd + _CoverTrimInset));
    float shimmer = 0.8 + 0.2 * sin(_Time.y * 2 + (p.x + p.y) * 6);
    return lerp(c, _CoverTrimColor.rgb * shimmer, trim * _CoverTrimColor.a);
}

// 表紙の板の小口（厚みの面）の色。along は辺に沿った位置
float3 BookBoardEdgeColor(float along, float across)
{
    float3 c = _CoverColor.rgb * 0.45 * (0.85 + 0.15 * BookLookNoise(float2(along * 60, across * 4)));
    // 表側の角に細い明るい線
    c += _CoverColor.rgb * 0.35 * (1 - smoothstep(0, 0.25, across));
    return c;
}

float4 BookCover(float2 p)
{
    float2 center, halfSize;
    BookBoardBox(center, halfSize);
    float sd = BookSdRoundBox(p - center, halfSize, _CoverRadius);
    // 閉じた本では板の厚み（小口）を描かない
    if (sd > 0 && _BoardThickness > 0 && abs(_BoardMode) < 0.5)
    {
        // 板の厚み：外側と下へ広げた板（背の側と上端はそろえる）から、板を除いた L 字の帯が小口
        float outward = abs(_BoardMode) > 0.5 ? (_BoardMode > 0 ? 1 : -1) : (p.x >= center.x ? 1 : -1);
        float2 grow = float2(_BoardThickness, _BoardThickness * 0.7) * 0.5;
        float edgeSd = BookSdRoundBox(p - center - float2(outward * grow.x, -grow.y), halfSize + grow, _CoverRadius);
        if (edgeSd <= 0)
            return float4(BookBoardEdgeColor(p.x + p.y, saturate(sd / max(_BoardThickness, 1e-4))), 1);
    }
    if (sd > 0)
    {
        // 本が背景に落とす影
        float shadowSd = BookSdRoundBox(p - center - float2(0.02, -0.035), halfSize, _CoverRadius);
        float a = (1 - smoothstep(0, 0.09, shadowSd)) * _BookShadow;
        return float4(0, 0, 0, a);
    }
    float2 local = p - center;
    // 閉じた本は表紙の絵を板全体に貼る
    float3 picture = float3(-1, -1, -1);
    bool closed = abs(_BoardMode) > 0.5;
    if (closed)
    {
        bool front = _BoardMode > 0;
        float2 coverUV = BookCoverUV(p, front);
        if ((front ? _CoverFlags.x : _CoverFlags.y) > 0.5)
            picture = front ? tex2Dlod(_FrontCoverTex, float4(coverUV, 0, 0)).rgb : tex2Dlod(_BackCoverTex, float4(coverUV, 0, 0)).rgb;
    }
    else
    {
        // 開いた本の縁は表紙の内側：表紙の絵の色だけを使う（文字や模様は内側に出ない）。
        // 低い解像度のミップで読んで色をならす。左は表紙、右は裏表紙
        bool front = p.x < 0;
        float2 insideUV = BookCoverUV(float2(-p.x, p.y), front);
        const float insideMip = 7;
        if ((front ? _CoverFlags.x : _CoverFlags.y) > 0.5)
            picture = (front ? tex2Dlod(_FrontCoverTex, float4(insideUV, 0, insideMip)).rgb
                             : tex2Dlod(_BackCoverTex, float4(insideUV, 0, insideMip)).rgb) * 0.8;
    }
    float3 c = BookCoverSurface(picture, local);
    // 縁の面取りと背の陰
    float bevel = smoothstep(-0.03, 0, sd);
    c *= lerp(1, 0.55, bevel);
    c += _CoverColor.rgb * 0.25 * (1 - smoothstep(-0.012, -0.004, sd)) * smoothstep(-0.03, -0.012, sd);
    c *= lerp(0.6, 1, smoothstep(0, 0.06, abs(p.x)));
    // 絵を貼った表紙には金の罫を引かない（絵の意匠を優先）
    if (picture.r < 0) c = BookCoverTrim(c, p, sd);
    c = BookCornerMetal(c, local, halfSize, sd);
    return float4(c, 1);
}

float4 BookOutside(float2 p)
{
    float4 stack = BookStack(p);
    if (stack.a > 0) return stack;
    return BookCover(p);
}

// ---------------------------------------------------------------------
// 暗転とスポット
// ---------------------------------------------------------------------
float BookSpotMask(float2 p, out float rim)
{
    float mask = 0;
    rim = 0;
    [unroll] for (int k = 0; k < BOOK_MAX_SPOTS; k++)
    {
        if (k < (int)_SpotCount)
        {
            float4 spot = _Spots[k];
            float4 param = _SpotParams[k];
            float2 halfSize = max(spot.zw, 1e-4);
            float2 d = p - spot.xy;
            float sd = param.x < 0.5
                ? (length(d / halfSize) - 1) * min(halfSize.x, halfSize.y)
                : BookSdRoundBox(d, halfSize, min(param.y, min(halfSize.x, halfSize.y)));
            float feather = max(param.z, 1e-4);
            mask = max(mask, (1 - smoothstep(-feather, 0, sd)) * param.w);
            rim = max(rim, exp(-abs(sd) / 0.006) * param.w);
        }
    }
    return saturate(mask);
}

// スポットの外側を回るルーンの輪。cell 内の座標 (u 周方向, v 半径方向) に図形を組み合わせる
float BookRuneGlyph(float2 uv, float id)
{
    float h = BookLookHash(float2(id, 7.31));
    float2 c = uv - 0.5;
    float w = 0.07;
    float g = 0;
    if (frac(h * 2) > 0.35) g = max(g, (1 - smoothstep(w, w + 0.05, abs(c.x))) * step(abs(c.y), 0.32));
    if (frac(h * 4) > 0.55) g = max(g, (1 - smoothstep(w, w + 0.05, abs(c.y - 0.18 * sign(h - 0.5)))) * step(abs(c.x), 0.28));
    if (frac(h * 8) > 0.5) g = max(g, (1 - smoothstep(w, w + 0.05, abs(c.x - c.y * (frac(h * 16) > 0.5 ? 0.8 : -0.8)))) * step(length(c), 0.36));
    if (frac(h * 32) > 0.7) g = max(g, 1 - smoothstep(w * 0.8, w * 0.8 + 0.05, abs(length(c) - 0.2)));
    if (frac(h * 64) > 0.8) g = max(g, 1 - smoothstep(0.05, 0.1, length(c - float2(0, 0.3))));
    return g;
}

float BookSpotRunes(float2 p)
{
    float rune = 0;
    float width = max(_SpotRuneWidth, 1e-3);
    [unroll] for (int k = 0; k < BOOK_MAX_SPOTS; k++)
    {
        if (k < (int)_SpotCount)
        {
            float4 spot = _Spots[k];
            float4 param = _SpotParams[k];
            float2 halfSize = max(spot.zw, 1e-4);
            float2 d = p - spot.xy;
            float sd = param.x < 0.5
                ? (length(d / halfSize) - 1) * min(halfSize.x, halfSize.y)
                : BookSdRoundBox(d, halfSize, min(param.y, min(halfSize.x, halfSize.y)));
            float v = (sd - width * 0.25) / width;   // 0〜1 がルーンの帯
            if (v > -0.3 && v < 1.3)
            {
                float2 n = param.x < 0.5 ? d / halfSize : d;
                float dir = (k & 1) == 0 ? 1 : -1;
                float angle = atan2(n.y, n.x) / 6.2832 + _Time.y * _SpotRuneSpeed * dir;
                float perimeter = 6.2832 * (max(halfSize.x, halfSize.y) + sd);
                float count = max(6, floor(perimeter / (width * 0.85)));
                float cell = angle * count;
                float glyph = BookRuneGlyph(float2(frac(cell), saturate(v)), floor(cell) + k * 17) * step(0, v) * step(v, 1);
                float lines = (1 - smoothstep(0.02, 0.07, abs(v))) + (1 - smoothstep(0.02, 0.07, abs(v - 1))) * 0.7;
                rune = max(rune, max(glyph, lines) * param.w);
            }
        }
    }
    return rune;
}

float4 BookApplyDim(float4 col, float2 p)
{
    if (_Dim <= 0) return col;
    float rim;
    float mask = BookSpotMask(p, rim);
    col.rgb = lerp(col.rgb, col.rgb * _DimColor.rgb, _Dim * (1 - mask));
    col.rgb += _SpotRimColor.rgb * rim * _Dim * col.a;
    if (_SpotRuneColor.a > 0)
    {
        float rune = BookSpotRunes(p) * _Dim * _SpotRuneColor.a;
        float pulse = 0.75 + 0.25 * sin(_Time.y * 3);
        col.rgb += _SpotRuneColor.rgb * rune * pulse;
        col.a = max(col.a, saturate(rune));
    }
    return col;
}

// 暗転時のぼかし量（Post の合成で使用）
float BookDimBlurMask(float2 p)
{
    if (_Dim <= 0) return 0;
    float rim;
    return _Dim * (1 - BookSpotMask(p, rim));
}

#endif
