#ifndef SHAFT_BEAM_STYLE_INCLUDED
#define SHAFT_BEAM_STYLE_INCLUDED
float4 _ShaftBeamStyle[4], _ShaftBeamRight[4], _ShaftBeamUp[4], _ShaftBeamAxes[4];
int _ShaftHasBeamStyle;
TEXTURE2D(_ShaftBeamNoise);
SAMPLER(sampler_ShaftBeamNoise);
// スポットライトのビームスタイル（輪郭の硬さ・不規則なムラ）を反映した角度方向の減衰を求める
float ComputeShaftBeamAngular(int light, float cone, float3 fromLight, float3 positionWS, float3 ray, float stepSize, bool detailFlag)
{
    float4 style = _ShaftBeamStyle[light];
    float profile = lerp(cone * cone, cone * (2 - cone), style.x);
    profile = lerp(profile, profile * profile * (3 - 2 * profile), style.y) * style.w;
    if (style.z > 0)
    {
        float axial = max(.001, dot(fromLight, _ShaftSpotDirection[light].xyz));
        float2 angular = float2(dot(fromLight, _ShaftBeamRight[light].xyz), dot(fromLight, _ShaftBeamUp[light].xyz)) * _ShaftBeamRight[light].w / axial;
        // 角度マスクは実際のサンプリング格子に合わせてフィルタする。適応的レイマーチの
        // 内部では微分命令が未定義になるため使えない。頂点付近ではマスクの平均値を使う。
        // _ScreenParams は出力の画素数で、Render Scale を下げると実際の描画より大きい。描画先の画素数を使う
        float height = detailFlag ? _ScaledScreenParams.y : _ShaftLowSize.w;
        float viewDepth = unity_OrthoParams.w > .5 ? 1 : max(.001, -TransformWorldToView(positionWS).z);
        float footprint = 2 * viewDepth / max(1, height * abs(UNITY_MATRIX_P._m11)) * _ShaftBeamRight[light].w / axial;
        float2 angularSlope = (float2(dot(ray, _ShaftBeamRight[light].xyz), dot(ray, _ShaftBeamUp[light].xyz)) * _ShaftBeamRight[light].w - angular * dot(ray, _ShaftSpotDirection[light].xyz)) / axial;
        footprint = max(footprint, length(angularSlope) * stepSize);
        float4 axes = _ShaftBeamAxes[light];
        float2 rotated = float2(dot(angular, axes.xy), dot(angular, float2(-axes.y, axes.x)));
        float frequency = _ShaftBeamUp[light].w * .18;
        float2 noiseOffset = rotated * frequency;
        // 明示的な LOD 指定は適応的ループ内でも安定する。最も粗いミップは平均値に近づくため、
        // 頂点でビームが消えたり、少ないステップ数でちらついたりしない。
        float mipLevel = max(0, log2(max(1, footprint * frequency * 128)));
        float2 forwardNoise = SAMPLE_TEXTURE2D_LOD(_ShaftBeamNoise, sampler_ShaftBeamNoise, axes.zw + noiseOffset, mipLevel).rg;
        float2 backwardNoise = SAMPLE_TEXTURE2D_LOD(_ShaftBeamNoise, sampler_ShaftBeamNoise, axes.zw - noiseOffset, mipLevel).rg;
        // 対蹠点どうしを引き算することで、放射状の包絡に対して角度方向の平均がゼロに保たれる。
        // 雲のような大きなムラと不規則な細かい隙間で、周期的な正弦波の縞を置き換える。
        float mask = clamp(dot(forwardNoise - backwardNoise, float2(2.8, .75)), -.95, .95);
        profile *= 1 + style.z * mask;
    }
    return profile;
}
#endif
