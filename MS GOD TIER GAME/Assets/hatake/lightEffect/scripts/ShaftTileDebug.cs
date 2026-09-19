using UnityEngine;

namespace LightShaftLab
{
    public enum ShaftTileDebugMode { Off, TotalCandidates, SpotCandidates, FogCandidates }

    // Transient diagnostics: no scene or Volume Profile is modified.
    public static class ShaftTileDebug
    {
        public static Camera Camera;
        public static ShaftTileDebugMode Mode;
        public static float Opacity = .55f;
        public static ShaftTileDebugMode ForCamera(Camera camera) => Camera == camera ? Mode : ShaftTileDebugMode.Off;
        public static void Clear() { Camera = null; Mode = ShaftTileDebugMode.Off; }
    }
}
