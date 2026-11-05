using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace hatake
{
    /// <summary>
    /// サンプルシーン（BookSample）用の確認入力。
    ///   開始時：暗転（Screen Fade = 1）から明ける。
    ///   F：画面フェードの暗転／明転を切り替える。
    ///   G：本文の紙の仕上げをマットコート紙／グロスコート紙で切り替える（シェーダーのバリアント）。
    ///   右上の「Book pose」パネル：本の姿勢（X / Y / Z 回転）を動かし、光の当たり方の変化を確かめる。
    /// 本編では BookFlipView.FadeScreen(目標, 秒数) / SetScreenFade(値) を直接呼ぶ。
    /// </summary>
    public sealed class BookSampleDemo : MonoBehaviour
    {
        [SerializeField] private BookFlipView m_flipView;

        [Header("画面フェード")]
        [Tooltip("開始時に暗転から明ける")]
        [SerializeField] private bool m_fadeInOnStart = true;
        [Tooltip("暗転・明転にかける秒数")]
        [SerializeField, Min(0)] private float m_fadeDuration = 0.8f;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key m_fadeKey = Key.F;
#else
        [SerializeField] private KeyCode m_fadeKey = KeyCode.F;
#endif

        [Header("紙の仕上げ")]
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key m_paperKey = Key.G;
#else
        [SerializeField] private KeyCode m_paperKey = KeyCode.G;
#endif

        [Header("本の姿勢（右上のパネル）")]
        [Tooltip("パネルを出す")]
        [SerializeField] private bool m_showPosePanel = true;
        [Tooltip("スライダーで動かせる角度の範囲（度）")]
        [SerializeField, Range(10f, 90f)] private float m_poseRange = 60f;
        [Tooltip("ONで本をゆっくり揺らす（スライダーの値を中心に）")]
        [SerializeField] private bool m_sway;
        [SerializeField, Min(0)] private float m_swayAngle = 20f;
        [SerializeField, Min(0)] private float m_swaySpeed = 0.6f;

        private Vector3 m_pose;
        // パネルの範囲（IMGUI の座標、左上原点）。この上ではページのドラッグ・スワイプを止める
        private Rect m_panelRect;
        private EventSystem m_pausedEventSystem;

        [Header("操作の説明")]
        [SerializeField] private bool m_showHelp = true;

        [Header("フレームレート")]
        [Tooltip("モバイルの目標のフレームレート（Application.targetFrameRate）。0 以下なら変えない（モバイルの既定は 30）")]
        [SerializeField] private int m_targetFrameRate = 60;

        private void Awake()
        {
            if (m_targetFrameRate > 0 && Application.isMobilePlatform)
            {
                // モバイルは targetFrameRate で上限が決まる（既定は 30）
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = m_targetFrameRate;
            }
            if (m_lightweightCameraMobile && Application.isMobilePlatform) MakeMainCameraLightweight();
        }

        [Tooltip("モバイルで、画面を描くカメラの要らない処理を切る：HDR・カメラの後処理（本は自前で HDR・Bloom を描く）、" +
                 "描画の縮小（Render Scale 0.8 の中間画像と拡大）。実行中だけで、アセットは変えない")]
        [SerializeField] private bool m_lightweightCameraMobile = true;

        private static void MakeMainCameraLightweight()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.allowHDR = false;
                camera.allowMSAA = false;
                var data = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (data != null)
                {
                    data.renderPostProcessing = false;
                    data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                    data.renderShadows = false;
                }
            }
            // 本は UI に表示するだけなので、縮小して描いて拡大する必要がない（実行中の値だけ変える）
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
                urp.renderScale = 1f;
        }

        private void Start()
        {
            if (m_flipView == null)
            {
                Debug.LogError("BookSampleDemo : Flip View が未設定です", this);
                enabled = false;
                return;
            }
            if (m_fadeInOnStart)
            {
                m_flipView.SetScreenFade(1f);
                m_flipView.FadeScreen(0f, m_fadeDuration);
            }
        }

        private void Update()
        {
            if (KeyPressed(m_fadeKey))
            {
                // 半分より暗ければ明ける、明るければ暗転する
                float target = m_flipView.ScreenFade > 0.5f ? 0f : 1f;
                m_flipView.FadeScreen(target, m_fadeDuration);
            }
            BlockBookInputOverPanel();

            // 本の姿勢を渡す（揺らすときはスライダーの値を中心に）
            Vector3 pose = m_pose;
            if (m_sway)
            {
                float t = Time.unscaledTime * m_swaySpeed * Mathf.PI * 2f;
                pose += new Vector3(Mathf.Sin(t) * m_swayAngle, Mathf.Sin(t * 0.7f + 1f) * m_swayAngle, 0f);
            }
            m_flipView.SetBookPose(pose);
            if (KeyPressed(m_paperKey))
            {
                // マテリアルのまま（既定はマット）→ グロス → マット → グロス…
                m_flipView.PaperFinish = m_flipView.PaperFinish == BookPaperFinish.Gloss
                    ? BookPaperFinish.Matte : BookPaperFinish.Gloss;
            }
        }

        // IMGUI のスライダーは uGUI の入力を止めないので、マウスがパネルの上にある間は EventSystem を止め、
        // スライダーを動かしたときにページがめくれないようにする
        private void BlockBookInputOverPanel()
        {
            Vector2 mouse = MousePosition();
            bool over = m_showPosePanel && m_panelRect.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            if (over && m_pausedEventSystem == null && EventSystem.current != null && EventSystem.current.enabled)
            {
                m_pausedEventSystem = EventSystem.current;
                m_pausedEventSystem.enabled = false;
            }
            else if (!over)
            {
                ResumeEventSystem();
            }
        }

        private void ResumeEventSystem()
        {
            if (m_pausedEventSystem != null)
            {
                m_pausedEventSystem.enabled = true;
                m_pausedEventSystem = null;
            }
        }

        private void OnDisable()
        {
            ResumeEventSystem();
        }

        private static Vector2 MousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static bool KeyPressed(Key key)
        {
            return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        }
