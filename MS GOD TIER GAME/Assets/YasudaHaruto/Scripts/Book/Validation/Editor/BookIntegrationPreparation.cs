using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YasudaHaruto.BookValidation.Editor
{
    public static class BookIntegrationPreparation
    {
        public const string Candidate = "Assets/YasudaHaruto/Scenes/BookIntegrationCandidate.unity";
        private const string Source = "Assets/YasudaHaruto/Scenes/BookScene.unity";

        // User approved applying the reviewed candidate after the naming audit.
        [MenuItem("Tools/YasudaHaruto/Apply Reviewed Book Integration")]
        public static void ApplyReviewedIntegration()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("An open scene has unsaved changes.");
            byte[] original = File.ReadAllBytes(Source);
            using (var hash = SHA256.Create())
                if (BitConverter.ToString(hash.ComputeHash(original)).Replace("-", "") !=
                    "889C979BFEB26C0498336CDA0EAB28FAAE71426E514176A7D0704D0C21219E46")
                    throw new InvalidOperationException("BookScene has changed since candidate preparation. Review the new changes first.");
            if (!File.Exists(Candidate)) throw new InvalidOperationException("Reviewed candidate does not exist.");
            byte[] metadata = File.ReadAllBytes(Source + ".meta");
            File.WriteAllBytes(Source, File.ReadAllBytes(Candidate));
            AssetDatabase.ImportAsset(Source, ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
            if (Convert.ToBase64String(metadata) != Convert.ToBase64String(File.ReadAllBytes(Source + ".meta")))
                throw new InvalidOperationException("Scene metadata changed unexpectedly.");
            Debug.Log("Reviewed integration applied to BookScene. Scene GUID, hatake sources and connection scene retained.");
        }

        [MenuItem("Tools/YasudaHaruto/Prepare Integration Candidate")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("An open scene has unsaved changes.");
            if (File.Exists(Candidate)) throw new InvalidOperationException("Candidate already exists; it will not be overwritten.");
            byte[] original = File.ReadAllBytes(Source);
            string yaml = Encoding.UTF8.GetString(original);
            string[] sourceScripts =
            {
                "Assets/hatake/book/scripts/BookController.cs",
                "Assets/YasudaHaruto/Scripts/Book/BookGameController.cs",
                "Assets/YasudaHaruto/Scripts/Book/BookGameBootstrap.cs",
                "Assets/YasudaHaruto/Scripts/Book/Input/BookGameDragInput.cs",
                "Assets/YasudaHaruto/Scripts/Book/Input/BookFlipCoordinateMapper.cs",
                "Assets/YasudaHaruto/Scripts/Book/Input/BookPageAreaDetector.cs",
                "Assets/YasudaHaruto/Scripts/Book/Input/BookFlipCoordinateTest.cs"
            };
            foreach (string source in sourceScripts)
            {
                string name = source.Contains("/hatake/") ? "YasudaBookViewController" : Path.GetFileNameWithoutExtension(source);
                string destination = "Assets/YasudaHaruto/Scripts/Book/Validation/" + name + ".cs";
                string oldGuid = AssetDatabase.AssetPathToGUID(source);
                string newGuid = AssetDatabase.AssetPathToGUID(destination);
                if (string.IsNullOrEmpty(oldGuid) || string.IsNullOrEmpty(newGuid)) throw new InvalidOperationException("Missing script: " + source);
                yaml = Regex.Replace(yaml, @"(?ms)^--- !u!114 &.*?(?=^--- |\z)", match =>
                {
                    if (!match.Value.Contains("guid: " + oldGuid + ",")) return match.Value;
                    // Preserve the disconnected detector used by the inactive legacy test rig.
                    if (source.EndsWith("/BookPageAreaDetector.cs") && match.Value.Contains("m_mapper: {fileID: 0}"))
                        return match.Value;
                    string block = match.Value.Replace(oldGuid, newGuid);
                    return Regex.Replace(block, @"(?m)^  m_EditorClassIdentifier:.*$",
                        "  m_EditorClassIdentifier: Assembly-CSharp::YasudaHaruto.BookValidation." + name);
                });
            }
            // The original keyboard and drag components depend on the original Controller type.
            yaml = Regex.Replace(yaml, @"(?ms)^--- !u!114 &.*?(?=^--- |\z)", match =>
            {
                if (!Regex.IsMatch(match.Value, @"Assembly-CSharp::hatake.BookFlip(KeyboardActions|DragInput)")) return match.Value;
                return Regex.Replace(match.Value.Replace("m_Enabled: 1", "m_Enabled: 0"),
                    @"m_bookController: \{fileID: \d+\}", "m_bookController: {fileID: 0}");
            });
            File.WriteAllText(Candidate, yaml, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(Candidate, ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.OpenScene(Candidate, OpenSceneMode.Single);
            BookGameController game = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var controller in root.GetComponentsInChildren<BookGameController>(true))
                {
                    if (!controller.gameObject.activeInHierarchy) continue;
                    if (game != null) throw new InvalidOperationException("Multiple active inventory controllers.");
                    game = controller;
                    var fields = new SerializedObject(game);
                    fields.FindProperty("m_inventorySettings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BookInventorySettings>(
                        "Assets/YasudaHaruto/Data/BookInventorySettings.asset");
                    fields.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var bootstrap in root.GetComponentsInChildren<BookGameBootstrap>(true))
                {
                    var fields = new SerializedObject(bootstrap);
                    fields.FindProperty("m_pageDatabase").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PageDatabase>(
                        "Assets/YasudaHaruto/Data/PageDatabase/TestPageDatabase.asset");
                    fields.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var debug in root.GetComponentsInChildren<BookFlipCoordinateTest>(true)) debug.enabled = false;
            }
            if (game == null) throw new InvalidOperationException("No active inventory controller.");
            var uiAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BookReplacementUIBuilder.Path);
            if (uiAsset == null) throw new InvalidOperationException("Selection UI prefab is missing.");
            var ui = (GameObject)PrefabUtility.InstantiatePrefab(uiAsset, scene);
            var uiFields = new SerializedObject(ui.GetComponent<BookPageReplacementPanel>());
            uiFields.FindProperty("m_controller").objectReferenceValue = game;
            uiFields.ApplyModifiedPropertiesWithoutUndo();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                        throw new InvalidOperationException("Missing script on " + transform.name);
            EditorSceneManager.SaveScene(scene, Candidate);
            byte[] after = File.ReadAllBytes(Source);
            if (Convert.ToBase64String(original) != Convert.ToBase64String(after)) throw new InvalidOperationException("Source scene changed unexpectedly.");
            Debug.Log("Integration candidate prepared. Source BookScene and Build Settings were not modified.");
        }
    }
}
