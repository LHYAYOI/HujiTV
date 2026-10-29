using UnityEngine;
using UnityEngine.UI;

public static class RuneSpellPalette
{
    // GPU readback supports imported sprites without changing Read/Write or the source asset.
    public static Color Sample(Image image, Color fallback)
    {
        if (image == null || image.sprite == null) return fallback;
        Sprite sprite = image.sprite;
        var target = RenderTexture.GetTemporary(32, 32, 0, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Rect rect = sprite.textureRect;
            Texture texture = sprite.texture;
            Graphics.Blit(texture, target, new Vector2(rect.width / texture.width, rect.height / texture.height),
                new Vector2(rect.x / texture.width, rect.y / texture.height));
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 32, 32), 0, 0);
            pixels.Apply();
            Color sum = Color.black;
            float total = 0f;
            foreach (Color pixel in pixels.GetPixels())
            {
                Color.RGBToHSV(pixel, out _, out float saturation, out float value);
                float weight = pixel.a * saturation * saturation * value;
                if (pixel.a < 0.1f || saturation < 0.15f || value < 0.15f) continue;
                sum += pixel * weight;
                total += weight;
            }
            if (total < 0.001f) return fallback;
            Color result = sum / total;
            Color.RGBToHSV(result, out float hue, out float sat, out float brightness);
            result = Color.HSVToRGB(hue, Mathf.Max(0.45f, sat), Mathf.Max(0.85f, brightness));
            result *= image.color;
            result.a = 1f;
            return result;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            if (Application.isPlaying) Object.Destroy(pixels); else Object.DestroyImmediate(pixels);
        }
    }
}
