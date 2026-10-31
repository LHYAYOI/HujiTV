// Dedicated fork; provenance recorded in PROJECT_STATUS.md.
using hatake;
using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Presentation only: inventory changes are supplied by BookGameController.
namespace YasudaHaruto.BookValidation
{
    public sealed class YasudaBookViewController : MonoBehaviour
    {
        [Header("ページ表示")]
        [SerializeField] private BookPageView m_leftPageView;
        [SerializeField] private BookPageView m_rightPageView;
        [Header("撮影")]
        [SerializeField] private Camera m_captureCamera;
        [SerializeField] private RenderTexture m_textureA;
        [SerializeField] private RenderTexture m_textureB;
        [Header("画面表示")]
        [SerializeField] private RawImage m_displayImage;
        [Header("3D表示（未設定なら従来の2D表示）")]
        [SerializeField] private BookModelView m_modelView;
        [Header("シェーダー合成のめくり表示（設定するとこちらを優先）")]
        [SerializeField] private BookFlipView m_flipView;
        [Header("表紙（Flip View のみ）")]
        [Tooltip("最初の見開きの前に表紙、最後の見開きの後に裏表紙を置き、閉じた本にめくれるようにする")]
        [SerializeField] private bool m_useCovers = true;
        [Tooltip("表紙を閉じた状態で始める")]
        [SerializeField] private bool m_startClosed = true;
        [Tooltip("最初の見開きの左を表紙の内側にする（データの最初のページは右から始まる）。最後の見開きの右が空なら裏表紙の内側にする")]
        [SerializeField] private bool m_coverInsides = true;
        [Tooltip("ON：最初の見開きで「前へ」、最後で「次へ」をすると閉じる。OFF（既定）：閉じるのは CloseBook だけ")]
        [SerializeField] private bool m_autoCloseAtEnds;
        [Header("めくり中のページ更新")]
        [Tooltip("めくり・燃焼・復活の間も、元の見開き（持っているページ）を撮影し続ける。撮影Cameraと撮影用Canvasを実行時に複製する")]
        [SerializeField] private bool m_liveTurningPages = true;

        private int m_spreadIndex;
        private bool m_captureFlag;
        private RenderTexture m_currentTexture;
        private RenderTexture m_previousTexture;

        public RenderTexture CurrentTexture => m_currentTexture;
        public RenderTexture PreviousTexture => m_previousTexture;
        public RawImage DisplayImage => m_displayImage;
        public BookModelView ModelView => m_flipView != null ? null : m_modelView;
        public BookFlipView FlipView => m_flipView;
        public int SpreadCount => Mathf.Max(1, (PageCount + 1) / 2);
        public bool CoversEnabled => m_useCovers && m_flipView != null;
        public BookCoverState CoverState => m_coverState;
        public bool IsClosed => m_coverState != BookCoverState.Open;
        /// <summary>表紙の開閉が終わったとき</summary>
        public event Action<BookCoverState> CoverChanged;
        public bool CanTurnForward => !IsBusy && (m_coverState == BookCoverState.FrontCover ||
            (m_coverState == BookCoverState.Open && (m_spreadIndex + 1 < SpreadCount || (CoversEnabled && m_autoCloseAtEnds))));
        public bool CanTurnBackward => !IsBusy && (m_coverState == BookCoverState.BackCover ||
            (m_coverState == BookCoverState.Open && (m_spreadIndex > 0 || (CoversEnabled && m_autoCloseAtEnds))));
        public bool AutoCloseAtEnds { get => m_autoCloseAtEnds; set => m_autoCloseAtEnds = value; }
        public int CurrentSpread => m_spreadIndex;
        public bool IsBusy => m_captureFlag;
        public bool IsReady => m_inventoryInitialized && isActiveAndEnabled && !IsBusy &&
            m_flipView != null && !m_flipView.IsTurning;
        private bool m_inventoryInitialized;
        public int PageCount => m_runtimePages.Count;
        public event Action<int> SpreadShown;
        private readonly List<BookPageData> m_runtimePages = new List<BookPageData>();
        private Action<bool> m_inventoryCompletion;

