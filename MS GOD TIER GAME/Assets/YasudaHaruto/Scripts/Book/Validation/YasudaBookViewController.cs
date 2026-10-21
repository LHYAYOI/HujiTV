// Dedicated fork; provenance recorded in PROJECT_STATUS.md.
using hatake;
using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BookControllerは、BookDataの見開きを撮影し、表示するためのコンポーネント
namespace YasudaHaruto.BookValidation
{
    public sealed class YasudaBookViewController : MonoBehaviour
    {
        [Header("ページデータ")]
        [SerializeField] private BookData m_bookData;
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
        public bool IsBusy => m_captureFlag || m_removingNoise;
        public bool IsRemovingNoise => m_removingNoise;
        public int PageCount => m_runtimePages.Count;
        public event Action<int, BookPageData> PageAdded;
        public event Action<int> PageRemoved;
        /// <summary>途中への挿入（InsertLeaf）。index 以降の既存ページは後ろへずれる</summary>
        public event Action<int, BookPageData> PageInserted;
        public event Action<int> SpreadShown;
        private readonly List<BookPageData> m_runtimePages = new List<BookPageData>();
        private Action<bool> m_inventoryCompletion;

        // The inventory owner supplies a complete candidate; no partial pair edits escape.
        public bool TryApplyInventory(IReadOnlyList<BookPageData> pages, int targetSpread, Action<bool> completed)
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
            m_runtimeSets.Clear();
            for (int i = 0; i < candidate.Count; i++) m_runtimeSets.Add(null);
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
        // ページごとの見開きセット（新規格）。旧方式のページは null。m_runtimePages と同じ長さ
        private readonly List<BookSpreadData> m_runtimeSets = new List<BookSpreadData>();
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
        private bool m_removingNoise;
        private bool m_clearDissolveAfterCapture;
        private IBookNoiseRemoval m_removalPresentation;
        // インターフェース越しでは破棄済みのコンポーネントが null にならないので、Unity の判定で確かめる
        private bool RemovalAlive => m_removalPresentation is UnityEngine.Object component
            ? component != null : m_removalPresentation != null;

