using System.Collections.Generic;
using hatake;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// <summary>
// BookPageViewは、BookPageDataの内容をUI上に表示するためのコンポーネント

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(RectMask2D))]
[RequireComponent(typeof(Image))]

public sealed class BookPageView : MonoBehaviour
{
    [SerializeField] private RectTransform m_pageContent;

    private readonly List<GameObject> m_imageObjects =
        new List<GameObject>();

    [Tooltip("ON：このページを撮影するカメラ（ページの Canvas の Render Camera）の、ページの UI に要らない処理を切る。" +
             "後処理・影・MSAA・アンチエイリアス。共通の Volume は中立なので見た目は変わらない。" +
             "hatake の BookController が自分の撮影カメラに掛けているのと同じ設定")]
    [SerializeField] private bool m_lightweightCaptureCamera = true;
    private Camera m_lightenedCamera;

    [Tooltip("ON：モバイルで、このページを撮影するレンダーテクスチャを軽い設定に変える（実行中だけ。アセットは書き換えない）。" +
             "HDR のまま半分の容量の形式（R11G11B10。不透明度は合成で使っていない）・ミップマップ無し・下の高さまで縮小。" +
             "撮影が毎フレーム動くコントローラーでは、撮影の時間が半分以下になる")]
    [SerializeField] private bool m_lightweightCaptureTextureMobile = true;
    [Tooltip("モバイルでの撮影テクスチャの高さの上限（本の出力の高さに合わせる）")]
    [SerializeField, Min(180)] private int m_captureHeightMobile = 720;
    private RenderTexture m_checkedTexture;
    private static readonly HashSet<RenderTexture> s_lightTextures = new HashSet<RenderTexture>();

