#ifndef BOOK_FLIP_CORE_INCLUDED
#define BOOK_FLIP_CORE_INCLUDED
#define BOOK_LAYER_NONE  0  // ページ外（表紙・小口・背景）
#define BOOK_LAYER_FLAT  1  // 現在の見開きの平らなページ
#define BOOK_LAYER_FRONT 2  // めくっているページの表
#define BOOK_LAYER_BACK  3  // めくっているページの裏（＝移動先の反対側ページ）
#define BOOK_LAYER_UNDER 4  // めくった下から見える移動先ページ

sampler2D _CurLeftTex, _CurRightTex, _DstLeftTex, _DstRightTex;
float4 _CurLeftRect, _CurRightRect, _DstLeftRect, _DstRightRect; // 各テクスチャ内のページ範囲 XYWH
float4 _BookRect; // 出力UV内の見開き範囲 (xMin, yMin, xMax, yMax)
float _PageAspect; // ページ高さ / ページ幅
float _TurnSide; // +1 右ページをめくる / -1 左ページをめくる
float _Turning; // 0 / 1
float4 _Fold; // xy 角の元位置, zw 角の現在位置（Turn space）


// 面の種類（現在の左, 現在の右, 移動先の左, 移動先の右）：0 ページ, 1 表紙, 2 裏表紙, 3 何も無い
float4 _SlotKinds;
sampler2D _FrontCoverTex, _BackCoverTex;
float4 _CoverFlags; // x 表紙の絵あり, y 裏表紙の絵あり
float4 _CoverMargin; // x 横, y 縦（表紙の板がページからはみ出す幅）
float _Rigid; // 1 = 硬い表紙のめくり（曲がらずに背を軸に回る）。_Fold.z が板の外側の位置（cos）
float _RigidCover; // 回っている板：1 表紙, 2 裏表紙
float _BoardThickness; // 表紙の板の厚み（ページ幅比）
float _CoverRadius; // 表紙の板の角丸
float _DropShadowWidth; // めくれた紙・立った板の落ち影の幅

#define BOOK_SLOT_PAGE  0
#define BOOK_SLOT_FRONT 1
#define BOOK_SLOT_BACK  2
#define BOOK_SLOT_EMPTY 3
#define BOOK_SLOT_INSIDE_FRONT 5   // 表紙の内側（最初の見開きの左）
#define BOOK_SLOT_INSIDE_BACK  6   // 裏表紙の内側（最後の見開きの右が空のとき）
#define BOOK_SPINE_HALF 0.03

struct BookFlipHit
{
    int layer;
    float right; // 1 = 右ページのテクスチャ
    float dest; // 1 = 移動先の見開き
    float2 uv; // ページ内UV
    float2 flatPos; // 平らに置いたときの本座標（ノド・紙の装飾用）
    float kind; // 面の種類（BOOK_SLOT_*）
    float fold; // 折り目からの符号付き距離（Turn space、負がめくれた紙の側）
    float foldLength; // 角の移動量。めくり始めのカールの幅に使用
    float2 foldNormal; // 折り目の法線（本座標）
    float flapInside; // 裏面：反射点のページ端からの距離
    float flapOutside; // 裏面以外：めくれた紙の外形からの距離（落ち影用）
    float2 sheetPos; // 裏面：めくっている紙の元の位置（本座標）
    float rigid; // 1 = 硬い表紙の板
    float rigidMargin; // 1 = 板のページより外の縁
    float rigidShade; // 板の明るさ（立つほど暗い）
    float rigidEdge; // 1 = 板の小口（厚みの面）
};

float2 BookFromUV(float2 uv)
{
    float2 t = (uv - _BookRect.xy) / max(_BookRect.zw - _BookRect.xy, 1e-5);
    return float2(t.x * 2 - 1, t.y * _PageAspect);
}

float2 BookToSpreadUV(float2 p)
{
    return float2((p.x + 1) * 0.5, p.y / max(_PageAspect, 1e-5));
}

// ページ矩形 [x0, x1] × [0, H] の内側での距離。外側は負
float BookPageInside(float2 p, float x0, float x1)
{
    float2 d = min(float2(p.x - x0, p.y), float2(x1 - p.x, _PageAspect - p.y));
    return min(d.x, d.y);
}

float2 BookPageUV(float2 p)
{
    return float2(p.x >= 0 ? p.x : p.x + 1, p.y / max(_PageAspect, 1e-5));
}

