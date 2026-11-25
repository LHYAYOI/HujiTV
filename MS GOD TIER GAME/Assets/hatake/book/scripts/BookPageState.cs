using System.Collections.Generic;
using UnityEngine;

namespace hatake
{
    /// <summary>
    /// ページ（BookPageData）ごとの見た目の状態：暗転・未獲得（白紙）・獲得の演出。
    /// BookPageView がページを表示するたびにここを読み、ページの UI の上に覆いを描く。
    /// 覆いはページの撮影（レンダーテクスチャ）に焼き込まれるので、めくっている最中も、
    /// 別の見開きへ行って戻ったときも、同じ状態のまま見える。合成のシェーダーは何も増えない。
    ///
    /// 使い方（ゲーム側）：
    ///   BookPageState.SetDim(page, 0.8f, 0.3f);                 // そのページだけ暗転（0.3秒で）
    ///   BookPageState.SetSpreadDim(left, right, 0.8f, 0.3f);    // 見開き両面を暗転
    ///   BookPageState.SetAcquired(page, false);                 // 未獲得：白紙で表示
    ///   BookPageState.PlayAcquire(page, partner, pageOnRight, origin, color);  // 獲得の演出
    /// 状態はアプリ実行中だけ保持する（保存が要るならゲーム側のセーブデータから起動時に設定し直す）。
    /// </summary>
    public static class BookPageState
    {
        /// <summary>獲得の演出の設定</summary>
        public struct AcquireSettings
        {
            public Color Color;          // 光の色（ページを塗らず、この色で光る）
            public float SpreadDuration; // 起点から見開き全体が光るまで
            public float HoldDuration;   // 全部光ってから光が引き始めるまで（この間に光が強まる）
            public float RevealDuration; // 色が薄れてページが現れるまで
            public float EdgeWidth;      // 光の境目のぎざぎざの幅（ページ幅比）
            public float NoiseScale;     // ぎざぎざの細かさ（ページ幅あたりの数）
            public float Glow;           // 光の明るさ（ページに足す。1 を超えると本の Bloom で光る）
            public float RimGlow;        // 広がっていく境目の光の強さ（Bloom で光る）
            public float Flash;          // 全部光ったときの光の強まり（Glow に掛ける倍率）。光の中からページが出てくる
            public float Whiten;         // 全部光ったときに光が白へ寄る量 0〜1（0 で指定色のまま光る）

            public static AcquireSettings Default => new AcquireSettings
            {
                Color = new Color(0.95f, 0.78f, 0.35f, 1f),
                SpreadDuration = 1.2f,
                HoldDuration = 0.35f,
                RevealDuration = 0.8f,
                EdgeWidth = 0.18f,
                NoiseScale = 9f,
                Glow = 1.6f,
                RimGlow = 4f,
                Flash = 2.5f,
                Whiten = 0.25f
            };
        }

        /// <summary>覆いに描く値（BookPageView が読む）</summary>
        public struct Overlay
        {
            public float Dim;          // 0〜1：黒を重ねる量
            public bool Blank;         // 白紙（中身を隠す）
            public float Fill;         // 0〜1：光の広がりの進み（1で見開き全体）
            public float FillAlpha;    // 0〜1：光の残り（1 = 光で覆われている。薄れて 0 でページが出る）
            public float Light;        // 0〜1：全部光った後の光の強まり
            public Vector2 Origin;     // 起点（見開き全体を0〜1、左下が0）
            public bool OnRight;       // このページが見開きの右側か
            public AcquireSettings Settings;

            public bool Visible => Dim > 0.001f || Blank || FillAlpha > 0.001f && Fill > 0f;
        }

        private sealed class Acquire
        {
            public float Start;
            public Vector2 Origin;
            public bool OnRight;
            public bool Reveals;      // このページが獲得されるページ（白紙→中身）
            public AcquireSettings Settings;
        }

        private sealed class Entry
        {
            public float DimFrom, DimTo, DimStart, DimDuration;
            public bool Unacquired;
            public Acquire Acquire;
        }

