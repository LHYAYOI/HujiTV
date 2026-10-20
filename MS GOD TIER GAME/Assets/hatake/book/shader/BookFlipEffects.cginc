#ifndef BOOK_FLIP_EFFECTS_INCLUDED
#define BOOK_FLIP_EFFECTS_INCLUDED

// =====================================================================
// ページの変化演出：燃焼（Burn）と復活（Revive）
//   どちらも _FxOrigin から広がる境界で、現在の見開き（_Cur*）を
//   変化後の見開き（_Dst*）へ置き換える。
//   Burn  ：燃えた穴から下のページが見える。焦げ・火の縁・火の粉。
//   Revive：光の縁から新しいページが組み上がる。光の粒。
// BookFlipCore.cginc と BookFlipLook.cginc の後に include する。
// =====================================================================

#define BOOK_FX_NONE   0
#define BOOK_FX_BURN   1
#define BOOK_FX_REVIVE 2

float _FxMode, _FxSide, _FxProgress;
float4 _FxOrigin;  // xy 起点（本の座標）, z 起点から最も遠い角までの距離, w 乱数の種
float4 _BurnCharColor, _BurnEmberColor;
float _BurnEdgeWidth;
float4 _ReviveColor;
float _ReviveEdgeWidth;
float _FxParticles;
float _FxAsh, _BurnCurl;

bool BookFxOnPage(float2 p)
{
    if (_FxMode < 0.5) return false;
    // _FxSide：1 右ページ、-1 左ページ、0 見開き両面
    bool side = _FxSide > 0.5 ? p.x >= 0 : (_FxSide < -0.5 ? p.x < 0 : true);
    return side && BookPageInside(p, -1, 1) >= 0;
}

// 0（起点）〜約1.1（最も遠い角）。ゆらぎで境界を不規則にする
float BookFxField(float2 p)
{
    float d = length(p - _FxOrigin.xy) / max(_FxOrigin.z, 1e-3);
    float n = BookLookFbm(p * 5.5 + _FxOrigin.w);
    return d * 0.78 + n * 0.32;
}

float BookFxThreshold()
{
    return lerp(-0.12, 1.15, saturate(_FxProgress));
}

// 正：変化済み（燃え落ちた／組み上がった）、負：まだ。値は境界からの距離
float BookFxEdge(float2 p)
{
    return BookFxThreshold() - BookFxField(p);
}

// 境界の進む向き（燃えていない側を指す単位ベクトル）
float2 BookFxDirection(float2 p)
{
    const float d = 0.01;
    float f = BookFxField(p);
    float2 g = float2(BookFxField(p + float2(d, 0)) - f, BookFxField(p + float2(0, d)) - f);
    return g / max(length(g), 1e-5);
}

// 燃えている縁の反り返り。燃えていない側の縁の近くで紙が持ち上がり、穴の側へ丸まる。
// uvShift はページUVのずれ（丸まって縮んだ分）、tilt は法線の傾き、lift は持ち上がり 0〜1
float BookFxCurl(float2 p, float e, out float2 uvShift, out float2 tilt)
{
    uvShift = 0;
    tilt = 0;
    if (_FxMode < 0.5 || _FxMode > 1.5 || _BurnCurl <= 0 || e > 0) return 0;
    float width = max(_BurnEdgeWidth * 3.5, 1e-4) * (0.7 + 0.6 * BookLookNoise(p * 9 + _FxOrigin.w));
    float lift = 1 - smoothstep(0, width, -e);
    lift *= lift * _BurnCurl;
    float2 dir = BookFxDirection(p);
    // 見えている紙は、穴に近い側から引き寄せられて縮む
    float2 shift = -dir * lift * width * 0.6 * max(_FxOrigin.z, 1e-3);
    uvShift = float2(shift.x, shift.y / max(_PageAspect, 1e-3));
    tilt = -dir * lift * 1.4;
    return lift;
}

float3 BookFxShadePage(float3 c, float2 p, float e)
{
    float flicker = 0.6 + 0.4 * BookLookNoise(p * 40 + _Time.y * 8);
    if (_FxMode < 1.5)
    {
        float w = max(_BurnEdgeWidth, 1e-4);
        if (e > 0)
        {
            // 穴の下のページ：縁に煤の陰と、反り返った縁の影
            c *= lerp(0.25, 1, smoothstep(0, w * (1.6 + 1.2 * _BurnCurl), e));
            // 反った縁の裏側が穴へせり出して見える
            float lip = w * 0.55 * _BurnCurl * (0.5 + BookLookNoise(p * 23 + _FxOrigin.w));
            if (e < lip)
            {
                float k = e / max(lip, 1e-5);
                float3 under = _BurnCharColor.rgb * (1.6 - k);
                float glowK = saturate(1 - k);
                under += _BurnEmberColor.rgb * glowK * glowK * glowK * flicker * 0.8;
                c = lerp(under, c, smoothstep(0.75, 1, k));
            }
        }
        else
        {
            float x = -e;
            float scorch = 1 - smoothstep(0, w * 4, x);
            c = lerp(c, c * float3(0.72, 0.48, 0.26), scorch * 0.85);
            float charred = 1 - smoothstep(w * 0.35, w, x);
            c = lerp(c, _BurnCharColor.rgb, charred);
            float ember = 1 - smoothstep(0, w * 0.45, x);
            c += _BurnEmberColor.rgb * ember * ember * flicker;
        }
    }
    else
    {
        float w = max(_ReviveEdgeWidth, 1e-4);
        if (e > 0)
        {
            // できたての部分は光が残り、だんだん紙の色へ
            float fresh = 1 - smoothstep(0, w * 2, e);
            c = lerp(c, _ReviveColor.rgb, fresh * 0.35);
            c += _ReviveColor.rgb * 0.12 * sin(saturate(_FxProgress) * 3.14159);
        }
        else
        {
            float near = 1 - smoothstep(0, w * 2.5, -e);
            c = lerp(c, c * 0.8 + _ReviveColor.rgb * 0.1, near);
        }
        c += _ReviveColor.rgb * exp(-abs(e) / (w * 0.35)) * flicker * 0.6;
    }
    return c;
}