void BookSetFlat(inout BookFlipHit h, int layer, float dest, float2 flat)
{
    h.layer = layer;
    h.dest = dest;
    h.flatPos = flat;
    h.right = flat.x >= 0 ? 1 : 0;
    h.uv = saturate(BookPageUV(flat));
}

BookFlipHit BookFlipResolve(float2 p)
{
    BookFlipHit h = (BookFlipHit) 0;
    h.layer = BOOK_LAYER_NONE;
    h.fold = 1e3;
    h.flapOutside = 1e3;
    h.flatPos = p;

    float s = _TurnSide >= 0 ? 1 : -1;
    float2 q = float2(p.x * s, p.y);
    float2 axis = _Fold.xy - _Fold.zw;
    float len = length(axis);
    h.foldLength = len;

    if (_Turning > 0.5 && _Rigid > 0.5)
    {
        // 表紙
        // 背を軸に板ごと回る。真上から見ると横に縮み、持ち上がった外側ほど少し大きく見える
        float c = clamp(_Fold.z, -1, 1);
        float sinT = sqrt(saturate(1 - c * c));
        float width = 1 + _CoverMargin.x;
        h.foldLength = 1;
        
        // 板の小口：立つほど厚みの面が見える
        // 開いた側（内側が上）に寝るほど、置いてある開いた本の板と同じ L 字の小口（外側と下）になる。
        // 閉じた側に寝ると小口は無い（閉じた本では厚みを描かない）
        float side = c >= 0 ? 1 : -1;
        float stillSide = _RigidCover < 1.5 ? 1 : -1; // 動かない板の側（表紙なら右、裏表紙なら左）
        float lying = saturate(-c * stillSide * s);
        float edgeWidth = _BoardThickness * max(sinT, lying); // 画面上の外側の小口の幅
        float drop = _BoardThickness * 0.7 * lying; // 下の小口の高さ
        float edgeStart = abs(c) * width;
        float edgeEnd = edgeStart + edgeWidth;
        float outerLift = 1 + 0.08 * sinT * width;
        float edgeY = (q.y - _PageAspect * 0.5) / outerLift + _PageAspect * 0.5;
        if (abs(c) > 1e-3)
        {
            // 板の上の座標（u：背からの距離、y：高さ）。板と小口は、置いてある板と同じ角丸の箱で判定する。
            // 外側の角は常に角丸。背の側の角は、閉じた側では閉じた表紙と同じ角丸、開いた側へ寝るほど
            // 角を立てる（開いた本では反対側の板とつながるため）。
            // 横に縮んで見える分、角丸も横に縮む（傾いた板の角の見え方）
            float u = q.x / c;
            float lift = 1 + 0.08 * sinT * u;
            float y = (q.y - _PageAspect * 0.5) / lift + _PageAspect * 0.5;
            float r = min(_CoverRadius, width * 0.5);
            r = u < width * 0.5 ? r * (1 - lying) : r; // 背の側の角丸
            // 背の側の端：閉じた側では閉じた表紙の板と同じく背の幅の半分だけ背の向こうへ出る（角金具・角丸の位置が一致する）。
            // 開いた側へ寝るほど背の位置へ戻す
            float uMin = -BOOK_SPINE_HALF * (1 - lying);
            float y0 = -_CoverMargin.y, y1 = _PageAspect + _CoverMargin.y;
            float2 boardMin = float2(uMin, y0), boardMax = float2(width, y1);
            float2 boardHalf = (boardMax - boardMin) * 0.5;
            float2 bq = abs(float2(u, y) - (boardMin + boardMax) * 0.5) - boardHalf + r;
            float boardSd = length(max(bq, 0)) + min(max(bq.x, bq.y), 0) - r;
            if (u >= uMin && boardSd <= 0)
            {
                bool front = c > 0;
                // 表側（閉じた表紙の絵の面）は背の向こうへ出た分もそのままの位置で描く。内側は背で止める
                float fu = front ? u : max(u, 0);
                BookSetFlat(h, front ? BOOK_LAYER_FRONT : BOOK_LAYER_BACK, front ? 0 : 1, float2((front ? fu : -fu) * s, y));
                h.right = (front ? s : -s) > 0 ? 1 : 0; // 背の向こうへ出た分も板の側の面として扱う
                h.uv = saturate(BookPageUV(float2(abs(h.flatPos.x) * (h.right > 0.5 ? 1 : -1), y)));
                h.rigid = 1;
                h.rigidMargin = (u > 1 || u < 0 || y < 0 || y > _PageAspect) ? 1 : 0;
                h.rigidShade = lerp(0.55, 1.0, abs(c));
                h.sheetPos = float2(u * s, y);
                return h;
            }
            // 小口：外側と下へ広げた角丸の箱から板を除いた部分
            float grow = edgeWidth / abs(c);
            if (u >= uMin && (grow > 1e-5 || drop > 1e-5))
            {
                float2 edgeMin = float2(uMin, y0 - drop), edgeMax = float2(width + grow, y1);
                float2 edgeHalf = (edgeMax - edgeMin) * 0.5;
                float2 eq = abs(float2(u, y) - (edgeMin + edgeMax) * 0.5) - edgeHalf + r;
                float edgeSd = length(max(eq, 0)) + min(max(eq.x, eq.y), 0) - r;
                if (edgeSd <= 0)
                {
                    h.layer = BOOK_LAYER_FRONT;
                    h.flatPos = float2(side * s, edgeY);
                    h.sheetPos = p; // 小口の色の模様（置いてある板の小口と同じ座標）
                    h.flapInside = saturate(boardSd / max(max(grow, drop), 1e-5)); // 表側の角からの割合
                    h.rigid = 1;
                    h.rigidEdge = 1;
                    h.rigidShade = 1;
                    return h;
                }
            }
        }
        else if (q.x * side >= 0 && q.x * side <= edgeWidth &&
                 edgeY >= -_CoverMargin.y && edgeY <= _PageAspect + _CoverMargin.y)
        {
            // 真横：厚みの帯だけ
            h.layer = BOOK_LAYER_FRONT;
            h.flatPos = float2(side * s, edgeY);
            h.sheetPos = p;
            h.flapInside = q.x * side / max(edgeWidth, 1e-5);
            h.rigid = 1;
            h.rigidEdge = 1;
            h.rigidShade = 1;
            return h;
        }
        // 板の外：立った板が落とす影。板の外形（角丸）に沿って落とし、上へは出さず、下へは短く、板が寝るほど薄くして
        // 着地した本の影に引き継ぐ。横は板が立つほど長く伸びるので、横と縦で縮尺を変えた空間で角丸の距離を測る
        float fade = smoothstep(0.3, 0.75, sinT);
        if (q.x * c > 0 && fade > 1e-3)
        {
            float stretchX = 0.06 / max(0.25 * sinT, 0.01); // 横：画面上の距離 → 影の距離
            const float stretchY = 4; // 縦：上下へは短く
            // 影の箱の上端は、影のぼかしの幅（_DropShadowWidth）だけ板の上端より下げ、板の上端で影が消えるようにする
            float sy0 = (-_CoverMargin.y - drop) * stretchY, sy1 = (_PageAspect + _CoverMargin.y) * stretchY - _DropShadowWidth;
            float2 sMin = float2(-1, sy0), sMax = float2(edgeEnd * stretchX, sy1);
            // 高さ：板の上はその位置の板の持ち上がりで戻す（板の上辺は外側ほど高く見える）。
            // 板より外は机の上なので持ち上がり無し（置いてある板の上端より上に影を出さない）
            float su = abs(q.x) / max(abs(c), 1e-3);
            float sLift = su <= width ? 1 + 0.08 * sinT * su : 1;
            float sy = (q.y - _PageAspect * 0.5) / sLift + _PageAspect * 0.5;
            float2 sp = float2(abs(q.x) * stretchX, sy * stretchY);
            const float sr = 0.03;
            float2 sq = abs(sp - (sMin + sMax) * 0.5) - (sMax - sMin) * 0.5 + sr;
            float shadowSd = length(max(sq, 0)) + min(max(sq.x, sq.y), 0) - sr;
            h.flapOutside = max(shadowSd, 0); // 板の角丸の外の隙間も影の中
            h.foldLength = fade * 0.25; // 合成側で影の濃さに掛かる（saturate(foldLength * 4)）
        }
        if (q.x >= 0 && BookPageInside(q, 0, 1) >= 0)
        {
            BookSetFlat(h, BOOK_LAYER_UNDER, 1, p);
            return h;
        }
        if (BookPageInside(p, -1, 1) >= 0)
            BookSetFlat(h, BOOK_LAYER_FLAT, 0, p);
        return h;
    }

    if (_Turning > 0.5 && len > 1e-4)
    {
        float2 n = axis / len;
        float d = dot(q - (_Fold.xy + _Fold.zw) * 0.5, n);
        h.fold = d;
        h.foldNormal = float2(n.x * s, n.y);
        if (d < 0)
        {
            // 折り返した紙の上にいる画素：折り目で反射した位置が紙の元の場所
            float2 r = q - 2 * d * n;
            float inside = BookPageInside(r, 0, 1);
            if (inside >= 0)
            {
                // 裏面には移動先の反対側ページが、めくり終えたときの位置で載る
                BookSetFlat(h, BOOK_LAYER_BACK, 1, float2(-r.x * s, r.y));
                h.flapInside = inside;
                h.sheetPos = float2(r.x * s, r.y);
                return h;
            }
            h.flapOutside = -inside;
        }
        if (q.x >= 0 && BookPageInside(q, 0, 1) >= 0)
        {
            if (d < 0)
                BookSetFlat(h, BOOK_LAYER_FRONT, 0, p);
            else
                BookSetFlat(h, BOOK_LAYER_UNDER, 1, p);
            return h;
        }
    }

    if (BookPageInside(p, -1, 1) >= 0)
        BookSetFlat(h, BOOK_LAYER_FLAT, 0, p);
    return h;
}

