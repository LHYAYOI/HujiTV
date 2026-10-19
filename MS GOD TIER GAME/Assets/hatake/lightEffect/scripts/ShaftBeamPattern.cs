using UnityEngine;

namespace LightShaftLab
{
    // レンダラーごとに1枚だけ持つ、不変のミップマップ付き角度マスク。選択された全ライトで共有する。
    // 不規則なビームが初めて使われたときにだけ生成する。シミュレーションは行わない。
    public static class ShaftBeamPattern
    {
        public const int TEXTURE_SIZE = 128;
        public static Texture2D CreateTexture()
        {
            var texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGBA32, true, true)
            {
                name = "LightShaft irregular beam mask",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 0
            };
            var pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];
            for (int y = 0; y < TEXTURE_SIZE; y++) for (int x = 0; x < TEXTURE_SIZE; x++)
            {
                float u = (x + .5f) / TEXTURE_SIZE, v = (y + .5f) / TEXTURE_SIZE;
                float cloud = .58f * Noise(u, v, 4, 17) + .28f * Noise(u, v, 8, 43) + .14f * Noise(u, v, 16, 89);
                float detail = .65f * Noise(u, v, 16, 137) + .35f * Noise(u, v, 32, 211);
                pixels[y * TEXTURE_SIZE + x] = new Color(cloud, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.28f, .72f, detail)), .5f, 1);
            }
            texture.SetPixels(pixels); texture.Apply(true, true);
            return texture;
        }
        // タイル可能なバリューノイズ（格子点のハッシュ値を滑らかに補間する）。
        static float Noise(float u, float v, int cells, uint seed)
        {
            float x = u * cells, y = v * cells;
            int cellX = Mathf.FloorToInt(x), cellY = Mathf.FloorToInt(y);
            float blendX = Mathf.SmoothStep(0, 1, x - cellX), blendY = Mathf.SmoothStep(0, 1, y - cellY);
            return Mathf.Lerp(Mathf.Lerp(Hash(cellX, cellY, cells, seed), Hash(cellX + 1, cellY, cells, seed), blendX),
                Mathf.Lerp(Hash(cellX, cellY + 1, cells, seed), Hash(cellX + 1, cellY + 1, cells, seed), blendX), blendY);
        }
        // 格子座標（cells で周期化）とシードから 0～1 の擬似乱数を返す。
        static float Hash(int x, int y, int cells, uint seed)
        {
            unchecked
            {
                uint hashValue = (uint)(x % cells) * 1597334677u ^ (uint)(y % cells) * 3812015801u ^ seed * 2798796415u;
                hashValue = (hashValue ^ (hashValue >> 16)) * 2246822519u; hashValue = (hashValue ^ (hashValue >> 13)) * 3266489917u;
                return (hashValue ^ (hashValue >> 16)) / (float)uint.MaxValue;
            }
        }
    }
}
