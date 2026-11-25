using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace hatake
{
    /// <summary>ページの角のどちらを持つか</summary>
    public enum BookFlipCorner { Bottom, Top }

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
                 "めくり・暗転・魔法の縁取りの間は毎フレーム描く。OFF：毎フレーム描く（従来）")]
        [SerializeField] private bool m_redrawOnlyOnChange = true;
        [Tooltip("静止中も、時間で動く模様（金の罫の光）を描き直す回数（毎秒）。0 で静止中は描かない")]
        [SerializeField, Min(0)] private float m_idleRefreshRate;
        [Tooltip("モバイルで、暗転・魔法の縁取りなど時間でゆっくり動く演出の間に描き直す回数（毎秒）。0 で毎フレーム。" +
                 "めくり・フェードは常に毎フレーム")]
        [SerializeField, Min(0)] private float m_ambientRefreshRateMobile = 30f;
        [Tooltip("出力（合成）の高さの上限（画素）。0 で上限なし。表示先より小さいと拡大して表示する（ちらつきは出ない）")]
        [SerializeField, Min(0)] private int m_maxOutputHeight;
        [Tooltip("モバイル（Android / iOS）での出力の高さの上限（画素）。合成シェーダーの負荷は画素数に比例する")]
        [SerializeField, Min(0)] private int m_maxOutputHeightMobile = 720;
        [Tooltip("モバイルでの Bloom の段数の上限。段ごとに描画の切り替えが1回増える")]
        [SerializeField, Range(1, 8)] private int m_bloomIterationsMobile = 3;
        [Tooltip("モバイルで、Bloom を2フレームに分けて求める（1フレーム目に縮小、2フレーム目に拡大。その間は前の結果を重ねる）。" +
                 "光のにじみが1〜2フレーム遅れる代わりに、めくり中や演出中の後処理が軽くなる")]
        [SerializeField] private bool m_bloomHalfRateMobile = true;
        [Tooltip("モバイルで、Bloom の最初の縮小（明るい部分の取り出し）を1点読みにする。" +
                 "細い光のにじみが少し粗くなる代わりに、後処理が数 ms 軽くなる")]
        [SerializeField] private bool m_bloomLightPrefilterMobile = true;
        [Tooltip("モバイルで、Bloom の縮小を 1/4 ずつにして段を減らす（取り出し → 1/16 → 拡大の3回の描画）。" +
                 "にじみが少し粗くなる代わりに、描画の切り替えが2回減る")]
        [SerializeField] private bool m_bloomSkipLevelMobile = true;
        [Tooltip("モバイルで、左右のページの中身が同時に変わり続けるとき（獲得の光など見開き全体の演出）は、" +
                 "1フレームに片側ずつ交互に描き直す（片側あたり毎秒30回）")]
        [SerializeField] private bool m_staggerPagesMobile = true;
        [Tooltip("ON：エディターでもモバイルの設定（出力の上限・Bloom の段数）を使う。実機の見た目の確認用")]
        [SerializeField] private bool m_forceMobileSettings;
        [Tooltip("モバイルで合成を面の種類ごとの小さなパスに分けて描く。1本の大きなシェーダーはモバイルの GPU で" +
                 "レジスタが足りずに極端に遅くなる（Adreno 610 で1回 500ms → 分割で数十ms）")]
        [SerializeField] private bool m_splitPassesMobile = true;
        [Tooltip("モバイルの本文のめくりで、動かない部分（今の見開き・本の外）をめくり始めの静止画から読む。" +
                 "毎フレーム描くのは移動先のページとめくった紙だけになる（めくりの間、表紙の金具の光などの動きは止まる）")]
        [SerializeField] private bool m_turnCacheMobile = true;
        [Tooltip("モバイルで、めくっていない・暗転していない見開きを、専用の小さなシェーダー（めくり・板の回転・暗転のコードを含まない）で描く。" +
                 "見た目は同じ。OFF にすると従来のシェーダーで描く（比較・切り分け用）")]
        [SerializeField] private bool m_stillVariantMobile = true;
        [Tooltip("ページの中身だけが変わったとき（線を引いている間など）、見開き全体を描き直さず、変わったページだけを軽いパスで更新する。" +
                 "OFF にすると、中身が変わるたびに見開き全体を描き直す（従来の動き）")]
        [SerializeField] private bool m_contentDelta = true;

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
        [Header("まとめてめくる束")]
        [Tooltip("束の厚み＝（めくる数−1）× Leaf Thickness × この倍率（Stack Thickness まで）")]
        [SerializeField, Range(0.5f, 8f)] private float m_bundleThicknessScale = 2f;
        [Tooltip("束の落ち影の広がり（束の厚みに対する倍率）")]
        [SerializeField, Range(0, 4)] private float m_bundleShadowGrow = 1.5f;
        [Tooltip("束の折り目のカールの広がり（束の厚みに対する倍率）")]
        [SerializeField, Range(0, 40)] private float m_bundleCurlGrow = 10f;

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
        private static readonly int s_turnThickness = Shader.PropertyToID("_TurnThickness");
        private static readonly int s_turnBandDrop = Shader.PropertyToID("_TurnBandDrop");
        private static readonly int s_dim = Shader.PropertyToID("_Dim");
        private static readonly int s_screenFade = Shader.PropertyToID("_ScreenFade");
        private static readonly int s_fadeColor = Shader.PropertyToID("_FadeColor");
        private static readonly int s_paperFinish = Shader.PropertyToID("_PaperFinish");
        private static readonly int s_bookPoseInv = Shader.PropertyToID("_BookPoseInv");
        private static readonly int s_pageLod = Shader.PropertyToID("_PageLod");
        private static readonly int s_prefilterSpread = Shader.PropertyToID("_PrefilterSpread");
        private static readonly int s_downSpread = Shader.PropertyToID("_DownSpread");
        private static readonly int s_coverLod = Shader.PropertyToID("_CoverLod");
        private static readonly int s_pagePixels = Shader.PropertyToID("_PagePixels");
        private static readonly int s_spotCount = Shader.PropertyToID("_SpotCount");
        private static readonly int s_spots = Shader.PropertyToID("_Spots");
        private static readonly int s_spotParams = Shader.PropertyToID("_SpotParams");
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


        private float m_readingProgress = 0.5f;
        private int m_progressIndex;
        private int m_turnLeaves = 1;     // 今のめくりで一緒にめくる見開きの数（1 = 従来の1枚）
        private int m_queuedTurnLeaves;   // 次の PlayTurn（3引数）でまとめてめくる数。0 = 指定なし
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
        /// <summary>直前の PlayTurn が最後までめくれたか。ドラッグを戻したときは false</summary>
        public bool LastTurnCommitted { get; private set; }

        // ------------------------------------------------------------------
        // ページめくりの禁止（BookPageState）
        //   ・表示中の見開きに暗転したページがある間
        //   ・どこかのページで獲得の演出をしている間
        // 禁止中に PlayTurn / PlayCoverTurn が呼ばれたら、めくらずに戻す（LastTurnCommitted = false。
        // ドラッグを戻したときと同じ扱いなので、どのコントローラーでも元の見開きのまま続く）
        // ------------------------------------------------------------------
        private bool m_spreadBlocksTurn;

        /// <summary>今ページをめくれないか（暗転したページが見開きにある・獲得の演出中）</summary>
        public bool TurnLocked => BookPageState.AnyAcquiring || m_spreadBlocksTurn;

        // 表示中の見開き（m_current）を撮っているページの表示から、めくりを止めるページがあるかを調べる。
        // めくりの準備で撮影先が次の見開きへ移っている間（どの表示も m_current を撮っていない）は、前の結果のまま
        private void UpdateTurnLock()
        {
            if (m_current == null || m_currentCover != BookCoverState.Open) { m_spreadBlocksTurn = false; return; }
            bool found = false, blocks = false;
            IReadOnlyList<BookPageView> views = BookPageView.ActiveViews;
            for (int i = 0; i < views.Count; i++)
            {
                BookPageView view = views[i];
                Camera camera = view != null ? view.CaptureCamera : null;
                if (camera == null || camera.targetTexture != m_current) continue;
                found = true;
                if (BookPageState.BlocksTurn(view.PageData)) { blocks = true; break; }
            }
            if (found) m_spreadBlocksTurn = blocks;
        }
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
            Vector2 value = new Vector2(leftIsFrontInside ? 1f : 0f, rightIsBackInside ? 1f : 0f);
            // 同じ指定のし直しでは描き直さない（めくれなかった見開きの撮影し直しなどで、毎回見開き全体を描き直さない）
            if (spread != null && m_coverInsides.TryGetValue(spread, out Vector2 old) && old == value) return;
            MarkDirty();
            if (spread == null) return;
            m_coverInsides[spread] = value;
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
            m_turnLeaves = 1;
            m_queuedTurnLeaves = 0; // 予約したまとめめくりが使われなかった：次の普通のめくりへ持ち越さない
            m_displayFlag = true;
            UpdateLayout();
        }

        public void StopDisplay()
        {
            MarkDirty();
            m_displayFlag = false;
            m_turning = false;
            m_turnLeaves = 1;
            m_queuedTurnLeaves = 0;
            m_dragPending = false;
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
        public IEnumerator PlayTurn(Texture current, Texture next, bool turnRightPage)
        {
            // QueueBundleTurn で予約されていれば、その数をまとめてめくる（1回だけ）
            int leaves = Mathf.Max(1, m_queuedTurnLeaves);
            m_queuedTurnLeaves = 0;
            return PlayTurn(current, BookCoverState.Open, next, BookCoverState.Open, turnRightPage, leaves);
        }

        /// <summary>
        /// leafCount 見開き分をまとめてめくる。何枚もの紙を描く代わりに、厚みのある1枚の束としてめくる
        /// （めくる紙の縁に束の小口を描き、左右の小口の厚みを枚数分動かす）。1 なら従来の PlayTurn と同じ。
        /// next は leafCount 先の見開き
        /// </summary>
        public IEnumerator PlayTurn(Texture current, Texture next, bool turnRightPage, int leafCount) =>
            PlayTurn(current, BookCoverState.Open, next, BookCoverState.Open, turnRightPage, leafCount);

        /// <summary>
        /// 次に呼ばれる PlayTurn（3引数）を leafCount 見開き分のまとめめくりにする。
        /// PlayTurn を直接呼べないコントローラーから使う（呼ぶ前に、next に leafCount 先の見開きを渡すこと）
        /// </summary>
        public void QueueBundleTurn(int leafCount) => m_queuedTurnLeaves = Mathf.Max(0, leafCount);

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
                ? PlayTurn(spread, cover, spread, BookCoverState.Open, turnRight, 1)
                : PlayTurn(spread, BookCoverState.Open, spread, cover, turnRight, 1);
        }

        private IEnumerator PlayTurn(Texture current, BookCoverState currentCover,
            Texture next, BookCoverState nextCover, bool turnRightPage, int leafCount)
        {
            if (!Initialize()) yield break;
            m_queuedTurnLeaves = 0;
            if (!m_turning) UpdateTurnLock();
            if (TurnLocked)
            {
                // めくれない：元の見開きのまま（ドラッグを戻したときと同じ）
                m_dragPending = false;
                LastTurnCommitted = false;
                ShowView(current, currentCover);
                yield break;
            }
            m_current = current;
            m_destination = next;
            m_currentCover = currentCover;
            m_destinationCover = nextCover;
            m_displayFlag = true;
            m_turnSide = turnRightPage ? 1f : -1f;
            // まとめてめくる数。表紙のめくりは1枚。残りの紙より多くはめくれない
            bool pagesOnly = currentCover == BookCoverState.Open && nextCover == BookCoverState.Open;
            int leaves = pagesOnly ? Mathf.Max(1, leafCount) : 1;
            if (pagesOnly && m_progressCount > 0)
                leaves = Mathf.Clamp(leaves, 1, Mathf.Max(1, turnRightPage ? m_progressCount - 1 - m_progressIndex : m_progressIndex));
            m_turnLeaves = leaves;
            UpdateLayout();

            bool dragFlag = m_dragPending && Mathf.Approximately(m_dragSide, m_turnSide);
            m_dragPending = false;
            m_corner = dragFlag ? m_dragCorner : m_autoCorner;
            Vector2 origin = CornerPosition(m_corner);
            Vector2 goal = new Vector2(-1f, origin.y);
            SetFoldPoint(origin);
            m_turning = true;
            m_turnSerial++; // めくりごとに静止画を作り直す

            bool committed;
            if (!dragFlag)
            {
                float duration = Mathf.Max(0.05f, m_turnDuration);
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    SetFoldPoint(AutoPath(origin, m_turnCurve.Evaluate(elapsed / duration)));
                    yield return null;
                }
                // 最後は必ずめくり終えた位置で1フレーム描く（モバイルはこの絵を次のめくりの静止画に使う）
                SetFoldPoint(AutoPath(origin, m_turnCurve.Evaluate(1f)));
                yield return null;
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
                SetFoldPoint(end);
                yield return null;
            }

            m_turning = false;
            int turnedLeaves = m_turnLeaves;
            m_turnLeaves = 1;
            LastTurnCommitted = committed;
            if (committed)
            {
                // ページをめくり終えた時点で枚数を進める（BookController が後で同じ値を設定する。1フレームの厚みの跳びを防ぐ）
                if (currentCover == BookCoverState.Open && nextCover == BookCoverState.Open && m_progressCount > 0)
                    m_progressIndex = Mathf.Clamp(m_progressIndex + (turnRightPage ? 1 : -1) * turnedLeaves, 0, m_progressCount - 1);
                ShowView(next, nextCover);
            }
            else ShowView(current, currentCover);
        }

        /// <summary>
        /// ドラッグ開始。bookPoint は本の座標（UvToBook で変換）。
        /// この後に PlayTurn（BookController 経由なら ShowSpread）を呼ぶと指に追従する。
        /// corner を省略すると、つかんだ高さで上下の角を選ぶ。
        /// </summary>
        public void BeginDrag(Vector2 bookPoint, BookFlipCorner? corner = null)
        {
            // めくれない間は指に追従させない（続く PlayTurn も戻す）
            if (TurnLocked) { m_dragPending = false; return; }
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
        public void MarkDirty()
        {
            m_contentVersion++;
            DebugMarkDirtyCount++;
        }

        // 計測用（BookPerfProbe）：描き直しの理由を調べる
        internal int DebugMarkDirtyCount, DebugContentDirtyCount;
        internal string DebugRedrawReason = "";
        private int m_debugVersion, m_debugCurrent;
        private Vector3 m_debugPose;
        private Vector4 m_debugRect;
        private float m_debugProgress;

        /// <summary>
        /// 表示中の見開きのページの中身だけが変わり、このフレームの撮影で撮り直されることを伝える（線を引いている間など、毎フレーム呼んでよい）。
        /// 次のフレームで、そのページだけを軽いパスで更新する。使えない状態（めくり中・暗転中・閉じた本など）では MarkDirty と同じ。
        /// rightPage：true 右ページ / false 左ページ / null 両方。
        /// BookPageView の下の Graphic の変化は自動で伝わるので、普通は呼ばなくてよい
        /// </summary>
        private int m_dirtyAgainFrame = -1;

        public void MarkContentDirty(bool? rightPage = null)
        {
            DebugContentDirtyCount++;
            if (!m_displayFlag || !CanContentDelta())
            {
                // 撮影はこのフレームの終わり：今描き直すと前の撮影の絵になることがあるので、次のフレームにもう1度描き直す
                MarkDirty();
                m_dirtyAgainFrame = Time.frameCount;
                return;
            }
            // 撮影はこのフレームの終わりなので、反映は次のフレーム
            if (m_dirtyNewFrame != Time.frameCount)
            {
                m_dirtyOldLeft |= m_dirtyNewLeft;
                m_dirtyOldRight |= m_dirtyNewRight;
                m_dirtyNewLeft = m_dirtyNewRight = false;
                m_dirtyNewFrame = Time.frameCount;
            }
            if (rightPage != true) m_dirtyNewLeft = true;
            if (rightPage != false) m_dirtyNewRight = true;
            m_contentDeltaWanted = true;
        }

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

        private static readonly int s_bookLightDirBS = Shader.PropertyToID("_BookLightDirBS");
        private static readonly int s_bookViewDirBS = Shader.PropertyToID("_BookViewDirBS");
        private static readonly int s_bookPoseShadeValue = Shader.PropertyToID("_BookPoseShadeValue");
        private static readonly int s_poseShading = Shader.PropertyToID("_PoseShading");

        // 画面全体で同じ値（本の座標での光・目の向き、姿勢の明暗）をここで求めて渡す。
        // シェーダーの BookLightDir / BookViewDir / BookPoseShade と同じ計算（画素ごとにしない）
        private void ApplyPoseLighting(Material material)
        {
            Quaternion toBook = Quaternion.Inverse(Quaternion.Euler(m_bookPose));
            Vector3 light = (Vector3)material.GetVector(s_lightDir);
            light = light.sqrMagnitude > 1e-8f ? light.normalized : Vector3.forward;
            Vector3 lightBook = ToBookSpace(toBook, light);
            Vector3 viewBook = ToBookSpace(toBook, new Vector3(0f, 0f, 1f));
            material.SetVector(s_bookLightDirBS, lightBook);
            material.SetVector(s_bookViewDirBS, viewBook);
            float k = Mathf.Clamp01(material.GetFloat(s_poseShading));
            float lit = 1f - k + k * Mathf.Clamp01(lightBook.z);
            float reference = 1f - k + k * Mathf.Clamp01(light.z);
            material.SetFloat(s_bookPoseShadeValue, Mathf.Clamp(lit / Mathf.Max(reference, 1e-3f), 0.25f, 2f));
        }

        // 本の座標は z が紙の表から手前（Unity の -Z）。ApplyBookPose の flipZ * inverse * flipZ と同じ回転
        private static Vector3 ToBookSpace(Quaternion toBook, Vector3 direction)
        {
            Vector3 flipped = new Vector3(direction.x, direction.y, -direction.z);
            Vector3 rotated = toBook * flipped;
            return new Vector3(rotated.x, rotated.y, -rotated.z).normalized;
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
            LateUpdateCore();
            // Bloom を前のフレームから使い回した後に描き直しが止まった：最後に1度だけ、今の絵の Bloom で後処理をやり直す
            if (m_bloomReused && m_postFrame != Time.frameCount)
            {
                m_bloomReused = false;
                RenderTexture source = m_postSource != null ? m_postSource : m_compositeTexture;
                if (m_displayFlag && m_postMaterial != null && m_outputTexture != null && source != null && source.IsCreated())
                {
                    m_postRect = null;
                    m_bloomForceFull = true;
                    RenderPost();
                    m_bloomForceFull = false;
                }
            }
        }

        private void LateUpdateCore()
        {
            if (!m_displayFlag || (m_current == null && m_currentCover == BookCoverState.Open) || !Initialize()) return;
            if (!m_turning) UpdateTurnLock();
            if (m_dirtyAgainFrame >= 0 && Time.frameCount > m_dirtyAgainFrame) { m_dirtyAgainFrame = -1; MarkDirty(); }
            MatchDisplayAspect();
            UpdateLayout();
            // 前のフレームまでに伝えられた「ページの中身だけの変化」（撮影は済んでいる）
            bool contentDirty = TakeContentDirty(out bool dirtyLeft, out bool dirtyRight);
            // 見た目が前に描いたときと同じなら描かない（出力には前の絵が残っている）
            if (!NeedsRedraw())
            {
                // 少しずつ描いている静止画の続き
                if (m_progressActive)
                {
                    // 中身の変化は、描き終えてから反映する（先に描いた帯は前の中身のまま）
                    if (contentDirty) HoldContentDirty(dirtyLeft, dirtyRight);
                    ApplyProperties();
                    ContinueProgress();
                    // 描いている途中も画面フェードは止めない（後処理だけで掛かる）
                    if (m_progressActive && m_postMaterial != null && !Mathf.Approximately(m_lastPostFade, m_screenFade))
                    {
                        m_lastPostFade = m_screenFade;
                        m_postMaterial.SetFloat(s_screenFade, Mathf.Clamp01(m_screenFade));
                        m_postRect = null;
                        RenderPost();
                    }
                    return;
                }
                // ページの中身だけが変わった：そのページだけを軽いパスで更新する（その準備も、止まっている間に進める）
                int delta = UpdateContentDelta(contentDirty, dirtyLeft, dirtyRight);
                if (delta == ContentDeltaApplied) return;
                // 表紙の開け閉めの移動先を、止まっている間に先に描いておく
                if (delta == ContentDeltaIdle) ContinueCoverPrerender();
                // 後処理があるときの画面フェードは後処理だけで掛かるので、合成は描き直さない
                if (m_postMaterial != null && m_compositeTexture != null && m_compositeTexture.IsCreated() &&
                    !Mathf.Approximately(m_lastPostFade, m_screenFade))
                {
                    m_lastPostFade = m_screenFade;
                    m_postMaterial.SetFloat(s_screenFade, Mathf.Clamp01(m_screenFade));
                    m_postRect = null;
                    RenderPost();
                }
                return;
            }
            m_lastPostFade = m_screenFade;
            if (contentDirty)
            {
                // 出力の合成には最後の撮影分がまだ入っていない：めくり・暗転の始めの静止画として使い回させない（描き直させる）
                m_targetHoldsIdle = null;
                // 暗転中は暗転の前の静止画を作り直させる。止まった見開きの描き直しなら、済んでから反映する
                // （数フレームに分けて描く場合、先に描いた帯は前の中身のまま）
                if (m_dim > 0f || m_dimRoutine != null) m_dimBaseValid = false;
                else if (!m_turning) HoldContentDirty(dirtyLeft, dirtyRight);
            }
            if (!m_warmedUp && UsesSplitPasses) WarmUpShaders();
            if (!m_turning) { m_turnCacheValid = false; m_coverCacheValid = false; m_coverPreStep = -1; }
            bool wasTurning = m_lastDrawWasTurn;
            m_lastDrawWasTurn = m_turning;
            m_renderCount++;
#if UNITY_EDITOR
            // 再生中にマテリアルアセットを調整したら、すぐ反映する
            if (m_material != null) m_runtimeMaterial.CopyPropertiesFromMaterial(m_material);
#endif
            ApplyProperties();
            if (m_postMaterial == null)
            {
                DrawComposite(m_outputTexture);
                return;
            }
            EnsureCompositeTexture();
            // めくっている間は小さめの解像度で描き、後処理で出力の大きさへ広げる（モバイル）。
            // 止まったら元の解像度で描き直す（数フレームに分けて）
            bool animated = m_turning && UsesSplitPasses && m_animationScaleMobile < 0.999f;
            if (animated)
            {
                EnsureAnimTexture();
                if (m_targetHoldsIdle == m_compositeTexture)
                {
                    // 静止した見開きを縮めて写し、動きの始めの静止画に使う
                    Graphics.Blit(m_compositeTexture, m_animTexture);
                    m_targetHoldsIdle = m_animTexture;
                }
                m_progressActive = false;
                m_progressPending = false;
                Note(GpuSync("gPre"));
                m_lastAnimFrameExact = true;
                DrawComposite(m_animTexture);
                m_postSource = m_animTexture;
                RenderPost();
                Note(GpuSync("gPost"));
                m_lastDrawTarget = m_animTexture;
                return;
            }
            if (UsesProgressive())
            {
                // めくり終えた直後は、最後のめくりの絵が移動先の見開きとほぼ同じなので、次のめくりの静止画に使える
                // 描いている途中にまた変わった（姿勢を動かし続けているなど）：今の分を描き終えてから、もう1度描く
                if (m_progressActive) m_progressPending = true;
                else StartProgress();
                // めくりの最後のフレームは、移動先を描き終えていたときだけ静止画として使う（代わりの絵が残らないように）
                if (wasTurning && m_lastAnimFrameExact)
                {
                    m_targetHoldsIdle = m_lastDrawTarget != null ? m_lastDrawTarget : m_compositeTexture;
                    m_targetHoldsUndimmed = m_dim <= 0f && (m_hasMagicStyle ? m_magicStrength : m_baseMagicStrength) <= 0f;
                }
                ContinueProgress();
                return;
            }
            m_progressActive = false;
            m_progressPending = false;
            DrawComposite(m_compositeTexture);
            m_postSource = null;
            RenderPost();
            m_lastDrawTarget = m_compositeTexture;
        }

        // ------------------------------------------------------------------
        // シェーダーの準備（モバイル）：使うパス・描き方・書き込み先の形式の組み合わせを最初に1度ずつ描いておく。
        // Vulkan では組み合わせを初めて使うときにパイプラインを作るので、めくりの始めなどで数十〜百ms止まるため
        // ------------------------------------------------------------------
        private bool m_warmedUp;

        private void WarmUpShaders()
        {
            m_warmedUp = true;
            Material material = m_runtimeMaterial;
            var hdr = RenderTexture.GetTemporary(8, 8, 0, HdrFormat);
            var bloom = RenderTexture.GetTemporary(8, 8, 0, BloomFormat);
            var output = RenderTexture.GetTemporary(8, 8, 0, m_outputTexture.format);
            RenderTextureFormat lutFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float) ? RenderTextureFormat.RGB111110Float : HdrFormat;
            RenderTextureFormat tiltFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf) ? RenderTextureFormat.RGHalf : HdrFormat;
            var lut = RenderTexture.GetTemporary(8, 8, 0, lutFormat);
            var tilt = RenderTexture.GetTemporary(8, 8, 0, tiltFormat);
            var rect = new Rect(0f, 0f, 1f, 1f);
            if (m_stillKeywordShader != material.shader)
            {
                m_stillKeyword = new UnityEngine.Rendering.LocalKeyword(material.shader, "BOOK_STILL");
                m_stillKeywordShader = material.shader;
            }
            // 先に従来のバリアント（BOOK_STILL 無し）を準備する
            if (m_stillKeyword.isValid) material.SetKeyword(m_stillKeyword, false);
            for (int pass = PassOutside; pass <= PassCoverTurn; pass++)
            {
                if (pass == PassPaperLut) { Graphics.Blit((Texture)null, lut, material, pass); continue; }
                if (pass == PassPaperTiltLut) { Graphics.Blit((Texture)null, tilt, material, pass); continue; }
                Graphics.Blit((Texture)null, hdr, material, pass);
                DrawRect(material, pass, hdr, rect);
            }
            // めくっていない見開き専用のバリアント（パス 1〜4）
            if (m_stillKeyword.isValid)
            {
                material.SetKeyword(m_stillKeyword, true);
                for (int pass = PassOutside; pass <= PassFinish; pass++)
                {
                    Graphics.Blit((Texture)null, hdr, material, pass);
                    DrawRect(material, pass, hdr, rect);
                }
                material.SetKeyword(m_stillKeyword, false);
            }
            if (material.passCount > PassLiveDelta) DrawRect(material, PassLiveDelta, hdr, rect);
            // 後処理
            if (m_postMaterial != null)
            {
                Material post = m_postMaterial;
                var bloom2 = RenderTexture.GetTemporary(8, 8, 0, BloomFormat);
                var hdr2 = RenderTexture.GetTemporary(8, 8, 0, HdrFormat);
                Graphics.Blit(hdr, bloom, post, PassPrefilter);
                Graphics.Blit(bloom, bloom2, post, PassDown);
                post.SetTexture(s_higherTex, bloom);
                Graphics.Blit(bloom2, bloom, post, PassUpAdd);
                Graphics.Blit(hdr, hdr2, post, PassDown);
                Graphics.Blit(hdr2, hdr, post, PassUp);
                RenderTexture.ReleaseTemporary(bloom2);
                RenderTexture.ReleaseTemporary(hdr2);
                post.SetTexture(s_bloomTex, bloom);
                post.SetTexture(s_blurTex, Texture2D.blackTexture);
                Graphics.Blit(hdr, output, post, PassComposite);
                post.SetTexture(s_mainTex, hdr);
                DrawRect(post, PassComposite, output, rect);
            }
            // ミップの作成・テクスチャの写し（初回は準備がかかる）
            var mip = new RenderTexture(16, 16, 0, HdrFormat) { useMipMap = true, autoGenerateMips = false, hideFlags = HideFlags.HideAndDontSave };
            mip.Create();
            Graphics.CopyTexture(hdr, 0, 0, 0, 0, 8, 8, mip, 0, 0, 0, 0);
            mip.GenerateMips();
            mip.Release();
            ReleaseObject(mip);
            RenderTexture.ReleaseTemporary(hdr);
            RenderTexture.ReleaseTemporary(bloom);
            RenderTexture.ReleaseTemporary(output);
            RenderTexture.ReleaseTemporary(lut);
            RenderTexture.ReleaseTemporary(tilt);
            Note(" warmUp");
        }

        [Tooltip("モバイルで、めくり・演出の間に描く解像度の倍率（出力の大きさに対して）。止まると元の解像度で描き直す")]
        [SerializeField, Range(0.5f, 1f)] private float m_animationScaleMobile = 0.8f;
        private RenderTexture m_animTexture;
        private RenderTexture m_postSource;   // 後処理が読む合成（null で m_compositeTexture）
        private RenderTexture m_lastDrawTarget;

        private void EnsureAnimTexture()
        {
            int w = Mathf.Max(8, Mathf.RoundToInt(m_compositeTexture.width * m_animationScaleMobile));
            int h = Mathf.Max(8, Mathf.RoundToInt(m_compositeTexture.height * m_animationScaleMobile));
            if (m_animTexture != null && m_animTexture.width == w && m_animTexture.height == h && m_animTexture.format == m_compositeTexture.format) return;
            if (m_animTexture != null) { m_animTexture.Release(); ReleaseObject(m_animTexture); }
            m_animTexture = new RenderTexture(w, h, 0, m_compositeTexture.format)
            {
                name = "BookFlipComposite (animation)", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear
            };
            m_animTexture.Create();
        }

        // ------------------------------------------------------------------
        // 静止した見開きを、数フレームに分けて描く（モバイル）。描き終えるまで出力は前の絵のまま。
        // 1回で描くとモバイルの GPU で数十ms かかり、その1フレームだけ止まって見えるため
        // ------------------------------------------------------------------
        [Tooltip("モバイルで、静止した見開きの描き直し（めくり終わり・姿勢の変更など）を何フレームに分けて描くか。1 で分けない")]
        [SerializeField, Range(1, 16)] private int m_progressiveStepsMobile = 6;
        [Tooltip("上の帯を、さらにこの数に分ける（1フレームに描く量が減り、描き終えるまでのフレームが増える）。" +
                 "ページの中身だけの更新の準備の絵は、この2倍に分ける")]
        [SerializeField, Range(1, 4)] private int m_stripSplitMobile = 2;
        private int ProgressSteps => Mathf.Max(1, m_progressiveStepsMobile) * Mathf.Max(1, m_stripSplitMobile);
        private bool m_progressActive;
        private bool m_hasEverDrawn;
        private bool m_progressPending;
        private int m_progressStep;
        private bool m_lastDrawWasTurn;
        private RenderTexture m_progressBase;
        private RenderTexture m_stripBase, m_stripBaseLow;
        private RenderTexture m_progressBuffer;

        private bool UsesProgressive()
        {
            if (!UsesSplitPasses || m_progressiveStepsMobile <= 1 || m_turning) return false;
            // 暗転中は暗転の静止画の仕組み（パス 9）を使う
            float magic = m_hasMagicStyle ? m_magicStrength : m_baseMagicStrength;
            if ((m_dim > 0f || m_dimRoutine != null) && magic <= 0f) return false;
            // 最初の1枚は分けずに描く（前の絵が無い）
            return m_hasEverDrawn && m_compositeTexture != null && m_compositeTexture.IsCreated();
        }

        private void StartProgress()
        {
            // 出力の合成は描き終えるまで前の状態のまま：静止画としては使わせない
            m_targetHoldsIdle = null;
            m_progressActive = true;
            m_progressStep = 0;
            // 見開き全体を描き直す：前のフレームまでに伝えられた中身の変化（撮影済み）は、この描き直しに含まれる
            m_dirtyOldLeft = m_dirtyOldRight = false;
            int w = m_compositeTexture.width, h = m_compositeTexture.height;
            if (m_progressBase == null || m_progressBase.width != w || m_progressBase.height != h)
            {
                if (m_progressBase != null) { m_progressBase.Release(); ReleaseObject(m_progressBase); }
                m_progressBase = new RenderTexture(w, h, 0, HdrFormat) { name = "BookProgressBase", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
                m_progressBase.Create();
            }
            if (m_progressBuffer == null || m_progressBuffer.width != w || m_progressBuffer.height != h || m_progressBuffer.format != m_compositeTexture.format)
            {
                if (m_progressBuffer != null) { m_progressBuffer.Release(); ReleaseObject(m_progressBuffer); }
                m_progressBuffer = new RenderTexture(w, h, 0, m_compositeTexture.format) { name = "BookProgressBuffer", hideFlags = HideFlags.HideAndDontSave };
                m_progressBuffer.Create();
            }
        }

        private void ContinueProgress()
        {
            Material material = m_runtimeMaterial;
            EnsurePaperLut(material);
            if (m_coverBaseTexture == null)
            {
                m_coverBaseTexture = new RenderTexture(1, 1, 0, HdrFormat) { name = "BookCoverBase", hideFlags = HideFlags.HideAndDontSave };
                m_coverBaseTexture.Create();
            }
            if (m_progressStep == 0) Graphics.Blit((Texture)null, m_coverBaseTexture, material, PassCoverBase);
            material.SetTexture(s_bookCoverBaseTex, m_coverBaseTexture);
            int steps = ProgressSteps;
            if (m_progressStep >= steps)
            {
                FinishProgress();
                return;
            }
            Rect strip = new Rect(0f, (float)m_progressStep / steps, 1f, 1f / steps);
            if (NotesEnabled) Note(" progress" + m_progressStep);
            ApplyStillKeyword(material);
            DrawRect(material, PassOutside, m_progressBase, strip);
            DrawRect(material, PassPage, m_progressBase, strip);
            DrawRect(material, PassCover, m_progressBase, strip);
            material.SetTexture(s_bookBaseTex, m_progressBase);
            DrawRect(material, PassFinish, m_progressBuffer, strip);
            material.SetTexture(s_bookBaseTex, null);
            m_progressStep++;
        }

        // 描き終えた：次のフレームで出力へ（最後の帯と同じフレームで後処理まで描くと重くなるため分ける）
        private void FinishProgress()
        {
            Note(" progressDone");
            m_progressActive = false;
            Graphics.CopyTexture(m_progressBuffer, m_compositeTexture);
            m_targetHoldsIdle = m_compositeTexture;
            m_targetHoldsUndimmed = m_dim <= 0f && (m_hasMagicStyle ? m_magicStrength : m_baseMagicStrength) <= 0f;
            m_lastDrawTarget = m_compositeTexture;
            m_postRect = null;
            m_postSource = null;
            RenderPost();
            if (m_progressPending)
            {
                m_progressPending = false;
                StartProgress();
            }
        }

        // ------------------------------------------------------------------
        // 合成の描画。モバイルは面の種類ごとのパス（1 本の外・2 ページ・3 表紙）で面の色を描き、4 で仕上げを重ねる
        // ------------------------------------------------------------------
        private const int PassFull = 0, PassOutside = 1, PassPage = 2, PassCover = 3, PassFinish = 4, PassCoverBase = 5;
        private static readonly int s_bookCoverBaseTex = Shader.PropertyToID("_BookCoverBaseTex");
        private RenderTexture m_coverBaseTexture;
        private const int PassTurn = 8;
        private static readonly int s_bookTurnCacheTex = Shader.PropertyToID("_BookTurnCacheTex");
        private RenderTexture m_turnCache;
        private const int PassCoverTurn = 10;
        private static readonly int s_bookDestReady = Shader.PropertyToID("_BookDestReady");
        private static readonly int s_bookOpening = Shader.PropertyToID("_BookOpening");
        private static readonly int s_bookTurnDestLowTex = Shader.PropertyToID("_BookTurnDestLowTex");
        private Vector2 m_coverDestReady;
        private int m_coverTurnFrames;
        private const int CoverDestDelayFrames = 0;

        // 表紙の開け閉めの移動先の静止画の i 番目の帯（出力の UV）。本の横の範囲（板の縁・小口・影の分を足す）だけを分け、
        // 板が寝ていた側の外側から順に描く。本より十分外はシェーダーが今の静止画を使うので描かない
        private Rect CoverDestStrip(int index, int steps)
        {
            float width = m_bookRect.z - m_bookRect.x;
            float x0 = Mathf.Clamp01(m_bookRect.x - width * 0.35f);
            float x1 = Mathf.Clamp01(m_bookRect.z + width * 0.35f);
            float step = (x1 - x0) / steps;
            // 板は最初 _TurnSide の側に寝ている（めくり空間の x が正の側）
            bool fromRight = m_turnSide >= 0f;
            float start = fromRight ? x1 - step * (index + 1) : x0 + step * index;
            if (index == steps - 1) start = fromRight ? x0 : x1 - step;
            return new Rect(start, 0f, step, 1f);
        }
        private bool m_coverCacheValid;

        // 本の座標の範囲 → 出力の UV の矩形
        private Rect BookToOutputRect(float bx0, float bx1, float by0, float by1)
        {
            float u0 = m_bookRect.x + (bx0 + 1f) * 0.5f * (m_bookRect.z - m_bookRect.x);
            float u1 = m_bookRect.x + (bx1 + 1f) * 0.5f * (m_bookRect.z - m_bookRect.x);
            float v0 = m_bookRect.y + by0 / Mathf.Max(m_pageAspect, 1e-5f) * (m_bookRect.w - m_bookRect.y);
            float v1 = m_bookRect.y + by1 / Mathf.Max(m_pageAspect, 1e-5f) * (m_bookRect.w - m_bookRect.y);
            return Rect.MinMaxRect(Mathf.Clamp01(u0), Mathf.Clamp01(v0), Mathf.Clamp01(u1), Mathf.Clamp01(v1));
        }
        private int m_turnSerial, m_turnCacheSerial = -1, m_coverCacheSerial = -1;
        private int m_coverDestStep;
        [Tooltip("モバイルで表紙を開け閉めするとき、移動先（開いた本・閉じた本）の静止画を何フレームに分けて描くか")]
        [SerializeField, Range(1, 48)] private int m_coverDestStepsMobile = 10;

        private RenderTexture EnsureCacheTexture(RenderTexture texture, int width, int height, string name, RenderTextureFormat format)
        {
            if (texture != null && texture.width == width && texture.height == height && texture.format == format) return texture;
            if (texture != null) { texture.Release(); ReleaseObject(texture); }
            texture = new RenderTexture(width, height, 0, format) { name = name, hideFlags = HideFlags.HideAndDontSave };
            texture.Create();
            return texture;
        }

        private RenderTexture EnsureCacheTexture(RenderTexture texture, RenderTexture like, string name, RenderTextureFormat format)
        {
            if (texture != null && texture.width == like.width && texture.height == like.height && texture.format == format) return texture;
            if (texture != null) { texture.Release(); ReleaseObject(texture); }
            texture = new RenderTexture(like.width, like.height, 0, format) { name = name, hideFlags = HideFlags.HideAndDontSave };
            texture.Create();
            return texture;
        }

        private RenderTexture m_coverCur, m_coverDest;
        // 移動先の半分の解像度の仮の絵：先に数回に分けて全体を描き、元の解像度の列が描けるまでの代わりに使う
        private RenderTexture m_coverDestLow;
        private const int CoverDestLowSteps = 2;

        // 表紙の開け閉め用の静止画はミップを持たせる：立った板では表紙の絵を横に縮めて読むので、
        // ミップが無いと読む位置が飛び飛びになり（テクスチャのキャッシュが効かず）極端に遅くなる
        private RenderTexture EnsureMipCache(RenderTexture texture, RenderTexture like, string name)
        {
            if (texture != null && texture.width == like.width && texture.height == like.height && texture.format == like.format) return texture;
            if (texture != null) { texture.Release(); ReleaseObject(texture); }
            texture = new RenderTexture(like.width, like.height, 0, like.format)
            {
                name = name, hideFlags = HideFlags.HideAndDontSave, useMipMap = true, autoGenerateMips = false,
                filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            return texture;
        }

        // 計測用（BookPerfProbe、端末に gpusync を置く）：GPU の処理の終わりを待ち、待った時間を返す
        internal static bool DebugGpuSync;
        private RenderTexture m_syncTexture;
        private Texture2D m_syncReadback;
        private string GpuSync(string label)
        {
            if (!DebugGpuSync || !NotesEnabled) return "";
            if (m_syncTexture == null) { m_syncTexture = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.HideAndDontSave }; m_syncTexture.Create(); }
            if (m_syncReadback == null) m_syncReadback = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Graphics.Blit(Texture2D.whiteTexture, m_syncTexture);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = m_syncTexture;
            m_syncReadback.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
            RenderTexture.active = previous;
            return " " + label + "=" + timer.Elapsed.TotalMilliseconds.ToString("0.0");
        }

        private bool m_lastAnimFrameExact = true; // 最後に描いた動きのフレームが、代わりの絵を使わずに描けたか

        // ------------------------------------------------------------------
        // 表紙の開け閉めの移動先を、止まっている間に先に描く（モバイル）。
        // 閉じた本なら開いた本、最初・最後の見開きなら閉じた本。めくりが始まったらそれを使い、
        // めくりの間に少しずつ描く処理（と、描き終えるまでの代わりの絵）を省く
        // ------------------------------------------------------------------
        [Tooltip("モバイルで、表紙の開け閉めの移動先を止まっている間に先に描いておく")]
        [SerializeField] private bool m_prerenderCoverMobile = true;
        private RenderTexture m_coverPre;
        private int m_coverPreStep = -1; // -1：まだ始めていない（見た目が変わったら -1 に戻す）
        private BookCoverState m_coverPreState;
        private Texture m_coverPreSpread;
        private const int CoverPreSteps = 8;
        private Hash128 m_coverPreSignature; // 描き始めたときの止まった状態の見た目（めくり始めに同じか確かめる）

        // 今の状態から表紙を開け閉めしたときの移動先（無ければ false）
        private bool CoverPrerenderTarget(out BookCoverState state)
        {
            state = BookCoverState.Open;
            if (m_current == null) return false;
            if (m_currentCover != BookCoverState.Open) return true;
            Vector2 kinds = SlotKinds(BookCoverState.Open, m_current);
            if (kinds.x > 4.5f) { state = BookCoverState.FrontCover; return true; }
            if (kinds.y > 4.5f) { state = BookCoverState.BackCover; return true; }
            return false;
        }

        private void ContinueCoverPrerender()
        {
            if (!m_prerenderCoverMobile || !m_turnCacheMobile || !m_rigidCovers || m_postMaterial == null || m_runtimeMaterial == null) return;
            if (!UsesSplitPasses || m_turning || m_progressActive) return;
            if (m_compositeTexture == null || !m_compositeTexture.IsCreated() || m_animationScaleMobile >= 0.999f) return;
            if (m_runtimeMaterial.passCount <= PassCoverTurn) return;
            // 暗転・魔法の縁取りが動いている間は描き直しが続くので、先に描いてもすぐ無効になる
            if (m_dim > 0f || m_dimRoutine != null || (m_hasMagicStyle ? m_magicStrength : m_baseMagicStrength) > 0f) return;
            if (m_coverPreStep >= CoverPreSteps || !CoverPrerenderTarget(out BookCoverState state)) return;
            if (m_coverPreStep < 0)
            {
                m_coverPreStep = 0;
                m_coverPreState = state;
                m_coverPreSpread = m_current;
                m_coverPreSignature = ComputeSignature(true);
            }
            EnsureAnimTexture();
            RenderTexture previous = m_coverPre;
            m_coverPre = EnsureMipCache(m_coverPre, m_animTexture, "BookCoverPre");
            // 途中で作り直した（大きさが変わった）：前に描いた帯は消えているので始めから
            if (previous != m_coverPre && m_coverPreStep > 0) m_coverPreStep = 0;
            // 移動先の状態として描く（表紙の開け閉めは、めくる前と後で同じ見開きを使う）
            BookCoverState destinationCover = m_destinationCover;
            Texture destination = m_destination;
            m_destinationCover = m_coverPreState;
            m_destination = m_coverPreSpread;
            RenderStateStrip(true, m_coverPre, new Rect(0f, (float)m_coverPreStep / CoverPreSteps, 1f, 1f / CoverPreSteps));
            m_destinationCover = destinationCover;
            m_destination = destination;
            m_coverPreStep++;
            if (m_coverPreStep >= CoverPreSteps) m_coverPre.GenerateMips();
            Note(" coverPre");
        }

        private bool CoverPrerenderMatches(RenderTexture target) =>
            m_coverPreStep >= CoverPreSteps && m_coverPre != null && m_coverPreState == m_destinationCover &&
            m_coverPreSpread == m_destination && m_coverPre.width == target.width && m_coverPre.height == target.height &&
            m_coverPre.format == target.format && m_dim <= 0f && (m_hasMagicStyle ? m_magicStrength : m_baseMagicStrength) <= 0f &&
            ComputeSignature(true) == m_coverPreSignature;

        private void DrawCoverTurn(Material material, RenderTexture target)
        {
            Note(GpuSync("g0"));
            int steps = Mathf.Max(1, m_coverDestStepsMobile);
            if (m_coverCacheSerial != m_turnSerial) m_coverCacheValid = false;
            if (!m_coverCacheValid)
            {
                m_coverCur = EnsureMipCache(m_coverCur, target, "BookCoverCur");
                m_coverDest = EnsureMipCache(m_coverDest, target, "BookCoverDest");
                m_coverDestLow = EnsureCacheTexture(m_coverDestLow, Mathf.Max(8, target.width / 2), Mathf.Max(8, target.height / 2),
                    "BookCoverDestLow", target.format);
                m_coverDestLow.filterMode = FilterMode.Bilinear;
                if (m_targetHoldsIdle == target) Graphics.CopyTexture(target, 0, 0, m_coverCur, 0, 0);
                else { RenderStateStrip(false, m_coverCur, new Rect(0f, 0f, 1f, 1f)); Note(" coverCurFull"); }
                m_coverCur.GenerateMips();
                Note(" coverStart");
                m_coverDestStep = 0;
                m_coverTurnFrames = 0;
                if (CoverPrerenderMatches(target))
                {
                    // 止まっている間に描いておいた移動先を使う（描き終えていて、ミップもある）
                    (m_coverDest, m_coverPre) = (m_coverPre, m_coverDest);
                    m_coverDestStep = CoverDestLowSteps + steps;
                    m_coverDestReady = new Vector2(0f, 1f);
                    Note(" coverPreUsed");
                }
                m_coverPreStep = -1;
                m_coverCacheValid = true;
                m_coverCacheSerial = m_turnSerial;
            }
            // CoverDestDelayFrames：動き始めに移動先を描かないフレーム数（止まっていた GPU はクロックが低い）。
            // 払っためくりは数フレームで終わるので、今は待たない
            else if (m_coverDestStep < CoverDestLowSteps + steps && ++m_coverTurnFrames > CoverDestDelayFrames)
            {
                if (m_coverDestStep < CoverDestLowSteps)
                {
                    // まず半分の解像度で全体を（横の帯に分けて）描く
                    RenderStateStrip(true, m_coverDestLow, new Rect(0f, (float)m_coverDestStep / CoverDestLowSteps, 1f, 1f / CoverDestLowSteps));
                    Note(" coverDestLow");
                    m_coverDestStep++;
                }
                else
                {
                    // 移動先の静止画を縦の帯で少しずつ描く（描き終えるまでは仮の絵で代用）。
                    // 板が寝ていた側の外側から描く：板が起き上がると、そちら側の外側から順に下が見えてくる
                    int index = m_coverDestStep - CoverDestLowSteps;
                    Rect strip = CoverDestStrip(index, steps);
                    RenderStateStrip(true, m_coverDest, strip);
                    Note(" coverDestStrip");
                    m_coverDestStep++;
                    m_coverDestReady = index == 0 ? new Vector2(strip.xMin, strip.xMax)
                        : new Vector2(Mathf.Min(m_coverDestReady.x, strip.xMin), Mathf.Max(m_coverDestReady.y, strip.xMax));
                    if (index + 1 >= steps) m_coverDest.GenerateMips();
                }
                Note(GpuSync("gStrip"));
            }
            bool destComplete = m_coverDestStep >= CoverDestLowSteps + steps;
            bool lowReady = m_coverDestStep >= CoverDestLowSteps;
            m_lastAnimFrameExact = destComplete;
            Vector2 ready = m_coverDestStep > CoverDestLowSteps ? m_coverDestReady : new Vector2(2f, -2f);
            material.SetVector(s_bookDestReady, new Vector4(ready.x, ready.y, destComplete ? 1f : 0f, lowReady ? 1f : 0f));
            material.SetTexture(s_bookTurnDestLowTex, m_coverDestLow);
            material.SetFloat(s_bookOpening, m_currentCover != BookCoverState.Open ? 1f : 0f);
            material.SetTexture(s_bookTurnCacheTex, m_coverCur);
            material.SetTexture(s_bookTurnDestTex, m_coverDest);
            if (m_coverBaseTexture != null) material.SetTexture(s_bookCoverBaseTex, m_coverBaseTexture);
            Graphics.Blit((Texture)null, target, material, PassCoverTurn);
            Note(GpuSync("gPass"));
            m_targetHoldsIdle = null;
        }

        // めくっていない状態（今の見開き、または移動先）の絵の一部を描く。状態を一時的に入れ替えてプロパティを作り直す
        private void RenderStateStrip(bool destination, RenderTexture texture, Rect strip)
        {
            bool turning = m_turning;
            BookCoverState currentCover = m_currentCover;
            Texture current = m_current;
            if (destination)
            {
                m_currentCover = m_destinationCover;
                if (m_destination != null) m_current = m_destination;
            }
            m_turning = false;
            ApplyProperties();
            Material material = m_runtimeMaterial;
            EnsurePaperLut(material);
            if (m_coverBaseTexture == null)
            {
                m_coverBaseTexture = new RenderTexture(1, 1, 0, HdrFormat) { name = "BookCoverBase", hideFlags = HideFlags.HideAndDontSave };
                m_coverBaseTexture.Create();
            }
            Graphics.Blit((Texture)null, m_coverBaseTexture, material, PassCoverBase);
            material.SetTexture(s_bookCoverBaseTex, m_coverBaseTexture);
            // 静止した見開きを少しずつ描く処理（m_progressBase、出力の解像度）とは別に持つ。
            // 動いている間は小さめの解像度なので、共有すると毎回作り直しになる（めくり始めが重くなる）
            // 半分の解像度の仮の絵（表紙の開け閉め）は別に持つ
            bool low = texture == m_coverDestLow;
            RenderTexture stripBase = low ? m_stripBaseLow : m_stripBase;
            stripBase = EnsureCacheTexture(stripBase, texture, low ? "BookStripBaseLow" : "BookStripBase", HdrFormat);
            stripBase.filterMode = FilterMode.Point;
            if (low) m_stripBaseLow = stripBase; else m_stripBase = stripBase;
            ApplyStillKeyword(material);
            DrawRect(material, PassOutside, stripBase, strip);
            DrawRect(material, PassPage, stripBase, strip);
            DrawRect(material, PassCover, stripBase, strip);
            material.SetTexture(s_bookBaseTex, stripBase);
            DrawRect(material, PassFinish, texture, strip);
            material.SetTexture(s_bookBaseTex, null);
            // めくりの状態へ戻す
            m_turning = turning;
            m_currentCover = currentCover;
            m_current = current;
            ApplyProperties();
        }
        private const int PassDim = 9;
        private static readonly int s_bookDimBaseTex = Shader.PropertyToID("_BookDimBaseTex");
        private RenderTexture m_dimBase;
        private bool m_dimBaseValid;
        private Hash128 m_dimBaseSignature;
        private float m_lastPostFade;
        private Rect m_previousTurnRect;
        private Rect? m_postRect; // このフレームで合成が変わった範囲（後処理もここだけ描く）。null で全体

        private static Rect Union(Rect a, Rect b) =>
            Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        private RenderTexture m_turnDest;
        private static readonly int s_bookTurnDestTex = Shader.PropertyToID("_BookTurnDestTex");

        // めくりで変わる範囲（出力の UV）：めくるページの矩形と、折り返した紙の範囲に、影の幅を足したもの。
        // シェーダーの BookFlipResolve と同じ「めくり空間」（x を _TurnSide で反転）で求める
        private static readonly int s_dropShadowWidth = Shader.PropertyToID("_DropShadowWidth");
        private static readonly int s_foldShadowWidth = Shader.PropertyToID("_FoldShadowWidth");

        private Rect TurnRect(Material material)
        {
            Vector4 fold = material.GetVector(s_fold);
            float side = m_turnSide >= 0f ? 1f : -1f;
            float height = m_pageAspect;
            Vector2 a = new Vector2(fold.x, fold.y), b = new Vector2(fold.z, fold.w);
            Vector2 axis = a - b;
            float xMin = 0f, xMax = 1f, yMin = 0f, yMax = height;
            if (axis.sqrMagnitude > 1e-8f)
            {
                Vector2 n = axis.normalized, mid = (a + b) * 0.5f;
                // 折り目より外側（めくれて現れた移動先のページ）は、折り目の陰の届く幅より先はもう変わらない。
                // 移動先の絵を描き終えた後は、そこを描き直さない（めくりの後半は描く範囲が半分近くになる）
                if (TurnDestReady && Mathf.Abs(n.x) > 0.05f)
                {
                    float x0 = mid.x + mid.y * n.y / n.x;                 // 折り目の y=0 での x
                    float x1 = mid.x - (height - mid.y) * n.y / n.x;      // 折り目の y=height での x
                    float reach = 3.5f * Mathf.Max(0.01f, material.GetFloat(s_foldShadowWidth));
                    xMax = Mathf.Clamp(Mathf.Max(x0, x1) + reach, 0f, 1f);
                }
                Vector2[] corners = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, height), new Vector2(0f, height) };
                foreach (Vector2 c in corners)
                {
                    // 折り目より角の側（めくれた部分）の角を折り目で折り返す
                    float d = Vector2.Dot(c - mid, n);
                    if (d <= 0f) continue;
                    Vector2 r = c - 2f * d * n;
                    xMin = Mathf.Min(xMin, r.x); xMax = Mathf.Max(xMax, r.x);
                    yMin = Mathf.Min(yMin, r.y); yMax = Mathf.Max(yMax, r.y);
                }
            }
            // 落ち影・折り目の光の幅（まとめてめくる束なら小口の帯と広げた落ち影も）
            Vector4 bundle = material.GetVector(s_turnThickness);
            float margin = Mathf.Max(0.12f, material.GetFloat(s_dropShadowWidth) + bundle.x * Mathf.Max(1f, m_stackDrop) + bundle.z + 0.02f);
            xMin -= margin; xMax += margin; yMin -= margin; yMax += margin;
            // めくり空間 → 本の座標（x）→ 出力の UV
            float bx0 = side > 0f ? xMin : -xMax, bx1 = side > 0f ? xMax : -xMin;
            float u0 = m_bookRect.x + (bx0 + 1f) * 0.5f * (m_bookRect.z - m_bookRect.x);
            float u1 = m_bookRect.x + (bx1 + 1f) * 0.5f * (m_bookRect.z - m_bookRect.x);
            float v0 = m_bookRect.y + yMin / Mathf.Max(height, 1e-5f) * (m_bookRect.w - m_bookRect.y);
            float v1 = m_bookRect.y + yMax / Mathf.Max(height, 1e-5f) * (m_bookRect.w - m_bookRect.y);
            u0 = Mathf.Clamp01(u0); u1 = Mathf.Clamp01(u1); v0 = Mathf.Clamp01(v0); v1 = Mathf.Clamp01(v1);
            return Rect.MinMaxRect(u0, v0, Mathf.Max(u0, u1), Mathf.Max(v0, v1));
        }

        // 出力の一部（UV の矩形）だけにパスを描く。Graphics.Blit と同じ向き（UV = 頂点の位置）
        private static void DrawRect(Material material, int pass, RenderTexture target, Rect rect)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.PushMatrix();
            GL.LoadOrtho();
            material.SetPass(pass);
            GL.Begin(GL.QUADS);
            GL.TexCoord2(rect.xMin, rect.yMin); GL.Vertex3(rect.xMin, rect.yMin, 0f);
            GL.TexCoord2(rect.xMin, rect.yMax); GL.Vertex3(rect.xMin, rect.yMax, 0f);
            GL.TexCoord2(rect.xMax, rect.yMax); GL.Vertex3(rect.xMax, rect.yMax, 0f);
            GL.TexCoord2(rect.xMax, rect.yMin); GL.Vertex3(rect.xMax, rect.yMin, 0f);
            GL.End();
            GL.PopMatrix();
            RenderTexture.active = previous;
        }

        // 移動先に何も無い面がある（本の外が見える）めくりは、静止画を使わずに描く
        private static bool HasEmptyDestination(Material material)
        {
            Vector4 kinds = material.GetVector(s_slotKinds);
            return Mathf.Abs(kinds.z - 3f) < 0.5f || Mathf.Abs(kinds.w - 3f) < 0.5f;
        }

        // 移動先の見開きのページの面の色を、めくっていない状態として1度だけ描く（パス 2。ページ以外は描かない）
        // 移動先の静止画は数フレームに分けて描く（めくり・演出の始めの1フレームが重くならないように）。
        // 描き終えるまでは今の静止画で代用する（始めは移動先がほとんど見えない）
        [Tooltip("モバイルで、めくり・演出の始めに移動先のページを何フレームに分けて描くか")]
        [SerializeField, Range(1, 24)] private int m_turnDestStepsMobile = 10;
        private int m_turnDestStep;
        // 先に半分の解像度で数回に分けて描き（すぐ使える仮の絵）、その後で元の解像度の帯を描く
        private const int TurnDestLowSteps = 1;
        private RenderTexture m_turnDestLow;
        private bool TurnDestReady => m_turnDestStep >= TurnDestLowSteps + Mathf.Max(1, m_turnDestStepsMobile);
        private Texture TurnDestTexture => TurnDestReady ? m_turnDest : m_turnDestStep >= TurnDestLowSteps && m_turnDestLow != null ? m_turnDestLow : m_turnCache;

        private void BeginTurnDestination(RenderTexture target)
        {
            if (m_turnDest == null || m_turnDest.width != target.width || m_turnDest.height != target.height)
            {
                if (m_turnDest != null) { m_turnDest.Release(); ReleaseObject(m_turnDest); }
                m_turnDest = new RenderTexture(target.width, target.height, 0, HdrFormat)
                {
                    name = "BookTurnDest", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear
                };
                m_turnDest.Create();
            }
            int lowW = Mathf.Max(1, target.width / 2), lowH = Mathf.Max(1, target.height / 2);
            if (m_turnDestLow == null || m_turnDestLow.width != lowW || m_turnDestLow.height != lowH)
            {
                if (m_turnDestLow != null) { m_turnDestLow.Release(); ReleaseObject(m_turnDestLow); }
                m_turnDestLow = new RenderTexture(lowW, lowH, 0, HdrFormat)
                {
                    name = "BookTurnDestLow", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear
                };
                m_turnDestLow.Create();
            }
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = m_turnDest;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = m_turnDestLow;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = previous;
            m_turnDestStep = 0;
        }

        // 移動先の見開きのページの面の色を、めくっていない状態として描く（パス 2。ページ以外は描かない）。1回で1つの帯
        private void ContinueTurnDestination(Material material, float firstBandY)
        {
            if (TurnDestReady) return;
            bool low = m_turnDestStep < TurnDestLowSteps;
            int steps = low ? TurnDestLowSteps : Mathf.Max(1, m_turnDestStepsMobile);
            int index = low ? m_turnDestStep : m_turnDestStep - TurnDestLowSteps;
            // めくり始める角に近い帯から描く
            int first = Mathf.Clamp(Mathf.FloorToInt(firstBandY * steps), 0, steps - 1);
            Note(low ? " destLow" : " destStrip");
            int band = (first + index) % steps;
            Texture destination = m_destination != null ? m_destination : m_current;
            Vector4 kinds = material.GetVector(s_slotKinds);
            float turning = material.GetFloat(s_turning);
            material.SetTexture(s_curLeftTex, destination);
            material.SetTexture(s_curRightTex, destination);
            material.SetVector(s_slotKinds, new Vector4(kinds.z, kinds.w, kinds.z, kinds.w));
            material.SetFloat(s_turning, 0f);
            ApplyStillKeyword(material);
            DrawRect(material, PassPage, low ? m_turnDestLow : m_turnDest, new Rect(0f, (float)band / steps, 1f, 1f / steps));
            // 元の状態へ戻す
            material.SetTexture(s_curLeftTex, m_current);
            material.SetTexture(s_curRightTex, m_current);
            material.SetVector(s_slotKinds, kinds);
            material.SetFloat(s_turning, turning);
            m_turnDestStep++;
        }
        private bool m_turnCacheValid;
        private RenderTexture m_targetHoldsIdle; // 静止した見開き（めくり・演出の無い状態）を最後に描いた先
        private bool m_targetHoldsUndimmed;      // その絵に暗転・魔法の縁取りが入っていないか（暗転の静止画に使えるか）
        private const int PassPaperLut = 6, PassPaperTiltLut = 7;
        private static readonly int s_bookPaperLut = Shader.PropertyToID("_BookPaperLut");
        private static readonly int s_bookPaperTiltLut = Shader.PropertyToID("_BookPaperTiltLut");
        private static readonly string[] s_paperLutFloats =
            { "_PaperTexStrength", "_PaperGrain", "_PaperAge", "_PaperNormalStrength", "_PaperBump", "_PaperBumpScale", "_PageAspect", "_PagePixels" };
        private static readonly string[] s_paperLutVectors = { "_PaperTex_ST", "_AgeColor", "_PaperNormalTex_ST" };
        private RenderTexture m_paperLut;
        private RenderTexture m_paperTiltLut;
        private Hash128 m_paperLutSignature;

        // 紙の模様（位置だけで決まる：むら・焼け・凹凸の傾き）をテクスチャに焼く。紙の設定・出力の大きさが変わったときだけ
        private void EnsurePaperLut(Material material)
        {
            var h = new Hash128();
            foreach (string name in s_paperLutFloats) h.Append(material.HasProperty(name) ? material.GetFloat(name) : 0f);
            foreach (string name in s_paperLutVectors)
            {
                Vector4 v = material.HasProperty(name) ? material.GetVector(name) : Vector4.zero;
                h.Append(ref v);
            }
            Texture paper = material.HasProperty("_PaperTex") ? material.GetTexture("_PaperTex") : null;
            Texture normal = material.HasProperty("_PaperNormalTex") ? material.GetTexture("_PaperNormalTex") : null;
            h.Append(paper != null ? paper.GetInstanceID() : 0);
            h.Append(normal != null ? normal.GetInstanceID() : 0);
            h.Append(material.IsKeywordEnabled("_PAPERFINISH_GLOSS") ? 1 : 0);
            // 見開きの幅を出力のページの画素数の 1.5 倍で焼く（ノイズは出力の画素で描ける細かさまでに抑えてある）
            float pagePixels = material.GetFloat(s_pagePixels);
            int width = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(pagePixels, 256f) * 1.5f) * 2, 512, 4096);
            int height = Mathf.Clamp(Mathf.CeilToInt(width * 0.5f * Mathf.Max(m_pageAspect, 0.1f)), 64, 4096);
            h.Append(width);
            h.Append(height);
            bool ready = m_paperLut != null && m_paperLut.IsCreated() && m_paperTiltLut != null && m_paperTiltLut.IsCreated();
            if (ready && h == m_paperLutSignature)
            {
                material.SetTexture(s_bookPaperLut, m_paperLut);
                material.SetTexture(s_bookPaperTiltLut, m_paperTiltLut);
                return;
            }
            m_paperLutSignature = h;
            Note(" paperLut");
            ReleasePaperLut();
            RenderTextureFormat colorFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float)
                ? RenderTextureFormat.RGB111110Float : HdrFormat;
            RenderTextureFormat tiltFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf)
                ? RenderTextureFormat.RGHalf : HdrFormat;
            m_paperLut = new RenderTexture(width, height, 0, colorFormat)
            {
                name = "BookPaperLut", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            m_paperTiltLut = new RenderTexture(width, height, 0, tiltFormat)
            {
                name = "BookPaperTiltLut", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            m_paperLut.Create();
            m_paperTiltLut.Create();
            Graphics.Blit((Texture)null, m_paperLut, material, PassPaperLut);
            Graphics.Blit((Texture)null, m_paperTiltLut, material, PassPaperTiltLut);
            material.SetTexture(s_bookPaperLut, m_paperLut);
            material.SetTexture(s_bookPaperTiltLut, m_paperTiltLut);
        }

        private void ReleasePaperLut()
        {
            if (m_paperLut != null) { m_paperLut.Release(); ReleaseObject(m_paperLut); m_paperLut = null; }
            if (m_paperTiltLut != null) { m_paperTiltLut.Release(); ReleaseObject(m_paperTiltLut); m_paperTiltLut = null; }
        }
        private static readonly int s_bookBaseTex = Shader.PropertyToID("_BookBaseTex");

        private bool UsesSplitPasses => m_splitPassesMobile && UsesMobileSettings && m_runtimeMaterial.passCount > PassPaperTiltLut;

        private void DrawComposite(RenderTexture target)
        {
            if (!UsesSplitPasses)
            {
                Graphics.Blit((Texture)null, target, m_runtimeMaterial, PassFull);
                return;
            }
            Material material = m_runtimeMaterial;
            m_postRect = null;
            EnsurePaperLut(material);

            // 本文のめくり：動かない部分はめくり始めの静止画を使い、移動先のページとめくった紙だけを描く（パス 8）
            bool pageTurn = m_turnCacheMobile && m_turning && m_postMaterial != null &&
                            m_currentCover == BookCoverState.Open && m_destinationCover == BookCoverState.Open &&
                            material.passCount > PassTurn && !HasEmptyDestination(material);
            if (pageTurn)
            {
                bool turnRebuilt = false, turnStarted = false;
                if (m_turnCacheSerial != m_turnSerial) m_turnCacheValid = false;
                if (!m_turnCacheValid || m_turnCache == null || m_turnCache.width != target.width || m_turnCache.height != target.height)
                {
                    if (m_turnCache == null || m_turnCache.width != target.width || m_turnCache.height != target.height)
                    {
                        if (m_turnCache != null) { m_turnCache.Release(); ReleaseObject(m_turnCache); }
                        m_turnCache = new RenderTexture(target.width, target.height, 0, target.format)
                        {
                            name = "BookTurnCache", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point
                        };
                        m_turnCache.Create();
                    }
                    if (m_targetHoldsIdle == target && target.format == m_turnCache.format)
                    {
                        // 直前に描いた静止した見開きをそのまま使う（描き直さない）
                        Graphics.CopyTexture(target, m_turnCache);
                    }
                    else
                    {
                        Note(" turnCurFull");
                        // めくっていない状態で1度だけ描き、出力にも写す（以後は変わる範囲だけを描くため）
                        material.SetFloat(s_turning, 0f);
                        DrawSplit(material, m_turnCache);
                        material.SetFloat(s_turning, 1f);
                        if (target.format == m_turnCache.format) Graphics.CopyTexture(m_turnCache, target);
                        else Graphics.Blit(m_turnCache, target);
                        turnRebuilt = true;
                    }
                    m_previousTurnRect = new Rect(0f, 0f, 0f, 0f);
                    turnStarted = true;
                    BeginTurnDestination(target);
                    m_turnCacheValid = true;
                    m_turnCacheSerial = m_turnSerial;
                }
                // 移動先は次のフレームから1フレームに1つの帯ずつ描く（始めのフレームは止まっていた GPU のクロックが低く重い。
                // 紙はまだほとんどめくれていない）。最初の帯は半分の解像度の仮の絵なので、2フレーム目から紙の裏が移動先になる
                if (!turnStarted)
                    ContinueTurnDestination(material, CornerPosition(m_corner).y / Mathf.Max(m_pageAspect, 1e-5f) *
                        (m_bookRect.w - m_bookRect.y) + m_bookRect.y);
                material.SetTexture(s_bookTurnCacheTex, m_turnCache);
                material.SetTexture(s_bookTurnDestTex, TurnDestTexture);
                m_lastAnimFrameExact = TurnDestReady;
                // めくりの間、ページの撮影テクスチャは描き始めの絵（今の見開きの静止画・移動先の絵）にしか使わない。
                // 中身は撮影済みなので、めくり終わるまで撮影を休ませる（モバイル。BookPageView）
                BookPageView.PauseCaptures();
                // めくっている紙と、その下・影の範囲だけを描く。それ以外は出力に残っている静止画のまま。
                // 前のフレームで描いた範囲も描き直して、前の紙の絵を消す
                Rect rect = TurnRect(material);
                Rect draw = m_previousTurnRect.width > 0f ? Union(rect, m_previousTurnRect) : rect;
                m_previousTurnRect = rect;
                DrawRect(material, PassTurn, target, draw);
                // 静止画を作り直したフレームは、出力全体を描き直す（出力の大きさが変わった場合など）
                m_postRect = turnRebuilt ? (Rect?)null : draw;
                m_targetHoldsIdle = null;
                return;
            }
            m_turnCacheValid = false;

            // 表紙の開け閉め：閉じた本と開いた本の静止画を読み、板の向きと影だけを描く（パス 10）
            bool coverTurn = m_turnCacheMobile && m_turning && m_postMaterial != null &&
                             (m_currentCover != BookCoverState.Open || m_destinationCover != BookCoverState.Open) &&
                             m_rigidCovers && material.passCount > PassCoverTurn;
            if (coverTurn)
            {
                DrawCoverTurn(material, target);
                return;
            }
            m_coverCacheValid = false;

            // 暗転だけが動いている（暗転の出入り・ルーンの脈動）：暗転の前の絵は1度だけ描き、暗転だけを重ねる（パス 9）
            float magic = m_hasMagicStyle ? m_magicStrength : m_baseMagicStrength;
            bool dimOnly = (m_dim > 0f || m_dimRoutine != null) && magic <= 0f && !m_turning &&
                           material.passCount > PassDim;
            if (dimOnly)
            {
                Hash128 signature = ComputeSignature();
                if (!m_dimBaseValid || signature != m_dimBaseSignature || m_dimBase == null ||
                    m_dimBase.width != target.width || m_dimBase.height != target.height)
                {
                    if (m_dimBase == null || m_dimBase.width != target.width || m_dimBase.height != target.height)
                    {
                        if (m_dimBase != null) { m_dimBase.Release(); ReleaseObject(m_dimBase); }
                        m_dimBase = new RenderTexture(target.width, target.height, 0, HdrFormat)
                        {
                            name = "BookDimBase", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point
                        };
                        m_dimBase.Create();
                    }
                    if (m_targetHoldsIdle == target && m_targetHoldsUndimmed && target.format == m_dimBase.format)
                    {
                        // 暗転の前の静止画がそのまま出力に残っている（描き直さない）
                        Graphics.CopyTexture(target, m_dimBase);
                    }
                    else
                    {
                        material.SetFloat(s_dim, 0f);
                        float fade = material.GetFloat(s_screenFade);
                        material.SetFloat(s_screenFade, 0f);
                        DrawSplit(material, m_dimBase);
                        material.SetFloat(s_dim, m_dim);
                        material.SetFloat(s_screenFade, fade);
                    }
                    m_dimBaseSignature = signature;
                    m_dimBaseValid = true;
                }
                material.SetTexture(s_bookDimBaseTex, m_dimBase);
                Graphics.Blit((Texture)null, target, material, PassDim);
                m_targetHoldsIdle = null;
                return;
            }
            m_dimBaseValid = false;
            Note(" fullSplit");
            DrawSplit(material, target);
            m_targetHoldsIdle = !m_turning ? target : null;
            m_targetHoldsUndimmed = m_dim <= 0f && (m_hasMagicStyle ? m_magicStrength : m_baseMagicStrength) <= 0f;
            m_hasEverDrawn = true;
        }

        // めくっていない・暗転していない見開き専用のバリアント（BOOK_STILL）を使うか。パス 1〜4 を描く直前に、
        // そのときマテリアルに入っている値から決める（一時的に _Turning や _Dim を変えて描く箇所があるため）
        private UnityEngine.Rendering.LocalKeyword m_stillKeyword;
        private Shader m_stillKeywordShader;

        private void ApplyStillKeyword(Material material)
        {
            if (m_stillKeywordShader != material.shader)
            {
                m_stillKeyword = new UnityEngine.Rendering.LocalKeyword(material.shader, "BOOK_STILL");
                m_stillKeywordShader = material.shader;
            }
            if (!m_stillKeyword.isValid) return;
            bool still = m_stillVariantMobile && material.GetFloat(s_turning) < 0.5f && material.GetFloat(s_dim) <= 0f;
            material.SetKeyword(m_stillKeyword, still);
        }

        private void DrawSplit(Material material, RenderTexture target)
        {
            ApplyStillKeyword(material);
            RenderTexture baseTexture = RenderTexture.GetTemporary(target.width, target.height, 0, HdrFormat);
            baseTexture.filterMode = FilterMode.Point;
            // 1〜3 は担当外の画素を捨てる。3つで全画素を1回ずつ塗る
            // 表紙の地の色を1画素に求める（パス 5）
            if (m_coverBaseTexture == null)
            {
                m_coverBaseTexture = new RenderTexture(1, 1, 0, HdrFormat) { name = "BookCoverBase", hideFlags = HideFlags.HideAndDontSave };
                m_coverBaseTexture.Create();
            }
            Graphics.Blit((Texture)null, m_coverBaseTexture, material, PassCoverBase);
            material.SetTexture(s_bookCoverBaseTex, m_coverBaseTexture);
            Graphics.Blit((Texture)null, baseTexture, material, PassOutside);
            Graphics.Blit((Texture)null, baseTexture, material, PassPage);
            Graphics.Blit((Texture)null, baseTexture, material, PassCover);
            material.SetTexture(s_bookBaseTex, baseTexture);
            Graphics.Blit((Texture)null, target, material, PassFinish);
            material.SetTexture(s_bookBaseTex, null);
            RenderTexture.ReleaseTemporary(baseTexture);
        }

        // ------------------------------------------------------------------
        // 計測用（BookPerfProbe のベンチマーク）：今の状態のまま、合成・後処理だけを描く
        // ------------------------------------------------------------------
        internal Material BenchMaterial => m_runtimeMaterial;
        // 計測用：このフレームで行った重い処理の名前（BookPerfProbe が遅いフレームの記録に使う）
        internal string FrameNote = "";
        // 計測（BookPerfProbe）がいるときだけ、そのフレームの処理の名前を残す（いなければ文字列を作らない）
        internal bool NotesEnabled;
        private void Note(string text)
        {
            if (NotesEnabled) FrameNote += text;
        }
        // 計測で、プロパティを一時的に変えて描く（ApplyProperties の後に呼ぶ）
        internal System.Action<Material> BenchOverride;
        internal void BenchPostWith(System.Action<Material> setup)
        {
            setup?.Invoke(m_postMaterial);
        }

        internal void BenchComposite(RenderTexture target, Material material = null)
        {
            if (m_runtimeMaterial == null) return;
            ApplyProperties();
            BenchOverride?.Invoke(m_runtimeMaterial);
            if (material != null) Graphics.Blit((Texture)null, target, material, 0);
            else DrawComposite(target);
        }

        // 分割パスの1つだけを描く（pass 1〜3 は baseTexture へ、4 は baseTexture を読んで target へ）
        internal void BenchSplitPass(RenderTexture baseTexture, RenderTexture target, int pass)
        {
            if (m_runtimeMaterial == null || m_runtimeMaterial.passCount <= PassCoverBase) return;
            ApplyProperties();
            if (m_coverBaseTexture != null) m_runtimeMaterial.SetTexture(s_bookCoverBaseTex, m_coverBaseTexture);
            EnsurePaperLut(m_runtimeMaterial);
            ApplyStillKeyword(m_runtimeMaterial);
            if (pass == PassFinish)
            {
                m_runtimeMaterial.SetTexture(s_bookBaseTex, baseTexture);
                Graphics.Blit((Texture)null, target, m_runtimeMaterial, PassFinish);
            }
            else Graphics.Blit((Texture)null, baseTexture, m_runtimeMaterial, pass);
        }

        // 静止画を分けて描くときの1フレーム分（真ん中の帯）
        internal void BenchProgressStrip()
        {
            if (m_compositeTexture == null || !UsesSplitPasses) return;
            ApplyProperties();
            StartProgress();
            m_progressStep = ProgressSteps / 2;
            int steps = ProgressSteps;
            Material material = m_runtimeMaterial;
            EnsurePaperLut(material);
            Rect strip = new Rect(0f, (float)m_progressStep / steps, 1f, 1f / steps);
            ApplyStillKeyword(material);
            DrawRect(material, PassOutside, m_progressBase, strip);
            DrawRect(material, PassPage, m_progressBase, strip);
            DrawRect(material, PassCover, m_progressBase, strip);
            material.SetTexture(s_bookBaseTex, m_progressBase);
            DrawRect(material, PassFinish, m_progressBuffer, strip);
            material.SetTexture(s_bookBaseTex, null);
            m_progressActive = false;
        }

        // 動いている間の1フレーム（小さめの解像度の合成 + 後処理）。実際の LateUpdate と同じ描き方
        internal void BenchAnimFrame()
        {
            if (m_animTexture == null || m_runtimeMaterial == null) return;
            ApplyProperties();
            BenchOverride?.Invoke(m_runtimeMaterial);
            DrawComposite(m_animTexture);
            m_postSource = m_animTexture;
            RenderPost();
        }

        // ------------------------------------------------------------------
        // 見た目の比較（BookPerfProbe）：同じ状態をモバイルの描き方と PC の1パス（パス 0）で描き、並べて保存する
        // ------------------------------------------------------------------
        internal System.Action<Material> CompareOverride;
        internal bool IsBusyRendering => m_progressActive || m_dimRoutine != null || m_screenFadeRoutine != null;

        internal void CompareRender(RenderTexture mobileTarget, RenderTexture fullTarget, System.Action<Material> setup)
        {
            if (m_runtimeMaterial == null) return;
            CompareOverride = setup;
            ApplyProperties();
            // 移動先の静止画を数フレームに分けて描く分も描き終える
            for (int i = 0; i < 30; i++) DrawComposite(mobileTarget);
            ApplyProperties();
            Graphics.Blit((Texture)null, fullTarget, m_runtimeMaterial, PassFull);
            CompareOverride = null;
            ApplyProperties();
        }

        // 計測用：今の見開きを、全体の描き直し（full）と、ページの中身だけの更新（delta：真っ黒・真っ白の準備の絵 + パス 11）で描く。
        // 2枚が同じなら、中身だけの更新は正しい絵を出している。戻り値は、パス 11 を左ページに1回描く時間を測る処理
        internal System.Action BenchContentDelta(RenderTexture full, RenderTexture delta)
        {
            if (m_runtimeMaterial == null || !UsesSplitPasses || m_runtimeMaterial.passCount <= PassLiveDelta) return null;
            Material material = m_runtimeMaterial;
            BenchContentDeltaEnd();
            RenderTexture zero = m_benchZero = RenderTexture.GetTemporary(full.width, full.height, 0, HdrFormat);
            RenderTexture one = m_benchOne = RenderTexture.GetTemporary(full.width, full.height, 0, HdrFormat);
            zero.filterMode = FilterMode.Point;
            one.filterMode = FilterMode.Point;
            Rect all = new Rect(0f, 0f, 1f, 1f);
            DrawContentDeltaStrip(zero, Texture2D.blackTexture, all);
            DrawContentDeltaStrip(one, Texture2D.whiteTexture, all);
            ApplyProperties();
            DrawSplit(material, full);
            Graphics.Blit(zero, delta);
            material.SetTexture(s_bookLiveZeroTex, zero);
            material.SetTexture(s_bookLiveOneTex, one);
            DrawRect(material, PassLiveDelta, delta, BookToOutputRect(-1f, 1f, 0f, m_pageAspect));
            Rect left = BookToOutputRect(-1f, 0f, 0f, m_pageAspect);
            return () =>
            {
                material.SetTexture(s_bookLiveZeroTex, zero);
                material.SetTexture(s_bookLiveOneTex, one);
                DrawRect(material, PassLiveDelta, delta, left);
            };
        }

        private RenderTexture m_benchZero, m_benchOne;

        // 計測で使った準備の絵を返す
        internal void BenchContentDeltaEnd()
        {
            if (m_benchZero != null) RenderTexture.ReleaseTemporary(m_benchZero);
            if (m_benchOne != null) RenderTexture.ReleaseTemporary(m_benchOne);
            m_benchZero = m_benchOne = null;
        }

        // 計測用：めくっていない見開き専用のバリアント（BOOK_STILL）を使うか（従来の描き方と比べる）
        internal bool BenchStillVariant
        {
            get => m_stillVariantMobile;
            set => m_stillVariantMobile = value;
        }

        internal void BenchPostBloomSteps(int steps)
        {
            int mobile = m_bloomIterationsMobile, all = m_bloomIterations;
            m_bloomIterationsMobile = m_bloomIterations = steps;
            BenchPost();
            m_bloomIterationsMobile = mobile;
            m_bloomIterations = all;
        }

        internal static int DebugBloomDiv;      // 計測用：Bloom を始める大きさ（1/n）。0 = 既定
        internal static bool DebugBloomReuse;   // 計測用：前に求めた Bloom を必ず使い回す
        internal void BenchPostVariant(int div, bool reuse)
        {
            DebugBloomDiv = div;
            DebugBloomReuse = reuse;
            BenchPost();
            DebugBloomDiv = 0;
            DebugBloomReuse = false;
        }

        internal void BenchPostNoBloom()
        {
            float intensity = m_bloomIntensity;
            m_bloomIntensity = 0f;
            BenchPost();
            m_bloomIntensity = intensity;
        }

        internal void BenchCopy()
        {
            if (m_compositeTexture == null || m_turnCache == null || m_turnCache.width != m_compositeTexture.width ||
                m_turnCache.height != m_compositeTexture.height || m_turnCache.format != m_compositeTexture.format) return;
            Graphics.CopyTexture(m_compositeTexture, m_turnCache);
        }

        internal void BenchPost()
        {
            if (m_postMaterial == null || m_outputTexture == null) return;
            EnsureCompositeTexture();
            RenderPost();
        }

        // ------------------------------------------------------------------
        // ページの中身だけの更新（線を引いている間など）。
        // 静止した見開きは、ページの中身 c に対して「出力 = K(位置) × Print(c) + S(位置)」の形になっている。
        // 止まっている間に、中身を真っ黒・真っ白に差し替えた静止画（m_liveZero・m_liveOne）を今までのパスで描いておき、
        // 中身が変わったら、撮影テクスチャを読んでその2枚の間を補うだけの小さなパス（11）を、変わったページの範囲にだけ描く。
        // 2枚は見た目に関わる状態（ComputeSignature）ごとに作り直す。作り終えるまでの変化は溜めておき、できてから反映する
        // ------------------------------------------------------------------
        private const int PassLiveDelta = 11;
        private const int ContentDeltaIdle = 0, ContentDeltaBuilding = 1, ContentDeltaApplied = 2;
        private static readonly int s_bookLiveZeroTex = Shader.PropertyToID("_BookLiveZeroTex");
        private static readonly int s_bookLiveOneTex = Shader.PropertyToID("_BookLiveOneTex");
        private static readonly int s_inkBleed = Shader.PropertyToID("_InkBleed");
        private RenderTexture m_liveZero, m_liveOne, m_liveBase;
        private int m_liveStep = -1;   // -1：まだ描いていない（見た目が変わったら -1 に戻す）
        private int m_liveSteps = 1;   // 1枚を何フレームに分けて描くか
        private Hash128 m_liveSignature;
        private bool m_contentDeltaWanted; // 中身だけの変化が1度でも伝えられた（それまでは準備の絵を描かない）
        private bool m_dirtyNewLeft, m_dirtyNewRight, m_dirtyOldLeft, m_dirtyOldRight;
        private int m_dirtyNewFrame = -1;
        private int m_contentDeltaCount;

        /// <summary>ページの中身だけの更新（軽いパス）で描いた回数（計測用）</summary>
        public int ContentDeltaCount => m_contentDeltaCount;

        // BookPageView の下の Graphic が変わった：表示中の見開きを撮影しているページなら、その側を更新する
        private void OnPageContentChanged(BookPageView view)
        {
            if (!m_contentDelta || !m_displayFlag || m_current == null || view == null) return;
            Camera camera = view.CaptureCamera;
            if (camera == null || !camera.isActiveAndEnabled || camera.targetTexture != m_current) return;
            RectTransform page = (RectTransform)view.transform;
            Vector3 viewport = camera.WorldToViewportPoint(page.TransformPoint(page.rect.center));
            MarkContentDirty(viewport.x >= m_rightCrop.x);
        }

        // 軽いパスで更新できる状態か（できないときは見開き全体の描き直しに任せる）
        private bool CanContentDelta()
        {
            if (!m_contentDelta || !m_redrawOnlyOnChange || m_postMaterial == null || m_runtimeMaterial == null) return false;
            if (m_turning || m_currentCover != BookCoverState.Open || m_current == null) return false;
            // 暗転・魔法の縁取りは中身の上に重なる。インクのにじみは周りの画素も読む
            if (m_dim > 0f || m_dimRoutine != null || (m_hasMagicStyle ? m_magicStrength : m_baseMagicStrength) > 0f) return false;
            // 準備の絵は 1 を超える明るさ（つや）を持つので、HDR の形式が要る
            if (HdrFormat != RenderTextureFormat.ARGBHalf) return false;
            return m_runtimeMaterial.passCount > PassLiveDelta && m_runtimeMaterial.GetFloat(s_inkBleed) <= 0f;
        }

        // 前のフレームまでに伝えられた変化を取り出す（このフレームに伝えられた分は、まだ撮影されていない）
        private bool TakeContentDirty(out bool left, out bool right)
        {
            if (m_dirtyNewFrame != Time.frameCount)
            {
                m_dirtyOldLeft |= m_dirtyNewLeft;
                m_dirtyOldRight |= m_dirtyNewRight;
                m_dirtyNewLeft = m_dirtyNewRight = false;
            }
            left = m_dirtyOldLeft;
            right = m_dirtyOldRight;
            m_dirtyOldLeft = m_dirtyOldRight = false;
            return left || right;
        }

        // 今は反映できない：次のフレームへ持ち越す
        private void HoldContentDirty(bool left, bool right)
        {
            m_dirtyOldLeft |= left;
            m_dirtyOldRight |= right;
        }

        // 止まっている間に呼ぶ。戻り値：ContentDeltaApplied 出力まで描いた / ContentDeltaBuilding 準備の絵を描いた / ContentDeltaIdle 何もしていない
        private int UpdateContentDelta(bool dirty, bool left, bool right)
        {
            if (!m_contentDeltaWanted) return ContentDeltaIdle;
            // 出力の合成に、今の状態の静止した見開きが残っていること
            bool still = m_compositeTexture != null && m_compositeTexture.IsCreated() &&
                         m_lastDrawTarget == m_compositeTexture && !m_lastDrawWasTurn;
            if (!still || !CanContentDelta())
            {
                // 使えない状態：見開き全体を描き直す
                if (dirty) MarkDirty();
                return ContentDeltaIdle;
            }
            Hash128 signature = ComputeSignature(true);
            signature.Append(m_compositeTexture.GetInstanceID());
            signature.Append(m_compositeTexture.width);
            signature.Append(m_compositeTexture.height);
            int steps = UsesSplitPasses ? ProgressSteps * 2 : 1;
            if (m_liveStep >= 0 && (signature != m_liveSignature || steps != m_liveSteps)) m_liveStep = -1;
            if (m_liveStep < 0 || m_liveStep < m_liveSteps * 2)
            {
                // 準備の絵がまだ：変化は溜めておき、1フレームに帯を1つ描く
                if (dirty) HoldContentDirty(left, right);
                if (m_liveStep < 0)
                {
                    m_liveStep = 0;
                    m_liveSteps = steps;
                    m_liveSignature = signature;
                }
                RenderTexture zero = m_liveZero, one = m_liveOne;
                m_liveZero = EnsureCacheTexture(m_liveZero, m_compositeTexture, "BookLiveZero", HdrFormat);
                m_liveOne = EnsureCacheTexture(m_liveOne, m_compositeTexture, "BookLiveOne", HdrFormat);
                m_liveZero.filterMode = FilterMode.Point;
                m_liveOne.filterMode = FilterMode.Point;
                // 作り直した（大きさが変わった）：前に描いた帯は消えているので始めから
                if (zero != m_liveZero || one != m_liveOne) m_liveStep = 0;
                // 変化が待っている間は、1フレームに帯を6つ描いて早く終える（中身の変化が画面に出るまでの待ちを短くする）
                int strips = m_dirtyOldLeft || m_dirtyOldRight || m_dirtyNewLeft || m_dirtyNewRight ? 6 : 1;
                for (int s = 0; s < strips && m_liveStep < m_liveSteps * 2; s++)
                {
                    bool white = m_liveStep >= m_liveSteps;
                    int index = m_liveStep % m_liveSteps;
                    DrawContentDeltaStrip(white ? m_liveOne : m_liveZero, white ? Texture2D.whiteTexture : Texture2D.blackTexture,
                        new Rect(0f, (float)index / m_liveSteps, 1f, 1f / m_liveSteps));
                    m_liveStep++;
                }
                if (NotesEnabled) Note(" liveBuild" + m_liveStep);
                return ContentDeltaBuilding;
            }
            if (!dirty) return ContentDeltaIdle;

            // モバイル：左右のページが同時に変わり続けるとき（見開き全体の演出）は、1フレームに片側ずつ交互に描く
            if (left && right && m_staggerPagesMobile && UsesMobileSettings)
            {
                bool leftNow = (Time.frameCount & 1) == 0;
                HoldContentDirty(!leftNow, leftNow);
                left = leftNow;
                right = !leftNow;
            }

            // 変わったページの範囲だけを描く
            Rect rect = BookToOutputRect(left ? -1f : 0f, right ? 1f : 0f, 0f, m_pageAspect);
            ApplyProperties();
            Material material = m_runtimeMaterial;
            material.SetTexture(s_bookLiveZeroTex, m_liveZero);
            material.SetTexture(s_bookLiveOneTex, m_liveOne);
            DrawRect(material, PassLiveDelta, m_compositeTexture, rect);
            // 後処理も変わった範囲だけ（画面フェードが動いたフレームは全体）
            bool fadeChanged = !Mathf.Approximately(m_lastPostFade, m_screenFade);
            m_lastPostFade = m_screenFade;
            m_postSource = null;
            m_postRect = fadeChanged ? (Rect?)null : rect;
            RenderPost();
            m_postRect = null;
            m_contentDeltaCount++;
            Note(" liveDelta");
            return ContentDeltaApplied;
        }

        // 準備の絵の帯を1つ描く：今の静止した見開きを、ページの中身だけ page（真っ黒・真っ白）に差し替えて描く
        private void DrawContentDeltaStrip(RenderTexture target, Texture page, Rect strip)
        {
            ApplyProperties();
            Material material = m_runtimeMaterial;
            material.SetTexture(s_curLeftTex, page);
            material.SetTexture(s_curRightTex, page);
            material.SetTexture(s_dstLeftTex, page);
            material.SetTexture(s_dstRightTex, page);
            if (!UsesSplitPasses)
            {
                DrawRect(material, PassFull, target, strip);
            }
            else
            {
                EnsurePaperLut(material);
                if (m_coverBaseTexture == null)
                {
                    m_coverBaseTexture = new RenderTexture(1, 1, 0, HdrFormat) { name = "BookCoverBase", hideFlags = HideFlags.HideAndDontSave };
                    m_coverBaseTexture.Create();
                }
                Graphics.Blit((Texture)null, m_coverBaseTexture, material, PassCoverBase);
                material.SetTexture(s_bookCoverBaseTex, m_coverBaseTexture);
                m_liveBase = EnsureCacheTexture(m_liveBase, target, "BookLiveBase", HdrFormat);
                m_liveBase.filterMode = FilterMode.Point;
                ApplyStillKeyword(material);
                DrawRect(material, PassOutside, m_liveBase, strip);
                DrawRect(material, PassPage, m_liveBase, strip);
                DrawRect(material, PassCover, m_liveBase, strip);
                material.SetTexture(s_bookBaseTex, m_liveBase);
                DrawRect(material, PassFinish, target, strip);
                material.SetTexture(s_bookBaseTex, null);
            }
            // 撮影テクスチャへ戻す
            material.SetTexture(s_curLeftTex, m_current);
            material.SetTexture(s_curRightTex, m_current);
            material.SetTexture(s_dstLeftTex, m_current);
            material.SetTexture(s_dstRightTex, m_current);
        }

        // ------------------------------------------------------------------
        // 描き直しの判定
        // ------------------------------------------------------------------
        private bool NeedsRedraw()
        {
            if (!m_redrawOnlyOnChange) return true;
            // 時間で動く演出の間は毎フレーム描く。終わった次のフレームは必ず描き直す（ドラッグを戻した場合など、
            // 状態が始める前と同じでも、出力にはめくりの途中の絵が残っているため）
            if (m_turning || m_dimRoutine != null || (m_screenFadeRoutine != null && m_postMaterial == null))
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
            if (NotesEnabled)
            {
                // 何が変わったか（計測用）
                int current = m_current != null ? m_current.GetInstanceID() : 0;
                string reason = !m_hasRendered ? "first" : "";
                if (m_contentVersion != m_debugVersion) reason += " version";
                if (current != m_debugCurrent) reason += " spread";
                if (m_bookPose != m_debugPose) reason += " pose";
                if (m_bookRect != m_debugRect) reason += " rect";
                if (!Mathf.Approximately(m_readingProgress, m_debugProgress)) reason += " progress";
                if (reason.Length == 0) reason = " other";
                DebugRedrawReason += reason;
                m_debugVersion = m_contentVersion;
                m_debugCurrent = current;
                m_debugPose = m_bookPose;
                m_debugRect = m_bookRect;
                m_debugProgress = m_readingProgress;
            }
            m_lastSignature = signature;
            m_hasRendered = true;
            if (m_idleRefreshRate > 0f) m_nextIdleRefresh = Time.unscaledTime + 1f / m_idleRefreshRate;
            return true;
        }

        // 見た目に関わる状態の要約。ここに無い値を変えたら MarkDirty を呼ぶ
        // stillOnly：めくりの途中だけ変わる値（移動先・角の位置など）を含めない（止まっていた状態の見た目だけ）
        private Hash128 ComputeSignature(bool stillOnly = false)
        {
            Hash128 h = new Hash128();
            h.Append(m_contentVersion);
            h.Append(m_current != null ? m_current.GetInstanceID() : 0);
            h.Append(m_frontCover != null ? m_frontCover.GetInstanceID() : 0);
            h.Append(m_backCover != null ? m_backCover.GetInstanceID() : 0);
            h.Append(m_outputTexture != null ? m_outputTexture.GetInstanceID() : 0);
            h.Append((int)m_currentCover);
            if (!stillOnly)
            {
                h.Append(m_destination != null ? m_destination.GetInstanceID() : 0);
                h.Append((int)m_destinationCover);
                h.Append(m_turnSide);
                h.Append((int)m_corner);
                h.Append(ref m_foldPoint);
                h.Append(m_turnAmount);
                h.Append(m_turnLeaves);
            }
            h.Append(ref m_bookRect);
            h.Append(m_pageAspect);
            h.Append(m_readingProgress);
            h.Append(m_progressIndex);
            h.Append(m_progressCount);
            if (m_postMaterial == null) h.Append(m_screenFade); // 後処理があればフェードは後処理だけ（LateUpdate）
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
            if (paused) return;
            MarkDirty();
            // RenderTexture の中身が消えていることがあるので、焼き込み・静止画をすべて作り直させる
            m_paperLutSignature = new Hash128();
            m_targetHoldsIdle = null;
            m_hasEverDrawn = false;
            m_progressActive = false;
            m_progressPending = false;
            m_turnCacheValid = false;
            m_coverCacheValid = false;
            m_coverPreStep = -1;
            m_dimBaseValid = false;
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
        // モバイル：Bloom（縮小・拡大の連鎖）を2フレームに分けて求め、その間は前の結果を使う
        private RenderTexture m_bloomKept;
        private int m_bloomWorkFrame = -10;
        private int m_bloomPending;     // 縮小まで済んだ段数（m_bloomMips に残っている）。0 = 無し
        private bool m_bloomReused;     // 今出ている Bloom は前のフレームの絵から求めたもの
        private bool m_bloomForceFull;  // 次の後処理は Bloom を1度に求める
        private int m_postFrame = -1;
        private int m_lastDrawPostFrame = -10; // 最後に（仕上げ以外で）後処理をしたフレーム

        private void ReleasePendingBloom()
        {
            for (int i = 0; i < m_bloomMips.Length; i++)
            {
                if (m_bloomMips[i] != null) RenderTexture.ReleaseTemporary(m_bloomMips[i]);
                m_bloomMips[i] = null;
            }
            m_bloomPending = 0;
        }

        private void ReleaseKeptBloom()
        {
            ReleasePendingBloom();
            if (m_bloomKept != null) RenderTexture.ReleaseTemporary(m_bloomKept);
            m_bloomKept = null;
            m_bloomReused = false;
        }

        // 明るい部分の取り出しと縮小。段数を返す（m_bloomMips[0..段数-1] に残る）
        private int BloomDownChain(Material post, RenderTexture source, int bloomWidth, int bloomHeight, int iterations,
            RenderTextureFormat bloomFormat)
        {
            int count = 0;
            m_bloomMips[count] = RenderTexture.GetTemporary(bloomWidth, bloomHeight, 0, bloomFormat);
            Graphics.Blit(source, m_bloomMips[count++], post, PassPrefilter);
            // モバイルの簡略化：1/4 ずつ縮める（段を1つ飛ばす）。描画の切り替えの回数が減る
            bool skip = UsesMobileSettings && m_bloomSkipLevelMobile;
            int div = skip ? 4 : 2;
            if (skip) iterations = Mathf.Min(iterations, 2);
            post.SetFloat(s_downSpread, skip ? 1f : 0.5f);
            for (int w = bloomWidth / div, h = bloomHeight / div; count < iterations && w >= 8 && h >= 8; w /= div, h /= div)
            {
                m_bloomMips[count] = RenderTexture.GetTemporary(w, h, 0, bloomFormat);
                Graphics.Blit(m_bloomMips[count - 1], m_bloomMips[count], post, PassDown);
                count++;
            }
            post.SetFloat(s_downSpread, 0.5f);
            return count;
        }

        // 縮小した絵を小さい方から拡大して足し合わせる。結果を返す（m_bloomMips は空になる）
        private RenderTexture BloomUpChain(Material post, int count, RenderTextureFormat bloomFormat)
        {
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
            RenderTexture bloom = m_bloomMips[0];
            m_bloomMips[0] = null;
            return bloom;
        }

        private void RenderPost()
        {
            m_postFrame = Time.frameCount;
            // 前のフレームにも後処理をした（連続して描いている）ときだけ、Bloom を2フレームに分ける
            bool continuous = m_lastDrawPostFrame == Time.frameCount - 1 || m_lastDrawPostFrame == Time.frameCount;
            if (!m_bloomForceFull) m_lastDrawPostFrame = Time.frameCount;
            Material post = m_postMaterial;
            RenderTexture source = m_postSource != null ? m_postSource : m_compositeTexture;
            RenderTextureFormat format = HdrFormat;
            int width = Mathf.Max(1, source.width / 2);
            int height = Mathf.Max(1, source.height / 2);
            // Bloom：モバイルは 1/4 の大きさから始め、段数を抑える。縮小・拡大の画像は軽い形式
            bool mobile = UsesMobileSettings;
            RenderTextureFormat bloomFormat = BloomFormat;
            int bloomDiv = DebugBloomDiv > 0 ? DebugBloomDiv : mobile ? 4 : 2;
            int bloomWidth = Mathf.Max(1, source.width / bloomDiv);
            int bloomHeight = Mathf.Max(1, source.height / bloomDiv);
            int iterations = mobile ? Mathf.Min(m_bloomIterations, m_bloomIterationsMobile) : m_bloomIterations;

            RenderTexture bloom = null;
            // 前のフレームに求めた Bloom がある（連続して描いている）：それを使い、次のフレームに求め直す
            // モバイル：Bloom を2フレームに分けて求める（1フレーム目に明るい部分の取り出しと縮小、2フレーム目に拡大）。
            // その間は前に求めた Bloom を重ねる（光のにじみが1〜2フレーム遅れる）
            bool keepBloom = mobile && m_bloomHalfRateMobile && m_bloomIntensity > 0f;
            if (!keepBloom && (m_bloomKept != null || m_bloomPending > 0)) ReleaseKeptBloom();
            if (keepBloom && DebugBloomReuse && m_bloomKept != null)
            {
                bloom = m_bloomKept;
            }
            else if (m_bloomIntensity > 0f)
            {
                post.SetVector(s_bloomThreshold, new Vector4(m_bloomThreshold, m_bloomKnee * Mathf.Max(m_bloomThreshold, 1e-3f),
                    m_bloomClamp, m_bloomScatter));
                // モバイルの簡略化：最初の縮小を1点読みにする（HDR の合成結果を全部読み直すと、その転送だけで数 ms かかる）
                post.SetFloat(s_prefilterSpread, mobile ? (m_bloomLightPrefilterMobile ? 0f : 1f) : 0.5f);
                // 分けて求められるのは、前の Bloom があって、同じフレームの描き直しでも、止まった後の仕上げでもないとき
                bool split = keepBloom && continuous && !m_bloomForceFull && m_bloomKept != null && m_bloomKept.IsCreated() &&
                             Time.frameCount != m_bloomWorkFrame &&
                             m_bloomKept.width == bloomWidth && m_bloomKept.height == bloomHeight;
                bool pendingReady = m_bloomPending > 0 && Time.frameCount - m_bloomWorkFrame == 1 &&
                    m_bloomMips[0] != null && m_bloomMips[0].width == bloomWidth && m_bloomMips[0].height == bloomHeight;
                if (m_bloomForceFull && keepBloom && pendingReady && m_bloomKept != null)
                {
                    // 仕上げ：前のフレーム（最後に描いた絵）の縮小が残っている。拡大だけすれば今の絵の Bloom になる
                    RenderTexture result = BloomUpChain(post, m_bloomPending, bloomFormat);
                    m_bloomPending = 0;
                    RenderTexture.ReleaseTemporary(m_bloomKept);
                    m_bloomKept = result;
                    bloom = result;
                    m_bloomReused = false;
                }
                else if (split && pendingReady)
                {
                    // 2フレーム目：前のフレームに縮小した絵を拡大して、新しい Bloom にする
                    RenderTexture result = BloomUpChain(post, m_bloomPending, bloomFormat);
                    m_bloomPending = 0;
                    RenderTexture.ReleaseTemporary(m_bloomKept);
                    m_bloomKept = result;
                    bloom = result;
                    m_bloomReused = true; // 前のフレームの絵から求めた Bloom：描き直しが止まったら求め直す
                }
                else if (split)
                {
                    // 1フレーム目：取り出しと縮小だけ。重ねるのは前の Bloom
                    ReleasePendingBloom();
                    m_bloomPending = BloomDownChain(post, source, bloomWidth, bloomHeight, iterations, bloomFormat);
                    bloom = m_bloomKept;
                    m_bloomReused = true;
                }
                else
                {
                    ReleasePendingBloom();
                    int count = BloomDownChain(post, source, bloomWidth, bloomHeight, iterations, bloomFormat);
                    bloom = BloomUpChain(post, count, bloomFormat);
                    if (keepBloom)
                    {
                        if (m_bloomKept != null) RenderTexture.ReleaseTemporary(m_bloomKept);
                        m_bloomKept = bloom;
                        m_bloomReused = false;
                    }
                }
                m_bloomWorkFrame = Time.frameCount;
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
            if (m_postRect is Rect postRect)
            {
                // 合成が変わった範囲だけ（Bloom は全体から求め直している）。Bloom の広がりの分だけ少し広げる
                post.SetTexture(s_mainTex, source);
                Rect grown = Rect.MinMaxRect(Mathf.Max(0f, postRect.xMin - 0.04f), Mathf.Max(0f, postRect.yMin - 0.06f),
                                             Mathf.Min(1f, postRect.xMax + 0.04f), Mathf.Min(1f, postRect.yMax + 0.06f));
                DrawRect(post, PassComposite, m_outputTexture, grown);
            }
            else Graphics.Blit(source, m_outputTexture, post, PassComposite);

            if (bloom != null && bloom != m_bloomKept) RenderTexture.ReleaseTemporary(bloom);
            if (blur != null) RenderTexture.ReleaseTemporary(blur);
        }

        private void ApplyProperties()
        {
            UpdateLayout();
            Material material = m_runtimeMaterial;
            Texture destination = m_turning && m_destination != null ? m_destination : m_current;
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
            Vector2 destinationKinds = SlotKinds(destinationCover, m_turning ? m_destination : m_current);
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
                int destinationIndex = Mathf.Clamp(m_progressIndex + (m_turnSide > 0f ? 1 : -1) * m_turnLeaves, 0, m_progressCount - 1);
                float lift = m_turnAmount, land = m_turnAmount;
                if (m_turnLeaves > 1)
                {
                    // 束：めくる側の小口は持ち上げた時点で減り、着く側の小口は束が着地するときに増える
                    lift = Mathf.Clamp01(m_turnAmount * 6f);
                    land = Mathf.SmoothStep(0f, 1f, (m_turnAmount - 0.8f) / 0.2f);
                }
                bool fromRight = m_turnSide > 0f;
                left = Mathf.Lerp(left, StackThickness(destinationIndex, m_destination, false), fromRight ? land : lift);
                right = Mathf.Lerp(right, StackThickness(destinationIndex, m_destination, true), fromRight ? lift : land);
            }
            // 閉じた本は、すべてのページが表紙の下に重なる。ページの束を見せない
            // （硬い表紙をめくっている間は、シェーダーが開き具合に合わせて束を出し入れする）
            if (boardState != BookCoverState.Open && !rigidTurn) { left = 0f; right = 0f; }
            material.SetVector(s_stack, new Vector4(left, right, m_stackLayers, m_stackDrop));
            // まとめてめくる束の厚み：めくる紙の外形の外側に小口の帯、落ち影とカールを広げる。1枚なら 0（従来と同じ）
            Vector4 bundle = Vector4.zero;
            if (pageTurn && m_turnLeaves > 1)
            {
                float full = Mathf.Min((m_turnLeaves - 1) * m_leafThickness * m_bundleThicknessScale, m_stackThickness);
                float layers = Mathf.Max(1f, Mathf.Round(m_stackLayers * full / Mathf.Max(m_stackThickness, 1e-5f)));
                float t = full * Mathf.Clamp01(m_turnAmount * 8f); // 持ち上げ始めに帯が急に現れない
                bundle = new Vector4(t, layers, t * m_bundleShadowGrow, t * m_bundleCurlGrow);
            }
            material.SetVector(s_turnThickness, bundle);
            material.SetFloat(s_turnBandDrop, m_stackDrop);

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
            ApplyPoseLighting(material);
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
            CompareOverride?.Invoke(material);
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

        private void OnEnable()
        {
            BookPageView.ContentChanged -= OnPageContentChanged;
            BookPageView.ContentChanged += OnPageContentChanged;
        }

        private void OnDisable()
        {
            BookPageView.ContentChanged -= OnPageContentChanged;
            ReleaseKeptBloom();
            StopDisplay();
            m_dimRoutine = null;
            m_screenFadeRoutine = null;
        }

        private void OnDestroy()
        {
            if (m_runtimeMaterial != null) ReleaseObject(m_runtimeMaterial);
            if (m_postMaterial != null) ReleaseObject(m_postMaterial);
            ReleasePaperLut();
            if (m_turnCache != null) { m_turnCache.Release(); ReleaseObject(m_turnCache); m_turnCache = null; }
            if (m_turnDest != null) { m_turnDest.Release(); ReleaseObject(m_turnDest); m_turnDest = null; }
            if (m_turnDestLow != null) { m_turnDestLow.Release(); ReleaseObject(m_turnDestLow); m_turnDestLow = null; }
            if (m_dimBase != null) { m_dimBase.Release(); ReleaseObject(m_dimBase); m_dimBase = null; }
            if (m_coverCur != null) { m_coverCur.Release(); ReleaseObject(m_coverCur); m_coverCur = null; }
            if (m_animTexture != null) { m_animTexture.Release(); ReleaseObject(m_animTexture); m_animTexture = null; }
            if (m_coverDest != null) { m_coverDest.Release(); ReleaseObject(m_coverDest); m_coverDest = null; }
            if (m_stripBase != null) { m_stripBase.Release(); ReleaseObject(m_stripBase); m_stripBase = null; }
            if (m_coverPre != null) { m_coverPre.Release(); ReleaseObject(m_coverPre); m_coverPre = null; }
            if (m_stripBaseLow != null) { m_stripBaseLow.Release(); ReleaseObject(m_stripBaseLow); m_stripBaseLow = null; }
            if (m_coverDestLow != null) { m_coverDestLow.Release(); ReleaseObject(m_coverDestLow); m_coverDestLow = null; }
            if (m_syncTexture != null) { m_syncTexture.Release(); ReleaseObject(m_syncTexture); m_syncTexture = null; }
            if (m_syncReadback != null) { ReleaseObject(m_syncReadback); m_syncReadback = null; }
            if (m_liveZero != null) { m_liveZero.Release(); ReleaseObject(m_liveZero); m_liveZero = null; }
            if (m_liveOne != null) { m_liveOne.Release(); ReleaseObject(m_liveOne); m_liveOne = null; }
            if (m_liveBase != null) { m_liveBase.Release(); ReleaseObject(m_liveBase); m_liveBase = null; }
            if (m_progressBase != null) { m_progressBase.Release(); ReleaseObject(m_progressBase); m_progressBase = null; }
            if (m_progressBuffer != null) { m_progressBuffer.Release(); ReleaseObject(m_progressBuffer); m_progressBuffer = null; }
            if (m_coverBaseTexture != null)
            {
                m_coverBaseTexture.Release();
                ReleaseObject(m_coverBaseTexture);
                m_coverBaseTexture = null;
            }
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