float BookSlotKind(float dest, float right)
{
    return dest < 0.5 ? (right > 0.5 ? _SlotKinds.y : _SlotKinds.x)
                      : (right > 0.5 ? _SlotKinds.w : _SlotKinds.z);
}

// 閉じた本の表紙の板。表紙は右側（背から外へ）、裏表紙は左側
void BookCoverBox(bool rightSide, out float2 center, out float2 halfSize)
{
    float x0 = rightSide ? -BOOK_SPINE_HALF : -1 - _CoverMargin.x;
    float x1 = rightSide ? 1 + _CoverMargin.x : BOOK_SPINE_HALF;
    float y0 = -_CoverMargin.y, y1 = _PageAspect + _CoverMargin.y;
    center = float2(x0 + x1, y0 + y1) * 0.5;
    halfSize = float2(x1 - x0, y1 - y0) * 0.5;
}

// 表紙の絵のUV（板全体に貼る）p は本の座標
float2 BookCoverUV(float2 p, bool rightSide)
{
    float2 center, halfSize;
    BookCoverBox(rightSide, center, halfSize);
    return saturate((p - center) / (halfSize * 2) + 0.5);
}

float2 BookCropUV(float2 uv, float4 rect)
{
    return rect.xy + saturate(uv) * rect.zw;
}

