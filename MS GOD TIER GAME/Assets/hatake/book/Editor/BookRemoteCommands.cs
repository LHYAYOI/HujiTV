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

        private static string Build()
        {
            string location = EditorUserBuildSettings.GetBuildLocation(BuildTarget.Android);
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
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
