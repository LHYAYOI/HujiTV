using System.IO;
using UnityEditor;
using UnityEngine;

namespace hatake
{
    /// <summary>
    /// 調査用：ページ画像の元の PNG を、取り込み設定と同じ大きさ（長辺 2048）に縮めたものを
    /// 圧縮なし／ASTC 6x6／ASTC 4x4 で Logs/astc/ に書き出す。アセットは変更しない
    /// </summary>
    internal static class BookAstcTest
    {
        private const string Source = "Assets/YasudaHaruto/Data/PageView/Images/Page/Obake_Obake1.png";

        public static string Run()
        {
            string dir = Path.GetFullPath("Logs/astc");
            Directory.CreateDirectory(dir);
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(Source));
            float scale = 2048f / Mathf.Max(src.width, src.height);
            int w = Mathf.RoundToInt(src.width * scale), h = Mathf.RoundToInt(src.height * scale);
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            // 縮小は GPU の双線形では荒いので、2倍ずつ縮める
            Texture current = src;
            var temps = new System.Collections.Generic.List<RenderTexture>();
            int cw = src.width, ch = src.height;
            while (cw / 2 >= w && ch / 2 >= h)
            {
                cw /= 2; ch /= 2;
                var t = RenderTexture.GetTemporary(cw, ch, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(current, t);
                temps.Add(t);
                current = t;
            }
            Graphics.Blit(current, rt);
            Texture2D baseTex = Read(rt, w, h);
            foreach (var t in temps) RenderTexture.ReleaseTemporary(t);
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(dir, "raw.png"), baseTex.EncodeToPNG());
            string result = "src=" + src.width + "x" + src.height + " out=" + w + "x" + h;
            foreach (var (format, name) in new[] { (TextureFormat.ASTC_6x6, "astc6x6"), (TextureFormat.ASTC_4x4, "astc4x4") })
            {
                var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.SetPixels32(baseTex.GetPixels32());
                copy.Apply();
                EditorUtility.CompressTexture(copy, format, TextureCompressionQuality.Normal);
                // 圧縮したデータを CPU で戻す（GPU が ASTC を読めないエディタでも動く）
                var decoded = new Texture2D(w, h, TextureFormat.RGBA32, false);
                decoded.SetPixels32(copy.GetPixels32());
                decoded.Apply();
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), decoded.EncodeToPNG());
                result += " " + name + "=" + copy.format;
                Object.DestroyImmediate(copy);
                Object.DestroyImmediate(decoded);
            }
            Object.DestroyImmediate(baseTex);
            Object.DestroyImmediate(src);
            return result;
        }

        private static Texture2D Read(RenderTexture rt, int w, int h)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            return tex;
        }
    }
}