// 分岐内でも微分に依存しないよう LOD 0 で読む
float4 BookSamplePage(float dest, float right, float2 uv)
{
    float4 c;
    float kind = BookSlotKind(dest, right);
    if (kind > 0.5 && kind < 2.5)
    {
        // 表紙・裏表紙 <板全体に貼った絵のうち、このページの範囲
        float2 p = float2(right > 0.5 ? uv.x : uv.x - 1, uv.y * _PageAspect);
        bool front = kind < 1.5;
        float2 coverUV = BookCoverUV(p, front);
        float has = front ? _CoverFlags.x : _CoverFlags.y;
        float4 t = front ? tex2Dlod(_FrontCoverTex, float4(coverUV, 0, 0)) : tex2Dlod(_BackCoverTex, float4(coverUV, 0, 0));
        return has > 0.5 ? t : float4(-1, -1, -1, 1); // 負 < 絵なし（革で塗る）
    }
    if (dest < 0.5)
    {
        if (right > 0.5)
            c = tex2Dlod(_CurRightTex, float4(BookCropUV(uv, _CurRightRect), 0, 0));
        else
            c = tex2Dlod(_CurLeftTex, float4(BookCropUV(uv, _CurLeftRect), 0, 0));
    }
    else
    {
        if (right > 0.5)
            c = tex2Dlod(_DstRightTex, float4(BookCropUV(uv, _DstRightRect), 0, 0));
        else
            c = tex2Dlod(_DstLeftTex, float4(BookCropUV(uv, _DstLeftRect), 0, 0));
    }
    return c;
}

#endif
