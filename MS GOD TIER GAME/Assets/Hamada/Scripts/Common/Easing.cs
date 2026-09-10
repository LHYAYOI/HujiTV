//
//イージング関数管理クラス
//濱田ルイス
//

using UnityEngine;

public static class Easing
{
    private const float PI = Mathf.PI;

    // イーズイン・サイン
    public static float EaseInSine(float t)
    {
        return 1.0f - Mathf.Cos((t * PI) * 0.5f);
    }

    // イーズアウト・サイン
    public static float EaseOutSine(float t)
    {
        return Mathf.Sin((t * PI) * 0.5f);
    }

    // イーズイン・アウト・サイン
    public static float EaseInOutSine(float t)
    {
        return -0.5f * (Mathf.Cos(PI * t) - 1.0f);
    }

    // イーズイン・クアドラティック
    public static float EaseInQuad(float t)
    {
        return t * t;
    }

    // イーズアウト・クアドラティック
    public static float EaseOutQuad(float t)
    {
        return 1.0f - (1.0f - t) * (1.0f - t);
    }

    // イーズイン・アウト・クアドラティック
    public static float EaseInOutQuad(float t)
    {
        if (t < 0.5f)
        {
            return 2.0f * t * t;
        }
        else
        {
            return 1.0f - Mathf.Pow(-2.0f * t + 2.0f, 2.0f) * 0.5f;
        }
    }

    // イーズイン・キュービック
    public static float EaseInCubic(float t)
    {
        return t * t * t;
    }

    // イーズインの乗指定可能関数
    public static float EaseInPow(float t,int pow)
    {
        return Mathf.Pow(t, pow);
    }

    // イーズアウト・キュービック
    public static float EaseOutCubic(float t)
    {
        return 1.0f - Mathf.Pow(1.0f - t, 3.0f);
    }

    // イーズイン・アウト・キュービック
    public static float EaseInOutCubic(float t)
    {
        if (t < 0.5f)
        {
            return 4.0f * t * t * t;
        }
        else
        {
            return 1.0f - Mathf.Pow(-2.0f * t + 2.0f, 3.0f) * 0.5f;
        }
    }

    // イーズイン・クォーティック
    public static float EaseInQuart(float t)
    {
        return t * t * t * t;
    }

    // イーズアウト・クォーティック
    public static float EaseOutQuart(float t)
    {
        return 1.0f - Mathf.Pow(1.0f - t, 4.0f);
    }

    // イーズイン・アウト・クォーティック
    public static float EaseInOutQuart(float t)
    {
        if (t < 0.5f)
        {
            return 8.0f * t * t * t * t;
        }
        else
        {
            return 1.0f - Mathf.Pow(-2.0f * t + 2.0f, 4.0f) * 0.5f;
        }
    }

    // イーズイン・サイクル
    public static float EaseInCirc(float t)
    {
        return 1.0f - Mathf.Sqrt(1.0f - Mathf.Pow(t, 2.0f));
    }

    // イーズアウト・サイクル
    public static float EaseOutCirc(float t)
    {
        return Mathf.Sqrt(1.0f - Mathf.Pow(t - 1.0f, 2.0f));
    }

    // イーズイン・アウト・サイクル
    public static float EaseInOutCirc(float t)
    {
        if (t < 0.5f)
        {
            return (1.0f - Mathf.Sqrt(1.0f - Mathf.Pow(2.0f * t, 2.0f))) * 0.5f;
        }
        else
        {
            return (Mathf.Sqrt(1.0f - Mathf.Pow(-2.0f * t + 2.0f, 2.0f)) + 1.0f) * 0.5f;
        }
    }

    // イーズイン・バック
    public static float EaseInBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1.0f;
        return c3 * t * t * t - c1 * t * t;
    }

    // イーズアウト・バック
    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1.0f;

        return 1.0f + c3 * Mathf.Pow(t - 1.0f, 3.0f) + c1 * Mathf.Pow(t - 1.0f, 2.0f);
    }

    // イーズインアウト・バック
    public static float EaseInOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c2 = c1 * 1.525f;
        if (t < 0.5f)
        {
            return Mathf.Pow(2.0f * t, 2.0f) * ((c2 + 1.0f) * 2.0f * t - c2) * 0.5f;
        }
        else
        {
            return (Mathf.Pow(2.0f * t - 2.0f, 2.0f) * ((c2 + 1) * (t * 2.0f - 2.0f) + c2) + 2.0f) * 0.5f;
        }
    }

    // イーズイン・エラスティック
    public static float EaseInElastic(float t)
    {
        const float c4 = (2.0f * PI) / 3;

        return Mathf.Pow(2.0f, 10.0f * t - 10.0f) * Mathf.Sin((t * 10.0f - 10.75f) * c4);
    }

    // イーズアウト・エラスティック
    public static float EaseOutElastic(float t)
    {
        const float c4 = (2.0f * PI) / 3;
        return 1.0f - Mathf.Pow(2.0f, -10.0f * t) * Mathf.Sin((t * 10.0f - 0.75f) * c4);
    }

    // イーズイン・アウト・エラスティック
    public static float EaseInOutElastic(float t)
    {
        const float c5 = (2.0f * PI) / 4.5f;
        if (t < 0.5f)
        {
            return (Mathf.Pow(2.0f, 20.0f * t - 10.0f) * Mathf.Sin((20.0f * t - 11.125f) * c5)) * 0.5f;
        }
        else
        {
            return (2.0f - Mathf.Pow(2.0f, -20.0f * t + 10.0f) * Mathf.Sin((20.0f * t - 11.125f) * c5)) * 0.5f;
        }
    }

    // イーズアウト・バウンス
    public static float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1.0f / d1)
        {
            return n1 * t * t;
        }
        else if (t < 2.0f / d1)
        {
            return n1 * (t -= 1.5f / d1) * t + 0.75f;
        }
        else if (t < 2.5f / d1)
        {
            return n1 * (t -= 2.25f / d1) * t + 0.9375f;
        }
        else
        {
            return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }
    }

    // イーズイン・バウンス
    public static float EaseInBounce(float t)
    {
        return 1.0f - EaseOutBounce(1.0f - t);
    }

    // イーズイン・アウト・バウンス
    public static float EaseInOutBounce(float t)
    {
        if (t < 0.5f)
        {
            return (1.0f - EaseOutBounce(1.0f - 2.0f * t)) * 0.5f;
        }
        else
        {
            return (1.0f + EaseOutBounce(2.0f * t - 1.0f)) * 0.5f;
        }
    }

    public static float Smooth01(float t)
    {
        return t * t * (3.0f - 2.0f * t);
    }
}