        private void Awake()
        {
            if (m_bookData != null) m_bookData.CopyTo(m_runtimePages, m_runtimeSets);
            if (CoversEnabled && m_coverInsides)
            {
                InsertRuntime(0, null, null);
                m_frontInsideSlot = true;
            }
            if (m_startClosed && CoversEnabled) m_coverState = BookCoverState.FrontCover;
            // ページを表示する前（OnEnable より前）に複製する
            if (m_liveTurningPages && Application.isPlaying) CreateLiveRig();
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

        /// <summary>ページが見開きセット（新規格）の一部ならそのセット。旧方式のページは null</summary>
        public BookSpreadData GetSpreadSet(int pageIndex) =>
            pageIndex >= 0 && pageIndex < m_runtimeSets.Count ? m_runtimeSets[pageIndex] : null;

        /// <summary>見開きの左右が同じセットならそのセット</summary>
        public BookSpreadData GetSpreadSetOfSpread(int spreadIndex)
        {
            BookSpreadData set = GetSpreadSet(spreadIndex * 2);
            return set != null && GetSpreadSet(spreadIndex * 2 + 1) == set ? set : null;
        }

        private void InsertRuntime(int index, BookPageData page, BookSpreadData set)
        {
            m_runtimePages.Insert(index, page);
            m_runtimeSets.Insert(index, set);
        }

        private void RemoveRuntime(int index)
        {
            m_runtimePages.RemoveAt(index);
            m_runtimeSets.RemoveAt(index);
        }

        // Runtime inventory only: never modify the shared ScriptableObject asset.
        public int AddPage(BookPageData page)
        {
            if (page == null) return -1;
            int index = PageCount;
            InsertRuntime(index, page, null);
            if (index / 2 == m_spreadIndex) ShowSpread(m_spreadIndex);
            PageAdded?.Invoke(index, page);
            return index;
        }

        public bool RemovePage(int index)
        {
            if (IsBusy || index < 0 || index >= PageCount || IsCoverInside(index)) return false;
            // 見開きセットの片方だけを抜くと左右がずれるので、セットは BurnSpread で扱う
            if (GetSpreadSet(index) != null) return false;
            if (Application.isPlaying && GetPage(index) != null && GetPage(index).Category == BookPageData.PageCategory.Noise)
            {
                var presentation = GetComponent<IBookNoiseRemoval>();
                if (presentation != null && presentation.CanDissolve) return RemoveNoisePage(index);
            }
            RemovePageImmediate(index);
            return true;
        }

        private void RemovePageImmediate(int index)
        {
            RemoveRuntime(index);
            if (m_pendingSpread >= 0)
            {
                if (m_pendingPage > index) m_pendingPage--;
                m_pendingPage = Mathf.Clamp(m_pendingPage, 0, Mathf.Max(0, PageCount - 1));
                m_pendingSpread = m_pendingPage / 2;
            }
            m_spreadIndex = Mathf.Clamp(m_spreadIndex, 0, Mathf.Max(0, (PageCount - 1) / 2));
            PageRemoved?.Invoke(index);
            ShowSpread(m_spreadIndex);
        }

        // ------------------------------------------------------------------
        // 一枚（表と裏の2ページ）を燃やす／挿入する。Flip View の演出付き
        // 見開き k の右ページ 2k+1 の裏は 2k+2、左ページ 2k の裏は 2k-1。
        // 戻り値は受付のみ。完了は PageRemoved / PageInserted と SpreadShown。
        // ------------------------------------------------------------------

        /// <summary>表示中の pageIndex とその裏のページを燃やして取り除く。origin は燃え始める位置（本の座標）</summary>
        public bool BurnLeaf(int pageIndex, Vector2? origin = null)
        {
            if (!CanEditLeaf(pageIndex)) return false;
            int k = m_spreadIndex;
            bool right = (pageIndex & 1) == 1;
            int back = right ? pageIndex + 1 : pageIndex - 1;
            // 最初の左ページは表紙に貼られていて裏ページが無い。1ページだけ抜くと以降の左右が入れ替わるので不可
            if (back < 0) return false;
            // 見開きセットを割る操作・表紙の内側を含む操作は不可
            if (GetSpreadSet(pageIndex) != null || GetSpreadSet(back) != null) return false;
            if (IsCoverInside(pageIndex) || IsCoverInside(back)) return false;
            int newSpread = right ? k : k - 1;
            // 燃えた後に見える見開き（一枚分を抜いた並び）
            BookPageData afterLeft = right ? GetPage(2 * k) : GetPage(2 * k - 2);
            BookPageData afterRight = right ? GetPage(2 * k + 3) : GetPage(2 * k + 1);
            var removed = back < PageCount ? new[] { Mathf.Max(pageIndex, back), Mathf.Min(pageIndex, back) } : new[] { pageIndex };
            StartCoroutine(EditLeaf(BookPageEffect.Burn, right ? 1 : -1, origin, newSpread, afterLeft, afterRight, () =>
            {
                foreach (int index in removed)
                {
                    RemoveRuntime(index);
                    ShiftPending(index, -1);
                    PageRemoved?.Invoke(index);
                }
            }, -removed.Length));
            return true;
        }

        /// <summary>
        /// 表示中の右／左ページの上に一枚を挿入し、光の粒とともに組み上げる。
        /// front が見える面、back がその裏（null は白紙）。
        /// </summary>
        public bool InsertLeaf(bool rightSide, BookPageData front, BookPageData back = null, Vector2? origin = null)
        {
            int k = m_spreadIndex;
            if (front == null || !CanEditLeaf(rightSide ? 2 * k + 1 : 2 * k, true)) return false;
            // 見開きセットの間には挟めない
            if (GetSpreadSetOfSpread(k) != null) return false;
            // どちら側でも 2k+1 に挿入する。左側に置く一枚は [裏, 表] の順で、見開きが1つ進む
            int at = 2 * k + 1;
            BookPageData first = rightSide ? front : back;
            BookPageData second = rightSide ? back : front;
            int newSpread = rightSide ? k : k + 1;
            BookPageData afterLeft = rightSide ? GetPage(2 * k) : front;
            BookPageData afterRight = rightSide ? front : GetPage(2 * k + 1);
            StartCoroutine(EditLeaf(BookPageEffect.Revive, rightSide ? 1 : -1, origin, newSpread, afterLeft, afterRight, () =>
            {
                // 左ページしか無い（ページ数が足りない）ときは白紙で詰めて左右をそろえる
                while (m_runtimePages.Count < at) InsertRuntime(m_runtimePages.Count, null, null);
                InsertRuntime(at, first, null);
                InsertRuntime(at + 1, second, null);
                ShiftPending(at, 2);
                PageInserted?.Invoke(at, first);
                PageInserted?.Invoke(at + 1, second);
            }, 2));
            return true;
        }

        // ------------------------------------------------------------------
        // 見開き両面を燃やす／挿入する。左右2ページ単位なので、以降のページの左右は変わらない。
        // 見開きセット（BookSpreadData）はこちらで扱う。旧方式のページにも使える
        // ------------------------------------------------------------------

        /// <summary>表示中の見開き（左右2ページ）を燃やして取り除く。origin から両ページへ燃え広がる</summary>
        public bool BurnSpread(Vector2? origin = null)
        {
            int k = m_spreadIndex;
            int left = 2 * k;
            if (!CanEditLeaf(left) || left >= PageCount) return false;
            if (IsCoverInside(left)) return false;   // 最初の見開き（表紙の内側）は燃やせない
            int count = Mathf.Min(2, PageCount - left);
            // 燃えた後：同じ番号の見開きに次の2ページ。最後の見開きなら1つ前の見開き
            bool last = left + 2 >= PageCount;
            int newSpread = last ? Mathf.Max(0, k - 1) : k;
            BookPageData afterLeft = last ? GetPage(2 * k - 2) : GetPage(2 * k + 2);
            BookPageData afterRight = last ? GetPage(2 * k - 1) : GetPage(2 * k + 3);
            StartCoroutine(EditLeaf(BookPageEffect.Burn, 0, origin, newSpread, afterLeft, afterRight, () =>
            {
                for (int index = left + count - 1; index >= left; index--)
                {
                    RemoveRuntime(index);
                    ShiftPending(index, -1);
                    PageRemoved?.Invoke(index);
                }
            }, -count));
            return true;
        }

        /// <summary>見開きセットを、表示中の見開きの位置に挿入して両面同時に組み上げる（今の見開きは1つ後ろへ）</summary>
        public bool InsertSpread(BookSpreadData spread, Vector2? origin = null)
        {
            if (spread == null) return false;
            return InsertSpreadPages(spread.LeftPage, spread.RightPage, spread, origin);
        }

        /// <summary>旧方式の2ページを見開きとして挿入する（セットにはしない）</summary>
        public bool InsertSpreadPages(BookPageData left, BookPageData right, Vector2? origin = null) =>
            InsertSpreadPages(left, right, null, origin);

        private bool InsertSpreadPages(BookPageData left, BookPageData right, BookSpreadData set, Vector2? origin)
        {
            int k = m_spreadIndex;
            int at = 2 * k;
            if ((left == null && right == null) || !CanEditLeaf(at, true)) return false;
            if (IsCoverInside(at)) return false;   // 表紙の内側の前には挟めない
            StartCoroutine(EditLeaf(BookPageEffect.Revive, 0, origin, k, left, right, () =>
            {
                while (m_runtimePages.Count < at) InsertRuntime(m_runtimePages.Count, null, null);
                InsertRuntime(at, left, set);
                InsertRuntime(at + 1, right, set);
                ShiftPending(at, 2);
                PageInserted?.Invoke(at, left);
                PageInserted?.Invoke(at + 1, right);
            }, 2));
            return true;
        }

        private bool CanEditLeaf(int pageIndex, bool allowEmpty = false)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsBusy || m_currentTexture == null || IsClosed) return false;
            if (pageIndex / 2 != m_spreadIndex || pageIndex < 0) return false;
            return allowEmpty || pageIndex < PageCount;
        }

