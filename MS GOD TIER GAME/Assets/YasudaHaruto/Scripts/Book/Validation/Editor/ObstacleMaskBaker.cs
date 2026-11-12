using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Bake occupancy once in the editor, never enable Read/Write on the source artwork.
public sealed class ObstacleMaskBaker : AssetPostprocessor
{
    private static bool s_pending;
    [InitializeOnLoadMethod]
    private static void Initialize() => Schedule();

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] from)
    {
        foreach (string path in imported)
            if (path.StartsWith("Assets/YasudaHaruto/Data/Texture/Dirt/") ||
                path.StartsWith("Assets/YasudaHaruto/Resources/BookObstacles/")) { Schedule(); return; }
    }

    private static void Schedule()
    {
        if (s_pending) return;
        s_pending = true;
        EditorApplication.delayCall += () =>
        {
            s_pending = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Bake(Resources.Load<ObstacleData>("BookObstacles/Rub"));
        };
    }

    public static void Bake(ObstacleData data)
    {
        if (data == null) return;
        var textures = new List<Texture2D>();
        if (data.Texture != null) textures.Add(data.Texture);
        for (int slot = 0; slot < 4; slot++)
            for (int part = 0; part <= (slot == 3 ? data.ExtraPartCount : 0); part++)
            {
                Texture2D texture = data.GetPartTexture(slot, part);
                if (texture != null && !textures.Contains(texture)) textures.Add(texture);
            }
        var masks = new List<ObstacleAlphaMask>();
        foreach (Texture2D texture in textures)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (!File.Exists(path)) continue;
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!decoded.LoadImage(File.ReadAllBytes(path))) continue;
                Color32[] pixels = decoded.GetPixels32();
                float scale = Mathf.Min(1f, 256f / Mathf.Max(decoded.width, decoded.height));
                int width = Mathf.Max(1, Mathf.CeilToInt(decoded.width * scale));
                int height = Mathf.Max(1, Mathf.CeilToInt(decoded.height * scale));
                var bits = new byte[(width * height + 7) / 8];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int sx = Mathf.Min(decoded.width - 1, (int)((x + 0.5f) * decoded.width / width));
                        int sy = Mathf.Min(decoded.height - 1, (int)((y + 0.5f) * decoded.height / height));
                        if (pixels[sy * decoded.width + sx].a < 26) continue;
                        int index = y * width + x;
                        bits[index / 8] |= (byte)(1 << (index % 8));
                    }
                masks.Add(new ObstacleAlphaMask(texture, width, height, bits));
            }
            finally { Object.DestroyImmediate(decoded); }
        }
        if (!data.SetHitMasks(masks.ToArray())) return;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
    }
}

[CustomEditor(typeof(ObstacleData))]
public sealed class ObstacleDataInspector : Editor
{
    public override void OnInspectorGUI()
    {
        if (DrawDefaultInspector()) ObstacleMaskBaker.Bake((ObstacleData)target);
    }
}
