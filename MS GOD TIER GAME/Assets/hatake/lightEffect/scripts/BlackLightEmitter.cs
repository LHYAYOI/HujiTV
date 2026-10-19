using UnityEngine;
using UnityEngine.Rendering;

namespace LightShaftLab
{
    /// <summary>
    /// ブラックライト（近紫外線ライト）。Light本体は「目に見えるわずかな紫」だけを担当し、
    /// 蛍光体を光らせる紫外線の強さはこのコンポーネントが別に保持する。
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("LightShaft/ブラックライト（BlackLightEmitter）")]
    [DisallowMultipleComponent, RequireComponent(typeof(Light))]
    public sealed class BlackLightEmitter : MonoBehaviour
    {
        public const float MIN_WAVELENGTH = 350, MAX_WAVELENGTH = 420;

        [Min(0), Tooltip("ランプの出力。蛍光体を光らせる紫外線の強さで、Lightの距離・円錐減衰と同じ単位で扱います。")]
        public float m_power = 8;
        [Tooltip("光源の種類。LEDは長波長側に裾があり、フィルター付きは可視光をほぼ遮断します。")]
        public BLACK_LIGHT_LAMP_TYPE m_lampType = BLACK_LIGHT_LAMP_TYPE.LED;
        [Range(MIN_WAVELENGTH, MAX_WAVELENGTH), Tooltip("発光のピーク波長(nm)。短いほど目に見える紫が弱くなり、蛍光だけが残ります。")]
        public float m_wavelength = 400;
        [Range(0, 50), Tooltip("目に見える紫の量の演出用倍率。1=スペクトルと視感度から求めた現実的な量。")]
        public float m_visibleLeak = 1;
        [Tooltip("波長と出力からLightの色・Intensityを自動設定する。オフにするとLight側の値を手動で調整できます。")]
        public bool m_driveLightFlag = true;
        [Tooltip("Lightの影を蛍光にも反映する（Fluorescent Lit / Lit Dust / 蛍光デカール）。")]
        public bool m_castShadowsFlag = true;
        [Min(0), Tooltip("同時8灯を超えたときの選別の優先倍率。0=蛍光の対象外。")]
        public float m_priority = 1;

        Light m_light;
        Color m_appliedColor;
        float m_appliedIntensity = -1;
        BlackLightSpectrum.Result m_spectrum;
        BLACK_LIGHT_LAMP_TYPE m_spectrumType;
        float m_spectrumWavelength = -1;

        /// <summary>波長・光源の種類から求めたスペクトルの評価結果（変更時のみ再計算）。</summary>
        public BlackLightSpectrum.Result Spectrum
        {
            get
            {
                if (m_spectrumWavelength != m_wavelength || m_spectrumType != m_lampType)
                {
                    m_spectrum = BlackLightSpectrum.Evaluate(m_lampType, m_wavelength);
                    m_spectrumWavelength = m_wavelength; m_spectrumType = m_lampType;
                }
                return m_spectrum;
            }
        }

        /// <summary>蛍光体へ届く紫外線の強さ（フィルターで吸収された分を除く）。</summary>
        public float UltravioletIntensity => isActiveAndEnabled ? Mathf.Max(0, m_power) * Spectrum.m_transmittedFraction : 0;

        /// <summary>シーンの暗順応（BlackLightVolume）。Volumeが無いときは既定値。</summary>
        public static float DarkAdaptation
        {
            get
            {
                var manager = VolumeManager.instance;
                var volume = manager.isInitialized ? manager.stack?.GetComponent<BlackLightVolume>() : null;
                return volume != null ? volume.m_darkAdaptation.value : BlackLightVolume.DEFAULT_DARK_ADAPTATION;
            }
        }

        /// <summary>出力1あたりの見える明るさ（暗順応を考慮）。</summary>
        public float VisibleFraction => Spectrum.Luminance(DarkAdaptation);

        /// <summary>目に見える成分（リニアRGB）。視感度で重み付けした実際の明るさ×演出用倍率。</summary>
        public Color VisibleLinearColor => Spectrum.m_visibleChroma * (Mathf.Max(0, m_power) * VisibleFraction * m_visibleLeak);

        void OnEnable() { m_appliedIntensity = -1; Apply(); }
        void OnValidate() { m_appliedIntensity = -1; Apply(); }
        void Update() { Apply(); }

        public void Apply()
        {
            if (!m_driveLightFlag) return;
            if (!m_light && !TryGetComponent(out m_light)) return;
            // Lightの最終色は color.linear × intensity。最大成分をIntensityにし、色は正規化してガンマ空間で渡す。
            Color visible = VisibleLinearColor;
            float intensity = Mathf.Max(visible.maxColorComponent, 0.00001f);
            Color color = visible.maxColorComponent > 0 ? (visible / visible.maxColorComponent).gamma : new Color(0.3f, 0, 1);
            color.a = 1;
            // 自分が書いた値と同じ間は再設定しない（アニメーションやInspector操作での変更だけ反映）。
            if (Mathf.Approximately(intensity, m_appliedIntensity) && color == m_appliedColor && m_light.color == color && Mathf.Approximately(m_light.intensity, intensity)) return;
            m_light.color = color;
            m_light.intensity = intensity;
            m_appliedColor = color; m_appliedIntensity = intensity;
        }
    }
}
