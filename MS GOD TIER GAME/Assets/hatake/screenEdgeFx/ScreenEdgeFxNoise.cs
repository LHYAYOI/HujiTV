using UnityEngine;

namespace Hatake.ScreenFx
{
    /// <summary>
    /// 縁取り演出用のノイズテクスチャを実行時に作る(アセット不要・上下左右につながる)。
    /// R: もや(なだらか)  G: ざらつき(細かく、濃淡が強い)  B: 星(まばらな点)  A: 未使用
    /// シェーダーではノイズを計算せず、このテクスチャを読むだけにしている。
    /// </summary>
    internal static class ScreenEdgeFxNoise
    {
        private const int k_starCount = 7;

        public static Texture2D Create(int size)
        {
            Color32[] pixels = new Color32[size * size];
            float[] stars = BuildStars(size);

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;

                    // もや: 低い周波数を 4 段
                    float cloud = Fbm(u, v, 3, 4, 11);
                    cloud = Mathf.Clamp01((cloud - 0.5f) * 2.0f + 0.5f);

                    // ざらつき: 高い周波数を 2 種類重ねて、濃淡を強める
                    float a = Fbm(u, v, 10, 5, 23);
                    float b = Fbm(u, v, 24, 3, 37);
                    float grunge = a * 0.55f + b * 0.45f;
                    grunge = Mathf.Clamp01((grunge - 0.5f) * 1.9f + 0.5f);

                    pixels[y * size + x] = new Color32(ToByte(cloud), ToByte(grunge), ToByte(stars[y * size + x]), 255);
                }
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "ScreenEdgeFx Noise (Runtime)",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>整数 3 つから 0〜1 の決まった乱数を返す。</summary>
        public static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / 16777215f;
            }
        }

        private static byte ToByte(float v)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
        }

        // 周期 period でつながる値ノイズ(u, v は 0〜1)
        private static float ValueNoise(float u, float v, int period, int seed)
        {
            float x = u * period;
            float y = v * period;
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float fx = x - xi;
            float fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            int x0 = Wrap(xi, period);
            int y0 = Wrap(yi, period);
            int x1 = Wrap(xi + 1, period);
            int y1 = Wrap(yi + 1, period);

            float a = Hash01(x0, y0, seed);
            float b = Hash01(x1, y0, seed);
            float c = Hash01(x0, y1, seed);
            float d = Hash01(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static int Wrap(int i, int period)
        {
            int m = i % period;
            return m < 0 ? m + period : m;
        }

        private static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f;
            float amp = 0.5f;
            float total = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += ValueNoise(u, v, period, seed + o * 101) * amp;
                total += amp;
                amp *= 0.5f;
                period *= 2;
            }

            return sum / total;
        }

        // まばらな丸い点。大きさと明るさをばらつかせ、端をまたぐ点は反対側にも描く
        private static float[] BuildStars(int size)
        {
            float[] stars = new float[size * size];
            for (int i = 0; i < k_starCount; i++)
            {
                float cx = Hash01(i, 1, 71) * size;
                float cy = Hash01(i, 2, 71) * size;
                float radius = 1.1f + Hash01(i, 3, 71) * 0.9f;
                float bright = 0.55f + Hash01(i, 4, 71) * 0.45f;

                int r = Mathf.CeilToInt(radius + 1f);
                int px = Mathf.FloorToInt(cx);
                int py = Mathf.FloorToInt(cy);
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = px + dx;
                        int y = py + dy;
                        float ddx = x + 0.5f - cx;
                        float ddy = y + 0.5f - cy;
                        float d = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                        float value = Mathf.Clamp01(radius - d + 0.5f) * bright;
                        int index = Wrap(y, size) * size + Wrap(x, size);
                        stars[index] = Mathf.Max(stars[index], value);
                    }
                }
            }

            return stars;
        }
    }
}