        public bool TryBeginPageDrag(Vector2 point, BookFlipCorner? corner, bool closeAtEnds)
        {
            bool forward = point.x >= 0f;
            if (!IsReady || !(closeAtEnds ? CanFlick(forward) : forward ? CanTurnForward : CanTurnBackward))
                return false;
            m_flipView.BeginDrag(point, corner);
            if (closeAtEnds) Flick(forward);
            else if (forward) ShowNextSpread();
            else ShowPreviousSpread();
            if (IsBusy) return true;
            m_flipView.CancelDrag();
            return false;
        }

        public void UpdatePageDrag(Vector2 point) => m_flipView?.UpdateDrag(point);
        public void EndPageDrag() => m_flipView?.EndDrag();

        // The inventory owner supplies a complete candidate; no partial pair edits escape.
        internal bool TryApplyInventory(IReadOnlyList<BookPageData> pages, int targetSpread, Action<bool> completed)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsBusy || IsClosed ||
                m_currentTexture == null || m_flipView == null || m_flipView.IsTurning ||
                pages == null || pages.Count < 3 || (pages.Count & 1) == 0 ||
                targetSpread < 0 || targetSpread >= (pages.Count + 1) / 2 ||
                pages[0] != null || pages[1] != GetPage(1) ||
                pages[pages.Count - 1] != GetPage(PageCount - 1)) return false;
            for (int i = 1; i < pages.Count; i++) if (pages[i] == null) return false;
            var candidate = new List<BookPageData>(pages.Count);
            for (int i = 0; i < pages.Count; i++) candidate.Add(pages[i]);
            m_inventoryCompletion = completed;
            StartCoroutine(ApplyInventory(candidate, targetSpread));
            return true;
        }

        private IEnumerator ApplyInventory(List<BookPageData> candidate, int targetSpread)
        {
            m_captureFlag = true;
            RenderTexture target = m_currentTexture == m_textureA ? m_textureB : m_textureA;
            int left = targetSpread * 2;
            m_leftPageView.ShowPage(candidate[left]);
            m_rightPageView.ShowPage(left + 1 < candidate.Count ? candidate[left + 1] : null);
            m_captureCamera.targetTexture = target;
            m_captureCamera.enabled = true;
            Canvas.ForceUpdateCanvases();
            yield return WaitForCameraFrame(m_captureCamera);
            TagCoverInsides(target, targetSpread, candidate.Count);

            // Commit both display inventory and logical inventory without a yield between them.
            m_runtimePages.Clear();
            m_runtimePages.AddRange(candidate);
            m_previousTexture = m_currentTexture;
            m_currentTexture = target;
            m_spreadIndex = targetSpread;
            m_flipView.SetDisplay(m_displayImage);
            m_flipView.ShowSpread(target);
            m_flipView.SetReadingProgress(targetSpread, SpreadCount);
            m_displayImage.texture = m_flipView.OutputTexture;
            m_displayImage.enabled = true;
            var completion = m_inventoryCompletion;
            m_inventoryCompletion = null;
            // Keep IsBusy true while the owner commits; prevent reentrant edits.
            completion?.Invoke(true);
            m_captureFlag = false;
            SpreadShown?.Invoke(targetSpread);
            ResumePendingSpread();
        }
        private BookCoverState m_coverState;
        // 最初の見開きの左（index 0）に置いた、表紙の内側の枠（ページではない）
        private bool m_frontInsideSlot;
        private Camera m_liveCamera;
        private GameObject m_liveCanvas;
        private BookPageView m_liveLeftView;
        private BookPageView m_liveRightView;
        private int m_pendingSpread = -1;
        private int m_pendingPage = -1;
        private bool m_pendingInstant;
        public Image GetCurrentRuneImage(int imageIndex)
        {
            BookPageData page = GetPage(m_spreadIndex * 2);
            if (page == null || m_leftPageView == null || page.Images.Count == 0) return null;
            int index = imageIndex < 0 ? page.Images.Count - 1 : imageIndex;
            if (index >= page.Images.Count) return null;
            BookImageData data = page.Images[index];
            if (data == null || data.Sprite == null) return null;
            Image result = null;
            foreach (Image image in m_leftPageView.GetComponentsInChildren<Image>())
            {
                if (image.sprite != data.Sprite) continue;
                Vector2 expected = new Vector2(data.Position.x, -data.Position.y);
                if ((image.rectTransform.anchoredPosition - expected).sqrMagnitude < 0.01f &&
                    (image.rectTransform.sizeDelta - data.Size).sqrMagnitude < 0.01f) result = image;
            }
            return result;
        }

        private void Awake()
        {
            if (m_startClosed && CoversEnabled) m_coverState = BookCoverState.FrontCover;
            if (m_liveTurningPages && Application.isPlaying) CreateLiveRig();
        }

        // Called before the first capture; the game inventory is the only authoring source.
        internal bool InitializeInventory(IReadOnlyList<BookPageData> pages)
        {
            if (m_inventoryInitialized || !isActiveAndEnabled || !CoversEnabled || !m_coverInsides ||
                m_leftPageView == null || m_rightPageView == null || m_captureCamera == null ||
                !m_captureCamera.gameObject.activeInHierarchy || m_displayImage == null ||
                m_textureA == null || m_textureB == null || m_textureA == m_textureB ||
                pages == null || pages.Count < 3 || pages.Count % 2 == 0 || pages[0] != null ||
                !m_flipView.Initialize()) return false;
            for (int i = 1; i < pages.Count; i++) if (pages[i] == null) return false;
            m_runtimePages.Clear();
            for (int i = 0; i < pages.Count; i++) m_runtimePages.Add(pages[i]);
            m_frontInsideSlot = true;
            m_inventoryInitialized = true;
            ShowSpread(m_spreadIndex);
            return true;
        }

        public BookPageData GetPage(int index) => index >= 0 && index < PageCount ? m_runtimePages[index] : null;

        /// <summary>index が表紙の内側の枠（最初の見開きの左）か。ページではないので燃焼・挿入・削除できない</summary>
        public bool IsCoverInside(int pageIndex) => m_frontInsideSlot && pageIndex == 0;

        /// <summary>データ上のページ番号（BookData の順、0始まり）→ 本の中の番号</summary>
        public int ToBookIndex(int dataIndex) => dataIndex + (m_frontInsideSlot ? 1 : 0);

        // 撮影した見開きの左右が表紙・裏表紙の内側かを Flip View に伝える
        private void TagCoverInsides(RenderTexture texture, int spreadIndex, int pageCount)
        {
            if (m_flipView == null) return;
            int spreadCount = Mathf.Max(1, (pageCount + 1) / 2);
            bool left = m_frontInsideSlot && spreadIndex == 0;
            bool right = CoversEnabled && m_coverInsides && spreadIndex == spreadCount - 1 && spreadIndex * 2 + 1 >= pageCount;
            m_flipView.SetCoverInsides(texture, left, right);
        }

        private void ResumePendingSpread()
        {
            if (m_pendingSpread < 0) return;
            int pending = m_pendingSpread;
            bool instant = m_pendingInstant;
            m_pendingSpread = -1;
            m_pendingPage = -1;
            ShowSpreadInternal(pending, instant);
        }

        public void ShowPage(int index, bool instant = false)
        {
            if (index < 0 || index >= PageCount) return;
            if (IsBusy)
            {
                m_pendingSpread = index / 2;
                m_pendingPage = index;
                m_pendingInstant = instant;
                return;
            }
            ShowSpreadInternal(index / 2, instant);
        }

        private void OnEnable()
        {
            if (m_displayImage != null) m_displayImage.enabled = false;
            ShowSpread(m_spreadIndex);
        }

        public void ShowSpread(int spreadIndex)
        {
            ShowSpreadInternal(spreadIndex, false);
        }

        private void ShowSpreadInternal(int spreadIndex, bool instant)
        {
            if (!isActiveAndEnabled || !m_inventoryInitialized) return;
            if (spreadIndex < 0 || spreadIndex >= Mathf.Max(1, (PageCount + 1) / 2)) return;
            if (IsBusy) { m_pendingSpread = spreadIndex; m_pendingPage = spreadIndex * 2; m_pendingInstant = instant; return; }
            if (m_leftPageView == null ||
                m_rightPageView == null || m_captureCamera == null ||
                m_displayImage == null || m_textureA == null || m_textureB == null)
            {
                Debug.LogWarning("BookControllerの参照をすべて設定", this);
                return;
            }
            if (m_textureA == m_textureB)
            {
                Debug.LogError("Texture_A/Bには別々のRenderTextureが必要", this);
                return;
            }
            if (!m_captureCamera.gameObject.activeInHierarchy)
            {
                Debug.LogError("撮影CameraのGameObjectを有効に", this);
                return;
            }
            if (IsClosed && m_currentTexture != null)
            {
                // 閉じた本：その見開きを撮影してから表紙を開く（栞のジャンプなど）
                StartCoroutine(OpenRoutine(spreadIndex));
                return;
            }
            BookPageData leftPage = GetPage(spreadIndex * 2);
            BookPageData rightPage = GetPage(spreadIndex * 2 + 1);
            if (m_flipView != null) { if (!m_flipView.Initialize()) return; }
            else if (m_modelView != null && !m_modelView.Initialize()) return;
            StartCoroutine(CaptureSpread(spreadIndex, leftPage, rightPage, instant));
        }

        /// <summary>次へ。表紙を閉じていれば開く。最後の見開きでは Auto Close At Ends のときだけ裏表紙を閉じる</summary>
        public void ShowNextSpread()
        {
            if (CoversEnabled && !IsBusy)
            {
                if (m_coverState == BookCoverState.FrontCover) { OpenBook(); return; }
                if (m_coverState == BookCoverState.BackCover) return;
                if (m_spreadIndex + 1 >= SpreadCount)
                {
                    if (m_autoCloseAtEnds) CloseBook(false);
                    return;
                }
            }
            ShowSpread(m_spreadIndex + 1);
        }

        /// <summary>前へ。裏表紙を閉じていれば開く。最初の見開きでは Auto Close At Ends のときだけ表紙を閉じる</summary>
        public void ShowPreviousSpread()
        {
            if (CoversEnabled && !IsBusy)
            {
                if (m_coverState == BookCoverState.BackCover) { OpenBook(); return; }
                if (m_coverState == BookCoverState.FrontCover) return;
                if (m_spreadIndex <= 0)
                {
                    if (m_autoCloseAtEnds) CloseBook(true);
                    return;
                }
            }
            ShowSpread(m_spreadIndex - 1);
        }

        /// <summary>
        /// ドラッグ・フリック用：forward（右をつかむ）でめくれるか。端の見開きでは、表紙・裏表紙を閉じるめくりも含める
        /// （Auto Close At Ends に関係なく、手でめくれば閉じられる）
        /// </summary>
        public bool CanFlick(bool forward)
        {
            if (IsBusy) return false;
            if (m_coverState == BookCoverState.FrontCover) return forward;
            if (m_coverState == BookCoverState.BackCover) return !forward;
            return forward ? (m_spreadIndex + 1 < SpreadCount || CoversEnabled) : (m_spreadIndex > 0 || CoversEnabled);
        }

        /// <summary>ドラッグ・フリックでめくる。端の見開きでは本を閉じる</summary>
        public void Flick(bool forward)
        {
            if (!CanFlick(forward)) return;
            if (m_coverState != BookCoverState.Open) { OpenBook(); return; }
            if (forward && m_spreadIndex + 1 >= SpreadCount) { CloseBook(false); return; }
            if (!forward && m_spreadIndex <= 0) { CloseBook(true); return; }
            ShowSpread(m_spreadIndex + (forward ? 1 : -1));
        }

        /// <summary>閉じた本を開く（閉じる前の見開き）。戻り値は受付</summary>
        public bool OpenBook() => OpenBook(-1);

        /// <summary>閉じた本を targetSpread の見開きで開く（負なら閉じる前の見開き）。見えないところで撮影してから開く</summary>
        public bool OpenBook(int targetSpread)
        {
            if (!CoversEnabled || !IsClosed || IsBusy || m_currentTexture == null || !isActiveAndEnabled) return false;
            int target = targetSpread < 0 ? m_spreadIndex : Mathf.Clamp(targetSpread, 0, SpreadCount - 1);
            StartCoroutine(OpenRoutine(target));
            return true;
        }

        /// <summary>
        /// 本を完全に閉じる。front = true で表紙、false で裏表紙が上になる。
        /// 閉じた後、次に開く見開き（表紙なら最初、裏表紙なら最後）を見えないところで撮影しておく
        /// </summary>
        public bool CloseBook(bool front = true)
        {
            if (!CoversEnabled || IsClosed || IsBusy || m_currentTexture == null || !isActiveAndEnabled) return false;
            StartCoroutine(CloseRoutine(front));
            return true;
        }

        /// <summary>旧名。OpenBook() と同じ</summary>
        public bool OpenCover() => OpenBook();

        /// <summary>旧名。CloseBook(front) と同じ</summary>
        public bool CloseCover(bool front) => CloseBook(front);

        private IEnumerator OpenRoutine(int target)
        {
            m_captureFlag = true;
            if (target != m_spreadIndex) yield return CaptureHidden(target);
            PrepareFlipDisplay();
            BookCoverState cover = m_coverState;
            yield return m_flipView.PlayCoverTurn(m_currentTexture, cover, true);
            if (m_flipView.LastTurnCommitted) SetCoverState(BookCoverState.Open);
            m_captureFlag = false;
            if (!IsClosed) SpreadShown?.Invoke(m_spreadIndex);
            ResumePendingSpread();
        }

        private IEnumerator CloseRoutine(bool front)
        {
            m_captureFlag = true;
            PrepareFlipDisplay();
            BookCoverState cover = front ? BookCoverState.FrontCover : BookCoverState.BackCover;
            yield return m_flipView.PlayCoverTurn(m_currentTexture, cover, false);
            if (m_flipView.LastTurnCommitted)
            {
                SetCoverState(cover);
                int edge = front ? 0 : SpreadCount - 1;
                if (m_spreadIndex != edge) yield return CaptureHidden(edge);
            }
            m_captureFlag = false;
            ResumePendingSpread();
        }

        private void PrepareFlipDisplay()
        {
            m_flipView.SetDisplay(m_displayImage);
            m_displayImage.texture = m_flipView.OutputTexture;
            m_displayImage.enabled = true;
        }

        // 閉じている間に別の見開きを撮影して差し替える（画面は閉じた本のまま）
        private IEnumerator CaptureHidden(int spreadIndex)
        {
            RenderTexture targetTexture = m_currentTexture == m_textureA ? m_textureB : m_textureA;
            m_captureCamera.targetTexture = targetTexture;
            m_leftPageView.ShowPage(GetPage(spreadIndex * 2));
            m_rightPageView.ShowPage(GetPage(spreadIndex * 2 + 1));
            Canvas.ForceUpdateCanvases();
            m_captureCamera.enabled = true;
            yield return WaitForCameraFrame(m_captureCamera);
            TagCoverInsides(targetTexture, spreadIndex, PageCount);
            m_previousTexture = m_currentTexture;
            m_currentTexture = targetTexture;
            m_spreadIndex = spreadIndex;
            if (m_flipView != null)
            {
                if (IsClosed) m_flipView.ShowClosed(targetTexture, m_coverState == BookCoverState.FrontCover);
                else m_flipView.ShowSpread(targetTexture);
                m_flipView.SetReadingProgress(m_spreadIndex, SpreadCount);
            }
        }

        private void SetCoverState(BookCoverState state)
        {
            if (m_coverState == state) return;
            m_coverState = state;
            CoverChanged?.Invoke(state);
        }

        // ------------------------------------------------------------------
        // めくり中も元の見開きを撮影する（撮影Cameraと撮影用Canvasの複製）
        // 複製は遠くへずらし、元のCameraと互いに映らないようにする
        // ------------------------------------------------------------------
        private static readonly Vector3 s_liveOffset = new Vector3(10000f, 0f, 0f);

        private void CreateLiveRig()
        {
            if (m_leftPageView == null || m_rightPageView == null || m_captureCamera == null) return;
            Canvas leftCanvas = m_leftPageView.GetComponentInParent<Canvas>();
            Canvas rightCanvas = m_rightPageView.GetComponentInParent<Canvas>();
            if (leftCanvas == null || rightCanvas == null || leftCanvas.rootCanvas != rightCanvas.rootCanvas) return;
            Canvas canvas = leftCanvas.rootCanvas;
            Transform canvasRoot = canvas.transform;
            bool cameraInside = m_captureCamera.transform.IsChildOf(canvasRoot);

            m_liveCanvas = Instantiate(canvas.gameObject, canvasRoot.parent);
            m_liveCanvas.name = canvas.name + " (Live Turn)";
            if (cameraInside)
            {
                m_liveCamera = FindCopy(canvasRoot, m_liveCanvas.transform, m_captureCamera.transform).GetComponent<Camera>();
            }
            else
            {
                GameObject cameraCopy = Instantiate(m_captureCamera.gameObject, m_captureCamera.transform.parent);
                cameraCopy.name = m_captureCamera.name + " (Live Turn)";
                m_liveCamera = cameraCopy.GetComponent<Camera>();
            }
            if (m_liveCamera.TryGetComponent(out AudioListener listener)) Destroy(listener);
            m_liveCamera.transform.position += s_liveOffset;
            m_liveCamera.targetTexture = null;
            m_liveCamera.enabled = false;

            Canvas canvasCopy = m_liveCanvas.GetComponent<Canvas>();
            canvasCopy.worldCamera = m_liveCamera;
            if (canvasCopy.renderMode == RenderMode.WorldSpace) m_liveCanvas.transform.position += s_liveOffset;
            if (m_liveCanvas.TryGetComponent(out GraphicRaycaster raycaster)) raycaster.enabled = false;
            m_liveLeftView = FindCopy(canvasRoot, m_liveCanvas.transform, m_leftPageView.transform).GetComponent<BookPageView>();
            m_liveRightView = FindCopy(canvasRoot, m_liveCanvas.transform, m_rightPageView.transform).GetComponent<BookPageView>();
        }

        private static Transform FindCopy(Transform originalRoot, Transform copyRoot, Transform original)
        {
            var path = new List<int>();
            for (Transform t = original; t != originalRoot; t = t.parent) path.Insert(0, t.GetSiblingIndex());
            Transform result = copyRoot;
            foreach (int index in path) result = result.GetChild(index);
            return result;
        }

        private void BeginLiveCurrent()
        {
            if (m_liveCamera == null || m_liveLeftView == null || m_liveRightView == null || m_currentTexture == null) return;
            m_liveLeftView.ShowPage(GetPage(m_spreadIndex * 2));
            m_liveRightView.ShowPage(GetPage(m_spreadIndex * 2 + 1));
            Canvas.ForceUpdateCanvases();
            m_liveCamera.targetTexture = m_currentTexture;
            m_liveCamera.enabled = true;
        }

        private void EndLiveCurrent()
        {
            if (m_liveCamera == null) return;
            m_liveCamera.enabled = false;
            m_liveCamera.targetTexture = null;
        }

        private void OnDestroy()
        {
            if (m_liveCanvas != null) Destroy(m_liveCanvas);
            if (m_liveCamera != null && m_liveCamera.gameObject != m_liveCanvas) Destroy(m_liveCamera.gameObject);
        }

        private IEnumerator CaptureSpread(
            int spreadIndex, BookPageData leftPage, BookPageData rightPage, bool instant)
        {
            m_captureFlag = true;
            bool advanceFlag = spreadIndex > m_spreadIndex;
            bool turnFlag = !instant && m_currentTexture != null && spreadIndex != m_spreadIndex;
            RenderTexture targetTexture = m_currentTexture == m_textureA ? m_textureB : m_textureA;
            // 持っているページ（元の見開き）は複製した撮影で更新し続ける
            if (turnFlag) BeginLiveCurrent();

            m_captureCamera.targetTexture = targetTexture;
            m_leftPageView.ShowPage(leftPage);
            m_rightPageView.ShowPage(rightPage);

            Canvas.ForceUpdateCanvases();

            m_captureCamera.enabled = true;
            yield return WaitForCameraFrame(m_captureCamera);
            TagCoverInsides(targetTexture, spreadIndex, PageCount);

            // Keep the source camera live for animated page materials.

            if (m_flipView != null)
            {
                m_flipView.SetDisplay(m_displayImage);
                m_displayImage.texture = m_flipView.OutputTexture;
                m_flipView.SetReadingProgress(m_spreadIndex, SpreadCount);
                if (turnFlag)
                {
                    yield return m_flipView.PlayTurn(m_currentTexture, targetTexture, advanceFlag);
                    EndLiveCurrent();
                    if (!m_flipView.LastTurnCommitted)
                    {
                        // ドラッグを戻した：撮影を元の見開きへ戻し、ページ番号は変えない
                        m_captureCamera.targetTexture = m_currentTexture;
                        m_leftPageView.ShowPage(GetPage(m_spreadIndex * 2));
                        m_rightPageView.ShowPage(GetPage(m_spreadIndex * 2 + 1));
                        Canvas.ForceUpdateCanvases();
                        m_captureFlag = false;
                        ResumePendingSpread();
                        yield break;
                    }
                }
                else if (IsClosed)
                {
                    m_flipView.ShowClosed(targetTexture, m_coverState == BookCoverState.FrontCover);
                }
                else
                {
                    m_flipView.ShowSpread(targetTexture);
                }
                m_flipView.SetReadingProgress(spreadIndex, SpreadCount);
                yield return null;
            }
            else if (m_modelView != null)
            {
                m_displayImage.texture = m_modelView.OutputTexture;
                if (turnFlag)
                {
                    // 入力ロックは撮影だけでなく、めくり終了まで維持
                    yield return m_modelView.PlayTurn(m_currentTexture, targetTexture, advanceFlag);
                    EndLiveCurrent();
                }
                else
                {
                    m_modelView.ShowSpread(targetTexture);
                }
                // 撮影終了はフレーム末,
                // 次フレームで3DCameraを描画
                yield return null;
                yield return WaitForCameraFrame(m_modelView.ModelCamera);
            }
            else
            {
                m_displayImage.texture = targetTexture;
            }
            EndLiveCurrent();
            m_previousTexture = m_currentTexture;
            m_currentTexture = targetTexture;
            m_spreadIndex = spreadIndex;
            m_displayImage.enabled = true;
            m_captureFlag = false;
            SpreadShown?.Invoke(spreadIndex);
            ResumePendingSpread();
        }

        private static IEnumerator WaitForCameraFrame(Camera camera)
        {
            // Editor batch mode does not issue WaitForEndOfFrame callbacks.
            if (Application.isBatchMode) { camera.Render(); yield return null; }
            else yield return new WaitForEndOfFrame();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            var completion = m_inventoryCompletion;
            m_inventoryCompletion = null;
            completion?.Invoke(false);
            m_pendingSpread = -1;
            m_pendingPage = -1;
            m_captureFlag = false;
            EndLiveCurrent();
            if (m_captureCamera != null) m_captureCamera.enabled = false;
            if (m_modelView != null) m_modelView.StopDisplay();
            if (m_flipView != null) m_flipView.StopDisplay();
            if (m_displayImage != null)
            {
                m_displayImage.enabled = false;
                m_displayImage.texture = null;
            }
            m_currentTexture = null;
            m_previousTexture = null;
        }
    }
}
