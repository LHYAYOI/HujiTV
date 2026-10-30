using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace YasudaHaruto.BookValidation.Editor
{
    // Explicitly tests the currently open scene; never saves or opens another scene.
    [InitializeOnLoad]
    public static class BookManagementValidation
    {
        private const string Running = "Yasuda.BookManagementValidation";
        private const string Report = "Temp/BookRefactorValidation/runtime-result.txt";
        private static int s_step;
        private static double s_deadline;
        private static BookGameController s_game;
        private static YasudaBookViewController s_view;
        private static PageInstance s_original;
        private static PageData s_sample;
        private static int s_count;
        private static PageData s_endPage;

        static BookManagementValidation()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Running, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    s_step = 0;
                    s_game = null;
                    s_view = null;
                    s_deadline = EditorApplication.timeSinceStartup + 90;
                    EditorApplication.update += Tick;
                }
                else if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Running, false);
                    EditorApplication.update -= Tick;
                    s_game = null;
                    s_view = null;
                }
            };
        }

        [MenuItem("Tools/YasudaHaruto/Verify Book Management In Current Scene")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory("Temp/BookRefactorValidation");
            File.WriteAllText(Report, "Current scene; no scene save or replacement.\n");
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            File.AppendAllText(Report, "PASS: " + message + "\n");
        }

        private static void CheckProjection()
        {
            Require(s_view.PageCount == 3 + 2 * s_game.Pages.Count, "Fixed slots and spread count agree");
            for (int i = 0; i < s_game.Pages.Count; i++)
                Require(s_view.GetPage(2 + i * 2) == s_game.Pages[i].Data.LeftVisualData &&
                    s_view.GetPage(3 + i * 2) == s_game.Pages[i].Data.RightVisualData,
                    "Visual pair matches owned spread " + i);
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (EditorApplication.timeSinceStartup > s_deadline) throw new TimeoutException("Book management timed out.");
                if (s_view != null && !s_view.IsReady) return;
                switch (s_step++)
                {
                    case 0:
                        s_game = UnityEngine.Object.FindFirstObjectByType<BookGameController>();
                        Require(s_game != null && s_game.IsInitialized, "Game initialized from its own page definitions");
                        s_view = s_game.BookView;
                        s_count = s_game.Pages.Count;
                        Require(s_count > 0, "Current scene has game spreads");
                        s_sample = s_game.Pages[0].Data;
                        CheckProjection();
                        Require(typeof(YasudaBookViewController).GetField("m_bookData", BindingFlags.Instance | BindingFlags.NonPublic) == null,
                            "Presentation no longer has a second authored book list");
                        Require(typeof(YasudaBookViewController).GetMethod("AddPage") == null &&
                            typeof(YasudaBookViewController).GetMethod("BurnSpread") == null,
                            "Presentation cannot independently edit game inventory");
                        break;
                    case 1:
                        Require(s_view.CurrentTexture != null && s_view.IsClosed, "Initial closed book captured");
                        Require(s_game.CanNavigate && !s_game.CanRemoveObstacles, "Closed book allows navigation only");
                        Require(s_view.OpenBook(1), "Open game spread accepted");
                        break;
                    case 2:
                        s_original = s_game.CurrentPage;
                        Require(s_original == s_game.Pages[0], "First game spread activates exact owned instance");
                        Require(s_game.GetCurrentRuneImage() != null, "Current rune image remains available");
                        Require(s_game.RequestAdd(null) == PAGE_ADD_RESULT.INVALID, "Null request rejected");
                        s_endPage = UnityEngine.Object.Instantiate(s_sample);
                        typeof(PageData).GetField("m_insertType", BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(s_endPage, PAGE_INSERT_TYPE.END);
                        break;
                    case 3:
                        if (s_game.Pages.Count < s_game.MaxPageCount)
                        {
                            Require(s_game.RequestAdd(s_endPage) == PAGE_ADD_RESULT.ACCEPTED, "Append accepted");
                            s_step--;
                        }
                        else
                        {
                            CheckProjection();
                            Require(s_game.CurrentPage == s_original, "Append preserves viewed instance");
                            Require(s_game.RequestAdd(s_endPage) == PAGE_ADD_RESULT.NEEDS_SELECTION, "Capacity opens replacement selection");
                            Require(!s_game.CanNavigate && !s_game.CanRemoveObstacles, "Selection blocks page and obstacle input");
                            Require(s_game.DeclinePending(), "Incoming page can be declined");
                            Require(s_game.CurrentPage == s_original, "Decline restores original interaction");
                        }
                        break;
                    case 4:
                        Require(s_game.RequestAdd(s_sample) == PAGE_ADD_RESULT.NEEDS_SELECTION, "Second capacity request accepted");
                        Require(s_game.ReplacePending(s_original), "Viewed spread replacement accepted");
                        break;
                    case 5:
                        CheckProjection();
                        Require(s_game.CurrentPage != null && s_game.CurrentPage != s_original && s_game.PendingPage == null,
                            "Replacement commits new individual and dismisses selection");
                        Require(!s_game.Remove(s_original), "Stale individual cannot remove duplicate definition");
                        s_original = s_game.CurrentPage;
                        Require(s_game.Remove(s_game.Pages[s_game.Pages.Count - 1]), "Last owned spread removal accepted");
                        break;
                    case 6:
                        CheckProjection();
                        s_count = s_game.Pages.Count;
                        Require(s_game.RequestAdd(s_endPage) == PAGE_ADD_RESULT.ACCEPTED, "Edit accepted before interruption");
                        s_view.enabled = false;
                        Require(!s_game.IsEditing && s_game.Pages.Count == s_count, "Disable rolls back uncommitted inventory");
                        s_view.enabled = true;
                        break;
                    case 7:
                        CheckProjection();
                        s_view.ShowSpread(1);
                        break;
                    case 8:
                        Require(s_game.CurrentPage != null, "Interaction restored after interrupted capture");
                        s_original = s_game.CurrentPage;
                        Require(s_game.Remove(s_original), "Visible spread removal accepted");
                        break;
                    case 9:
                        CheckProjection();
                        Require(s_game.CurrentPage != s_original, "Removed interaction no longer current");
                        Require(s_view.CloseBook(), "Close accepted after edits");
                        break;
                    case 10:
                        Require(s_game.CurrentPage == null && s_view.IsClosed, "Close ends game interaction");
                        Require(s_view.OpenBook(1), "Reopen accepted after edits");
                        break;
                    case 11:
                        Require(s_game.CurrentPage == s_game.Pages[0] && s_game.CanNavigate, "Reopen restores owned spread and navigation");
                        File.AppendAllText(Report, "BookManagement COMPLETE\n");
                        Finish();
                        break;
                }
            }
            catch (Exception exception)
            {
                File.AppendAllText(Report, "FAIL: " + exception + "\n");
                Debug.LogException(exception);
                Finish();
            }
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            if (s_endPage != null) UnityEngine.Object.Destroy(s_endPage);
            EditorApplication.ExitPlaymode();
        }
    }
}
