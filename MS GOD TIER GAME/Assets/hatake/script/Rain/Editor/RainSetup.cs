using UnityEditor;
using UnityEngine;

/// <summary>雨漏り・水たまり・ゴースト反射をシーンに追加するメニュー。マテリアルは無ければ作る。</summary>
public static class RainSetup
{
    private const string MATERIAL_FOLDER = "Assets/hatake/materials/Rain";
    private const string DROP_SHADER = "custom/hatake/RainDrop";
    private const string PUDDLE_SHADER = "custom/hatake/Puddle";

    [MenuItem("Tools/hatake/雨漏り/雨漏り＋水たまりを追加", false, 10)]
    public static void CreateLeakWithPuddle()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        GameObject leak = CreateLeakObject(out Vector3 floor, out bool hasFloorFlag);
        if (hasFloorFlag)
        {
            CreatePuddleObject(floor);
        }
        Selection.activeGameObject = leak;
        Undo.CollapseUndoOperations(group);
    }

    [MenuItem("Tools/hatake/雨漏り/雨漏りを追加", false, 11)]
    public static void CreateLeak()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Selection.activeGameObject = CreateLeakObject(out _, out _);
        Undo.CollapseUndoOperations(group);
    }

    [MenuItem("Tools/hatake/雨漏り/水たまりを追加", false, 12)]
    public static void CreatePuddle()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Vector3 pivot = PlacementPosition();
        Vector3 floor = Physics.Raycast(pivot + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 5, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.point : pivot;
        Selection.activeGameObject = CreatePuddleObject(floor);
        Undo.CollapseUndoOperations(group);
    }

    [MenuItem("Tools/hatake/雨漏り/ゴースト反射を追加", false, 30)]
    public static void CreateGhostReflection()
    {
        var existing = Object.FindFirstObjectByType<GhostReflection>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorUtility.DisplayDialog("ゴースト反射", "シーンにはすでに GhostReflection があります。Ghost Layers に水たまりへ映すレイヤーを設定してください。", "閉じる");
            return;
        }
        var go = new GameObject("GhostReflection");
        Undo.RegisterCreatedObjectUndo(go, "ゴースト反射を追加");
        var reflection = Undo.AddComponent<GhostReflection>(go);
        // 「Ghost」という名前のレイヤーがあれば、それを映す
        int ghostLayer = LayerMask.NameToLayer("Ghost");
        if (ghostLayer >= 0)
        {
            reflection.GhostLayers = 1 << ghostLayer;
        }
        Selection.activeGameObject = go;
        if (ghostLayer < 0)
        {
            EditorUtility.DisplayDialog("ゴースト反射", "GhostReflection を追加しました。Ghost Layers に水たまりへ映すレイヤーを設定してください（「Ghost」という名前のレイヤーを作ると自動で選ばれます）。", "閉じる");
        }
    }

    [MenuItem("Tools/hatake/雨漏り/雨漏り＋水たまりを追加", true)]
    [MenuItem("Tools/hatake/雨漏り/雨漏りを追加", true)]
    [MenuItem("Tools/hatake/雨漏り/水たまりを追加", true)]
    [MenuItem("Tools/hatake/雨漏り/ゴースト反射を追加", true)]
    private static bool CanCreate()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode && UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() == null;
    }

    // 天井のすぐ下に雨漏りを置く（天井が見つからなければ選択位置の 2.5m 上）
    private static GameObject CreateLeakObject(out Vector3 floor, out bool hasFloorFlag)
    {
        Vector3 pivot = PlacementPosition();
        Vector3 position = Physics.Raycast(pivot + Vector3.up * 0.2f, Vector3.up, out RaycastHit ceiling, 10, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? ceiling.point + Vector3.down * 0.02f
            : pivot + Vector3.up * 2.5f;
        var go = new GameObject("RainLeak");
        Undo.RegisterCreatedObjectUndo(go, "雨漏りを追加");
        go.transform.position = position;
        var leak = Undo.AddComponent<RainLeak>(go);
        var serialized = new SerializedObject(leak);
        serialized.FindProperty("m_dropMaterial").objectReferenceValue = GetOrCreateMaterial("M_RainDrop", DROP_SHADER, null);
        serialized.FindProperty("m_splashMaterial").objectReferenceValue = GetOrCreateMaterial("M_RainSplash", DROP_SHADER, material => material.SetFloat("_Softness", 0.9f));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        hasFloorFlag = Physics.Raycast(position, Vector3.down, out RaycastHit hit, 12, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        floor = hasFloorFlag ? hit.point : position;
        return go;
    }

    // 床に寝かせた Quad の水たまり
    private static GameObject CreatePuddleObject(Vector3 floor)
    {
        var go = new GameObject("Puddle");
        Undo.RegisterCreatedObjectUndo(go, "水たまりを追加");
        go.transform.SetPositionAndRotation(floor + Vector3.up * 0.003f, Quaternion.Euler(90, Random.Range(0f, 360f), 0));
        go.transform.localScale = new Vector3(1.4f, 1.1f, 1);
        go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GetOrCreateMaterial("M_Puddle", PUDDLE_SHADER, null);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var puddle = go.AddComponent<Puddle>();
        var serialized = new SerializedObject(puddle);
        serialized.FindProperty("m_shapeSeed").intValue = Random.Range(0, 1000);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    private static Vector3 PlacementPosition()
    {
        Transform selected = Selection.activeTransform;
        if (selected != null && selected.gameObject.scene.IsValid())
        {
            return selected.position;
        }
        return SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
    }

    private static Material GetOrCreateMaterial(string name, string shaderName, System.Action<Material> setup)
    {
        string path = $"{MATERIAL_FOLDER}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError($"シェーダー {shaderName} が見つかりません。");
            return null;
        }
        if (!AssetDatabase.IsValidFolder(MATERIAL_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/hatake/materials", "Rain");
        }
        material = new Material(shader) { name = name };
        setup?.Invoke(material);
        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();
        return material;
    }
}