#else
        private static bool KeyPressed(KeyCode key)
        {
            return Input.GetKeyDown(key);
        }
#endif

        // 計測用：画面の説明・姿勢のパネルを出さない（IMGUI の負荷を除いて測る）
        internal static bool HideGui;

        private void OnGUI()
        {
            if (HideGui) return;
            if (m_showHelp)
            {
                string finish = m_flipView != null && m_flipView.PaperFinish == BookPaperFinish.Gloss ? "gloss" : "matte";
                string help = "F : screen fade (0 / 1)\nG : paper finish (" + finish + ")\nC : open / close the book\n" +
                              "Drag or swipe : turn pages\nO / L / I : spot / burn / insert";
                GUI.Label(new Rect(12, 12, 420, 110), help);
            }
            DrawPosePanel();
        }

        // 右上：本の姿勢のスライダー
        private void DrawPosePanel()
        {
            if (!m_showPosePanel)
            {
                return;
            }
            const float width = 260f;
            m_panelRect = new Rect(Screen.width - width - 12f, 12f, width, 200f);
            GUILayout.BeginArea(m_panelRect, GUI.skin.box);
            GUILayout.Label("Book pose (light direction is fixed)");
            m_pose.x = PoseSlider("X", m_pose.x);
            m_pose.y = PoseSlider("Y", m_pose.y);
            m_pose.z = PoseSlider("Z", m_pose.z);
            m_sway = GUILayout.Toggle(m_sway, "Sway");
            if (GUILayout.Button("Reset"))
            {
                m_pose = Vector3.zero;
                m_sway = false;
            }
            GUILayout.EndArea();
        }

        private float PoseSlider(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + " " + value.ToString("0"), GUILayout.Width(50f));
            value = GUILayout.HorizontalSlider(value, -m_poseRange, m_poseRange);
            GUILayout.EndHorizontal();
            return value;
        }
    }
}
