using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace LightShaftLab
{
    // 霧のノイズ用の 3D テクスチャ。格子点 (x,y,z) に、シェーダーの Hash(float3) と同じ値を入れる。
    // ShaftRaymarch.hlsl の Noise() が、smoothstep 済みの位置で線形補間して引く（以前の 8 回の Hash 計算の代わり）。
    public static class ShaftNoiseVolume
    {
        // ShaftRaymarch.hlsl の SHAFT_NOISE_SIZE と合わせる
        public const int TEXTURE_SIZE = 64;

        public static Texture3D CreateTexture()
        {
            var texture = new Texture3D(TEXTURE_SIZE, TEXTURE_SIZE, TEXTURE_SIZE, GraphicsFormat.R16_UNorm, TextureCreationFlags.None)
            {
                name = "LightShaft fog noise",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            var values = new ushort[TEXTURE_SIZE * TEXTURE_SIZE * TEXTURE_SIZE];
            for (int z = 0; z < TEXTURE_SIZE; z++)
            {
                for (int y = 0; y < TEXTURE_SIZE; y++)
                {
                    for (int x = 0; x < TEXTURE_SIZE; x++)
                    {
                        values[(z * TEXTURE_SIZE + y) * TEXTURE_SIZE + x] = (ushort)Mathf.RoundToInt(Hash(x, y, z) * 65535);
                    }
                }
            }
            texture.SetPixelData(values, 0);
            texture.Apply(false, true);
            return texture;
        }

        // シェーダーの Hash(float3 p) と同じ式
        // p=frac(p*0.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z);
        static float Hash(float x, float y, float z)
        {
            x = Frac(x * 0.1031f);
            y = Frac(y * 0.1031f);
            z = Frac(z * 0.1031f);
            float offset = x * (y + 33.33f) + y * (z + 33.33f) + z * (x + 33.33f);
            x += offset;
            y += offset;
            z += offset;
            return Frac((x + y) * z);
        }

        static float Frac(float value) => value - Mathf.Floor(value);
    }
}
