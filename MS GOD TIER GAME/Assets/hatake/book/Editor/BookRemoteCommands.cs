using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace hatake.EditorTools
{
    /// <summary>
    /// 計測の自動化用：Logs/hatake_cmd.txt に書いたコマンドをエディターが拾って実行する（エディターを操作せずに回すため）。
    /// 1行1コマンド：refresh / check / diag / turn / launch / build（Build And Run）。結果は Logs/hatake_cmd_result.txt
    /// </summary>
    [InitializeOnLoad]
    internal static class BookRemoteCommands
    {
        private const string CommandPath = "Logs/hatake_cmd.txt";
        private const string ResultPath = "Logs/hatake_cmd_result.txt";
        private static double s_next;

        static BookRemoteCommands()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < s_next) return;
            s_next = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
            if (!File.Exists(CommandPath)) return;
            string[] commands;
            try
            {
                commands = File.ReadAllLines(CommandPath);
                File.Delete(CommandPath);
            }
            catch (IOException) { return; }

            foreach (string raw in commands)
            {
                string command = raw.Trim();
                if (command.Length == 0) continue;
                string result;
                try { result = Run(command); }
                catch (Exception e) { result = "EXCEPTION " + e; }
                File.AppendAllText(ResultPath, DateTime.Now.ToString("HH:mm:ss") + " " + command + " -> " + result + "\n");
                // refresh でスクリプトが変わったら、ドメインの再読み込み後に残りを続ける
                if (command == "refresh" && EditorApplication.isCompiling)
                {
                    int index = Array.IndexOf(commands, raw);
                    string[] rest = commands.Skip(index + 1).ToArray();
                    if (rest.Length > 0) File.WriteAllLines(CommandPath, rest);
                    return;
                }
            }
        }

        private static string Run(string command)
        {
            // adb <引数>：adb をそのまま実行して出力を Logs/hatake_adb.txt に書く
            if (command.StartsWith("adb "))
            {
                string output = BookAndroidDiagnostics.RunText(BookAndroidDiagnostics.FindAdb(), command.Substring(4));
                File.WriteAllText("Logs/hatake_adb.txt", output);
                return "ok " + output.Length + " chars";
            }
            // build <シーンのパス>：そのシーンだけでビルドして端末で起動する（Build Settings のシーン一覧は変えない）
            if (command.StartsWith("build "))
            {
                return Build(new[] { command.Substring(6).Trim() });
            }
            // play <シーンのパス>：そのシーンを開いて再生する（未保存のシーンがあれば何もしない）。stop で止めて元のシーンへ戻す
            if (command.StartsWith("play "))
            {
                if (EditorApplication.isPlaying) return "already playing";
                for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
                    if (UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i).isDirty) return "dirty scene (not opened)";
                EditorPrefs.SetString("hatake_prev_scene", UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path);
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(command.Substring(5).Trim());
                EditorApplication.EnterPlaymode();
                return "ok";
            }
            if (command == "stop")
            {
                if (!EditorApplication.isPlaying) return "not playing";
                EditorApplication.playModeStateChanged += ReopenPrevious;
                EditorApplication.ExitPlaymode();
                return "ok";
            }
            // screen <名前>：端末の画面を Logs/<名前>.png に保存する
            if (command.StartsWith("screen "))
            {
                string adb = BookAndroidDiagnostics.FindAdb();
                BookAndroidDiagnostics.CaptureScreen(adb, "Logs/" + command.Substring(7).Trim() + ".png");
                return "ok";
            }
            // fps <ミリ秒> <adb shell の引数>：コマンド（入力の操作など）を始め、その間の画面の更新の速さを測る
            if (command.StartsWith("fps "))
            {
                string[] parts = command.Substring(4).Trim().Split(new[] { ' ' }, 2);
                int wait = int.Parse(parts[0]);
                string adb = BookAndroidDiagnostics.FindAdb();
                string package = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
                string layer = BookAndroidDiagnostics.FindLayer(adb, package);
                if (layer.Length == 0) return "no layer (app not running?)";
                BookAndroidDiagnostics.RunText(adb, "shell dumpsys SurfaceFlinger --latency-clear");
                using var input = parts.Length > 1
                    ? System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(adb, "shell " + parts[1])
                    {
                        UseShellExecute = false, CreateNoWindow = true
                    })
                    : null;
                System.Threading.Thread.Sleep(wait);
                string latency = BookAndroidDiagnostics.RunText(adb, "shell dumpsys SurfaceFlinger --latency '" + layer + "'");
                if (input != null && !input.WaitForExit(10000)) input.Kill();
                return BookAndroidDiagnostics.Summarize(latency);
            }
            switch (command)
            {
                case "refresh":
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    return "ok compiling=" + EditorApplication.isCompiling;
                case "check":
                    BookShaderBuildCheck.Run();
                    return "ok";
                case "diag":
                    BookAndroidDiagnostics.Run();
                    return "ok";
                case "turn":
                    BookAndroidDiagnostics.TurnTest();
                    return "ok";
                case "launch":
                    BookAndroidDiagnostics.LaunchLog();
                    return "ok";
                case "scenario":
                    BookAndroidDiagnostics.Scenario();
                    return "ok";
                case "visual":
                    BookAndroidDiagnostics.VisualCheck();
                    return "ok";
                case "build":
                    return Build();
                default:
                    return "unknown";
            }
        }

        private static void ReopenPrevious(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= ReopenPrevious;
            string previous = EditorPrefs.GetString("hatake_prev_scene", "");
            if (previous.Length > 0) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(previous);
        }

        private static string Build(string[] scenes = null)
        {
            string location = EditorUserBuildSettings.GetBuildLocation(BuildTarget.Android);
            // エディタを開き直すと保存先が空になることがある：Logs の下へ（git には入らない）
            if (string.IsNullOrEmpty(location)) location = System.IO.Path.GetFullPath("Logs/hatake_book.apk");
            scenes ??= EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = location,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.AutoRunPlayer
            };
            var report = BuildPipeline.BuildPlayer(options);
            return report.summary.result + " errors=" + report.summary.totalErrors + " scenes=" + string.Join(",", scenes) +
                   " path=" + location + " time=" + report.summary.totalTime;
        }
    }
}
