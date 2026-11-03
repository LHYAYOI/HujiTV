using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace hatake
{
    /// <summary>ページの角のどちらを持つか</summary>
    public enum BookFlipCorner { Bottom, Top }

    /// <summary>片ページ（または見開き両面）を別の内容へ置き換える演出</summary>
    public enum BookPageEffect { None, Burn, Revive }

    /// <summary>本の開閉。FrontCover = 表紙を上に閉じている、BackCover = 裏表紙を上に閉じている</summary>
    public enum BookCoverState { Open, FrontCover, BackCover }

    /// <summary>
    /// 本文の紙の仕上げ。シェーダーのバリアント（_PAPERFINISH_MATTE / _PAPERFINISH_GLOSS）を切り替える。
    /// FromMaterial = マテリアルの Paper Finish のまま
    /// </summary>
    public enum BookPaperFinish { FromMaterial, Matte, Gloss }

    /// <summary>
    /// 暗転中に明るく残す範囲。area は見開き全体を0〜1とした範囲（左下原点）。
    /// cornerRadius / feather はページ幅 = 1 の長さ。
    /// </summary>
    [Serializable]
    public struct BookFlipSpot
    {
        public enum SpotShape { Ellipse, Rect }

        public Rect area;
        public SpotShape shape;
        [Min(0)] public float cornerRadius;
        [Min(0.0001f)] public float feather;
        [Range(0, 1)] public float strength;

        /// <summary>片ページ内のUV範囲（0〜1）から作る</summary>
        public static BookFlipSpot OnPage(bool rightPage, Rect pageArea,
            SpotShape shape = SpotShape.Rect, float feather = 0.04f, float cornerRadius = 0.03f)
        {
            return new BookFlipSpot
            {
                area = new Rect((rightPage ? 0.5f : 0f) + pageArea.x * 0.5f, pageArea.y,
                    pageArea.width * 0.5f, pageArea.height),
                shape = shape,
                feather = feather,
                cornerRadius = cornerRadius,
                strength = 1f
            };
        }

        public static BookFlipSpot WholePage(bool rightPage, float feather = 0.04f) =>
            OnPage(rightPage, new Rect(0, 0, 1, 1), SpotShape.Rect, feather, 0f);
    }

    /// <summary>
    /// 撮影済みの見開きRenderTextureを Book/Page Flip Composite で合成し、
    /// ページめくり・本の装飾・暗転スポットを出力RenderTextureへ描く。
    /// BookController の m_flipView に設定すると、めくり表示を担当する。
    /// 単体でも ShowSpread / PlayTurn / BeginDrag 系を呼べば使える。
    /// </summary>
    public sealed class BookFlipView : MonoBehaviour
    {
        public const int MaxSpots = 8;

        [Header("出力")]
        [SerializeField] private Shader m_shader;
        [Tooltip("見た目の調整値を持つマテリアル。未設定ならシェーダーの既定値。実行時は複製して使い、元のアセットは書き換えない")]
        [SerializeField] private Material m_material;
        [Tooltip("未設定なら Output Size で作成")]
        [SerializeField] private RenderTexture m_outputTexture;
        [Tooltip("表示先（BookController の Display Image なら自動）。縦横比をこれに合わせ、ページが伸びないようにする")]
        [SerializeField] private RawImage m_display;
        [Tooltip("作成する出力の大きさ。表示先がある場合は高さだけ使い、幅は表示先の縦横比に合わせる")]
        [SerializeField] private Vector2Int m_outputSize = new Vector2Int(1600, 1000);
        [Tooltip("ON：出力の高さを表示先の実際の画素数に合わせる（Output Size の高さは使わない）。" +
                 "出力を縮小して表示すると細かな模様と文字がちらつくので、通常はON")]
        [SerializeField] private bool m_fitDisplayResolution = true;
        [Tooltip("ONで出力をHDRのまま渡す（トーンマップしない）。別のカメラのBloomで光らせる場合用。通常はOFFで、下の Bloom を使う")]
        [SerializeField] private bool m_hdrOutput;

        [Header("負荷（モバイル向け）")]
        [Tooltip("ON：見た目に関わる値が変わったフレームだけ描き直す。静止している間は前の絵をそのまま見せ、GPU を使わない。" +
                 "めくり・燃焼・復活・暗転・魔法の縁取りの間は毎フレーム描く。OFF：毎フレーム描く（従来）")]
        [SerializeField] private bool m_redrawOnlyOnChange = true;
        [Tooltip("静止中も、時間で動く模様（表紙の金具・金の罫の光）を描き直す回数（毎秒）。0 で静止中は描かない")]
        [SerializeField, Min(0)] private float m_idleRefreshRate;
        [Tooltip("モバイルで、暗転・魔法の縁取りなど時間でゆっくり動く演出の間に描き直す回数（毎秒）。0 で毎フレーム。" +
                 "めくり・燃焼・復活・フェードは常に毎フレーム")]
        [SerializeField, Min(0)] private float m_ambientRefreshRateMobile = 30f;
        [Tooltip("出力（合成）の高さの上限（画素）。0 で上限なし。表示先より小さいと拡大して表示する（ちらつきは出ない）")]
        [SerializeField, Min(0)] private int m_maxOutputHeight;
        [Tooltip("モバイル（Android / iOS）での出力の高さの上限（画素）。合成シェーダーの負荷は画素数に比例する")]
        [SerializeField, Min(0)] private int m_maxOutputHeightMobile = 720;
        [Tooltip("モバイルでの Bloom の段数の上限。段ごとに描画の切り替えが1回増える")]
        [SerializeField, Range(1, 8)] private int m_bloomIterationsMobile = 3;
        [Tooltip("ON：エディターでもモバイルの設定（出力の上限・Bloom の段数）を使う。実機の見た目の確認用")]
        [SerializeField] private bool m_forceMobileSettings;

        [Header("HDR・Bloom（本の中で完結。Overlay UIにはカメラのBloomが効かないため）")]
        [Tooltip("Hidden/Book/Page Flip Post。未設定なら Bloom・ぼかし無しでそのまま出力")]
        [SerializeField] private Shader m_postShader;
        [SerializeField, Min(0)] private float m_bloomIntensity = 0.9f;
        [Tooltip("これより明るい部分が光る（1 = 白）")]
        [SerializeField, Min(0)] private float m_bloomThreshold = 0.95f;
        [SerializeField, Range(0, 1)] private float m_bloomKnee = 0.5f;
        [Tooltip("光の広がり")]
        [SerializeField, Range(0, 1)] private float m_bloomScatter = 0.7f;
        [SerializeField, Range(1, 8)] private int m_bloomIterations = 6;
        [SerializeField, Min(1)] private float m_bloomClamp = 40f;
        [SerializeField] private Color m_bloomTint = Color.white;
        [SerializeField, Min(0)] private float m_exposure = 1f;
        [Tooltip("明るすぎる部分だけを丸める（0.8までは色を変えない）")]
        [SerializeField] private bool m_tonemap = true;
        [Tooltip("暗転中、スポットの外側をぼかす量")]
        [SerializeField, Range(0, 1)] private float m_dimBlur = 0.85f;

        [Header("レイアウト")]
        [Tooltip("出力画像の中で見開きページを置ける最大範囲 (xMin, yMin, xMax, yMax)。ページの縦横比に合わせて中央に収める")]
        [SerializeField] private Vector4 m_bookArea = new Vector4(0.06f, 0.08f, 0.94f, 0.92f);
        [Tooltip("撮影RenderTexture内の左ページ範囲 XYWH（BookModelAtlas の Left Crop と同じ）")]
        [SerializeField] private Vector4 m_leftCrop = new Vector4(0.1125f, 0.1f, 0.3875f, 0.8f);
        [Tooltip("撮影RenderTexture内の右ページ範囲 XYWH")]
        [SerializeField] private Vector4 m_rightCrop = new Vector4(0.5f, 0.1f, 0.3875f, 0.8f);
        [Tooltip("0なら撮影RenderTextureとCropから自動計算（ページ高さ / ページ幅）")]
        [SerializeField, Min(0)] private float m_pageAspectOverride;

        [Header("めくり")]
        [SerializeField, Min(0.05f)] private float m_turnDuration = 0.75f;
        [SerializeField] private AnimationCurve m_turnCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("ボタン・スワイプでめくるときに持つ角")]
        [SerializeField] private BookFlipCorner m_autoCorner = BookFlipCorner.Bottom;
        [Tooltip("自動めくりで角が持ち上がる高さ（ページ幅比）")]
        [SerializeField, Range(0, 1)] private float m_autoLift = 0.12f;
        [Tooltip("ドラッグを離した後、残りを動かす速さ（ページ幅/秒）")]
        [SerializeField, Min(0.1f)] private float m_releaseSpeed = 4f;
        [Tooltip("この速さ（ページ幅/秒）以上で背の方へ払うと、半分に届かなくてもめくる")]
        [SerializeField, Min(0)] private float m_flickSpeed = 2.5f;

        [Header("表紙（閉じた本）")]
        [Tooltip("表紙の絵。閉じた本の板全体（縁を含む）に貼る。未設定なら革")]
        [SerializeField] private Texture m_frontCover;
        [Tooltip("裏表紙の絵。未設定なら革")]
        [SerializeField] private Texture m_backCover;

        [Tooltip("表紙・裏表紙は曲がらず、背を軸に板ごと回る")]
        [SerializeField] private bool m_rigidCovers = true;

        [Header("燃焼・復活")]
        [SerializeField, Min(0.1f)] private float m_burnDuration = 1.9f;
        [SerializeField, Min(0.1f)] private float m_reviveDuration = 1.7f;

        [Header("見た目（状態で変わる値）")]
        [Tooltip("めくり中に折り目へ乗る魔法の光")]
        [SerializeField, Min(0)] private float m_turnGlow = 0.8f;
        [Tooltip("全ページを閉じた側に積んだときの小口の厚み（ページ幅比）")]
        [SerializeField, Range(0, 0.1f)] private float m_stackThickness = 0.035f;
        [SerializeField, Range(0, 0.1f)] private float m_minStackThickness = 0.006f;
        [Tooltip("紙一枚（見開き1つ分）ごとに増える小口の厚み。残りが少ないと薄く、多いと Stack Thickness で頭打ち")]
        [SerializeField, Range(0, 0.02f)] private float m_leafThickness = 0.0025f;
        [SerializeField, Range(1, 40)] private int m_stackLayers = 14;
        [Tooltip("小口が下へずれる割合（斜め上から見た厚み）")]
        [SerializeField, Range(0, 2)] private float m_stackDrop = 0.6f;

        [Header("本の姿勢（ライティング）")]
        [Tooltip("本の姿勢（オイラー角、度。Unity の向き）。姿勢 0 で本は正面（カメラ側、-Z）を向いている。" +
                 "光の向き（マテリアルの Light Direction）は一定のまま、本から見た光と目の向きが変わり、明暗・照り返し・映り込みが変わる")]
        [SerializeField] private Vector3 m_bookPose;

        [Header("紙の仕上げ")]
        [Tooltip("FromMaterial：マテリアルの Paper Finish のまま。Matte / Gloss：実行時にシェーダーのバリアントを切り替える")]
        [SerializeField] private BookPaperFinish m_paperFinish = BookPaperFinish.FromMaterial;

        [Header("画面フェード")]
        [Tooltip("0 = 通常の表示、1 = 出力全体が Fade Color（マテリアル）で塗りつぶされる。FadeScreen で時間をかけて動かせる")]
        [SerializeField, Range(0, 1)] private float m_screenFade;

        private static readonly int s_curLeftTex = Shader.PropertyToID("_CurLeftTex");
        private static readonly int s_curRightTex = Shader.PropertyToID("_CurRightTex");
        private static readonly int s_dstLeftTex = Shader.PropertyToID("_DstLeftTex");
        private static readonly int s_dstRightTex = Shader.PropertyToID("_DstRightTex");
        private static readonly int s_curLeftRect = Shader.PropertyToID("_CurLeftRect");
        private static readonly int s_curRightRect = Shader.PropertyToID("_CurRightRect");
        private static readonly int s_dstLeftRect = Shader.PropertyToID("_DstLeftRect");
        private static readonly int s_dstRightRect = Shader.PropertyToID("_DstRightRect");
        private static readonly int s_bookRect = Shader.PropertyToID("_BookRect");
        private static readonly int s_pageAspect = Shader.PropertyToID("_PageAspect");
        private static readonly int s_turnSide = Shader.PropertyToID("_TurnSide");
        private static readonly int s_turning = Shader.PropertyToID("_Turning");
        private static readonly int s_fold = Shader.PropertyToID("_Fold");
        private static readonly int s_turnGlowId = Shader.PropertyToID("_TurnGlow");
        private static readonly int s_stack = Shader.PropertyToID("_Stack");
        private static readonly int s_dim = Shader.PropertyToID("_Dim");
        private static readonly int s_screenFade = Shader.PropertyToID("_ScreenFade");
        private static readonly int s_fadeColor = Shader.PropertyToID("_FadeColor");
        private static readonly int s_paperFinish = Shader.PropertyToID("_PaperFinish");
        private static readonly int s_bookPoseInv = Shader.PropertyToID("_BookPoseInv");
        private static readonly int s_pageLod = Shader.PropertyToID("_PageLod");
        private static readonly int s_prefilterSpread = Shader.PropertyToID("_PrefilterSpread");
        private static readonly int s_coverLod = Shader.PropertyToID("_CoverLod");
        private static readonly int s_pagePixels = Shader.PropertyToID("_PagePixels");
        private static readonly int s_spotCount = Shader.PropertyToID("_SpotCount");
        private static readonly int s_spots = Shader.PropertyToID("_Spots");
        private static readonly int s_spotParams = Shader.PropertyToID("_SpotParams");
        private static readonly int s_fxMode = Shader.PropertyToID("_FxMode");
        private static readonly int s_fxSide = Shader.PropertyToID("_FxSide");
        private static readonly int s_fxProgress = Shader.PropertyToID("_FxProgress");
        private static readonly int s_fxOrigin = Shader.PropertyToID("_FxOrigin");
        private static readonly int s_mainTex = Shader.PropertyToID("_MainTex");
        private static readonly int s_higherTex = Shader.PropertyToID("_HigherTex");
        private static readonly int s_bloomTex = Shader.PropertyToID("_BloomTex");
        private static readonly int s_blurTex = Shader.PropertyToID("_BlurTex");
        private static readonly int s_bloomThreshold = Shader.PropertyToID("_BloomThreshold");
        private static readonly int s_bloomTint = Shader.PropertyToID("_BloomTint");
        private static readonly int s_dimBlur = Shader.PropertyToID("_DimBlur");
        private static readonly int s_tonemap = Shader.PropertyToID("_Tonemap");
        private static readonly int s_exposure = Shader.PropertyToID("_Exposure");
        private static readonly int s_lightDir = Shader.PropertyToID("_LightDir");
        private const int PassPrefilter = 0, PassDown = 1, PassUpAdd = 2, PassUp = 3, PassComposite = 4;
        private static readonly int s_rigid = Shader.PropertyToID("_Rigid");
        private static readonly int s_rigidCover = Shader.PropertyToID("_RigidCover");
        private static readonly int s_slotKinds = Shader.PropertyToID("_SlotKinds");
        private static readonly int s_frontCoverTex = Shader.PropertyToID("_FrontCoverTex");
        private static readonly int s_backCoverTex = Shader.PropertyToID("_BackCoverTex");
        private static readonly int s_coverFlags = Shader.PropertyToID("_CoverFlags");
        private static readonly int s_boardMode = Shader.PropertyToID("_BoardMode");
        private static readonly int s_boardOpen = Shader.PropertyToID("_BoardOpen");
        private static readonly int s_magicColor = Shader.PropertyToID("_MagicColor");
        private static readonly int s_paperTint = Shader.PropertyToID("_PaperTint");
        private static readonly int s_magicStrength = Shader.PropertyToID("_MagicStrength");

        private Material m_runtimeMaterial;
        private Material m_postMaterial;
        private RenderTexture m_compositeTexture;
        private RenderTexture[] m_bloomMips = new RenderTexture[8];
        private bool m_hasLightDirection;
        private Vector3 m_lightDirection;
        private bool m_ownsOutput;
        private bool m_displayFlag;
        private Texture m_current;
        private Texture m_destination;
        private BookCoverState m_currentCover;
        private BookCoverState m_destinationCover;
        // 撮影テクスチャごとの「左が表紙の内側／右が裏表紙の内側」
        private readonly Dictionary<Texture, Vector2> m_coverInsides = new Dictionary<Texture, Vector2>();
        private Vector4 m_bookRect;
        private float m_pageAspect = 1.2f;

        private bool m_turning;
        private float m_turnSide = 1f;
        private BookFlipCorner m_corner;
        private Vector2 m_foldPoint;
        private float m_turnAmount;

        private bool m_dragPending;
        private bool m_dragReleased;
        private float m_dragSide;
        private BookFlipCorner m_dragCorner;
        private Vector2 m_dragPointer;
        private Vector2 m_dragOffset;
        private float m_dragVelocity;

        private BookPageEffect m_effect;
        private float m_effectSide = 1f;
        private float m_effectProgress;
        private Vector2 m_effectOrigin;
        private float m_effectSeed;

        private float m_readingProgress = 0.5f;
        private int m_progressIndex;
        private int m_progressCount;   // 0 = 枚数が未設定（進み具合の割合で厚みを決める）
        private float m_dim;
        private Coroutine m_dimRoutine;
        private Coroutine m_screenFadeRoutine;
        // 描き直しの判定：前回描いたときの状態の要約。MarkDirty で m_contentVersion を進める
        private Hash128 m_lastSignature;
        private bool m_hasRendered;
        private int m_contentVersion;
        private float m_nextIdleRefresh;
        private float m_nextAmbientRefresh;
        private int m_renderCount;
        private UnityEngine.Rendering.LocalKeyword m_glossKeyword;
        private UnityEngine.Rendering.LocalKeyword m_matteKeyword;
        private bool m_hasPaperKeywords;
        private readonly BookFlipSpot?[] m_spots = new BookFlipSpot?[MaxSpots];
        private readonly Vector4[] m_spotData = new Vector4[MaxSpots];
        private readonly Vector4[] m_spotParamData = new Vector4[MaxSpots];
        private bool m_hasMagicStyle;
        private Color m_magicColor, m_paperTint;
        private float m_magicStrength;
        private Color m_baseMagicColor, m_basePaperTint;
        private float m_baseMagicStrength;

        public RenderTexture OutputTexture => m_outputTexture;
        public bool IsTurning => m_turning;
        public BookCoverState CoverState => m_currentCover;
        public Texture FrontCover => m_frontCover;
        public Texture BackCover => m_backCover;
        public bool IsPlayingEffect => m_effect != BookPageEffect.None;
        public float BurnDuration => m_burnDuration;
        public float ReviveDuration => m_reviveDuration;
        /// <summary>直前の PlayTurn が最後までめくれたか。ドラッグを戻したときは false</summary>
        public bool LastTurnCommitted { get; private set; }
        /// <summary>めくりの進み具合 0〜1（角が背を越えると0.5）</summary>
        public float TurnAmount => m_turnAmount;
        public float PageAspect => m_pageAspect;
        /// <summary>撮影テクスチャ内の左ページの範囲（XYWH、0〜1）。ページUV → 撮影カメラの Viewport 座標の変換に使う</summary>
        public Vector4 LeftCrop => m_leftCrop;
        /// <summary>撮影テクスチャ内の右ページの範囲（XYWH、0〜1）</summary>
        public Vector4 RightCrop => m_rightCrop;
        public float Dim => m_dim;

        // ------------------------------------------------------------------
        // 初期化と表示
        // ------------------------------------------------------------------
        /// <summary>表示先を設定（縦横比を合わせる）。所有している出力は作り直し、表示先のテクスチャも差し替える</summary>
        public void SetDisplay(RawImage display)
        {
            m_display = display;
            MatchDisplayAspect();
        }

        private float DisplayAspect
        {
            get
            {
                if (m_display == null) return 0f;
                Rect rect = m_display.rectTransform.rect;
                return rect.width > 1f && rect.height > 1f ? rect.width / rect.height : 0f;
            }
        }

        private void MatchDisplayAspect()
        {
            float aspect = DisplayAspect;
            if (aspect <= 0f || !m_ownsOutput || m_outputTexture == null) return;
            int height = Mathf.Max(16, m_outputSize.y);
            // 表示先の実際の画素数に合わせる（縮小して表示しない）
            if (m_fitDisplayResolution)
            {
                Canvas canvas = m_display.canvas;
                float scale = canvas != null ? canvas.scaleFactor : 1f;
                height = Mathf.Clamp(Mathf.RoundToInt(m_display.rectTransform.rect.height * scale), 16, 4096);
            }
            // 合成の画素数を抑える（モバイルは既定 720）。表示では拡大されるだけなので、ちらつきは出ない
            int maxHeight = UsesMobileSettings ? m_maxOutputHeightMobile : m_maxOutputHeight;
            if (maxHeight > 0) height = Mathf.Min(height, Mathf.Max(16, maxHeight));
            int width = Mathf.Clamp(Mathf.RoundToInt(height * aspect), 16, 8192);
            if (m_outputTexture.width == width && m_outputTexture.height == height) return;
            RenderTexture old = m_outputTexture;
            m_outputTexture = CreateOutput(width, height);
            if (m_display != null && m_display.texture == old) m_display.texture = m_outputTexture;
            old.Release();
            ReleaseObject(old);
            UpdateLayout();
        }

        private RenderTexture CreateOutput(int width, int height) =>
            new RenderTexture(width, height, 0, m_hdrOutput ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.ARGB32)
            {
                name = "BookFlipOutput",
                hideFlags = HideFlags.HideAndDontSave
            };

        public bool Initialize()
        {
            if (m_runtimeMaterial != null && m_outputTexture != null) return true;
            Shader shader = m_material != null ? m_material.shader : m_shader;
            if (shader == null)
            {
                Debug.LogError("BookFlipViewのShaderかMaterialを設定", this);
                return false;
            }
            if (m_runtimeMaterial == null)
            {
                m_runtimeMaterial = m_material != null ? new Material(m_material) : new Material(shader);
                m_runtimeMaterial.name = "BookFlip (Runtime)";
                m_runtimeMaterial.hideFlags = HideFlags.HideAndDontSave;
                m_baseMagicColor = m_runtimeMaterial.GetColor(s_magicColor);
                m_basePaperTint = m_runtimeMaterial.GetColor(s_paperTint);
                m_baseMagicStrength = m_runtimeMaterial.GetFloat(s_magicStrength);
            }
            if (m_outputTexture == null)
            {
                m_outputTexture = CreateOutput(Mathf.Max(16, m_outputSize.x), Mathf.Max(16, m_outputSize.y));
                m_ownsOutput = true;
                MatchDisplayAspect();
            }
            if (m_postMaterial == null)
            {
                Shader post = m_postShader != null ? m_postShader : Shader.Find("Hidden/Book/Page Flip Post");
                if (post != null && post.isSupported)
                    m_postMaterial = new Material(post) { name = "BookFlipPost (Runtime)", hideFlags = HideFlags.HideAndDontSave };
            }
            UpdateLayout();
            return true;
        }

        /// <summary>紙の凹凸・ハイライトを照らす光の向き（Material の Light Direction を上書き）</summary>
        public void SetLightDirection(Vector3 direction)
        {
            m_hasLightDirection = direction.sqrMagnitude > 1e-6f;
            m_lightDirection = direction;
        }

        public void ClearLightDirection() => m_hasLightDirection = false;

        /// <summary>
        /// 撮影した見開きの左右が表紙・裏表紙の内側かを登録する（BookController が撮影のたびに呼ぶ）。
        /// 内側のページは紙ではなく、表紙の内側として描く
        /// </summary>
        public void SetCoverInsides(Texture spread, bool leftIsFrontInside, bool rightIsBackInside)
        {
            MarkDirty();
            if (spread == null) return;
            m_coverInsides[spread] = new Vector2(leftIsFrontInside ? 1f : 0f, rightIsBackInside ? 1f : 0f);
        }

        /// <summary>表紙・裏表紙の絵を差し替える（null で革）</summary>
        public void SetCovers(Texture front, Texture back)
        {
            m_frontCover = front;
            m_backCover = back;
        }

        public void ShowSpread(Texture spread) => ShowView(spread, BookCoverState.Open);

        /// <summary>閉じた本を表示する。spread は開いたときの見開き（ページの縦横比と、開くときの中身）</summary>
        public void ShowClosed(Texture spread, bool frontCover) =>
            ShowView(spread, frontCover ? BookCoverState.FrontCover : BookCoverState.BackCover);

        private void ShowView(Texture spread, BookCoverState cover)
        {
            if (!Initialize()) return;
            if (spread != null) m_current = spread;
            m_destination = m_current;
            m_currentCover = cover;
            m_destinationCover = cover;
            m_turning = false;
            m_turnAmount = 0f;
            m_displayFlag = true;
            UpdateLayout();
        }

        public void StopDisplay()
        {
            MarkDirty();
            m_displayFlag = false;
            m_turning = false;
            m_dragPending = false;
            m_effect = BookPageEffect.None;
        }

        /// <summary>閉じた側の小口の厚みを読書の進み具合（0〜1）で決める</summary>
        public void SetReadingProgress(float progress01)
        {
            m_readingProgress = Mathf.Clamp01(progress01);
        }

        /// <summary>
        /// 小口の厚みを左右の紙の枚数で決める（左 = spreadIndex 枚、右 = 残り）。
        /// 残りが1枚なら薄く、めくっている間は前後の厚みの間を動く
        /// </summary>
        public void SetReadingProgress(int spreadIndex, int spreadCount)
        {
            m_progressCount = Mathf.Max(1, spreadCount);
            m_progressIndex = Mathf.Clamp(spreadIndex, 0, m_progressCount - 1);
            SetReadingProgress(spreadCount <= 1 ? 0.5f : spreadIndex / (float)(spreadCount - 1));
        }

        // 片側の小口の厚み。表紙・裏表紙の内側だけの側（ページが無い）は 0
        private float StackThickness(int spreadIndex, Texture spread, bool rightSide)
        {
            if (spread != null && m_coverInsides.TryGetValue(spread, out Vector2 inside) &&
                (rightSide ? inside.y : inside.x) > 0.5f) return 0f;
            if (m_progressCount <= 0)
                return Mathf.Lerp(m_minStackThickness, m_stackThickness, rightSide ? 1f - m_readingProgress : m_readingProgress);
            int leaves = rightSide ? m_progressCount - 1 - spreadIndex : spreadIndex;
            return Mathf.Min(m_stackThickness, m_minStackThickness + Mathf.Max(0, leaves) * m_leafThickness);
        }

        /// <summary>BookModelView と同じ引数。魔導書の縁取りと紙の色。strength = 0 でマテリアルの値へ戻す</summary>
        public void SetMagicStyle(Color primary, Color paper, float strength)
        {
            m_hasMagicStyle = strength > 0f;
            m_magicColor = primary;
            m_paperTint = paper;
            m_magicStrength = strength;
        }

        // ------------------------------------------------------------------
        // 自動めくり／ドラッグめくり
        // ------------------------------------------------------------------

        /// <summary>
        /// current から next へめくる。turnRightPage = true で右ページを左へ、false で左ページを右へ。
        /// 直前に BeginDrag した側と一致すれば指に追従し、離した位置と速さで完了／戻しを決める。
        /// </summary>
        public IEnumerator PlayTurn(Texture current, Texture next, bool turnRightPage) =>
            PlayTurn(current, BookCoverState.Open, next, BookCoverState.Open, turnRightPage);

        /// <summary>
        /// 表紙を開く／閉じるめくり。spread は開いている側の見開き。
        /// 表紙を開く：右（表紙）をめくると、表紙の裏に左ページが載って開く。閉じる：左ページをめくって表紙をかぶせる。
        /// 裏表紙は左右が逆。
        /// </summary>
        public IEnumerator PlayCoverTurn(Texture spread, BookCoverState cover, bool opening)
        {
            bool front = cover == BookCoverState.FrontCover;
            bool turnRight = front == opening;
            return opening
                ? PlayTurn(spread, cover, spread, BookCoverState.Open, turnRight)
                : PlayTurn(spread, BookCoverState.Open, spread, cover, turnRight);
        }

        private IEnumerator PlayTurn(Texture current, BookCoverState currentCover,
            Texture next, BookCoverState nextCover, bool turnRightPage)
        {
            if (!Initialize()) yield break;
            m_current = current;
            m_destination = next;
            m_currentCover = currentCover;
            m_destinationCover = nextCover;
            m_displayFlag = true;
            m_turnSide = turnRightPage ? 1f : -1f;
            UpdateLayout();

            bool dragFlag = m_dragPending && Mathf.Approximately(m_dragSide, m_turnSide);
            m_dragPending = false;
            m_corner = dragFlag ? m_dragCorner : m_autoCorner;
            Vector2 origin = CornerPosition(m_corner);
            Vector2 goal = new Vector2(-1f, origin.y);
            SetFoldPoint(origin);
            m_turning = true;

            bool committed;
            if (!dragFlag)
            {
                float duration = Mathf.Max(0.05f, m_turnDuration);
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    SetFoldPoint(AutoPath(origin, m_turnCurve.Evaluate(elapsed / duration)));
                    yield return null;
                }
                committed = true;
            }
            else
            {
                while (!m_dragReleased)
                {
                    SetFoldPoint(m_dragPointer + m_dragOffset);
                    yield return null;
                    // 指を止めている間は払う速さを失う
                    m_dragVelocity *= Mathf.Exp(-Time.unscaledDeltaTime * 8f);
                }
                SetFoldPoint(m_dragPointer + m_dragOffset);
                committed = m_foldPoint.x < 0f || m_dragVelocity < -m_flickSpeed;
                Vector2 start = m_foldPoint;
                Vector2 end = committed ? goal : origin;
                float duration = Mathf.Max(0.05f, Vector2.Distance(start, end) / m_releaseSpeed);
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    SetFoldPoint(Vector2.Lerp(start, end, k));
                    yield return null;
                }
            }

            m_turning = false;
            LastTurnCommitted = committed;
            if (committed)
            {
                // ページをめくり終えた時点で枚数を進める（BookController が後で同じ値を設定する。1フレームの厚みの跳びを防ぐ）
                if (currentCover == BookCoverState.Open && nextCover == BookCoverState.Open && m_progressCount > 0)
                    m_progressIndex = Mathf.Clamp(m_progressIndex + (turnRightPage ? 1 : -1), 0, m_progressCount - 1);
                ShowView(next, nextCover);
            }
            else ShowView(current, currentCover);
        }

        /// <summary>
        /// 片ページを current から next の内容へ置き換える演出。origin（本の座標）から境界が広がる。
        /// Burn：燃えた穴から next が見える。Revive：光の縁から next が組み上がる。
        /// 終了後は next を表示する。duration が0以下なら Burn/Revive Duration を使う。
        /// </summary>
        public IEnumerator PlayPageEffect(BookPageEffect effect, Texture current, Texture next,
            bool rightPage, Vector2 origin, float duration = 0f) =>
            PlayPageEffect(effect, current, next, rightPage ? 1 : -1, origin, duration);

        /// <summary>side：1 右ページ、-1 左ページ、0 見開き両面（origin から両ページへ広がる）</summary>
        public IEnumerator PlayPageEffect(BookPageEffect effect, Texture current, Texture next,
            int side, Vector2 origin, float duration = 0f)
        {
            if (!Initialize() || effect == BookPageEffect.None) yield break;
            m_current = current;
            m_destination = next;
            m_currentCover = BookCoverState.Open;
            m_destinationCover = BookCoverState.Open;
            m_displayFlag = true;
            m_turning = false;
            UpdateLayout();

            m_effect = effect;
            m_effectSide = Mathf.Clamp(side, -1, 1);
            float minX = side > 0 ? 0f : -1f;
            float maxX = side < 0 ? 0f : 1f;
            m_effectOrigin = new Vector2(Mathf.Clamp(origin.x, minX, maxX), Mathf.Clamp(origin.y, 0f, m_pageAspect));
            m_effectSeed = UnityEngine.Random.Range(0f, 100f);
            if (duration <= 0f) duration = effect == BookPageEffect.Burn ? m_burnDuration : m_reviveDuration;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                m_effectProgress = elapsed / duration;
                yield return null;
            }
            m_effectProgress = 1f;
            m_effect = BookPageEffect.None;
            ShowSpread(next);
        }

        /// <summary>
        /// ドラッグ開始。bookPoint は本の座標（UvToBook で変換）。
        /// この後に PlayTurn（BookController 経由なら ShowSpread）を呼ぶと指に追従する。
        /// corner を省略すると、つかんだ高さで上下の角を選ぶ。
        /// </summary>
        public void BeginDrag(Vector2 bookPoint, BookFlipCorner? corner = null)
        {
            m_dragSide = bookPoint.x >= 0f ? 1f : -1f;
            Vector2 point = ToTurnSpace(bookPoint, m_dragSide);
            m_dragCorner = corner ?? (point.y > m_pageAspect * 0.5f ? BookFlipCorner.Top : BookFlipCorner.Bottom);
            m_dragOffset = CornerPosition(m_dragCorner) - point;
            m_dragPointer = point;
            m_dragVelocity = 0f;
            m_dragReleased = false;
            m_dragPending = true;
        }

        public void UpdateDrag(Vector2 bookPoint)
        {
            if (!m_dragPending && !m_turning) return;
            Vector2 point = ToTurnSpace(bookPoint, m_dragSide);
            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            float velocity = (point.x - m_dragPointer.x) / deltaTime;
            m_dragVelocity = Mathf.Lerp(m_dragVelocity, velocity, 0.5f);
            m_dragPointer = point;
        }

        public void EndDrag()
        {
            m_dragReleased = true;
        }

        /// <summary>めくりが始まらなかったとき（端のページ・処理中）に呼ぶ</summary>
        public void CancelDrag()
        {
            m_dragPending = false;
            m_dragReleased = true;
        }

        /// <summary>出力画像のUV（0〜1）→ 本の座標（背 x=0、ページ幅1、下端 y=0）</summary>
        public Vector2 UvToBook(Vector2 uv)
        {
            Vector2 size = new Vector2(m_bookRect.z - m_bookRect.x, m_bookRect.w - m_bookRect.y);
            Vector2 t = new Vector2((uv.x - m_bookRect.x) / Mathf.Max(size.x, 1e-5f),
                (uv.y - m_bookRect.y) / Mathf.Max(size.y, 1e-5f));
            return new Vector2(t.x * 2f - 1f, t.y * m_pageAspect);
        }

        /// <summary>本の座標 → 見開き全体を0〜1としたUV（スポット指定と同じ単位）</summary>
        public Vector2 BookToSpreadUv(Vector2 bookPoint) =>
            new Vector2((bookPoint.x + 1f) * 0.5f, bookPoint.y / Mathf.Max(m_pageAspect, 1e-5f));

        public bool ContainsPage(Vector2 bookPoint) =>
            Mathf.Abs(bookPoint.x) <= 1f && bookPoint.y >= 0f && bookPoint.y <= m_pageAspect;

        /// <summary>
        /// 出力を表示している RectTransform 上のスクリーン座標 → 本の座標。
        /// eventCamera は Screen Space Overlay なら null。
        /// </summary>
        public bool ScreenToBook(RectTransform display, Vector2 screenPosition, Camera eventCamera, out Vector2 bookPoint)
        {
            bookPoint = default;
            if (display == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    display, screenPosition, eventCamera, out Vector2 local))
                return false;
            // Rect.PointToNormalized は0〜1に丸めるので使わない（枠外も扱う）
            Rect rect = display.rect;
            Vector2 uv = new Vector2((local.x - rect.xMin) / Mathf.Max(rect.width, 1e-5f),
                (local.y - rect.yMin) / Mathf.Max(rect.height, 1e-5f));
            bookPoint = UvToBook(uv);
            return true;
        }

        /// <summary>本の座標で中心と半サイズを指定するスポット（ページ幅 = 1）</summary>
        public BookFlipSpot SpotAt(Vector2 bookCenter, Vector2 halfSize,
            BookFlipSpot.SpotShape shape = BookFlipSpot.SpotShape.Ellipse, float feather = 0.06f)
        {
            Vector2 center = BookToSpreadUv(bookCenter);
            Vector2 size = new Vector2(halfSize.x, halfSize.y * 2f / Mathf.Max(m_pageAspect, 1e-5f));
            return new BookFlipSpot
            {
                area = new Rect(center - size * 0.5f, size),
                shape = shape,
                feather = feather,
                cornerRadius = Mathf.Min(halfSize.x, halfSize.y) * 0.3f,
                strength = 1f
            };
        }

        // ------------------------------------------------------------------
        // 暗転とスポット
        // ------------------------------------------------------------------
        public void SetDim(float amount)
        {
            if (m_dimRoutine != null) StopCoroutine(m_dimRoutine);
            m_dimRoutine = null;
            m_dim = Mathf.Clamp01(amount);
        }

        /// <summary>暗転量を duration 秒で target へ（timeScaleに依存しない）</summary>
        public Coroutine FadeDim(float target, float duration)
        {
            // 0 秒なら即座に（終わったコルーチンを残さない。残ると描き直しの判定が演出中のままになる）
            if (duration <= 0f)
            {
                SetDim(target);
                return null;
            }
            if (m_dimRoutine != null) StopCoroutine(m_dimRoutine);
            m_dimRoutine = StartCoroutine(FadeDimRoutine(Mathf.Clamp01(target), duration));
            return m_dimRoutine;
        }

        private IEnumerator FadeDimRoutine(float target, float duration)
        {
            float start = m_dim;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                m_dim = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }
            m_dim = target;
            m_dimRoutine = null;
        }

        // ------------------------------------------------------------------
        // 画面フェード（出力全体を Fade Color へ。0 通常 〜 1 完全に暗転）
        // ------------------------------------------------------------------
        public float ScreenFade => m_screenFade;

        /// <summary>合成を描き直した回数（計測用。静止中に増えなければ描画を止められている）</summary>
        public int RenderCount => m_renderCount;

        /// <summary>モバイル向けの設定（出力の上限・Bloom の段数）を使っているか</summary>
        public bool UsesMobileSettings => Application.isMobilePlatform || m_forceMobileSettings;

        /// <summary>
        /// 次のフレームで必ず描き直す。撮影テクスチャの中身だけが変わった（同じテクスチャへ撮り直した）ときなどに呼ぶ
        /// </summary>
        public void MarkDirty() => m_contentVersion++;

        /// <summary>本の姿勢（オイラー角、度）。外から毎フレーム渡してよい（例：本のオブジェクトの transform.eulerAngles）</summary>
        public Vector3 BookPose
        {
            get => m_bookPose;
            set => m_bookPose = value;
        }

        public void SetBookPose(Vector3 eulerAngles) => m_bookPose = eulerAngles;

        public void SetBookPose(Quaternion rotation) => m_bookPose = rotation.eulerAngles;

        // 世界の向き → 本の座標への回転をシェーダーへ渡す。
        // 本の座標は z が紙の表から手前（Unity の -Z）なので、Z を反転して Unity の回転をはさむ
        private void ApplyBookPose(Material material)
        {
            Matrix4x4 flipZ = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));
            Matrix4x4 inverse = Matrix4x4.Rotate(Quaternion.Inverse(Quaternion.Euler(m_bookPose)));
            material.SetMatrix(s_bookPoseInv, flipZ * inverse * flipZ);
        }

        /// <summary>本文の紙の仕上げ（マット／グロス）。毎フレーム、合成のマテリアルのキーワードに反映する</summary>
        public BookPaperFinish PaperFinish
        {
            get => m_paperFinish;
            set => m_paperFinish = value;
        }

        // 紙の仕上げのキーワード（BookFlipComposite の multi_compile_local_fragment）
        private void ApplyPaperFinish(Material material)
        {
            if (m_paperFinish == BookPaperFinish.FromMaterial) return;
            bool gloss = m_paperFinish == BookPaperFinish.Gloss;
            // キーワードは一度だけ引く（毎フレーム名前から探さない）
            if (!m_hasPaperKeywords)
            {
                m_glossKeyword = new UnityEngine.Rendering.LocalKeyword(material.shader, "_PAPERFINISH_GLOSS");
                m_matteKeyword = new UnityEngine.Rendering.LocalKeyword(material.shader, "_PAPERFINISH_MATTE");
                m_hasPaperKeywords = true;
            }
            material.SetKeyword(m_glossKeyword, gloss);
            material.SetKeyword(m_matteKeyword, !gloss);
            // マテリアルの KeywordEnum の値もそろえる（インスペクターの表示が食い違わないように）
            material.SetFloat(s_paperFinish, gloss ? 1f : 0f);
        }

        public void SetScreenFade(float amount)
        {
            if (m_screenFadeRoutine != null) StopCoroutine(m_screenFadeRoutine);
            m_screenFadeRoutine = null;
            m_screenFade = Mathf.Clamp01(amount);
        }

        /// <summary>画面フェードを duration 秒で target（0 通常 〜 1 暗転）へ（timeScaleに依存しない）</summary>
        public Coroutine FadeScreen(float target, float duration)
        {
            if (duration <= 0f)
            {
                SetScreenFade(target);
                return null;
            }
            if (m_screenFadeRoutine != null) StopCoroutine(m_screenFadeRoutine);
            m_screenFadeRoutine = StartCoroutine(FadeScreenRoutine(Mathf.Clamp01(target), duration));
            return m_screenFadeRoutine;
        }

        private IEnumerator FadeScreenRoutine(float target, float duration)
        {
            float start = m_screenFade;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                m_screenFade = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }
            m_screenFade = target;
            m_screenFadeRoutine = null;
        }

        public void SetSpot(int index, BookFlipSpot spot)
        {
            if (index < 0 || index >= MaxSpots) return;
            m_spots[index] = spot;
        }

        public void ClearSpot(int index)
        {
            if (index < 0 || index >= MaxSpots) return;
            m_spots[index] = null;
        }

        public void ClearSpots()
        {
            for (int i = 0; i < MaxSpots; i++) m_spots[i] = null;
        }

        // ------------------------------------------------------------------
        // 描画
        // ------------------------------------------------------------------
        private void LateUpdate()
        {
            if (!m_displayFlag || (m_current == null && m_currentCover == BookCoverState.Open) || !Initialize()) return;
            MatchDisplayAspect();
            UpdateLayout();
            // 見た目が前に描いたときと同じなら描かない（出力には前の絵が残っている）
            if (!NeedsRedraw()) return;
            m_renderCount++;
#if UNITY_EDITOR
            // 再生中にマテリアルアセットを調整したら、すぐ反映する
            if (m_material != null) m_runtimeMaterial.CopyPropertiesFromMaterial(m_material);
#endif
            ApplyProperties();
            if (m_postMaterial == null)
            {
                Graphics.Blit((Texture)null, m_outputTexture, m_runtimeMaterial, 0);
                return;
            }
            EnsureCompositeTexture();
            Graphics.Blit((Texture)null, m_compositeTexture, m_runtimeMaterial, 0);
            RenderPost();
        }

        // ------------------------------------------------------------------
        // 描き直しの判定
        // ------------------------------------------------------------------
        private bool NeedsRedraw()
        {
            if (!m_redrawOnlyOnChange) return true;
            // 時間で動く演出の間は毎フレーム描く。終わった次のフレームは必ず描き直す（ドラッグを戻した場合など、
            // 状態が始める前と同じでも、出力にはめくりの途中の絵が残っているため）
            if (m_turning || m_effect != BookPageEffect.None || m_dimRoutine != null || m_screenFadeRoutine != null)
            {
                m_hasRendered = false;
                return true;
            }
            // 暗転のルーン・魔法の縁取りの脈動はゆっくり動くので、モバイルでは回数を抑える
            float magic = m_hasMagicStyle ? m_magicStrength : m_baseMagicStrength;
            if (m_dim > 0f || magic > 0f)
            {
                m_hasRendered = false;
                if (!UsesMobileSettings || m_ambientRefreshRateMobile <= 0f) return true;
                if (Time.unscaledTime < m_nextAmbientRefresh) return false;
                m_nextAmbientRefresh = Time.unscaledTime + 1f / m_ambientRefreshRateMobile;
                return true;
            }
            Hash128 signature = ComputeSignature();
            bool redraw = !m_hasRendered || signature != m_lastSignature || !m_outputTexture.IsCreated() ||
                          (m_idleRefreshRate > 0f && Time.unscaledTime >= m_nextIdleRefresh);
            if (!redraw) return false;
            m_lastSignature = signature;
            m_hasRendered = true;
            if (m_idleRefreshRate > 0f) m_nextIdleRefresh = Time.unscaledTime + 1f / m_idleRefreshRate;
            return true;
        }

        // 見た目に関わる状態の要約。ここに無い値を変えたら MarkDirty を呼ぶ
        private Hash128 ComputeSignature()
        {
            Hash128 h = new Hash128();
            h.Append(m_contentVersion);
            h.Append(m_current != null ? m_current.GetInstanceID() : 0);
            h.Append(m_destination != null ? m_destination.GetInstanceID() : 0);
            h.Append(m_frontCover != null ? m_frontCover.GetInstanceID() : 0);
            h.Append(m_backCover != null ? m_backCover.GetInstanceID() : 0);
            h.Append(m_outputTexture != null ? m_outputTexture.GetInstanceID() : 0);
            h.Append((int)m_currentCover);
            h.Append((int)m_destinationCover);
            h.Append(m_turnSide);
            h.Append((int)m_corner);
            h.Append(ref m_foldPoint);
            h.Append(m_turnAmount);
            h.Append(ref m_bookRect);
            h.Append(m_pageAspect);
            h.Append(m_readingProgress);
            h.Append(m_progressIndex);
            h.Append(m_progressCount);
            h.Append(m_screenFade);
            h.Append(ref m_bookPose);
            h.Append((int)m_paperFinish);
            h.Append(m_hasLightDirection ? 1 : 0);
            h.Append(ref m_lightDirection);
            h.Append(m_hasMagicStyle ? 1 : 0);
            h.Append(ref m_magicColor);
            h.Append(ref m_paperTint);
            for (int i = 0; i < MaxSpots; i++)
            {
                if (m_spots[i] is not BookFlipSpot spot)
                {
                    h.Append(-1);
                    continue;
                }
                h.Append(ref spot.area);
                h.Append((int)spot.shape);
                h.Append(spot.cornerRadius);
                h.Append(spot.feather);
                h.Append(spot.strength);
            }
#if UNITY_EDITOR
            // エディターでマテリアルアセットを調整したら描き直す
            if (m_material != null) h.Append(UnityEditor.EditorUtility.GetDirtyCount(m_material));
#endif
            return h;
        }

        // アプリが裏から戻ると RenderTexture の中身が消えることがあるので描き直す
        private void OnApplicationPause(bool paused)
        {
            if (!paused) MarkDirty();
        }

        private void OnValidate() => MarkDirty();

        private void EnsureCompositeTexture()
        {
            if (m_compositeTexture != null &&
                m_compositeTexture.width == m_outputTexture.width && m_compositeTexture.height == m_outputTexture.height)
                return;
            if (m_compositeTexture != null) { m_compositeTexture.Release(); ReleaseObject(m_compositeTexture); }
            m_compositeTexture = new RenderTexture(m_outputTexture.width, m_outputTexture.height, 0, HdrFormat)
            {
                name = "BookFlipComposite (HDR)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private static RenderTextureFormat HdrFormat =>
            SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;

        // Bloom の縮小・拡大の画像は不透明度が要らないので、半分の大きさの HDR 形式（R11G11B10）を使う
        private static RenderTextureFormat BloomFormat =>
            SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float) ? RenderTextureFormat.RGB111110Float : HdrFormat;

        // HDRの合成結果 → Bloom（縮小・拡大の連鎖）・暗転時のぼかし → トーンマップして出力
        private void RenderPost()
        {
            Material post = m_postMaterial;
            RenderTexture source = m_compositeTexture;
            RenderTextureFormat format = HdrFormat;
            int width = Mathf.Max(1, source.width / 2);
            int height = Mathf.Max(1, source.height / 2);
            // Bloom：モバイルは 1/4 の大きさから始め、段数を抑える。縮小・拡大の画像は軽い形式
            bool mobile = UsesMobileSettings;
            RenderTextureFormat bloomFormat = BloomFormat;
            int bloomWidth = Mathf.Max(1, source.width / (mobile ? 4 : 2));
            int bloomHeight = Mathf.Max(1, source.height / (mobile ? 4 : 2));
            int iterations = mobile ? Mathf.Min(m_bloomIterations, m_bloomIterationsMobile) : m_bloomIterations;

            RenderTexture bloom = null;
            if (m_bloomIntensity > 0f)
            {
                post.SetVector(s_bloomThreshold, new Vector4(m_bloomThreshold, m_bloomKnee * Mathf.Max(m_bloomThreshold, 1e-3f),
                    m_bloomClamp, m_bloomScatter));
                post.SetFloat(s_prefilterSpread, mobile ? 1f : 0.5f);
                int count = 0;
                m_bloomMips[count] = RenderTexture.GetTemporary(bloomWidth, bloomHeight, 0, bloomFormat);
                Graphics.Blit(source, m_bloomMips[count++], post, PassPrefilter);
                for (int w = bloomWidth / 2, h = bloomHeight / 2; count < iterations && w >= 8 && h >= 8; w /= 2, h /= 2)
                {
                    m_bloomMips[count] = RenderTexture.GetTemporary(w, h, 0, bloomFormat);
                    Graphics.Blit(m_bloomMips[count - 1], m_bloomMips[count], post, PassDown);
                    count++;
                }
                for (int i = count - 2; i >= 0; i--)
                {
                    RenderTexture higher = m_bloomMips[i];
                    RenderTexture result = RenderTexture.GetTemporary(higher.width, higher.height, 0, bloomFormat);
                    post.SetTexture(s_higherTex, higher);
                    Graphics.Blit(m_bloomMips[i + 1], result, post, PassUpAdd);
                    RenderTexture.ReleaseTemporary(m_bloomMips[i + 1]);
                    RenderTexture.ReleaseTemporary(higher);
                    m_bloomMips[i + 1] = null;
                    m_bloomMips[i] = result;
                }
                bloom = m_bloomMips[0];
                m_bloomMips[0] = null;
            }

            RenderTexture blur = null;
            bool blurFlag = m_dimBlur > 0f && m_dim > 0.001f;
            if (blurFlag)
            {
                RenderTexture half = RenderTexture.GetTemporary(width, height, 0, format);
                RenderTexture quarter = RenderTexture.GetTemporary(Mathf.Max(1, width / 2), Mathf.Max(1, height / 2), 0, format);
                RenderTexture eighth = RenderTexture.GetTemporary(Mathf.Max(1, width / 4), Mathf.Max(1, height / 4), 0, format);
                Graphics.Blit(source, half, post, PassDown);
                Graphics.Blit(half, quarter, post, PassDown);
                Graphics.Blit(quarter, eighth, post, PassDown);
                Graphics.Blit(eighth, quarter, post, PassUp);
                Graphics.Blit(quarter, half, post, PassUp);
                RenderTexture.ReleaseTemporary(quarter);
                RenderTexture.ReleaseTemporary(eighth);
                blur = half;
            }

            post.SetTexture(s_bloomTex, bloom != null ? (Texture)bloom : Texture2D.blackTexture);
            post.SetTexture(s_blurTex, blur != null ? (Texture)blur : Texture2D.blackTexture);
            post.SetColor(s_bloomTint, new Color(m_bloomTint.r, m_bloomTint.g, m_bloomTint.b, bloom != null ? m_bloomIntensity : 0f));
            post.SetFloat(s_dimBlur, blurFlag ? m_dimBlur : 0f);
            post.SetFloat(s_tonemap, m_tonemap && !m_hdrOutput ? 1f : 0f);
            post.SetFloat(s_exposure, m_exposure);
            Graphics.Blit(source, m_outputTexture, post, PassComposite);

            if (bloom != null) RenderTexture.ReleaseTemporary(bloom);
            if (blur != null) RenderTexture.ReleaseTemporary(blur);
        }

        private void ApplyProperties()
        {
            UpdateLayout();
            Material material = m_runtimeMaterial;
            Texture destination = (m_turning || m_effect != BookPageEffect.None) && m_destination != null
                ? m_destination : m_current;
            material.SetTexture(s_curLeftTex, m_current);
            material.SetTexture(s_curRightTex, m_current);
            material.SetTexture(s_dstLeftTex, destination);
            material.SetTexture(s_dstRightTex, destination);
            material.SetVector(s_curLeftRect, m_leftCrop);
            material.SetVector(s_curRightRect, m_rightCrop);
            material.SetVector(s_dstLeftRect, m_leftCrop);
            material.SetVector(s_dstRightRect, m_rightCrop);
            BookCoverState destinationCover = m_turning ? m_destinationCover : m_currentCover;
            Vector2 currentKinds = SlotKinds(m_currentCover, m_current);
            Vector2 destinationKinds = SlotKinds(destinationCover, m_turning || m_effect != BookPageEffect.None ? m_destination : m_current);
            material.SetVector(s_slotKinds, new Vector4(currentKinds.x, currentKinds.y, destinationKinds.x, destinationKinds.y));
            material.SetTexture(s_frontCoverTex, m_frontCover != null ? m_frontCover : Texture2D.whiteTexture);
            material.SetTexture(s_backCoverTex, m_backCover != null ? m_backCover : Texture2D.whiteTexture);
            material.SetVector(s_coverFlags, new Vector4(m_frontCover != null ? 1f : 0f, m_backCover != null ? 1f : 0f, 0f, 0f));
            // 表紙が回るめくりは硬い板として描く
            bool coverTurn = m_turning && (m_currentCover != BookCoverState.Open || m_destinationCover != BookCoverState.Open);
            bool rigidTurn = coverTurn && m_rigidCovers;
            material.SetFloat(s_rigid, rigidTurn ? 1f : 0f);
            BookCoverState turningCover = m_currentCover != BookCoverState.Open ? m_currentCover : m_destinationCover;
            // 表紙の板：硬い表紙をめくっている間は、開いた本の板のうち動かない側だけを置く（表紙なら右、裏表紙なら左）。
            // 着地する側は回っている板だけが描くので、表紙が二重に見えない。開いた本と同じ色・罫・角金具で描き、
            // 小口と板の厚みは開き具合（_BoardOpen）に合わせて出し入れする（めくり終えた瞬間に現れない）。
            // 曲がる表紙（Rigid Covers OFF）は従来どおり、めくり終えるまで閉じた形のまま
            BookCoverState boardState = m_currentCover != BookCoverState.Open ? m_currentCover
                : m_turning ? m_destinationCover : m_currentCover;
            float boardMode = boardState == BookCoverState.FrontCover ? 1f : boardState == BookCoverState.BackCover ? -1f : 0f;
            if (rigidTurn) boardMode = turningCover == BookCoverState.BackCover ? -2f : 2f;
            material.SetFloat(s_boardMode, boardMode);
            float openAmount = !rigidTurn ? 1f
                : m_currentCover != BookCoverState.Open ? m_turnAmount : 1f - m_turnAmount;
            material.SetFloat(s_boardOpen, Mathf.Clamp01(openAmount));
            material.SetFloat(s_rigidCover, turningCover == BookCoverState.BackCover ? 2f : 1f);
            material.SetVector(s_bookRect, m_bookRect);
            material.SetFloat(s_pageAspect, m_pageAspect);
            ApplyResolution(material);
            ApplyPaperFinish(material);
            ApplyBookPose(material);
            material.SetFloat(s_turnSide, m_turnSide);
            material.SetFloat(s_turning, m_turning ? 1f : 0f);
            Vector2 origin = CornerPosition(m_corner);
            material.SetVector(s_fold, new Vector4(origin.x, origin.y, m_foldPoint.x, m_foldPoint.y));
            material.SetFloat(s_turnGlowId, m_turning ? m_turnGlow * Mathf.Sin(Mathf.PI * m_turnAmount) : 0f);

            // 小口の厚み：左右の紙の枚数から。ページをめくっている間は、めくる前と後の厚みの間を開き具合で動かす
            // （最後の1枚をめくるときに、めくり始めで右の束が急に消えたりしない）
            Texture openSpread = m_currentCover == BookCoverState.Open ? m_current : m_destination;
            float left = StackThickness(m_progressIndex, openSpread, false);
            float right = StackThickness(m_progressIndex, openSpread, true);
            bool pageTurn = m_turning && m_currentCover == BookCoverState.Open && m_destinationCover == BookCoverState.Open;
            if (pageTurn && m_progressCount > 0)
            {
                int destinationIndex = Mathf.Clamp(m_progressIndex + (m_turnSide > 0f ? 1 : -1), 0, m_progressCount - 1);
                left = Mathf.Lerp(left, StackThickness(destinationIndex, m_destination, false), m_turnAmount);
                right = Mathf.Lerp(right, StackThickness(destinationIndex, m_destination, true), m_turnAmount);
            }
            // 閉じた本は、すべてのページが表紙の下に重なる。ページの束を見せない
            // （硬い表紙をめくっている間は、シェーダーが開き具合に合わせて束を出し入れする）
            if (boardState != BookCoverState.Open && !rigidTurn) { left = 0f; right = 0f; }
            material.SetVector(s_stack, new Vector4(left, right, m_stackLayers, m_stackDrop));

            material.SetFloat(s_fxMode, (float)m_effect);
            material.SetFloat(s_fxSide, m_effectSide);
            material.SetFloat(s_fxProgress, m_effectProgress);
            float far = 0f;
            for (int i = 0; i < 4; i++)
            {
                float x0 = m_effectSide > 0.5f ? 0f : -1f;
                float x1 = m_effectSide < -0.5f ? 0f : 1f;
                Vector2 corner = new Vector2((i & 1) == 0 ? x0 : x1, (i & 2) == 0 ? 0f : m_pageAspect);
                far = Mathf.Max(far, Vector2.Distance(corner, m_effectOrigin));
            }
            material.SetVector(s_fxOrigin, new Vector4(m_effectOrigin.x, m_effectOrigin.y, far, m_effectSeed));

            material.SetFloat(s_dim, m_dim);
            // 画面フェードは最後の段で1回だけ掛ける：後処理があれば後処理、無ければ合成
            float screenFade = Mathf.Clamp01(m_screenFade);
            material.SetFloat(s_screenFade, m_postMaterial == null ? screenFade : 0f);
            if (m_postMaterial != null)
            {
                m_postMaterial.SetFloat(s_screenFade, screenFade);
                m_postMaterial.SetColor(s_fadeColor, material.GetColor(s_fadeColor));
            }
            int count = 0;
            for (int i = 0; i < MaxSpots; i++)
            {
                if (m_spots[i] is not BookFlipSpot spot) continue;
                Rect area = spot.area;
                Vector2 center = new Vector2(area.center.x * 2f - 1f, area.center.y * m_pageAspect);
                Vector2 halfSize = new Vector2(area.width, area.height * m_pageAspect * 0.5f);
                m_spotData[count] = new Vector4(center.x, center.y, halfSize.x, halfSize.y);
                m_spotParamData[count] = new Vector4(spot.shape == BookFlipSpot.SpotShape.Rect ? 1f : 0f,
                    spot.cornerRadius, Mathf.Max(spot.feather, 1e-4f), spot.strength);
                count++;
            }
            material.SetFloat(s_spotCount, count);
            material.SetVectorArray(s_spots, m_spotData);
            material.SetVectorArray(s_spotParams, m_spotParamData);
            if (m_hasLightDirection) material.SetVector(s_lightDir, m_lightDirection);
            if (m_postMaterial != null)
            {
                // 暗転ぼかしのスポット判定に使う
                m_postMaterial.SetVector(s_bookRect, m_bookRect);
                m_postMaterial.SetFloat(s_pageAspect, m_pageAspect);
                m_postMaterial.SetFloat(s_dim, m_dim);
                m_postMaterial.SetFloat(s_spotCount, count);
                m_postMaterial.SetVectorArray(s_spots, m_spotData);
                m_postMaterial.SetVectorArray(s_spotParams, m_spotParamData);
            }

            if (m_hasMagicStyle)
            {
                material.SetColor(s_magicColor, m_magicColor);
                material.SetColor(s_paperTint, m_paperTint);
                material.SetFloat(s_magicStrength, m_magicStrength);
            }
            else if (!Application.isEditor || m_material == null)
            {
                // エディターでマテリアルがあるときは、毎フレーム複製し直した値をそのまま使う
                material.SetColor(s_magicColor, m_baseMagicColor);
                material.SetColor(s_paperTint, m_basePaperTint);
                material.SetFloat(s_magicStrength, m_baseMagicStrength);
            }
        }

        // 縮小表示でのちらつき対策：出力でのページの画素数から、撮影テクスチャ・表紙の絵を読むミップの段と、
        // 紙の細かな模様をどこまで描くかを決める（シェーダーは分岐内で読むので、微分の代わりにこの値を使う）
        private void ApplyResolution(Material material)
        {
            float outWidth = m_outputTexture != null ? m_outputTexture.width : m_outputSize.x;
            float pagePixels = Mathf.Max(1f, (m_bookRect.z - m_bookRect.x) * 0.5f * outWidth);
            material.SetFloat(s_pagePixels, pagePixels);
            float pageTexels = m_current != null ? m_rightCrop.z * m_current.width : pagePixels;
            material.SetFloat(s_pageLod, Mathf.Max(0f, Mathf.Log(pageTexels / pagePixels, 2f)));
            Texture cover = m_frontCover != null ? m_frontCover : m_backCover;
            // 閉じた表紙の板の幅はページ幅の約 1.1 倍
            float coverLod = cover != null ? Mathf.Log(cover.width / (pagePixels * 1.1f), 2f) : 0f;
            material.SetFloat(s_coverLod, Mathf.Max(0f, coverLod));
        }

        // シェーダーの面の種類：0 ページ、1 表紙、2 裏表紙、3 何も無い（閉じた本の反対側）
        // 5 表紙の内側、6 裏表紙の内側（開いた見開きの左・右）
        private Vector2 SlotKinds(BookCoverState cover, Texture spread)
        {
            if (cover == BookCoverState.FrontCover) return new Vector2(3f, 1f);
            if (cover == BookCoverState.BackCover) return new Vector2(2f, 3f);
            if (spread != null && m_coverInsides.TryGetValue(spread, out Vector2 inside))
                return new Vector2(inside.x > 0.5f ? 5f : 0f, inside.y > 0.5f ? 6f : 0f);
            return Vector2.zero;
        }

        private void UpdateLayout()
        {
            float aspect = m_pageAspectOverride;
            if (aspect <= 0f && m_current != null && m_rightCrop.z > 0f)
                aspect = (m_rightCrop.w * m_current.height) / (m_rightCrop.z * m_current.width);
            if (aspect <= 0f) aspect = m_pageAspect;
            m_pageAspect = aspect;

            // 見開き（幅2・高さaspect）を Book Area の中央に縦横比を保って収める
            // 表示先の縦横比を優先（外から渡された出力が表示先と違う比率でも、ページが伸びない）
            float outW = m_outputTexture != null ? m_outputTexture.width : m_outputSize.x;
            float outH = m_outputTexture != null ? m_outputTexture.height : m_outputSize.y;
            float displayAspect = DisplayAspect;
            if (displayAspect > 0f) { outW = displayAspect; outH = 1f; }
            float areaW = (m_bookArea.z - m_bookArea.x) * outW;
            float areaH = (m_bookArea.w - m_bookArea.y) * outH;
            float scale = Mathf.Min(areaW / 2f, areaH / aspect);
            float w = scale * 2f / outW;
            float h = scale * aspect / outH;
            float cx = (m_bookArea.x + m_bookArea.z) * 0.5f;
            float cy = (m_bookArea.y + m_bookArea.w) * 0.5f;
            m_bookRect = new Vector4(cx - w * 0.5f, cy - h * 0.5f, cx + w * 0.5f, cy + h * 0.5f);
        }

        private Vector2 CornerPosition(BookFlipCorner corner) =>
            new Vector2(1f, corner == BookFlipCorner.Top ? m_pageAspect : 0f);

        private Vector2 ToTurnSpace(Vector2 bookPoint, float side) =>
            new Vector2(bookPoint.x * side, bookPoint.y);

        private Vector2 AutoPath(Vector2 origin, float progress)
        {
            // 角を背の上で半円状に運ぶ。両端で紙が平らになる
            float angle = Mathf.Clamp01(progress) * Mathf.PI;
            float lift = m_autoLift * Mathf.Sin(angle) * (m_corner == BookFlipCorner.Top ? -1f : 1f);
            return new Vector2(Mathf.Cos(angle), origin.y + lift);
        }

        private void SetFoldPoint(Vector2 point)
        {
            m_foldPoint = Constrain(point);
            m_turnAmount = Mathf.Clamp01((1f - m_foldPoint.x) * 0.5f);
        }

        // 紙は背から離れない：角は背の近い端から1、遠い端から対角線の長さより遠くへ行けない
        private Vector2 Constrain(Vector2 point)
        {
            bool top = m_corner == BookFlipCorner.Top;
            Vector2 near = new Vector2(0f, top ? m_pageAspect : 0f);
            Vector2 far = new Vector2(0f, top ? 0f : m_pageAspect);
            float diagonal = Mathf.Sqrt(1f + m_pageAspect * m_pageAspect);
            for (int i = 0; i < 2; i++)
            {
                Vector2 fromNear = point - near;
                if (fromNear.magnitude > 1f) point = near + fromNear.normalized;
                Vector2 fromFar = point - far;
                if (fromFar.magnitude > diagonal) point = far + fromFar.normalized * diagonal;
            }
            return point;
        }

        private void OnDisable()
        {
            StopDisplay();
            m_dimRoutine = null;
            m_screenFadeRoutine = null;
        }

        private void OnDestroy()
        {
            if (m_runtimeMaterial != null) ReleaseObject(m_runtimeMaterial);
            if (m_postMaterial != null) ReleaseObject(m_postMaterial);
            if (m_compositeTexture != null)
            {
                m_compositeTexture.Release();
                ReleaseObject(m_compositeTexture);
                m_compositeTexture = null;
            }
            if (m_ownsOutput && m_outputTexture != null)
            {
                m_outputTexture.Release();
                ReleaseObject(m_outputTexture);
                m_outputTexture = null;
            }
        }

        private static void ReleaseObject(UnityEngine.Object target)
        {
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
    }
}
