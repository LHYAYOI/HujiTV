using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;


/// <summary>
/// シーンにFogとかを追加するためのメニュー
namespace LightShaftLab.Editor
{
    [CustomEditor(typeof(LocalFogVolume)), CanEditMultipleObjects]
    public sealed class LocalFogVolumeInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("全体の空気感はGlobal Volumeで設定します。この領域では密度と太陽散乱を局所的に強調できます。Sun Boostは空間God Ray用です。", MessageType.Info);
            DrawDefaultInspector();
            if (GUILayout.Button("見せ場プリセット（太陽散乱＋柔らかい霧）"))
                foreach (var item in targets)
                {
                    var fog = (LocalFogVolume)item;
                    Undo.RecordObject(fog, "Apply God Ray Highlight");
                    fog.m_density = 0.025f; fog.m_sunBoost = 2; fog.m_priority = 3;
                    fog.m_edgeFade = 0.5f; fog.m_subtractFlag = false;
                    EditorUtility.SetDirty(fog);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(fog);
                }
            EditorGUILayout.HelpBox("Priorityは選別倍率、Sun Boostは領域内の太陽散乱の追加倍率です。2なら最大3倍。領域が重なる場合は最大の強調値を使用します。局所霧と共通の16枠を使用します。", MessageType.None);
        }
    }

    public static class AtmosphereAuthoring
    {

        [MenuItem("Tools/LightShaft/Fogを追加/箱型の霧", false, 12)]
        public static void CreateBoxFog() => CreateFog(LocalFogVolume.SHAPE.BOX);

        [MenuItem("Tools/LightShaft/Fogを追加/球型の霧", false, 13)]
        public static void CreateSphereFog() => CreateFog(LocalFogVolume.SHAPE.SPHERE);

        [MenuItem("Tools/LightShaft/Fogを追加/箱型の霧", true)]
        [MenuItem("Tools/LightShaft/Fogを追加/球型の霧", true)]
        static bool CanCreateFog() => !EditorApplication.isPlayingOrWillChangePlaymode &&
            UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() == null;

        static void CreateFog(LocalFogVolume.SHAPE shape)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("局所Fogを追加");
            var selected = Selection.activeTransform;
            Vector3 position = selected && selected.gameObject.scene.IsValid() ? selected.position :
                SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            var go = new GameObject(shape == LocalFogVolume.SHAPE.BOX ? "Fog（箱型）" : "Fog（球型）");
            Undo.RegisterCreatedObjectUndo(go, "局所Fogを追加");
            go.transform.position = position;
            go.transform.localScale = new Vector3(6, 4, 6);
            var fog = Undo.AddComponent<LocalFogVolume>(go);
            fog.m_shape = shape;
            fog.m_density = 0.05f;
            fog.m_edgeFade = 0.5f;
            fog.m_sunBoost = 0;
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            Undo.CollapseUndoOperations(group);
        }

        [MenuItem("GameObject/Light Shaft/God Ray Highlight Region", false, 10)]
        public static void CreateHighlight()
        {
            var go = new GameObject("God Ray Highlight");
            Undo.RegisterCreatedObjectUndo(go, "Create God Ray Highlight");
            if (Selection.activeTransform) go.transform.position = Selection.activeTransform.position;
            go.transform.localScale = new Vector3(6, 5, 6);
            var fog = Undo.AddComponent<LocalFogVolume>(go);
            fog.m_density = 0.025f; fog.m_sunBoost = 2; fog.m_priority = 3; fog.m_edgeFade = 0.5f;
            Selection.activeGameObject = go;
        }

        [MenuItem("GameObject/Light Shaft/Global God Ray Atmosphere", false, 11)]
        public static void CreateAtmosphere()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Atmosphere Profile", "GodRayGlobal", "asset", "全体の空気感を保存するProfileを指定");
            if (string.IsNullOrEmpty(path)) return;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, AssetDatabase.GenerateUniqueAssetPath(path));
            var settings = profile.Add<LightShaftVolume>(true);
            settings.m_enableFlag.value = true; settings.m_enableSpatialGodRaysFlag.value = true;
            settings.m_density.value = 0.012f; settings.m_spatialGodRayIntensity.value = 0.7f;
            AssetDatabase.AddObjectToAsset(settings, profile); AssetDatabase.SaveAssets();
            var go = new GameObject("Global God Ray Atmosphere");
            Undo.RegisterCreatedObjectUndo(go, "Create Global Atmosphere");
            var volume = Undo.AddComponent<Volume>(go); volume.isGlobal = true; volume.sharedProfile = profile;
            Selection.activeGameObject = go;
            Debug.Log("Global atmosphere created. Use a Renderer with LightShaftFeature and a shadow-casting Main Directional Light.", go);
        }



    }
}
