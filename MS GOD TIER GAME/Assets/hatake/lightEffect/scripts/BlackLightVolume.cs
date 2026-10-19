using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace LightShaftLab
{
    /// <summary>ブラックライトのシーン全体の調整。プロファイルに追加しなくても既定値で動作する。</summary>
    [Serializable, VolumeComponentMenu("Light Shaft/Black Light")]
    public sealed class BlackLightVolume : VolumeComponent
    {
        public const float DEFAULT_DARK_ADAPTATION = 0.5f;
        [Tooltip("見る人の目の暗順応。0=明所視（明るい場所・カメラ。400nm付近はほぼ見えない）、1=暗所視（真っ暗な部屋に慣れた目。紫がはっきり見える）。365nmのような短い波長はどちらでもほぼ見えません。")]
        public ClampedFloatParameter m_darkAdaptation = new ClampedFloatParameter(DEFAULT_DARK_ADAPTATION, 0, 1);
        [Tooltip("全マテリアルの蛍光の明るさ。Bloomがにじみ過ぎるときはここを下げます。")]
        public ClampedFloatParameter m_fluorescenceGain = new ClampedFloatParameter(1, 0, 8);
        [Tooltip("空気中の埃が紫外線で光る量。光柱（LightShaftEmitter）を付けたブラックライトの霧に加算します。")]
        public ClampedFloatParameter m_fogFluorescence = new ClampedFloatParameter(0.01f, 0, 2);
        [Tooltip("霧の蛍光色。")]
        public ColorParameter m_fogFluorescenceColor = new ColorParameter(new Color(0.55f, 0.7f, 1), false, false, true);
    }
}
