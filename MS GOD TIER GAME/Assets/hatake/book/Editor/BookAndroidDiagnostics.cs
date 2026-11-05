using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Android;
using Debug = UnityEngine.Debug;

namespace hatake.EditorTools
{
    /// <summary>
    /// USB でつないだ Android で動いているビルドの様子を、エディターから adb で集める（計測用）。
    /// メニュー「Tools/hatake/Book Android Diagnostics」で実行し、Logs/BookAndroidDiagnostics.txt に書く。
    ///   ・端末名と GPU、温度の目安
    ///   ・logcat の Unity のログ（[BookPerf] の行・エラー）
    ///   ・dumpsys gfxinfo（描画のフレーム時間の分布）
    ///   ・アプリの CPU とメモリ
    /// </summary>
    public static class BookAndroidDiagnostics
    {
        [MenuItem("Tools/hatake/Book Android Diagnostics")]
        public static void Run()
        {
            string adb = FindAdb();
            var report = new StringBuilder();
            report.AppendLine("time " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("adb " + adb);
            string package = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
            report.AppendLine("package " + package);
            Section(report, "devices", adb, "devices -l");
            Section(report, "model", adb, "shell getprop ro.product.model");
            Section(report, "soc", adb, "shell getprop ro.soc.model");
            Section(report, "android", adb, "shell getprop ro.build.version.release");
            Section(report, "display", adb, "shell wm size");
            Section(report, "thermal", adb, "shell dumpsys thermalservice");
            string pid = RunText(adb, "shell pidof " + package).Trim();
            report.AppendLine("pid " + pid);
            if (pid.Length > 0) Section(report, "cpu (top)", adb, "shell top -b -n 1 -p " + pid);
            // Qualcomm（Adreno）の GPU の使用率と周波数（読めない端末もある）
            Section(report, "gpu busy %", adb, "shell cat /sys/class/kgsl/kgsl-3d0/gpu_busy_percentage");
            Section(report, "gpu clock", adb, "shell cat /sys/class/kgsl/kgsl-3d0/gpuclk");
            Section(report, "gpu max clock", adb, "shell cat /sys/class/kgsl/kgsl-3d0/max_gpuclk");
            Section(report, "temperature", adb, "shell cat /sys/class/thermal/thermal_zone0/temp");
            // 画面の更新の間隔（アプリの描画面の、直近 127 フレームの表示時刻）。fps を出すのに使う
            string layers = RunText(adb, "shell dumpsys SurfaceFlinger --list");
            foreach (string line in layers.Split('\n'))
            {
                string layer = line.Trim();
                if (layer.Contains(package) && layer.Contains("SurfaceView") && !layer.StartsWith("Background"))
                {
                    // 名前に空白があるので、端末側のシェルへ単一引用符のまま渡す（二重引用符は adb に届く前に外れる）
                    Section(report, "latency " + layer, adb, "shell dumpsys SurfaceFlinger --latency '" + layer + "'");
                }
            }
            Section(report, "meminfo", adb, "shell dumpsys meminfo " + package);
            Section(report, "gfxinfo", adb, "shell dumpsys gfxinfo " + package);
            Section(report, "logcat app", adb, pid.Length > 0 ? "logcat -d -v time -t 600 --pid=" + pid : "logcat -d -v time -t 300");
            Section(report, "logcat errors", adb, "logcat -d -v time -t 200 *:E");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/BookAndroidDiagnostics.txt", report.ToString());
            // 端末の画面（BookPerfProbe の表示を読む）
            CaptureScreen(adb, "Logs/BookAndroidScreen.png");
            Debug.Log("[BookAndroidDiagnostics] Logs/BookAndroidDiagnostics.txt に書きました");
        }

        /// <summary>
        /// めくりの負荷を測る：端末の画面を右から左へ（次へ）スワイプし、直後のフレーム時間を SurfaceFlinger から読む。
        /// 1回目は閉じた本を開く。結果は Logs/BookAndroidTurnTest.txt、各回の画面は Logs/BookAndroidTurn_N.png
        /// </summary>
        [MenuItem("Tools/hatake/Book Android Turn Test")]
        public static void TurnTest()
        {
            string adb = FindAdb();
            WakeDevice(adb);
            string package = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
            string layer = FindLayer(adb, package);
            var report = new StringBuilder();
            report.AppendLine("layer " + layer);
            string size = RunText(adb, "shell wm size");
            int w = 1560, h = 720;
            var m = System.Text.RegularExpressions.Regex.Match(size, @"(\d+)x(\d+)");
            if (m.Success)
            {
                // 端末は横向きで使う：長い方を幅にする
                int a = int.Parse(m.Groups[1].Value), b = int.Parse(m.Groups[2].Value);
                w = Math.Max(a, b);
                h = Math.Min(a, b);
            }
            for (int i = 0; i < 4; i++)
            {
                RunText(adb, "shell dumpsys SurfaceFlinger --latency-clear");
                // 本の右側を右から左へ払う（めくる・開く）
                RunText(adb, "shell input swipe " + (int)(w * 0.85f) + " " + (h / 2) + " " + (int)(w * 0.35f) + " " + (h / 2) + " 250");
                System.Threading.Thread.Sleep(1800);
                string latency = RunText(adb, "shell dumpsys SurfaceFlinger --latency '" + layer + "'");
                report.AppendLine("turn " + i + " " + Summarize(latency));
                CaptureScreen(adb, "Logs/BookAndroidTurn_" + i + ".png");
                System.Threading.Thread.Sleep(600);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/BookAndroidTurnTest.txt", report.ToString());
            Debug.Log("[BookAndroidDiagnostics] Turn test\n" + report);
        }

        /// <summary>
        /// アプリを起動し直し、起動から数回めくるまでの logcat を全部残す（Logs/BookAndroidLaunch.txt）。
        /// GPU の異常（Adreno-GSL の警告など）がいつ始まるかを調べる
        /// </summary>
        [MenuItem("Tools/hatake/Book Android Launch Log")]
        public static void LaunchLog()
        {
            string adb = FindAdb();
            WakeDevice(adb);
            string package = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
            string size = RunText(adb, "shell wm size");
            int w = 1560, h = 720;
            var m = System.Text.RegularExpressions.Regex.Match(size, @"(\d+)x(\d+)");
            if (m.Success)
            {
                int a = int.Parse(m.Groups[1].Value), b = int.Parse(m.Groups[2].Value);
                w = Math.Max(a, b);
                h = Math.Min(a, b);
            }
            RunText(adb, "shell am force-stop " + package);
            RunText(adb, "logcat -c");
            RunText(adb, "shell mkdir -p /sdcard/Android/data/" + package + "/files");
            RunText(adb, "shell touch /sdcard/Android/data/" + package + "/files/fxtest");
            RunText(adb, "shell touch /sdcard/Android/data/" + package + "/files/bench");
            RunText(adb, "shell monkey -p " + package + " -c android.intent.category.LAUNCHER 1");
            var launchTimer = System.Diagnostics.Stopwatch.StartNew();
            System.Threading.Thread.Sleep(10000);
            CaptureScreen(adb, "Logs/BookAndroidLaunch_0.png");
            // 1回目（表紙を開く）はゆっくり払い、途中の画面を撮る（Logs/BookAndroidCoverMid.png）
            {
                var swipe = Process.Start(new ProcessStartInfo(adb, "shell input swipe " + (int)(w * 0.85f) + " " + (h / 2) + " " +
                                                               (int)(w * 0.3f) + " " + (h / 2) + " 2500")
                {
                    UseShellExecute = false, CreateNoWindow = true
                });
                System.Threading.Thread.Sleep(1200);
                CaptureScreen(adb, "Logs/BookAndroidCoverMid.png");
                swipe?.WaitForExit(10000);
                System.Threading.Thread.Sleep(2500);
            }
            for (int i = 0; i < 3; i++)
            {
                RunText(adb, "shell input swipe " + (int)(w * 0.85f) + " " + (h / 2) + " " + (int)(w * 0.35f) + " " + (h / 2) + " 250");
                System.Threading.Thread.Sleep(4000);
                CaptureScreen(adb, "Logs/BookAndroidLaunch_" + (i + 1) + ".png");
            }
            // 燃焼の演出（BookPerfProbe が fxtest を見て起動 24 秒後に燃やす）を待って撮る
            System.Threading.Thread.Sleep(Math.Max(0, 26000 - (int)launchTimer.ElapsedMilliseconds));
            CaptureScreen(adb, "Logs/BookAndroidEffect_0.png");
            System.Threading.Thread.Sleep(6000);
            // ゆっくりめくり、途中の画面を撮る（見た目の確認用：Logs/BookAndroidMidTurn_N.png）
            for (int i = 0; i < 2; i++)
            {
                var swipe = Process.Start(new ProcessStartInfo(adb, "shell input swipe " + (int)(w * 0.85f) + " " + (int)(h * 0.6f) + " " +
                                                               (int)(w * 0.3f) + " " + (int)(h * 0.45f) + " 2500")
                {
                    UseShellExecute = false, CreateNoWindow = true
                });
                System.Threading.Thread.Sleep(1300);
                CaptureScreen(adb, "Logs/BookAndroidMidTurn_" + i + ".png");
                swipe?.WaitForExit(10000);
                System.Threading.Thread.Sleep(2500);
            }
            RunText(adb, "shell rm /sdcard/Android/data/" + package + "/files/fxtest");
            RunText(adb, "shell rm /sdcard/Android/data/" + package + "/files/bench");
            string log = RunText(adb, "logcat -d -v time");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/BookAndroidLaunch.txt", log);
            Debug.Log("[BookAndroidDiagnostics] Launch log written (" + log.Length + " chars)");
        }

        /// <summary>
        /// 見た目の確認：計測を止めて起動し直し、表紙を開く・ページをめくる・表紙を閉じる途中の画面を撮る（Logs/BookAndroidVisual_*.png）
        /// </summary>
        [MenuItem("Tools/hatake/Book Android Visual Check")]
        public static void VisualCheck()
        {
            string adb = FindAdb();
            WakeDevice(adb);
            string package = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
            string size = RunText(adb, "shell wm size");
            int w = 1560, h = 720;
            var m = System.Text.RegularExpressions.Regex.Match(size, @"(\d+)x(\d+)");
            if (m.Success)
            {
                int a = int.Parse(m.Groups[1].Value), b = int.Parse(m.Groups[2].Value);
                w = Math.Max(a, b);
                h = Math.Min(a, b);
            }
            string flag = "/sdcard/Android/data/" + package + "/files/nobench";
            RunText(adb, "shell am force-stop " + package);
            RunText(adb, "shell mkdir -p /sdcard/Android/data/" + package + "/files");
            RunText(adb, "shell touch " + flag);
            string fx = "/sdcard/Android/data/" + package + "/files/fxtest";
            RunText(adb, "shell monkey -p " + package + " -c android.intent.category.LAUNCHER 1");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            System.Threading.Thread.Sleep(8000);
            int index = 0;
            // 右から左へ5回（表紙を開く・めくる…）、続けて左から右へ（戻る・表紙を閉じる）
            for (int i = 0; i < 8; i++)
            {
                bool forward = i < 4;
                int x0 = (int)(w * (forward ? 0.85f : 0.15f)), x1 = (int)(w * (forward ? 0.3f : 0.7f));
                var swipe = Process.Start(new ProcessStartInfo(adb, "shell input swipe " + x0 + " " + (int)(h * 0.6f) + " " + x1 + " " + (int)(h * 0.5f) + " 2000")
                {
                    UseShellExecute = false, CreateNoWindow = true
                });
                System.Threading.Thread.Sleep(1000);
                CaptureScreen(adb, "Logs/BookAndroidVisual_" + (index++) + ".png");
                swipe?.WaitForExit(10000);
                System.Threading.Thread.Sleep(2000);
                if (i == 2)
                {
                    // 燃やして途中を撮る（BookPerfProbe が fxtest = now を見て燃やす）
                    RunText(adb, "shell \"echo now > " + fx + "\"");
                    for (int k = 0; k < 4; k++)
                    {
                        System.Threading.Thread.Sleep(600);
                        CaptureScreen(adb, "Logs/BookAndroidVisualFx_" + k + ".png");
                    }
                    System.Threading.Thread.Sleep(3000);
                }
            }
            RunText(adb, "shell rm " + flag);
            RunText(adb, "shell rm " + fx);
            Debug.Log("[BookAndroidDiagnostics] Visual check done");
        }

        /// <summary>
        /// 場面ごとのフレームの時間：計測を止めて起動し直し、表紙を開く→めくる×3→燃やす→戻る×3→表紙を閉じる を順に行い、
        /// それぞれの直後のフレームの間隔を SurfaceFlinger から読む（Logs/BookAndroidScenario.txt）
        /// </summary>
        [MenuItem("Tools/hatake/Book Android Scenario")]
        public static void Scenario()
        {
            string adb = FindAdb();
            string package = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
            WakeDevice(adb);
            string size = RunText(adb, "shell wm size");
            int w = 1560, h = 720;
            var m = System.Text.RegularExpressions.Regex.Match(size, @"(\d+)x(\d+)");
            if (m.Success)
            {
                int a = int.Parse(m.Groups[1].Value), b = int.Parse(m.Groups[2].Value);
                w = Math.Max(a, b);
                h = Math.Min(a, b);
            }
            string files = "/sdcard/Android/data/" + package + "/files";
            RunText(adb, "shell am force-stop " + package);
            RunText(adb, "shell mkdir -p " + files);
            RunText(adb, "shell touch " + files + "/nobench");
            RunText(adb, "shell monkey -p " + package + " -c android.intent.category.LAUNCHER 1");
            System.Threading.Thread.Sleep(9000);
            string layer = FindLayer(adb, package);
            var report = new StringBuilder();
            report.AppendLine("layer " + layer);
            string[] steps = { "open cover", "turn 1", "turn 2", "turn 3", "burn", "back 1", "back 2", "back 3", "close cover" };
            foreach (string step in steps)
            {
                WakeDevice(adb);
                RunText(adb, "shell dumpsys SurfaceFlinger --latency-clear");
                if (step == "burn") RunText(adb, "shell \"echo now > " + files + "/fxtest\"");
                else
                {
                    bool forward = !step.StartsWith("back") && step != "close cover";
                    int x0 = (int)(w * (forward ? 0.85f : 0.15f)), x1 = (int)(w * (forward ? 0.3f : 0.75f));
                    RunText(adb, "shell input swipe " + x0 + " " + (h / 2) + " " + x1 + " " + (h / 2) + " 250");
                }
                System.Threading.Thread.Sleep(2200);
                string latency = RunText(adb, "shell dumpsys SurfaceFlinger --latency '" + layer + "'");
                report.AppendLine(step.PadRight(12) + " " + Summarize(latency));
                System.Threading.Thread.Sleep(500);
            }
            RunText(adb, "shell rm " + files + "/nobench");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/BookAndroidScenario.txt", report.ToString());
            Debug.Log("[BookAndroidDiagnostics] Scenario\n" + report);
        }

        // 画面が消えている・ロックされているとアプリが止まるので起こす（ロックの解除は暗証番号の無い端末だけ）
        internal static void WakeDevice(string adb)
        {
            RunText(adb, "shell input keyevent KEYCODE_WAKEUP");
            RunText(adb, "shell wm dismiss-keyguard");
        }

        private static string FindLayer(string adb, string package)
        {
            foreach (string line in RunText(adb, "shell dumpsys SurfaceFlinger --list").Split('\n'))
            {
                string layer = line.Trim();
                if (layer.Contains(package) && layer.Contains("SurfaceView") && !layer.StartsWith("Background")) return layer;
            }
            return "";
        }

        // SurfaceFlinger --latency の出力から、fps と最も長いフレームの時間を出す
        private static string Summarize(string latency)
        {
            var times = new System.Collections.Generic.List<long>();
            foreach (string line in latency.Split('\n'))
            {
                string[] parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3 && long.TryParse(parts[1], out long t) && t > 0 && t < long.MaxValue / 2) times.Add(t);
            }
            if (times.Count < 3) return "frames " + times.Count;
            double span = (times[times.Count - 1] - times[0]) / 1e9;
            double worst = 0;
            var gaps = new System.Collections.Generic.List<double>();
            for (int i = 1; i < times.Count; i++)
            {
                double d = (times[i] - times[i - 1]) / 1e6;
                gaps.Add(d);
                worst = Math.Max(worst, d);
            }
            gaps.Sort();
            return string.Format("frames {0} fps {1:0.0} median {2:0.0}ms p90 {3:0.0}ms worst {4:0.0}ms",
                times.Count, (times.Count - 1) / span, gaps[gaps.Count / 2], gaps[(int)(gaps.Count * 0.9)], worst);
        }

        // Unity が使っている Android SDK の adb（無ければ PATH の adb）
        internal static string FindAdb()
        {
            string sdk = AndroidExternalToolsSettings.sdkRootPath;
            if (!string.IsNullOrEmpty(sdk))
            {
                string path = Path.Combine(sdk, "platform-tools", "adb.exe");
                if (File.Exists(path)) return path;
            }
            return "adb";
        }

        internal static string RunText(string adb, string arguments)
        {
            var sb = new StringBuilder();
            Section(sb, "", adb, arguments);
            string text = sb.ToString();
            int start = text.IndexOf('\n', text.IndexOf("=====", StringComparison.Ordinal) + 1);
            return start >= 0 ? text.Substring(start + 1) : text;
        }

        private static void CaptureScreen(string adb, string path)
        {
            try
            {
                var info = new ProcessStartInfo(adb, "exec-out screencap -p")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                using (Process process = Process.Start(info))
                using (var file = File.Create(path))
                {
                    process.StandardOutput.BaseStream.CopyTo(file);
                    process.WaitForExit(20000);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BookAndroidDiagnostics] screencap failed: " + e.Message);
            }
        }

        private static void Section(StringBuilder report, string title, string adb, string arguments)
        {
            report.AppendLine();
            report.AppendLine("===== " + title + " (adb " + arguments + ")");
            try
            {
                var info = new ProcessStartInfo(adb, arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };
                using (Process process = Process.Start(info))
                {
                    // 出力は非同期に読み、詰まって止まらないようにする
                    var output = new StringBuilder();
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (output) output.AppendLine("ERR " + e.Data); };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    if (!process.WaitForExit(20000))
                    {
                        process.Kill();
                        output.AppendLine("(timeout)");
                    }
                    process.WaitForExit();
                    lock (output) report.Append(output);
                }
            }
            catch (Exception e)
            {
                report.AppendLine("failed: " + e.Message);
            }
        }
    }
}