// 燃えかすの紙片：境界から剥がれ、回転しながら上へ舞う。縁は残り火で光る
// 返り値は乗算済みの色と覆う量（rgb, a）
float4 BookFxAsh(float2 p)
{
    if (_FxMode < 0.5 || _FxMode > 1.5 || _FxAsh <= 0) return 0;
    const float cell = 0.08;
    const float rise = 0.32;
    float t = BookFxThreshold();
    float4 acc = 0;
    float2 base = floor(p / cell);
    [unroll] for (int oy = -4; oy <= 0; oy++)
    {
        [unroll] for (int ox = -1; ox <= 1; ox++)
        {
            float2 id = base + float2(ox, oy);
            float h1 = BookLookHash(id * 1.31 + _FxOrigin.w + 5.7);
            float h2 = BookLookHash(id * 2.11 + 1.9);
            float h3 = BookLookHash(id * 0.73 + 9.4);
            float h4 = BookLookHash(id * 3.07 + 4.2);
            float2 spawn = (id + float2(h2, h3)) * cell;
            if (h1 > _FxAsh || !BookFxOnPage(spawn)) continue;

            float phase = frac(_Time.y * (0.35 + h4 * 0.2) + h1 * 5);
            float alive = exp(-abs(t - BookFxField(spawn)) / 0.12);
            float2 pos = spawn + float2(sin(phase * 4 + h2 * 6.2832) * 0.03 + phase * 0.05 * (h3 - 0.5), phase * rise);
            float2 q = p - pos;
            float spin = _Time.y * (1.5 + h2 * 2.5) * (h3 > 0.5 ? 1 : -1) + h1 * 6.2832;
            float cs = cos(spin), sn = sin(spin);
            q = float2(q.x * cs - q.y * sn, q.x * sn + q.y * cs);
            q.x /= max(abs(cos(spin * 0.7 + h4 * 3)), 0.2);   // 舞いながら裏返る
            float size = 0.009 + 0.016 * h4;
            float shape = length(q) / size + (BookLookNoise(q / size * 1.8 + id * 3) - 0.5) * 0.9;
            float body = 1 - smoothstep(0.82, 1, shape);
            float edge = smoothstep(0.5, 0.95, shape) * body;
            float life = smoothstep(0, 0.08, phase) * (1 - smoothstep(0.55, 1, phase));
            float a = body * alive * life;
            float glow = (0.5 + 0.5 * sin(_Time.y * 12 + h2 * 30)) * (1 - phase);
            float3 color = _BurnCharColor.rgb * 1.5 + _BurnEmberColor.rgb * edge * glow;
            acc.rgb = acc.rgb * (1 - a) + color * a;
            acc.a = acc.a + a * (1 - acc.a);
        }
    }
    return acc;
}

// 境界の近くから生まれて上へ漂う粒。ページの外（表紙・背景）にも描く
float3 BookFxParticles(float2 p)
{
    if (_FxMode < 0.5 || _FxParticles <= 0) return 0;
    bool burn = _FxMode < 1.5;
    const float cell = 0.045;
    float rise = burn ? 0.16 : 0.09;
    float t = BookFxThreshold();
    float3 sum = 0;
    float2 base = floor(p / cell);
    [unroll] for (int oy = -4; oy <= 0; oy++)
    {
        [unroll] for (int ox = -1; ox <= 1; ox++)
        {
            float2 id = base + float2(ox, oy);
            float h1 = BookLookHash(id + _FxOrigin.w);
            float h2 = BookLookHash(id * 1.7 + 3.1);
            float h3 = BookLookHash(id * 2.3 + 7.7);
            float2 spawn = (id + float2(h2, h3)) * cell;
            if (h1 > _FxParticles || !BookFxOnPage(spawn)) continue;

            float phase = frac(_Time.y * (burn ? 1.1 : 0.55) + h1 * 7);
            float alive = exp(-abs(t - BookFxField(spawn)) / (burn ? 0.06 : 0.09));
            float2 pos = spawn + float2(sin((phase + h2) * 6.2832) * 0.012, phase * rise);
            float2 d = p - pos;
            float r = (burn ? 0.0035 : 0.0045) * (0.6 + h3);
            float glow = exp(-dot(d, d) / (r * r));
            if (!burn)
            {
                // 十字のきらめき
                float2 a = abs(d);
                glow += (exp(-a.x / (r * 0.3)) * exp(-a.y / (r * 3.5)) +
                         exp(-a.y / (r * 0.3)) * exp(-a.x / (r * 3.5))) * 0.6;
            }
            float life = smoothstep(0, 0.15, phase) * (1 - smoothstep(0.55, 1, phase));
            float twinkle = burn ? 0.6 + 0.4 * sin(_Time.y * 30 + h2 * 50)
                                 : 0.5 + 0.5 * sin(_Time.y * 9 + h3 * 40);
            float3 color = burn ? lerp(_BurnEmberColor.rgb, float3(2, 1.8, 1.2), h2 * 0.5)
                                : lerp(_ReviveColor.rgb, float3(2, 2, 2), h3 * 0.5);
            sum += color * glow * alive * life * twinkle;
        }
    }
    return sum;
}

#endif
