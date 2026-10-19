using UnityEngine;

namespace LightShaftLab
{
    /// <summary>Spot/Pointライトに付けて光柱（霧の散乱）を描画させる設定。</summary>
    [AddComponentMenu("LightShaft/光柱（LightShaftEmitter）")]
    [DisallowMultipleComponent, RequireComponent(typeof(Light))]
    public sealed class LightShaftEmitter : MonoBehaviour
    {
        [Min(0), Tooltip("描画選別の優先倍率。1=通常、値を上げると見せ場を優先。0=選別対象外。")]
        public float m_priority = 1;
        [Min(0)]
        [UnityEngine.Serialization.FormerlySerializedAs("scattering")]
        public float m_scattering = 1;
        [Tooltip("URPの影を霧の散乱に反映する。")]
        [UnityEngine.Serialization.FormerlySerializedAs("shadows")]
        public bool m_castShadowsFlag = true;

        [Range(0, 1), Tooltip("Lightの円錐内で光を外側へ分布。照射角自体はLightのSpot Angleで調整します。")]
        public float m_beamSpread;
        [Range(0, 1)] public float m_edgeSoftness;
        [Range(0, 1)] public float m_streakVariation;
        [Range(.5f, 6)] public float m_streakFrequency = 2;
        [Range(0, 999)] public int m_streakSeed;
        [Range(0, .1f), Tooltip("濃淡のゆっくりした移動速度。0なら固定。0.005程度から調整。")]
        public float m_streakFlow;
        Vector4 m_profileKey;
        float m_normalization = 1;
        bool m_profileReadyFlag;

        /// <summary>シェーダーへ渡す光柱の形状パラメータ（広がり・柔らかさ・ばらつき・正規化係数）。正規化係数は形状が変わったときだけ再計算する。</summary>
        public Vector4 ComputeBeamParameters(Light light)
        {
            float spread = Mathf.Clamp01(m_beamSpread), softness = Mathf.Clamp01(m_edgeSoftness);
            float outer = Mathf.Cos(light.spotAngle * Mathf.Deg2Rad * .5f), inner = Mathf.Cos(light.innerSpotAngle * Mathf.Deg2Rad * .5f);
            var key = new Vector4(spread, softness, outer, inner);
            if (!m_profileReadyFlag || key != m_profileKey)
            {
                m_profileReadyFlag = true; m_profileKey = key;
                // 立体角は cos(θ) に対して一様。変更後の半径方向プロファイルを従来の cone^2 プロファイルに対して正規化する。
                // 対蹠（反対側）のノイズマスクは角度方向の平均が0なので正規化に影響しない。
                double original = 0, modified = 0;
                for (int i = 0; i < 128; i++)
                {
                    float cosine = Mathf.Lerp(outer, 1, (i + .5f) / 128);
                    float cone = Mathf.Clamp01((cosine - outer) / Mathf.Max(.001f, inner - outer));
                    original += cone * cone; modified += EvaluateProfile(cone, spread, softness);
                }
                m_normalization = (float)(original / System.Math.Max(.000001, modified));
            }
            return new Vector4(spread, softness, Mathf.Clamp01(m_streakVariation), m_normalization);
        }
        public Vector4 PatternSettings => new Vector4(Mathf.Clamp(m_streakFrequency, .5f, 6), Mathf.Clamp(m_streakSeed, 0, 999), Mathf.Clamp(m_streakFlow, 0, .1f), 0);
        /// <summary>筋模様の向き（cos, sin）と時間で流れるオフセット（x, y）。</summary>
        public Vector4 ComputePatternAxes(float time)
        {
            var settings = PatternSettings;
            float angle = settings.y * 2.39996323f;
            // 向きは固定する。回転ではなくゆっくり平行移動させることで、格子状の筋が回って見えるのを防ぐ。
            float drift = time * settings.z;
            return new Vector4(Mathf.Cos(angle), Mathf.Sin(angle),
                Mathf.Repeat(settings.y * .754877666f + .173f + drift, 1),
                Mathf.Repeat(settings.y * .569840296f + .619f + drift * .37f, 1));
        }
        /// <summary>光柱の見た目のプリセット。0=従来の光柱、1=広がり＋柔らかい輪郭、2=さらに濃淡・筋のばらつき。</summary>
        public void ApplyBeamStyle(int style)
        {
            m_beamSpread = style == 0 ? 0 : .75f;
            m_edgeSoftness = style == 0 ? 0 : .5f;
            m_streakVariation = style < 2 ? 0 : .75f;
            m_streakFrequency = 1.6f;
            m_streakFlow = 0;
        }
        /// <summary>円錐内の位置 cone（0=縁、1=中心）に対する光の分布。</summary>
        public static float EvaluateProfile(float cone, float spread, float softness)
        {
            float profile = Mathf.Lerp(cone * cone, cone * (2 - cone), spread);
            return Mathf.Lerp(profile, profile * profile * (3 - 2 * profile), softness);
        }
    }
}