        private static readonly Dictionary<BookPageData, Entry> s_entries = new Dictionary<BookPageData, Entry>();

        /// <summary>状態が変わるたびに増える（BookPageView が覆いを描き直すかの判定に使う）</summary>
        public static int Version { get; private set; }

        private static float Now => Time.unscaledTime;

        // ------------------------------------------------------------------
        // 暗転
        // ------------------------------------------------------------------
        /// <summary>そのページだけを暗転する。amount 0で解除。duration 秒かけて変える</summary>
        public static void SetDim(BookPageData page, float amount, float duration = 0f)
        {
            if (page == null) return;
            Entry e = Get(page, true);
            e.DimFrom = DimOf(e, Now);
            e.DimTo = Mathf.Clamp01(amount);
            e.DimStart = Now;
            e.DimDuration = Mathf.Max(0f, duration);
            Changed(page);
        }

        /// <summary>見開き両面を暗転する（左右どちらかが null でもよい）</summary>
        public static void SetSpreadDim(BookPageData left, BookPageData right, float amount, float duration = 0f)
        {
            SetDim(left, amount, duration);
            SetDim(right, amount, duration);
        }

        /// <summary>暗転の目標値（フェード中なら行き先）</summary>
        public static float GetDim(BookPageData page) =>
            page != null && s_entries.TryGetValue(page, out Entry e) ? e.DimTo : 0f;

        // ------------------------------------------------------------------
        // 獲得
        // ------------------------------------------------------------------
        /// <summary>未獲得（false）にすると白紙で表示する。既定はすべて獲得済み</summary>
        public static void SetAcquired(BookPageData page, bool acquired)
        {
            if (page == null) return;
            Entry e = Get(page, true);
            e.Unacquired = !acquired;
            if (e.Acquire != null && e.Acquire.Reveals) e.Acquire = null;
            Changed(page);
        }

        public static bool IsAcquired(BookPageData page) =>
            page == null || !s_entries.TryGetValue(page, out Entry e) || !e.Unacquired;

        /// <summary>
        /// 見開きの1ページを獲得する演出。
        /// origin（見開き全体を0〜1、左下が0）から Dissolve で指定色の光が広がって見開き全体が光り、
        /// 全部光ったら光が強まり、その中で page が白紙から中身に変わって、光が引くとページが現れる。
        /// partner は同じ見開きのもう片方（光って、引くと元のまま）。null でもよい。
        /// </summary>
        public static void PlayAcquire(BookPageData page, BookPageData partner, bool pageOnRight, Vector2 origin,
            AcquireSettings settings)
        {
            if (page == null) return;
            float now = Now;
            Entry e = Get(page, true);
            e.Unacquired = true;   // 光が引き始めるまでは白紙のまま
            e.Acquire = new Acquire { Start = now, Origin = origin, OnRight = pageOnRight, Reveals = true, Settings = settings };
            Changed(page);
            if (partner != null && partner != page)
            {
                Entry p = Get(partner, true);
                p.Acquire = new Acquire { Start = now, Origin = origin, OnRight = !pageOnRight, Reveals = false, Settings = settings };
                Changed(partner);
            }
        }

        public static void PlayAcquire(BookPageData page, BookPageData partner, bool pageOnRight, Vector2 origin, Color color)
        {
            AcquireSettings settings = AcquireSettings.Default;
            settings.Color = color;
            PlayAcquire(page, partner, pageOnRight, origin, settings);
        }

        /// <summary>獲得の演出中か</summary>
        public static bool IsAcquiring(BookPageData page) =>
            page != null && s_entries.TryGetValue(page, out Entry e) && Acquiring(e);

        /// <summary>どこかのページで獲得の演出中か（演出中はページをめくれない）</summary>
        public static bool AnyAcquiring
        {
            get
            {
                bool any = false;
                foreach (Entry e in s_entries.Values) if (Acquiring(e)) any = true;
                return any;
            }
        }

        /// <summary>
        /// このページが見開きにある間はページをめくれないか（暗転している・暗転へ変わっている途中・獲得の演出中）。
        /// 暗転を解除するフェードの途中はめくれる
        /// </summary>
        public static bool BlocksTurn(BookPageData page) =>
            page != null && s_entries.TryGetValue(page, out Entry e) && (e.DimTo > 0f || Acquiring(e));

