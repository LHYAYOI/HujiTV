
#ifndef HATAKE_PAINTERLY_INCLUDED
#define HATAKE_PAINTERLY_INCLUDED

// 楕円の最大半径（ピクセル）。ループ回数の上限になる
#define PAINTERLY_MAX_KERNEL_EXTENT 16
#define PAINTERLY_PI 3.14159265

float _Contrast;
float _Brightness;
float _uBrushRadius;
float _uBrushSharpness;
float _uBrushAnisotropy;
float _uStrokeScale;
float _uStrokeStrength;
float _uCanvasScale;
float _uCanvasStrength;
float _uImpasto;
float _uSaturation;
float _uWarmth;
float _uVignette;

float PainterlyLuminance(float3 c) {
    return dot(c, float3(0.299, 0.587, 0.114));
}

float PainterlyHash(float2 p) {
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float PainterlyNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(PainterlyHash(i + float2(0, 0)), PainterlyHash(i + float2(1, 0)), u.x),
        lerp(PainterlyHash(i + float2(0, 1)), PainterlyHash(i + float2(1, 1)), u.x),
        u.y);
}

// 線形色とsRGBの相互変換。筆致の判定や色調整は見た目に近いsRGB空間で行う
float3 PainterlyToDisplay(float3 c) {
    #if defined(UNITY_COLORSPACE_GAMMA)
        return c;
    #else
        return pow(max(c, 0.0), 1.0 / 2.2);
    #endif
}

float3 PainterlyToLinear(float3 c) {
    #if defined(UNITY_COLORSPACE_GAMMA)
        return c;
    #else
        return pow(max(c, 0.0), 2.2);
    #endif
}

float3 PainterlySample(float2 uv) {
    float3 c = PainterlyToDisplay(PainterlySource(uv));
    return saturate((c - 0.5) * _Contrast + 0.5 + _Brightness);
}

// 構造テンソル (E, F, G)。Sobelで求めた勾配から作る
float3 PainterlyStructureTensor(float2 uv, float2 texel) {
    float3 tl = PainterlySample(uv + float2(-texel.x,  texel.y));
    float3 tm = PainterlySample(uv + float2(     0.0,  texel.y));
    float3 tr = PainterlySample(uv + float2( texel.x,  texel.y));
    float3 ml = PainterlySample(uv + float2(-texel.x,      0.0));
    float3 mr = PainterlySample(uv + float2( texel.x,      0.0));
    float3 bl = PainterlySample(uv + float2(-texel.x, -texel.y));
    float3 bm = PainterlySample(uv + float2(     0.0, -texel.y));
    float3 br = PainterlySample(uv + float2( texel.x, -texel.y));
    float3 gx = (-tl - 2.0 * ml - bl + tr + 2.0 * mr + br) * 0.25;
    float3 gy = (-bl - 2.0 * bm - br + tl + 2.0 * tm + tr) * 0.25;
    return float3(dot(gx, gx), dot(gx, gy), dot(gy, gy));
}

// 周辺の構造テンソルを平均し、筆の流れ方向(t)と異方性(A)を得る
void PainterlyBrushFlow(float2 uv, float2 texel, out float2 t, out float A) {
    float2 o = texel * 2.0;
    float3 st = PainterlyStructureTensor(uv, texel) * 0.4;
    st += PainterlyStructureTensor(uv + float2( o.x, 0.0), texel) * 0.15;
    st += PainterlyStructureTensor(uv + float2(-o.x, 0.0), texel) * 0.15;
    st += PainterlyStructureTensor(uv + float2(0.0,  o.y), texel) * 0.15;
    st += PainterlyStructureTensor(uv + float2(0.0, -o.y), texel) * 0.15;

    float E = st.x, F = st.y, G = st.z;
    float d = sqrt((E - G) * (E - G) + 4.0 * F * F);
    float lambda1 = 0.5 * (E + G + d);
    float lambda2 = 0.5 * (E + G - d);
    float2 v = float2(lambda1 - E, -F);
    t = dot(v, v) > 1e-8 ? normalize(v) : float2(0.7071, 0.7071);
    A = (lambda1 + lambda2 > 1e-6) ? (lambda1 - lambda2) / (lambda1 + lambda2) : 0.0;
}