        // side：1 右ページ、-1 左ページ、0 見開き両面
        private IEnumerator EditLeaf(BookPageEffect effect, int side, Vector2? origin, int newSpread,
            BookPageData afterLeft, BookPageData afterRight, Action applyPages, int pageDelta)
        {
            m_captureFlag = true;
            BeginLiveCurrent();
            RenderTexture targetTexture = m_currentTexture == m_textureA ? m_textureB : m_textureA;
            m_captureCamera.targetTexture = targetTexture;
            m_leftPageView.ShowPage(afterLeft);
            m_rightPageView.ShowPage(afterRight);
            Canvas.ForceUpdateCanvases();
            m_captureCamera.enabled = true;
            yield return WaitForCameraFrame(m_captureCamera);
            TagCoverInsides(targetTexture, newSpread, PageCount + pageDelta);

            if (m_flipView != null)
            {
                Vector2 start = origin ?? new Vector2(side * 0.5f, m_flipView.PageAspect * 0.5f);
                yield return m_flipView.PlayPageEffect(effect, m_currentTexture, targetTexture, side, start);
            }
            else if (m_modelView != null)
            {
                m_modelView.ShowSpread(targetTexture);
            }
            else
            {
                m_displayImage.texture = targetTexture;
            }

            EndLiveCurrent();
            applyPages();
            m_previousTexture = m_currentTexture;
            m_currentTexture = targetTexture;
            m_spreadIndex = Mathf.Clamp(newSpread, 0, SpreadCount - 1);
            if (m_flipView != null) m_flipView.SetReadingProgress(m_spreadIndex, SpreadCount);
            m_captureFlag = false;
            SpreadShown?.Invoke(m_spreadIndex);
            ResumePendingSpread();
        }