        // 獲得の演出が続いているか。時間が過ぎていれば、表示されていないページでもここで片付ける
        // （画面に出ていないページの演出が残って、ページがめくれないままにならないように）
        private static bool Acquiring(Entry e)
        {
            Acquire a = e.Acquire;
            if (a == null) return false;
            AcquireSettings s = a.Settings;
            float length = Mathf.Max(1e-3f, s.SpreadDuration) + Mathf.Max(0f, s.HoldDuration) + Mathf.Max(1e-3f, s.RevealDuration);
            if (Now < a.Start + length) return true;
            if (a.Reveals) e.Unacquired = false;
            e.Acquire = null;
            Version++;
            return false;
        }

        /// <summary>すべての状態を消す（新しいゲームなど）</summary>
        public static void Clear()
        {
            s_entries.Clear();
            Version++;
        }

        // ------------------------------------------------------------------
        // 覆いの値
        // ------------------------------------------------------------------
        /// <summary>
        /// 今の覆いの値。動いている（フェード中・演出中）なら true を返す。
        /// 演出が終わったらここで状態を片付ける。
        /// </summary>
        public static bool Evaluate(BookPageData page, out Overlay overlay)
        {
            overlay = default;
            if (page == null || !s_entries.TryGetValue(page, out Entry e)) return false;
            float now = Now;
            bool animating = false;

            overlay.Dim = DimOf(e, now);
            if (e.DimDuration > 0f && now < e.DimStart + e.DimDuration) animating = true;
            overlay.Blank = e.Unacquired;

            Acquire a = e.Acquire;
            if (a != null)
            {
                AcquireSettings s = a.Settings;
                float t = now - a.Start;
                float spread = Mathf.Max(1e-3f, s.SpreadDuration);
                float revealStart = spread + Mathf.Max(0f, s.HoldDuration);
                float reveal = Mathf.Max(1e-3f, s.RevealDuration);
                if (t >= revealStart + reveal)
                {
                    // 終わり：獲得済みにして片付ける
                    if (a.Reveals) e.Unacquired = false;
                    e.Acquire = null;
                    Version++;
                    overlay.Blank = e.Unacquired;
                }
                else
                {
                    animating = true;
                    overlay.Fill = Mathf.Clamp01(t / spread);
                    float fade = Mathf.Clamp01((t - revealStart) / reveal);
                    // 光の残り（シェーダーでぎざぎざに抜けて、光の中からページが出てくる）
                    overlay.FillAlpha = 1f - fade;
                    // 全部光ったら光が強まり（Whiten 分だけ白へ寄る）、引きながら弱まる
                    float hold = Mathf.Max(1e-3f, revealStart - spread);
                    float rise = Mathf.Clamp01((t - spread) / hold);
                    overlay.Light = t < spread ? 0f : rise * rise * (3f - 2f * rise) * (1f - fade * fade);
                    // 色が薄れ始めたら、下は中身に変わっている
                    if (a.Reveals && t >= revealStart) overlay.Blank = false;
                    overlay.Origin = a.Origin;
                    overlay.OnRight = a.OnRight;
                    overlay.Settings = s;
                }
            }
            return animating;
        }

        private static float DimOf(Entry e, float now)
        {
            if (e.DimDuration <= 0f) return e.DimTo;
            float t = Mathf.Clamp01((now - e.DimStart) / e.DimDuration);
            return Mathf.Lerp(e.DimFrom, e.DimTo, t * t * (3f - 2f * t));
        }

        private static Entry Get(BookPageData page, bool create)
        {
            if (!s_entries.TryGetValue(page, out Entry e) && create)
            {
                e = new Entry();
                s_entries.Add(page, e);
            }
            return e;
        }

        private static void Changed(BookPageData page) => Version++;

        // ドメインの再読み込みを切った Play でも前回の状態を持ち越さない
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            s_entries.Clear();
            Version = 0;
        }
    }
}
