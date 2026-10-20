using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace hatake
{
    /// <summary>
    /// 本の表示（BookController の Display Image）に付けて、マウス位置にキー操作で演出をかける確認用の入力。
    ///   O：暗転してマウス位置を照らす（もう一度押すと解除）。照らす位置はマウスに追従。
    ///   L：燃やして取り除く。見開き両面（既定）か、マウスが載っているページとその裏（旧：一枚）。
    ///   I：ページを光の粒とともに挿入する（入手）。Revive Spread（見開きセット）があれば見開きで、
    ///      無ければ Revive Page を見開き両面（既定）か一枚（旧）で。
    ///   C：閉じていれば開く、開いていれば表紙で閉じる。
    /// それぞれ Use ～ で使うかどうかを選べる。Use Keys をOFFにすると全部止まる。
    /// ゲーム本編では同じ処理を BookController.BurnLeaf / InsertLeaf と BookFlipView.SetSpot で呼ぶ。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BookFlipKeyboardActions : MonoBehaviour
    {
        /// <summary>燃焼・挿入の単位</summary>
        public enum EditUnit
        {
            [InspectorName("見開き両面")] Spread,   // 左右2ページ。以降のページの左右がずれない
            [InspectorName("一枚（旧）")] Leaf       // マウス側のページとその裏
        }

        [SerializeField] private BookController m_bookController;
        [SerializeField] private BookFlipView m_flipView;

        [Header("使うキー")]
        [Tooltip("OFFにするとすべてのキー操作を止める")]
        [SerializeField] private bool m_useKeys = true;
        [SerializeField] private bool m_useSpot = true;
        [SerializeField] private bool m_useBurn = true;
        [SerializeField] private bool m_useRevive = true;
        [SerializeField] private bool m_useOpenClose = true;

        [Header("キー")]
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key m_spotKey = Key.O;
        [SerializeField] private Key m_burnKey = Key.L;
        [SerializeField] private Key m_reviveKey = Key.I;
        [SerializeField] private Key m_openCloseKey = Key.C;
#else
        [SerializeField] private KeyCode m_spotKey = KeyCode.O;
        [SerializeField] private KeyCode m_burnKey = KeyCode.L;
        [SerializeField] private KeyCode m_reviveKey = KeyCode.I;
        [SerializeField] private KeyCode m_openCloseKey = KeyCode.C;
#endif

        [Header("O：暗転スポット")]
        [SerializeField, Range(0, 1)] private float m_dimAmount = 0.85f;
        [Tooltip("照らす半径（ページ幅比）")]
        [SerializeField, Range(0.02f, 1f)] private float m_spotRadius = 0.22f;
        [SerializeField, Range(0.001f, 0.3f)] private float m_spotFeather = 0.07f;
        [SerializeField] private BookFlipSpot.SpotShape m_spotShape = BookFlipSpot.SpotShape.Ellipse;
        [Tooltip("位置の追従の速さ。0で即時")]
        [SerializeField, Min(0)] private float m_spotFollow = 18f;
        [SerializeField, Min(0)] private float m_fadeDuration = 0.3f;
        [SerializeField, Range(0, BookFlipView.MaxSpots - 1)] private int m_spotSlot;

        [Header("L / I：単位")]
        [Tooltip("Spread：見開き両面。Leaf：旧方式の一枚（表と裏）。見開きセットのページは常に見開き単位")]
        [SerializeField] private EditUnit m_editUnit = EditUnit.Spread;

        [Header("I：挿入するページ")]
        [Tooltip("入手する見開きセット（新規格）。設定するとこちらを優先")]
        [SerializeField] private BookSpreadData m_reviveSpread;
        [Tooltip("入手するページ（旧方式）。Spread では左ページ、Leaf ではマウス側に見える面")]
        [SerializeField] private BookPageData m_revivePage;
        [Tooltip("Spread では右ページ、Leaf ではその裏。空なら白紙")]
        [SerializeField] private BookPageData m_reviveBackPage;

        private RectTransform m_rectTransform;
        private Canvas m_canvas;
        private bool m_spotFlag;
        private Vector2 m_spotPoint;

        public BookPageData RevivePage { get => m_revivePage; set => m_revivePage = value; }
        public BookSpreadData ReviveSpread { get => m_reviveSpread; set => m_reviveSpread = value; }
        public EditUnit Unit { get => m_editUnit; set => m_editUnit = value; }
        public bool UseKeys { get => m_useKeys; set => m_useKeys = value; }
        public bool UseSpot { get => m_useSpot; set => m_useSpot = value; }
        public bool UseBurn { get => m_useBurn; set => m_useBurn = value; }
        public bool UseRevive { get => m_useRevive; set => m_useRevive = value; }
        public bool UseOpenClose { get => m_useOpenClose; set => m_useOpenClose = value; }

        private void Awake()
        {
            m_rectTransform = (RectTransform)transform;
            m_canvas = GetComponentInParent<Canvas>();
        }

        private void Update()
        {
            if (m_bookController == null || m_flipView == null) return;
            // 使わない設定に切り替えたら、点けていた暗転も消す
            if (m_spotFlag && (!m_useKeys || !m_useSpot)) ToggleSpot(m_spotPoint);
            if (!m_useKeys) return;
            bool hasPoint = TryGetMouseBookPoint(out Vector2 point);

            if (m_useSpot && WasPressed(m_spotKey)) ToggleSpot(hasPoint ? point : m_spotPoint);
            if (m_useOpenClose && WasPressed(m_openCloseKey))
            {
                bool accepted = m_bookController.IsClosed
                    ? m_bookController.OpenBook()
                    : m_bookController.CloseBook(true);
                if (!accepted) Debug.Log("開閉できません（処理中・表紙が無効）", this);
            }
            if (m_spotFlag && hasPoint)
            {
                float t = m_spotFollow <= 0 ? 1f : 1f - Mathf.Exp(-m_spotFollow * Time.unscaledDeltaTime);
                m_spotPoint = Vector2.Lerp(m_spotPoint, point, t);
                ApplySpot();
            }


            if (!hasPoint || !m_flipView.ContainsPage(point)) return;
            bool rightPage = point.x >= 0f;
            if (m_useBurn && WasPressed(m_burnKey))
            {
                int pageIndex = m_bookController.CurrentSpread * 2 + (rightPage ? 1 : 0);
                bool spread = m_editUnit == EditUnit.Spread || m_bookController.GetSpreadSet(pageIndex) != null;
                bool accepted = spread ? m_bookController.BurnSpread(point) : m_bookController.BurnLeaf(pageIndex, point);
                if (!accepted)
                    Debug.Log(spread
                        ? "見開きを燃やせません（処理中・閉じている・空の見開き）"
                        : $"ページ{pageIndex}は燃やせません（処理中・空のページ・最初の左ページ・見開きセットを割る）", this);
            }
            if (m_useRevive && WasPressed(m_reviveKey))
            {
                bool accepted;
                if (m_reviveSpread != null) accepted = m_bookController.InsertSpread(m_reviveSpread, point);
                else if (m_revivePage == null)
                {
                    Debug.LogWarning("BookFlipKeyboardActions の Revive Spread か Revive Page を設定してください", this);
                    return;
                }
                else if (m_editUnit == EditUnit.Spread)
                    accepted = m_bookController.InsertSpreadPages(m_revivePage, m_reviveBackPage, point);
                else
                    accepted = m_bookController.InsertLeaf(rightPage, m_revivePage, m_reviveBackPage, point);
                if (!accepted) Debug.Log("ページを挿入できません（処理中・閉じている・見開きセットの間）", this);
            }
        }

        private void ToggleSpot(Vector2 point)
        {
            m_spotFlag = !m_spotFlag;
            if (m_spotFlag)
            {
                m_spotPoint = point;
                ApplySpot();
                m_flipView.FadeDim(m_dimAmount, m_fadeDuration);
            }
            else
            {
                m_flipView.FadeDim(0f, m_fadeDuration);
            }
        }

        private void ApplySpot()
        {
            m_flipView.SetSpot(m_spotSlot, m_flipView.SpotAt(m_spotPoint,
                new Vector2(m_spotRadius, m_spotRadius), m_spotShape, m_spotFeather));
        }

        private bool TryGetMouseBookPoint(out Vector2 point)
        {
            point = default;
            if (!TryGetMousePosition(out Vector2 screen)) return false;
            Camera eventCamera = m_canvas != null && m_canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? m_canvas.worldCamera : null;
            return m_flipView.ScreenToBook(m_rectTransform, screen, eventCamera, out point);
        }

#if ENABLE_INPUT_SYSTEM
        private static bool WasPressed(Key key) =>
            Keyboard.current != null && key != Key.None && Keyboard.current[key].wasPressedThisFrame;

        private static bool TryGetMousePosition(out Vector2 position)
        {
            position = Mouse.current != null ? Mouse.current.position.ReadValue() : default;
            return Mouse.current != null;
        }
#else
        private static bool WasPressed(KeyCode key) => Input.GetKeyDown(key);

        private static bool TryGetMousePosition(out Vector2 position)
        {
            position = Input.mousePosition;
            return Input.mousePresent;
        }
#endif

        private void OnDisable()
        {
            if (m_spotFlag && m_flipView != null) m_flipView.SetDim(0f);
            m_spotFlag = false;
        }
    }
}