// 異方性Kuwaharaフィルタ（8セクター・多項式重み）
// エッジは保ちつつ、領域内を筆で塗ったような平坦なタッチにまとめる
float3 PainterlyKuwahara(float2 uv, float2 texel, float2 t, float A, float radius) {
    A *= _uBrushAnisotropy;
    float a = radius * clamp(1.0 + A, 0.1, 2.0);
    float b = radius * clamp(1.0 / (1.0 + A), 0.1, 2.0);

    float phi = -atan2(t.y, t.x);
    float cosPhi = cos(phi);
    float sinPhi = sin(phi);
    float2x2 R = float2x2(cosPhi, -sinPhi, sinPhi, cosPhi);
    float2x2 S = float2x2(0.5 / a, 0.0, 0.0, 0.5 / b);
    float2x2 SR = mul(S, R);

    int maxX = min(PAINTERLY_MAX_KERNEL_EXTENT, (int)sqrt(a * a * cosPhi * cosPhi + b * b * sinPhi * sinPhi));
    int maxY = min(PAINTERLY_MAX_KERNEL_EXTENT, (int)sqrt(a * a * sinPhi * sinPhi + b * b * cosPhi * cosPhi));

    float zeta = 2.0 / radius;
    float zeroCross = 0.58;
    float sinZeroCross = sin(zeroCross);
    float eta = (zeta + cos(zeroCross)) / (sinZeroCross * sinZeroCross);

    float4 m[8];
    float3 s[8];
    [unroll] for (int n = 0; n < 8; ++n) {
        m[n] = 0.0;
        s[n] = 0.0;
    }

    [loop] for (int y = -maxY; y <= maxY; ++y) {
        [loop] for (int x = -maxX; x <= maxX; ++x) {
            float2 v = mul(SR, float2(x, y));
            if (dot(v, v) > 0.25) continue;

            float3 c = PainterlySample(uv + float2(x, y) * texel);
            float w[8];
            float z, vxx, vyy;
            float sum = 0.0;

            vxx = zeta - eta * v.x * v.x;
            vyy = zeta - eta * v.y * v.y;
            z = max(0.0,  v.y + vxx); w[0] = z * z; sum += w[0];
            z = max(0.0, -v.x + vyy); w[2] = z * z; sum += w[2];
            z = max(0.0, -v.y + vxx); w[4] = z * z; sum += w[4];
            z = max(0.0,  v.x + vyy); w[6] = z * z; sum += w[6];

            v = 0.70710678 * float2(v.x - v.y, v.x + v.y);
            vxx = zeta - eta * v.x * v.x;
            vyy = zeta - eta * v.y * v.y;
            z = max(0.0,  v.y + vxx); w[1] = z * z; sum += w[1];
            z = max(0.0, -v.x + vyy); w[3] = z * z; sum += w[3];
            z = max(0.0, -v.y + vxx); w[5] = z * z; sum += w[5];
            z = max(0.0,  v.x + vyy); w[7] = z * z; sum += w[7];

            float g = exp(-3.125 * dot(v, v)) / max(sum, 1e-5);
            [unroll] for (int k = 0; k < 8; ++k) {
                float wk = w[k] * g;
                m[k] += float4(c * wk, wk);
                s[k] += c * c * wk;
            }
        }
    }

    float4 result = 0.0;
    float3 calmest = PainterlySample(uv);
    float calmestSigma = 1e9;
    [unroll] for (int j = 0; j < 8; ++j) {
        float3 mean = m[j].rgb / max(m[j].w, 1e-5);
        float3 variance = abs(s[j] / max(m[j].w, 1e-5) - mean * mean);
        float sigma2 = variance.r + variance.g + variance.b;
        float wk = 1.0 / (1.0 + pow(1000.0 * sigma2, 0.5 * _uBrushSharpness));
        result += float4(mean * wk, wk);
        if (m[j].w > 1e-5 && sigma2 < calmestSigma) { calmestSigma = sigma2; calmest = mean; }
    }
    // 細かい模様では全セクターの重みが極小になる。割り算で黒くつぶれないよう、最も平坦なセクターの色を使う
    return result.w > 1e-6 ? result.rgb / result.w : calmest;
}

// 平坦部では方向が定まらないので、低周波ノイズで筆の向きを揺らす
// 向きは180度で同一視されるため、角度を2倍にしたベクトルで補間する
float2 PainterlyStrokeDirection(float2 pix, float2 t, float A) {
    float angle = 0.8 + (PainterlyNoise(pix * 0.006) - 0.5) * 1.8 + (PainterlyNoise(pix * 0.02) - 0.5) * 0.8;
    float2 flat2 = float2(cos(angle), sin(angle));
    float2 edge2 = float2(t.x * t.x - t.y * t.y, 2.0 * t.x * t.y);
    float2 d2 = lerp(flat2, edge2, smoothstep(0.05, 0.4, A));
    float halfAngle = 0.5 * atan2(d2.y, d2.x);
    return float2(cos(halfAngle), sin(halfAngle));
}

