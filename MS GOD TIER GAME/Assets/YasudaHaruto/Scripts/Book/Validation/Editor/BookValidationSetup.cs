using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YasudaHaruto.BookValidation.Editor
{
    // Runs only from the explicit menu command; never rewrites the source scene.
    [InitializeOnLoad]
    public static class BookValidationSetup
    {
        private const string ScenePath = "Assets/YasudaHaruto/Scenes/BookValidation.unity";
        private const string PrefabPath = "Assets/YasudaHaruto/Prefabs/BookValidation.prefab";
        private static string ReportPath => SessionState.GetBool(Preview, false)
            ? "Assets/YasudaHaruto/BookValidationPreview.txt" : "Assets/YasudaHaruto/BookValidationResults.txt";
        private const string Preview = "YasudaBookValidation.Preview";
        private const string Running = "YasudaBookValidation.Running";
        private static int s_step;
        private static double s_next;
        private static double s_deadline;
        private static YasudaBookViewController s_view;
        private static BookGameController s_game;
        private static PageData s_sample;
        private static PageInstance s_survivor;
        private static int s_count;
        private static PageData s_endSample;
        private static BookPageReplacementPanel s_panel;
        private static bool s_selectionReopened;

        [MenuItem("Tools/YasudaHaruto/Preview Page Selection")]
        public static void PreviewSelection()
        {
            VerifyExisting(true);
        }

        private static void ConfigureInventory()
        {
            const string settingsPath = "Assets/YasudaHaruto/Data/BookInventorySettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<BookInventorySettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<BookInventorySettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (var game in root.GetComponentsInChildren<BookGameController>(true))
                {
                    var serialized = new SerializedObject(game);
                    serialized.FindProperty("m_inventorySettings").objectReferenceValue = settings;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var bootstrap in root.GetComponentsInChildren<BookGameBootstrap>(true))
                {
                    var serialized = new SerializedObject(bootstrap);
                    serialized.FindProperty("m_pageDatabase").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<PageDatabase>("Assets/YasudaHaruto/Data/PageDatabase/TestPageDatabase.asset");
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                BookReplacementUIBuilder.Attach(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static BookValidationSetup()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("Tools/YasudaHaruto/Prepare and Verify Book Validation")]
        public static void PrepareAndVerify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes. Save it yourself before validation.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                throw new InvalidOperationException("Validation prefab already exists; it will not be overwritten.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        throw new InvalidOperationException("Missing script on " + transform.name);

            var container = new GameObject("BookValidationRoot");
            foreach (var root in scene.GetRootGameObjects())
                if (root != container) root.transform.SetParent(container.transform, true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(container, PrefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, ScenePath);
            ConfigureInventory();
            File.WriteAllText(ReportPath, "Prepared dedicated scene and prefab.\n");
            StartSmokeCheck();
        }

        [MenuItem("Tools/YasudaHaruto/Verify Existing Book Validation")]
        public static void VerifyExisting()
        {
            VerifyExisting(false);
        }

        private static void VerifyExisting(bool preview)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes. Save it yourself first.");
            SessionState.SetBool(Preview, preview);
            ConfigureInventory();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            File.WriteAllText(ReportPath, "Verifying existing dedicated scene.\n");
            StartSmokeCheck();
        }

        private static void StartSmokeCheck()
        {
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/YasudaHaruto/Verify Integrated Book Scene")]
        public static void VerifyIntegratedBookScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("An open scene has unsaved changes.");
            SessionState.SetBool(Preview, false);
            EditorSceneManager.OpenScene("Assets/YasudaHaruto/Scenes/BookScene.unity", OpenSceneMode.Single);
            File.WriteAllText(ReportPath, "Verifying integrated BookScene.\n");
            StartSmokeCheck();
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                s_step = 0;
                s_selectionReopened = false;
                s_next = EditorApplication.timeSinceStartup + 3;
                s_deadline = EditorApplication.timeSinceStartup + 120;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (s_endSample != null) UnityEngine.Object.DestroyImmediate(s_endSample);
                SessionState.SetBool(Running, false);
                SessionState.SetBool(Preview, false);
                EditorApplication.update -= Tick;
                AssetDatabase.Refresh();
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            File.AppendAllText(ReportPath, "PASS: " + message + "\n");
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > s_deadline)
                    throw new TimeoutException("Book operation did not finish within 120 seconds.");
                if (EditorApplication.timeSinceStartup < s_next) return;
                if (s_view != null && (s_view.IsBusy || s_view.FlipView.IsTurning)) return;
                switch (s_step++)
                {
                    case 0:
                        s_view = UnityEngine.Object.FindFirstObjectByType<YasudaBookViewController>();
                        s_game = UnityEngine.Object.FindFirstObjectByType<BookGameController>();
                        s_panel = UnityEngine.Object.FindFirstObjectByType<BookPageReplacementPanel>();
                        Require(s_panel != null && !s_panel.IsVisible, "Selection panel starts hidden");
                        Require(s_view != null && s_game != null, "Dedicated controllers instantiated");
                        Require(s_view.CurrentTexture != null, "Initial render texture captured");
                        Require(s_view.OpenBook(1), "Open at game spread accepted");
                        break;
                    case 1:
                        Require(!s_view.IsClosed && s_view.CurrentSpread == 1 && s_game.CurrentPage != null,
                            "Game spread active after opening");
                        var bootstrap = UnityEngine.Object.FindFirstObjectByType<BookGameBootstrap>();
                        var rune = (RuneTraceController)typeof(BookGameBootstrap)
                            .GetField("m_runeTraceController", BindingFlags.Instance | BindingFlags.NonPublic)
                            .GetValue(bootstrap);
                        Require(rune.CanBeginStroke, "Rune interaction ready for pointer input");
                        s_view.ShowNextSpread();
                        break;
                    case 2:
                        Require(s_view.CurrentSpread == 2 && s_game.CurrentPage == null,
                            "Last fixed spread has no game interaction");
                        s_view.ShowPreviousSpread();
                        break;
                    case 3:
                        Require(s_view.CurrentSpread == 1 && s_game.CurrentPage != null,
                            "Previous spread restores game interaction");
                        Require(s_view.CloseBook(), "Close accepted");
                        break;
                    case 4:
                        Require(s_view.IsClosed && s_game.CurrentPage == null, "Closing ends interaction");
                        Require(s_view.OpenBook(1), "Reopen accepted");
                        break;
                    case 5:
                        Require(!s_view.IsClosed && s_game.CurrentPage != null, "Reopening restores interaction");
                        s_sample = s_game.CurrentPage.Data;
                        s_survivor = s_game.CurrentPage;
                        Require(s_game.RequestAdd(null) == PAGE_ADD_RESULT.INVALID, "Null page rejected");
                        break;
                    case 6:
                        CheckInventory();
                        if (s_game.Pages.Count < s_game.MaxPageCount)
                        {
                            Require(s_game.RequestAdd(s_sample) == PAGE_ADD_RESULT.ACCEPTED,
                                "Duplicate page accepted below limit");
                            s_step--;
                        }
                        else
                        {
                            Require(s_game.Pages[0] != s_game.Pages[1], "Duplicate definitions have distinct instances");
                            s_count = s_game.Pages.Count;
                            Require(s_game.RequestAdd(s_sample) == PAGE_ADD_RESULT.NEEDS_SELECTION,
                                "Full inventory requests selection");
                            Require(s_game.Pages.Count == s_count && s_game.IsInputBlocked, "Selection preserves inventory and blocks input");
                            Require(s_panel.IsVisible && !s_panel.CanConfirm, "Selection opens without a default destructive choice");
                            if (SessionState.GetBool(Preview, false))
                            {
                                EditorApplication.update -= Tick;
                                // Interactive preview remains in Play Mode until the user stops it.
                                return;
                            }
                        }
                        break;
                    case 7:
                        if (!s_selectionReopened)
                        {
                        ClickPanelButton("DeclineButton");
                        Require(!s_panel.IsVisible && s_game.PendingPage == null, "Decline button closes selection");
                        Require(s_game.Pages.Count == s_count && s_game.CurrentPage == s_survivor, "Decline preserves viewed instance");
                        Require(s_game.RequestAdd(s_sample) == PAGE_ADD_RESULT.NEEDS_SELECTION, "Selection can reopen");
                            s_selectionReopened = true;
                            s_step--;
                            // Newly instantiated graphics receive their draw depth on the next rendered frame.
                            break;
                        }
                        var rows = s_panel.GetComponentsInChildren<BookPageChoiceRow>();
                        Require(rows.Length == s_count, "Each duplicate has its own selection row");
                        Canvas.ForceUpdateCanvases();
                        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                        pointer.position = RectTransformUtility.WorldToScreenPoint(null,
                            rows[0].GetComponent<RectTransform>().TransformPoint(rows[0].GetComponent<RectTransform>().rect.center));
                        var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
                        File.AppendAllText(ReportPath, $"UI raycast at {pointer.position}, screen {Screen.width}x{Screen.height}, hits={hits.Count}\n");
                        foreach (var hit in hits)
                            File.AppendAllText(ReportPath, $"Hit: {hit.gameObject.name} / {hit.gameObject.transform.parent?.name}\n");
                        Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<BookPageChoiceRow>() == rows[0],
                            "Visible row receives UI raycasts above the book");
                        rows[rows.Length - 1].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                        Require(s_panel.CanConfirm && s_panel.SelectedPage == s_game.Pages[s_game.Pages.Count - 1],
                            "Row button selects the exact owned instance");
                        Require(s_game.Pages.Count == s_count && !s_game.IsEditing, "Selecting a row does not edit inventory");
                        ClickPanelButton("ConfirmButton");
                        Require(s_game.IsEditing && !s_panel.CanConfirm, "Confirm button starts edit and blocks repeated confirmation");
                        break;
                    case 8:
                        CheckInventory();
                        Require(s_game.PendingPage == null && s_game.Pages.Count == s_count && s_game.CurrentPage == s_survivor,
                            "Atomic replacement preserves count and viewed instance");
                        Require(!s_panel.IsVisible, "Selection closes after successful replacement");
                        Require(s_game.RequestAdd(s_sample) == PAGE_ADD_RESULT.NEEDS_SELECTION, "Viewed replacement requested");
                        Require(s_game.ReplacePending(s_survivor), "Viewed instance replacement accepted");
                        break;
                    case 9:
                        CheckInventory();
                        Require(s_game.CurrentPage != s_survivor && s_game.CurrentPage != null, "Replacing viewed page selects new instance");
                        Require(!s_game.Remove(s_survivor), "Stale instance cannot remove a duplicate");
                        Require(s_game.Remove(s_game.Pages[s_game.Pages.Count - 1]), "Last game spread removed");
                        break;
                    case 10:
                        CheckInventory();
                        s_count = s_game.Pages.Count;
                        Require(s_game.RequestAdd(s_sample) == PAGE_ADD_RESULT.ACCEPTED, "Edit accepted before interruption");
                        s_view.enabled = false;
                        Require(!s_game.IsEditing && s_game.Pages.Count == s_count, "Disable cancels uncommitted edit");
                        s_view.enabled = true;
                        break;
                    case 11:
                        CheckInventory();
                        if (s_game.Pages.Count > 0)
                        {
                            Require(s_game.Remove(s_game.Pages[0]), "First game spread removed");
                            s_step--;
                        }
                        break;
                    case 12:
                        CheckInventory();
                        Require(s_game.Pages.Count == 0 && s_view.CurrentSpread == 0, "Empty inventory shows first fixed spread");
                        Require(s_game.RequestAdd(s_sample) == PAGE_ADD_RESULT.ACCEPTED, "First page can be added to empty inventory");
                        break;
                    case 13:
                        CheckInventory();
                        Require(s_game.Pages.Count == 1, "Empty-to-one addition completed");
                        s_survivor = s_game.Pages[0];
                        s_view.ShowSpread(1);
                        break;
                    case 14:
                        s_endSample = UnityEngine.Object.Instantiate(s_sample);
                        var sampleSettings = new SerializedObject(s_endSample);
                        sampleSettings.FindProperty("m_insertType").intValue = 2;
                        sampleSettings.ApplyModifiedPropertiesWithoutUndo();
                        Require(s_game.RequestAdd(s_endSample) == PAGE_ADD_RESULT.INVALID, "Legacy RANDOM is rejected");
                        sampleSettings.FindProperty("m_insertType").intValue = 0;
                        sampleSettings.ApplyModifiedPropertiesWithoutUndo();
                        Require(s_game.RequestAdd(s_endSample) == PAGE_ADD_RESULT.ACCEPTED, "END insertion accepted");
                        break;
                    default:
                        CheckInventory();
                        Require(s_game.Pages.Count == 2 && s_game.Pages[1].Data == s_endSample &&
                            s_game.CurrentPage == s_survivor, "END insertion preserves viewed instance and precedes fixed page");
                        File.AppendAllText(ReportPath, "COMPLETE: Inventory and controller checks passed. Pointer tracing and networking were not exercised.\n");
                        EditorApplication.update -= Tick;
                        EditorApplication.ExitPlaymode();
                        break;
                }
                s_next = EditorApplication.timeSinceStartup + 0.5;
            }
            catch (Exception exception)
            {
                File.AppendAllText(ReportPath, "FAIL: " + exception + "\n");
                Debug.LogException(exception);
                EditorApplication.update -= Tick;
                EditorApplication.ExitPlaymode();
            }
        }

        private static void CheckInventory()
        {
            Require(s_view.PageCount == 3 + s_game.Pages.Count * 2, "Display count matches logical inventory");
            for (int i = 0; i < s_game.Pages.Count; i++)
                Require(s_view.GetPage(2 + i * 2) == s_game.Pages[i].Data.LeftVisualData &&
                    s_view.GetPage(3 + i * 2) == s_game.Pages[i].Data.RightVisualData, "Visual pair matches instance " + i);
            Require(s_view.IsCoverInside(0) && s_view.GetPage(1) != null && s_view.GetPage(s_view.PageCount - 1) != null,
                "Fixed pages preserved");
        }

        private static void ClickPanelButton(string name)
        {
            foreach (var button in s_panel.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (button.name == name) { button.onClick.Invoke(); return; }
            throw new InvalidOperationException("Button not found: " + name);
        }
    }
}
