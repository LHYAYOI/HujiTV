using System.Collections;
using System.Reflection;
using UnityEngine;

namespace hatake
{
    /// <summary>
    /// 実機確認用：pagefx の印（端末は persistentDataPath/pagefx、エディターは Logs/pagefx）があるとき、
    /// シーン読み込み後に自動で付き、暗転・めくりの禁止・獲得（発光）・めくり・まとめてめくりを順に再生する。
    /// 場面ごとにフレーム時間（平均・最悪）を [PageFx] のログに書き、要所で画面を pagefx_*.png に保存する。
    /// どのコントローラー（m_leftPageView / m_flipView を持つもの）でも動く。印が無ければ何もしない。
    /// </summary>
    public sealed class BookPageFxDemo : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            string flag = Application.isEditor ? "Logs/pagefx" : System.IO.Path.Combine(Application.persistentDataPath, "pagefx");
            if (!System.IO.File.Exists(flag)) return;
            // fps60 の印もあれば、フレームレートの上限を 60 にして測る（シーンの設定は変えない）
            if (System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "fps60"))) Application.targetFrameRate = 60;
            new GameObject("BookPageFxDemo").AddComponent<BookPageFxDemo>();
        }

        private MonoBehaviour m_controller;
        private BookPageView m_left, m_right;
        private BookFlipView m_flipView;

        // 場面ごとのフレーム時間
        private string m_phase;
        private float m_sum, m_worst;
        private int m_frames, m_slow;

        // 切り分け用の印：noalt（撮影の交互化を止める）、nocap（めくり中はページの撮影を止める）、nobloom（Bloom を切る）
        private bool m_noCapture;
        private readonly System.Collections.Generic.List<Camera> m_stopped = new System.Collections.Generic.List<Camera>();
        private static bool Flag(string name) => System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, name));

        private void LateUpdate()
        {
            if (!m_noCapture || m_flipView == null) return;
            if (m_flipView.IsTurning && m_flipView.TurnAmount > 0.02f)
            {
                foreach (BookPageView view in BookPageView.ActiveViews)
                {
                    Camera c = view.CaptureCamera;
                    if (c != null && c.enabled && c.targetTexture != null) { c.enabled = false; m_stopped.Add(c); }
                }
            }
            else if (m_stopped.Count > 0)
            {
                foreach (Camera c in m_stopped) if (c != null && c.targetTexture != null) c.enabled = true;
                m_stopped.Clear();
            }
        }

        private void Update()
        {
            if (m_phase == null) return;
            float ms = Time.unscaledDeltaTime * 1000f;
            m_sum += ms; m_frames++;
            m_worst = Mathf.Max(m_worst, ms);
            if (ms > 25f) m_slow++;
        }

        private void Phase(string name)
        {
            if (m_phase != null && m_frames > 0)
                Debug.Log($"[PageFx] PHASE {m_phase}: avg={m_sum / m_frames:F1}ms worst={m_worst:F1}ms frames={m_frames} slow(>25ms)={m_slow}");
            m_phase = name; m_sum = 0f; m_worst = 0f; m_frames = 0; m_slow = 0;
        }

        private IEnumerator Start()
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            for (int i = 0; i < 100 && m_controller == null; i++)
            {
                foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    System.Type t = mb.GetType();
                    FieldInfo l = t.GetField("m_leftPageView", F), r = t.GetField("m_rightPageView", F), f = t.GetField("m_flipView", F);
                    if (l == null || r == null || f == null || t == typeof(BookPageFxDemo)) continue;
                    m_left = l.GetValue(mb) as BookPageView;
                    m_right = r.GetValue(mb) as BookPageView;
                    m_flipView = f.GetValue(mb) as BookFlipView;
                    if (m_left != null && m_right != null && m_flipView != null) { m_controller = mb; break; }
                }
                if (m_controller == null) yield return new WaitForSeconds(0.2f);
            }
            if (m_controller == null) { Debug.Log("[PageFx] controller not found"); yield break; }
            Debug.Log($"[PageFx] controller {m_controller.GetType().Name}");
            m_noCapture = Flag("nocap");
            BookPageView.DebugNoAlternate = Flag("noalt");
            if (Flag("nobloom"))
                typeof(BookFlipView).GetField("m_bloomIntensity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(m_flipView, 0f);
            Debug.Log($"[PageFx] flags nocap={m_noCapture} noalt={BookPageView.DebugNoAlternate} nobloom={Flag("nobloom")}");

            // 本が開いて落ち着くまで待つ（閉じていれば開く）
            yield return new WaitForSeconds(6f);
            yield return WaitIdle();
            if (m_flipView.CoverState != BookCoverState.Open)
            {
                m_controller.SendMessage("ShowSpread", 1);
                yield return new WaitForSeconds(3f);
                yield return WaitIdle();
            }
            int count = Count();
            if (Spread() != 1 && count > 2) { m_controller.SendMessage("ShowSpread", 1); yield return new WaitForSeconds(2.5f); yield return WaitIdle(); }
            int start = Spread();
            BookPageData left = m_left.PageData, right = m_right.PageData;
            Log($"start pages left={(left != null ? left.name : "-")} right={(right != null ? right.name : "-")} spreads={count} cover={m_flipView.CoverState}");

            Phase("idle");
            yield return new WaitForSeconds(3f);
            yield return Shot("0_start");

            // 余力の測定：静止中に、重さの分かっている描画（後処理1回 ≒ 全画面1回）を n 回足して、画面の更新が間に合わなくなる所を探す
            if (System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "headroom")))
            {
                Camera capture = m_left.CaptureCamera;
                for (int pass = 0; pass < 2; pass++)
                {
                    if (pass == 1 && capture != null) capture.enabled = false;
                    for (int n = 0; n <= 7; n++)
                    {
                        Phase($"HEADROOM capture={(pass == 0 ? "on" : "off")} extra={n}");
                        float until = Time.unscaledTime + 1.2f;
                        while (Time.unscaledTime < until)
                        {
                            for (int k = 0; k < n; k++) m_flipView.BenchPostNoBloom();
                            yield return null;
                        }
                    }
                }
                if (capture != null) capture.enabled = true;
                Phase("idle");
                yield return new WaitForSeconds(1f);
            }

            // 1. 右ページだけ暗転
            Phase("dim fade in (0.5s)");
            BookPageState.SetDim(right, 0.75f, 0.5f);
            yield return new WaitForSeconds(0.5f);
            Phase("dim static");
            yield return new WaitForSeconds(2.5f);
            yield return Shot("1_dim_right");
            // 暗転中はめくれない
            Phase("turn request while dimmed");
            m_controller.SendMessage("ShowSpread", start + 1);
            yield return new WaitForSeconds(2f);
            yield return WaitIdle();
            Log($"LOCK dim: spread={Spread()} (expect {start}) locked={m_flipView.TurnLocked} -> {(Spread() == start ? "OK" : "NG")}");
            Phase("dim fade out (0.5s)");
            BookPageState.SetDim(right, 0f, 0.5f);
            yield return new WaitForSeconds(0.5f);
            Phase("idle after dim");
            yield return new WaitForSeconds(2f);

            // 2. 左ページを白紙に → 獲得（発光）。演出中はめくれない
            BookPageState.SetAcquired(left, false);
            yield return new WaitForSeconds(1f);
            yield return Shot("2_blank_left");
            BookPageState.AcquireSettings s = BookPageState.AcquireSettings.Default;
            Phase("acquire spreading");
            BookPageState.PlayAcquire(left, right, false, new Vector2(0.25f, 0.6f), s);
            yield return new WaitForSeconds(s.SpreadDuration * 0.5f);
            yield return Shot("3_acquire_spreading");
            LogHdr();
            m_controller.SendMessage("ShowSpread", start + 1);
            yield return new WaitForSeconds(s.SpreadDuration * 0.5f);
            Phase("acquire full light");
            yield return new WaitForSeconds(s.HoldDuration * 0.9f);
            yield return Shot("4_acquire_full");
            Phase("acquire reveal");
            yield return new WaitForSeconds(s.RevealDuration * 0.45f);
            yield return Shot("5_acquire_reveal");
            yield return new WaitForSeconds(s.RevealDuration * 0.6f);
            Phase("idle after acquire");
            Log($"LOCK acquire: spread={Spread()} (expect {start}) -> {(Spread() == start ? "OK" : "NG")}");
            yield return new WaitForSeconds(1.5f);
            yield return WaitIdle();
            yield return Shot("6_acquired");

            // 3. 次の見開きの左ページを先に暗転 → めくって行くと暗転したまま見える
            BookPageData nextLeft = GetPage((start + 1) * 2);
            BookPageState.SetDim(nextLeft, 0.75f, 0f);
            Phase("page turn");
            m_controller.SendMessage("ShowSpread", start + 1);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("7_turning_to_dimmed");
            yield return new WaitForSeconds(0.22f);
            yield return Shot("7b_turn_late");
            yield return WaitIdle();
            Phase("idle next (dim)");
            yield return new WaitForSeconds(1f);
            yield return Shot("8_next_dimmed");
            Log($"TURN: spread={Spread()} (expect {start + 1})");
            BookPageState.SetDim(nextLeft, 0f, 0f);
            yield return new WaitForSeconds(0.5f);
            Phase("page turn back");
            m_controller.SendMessage("ShowSpread", start);
            yield return new WaitForSeconds(0.5f);
            yield return WaitIdle();
            Phase("idle");
            yield return new WaitForSeconds(1f);

            // 4. まとめてめくり（残りの見開きの数まで）
            int bundle = Mathf.Min(4, count - 1 - start);
            if (bundle >= 2)
            {
                Phase("bundle turn");
                m_flipView.QueueBundleTurn(bundle);
                m_controller.SendMessage("ShowSpread", start + bundle);
                yield return new WaitForSeconds(0.45f);
                yield return Shot("9_bundle_turning");
                yield return WaitIdle();
                Log($"BUNDLE: spread={Spread()} (expect {start + bundle})");
                Phase("bundle turn back");
                m_flipView.QueueBundleTurn(bundle);
                m_controller.SendMessage("ShowSpread", start);
                yield return new WaitForSeconds(0.5f);
                yield return WaitIdle();
            }
            else Log($"bundle skipped (spreads={count})");
            Phase("idle end");
            yield return new WaitForSeconds(2f);
            Phase(null);
            Log("end");
        }

        // めくり・撮影が終わるまで待つ
        private IEnumerator WaitIdle()
        {
            PropertyInfo busy = m_controller.GetType().GetProperty("IsBusy");
            for (int i = 0; i < 600; i++)
            {
                bool b = busy != null && (bool)busy.GetValue(m_controller);
                if (!b && !m_flipView.IsTurning) break;
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
        }

        private int Count()
        {
            PropertyInfo p = m_controller.GetType().GetProperty("SpreadCount");
            return p != null ? (int)p.GetValue(m_controller) : 1;
        }

        private IEnumerator Shot(string name)
        {
            // noshot の印：画面を保存しない（保存は数フレーム止まるので、時間を測るときは外す）
            if (Flag("noshot")) yield break;
            ScreenCapture.CaptureScreenshot((Application.isEditor ? "Logs/" : "") + "pagefx_" + name + ".png");
            yield return null;
        }

        // 撮影テクスチャに 1 を超える明るさ（発光）が入っているか
        private void LogHdr()
        {
            if (Flag("noshot")) return;
            Camera camera = m_left.CaptureCamera;
            RenderTexture rt = camera != null ? camera.targetTexture : null;
            if (rt == null) { Log("hdr: no texture"); return; }
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            RenderTexture.active = prev;
            float max = 0f;
            Color[] px = tex.GetPixels();
            for (int i = 0; i < px.Length; i += 7) max = Mathf.Max(max, px[i].maxColorComponent);
            Destroy(tex);
            Log($"hdr max={max:F2} format={rt.format}");
        }

        private int Spread()
        {
            PropertyInfo p = m_controller.GetType().GetProperty("CurrentSpread");
            return p != null ? (int)p.GetValue(m_controller) : -1;
        }

        private BookPageData GetPage(int index)
        {
            MethodInfo m = m_controller.GetType().GetMethod("GetPage", new[] { typeof(int) });
            return m != null ? m.Invoke(m_controller, new object[] { index }) as BookPageData : null;
        }

        private void Log(string what) => Debug.Log($"[PageFx] {Time.realtimeSinceStartup:F1} {what}");
    }
}
