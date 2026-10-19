using UnityEngine;

namespace LightShaftLab
{
    public enum SHAFT_TILE_DEBUG_MODE
    {
        [InspectorName("Off")] OFF,
        [InspectorName("Total Candidates")] TOTAL_CANDIDATES,
        [InspectorName("Spot Candidates")] SPOT_CANDIDATES,
        [InspectorName("Fog Candidates")] FOG_CANDIDATES
    }

    // 一時的な診断表示。シーンや Volume Profile は一切変更しない。
    public static class ShaftTileDebug
    {
        public static Camera g_camera;
        public static SHAFT_TILE_DEBUG_MODE g_mode;
        public static float g_opacity = .55f;
        public static bool g_showRefinementCoverageFlag;
        public static SHAFT_TILE_DEBUG_MODE ForCamera(Camera camera) => g_camera == camera ? g_mode : SHAFT_TILE_DEBUG_MODE.OFF;
        public static void Clear() { g_camera = null; g_mode = SHAFT_TILE_DEBUG_MODE.OFF; g_showRefinementCoverageFlag = false; }
    }
}
