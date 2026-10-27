using UnityEditor;
using UnityEngine;

/// <summary>鏡をシーンに追加するメニュー。シーンビューのカメラの方を向けて置く。</summary>
public static class MirrorSetup
{
    private const string MATERIAL_FOLDER = "Assets/hatake/materials/Reflection";
    private const string MIRROR_SHADER = "custom/hatake/Mirror";

    [MenuItem("Tools/hatake/鏡/鏡を追加", false, 10)]
    public static void CreateMirror()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        SceneView view = SceneView.lastActiveSceneView;
        Transform selected = Selection.activeTransform;
        Vector3 position = selected != null && selected.gameObject.scene.IsValid() ? selected.position : (view != null ? view.pivot : Vector3.zero);

        var go = new GameObject("Mirror");
        Undo.RegisterCreatedObjectUndo(go, "鏡を追加");
        // 表（-Z）をシーンビューのカメラへ向け、まっすぐ立てる
        Vector3 away = view != null ? position - view.camera.transform.position : Vector3.forward;
        away.y = 0;
        if (away.sqrMagnitude < 0.0001f)
        {
            away = Vector3.forward;
        }
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(away.normalized, Vector3.up));
        go.transform.localScale = new Vector3(1.2f, 1.8f, 1);
        go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GetOrCreateMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<Mirror>();
        Selection.activeGameObject = go;
        Undo.CollapseUndoOperations(group);
    }

    [MenuItem("Tools/hatake/鏡/鏡を追加", true)]
    private static bool CanCreate()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode && UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() == null;
    }

    private static Material GetOrCreateMaterial()
    {
        string path = $"{MATERIAL_FOLDER}/M_Mirror.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }
        Shader shader = Shader.Find(MIRROR_SHADER);
        if (shader == null)
        {
            Debug.LogError($"シェーダー {MIRROR_SHADER} が見つかりません。");
            return null;
        }
        if (!AssetDatabase.IsValidFolder(MATERIAL_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/hatake/materials", "Reflection");
        }
        material = new Material(shader) { name = "M_Mirror" };
        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();
        return material;
    }
}
