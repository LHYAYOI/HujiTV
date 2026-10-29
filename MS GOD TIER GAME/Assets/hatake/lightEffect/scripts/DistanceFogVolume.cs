using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace LightShaftLab
{
    /// <summary>
    /// 遠くを闇（や色）に沈める距離フォグの設定（Volumeで調整）。
    /// 手前は LightShaft の霧（ライトで光る空気）が受け持ち、その奥をこのフォグが受け持つ。
    /// 画面の深度から距離を求めるので、シェーダーの種類に関係なくすべての不透明物に効く。
    /// </summary>
    [Serializable, VolumeComponentMenu("Light Shaft/Distance Fog")]
    public sealed class DistanceFogVolume : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter m_enableFlag = new BoolParameter(false);

        [Header("始まる距離")]
        [Tooltip("始まる距離を LightShaft の霧の Max Distance に合わせる。手前の光る霧から奥の距離フォグへ自然につなぐ。")]
        public BoolParameter m_linkToLightShaftFlag = new BoolParameter(true);
        [Tooltip("始まる距離（m）。LightShaft に合わせる設定がオフのときに使う。")]
        public ClampedFloatParameter m_startDistance = new ClampedFloatParameter(8, 0, 200);
        [Tooltip("LightShaft に合わせるとき、LightShaft の霧の終わりより手前から重ね始める距離（m）。")]
        public ClampedFloatParameter m_overlap = new ClampedFloatParameter(2, 0, 20);

        [Header("濃さ")]
        [Tooltip("1mあたりの濃さ。0.05で約20m先が6割ほど沈む。")]
        public ClampedFloatParameter m_density = new ClampedFloatParameter(0.06f, 0, 2);
        [Tooltip("一番濃いときの不透明度。1未満にすると遠くもうっすら見える。")]
        public ClampedFloatParameter m_maxOpacity = new ClampedFloatParameter(1, 0, 1);

        [Header("色")]
        [Tooltip("始まる距離付近のフォグの色。")]
        public ColorParameter m_nearColor = new ColorParameter(new Color(0.06f, 0.07f, 0.11f), true, false, true);
        [Tooltip("遠くのフォグの色。")]
        public ColorParameter m_farColor = new ColorParameter(new Color(0.01f, 0.012f, 0.02f), true, false, true);
        [Tooltip("始まる距離から、遠くの色になりきるまでの距離（m）。")]
        public ClampedFloatParameter m_colorDistance = new ClampedFloatParameter(25, 0.1f, 500);

        [Header("高さ")]
        [Tooltip("この高さ（ワールドY）で設定どおりの濃さになる。")]
        public FloatParameter m_baseHeight = new FloatParameter(0);
        [Tooltip("上に行くほど薄くなる割合。0で高さに関係なく一様。")]
        public ClampedFloatParameter m_heightFalloff = new ClampedFloatParameter(0, 0, 2);

        [Header("その他")]
        [Tooltip("空（何も描かれていない所）にもフォグをかける。")]
        public BoolParameter m_applyToSkyFlag = new BoolParameter(true);

        public bool IsActive() => m_enableFlag.value && m_density.value > 0 && m_maxOpacity.value > 0;
        public bool IsTileCompatible() => false;
    }
}
