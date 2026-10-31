#ifndef HATAKE_DISTANCE_FOG_COMMON_INCLUDED
#define HATAKE_DISTANCE_FOG_COMMON_INCLUDED

// 距離フォグの値（DistanceFogFeature が毎カメラ設定する）。半透明のシェーダーからも同じフォグをかけられる。
// x = 始まる距離、y = 濃さ、z = 最大の不透明度、w = 有効なら 1
float4 _HatakeFogParams;
// x = 基準の高さ、y = 高さで薄くなる割合、z = 遠くの色になりきる距離、w = 空にもかけるなら 1
float4 _HatakeFogHeight;
float4 _HatakeFogNearColor;
float4 _HatakeFogFarColor;

// カメラから positionWS までの間にあるフォグの量（0〜最大の不透明度）
float HatakeFogAmount(float3 cameraPositionWS, float3 positionWS)
{
    if (_HatakeFogParams.w < 0.5)
    {
        return 0;
    }
    float3 delta = positionWS - cameraPositionWS;
    float distance = length(delta);
    float start = _HatakeFogParams.x;
    if (distance <= start)
    {
        return 0;
    }
    float3 ray = delta / distance;
    float span = distance - start;
    float falloff = _HatakeFogHeight.y;
    float optical = span;
    if (falloff > 0.0001)
    {
        // 高さで指数的に薄くなる霧を、始まる点から光線に沿って解析的に積分する
        float startHeight = cameraPositionWS.y + ray.y * start - _HatakeFogHeight.x;
        float slope = falloff * ray.y;
        float along = abs(slope * span) < 0.001 ? span * (1 - 0.5 * slope * span) : (1 - exp(-slope * span)) / slope;
        optical = exp(min(-falloff * startHeight, 10)) * along;
    }
    return min(1 - exp(-_HatakeFogParams.y * optical), _HatakeFogParams.z);
}

// 距離に応じたフォグの色
float3 HatakeFogColor(float3 cameraPositionWS, float3 positionWS)
{
    float distance = length(positionWS - cameraPositionWS);
    float t = saturate((distance - _HatakeFogParams.x) / max(_HatakeFogHeight.z, 0.01));
    return lerp(_HatakeFogNearColor.rgb, _HatakeFogFarColor.rgb, t);
}

// 半透明のシェーダー用：色にフォグを混ぜる
float3 HatakeApplyFog(float3 color, float3 cameraPositionWS, float3 positionWS)
{
    float amount = HatakeFogAmount(cameraPositionWS, positionWS);
    return lerp(color, HatakeFogColor(cameraPositionWS, positionWS), amount);
}

#endif
