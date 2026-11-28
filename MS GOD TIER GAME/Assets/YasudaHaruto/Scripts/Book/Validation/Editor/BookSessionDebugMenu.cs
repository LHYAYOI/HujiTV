using UnityEditor;
using UnityEngine;

namespace YasudaHaruto.BookValidation.Editor
{
    internal static class BookSessionDebugMenu
    {
        private const string MenuPath = "Tools/YasudaHaruto/Restart PC Game Session";

        [MenuItem(MenuPath)]
        private static void Restart()
        {
            var server = BookNetworkServer.Instance;
            if (CanRestart() && !server.RestartActiveGameScene())
                Debug.LogWarning("Could not restart the current PC game scene.", server);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRestart()
        {
            var server = BookNetworkServer.Instance;
            return EditorApplication.isPlaying && !EditorApplication.isPaused &&
                server != null && server.CanRestartActiveGameScene;
        }
    }
}