// 筆致の高さ。流れ方向に長い毛のすじ(streak)を、短いタッチ単位(dab)でまとめる
float PainterlyBristleHeight(float2 pix, float2 t) {
    float2 n = float2(-t.y, t.x);
    float2 p = float2(dot(pix, t), dot(pix, n)) / _uStrokeScale;
    float streak = PainterlyNoise(float2(p.x * 0.12, p.y * 0.8)) * 0.6
                 + PainterlyNoise(float2(p.x * 0.3,  p.y * 2.2)) * 0.4;
    float dab = smoothstep(0.3, 0.7, PainterlyNoise(float2(p.x * 0.035 + 17.0, p.y * 0.09 + 5.0)));
    return 0.5 + (streak - 0.5) * (0.35 + 0.65 * dab) + (dab - 0.5) * 0.25;
}

// キャンバスの織り目の高さ
float PainterlyCanvasHeight(float2 uv, float aspect) {
    float2 p = uv * float2(aspect, 1.0) * _uCanvasScale;
    float2 cell = floor(p);
    float2 f = frac(p);
    float alt = fmod(cell.x + cell.y, 2.0);
    float warp = sin(f.x * PAINTERLY_PI);
    float weft = sin(f.y * PAINTERLY_PI);
    float weave = lerp(warp * (0.6 + 0.4 * weft), weft * (0.6 + 0.4 * warp), alt);
    float grain = PainterlyNoise(p * 3.7) * 0.35;
    return weave * 0.65 + grain;
}

// 絵画風の色を返す（線形色）。
// texel: 1ピクセルのUV幅。pixelScale: 1080p基準の画素倍率（解像度が変わっても筆の大きさを保つ）。
float3 PainterlyShade(float2 uv, float2 texel, float pixelScale) {
    pixelScale = max(pixelScale, 0.25);
    float aspect = texel.y / texel.x;
    // 筆致の模様は1080p基準の画素座標で作る
    float2 pix = uv / texel / pixelScale;

    float2 t;
    float A;
    PainterlyBrushFlow(uv, texel, t, A);

    #if defined (U_KUWAHARA_ON)
        float3 color = PainterlyKuwahara(uv, texel, t, A, max(_uBrushRadius * pixelScale, 1.0));
    #else
        float3 color = PainterlySample(uv);
    #endif // if defined (U_KUWAHARA_ON)

    // 色調：彩度を上げ、少し暖色寄りにして絵具らしい発色にする
    float luma = PainterlyLuminance(color);
    color = lerp(luma.xxx, color, _uSaturation);
    color += float3(0.06, 0.02, -0.06) * _uWarmth;
    color = saturate(color);

    float height = 0.0;
    float2 heightGrad = 0.0;

    #if defined (U_STROKE_ON)
        float2 sd = PainterlyStrokeDirection(pix, t, A);
        float h0 = PainterlyBristleHeight(pix, sd);
        float hx = PainterlyBristleHeight(pix + float2(1.0, 0.0), sd);
        float hy = PainterlyBristleHeight(pix + float2(0.0, 1.0), sd);
        // すじの濃淡で絵具の厚みのムラを出す（明部は控えめ）
        color *= 1.0 + (h0 - 0.5) * _uStrokeStrength * lerp(0.6, 0.3, luma);
        height += h0 * _uStrokeStrength;
        heightGrad += float2(hx - h0, hy - h0) * _uStrokeStrength;
    #endif // if defined (U_STROKE_ON)

    #if defined (U_CANVAS_ON)
        float c0 = PainterlyCanvasHeight(uv, aspect);
        float cx = PainterlyCanvasHeight(uv + float2(texel.x, 0.0) * pixelScale, aspect);
        float cy = PainterlyCanvasHeight(uv + float2(0.0, texel.y) * pixelScale, aspect);
        // 塗りが薄い暗部ほどキャンバスの目が出る
        float canvasShow = _uCanvasStrength * lerp(1.0, 0.55, luma);
        color *= 1.0 - (1.0 - c0) * canvasShow * 0.18;
        height += c0 * canvasShow;
        heightGrad += float2(cx - c0, cy - c0) * canvasShow;
    #endif // if defined (U_CANVAS_ON)

    // インパスト：絵具とキャンバスの凹凸に左上からの光を当てる
    float3 normal = normalize(float3(-heightGrad * _uImpasto * 4.0, 1.0));
    float3 lightDir = normalize(float3(-0.5, 0.6, 0.62));
    float diffuse = dot(normal, lightDir) - lightDir.z;
    float3 halfDir = normalize(lightDir + float3(0, 0, 1));
    float spec = max(0.0, pow(saturate(dot(normal, halfDir)), 24.0) - pow(halfDir.z, 24.0));
    color += diffuse * 0.4 * _uImpasto * (0.5 + 0.5 * luma);
    color += spec * 0.25 * _uImpasto * saturate(height);

    // 周辺減光
    float vignette = smoothstep(0.8, 0.3, length(uv - 0.5));
    color *= lerp(1.0, vignette, _uVignette);

    return PainterlyToLinear(saturate(color));
}

#endif // HATAKE_PAINTERLY_INCLUDED
