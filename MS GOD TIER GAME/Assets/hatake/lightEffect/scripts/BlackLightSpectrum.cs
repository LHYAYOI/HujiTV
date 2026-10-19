using UnityEngine;

namespace LightShaftLab
{
    /// <summary>ブラックライトの光源の種類。発光スペクトルとフィルターの有無が変わる。</summary>
    public enum BLACK_LIGHT_LAMP_TYPE
    {
        [InspectorName("LED（フィルターなし）")] LED = 0,
        [InspectorName("LED＋可視光カットフィルター（ZWB2/ウッドガラス）")] FILTERED_LED = 1,
        [InspectorName("ブラックライト蛍光管（BLB）")] TUBE = 2,
    }

    /// <summary>
    /// ブラックライトの発光スペクトルを CIE 1931 等色関数で積分し、「目（カメラ）に見える成分」を求める。
    /// 出力（Power）は放射のエネルギー量として扱い、555nm の光なら同じ値のIntensityになる単位にそろえる。
    /// 400nm の視感度は明所視（明るい場所・カメラ）で555nmの約1/2500しかなく、同じ出力でも紫はほとんど映らない。
    /// 暗い部屋では目が暗順応して桿体（暗所視、CIE 1951）が働き、400nm付近の感度が約20倍になるため紫が見えるようになる。
    /// </summary>
    public static class BlackLightSpectrum
    {
        public struct Result
        {
            /// <summary>暗順応 0（明所視）〜1（暗所視）での出力1あたりの明るさ。</summary>
            public float Luminance(float darkAdaptation) => Mathf.Lerp(m_luminousFraction, m_scotopicFraction, Mathf.Clamp01(darkAdaptation));

            /// <summary>見える色（リニアsRGB、輝度1に正規化）。明るさは <see cref="Luminance"/> を掛ける。</summary>
            public Color m_visibleChroma;
            /// <summary>出力1あたりの輝度（CIE Y、明所視）。555nm の単色光で1。</summary>
            public float m_luminousFraction;
            /// <summary>出力1あたりの暗所視の明るさ（CIE 1951 V'、507nmで1）。</summary>
            public float m_scotopicFraction;
            /// <summary>フィルターを通過して蛍光体へ届く割合（フィルターなしは1）。</summary>
            public float m_transmittedFraction;
        }

        // CIE 1931 2°等色関数（360〜780nm、10nm間隔）。
        const float TABLE_START = 360, TABLE_STEP = 10;
        static readonly float[] g_x =
        {
            0.0001299f, 0.0004149f, 0.001368f, 0.004243f, 0.01431f, 0.04351f, 0.13438f, 0.2839f, 0.34828f, 0.3362f,
            0.2908f, 0.19536f, 0.09564f, 0.03201f, 0.0049f, 0.0093f, 0.06327f, 0.1655f, 0.2904f, 0.43345f,
            0.5945f, 0.7621f, 0.9163f, 1.0263f, 1.0622f, 1.0026f, 0.85445f, 0.6424f, 0.4479f, 0.2835f,
            0.1649f, 0.0874f, 0.04677f, 0.0227f, 0.011359f, 0.00579f, 0.002899f, 0.00144f, 0.00069f, 0.000332f,
            0.000166f, 0.000083f, 0.000042f,
        };
        static readonly float[] g_y =
        {
            0.000003917f, 0.00001239f, 0.000039f, 0.00012f, 0.000396f, 0.00121f, 0.004f, 0.0116f, 0.023f, 0.038f,
            0.06f, 0.09098f, 0.13902f, 0.20802f, 0.323f, 0.503f, 0.71f, 0.862f, 0.954f, 0.99495f,
            0.995f, 0.952f, 0.87f, 0.757f, 0.631f, 0.503f, 0.381f, 0.265f, 0.175f, 0.107f,
            0.061f, 0.032f, 0.017f, 0.00821f, 0.004102f, 0.002091f, 0.001047f, 0.00052f, 0.000249f, 0.00012f,
            0.00006f, 0.00003f, 0.000015f,
        };
        static readonly float[] g_z =
        {
            0.0006061f, 0.001946f, 0.00645f, 0.02005f, 0.06785f, 0.2074f, 0.6456f, 1.3856f, 1.74706f, 1.77211f,
            1.6692f, 1.28764f, 0.81295f, 0.46518f, 0.272f, 0.1582f, 0.07825f, 0.04216f, 0.0203f, 0.00875f,
            0.0039f, 0.0021f, 0.00165f, 0.0011f, 0.0008f, 0.00034f, 0.00019f, 0.00005f, 0.00002f, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        };

        // CIE 1951 暗所視の比視感度 V'(λ)（360〜780nm、10nm間隔。360/370nmは外挿）。
        static readonly float[] g_scotopic =
        {
            0.000042f, 0.000157f, 0.000589f, 0.002209f, 0.00929f, 0.03484f, 0.0966f, 0.1998f, 0.3281f, 0.455f,
            0.567f, 0.676f, 0.793f, 0.904f, 0.982f, 0.997f, 0.935f, 0.811f, 0.65f, 0.481f,
            0.3288f, 0.2076f, 0.1212f, 0.0655f, 0.03315f, 0.01593f, 0.00737f, 0.003335f, 0.001497f, 0.000677f,
            0.000313f, 0.000148f, 0.0000715f, 0.0000353f, 0.0000178f, 0.0000091f, 0.0000045f, 0.0000023f, 0.0000012f, 0.0000006f,
            0.0000003f, 0.00000015f, 0.00000008f,
        };

