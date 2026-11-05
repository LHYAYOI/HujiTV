using System.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace hatake
{
    /// <summary>
    /// 実機（Android など）での計測用。画面の左下に CPU / GPU の時間・メモリ・本の状態を出し、
    /// 一定の間隔でログ（Android では logcat、タグ Unity）へ「[BookPerf]」で書く。
    /// GPU の時間は Player Settings の「Frame Timing Stats」を ON にすると取れる（取れない端末では n/a）。
    /// 開始時に1度、端末とグラフィックスの情報・本のシェーダーが動くかを書く。
    /// </summary>
    // 他のスクリプトより先に Update を始め、フレームの終わりまでの時間（メインスレッドの処理）を測る
    [DefaultExecutionOrder(-10000)]
    public sealed class BookPerfProbe : MonoBehaviour
    {
        [SerializeField] private BookFlipView m_flipView;
        [Tooltip("ログと表示を更新する間隔（秒）")]
        [SerializeField, Min(0.25f)] private float m_interval = 1f;
        [SerializeField] private bool m_showOnScreen = true;
        [Tooltip("静止中・本文のめくり・表紙の開け閉め・燃焼の演出で1回ずつ、GPU の処理ごとの時間を測ってログ（[BookBench]）に書く。" +
                 "測っている間（数秒）は画面が止まる。端末の persistentDataPath に bench を置いても ON になる")]
        [SerializeField] private bool m_benchmark;
        [SerializeField] private BookController m_controller;
        private bool m_benchIdleDone;
        private bool m_benchTurnDone;
        private bool m_benchCoverTurnDone;
        private bool m_benchEffectDone;
        private float m_fxTestAt = -1f;
        private float m_benchStart;

        private readonly FrameTiming[] m_timings = new FrameTiming[1];
        private float m_cpuSum;
        private float m_gpuSum;
        private float m_cpuWorst;
        private int m_frames;
        private int m_gpuFrames;
        private float m_next;
        private string m_text = "";
        private int m_lastRenderCount;
        // メインスレッドの処理の時間（Update の始め〜フレームの終わり）。フレームの時間より大幅に短ければ GPU 待ち
        private double m_frameStart;
        private float m_workSum;
        private float m_workWorst;
        private int m_workFrames;
        private readonly WaitForEndOfFrame m_endOfFrame = new WaitForEndOfFrame();

        private void OnEnable()
        {
            StartCoroutine(MeasureWork());
        }

        private string m_prevNote = "";
        private float m_lastGpu;
        private bool m_logTurns; // 端末に turnlog を置くと、めくり中の全フレームを [BookSpike] に書く
        private float m_prevWork;

        private IEnumerator MeasureWork()
        {
            while (true)
            {
                yield return m_endOfFrame;
                bool benched = false;
                if (m_compare && m_flipView != null && Time.unscaledTime > 3f) RunCompareIfDue();
                if (m_benchmark && m_flipView != null)
                {
                    int before = (m_benchIdleDone ? 1 : 0) + (m_benchTurnDone ? 1 : 0) + (m_benchCoverTurnDone ? 1 : 0) + (m_benchEffectDone ? 1 : 0);
                    RunBenchmarkIfDue();
                    benched = before != (m_benchIdleDone ? 1 : 0) + (m_benchTurnDone ? 1 : 0) + (m_benchCoverTurnDone ? 1 : 0) + (m_benchEffectDone ? 1 : 0);
                }
                if (m_frameStart <= 0) continue;
                float work = (float)((Time.realtimeSinceStartupAsDouble - m_frameStart) * 1000.0);
                // 遅いフレームを、そのフレームで行った重い処理の名前と一緒に書く（計測したフレームは除く）
                if (!benched && m_flipView != null)
                {
                    float frame = Time.unscaledDeltaTime * 1000f;
                    // 目標のフレーム間隔（targetFrameRate）より長くかかったフレーム
                    float budget = Application.targetFrameRate > 0 ? 1000f / Application.targetFrameRate : 33.4f;
                    // 前のフレームの処理（GPU の描画）が長いと、このフレームの間隔が延びる。前のフレームの内容も一緒に書く
                    if ((frame > budget * 1.25f || m_flipView.IsPlayingEffect || (m_logTurns && m_flipView.IsTurning)) && Time.unscaledTime > 4f)
                        Debug.Log("[BookSpike] main=" + work.ToString("0.0") + "ms frame=" + frame.ToString("0.0") + "ms" +
                                  (m_flipView.IsTurning ? " turning" : "") + (m_flipView.IsPlayingEffect ? " effect" : "") + " note=" + m_flipView.FrameNote +
                                  " gpu=" + m_lastGpu.ToString("0.0") + " | prev main=" + m_prevWork.ToString("0.0") + " note=" + m_prevNote);
                    m_prevNote = m_flipView.FrameNote;
                    m_prevWork = work;
                    m_flipView.FrameNote = "";
                }
                m_workSum += work;
                m_workWorst = Mathf.Max(m_workWorst, work);
                m_workFrames++;
            }
        }

        private void Start()
        {
            // 端末・グラフィックスの情報と、本のシェーダーがこの端末で動くか
            Shader composite = Shader.Find("Book/Page Flip Composite");
            Shader post = Shader.Find("Hidden/Book/Page Flip Post");
            Debug.Log("[BookPerf] device=" + SystemInfo.deviceModel +
                      " gpu=" + SystemInfo.graphicsDeviceName +
                      " api=" + SystemInfo.graphicsDeviceType + " " + SystemInfo.graphicsDeviceVersion +
                      " vram=" + SystemInfo.graphicsMemorySize + "MB ram=" + SystemInfo.systemMemorySize + "MB" +
                      " screen=" + Screen.width + "x" + Screen.height +
                      " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] +
                      " ARGBHalf=" + SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) +
                      " composite=" + (composite == null ? "missing" : composite.isSupported ? "ok" : "NOT SUPPORTED") +
                      " post=" + (post == null ? "missing" : post.isSupported ? "ok" : "NOT SUPPORTED"));
            m_next = Time.unscaledTime + m_interval;
            m_benchStart = Time.unscaledTime + 3f;
            if (m_controller == null) m_controller = FindAnyObjectByType<BookController>();
            if (m_flipView != null) m_flipView.NotesEnabled = true;
            // 見た目の確認で撮影するときは測らない（測っている間は画面が止まる）。端末に nobench を置くと止まる
            if (System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "bench"))) m_benchmark = true;
            BookFlipView.DebugGpuSync = System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "gpusync"));
            m_logTurns = System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "turnlog"));
            m_compare = System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "compare"));
            // 計測用：画面の文字・パネル（IMGUI）を出さない
            if (System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "noui")))
            {
                m_showOnScreen = false;
                BookSampleDemo.HideGui = true;
            }
            if (System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "nobench"))) m_benchmark = false;
            // 見た目の確認用：端末に leather を置くと、表紙の絵を外して革の表紙で表示する
            if (System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "leather")) && m_flipView != null)
                m_flipView.SetCovers(null, null);
            // 演出の負荷の計測用：端末に fxtest を置くと、起動から 24 秒後に見開きを燃やす
            // （ファイルに秒数が書いてあればその秒数後）
            string fxPath = System.IO.Path.Combine(Application.persistentDataPath, "fxtest");
            m_fxTestAt = -1f;
            if (System.IO.File.Exists(fxPath))
            {
                float delay = 24f;
                try { float.TryParse(System.IO.File.ReadAllText(fxPath).Trim(), out delay); } catch (System.Exception) { }
                m_fxTestAt = Time.unscaledTime + (delay > 0f ? delay : 24f);
            }
        }

        private float m_fxPollAt;

        private void Update()
        {
            // 起動後に fxtest（中身 now）が置かれたら、すぐ燃やす（見た目の確認で途中の画面を撮るため）
            if (m_fxTestAt < 0f && Time.unscaledTime >= m_fxPollAt)
            {
                m_fxPollAt = Time.unscaledTime + 0.5f;
                string fxPath = System.IO.Path.Combine(Application.persistentDataPath, "fxtest");
                try
                {
                    if (System.IO.File.Exists(fxPath) && System.IO.File.ReadAllText(fxPath).Trim() == "now")
                    {
                        System.IO.File.Delete(fxPath);
                        m_fxTestAt = Time.unscaledTime;
                    }
                }
                catch (System.Exception) { }
            }
            if (m_fxTestAt > 0f && Time.unscaledTime >= m_fxTestAt && m_controller != null)
            {
                // めくりの途中などで燃やせないときは、燃やせるまで（10 秒まで）待つ
                bool burned = m_controller.BurnSpread();
                if (burned || Time.unscaledTime > m_fxTestAt + 10f)
                {
                    m_fxTestAt = -1f;
                    Debug.Log("[BookPerf] fxtest burn=" + burned);
                }
            }
            m_frameStart = Time.realtimeSinceStartupAsDouble;
            // 1フレームの CPU / GPU の時間（取れなければ CPU はフレームの間隔）
            FrameTimingManager.CaptureFrameTimings();
            float cpu = Time.unscaledDeltaTime * 1000f;
            if (FrameTimingManager.GetLatestTimings(1, m_timings) > 0)
            {
                if (m_timings[0].cpuFrameTime > 0)
                {
                    cpu = (float)m_timings[0].cpuFrameTime;
                }
                m_lastGpu = (float)m_timings[0].gpuFrameTime;
                if (m_timings[0].gpuFrameTime > 0)
                {
                    m_gpuSum += (float)m_timings[0].gpuFrameTime;
                    m_gpuFrames++;
                }
            }
            m_cpuSum += cpu;
            m_cpuWorst = Mathf.Max(m_cpuWorst, cpu);
            m_frames++;
            if (Time.unscaledTime < m_next)
            {
                return;
            }
            Report();
            m_next = Time.unscaledTime + m_interval;
        }

        private void Report()
        {
            float cpu = m_cpuSum / Mathf.Max(1, m_frames);
            string gpu = m_gpuFrames > 0 ? (m_gpuSum / m_gpuFrames).ToString("0.0") + "ms" : "n/a";
            string output = "-";
            string state = "-";
            if (m_flipView != null)
            {
                RenderTexture rt = m_flipView.OutputTexture;
                output = rt != null ? rt.width + "x" + rt.height + " " + rt.format : "none";
                // 合成を描き直した回数（静止中は 0 になるのが正常）
                int renders = m_flipView.RenderCount - m_lastRenderCount;
                m_lastRenderCount = m_flipView.RenderCount;
                state = (m_flipView.IsTurning ? "turning" : "idle") + " cover=" + m_flipView.CoverState +
                        " dim=" + m_flipView.Dim.ToString("0.00") +
                        " redraw=" + (renders / Mathf.Max(m_interval, 0.01f)).ToString("0.0") + "/s" +
                        (m_flipView.UsesMobileSettings ? " mobile" : "");
            }
            m_text = "[BookPerf] fps=" + (1000f / Mathf.Max(cpu, 0.01f)).ToString("0") +
                     " cpu=" + cpu.ToString("0.0") + "ms (worst " + m_cpuWorst.ToString("0.0") + ")" +
                     " gpu=" + gpu +
                     " main=" + (m_workSum / Mathf.Max(1, m_workFrames)).ToString("0.0") + "ms (worst " + m_workWorst.ToString("0.0") + ")" +
                     " output=" + output + " " + state +
                     " mono=" + (Profiler.GetMonoUsedSizeLong() >> 20) + "MB" +
                     " gfx=" + (Profiler.GetAllocatedMemoryForGraphicsDriver() >> 20) + "MB" +
                     " total=" + (Profiler.GetTotalAllocatedMemoryLong() >> 20) + "MB";
            Debug.Log(m_text);
            m_workSum = 0f;
            m_workWorst = 0f;
            m_workFrames = 0;
            m_cpuSum = 0f;
            m_gpuSum = 0f;
            m_cpuWorst = 0f;
            m_frames = 0;
            m_gpuFrames = 0;
        }

        // ------------------------------------------------------------------
        // ベンチマーク：処理ごとに n 回描き、GPU の完了まで待った時間を 1 回あたりで出す
        // （GPU の完了は 1 画素の読み戻しで待つ）。めくり中の重さがどこにあるかを切り分ける
        // ------------------------------------------------------------------
        // ------------------------------------------------------------------
        // 見た目の比較：端末に compare を置くと、表紙の開け閉め・本文のめくり・燃焼の最初のフレームで、
        // 角の位置（進み具合）を変えながらモバイルの描き方と PC の1パスで描いて、左右に並べた PNG を保存する
        // （persistentDataPath/compare_*.png）
        // ------------------------------------------------------------------
        private bool m_compare;
        private bool m_compareCoverDone, m_comparePageDone, m_compareFxDone;
        private readonly System.Collections.Generic.HashSet<string> m_compareIdleKinds = new System.Collections.Generic.HashSet<string>();

        private void RunCompareIfDue()
        {
            bool turning = m_flipView.IsTurning;
            bool effect = m_flipView.IsPlayingEffect;
            bool coverTurn = turning && m_flipView.CoverState != BookCoverState.Open;
            string kind;
            if (effect) { if (m_compareFxDone) return; m_compareFxDone = true; kind = "fx"; }
            else if (coverTurn) { if (m_compareCoverDone) return; m_compareCoverDone = true; kind = "cover"; }
            else if (turning) { if (m_comparePageDone) return; m_comparePageDone = true; kind = "page"; }
            else if (Time.unscaledTime > 8f && !m_flipView.IsBusyRendering && m_compareIdleKinds.Add("idle" + m_flipView.CoverState)) kind = "idle" + m_flipView.CoverState;
            else return;
            RenderTexture output = m_flipView.OutputTexture;
            if (output == null) return;
            int w = output.width, h = output.height;
            RenderTextureFormat hdr = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            float[] steps = { 0.05f, 0.2f, 0.35f, 0.5f, 0.65f, 0.8f, 0.95f };
            var sheet = new Texture2D(w * 2, h * steps.Length, TextureFormat.RGBA32, false);
            for (int i = 0; i < steps.Length; i++)
            {
                float t = steps[i];
                var mobile = RenderTexture.GetTemporary(w, h, 0, hdr);
                var full = RenderTexture.GetTemporary(w, h, 0, hdr);
                m_flipView.CompareRender(mobile, full, m =>
                {
                    if (kind == "fx") { m.SetFloat("_FxProgress", t); return; }
                    if (kind.StartsWith("idle"))
                    {
                        // 静止した見開き：暗転・スポット・魔法の縁取りの組み合わせ
                        int row = System.Array.IndexOf(steps, t);
                        if (row == 1 || row == 2 || row == 4) m.SetFloat("_Dim", 0.6f);
                        if (row == 6) m.SetFloat("_Dim", 1f);
                        if (row == 3 || row == 4 || row == 5) m.SetFloat("_MagicStrength", row == 5 ? 0.5f : 1f);
                        if (row == 2 || row == 6)
                        {
                            Vector4[] spots = m.GetVectorArray("_Spots");
                            Vector4[] spotParams = m.GetVectorArray("_SpotParams");
                            if (spots != null && spotParams != null && spots.Length > 0)
                            {
                                float aspect = m.GetFloat("_PageAspect");
                                spots[0] = new Vector4(0.5f, aspect * 0.5f, 0.3f, aspect * 0.2f);
                                spotParams[0] = new Vector4(0f, 0.05f, 0.05f, 1f);
                                m.SetVectorArray("_Spots", spots);
                                m.SetVectorArray("_SpotParams", spotParams);
                                m.SetFloat("_SpotCount", 1f);
                            }
                        }
                        return;
                    }
                    if (m.GetFloat("_Turning") < 0.5f) return;
                    // 角の位置（めくり空間の x：1 → -1）と、表紙の開き具合
                    Vector4 f = m.GetVector("_Fold");
                    float x = Mathf.Lerp(1f, -1f, t);
                    m.SetVector("_Fold", new Vector4(f.x, f.y, x, f.y + (kind == "page" ? Mathf.Sin(t * Mathf.PI) * 0.15f : 0f)));
                    if (kind == "cover")
                    {
                        bool opening = m_flipView.CoverState != BookCoverState.Open;
                        m.SetFloat("_BoardOpen", opening ? t : 1f - t);
                    }
                    m.SetFloat("_TurnGlow", m.GetFloat("_TurnGlow") > 0f ? m.GetFloat("_TurnGlow") : 0f);
                });
                CopyInto(sheet, mobile, 0, (steps.Length - 1 - i) * h);
                CopyInto(sheet, full, w, (steps.Length - 1 - i) * h);
                RenderTexture.ReleaseTemporary(mobile);
                RenderTexture.ReleaseTemporary(full);
            }
            sheet.Apply();
            string path = System.IO.Path.Combine(Application.persistentDataPath, "compare_" + kind + ".png");
            System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
            Destroy(sheet);
            Debug.Log("[BookCompare] saved " + path + "（左：モバイル、右：PC の1パス）");
        }

        private static void CopyInto(Texture2D sheet, RenderTexture source, int x, int y)
        {
            var ldr = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, ldr);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = ldr;
            sheet.ReadPixels(new Rect(0, 0, source.width, source.height), x, y, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(ldr);
        }

        private void RunBenchmarkIfDue()
        {
            if (Time.unscaledTime < m_benchStart) return;
            bool turning = m_flipView.IsTurning;
            bool effect = m_flipView.IsPlayingEffect;
            // 表紙をめくっている間（閉じた状態から開く・閉じる）と、本文のめくり・燃焼などの演出を別に測る
            bool coverTurn = turning && m_flipView.CoverState != BookCoverState.Open;
            if (effect ? m_benchEffectDone : coverTurn ? m_benchCoverTurnDone : turning ? m_benchTurnDone : m_benchIdleDone) return;
            RenderTexture output = m_flipView.OutputTexture;
            if (output == null || !output.IsCreated()) return;
            if (effect) m_benchEffectDone = true;
            else if (coverTurn) m_benchCoverTurnDone = true; else if (turning) m_benchTurnDone = true; else m_benchIdleDone = true;

            // GPU のクロックを上げてから測る（軽い処理が続いた後はクロックが下がっていて遅く出る）
            {
                RenderTexture warm = RenderTexture.GetTemporary(output.width, output.height, 0, RenderTextureFormat.ARGB32);
                var warmTimer = Stopwatch.StartNew();
                while (warmTimer.ElapsedMilliseconds < 400) { m_flipView.BenchComposite(warm); WaitGpu(); }
                RenderTexture.ReleaseTemporary(warm);
            }
            var sb = new System.Text.StringBuilder("[BookBench] " + (effect ? "effect" : coverTurn ? "coverTurn" : turning ? "pageTurn" : "idle") +
                                                   " cover=" + m_flipView.CoverState);
            RenderTexture sync = RenderTexture.GetTemporary(4, 4, 0, RenderTextureFormat.ARGB32);
            const int n = 2;
            sb.Append(" sync=").Append(Measure(sync, n, () => Graphics.Blit(Texture2D.whiteTexture, sync)).ToString("0.0"));

            RenderTextureFormat hdr = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
                ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            foreach (float scale in new[] { 1f, 0.5f, 0.25f })
            {
                int w = Mathf.Max(8, Mathf.RoundToInt(output.width * scale));
                int h = Mathf.Max(8, Mathf.RoundToInt(output.height * scale));
                RenderTexture target = RenderTexture.GetTemporary(w, h, 0, hdr);
                sb.Append(" composite" + w + "x" + h + "=").Append(Measure(target, n, () => m_flipView.BenchComposite(target)).ToString("0.0"));
                RenderTexture.ReleaseTemporary(target);
            }

            // プロパティを変えた合成（どの処理が重いか）
            {
                RenderTexture target = RenderTexture.GetTemporary(output.width, output.height, 0, hdr);
                void Try(string label, System.Action<Material> setup)
                {
                    m_flipView.BenchOverride = setup;
                    sb.Append(" " + label + "=").Append(Measure(target, n, () => m_flipView.BenchComposite(target)).ToString("0.0"));
                    m_flipView.BenchOverride = null;
                }
                Try("noGlow", m => m.SetFloat("_TurnGlow", 0f));
                Try("noShadow", m => { m.SetFloat("_DropShadow", 0f); m.SetFloat("_FoldShadow", 0f); });
                Try("noGlowShadow", m => { m.SetFloat("_TurnGlow", 0f); m.SetFloat("_DropShadow", 0f); });
                Try("lod0", m => { m.SetFloat("_PageLod", 0f); m.SetFloat("_CoverLod", 0f); });
                if (turning)
                {
                    // めくりの途中・終わり近く（角の位置を変えて描く範囲を広げる）
                    Try("foldMid", m => { Vector4 f = m.GetVector("_Fold"); m.SetVector("_Fold", new Vector4(f.x, f.y, 0f, f.y + 0.15f)); });
                    Try("foldLate", m => { Vector4 f = m.GetVector("_Fold"); m.SetVector("_Fold", new Vector4(f.x, f.y, -0.8f, f.y + 0.05f)); });
                }
                RenderTexture.ReleaseTemporary(target);
            }

            // 分割パスごと（モバイルの描き方）
            {
                RenderTexture baseTexture = RenderTexture.GetTemporary(output.width, output.height, 0, hdr);
                RenderTexture finish = RenderTexture.GetTemporary(output.width, output.height, 0, hdr);
                string[] names = { "pOutside", "pPage", "pCover", "pFinish" };
                for (int pass = 1; pass <= 4; pass++)
                {
                    int captured = pass;
                    sb.Append(" " + names[pass - 1] + "=").Append(Measure(finish, n, () => m_flipView.BenchSplitPass(baseTexture, finish, captured)).ToString("0.0"));
                }
                RenderTexture.ReleaseTemporary(baseTexture);
                RenderTexture.ReleaseTemporary(finish);
            }

            if (turning || effect)
            {
                // 実際の1フレーム（動いている間の解像度の合成 + 後処理）
                sb.Append(" FRAME=").Append(Measure(output, n, () => m_flipView.BenchAnimFrame()).ToString("0.0"));
                if (turning)
                {
                    void Frame(string label, System.Action<Material> setup)
                    {
                        m_flipView.BenchOverride = setup;
                        sb.Append(" " + label + "=").Append(Measure(output, n, () => m_flipView.BenchAnimFrame()).ToString("0.0"));
                        m_flipView.BenchOverride = null;
                    }
                    Frame("FRAMEearly97", m => { Vector4 f = m.GetVector("_Fold"); m.SetVector("_Fold", new Vector4(f.x, f.y, 0.97f, f.y)); });
                    Frame("FRAMEearly85", m => { Vector4 f = m.GetVector("_Fold"); m.SetVector("_Fold", new Vector4(f.x, f.y, 0.85f, f.y + 0.05f)); });
                    Frame("FRAMEmid", m => { Vector4 f = m.GetVector("_Fold"); m.SetVector("_Fold", new Vector4(f.x, f.y, 0f, f.y + 0.15f)); });
                    Frame("FRAMElate", m => { Vector4 f = m.GetVector("_Fold"); m.SetVector("_Fold", new Vector4(f.x, f.y, -0.8f, f.y + 0.05f)); });
                }
            }
            sb.Append(" post=").Append(Measure(output, n, () => m_flipView.BenchPost()).ToString("0.0"));
            sb.Append(" postNoBloom=").Append(Measure(output, n, () => m_flipView.BenchPostNoBloom()).ToString("0.0"));
            if (!turning) sb.Append(" strip=").Append(Measure(output, n, () => m_flipView.BenchProgressStrip()).ToString("0.0"));
            sb.Append(" copy=").Append(Measure(output, n, () => m_flipView.BenchCopy()).ToString("0.0"));

            if (m_controller != null)
            {
                RenderTexture page = m_controller.BenchCurrentTexture;
                Camera live = m_controller.BenchLiveCamera;
                Camera capture = m_controller.BenchCaptureCamera;
                if (page != null)
                {
                    sb.Append(" pageRT=" + page.width + "x" + page.height + " " + page.format + " mips=" + page.useMipMap +
                              " depth=" + page.depthStencilFormat + " msaa=" + page.antiAliasing);
                    Camera cam = live != null ? live : capture;
                    if (cam != null)
                    {
                        RenderTexture before = cam.targetTexture;
                        bool enabled = cam.enabled;
                        cam.targetTexture = page;
                        sb.Append(live != null ? " liveCam=" : " captureCam=").Append(Measure(page, n, () => cam.Render()).ToString("0.0"));
                        cam.targetTexture = before;
                        cam.enabled = enabled;
                    }
                    if (page.useMipMap)
                    {
                        RenderTextureDescriptor d = page.descriptor;
                        d.autoGenerateMips = false;
                        RenderTexture mip = RenderTexture.GetTemporary(d);
                        Graphics.Blit(page, mip);
                        sb.Append(" mips=").Append(Measure(mip, n, () => mip.GenerateMips()).ToString("0.0"));
                        RenderTexture.ReleaseTemporary(mip);
                    }
                }
            }
            RenderTexture.ReleaseTemporary(sync);
            Debug.Log(sb.ToString() + " (ms/回)");
        }

        private static RenderTexture s_sync;

        private static float Measure(RenderTexture unused, int n, System.Action action)
        {
            // 1回目はシェーダーの準備（パイプラインの作成）が入るので測らない
            action();
            WaitGpu();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < n; i++) action();
            WaitGpu();
            return (float)sw.Elapsed.TotalMilliseconds / n;
        }

        // 読み戻しはコマンドの順に処理されるので、小さな RT を読み戻せば、それまでに積んだ描画の完了を待てる
        private static void WaitGpu()
        {
            if (s_sync == null)
            {
                s_sync = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.HideAndDontSave };
                s_sync.Create();
            }
            Graphics.Blit(Texture2D.blackTexture, s_sync);
            if (!SystemInfo.supportsAsyncGPUReadback) { GL.Flush(); return; }
            AsyncGPUReadback.Request(s_sync, 0, 0, 1, 0, 1, 0, 1, TextureFormat.RGBA32).WaitForCompletion();
        }

        private void OnGUI()
        {
            if (!m_showOnScreen || m_text.Length == 0)
            {
                return;
            }
            GUI.Label(new Rect(12f, Screen.height - 60f, Screen.width - 24f, 50f), m_text);
        }
    }
}
