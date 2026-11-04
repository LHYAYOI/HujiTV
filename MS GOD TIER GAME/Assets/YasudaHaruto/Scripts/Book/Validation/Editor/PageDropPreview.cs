using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace YasudaHaruto.BookValidation.Editor
{
    [InitializeOnLoad]
    public static class PageDropPreview
    {
        private const string Running = "Yasuda.PageDropPreview";
        private const string PrefabPath = "Assets/YasudaHaruto/Prefabs/PreviewPageBubble.prefab";
        private const string BookScene = "Assets/YasudaHaruto/Scenes/BookScene.unity";
        private static BookNetworkServer s_server;
        private static BookNetworkClient s_client;
        private static BattlePageDrops s_drops;
        private static Transform s_cart;
        private static RenderTexture s_preview;
        private static Text s_caption;
        private static double s_deadline;
        private static double s_next;
        private static int s_step;

        static PageDropPreview()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Running, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    try { Setup(); }
                    catch (Exception error) { Debug.LogException(error); Finish(); }
                }
                if (state == PlayModeStateChange.ExitingPlayMode) Finish();
            };
        }

        [MenuItem("Tools/YasudaHaruto/Preview Page Drop and Acquisition")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().path != BookScene)
                throw new InvalidOperationException("Open BookScene and exit Play Mode first.");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                throw new InvalidOperationException("PreviewPageBubble prefab is missing.");
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Setup()
        {
            var network = new GameObject("Page Drop Loopback");
            s_server = network.AddComponent<BookNetworkServer>();
            s_client = network.AddComponent<BookNetworkClient>();
            // Reload with the network singleton already present before BookGameBootstrap.Start.
            SceneManager.sceneLoaded += SetupStage;
            EditorSceneManager.LoadSceneInPlayMode(BookScene, new LoadSceneParameters(LoadSceneMode.Single));
        }

        private static void SetupStage(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != BookScene) return;
            SceneManager.sceneLoaded -= SetupStage;
            var stage = new GameObject("Page Drop Preview Stage");
            stage.transform.position = new Vector3(0f, 150f, 0f);
            s_drops = stage.AddComponent<BattlePageDrops>();
            var serialized = new SerializedObject(s_drops);
            serialized.FindProperty("m_bubblePrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            s_cart = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            s_cart.name = "Preview Cart";
            s_cart.SetParent(stage.transform, false);
            s_cart.localPosition = new Vector3(1.45f, -0.55f, 0f);
            s_cart.localScale = new Vector3(0.75f, 0.45f, 0.5f);

            var camera = new GameObject("Page Drop Preview Camera").AddComponent<Camera>();
            camera.transform.SetParent(stage.transform, false);
            camera.transform.localPosition = new Vector3(0f, 0.15f, -6f);
            camera.orthographic = true;
            camera.orthographicSize = 1.35f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.13f, 0.2f);
            s_preview = new RenderTexture(640, 360, 24);
            camera.targetTexture = s_preview;

            var lamp = new GameObject("Page Drop Preview Light").AddComponent<Light>();
            lamp.transform.SetParent(stage.transform, false);
            lamp.transform.localPosition = new Vector3(-1f, 2f, -3f);
            lamp.type = LightType.Point;
            lamp.range = 8f;
            lamp.intensity = 7f;

            var canvas = new GameObject("Page Drop Preview Overlay").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var panel = new GameObject("Bubble Camera", typeof(RectTransform), typeof(RawImage));
            panel.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0.62f, 0.67f);
            rect.anchorMax = new Vector2(0.98f, 0.97f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var previewImage = panel.GetComponent<RawImage>();
            previewImage.texture = s_preview;
            previewImage.raycastTarget = false;

            var label = new GameObject("Preview Status", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(panel.transform, false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = new Vector2(1f, 0.18f);
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            s_caption = label.GetComponent<Text>();
            s_caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            s_caption.alignment = TextAnchor.MiddleCenter;
            s_caption.raycastTarget = false;
            s_caption.text = "Connecting loopback...";

            s_step = 0;
            s_deadline = EditorApplication.timeSinceStartup + 35;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now > s_deadline) throw new TimeoutException("Page drop preview did not complete.");
                var book = UnityEngine.Object.FindFirstObjectByType<BookGameController>();
                switch (s_step)
                {
                    case 0 when s_server.IsListening:
                        s_client.Connect("127.0.0.1", PLAYER_SLOT.PLAYER1);
                        s_step++;
                        break;
                    case 1 when s_client.ConnectionState == BOOK_CONNECTION_STATE.CONNECTED &&
                        book != null && book.IsInitialized:
                        s_server.BeginGame();
                        if (book.Pages.Any(page => page.Data.PageId == 5))
                            throw new InvalidOperationException("Preview requires Page ID 5 to start unowned.");
                        s_drops.ReportEnemyDefeated("MiniGhost_A", s_drops.transform.position + new Vector3(-1.4f, 0.45f, 0f));
                        s_drops.ReportEnemyDefeated("MiniGhost_B", s_drops.transform.position + new Vector3(-0.45f, 0.52f, 0f));
                        s_caption.text = "Two enemies dropped Page 5";
                        s_next = now + 1.5;
                        s_step++;
                        break;
                    case 2 when now >= s_next:
                        s_drops.CollectBattleDrops(s_cart);
                        s_caption.text = "Collecting both bubbles...";
                        s_step++;
                        break;
                    case 3 when book != null && book.BookView.IsReady && book.CurrentPage?.Data.PageId == 5:
                        if (book.Pages.Count(page => page.Data.PageId == 5) != 1)
                            throw new InvalidOperationException("Page 5 was added more than once.");
                        s_caption.text = "PASS: Page 5 acquired once";
                        Debug.Log("PageDropPreview PASS: two bubbles, real loopback, one Page 5 added and opened.");
                        EditorApplication.update -= Tick;
                        break;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (s_caption != null) s_caption.text = "FAIL: see Console";
                EditorApplication.update -= Tick;
            }
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            SceneManager.sceneLoaded -= SetupStage;
            SessionState.EraseBool(Running);
            if (s_preview != null) UnityEngine.Object.DestroyImmediate(s_preview);
        }
    }
}