        // 処理中に予約されためくり先を、ページの増減に合わせてずらす
        private void ShiftPending(int index, int delta)
        {
            if (m_pendingSpread < 0) return;
            if (m_pendingPage >= index) m_pendingPage = Mathf.Max(index, m_pendingPage + delta);
            m_pendingPage = Mathf.Clamp(m_pendingPage, 0, Mathf.Max(0, PageCount - 1));
            m_pendingSpread = m_pendingPage / 2;
        }

        // Returns acceptance, not completion. PageRemoved is the completion notification.
        // Only the visible noise page can be purified; never switch the player's page implicitly.
        public bool RemoveNoisePage(int index)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsBusy || m_currentTexture == null || index / 2 != m_spreadIndex) return false;
            BookPageData page = GetPage(index);
            if (page == null || page.Category != BookPageData.PageCategory.Noise) return false;
            if (GetSpreadSet(index) != null) return false;   // セットは BurnSpread で扱う
            m_removalPresentation = GetComponent<IBookNoiseRemoval>();
            if (!RemovalAlive || !m_removalPresentation.CanDissolve) return false;
            m_removingNoise = true;
            StartCoroutine(DissolveNoise(index));
            return true;
        }

        private IEnumerator DissolveNoise(int index)
        {
            m_removalPresentation.BeginNoiseRemoval(index, index % 2 == 0 ? m_leftPageView : m_rightPageView);
            float duration = m_removalPresentation.NoiseRemovalDuration;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                if (!RemovalAlive || !m_removalPresentation.CanDissolve)
                {
                    if (RemovalAlive) m_removalPresentation.EndNoiseRemoval(true);
                    if (m_modelView != null) m_modelView.ResetPageDissolve();
                    m_removingNoise = false;
                    ResumePendingSpread();
                    yield break;
                }
                m_removalPresentation.SampleNoiseRemoval(elapsed / duration, Time.deltaTime);
                yield return null;
            }
            m_removalPresentation.SampleNoiseRemoval(1, 0);
            yield return null;
            if (!RemovalAlive || !m_removalPresentation.CanDissolve)
            {
                if (RemovalAlive) m_removalPresentation.EndNoiseRemoval(true);
                if (m_modelView != null) m_modelView.ResetPageDissolve();
                m_removingNoise = false;
                ResumePendingSpread();
                yield break;
            }
            m_removalPresentation.EndNoiseRemoval(false);
            m_removingNoise = false;
            m_clearDissolveAfterCapture = true;
            RemovePageImmediate(index);
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
            if (!isActiveAndEnabled) return;
            if (spreadIndex < 0 || spreadIndex >= Mathf.Max(1, (PageCount + 1) / 2)) return;
            if (IsBusy) { m_pendingSpread = spreadIndex; m_pendingPage = spreadIndex * 2; m_pendingInstant = instant; return; }
            if (m_bookData == null || m_leftPageView == null ||
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
                        m_clearDissolveAfterCapture = false;
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
                if (m_clearDissolveAfterCapture) m_modelView.ResetPageDissolve();
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
            m_clearDissolveAfterCapture = false;
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
            if (RemovalAlive) m_removalPresentation.EndNoiseRemoval(true);
            m_removingNoise = false;
            m_clearDissolveAfterCapture = false;
            if (m_modelView != null) m_modelView.ResetPageDissolve();
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