        /// <summary>等色関数。紫外側は1桁ずつ急に落ちるので対数で補間する（線形だと紫が数倍明るくなる）。</summary>
        static float Sample(float[] table, float wavelength)
        {
            float tablePosition = (wavelength - TABLE_START) / TABLE_STEP;
            if (tablePosition < 0)
            {
                // 360nm未満は同じ傾きで外挿（視感度はほぼ0）。
                return table[0] * Mathf.Pow(table[0] / table[1], -tablePosition);
            }
            int i = Mathf.Min((int)tablePosition, table.Length - 2);
            float fraction = Mathf.Clamp01(tablePosition - i), lower = table[i], upper = table[i + 1];
            return lower > 0 && upper > 0 ? lower * Mathf.Pow(upper / lower, fraction) : Mathf.Lerp(lower, upper, fraction);
        }

        /// <summary>可視光カットフィルター（ZWB2/ウッドガラス）の透過率。365nmで約85%、405nmで約30%、420nmで約2%、435nm以上はほぼ0。</summary>
        public static float EvaluateFilterTransmission(float wavelength) => 0.85f / (1 + Mathf.Exp((wavelength - 402f) / 5f));

        static float SkewedGaussian(float wavelength, float peak, float fwhm, float skew)
        {
            // 長波長側に裾が広い非対称ガウス（LED・蛍光体の典型的な形）。
            float sigma = fwhm / 2.3548f, shortSide = sigma * 2 / (1 + skew), longSide = shortSide * skew;
            float offset = (wavelength - peak) / (wavelength < peak ? shortSide : longSide);
            return Mathf.Exp(-0.5f * offset * offset);
        }

        /// <summary>フィルター前の発光スペクトル（相対値）。</summary>
        public static float EvaluateEmission(BLACK_LIGHT_LAMP_TYPE type, float peak, float wavelength)
        {
            if (type == BLACK_LIGHT_LAMP_TYPE.TUBE)
            {
                // 紫外蛍光体（半値幅約20nm）＋水銀の輝線。BLB管はウッドガラスが外管を兼ねる。
                float phosphor = SkewedGaussian(wavelength, peak, 20, 1);
                float mercury = 0.6f * Mathf.Exp(-0.5f * Square(wavelength - 404.7f)) + 0.8f * Mathf.Exp(-0.5f * Square(wavelength - 435.8f));
                return phosphor + mercury;
            }
            // UV LED：半値幅は365nmで約10nm、405nmで約15nm。長波長側に指数的な裾を持つ。
            float fwhm = 10 + 5 * Mathf.InverseLerp(365, 405, peak);
            float band = SkewedGaussian(wavelength, peak, fwhm, 1.25f);
            float tail = wavelength > peak ? 0.004f * Mathf.Exp(-(wavelength - peak) / 12f) : 0;
            return Mathf.Max(band, tail);
        }

        static float Square(float value) => value * value;

        static bool HasFilter(BLACK_LIGHT_LAMP_TYPE type) => type != BLACK_LIGHT_LAMP_TYPE.LED;

        /// <summary>波長と光源の種類から、可視成分と透過率を求める（1nm刻みの数値積分）。</summary>
        public static Result Evaluate(BLACK_LIGHT_LAMP_TYPE type, float peakWavelength)
        {
            double total = 0, transmitted = 0, x = 0, y = 0, z = 0, scotopic = 0;
            for (int wavelength = 300; wavelength <= 780; wavelength++)
            {
                float emission = EvaluateEmission(type, peakWavelength, wavelength);
                float passed = emission * (HasFilter(type) ? EvaluateFilterTransmission(wavelength) : 1);
                total += emission; transmitted += passed;
                x += passed * Sample(g_x, wavelength); y += passed * Sample(g_y, wavelength); z += passed * Sample(g_z, wavelength);
                scotopic += passed * Sample(g_scotopic, wavelength);
            }
            if (total <= 0) return default;
            x /= total; y /= total; z /= total; scotopic /= total;

            // XYZ → リニアsRGB。近紫外の紫はsRGBの色域外（Gが負）なので、負の成分を0にしてから輝度で正規化する。
            float red = (float)(3.2406 * x - 1.5372 * y - 0.4986 * z);
            float green = (float)(-0.9689 * x + 1.8758 * y + 0.0415 * z);
            float blue = (float)(0.0557 * x - 0.2040 * y + 1.0570 * z);
            red = Mathf.Max(0, red); green = Mathf.Max(0, green); blue = Mathf.Max(0, blue);
            float luminance = 0.2126f * red + 0.7152f * green + 0.0722f * blue;
            // 色が求まらない（可視成分0）ときは近紫外の見え方（青紫）を使う。
            if (luminance <= 0) { red = 0.07f; green = 0; blue = 1; luminance = 0.2126f * red + 0.0722f * blue; }
            return new Result
            {
                m_visibleChroma = new Color(red / luminance, green / luminance, blue / luminance, 1),
                m_luminousFraction = (float)y,
                m_scotopicFraction = (float)scotopic,
                m_transmittedFraction = (float)(transmitted / total),
            };
        }
    }
}