    // 撮影先のレンダーテクスチャを、最初の撮影の前に軽い設定へ変える（モバイルだけ）
    private void LightenCaptureTexture()
    {
        if (!m_lightweightCaptureTextureMobile || !Application.isPlaying || !Application.isMobilePlatform) return;
        Camera camera = CaptureCamera;
        RenderTexture texture = camera != null ? camera.targetTexture : null;
        if (texture == m_checkedTexture) return;
        m_checkedTexture = texture;
        if (texture == null || !s_lightTextures.Add(texture)) return;
        if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float)) return;
        int height = Mathf.Min(texture.height, m_captureHeightMobile);
        int width = Mathf.Max(1, Mathf.RoundToInt(texture.width * (height / (float)texture.height)));
        // もう描かれている（表示に使われているかもしれない）：変えた後、撮影し直して伝える
        bool used = texture.IsCreated();
        texture.Release();
        texture.width = width;
        texture.height = height;
        texture.format = RenderTextureFormat.RGB111110Float;
        texture.useMipMap = false;
        texture.autoGenerateMips = false;
        texture.Create();
        if (used)
        {
            m_lastNotifyFrame = -1;
            NotifyContentChanged();
            StateOverlayChanged?.Invoke(this);
        }
    }

    // ------------------------------------------------------------------
    // ページの状態の覆い（暗転・白紙・獲得の光）。BookPageState をページごとに読んで、
    // ページの UI の一番上に1枚重ねる。撮影に焼き込まれるので、めくり中も開き直しても続く。
    // 何も無いページでは覆いを止めているので、ふだんの負荷は増えない。
    // ------------------------------------------------------------------
    [Tooltip("覆いのシェーダー（hatake/BookPageOverlay）。空なら Resources から読む")]
    [SerializeField] private Shader m_overlayShader;
    private BookPageData m_pageData;
    private RawImage m_overlay;
    private Material m_overlayMaterial;
    private int m_overlayVersion = -1;
    private bool m_overlayAnimating;

    private static readonly int s_overlayBlankColor = Shader.PropertyToID("_BlankColor");
    private static readonly int s_overlayBlank = Shader.PropertyToID("_Blank");
    private static readonly int s_overlayDim = Shader.PropertyToID("_Dim");
    private static readonly int s_overlayFillColor = Shader.PropertyToID("_FillColor");
    private static readonly int s_overlayFill = Shader.PropertyToID("_Fill");
    private static readonly int s_overlayFillAlpha = Shader.PropertyToID("_FillAlpha");
    private static readonly int s_overlayOrigin = Shader.PropertyToID("_Origin");
    private static readonly int s_overlaySide = Shader.PropertyToID("_Side");
    private static readonly int s_overlayPageAspect = Shader.PropertyToID("_PageAspect");
    private static readonly int s_overlayEdge = Shader.PropertyToID("_Edge");
    private static readonly int s_overlayGlow = Shader.PropertyToID("_Glow");
    private static readonly int s_overlayNoise = Shader.PropertyToID("_NoiseTex");
    private static Texture2D s_overlayNoiseTexture;

    // 光の境目のぎざぎざ用の乱数（64x64。繰り返し・なめらかに補間して読む）
    private static Texture2D OverlayNoise()
    {
        if (s_overlayNoiseTexture != null) return s_overlayNoiseTexture;
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.R8, false, true)
        {
            name = "BookPageOverlayNoise", hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear
        };
        var random = new System.Random(20261117);
        byte[] data = new byte[size * size];
        random.NextBytes(data);
        texture.SetPixelData(data, 0);
        texture.Apply(false, true);
        return s_overlayNoiseTexture = texture;
    }

    /// <summary>表示中のページ</summary>
    public BookPageData PageData => m_pageData;

    // ------------------------------------------------------------------
    // ページの中身が変わったことの通知。
    // ページの下にある Graphic（線・画像など）の頂点やマテリアルが変わると、そのフレームに1度だけ知らせる。
    // BookFlipView はこれを受けて、変わったページだけを軽く描き直す（線を引くコードが MarkDirty を呼ばなくてよい）。
    // 拾えないもの：Transform だけを動かした場合、マテリアルのプロパティだけを書き換えた場合。
    // そのときは BookFlipView.MarkContentDirty を呼ぶ
    // ------------------------------------------------------------------
    /// <summary>このページの中身が変わった（次の撮影で絵が変わる）</summary>
    public static event System.Action<BookPageView> ContentChanged;

    /// <summary>このページの状態の覆い（暗転・白紙・獲得の光）が変わった。ContentChanged も同時に出る</summary>
    public static event System.Action<BookPageView> StateOverlayChanged;

    private readonly List<Graphic> m_watchedGraphics = new List<Graphic>();
    private UnityEngine.Events.UnityAction m_graphicDirtyAction;
    private int m_lastNotifyFrame = -1;
    private Canvas m_rootCanvas;

    /// <summary>このページを撮影するカメラ（ページの Canvas の Render Camera）。無ければ null</summary>
    public Camera CaptureCamera
    {
        get
        {
            if (m_rootCanvas == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                m_rootCanvas = canvas != null ? canvas.rootCanvas : null;
            }
            return m_rootCanvas != null ? m_rootCanvas.worldCamera : null;
        }
    }

    // 有効なページの表示（BookFlipView が、表示中の見開きのページの状態を調べるのに使う）
    private static readonly List<BookPageView> s_activeViews = new List<BookPageView>();
    public static IReadOnlyList<BookPageView> ActiveViews => s_activeViews;

    // ------------------------------------------------------------------
    // 撮影は URP の Render Scale を使わない。
    // Render Scale が 1 未満だと、URP はページを小さく描いてから撮影テクスチャへ引き伸ばす（ページがぼけ、引き伸ばしの描画が1回増える）。
    // ページの撮影カメラを描く間だけ Render Scale を 1 にして、撮影テクスチャへ直接描かせる
    // ------------------------------------------------------------------
    [Tooltip("ON：ページの撮影カメラは URP の Render Scale の影響を受けない（撮影テクスチャの解像度でそのまま描く）")]
    [SerializeField] private bool m_fullScaleCapture = true;
    private static bool s_scaleHooked;
    private static float s_savedScale = -1f;

    private static void HookRenderScale()
    {
        if (s_scaleHooked) return;
        s_scaleHooked = true;
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += OnEndCamera;
        // このフレームの撮影を休ませたカメラは、描画が終わったらすぐ戻す（休ませている間に他の処理が挟まらないように）
        UnityEngine.Rendering.RenderPipelineManager.endContextRendering += (context, cameras) => RestoreSkippedCaptures(true);
    }

    private static bool IsFullScaleCapture(Camera camera)
    {
        if (camera == null || camera.targetTexture == null) return false;
        for (int i = 0; i < s_activeViews.Count; i++)
        {
            BookPageView view = s_activeViews[i];
            if (view.m_fullScaleCapture && view.CaptureCamera == camera) return true;
        }
        return false;
    }

    private static void OnBeginCamera(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
    {
        UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
        if (asset == null || s_savedScale >= 0f || Mathf.Approximately(asset.renderScale, 1f) || !IsFullScaleCapture(camera)) return;
        s_savedScale = asset.renderScale;
        asset.renderScale = 1f;
    }

    private static void OnEndCamera(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
    {
        if (s_savedScale < 0f) return;
        UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
        if (asset != null) asset.renderScale = s_savedScale;
        s_savedScale = -1f;
    }

    private void OnEnable()
    {
        HookRenderScale();
        if (!s_activeViews.Contains(this)) s_activeViews.Add(this);
        WatchGraphics();
        LightenCaptureCamera();
    }

    // 撮影カメラが差し替わった（めくり中の撮影用に複製された Canvas など）ときも軽くする
    private void LateUpdate()
    {
        RestoreSkippedCaptures();
        if (m_dirtyWhilePaused && !CapturesPaused)
        {
            m_dirtyWhilePaused = false;
            m_lastNotifyFrame = -1;
            NotifyContentChanged();
        }
        UpdateOverlay(false);
        LightenCaptureTexture();
        AlternateCaptures();
        if (m_lightweightCaptureCamera && m_rootCanvas != null && m_rootCanvas.worldCamera != m_lightenedCamera)
        {
            LightenCaptureCamera();
        }
    }

    private void LightenCaptureCamera()
    {
        if (!m_lightweightCaptureCamera || !Application.isPlaying) return;
        m_rootCanvas = null;
        Camera camera = CaptureCamera;
        m_lightenedCamera = camera;
        // ページの Canvas だけを撮るカメラ（RenderTexture へ描く）に限る。画面を描くカメラは触らない
        if (camera == null || camera.targetTexture == null && !IsPageTexture(camera)) return;
        camera.allowMSAA = false;
        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        if (data == null) return;
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.renderShadows = false;
    }

    // targetTexture が実行中に入る撮影カメラ：ページの Canvas が Screen Space - Camera で、そのカメラが画面を描いていない
    private bool IsPageTexture(Camera camera)
    {
        return m_rootCanvas != null && m_rootCanvas.renderMode == RenderMode.ScreenSpaceCamera &&
               (camera.cullingMask & ~(1 << gameObject.layer)) == 0;
    }

    private void OnDisable()
    {
        s_activeViews.Remove(this);
        UnwatchGraphics();
    }

    // 直下の子が増減した（ページの下へ表示物を足した・消した）
    private void OnTransformChildrenChanged()
    {
        if (!isActiveAndEnabled) return;
        // 後から足された表示物（線など）も覆いの下にする
        if (m_overlay != null && m_overlay.transform.GetSiblingIndex() != transform.childCount - 1)
            m_overlay.transform.SetAsLastSibling();
        WatchGraphics();
        NotifyContentChanged();
    }

    private void OnTransformParentChanged()
    {
        m_rootCanvas = null;
    }

    // ページの下の Graphic を見張り直す
    private void WatchGraphics()
    {
        UnwatchGraphics();
        m_graphicDirtyAction ??= NotifyContentChanged;
        GetComponentsInChildren(true, m_watchedGraphics);
        foreach (Graphic graphic in m_watchedGraphics)
        {
            graphic.RegisterDirtyVerticesCallback(m_graphicDirtyAction);
            graphic.RegisterDirtyMaterialCallback(m_graphicDirtyAction);
        }
    }

    private void UnwatchGraphics()
    {
        if (m_graphicDirtyAction != null)
        {
            foreach (Graphic graphic in m_watchedGraphics)
            {
                // 消された Graphic は飛ばす
                if (graphic == null) continue;
                graphic.UnregisterDirtyVerticesCallback(m_graphicDirtyAction);
                graphic.UnregisterDirtyMaterialCallback(m_graphicDirtyAction);
            }
        }
        m_watchedGraphics.Clear();
    }

    // ------------------------------------------------------------------
    // 撮影の交互化（モバイル）。
    // めくっている間は、コントローラーが「今の見開き」と「次の見開き」を別々のカメラで毎フレーム撮影する。
    // 2台が同時に動いているフレームでは1台ずつ交互に撮影して（それぞれ毎秒30回）、撮影の時間を半分にする。
    // 撮影し始めた直後・撮影先が変わった直後・中身が変わったフレームは必ず撮影する
    // ------------------------------------------------------------------
    [Tooltip("ON：モバイルで、ページの撮影カメラが2台同時に動いている間（めくり中）は、1台ずつ交互に撮影する")]
    [SerializeField] private bool m_alternateCapturesMobile = true;

    private sealed class CaptureState
    {
        public RenderTexture Target;
        public int SinceFrame;    // この撮影先で動き始めたフレーム
        public int SeenFrame;     // 最後に動いているのを見たフレーム
        public bool Skipped;      // このフレームの撮影を止めている
    }
    private static readonly Dictionary<Camera, CaptureState> s_captures = new Dictionary<Camera, CaptureState>();
    private static readonly List<Camera> s_captureList = new List<Camera>();
    private static int s_alternateFrame = -1, s_restoreFrame = -1, s_alternateTurn, s_pauseFrame = -10;
    private bool m_dirtyWhilePaused;

    [Tooltip("ON：モバイルで、本のめくりが撮影テクスチャを読まない間（めくり始めに絵を作り終えてから、めくり終わるまで）は" +
             "ページの撮影を休む。その間に中身が変わったページは、めくり終わりに撮影し直して伝える")]
    [SerializeField] private bool m_pauseCapturesWhileTurningMobile = true;

    /// <summary>
    /// このフレームと次のフレームは、ページの撮影テクスチャを読まない（BookFlipView がめくり中に毎フレーム呼ぶ）。
    /// モバイルでは、その間の撮影を休む
    /// </summary>
    public static void PauseCaptures() => s_pauseFrame = Time.frameCount;
    private static bool CapturesPaused => Time.frameCount - s_pauseFrame >= 0 && Time.frameCount - s_pauseFrame <= 1;
    private static bool s_wasPaused;
    internal static bool DebugNoAlternate; // 計測用

    // 前のフレームに止めた撮影カメラを戻す（撮影先がそのままのときだけ。コントローラーが撮影をやめていたら戻さない）
    private static void RestoreSkippedCaptures(bool afterRendering = false)
    {
        if (!afterRendering)
        {
            if (s_restoreFrame == Time.frameCount) return;
            s_restoreFrame = Time.frameCount;
        }
        foreach (KeyValuePair<Camera, CaptureState> pair in s_captures)
        {
            if (!pair.Value.Skipped) continue;
            pair.Value.Skipped = false;
            Camera camera = pair.Key;
            if (camera != null && camera.targetTexture == pair.Value.Target && pair.Value.Target != null) camera.enabled = true;
        }
    }

    private void Update()
    {
        RestoreSkippedCaptures();
    }

    // 止めていた撮影を、このフレームは行う（中身が変わった）
    private void EnsureCapturedThisFrame()
    {
        Camera camera = CaptureCamera;
        if (camera == null || !s_captures.TryGetValue(camera, out CaptureState state) || !state.Skipped) return;
        state.Skipped = false;
        if (camera.targetTexture == state.Target && state.Target != null) camera.enabled = true;
    }

    // 1フレームに1度：動いている撮影カメラが2台以上なら、順番に1台だけ止める
    private void AlternateCaptures()
    {
        if (s_alternateFrame == Time.frameCount) return;
        s_alternateFrame = Time.frameCount;
        if (!m_alternateCapturesMobile || !Application.isMobilePlatform || DebugNoAlternate) return;
        int frame = Time.frameCount;
        // 休みが明けた：休んでいる間の変化（知らせの来ない動きも含む）を、全部のページで1度撮影して伝え直す
        bool pausedNow = m_pauseCapturesWhileTurningMobile && CapturesPaused;
        if (s_wasPaused && !pausedNow)
            for (int i = 0; i < s_activeViews.Count; i++) s_activeViews[i].m_dirtyWhilePaused = true;
        s_wasPaused = pausedNow;
        s_captureList.Clear();
        for (int i = 0; i < s_activeViews.Count; i++)
        {
            BookPageView view = s_activeViews[i];
            Camera camera = view.CaptureCamera;
            if (camera == null || !camera.isActiveAndEnabled || camera.targetTexture == null || s_captureList.Contains(camera)) continue;
            if (!s_captures.TryGetValue(camera, out CaptureState state)) s_captures.Add(camera, state = new CaptureState { SeenFrame = -10 });
            // 撮影し始めた・撮影先が変わった：そこから数え直す
            if (state.Target != camera.targetTexture || state.SeenFrame < frame - 2)
            {
                state.Target = camera.targetTexture;
                state.SinceFrame = frame;
            }
            state.SeenFrame = frame;
            s_captureList.Add(camera);
        }
        if (m_pauseCapturesWhileTurningMobile && CapturesPaused)
        {
            // 本が撮影テクスチャを読まない間：全部休む（動き始めた直後のカメラは除く）
            for (int i = 0; i < s_captureList.Count; i++)
            {
                CaptureState state = s_captures[s_captureList[i]];
                if (frame - state.SinceFrame < 2) continue;
                state.Skipped = true;
                s_captureList[i].enabled = false;
            }
            return;
        }
        if (s_captureList.Count < 2) return;
        Camera skip = s_captureList[s_alternateTurn++ % s_captureList.Count];
        CaptureState skipState = s_captures[skip];
        // 動き始めて2フレームは必ず撮影する（コントローラーが撮影の完了を待っている）
        if (frame - skipState.SinceFrame < 2) return;
        // そのカメラが撮るページの中身がこのフレームに変わっていたら撮影する
        for (int i = 0; i < s_activeViews.Count; i++)
            if (s_activeViews[i].m_lastNotifyFrame == frame && s_activeViews[i].CaptureCamera == skip) return;
        skipState.Skipped = true;
        skip.enabled = false;
    }

    private void NotifyContentChanged()
    {
        // 1フレームに何度変わっても、知らせるのは1度
        if (m_lastNotifyFrame == Time.frameCount) return;
        m_lastNotifyFrame = Time.frameCount;
        if (m_pauseCapturesWhileTurningMobile && CapturesPaused && Application.isMobilePlatform && !DebugNoAlternate)
        {
            // 撮影を休んでいる間の変化：休みが明けてから撮影して伝える
            m_dirtyWhilePaused = true;
            return;
        }
        EnsureCapturedThisFrame();
        ContentChanged?.Invoke(this);
    }

    public void ShowPage(BookPageData pageData)
    {
        // Page Content が未設定なら、ページ自身に画像を置く（ページは RectMask2D で切り抜かれる）
        if (m_pageContent == null)
        {
            m_pageContent = (RectTransform)transform;
        }

        ClearImages();
        m_pageData = pageData;
        // コントローラーは撮影先を決めてからページを表示する：撮影の前に撮影先を軽くしておく
        m_rootCanvas = null;
        LightenCaptureTexture();

        RectTransform pageTransform = (RectTransform)transform;

        pageTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            BookPageData.PageSize.x);

        pageTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            BookPageData.PageSize.y);

        Image backgroundImage = GetComponent<Image>();

        backgroundImage.sprite = null;
        backgroundImage.color = pageData != null
            ? pageData.BackgroundColor
            : Color.white;

        backgroundImage.raycastTarget = true;

        if (pageData == null)
        {
            UpdateOverlay(true);
            return;
        }

        foreach (BookImageData imageData in pageData.Images)
        {
            if (imageData == null || imageData.Sprite == null)
            {
                continue;
            }

            GameObject imageObject = new GameObject(
                imageData.Sprite.name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            imageObject.transform.SetParent(m_pageContent, false);

            // 撮影Cameraの対象Layerに揃えます。
            imageObject.layer = gameObject.layer;

            m_imageObjects.Add(imageObject);

            RectTransform imageTransform =
                imageObject.GetComponent<RectTransform>();

            // 画像の基準点を左上に統一
            imageTransform.anchorMin = new Vector2(0f, 1f);
            imageTransform.anchorMax = new Vector2(0f, 1f);
            imageTransform.pivot = new Vector2(0f, 1f);

            // 保存データのYは下向き、UI座標のYは上向き
            imageTransform.anchoredPosition = new Vector2(
                imageData.Position.x,
                -imageData.Position.y);

            imageTransform.sizeDelta = imageData.Size;

            Image image = imageObject.GetComponent<Image>();

            image.sprite = imageData.Sprite;
            image.material = imageData.Material;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }

        // 作り直した画像の上に覆いを置き直す
        UpdateOverlay(true);

        // 作り直した画像も見張る
        if (isActiveAndEnabled)
        {
            WatchGraphics();
            NotifyContentChanged();
        }
    }

    // 覆いを今のページの状態に合わせる。変わったときだけ書き換えて、中身が変わったと知らせる
    private void UpdateOverlay(bool force)
    {
        if (!force && !m_overlayAnimating && m_overlayVersion == BookPageState.Version) return;
        m_overlayVersion = BookPageState.Version;
        m_overlayAnimating = BookPageState.Evaluate(m_pageData, out BookPageState.Overlay state);
        // Evaluate が演出を片付けたら版が進むので、合わせ直す
        m_overlayVersion = BookPageState.Version;

        bool visible = m_pageData != null && state.Visible;
        if (!visible)
        {
            if (m_overlay != null && m_overlay.enabled)
            {
                m_overlay.enabled = false;
                NotifyContentChanged();
                StateOverlayChanged?.Invoke(this);
            }
            return;
        }

        if (!EnsureOverlay()) return;
        Material m = m_overlayMaterial;
        RectTransform page = (RectTransform)transform;
        Vector2 size = page.rect.size;
        m.SetColor(s_overlayBlankColor, m_pageData.BackgroundColor);
        m.SetFloat(s_overlayBlank, state.Blank ? 1f : 0f);
        m.SetFloat(s_overlayDim, state.Dim);
        m.SetFloat(s_overlayFill, state.Fill);
        m.SetFloat(s_overlayFillAlpha, state.FillAlpha);
        m.SetColor(s_overlayFillColor, state.Settings.Color);
        m.SetVector(s_overlayOrigin, state.Origin);
        m.SetFloat(s_overlaySide, state.OnRight ? 1f : 0f);
        m.SetFloat(s_overlayPageAspect, size.x > 0f ? size.y / size.x : 1f);
        BookPageState.AcquireSettings s = state.Settings;
        m.SetVector(s_overlayEdge, new Vector4(s.EdgeWidth, Mathf.Max(0.01f, s.NoiseScale), state.Light, 0f));
        // 光の強さ：x 光, y 境目, z 全部光ったときの倍率, w 白へ寄る量（0 の設定は既定の見た目に）
        m.SetVector(s_overlayGlow, new Vector4(s.Glow > 0f ? s.Glow : 1f, Mathf.Max(0f, s.RimGlow),
            Mathf.Max(1f, s.Flash), Mathf.Clamp01(s.Whiten)));
        if (m_overlay.transform.GetSiblingIndex() != transform.childCount - 1) m_overlay.transform.SetAsLastSibling();
        m_overlay.enabled = true;
        // マスクで複製されたマテリアルにも値を渡す
        m_overlay.SetMaterialDirty();
        NotifyContentChanged();
        StateOverlayChanged?.Invoke(this);
    }

    private bool EnsureOverlay()
    {
        if (m_overlay != null) return true;
        // 覆いが外から消された：作り直す（前のマテリアルは捨てる）
        if (m_overlayMaterial != null) Destroy(m_overlayMaterial);
        Shader shader = m_overlayShader != null ? m_overlayShader : Resources.Load<Shader>("BookPageOverlay");
        if (shader == null)
        {
            Debug.LogWarning("BookPageView：覆いのシェーダー hatake/BookPageOverlay が見つかりません", this);
            return false;
        }
        m_overlayMaterial = new Material(shader) { name = "BookPageOverlay (Instance)", hideFlags = HideFlags.DontSave };
        m_overlayMaterial.SetTexture(s_overlayNoise, OverlayNoise());
        GameObject go = new GameObject("PageStateOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.layer = gameObject.layer;
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        m_overlay = go.GetComponent<RawImage>();
        m_overlay.raycastTarget = false;
        m_overlay.material = m_overlayMaterial;
        m_overlay.enabled = false;
        return true;
    }

    private void OnDestroy()
    {
        if (m_overlayMaterial != null) Destroy(m_overlayMaterial);
    }

    private void ClearImages()
    {
        foreach (GameObject imageObject in m_imageObjects)
        {
            if (imageObject == null)
            {
                continue;
            }

            // Destroyの実行前に表示を止めます
            imageObject.SetActive(false);
            Destroy(imageObject);
        }

        m_imageObjects.Clear();
    }
}