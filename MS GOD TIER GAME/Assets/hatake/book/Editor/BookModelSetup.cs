using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class BookModelSetup
{
    [MenuItem("Tools/Book/Connect Provided 3D Model")]
    private static void ConnectModel()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("停止してから実行してください。");
            return;
        }

        BookController controller = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<BookController>() : null;

        if (controller == null)
        {
            Debug.LogError("HierarchyのBookController付きBookManagerを選択してください。");
            return;
        }
        SerializedObject controllerObject = new SerializedObject(controller);

        if (controllerObject.FindProperty("m_modelView").objectReferenceValue != null)
        {
            Debug.LogError("このBookControllerはすでに3Dモデルに接続されています。");
            return;
        }

        string[] fields = { "m_bookData", "m_leftPageView", "m_rightPageView",
            "m_captureCamera", "m_textureA", "m_textureB", "m_displayImage" };

        foreach (string field in fields)
        {
            if (controllerObject.FindProperty(field).objectReferenceValue == null)
            {
                Debug.LogError("先にBookControllerの参照を設定してください: " + field);
                return;
            }
        }

        if (GraphicsSettings.currentRenderPipeline != null &&
            !GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal"))
        {
            Debug.LogError("このセットアップはURPまたはBuiltのみ");
            return;
        }


        string modelPath = AssetDatabase.GUIDToAssetPath("77c5fdb27a59e9d4d8f3a6c981979c6c");
        string clipPath = AssetDatabase.GUIDToAssetPath("ae175b7bcef45a3488601760020d7cd5");
        string atlasPath = AssetDatabase.GUIDToAssetPath("982f4d5777ca32243bd7444e5c31d805");

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
        AnimationClip clip = string.IsNullOrEmpty(clipPath) ? null :
            AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>()
                .FirstOrDefault(item => !item.name.StartsWith("__preview__"));
        Shader shader = Shader.Find("Book/Model Atlas");


        if (modelAsset == null || clip == null || atlas == null || shader == null)
        {
            Debug.LogError("book_anim.unitypackageと接続用Shaderをインポートしてください。");
            return;
        }

        if (clip.legacy)
        {
            Debug.LogError("page_anim.fbxのRigをGenericにしてください。");
            return;
        }

        RenderTexture textureA = (RenderTexture)controllerObject.FindProperty("m_textureA").objectReferenceValue;
        RenderTexture textureB = (RenderTexture)controllerObject.FindProperty("m_textureB").objectReferenceValue;

        if (textureA == textureB || textureA.width != 800 || textureA.height != 450 ||
            textureB.width != 800 || textureB.height != 450)
        {
            Debug.LogError("A/Bには別々の800x450 Render Textureを指定してください。");
            return;
        }

        int layer = EnsureLayer("BookModel");
        if (layer < 0) return;
        string folder = CreateOutputFolder();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Connect book model");

        GameObject stage = new GameObject("BookModelStage");
        Undo.RegisterCreatedObjectUndo(stage, "Create book model stage");

        // 選択したManagerと同じシーンに作成します。
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(stage, controller.gameObject.scene);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, stage.transform);


        Renderer bookRenderer = model.GetComponentsInChildren<Renderer>(true)
            .FirstOrDefault(item => item.name == "book");


        SkinnedMeshRenderer pageRenderer = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .FirstOrDefault(item => item.name == "page");

        if (bookRenderer == null || pageRenderer == null)
        {
            Undo.DestroyObjectImmediate(stage);
            Debug.LogError("book Rendererまたはpage SkinnedMeshRendererが見つかりません。");
            return;
        }
        foreach (Transform child in stage.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;

        Material bookMaterial = new Material(shader) { name = "BookBodyMaterial" };
        bookMaterial.SetTexture("_MainTex", atlas);
        AssetDatabase.CreateAsset(bookMaterial, folder + "/BookBodyMaterial.mat");
        Material pageMaterial = new Material(shader) { name = "BookPageMaterial" };
        pageMaterial.SetTexture("_MainTex", atlas);
        pageMaterial.SetFloat("_PageMode", 1f);
        pageMaterial.SetFloat("_DepthBias", -1f);
        AssetDatabase.CreateAsset(pageMaterial, folder + "/BookPageMaterial.mat");
        bookRenderer.sharedMaterial = bookMaterial;
        pageRenderer.sharedMaterial = pageMaterial;
        pageRenderer.enabled = false;
        pageRenderer.updateWhenOffscreen = true;

        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();

        // Playablesから制御するため、自動ループするControllerは使用しないよ
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;

        RenderTexture modelTexture = new RenderTexture(800, 450, 24, RenderTextureFormat.ARGB32)
        {
            name = "BookModelTexture",
            antiAliasing = 1,
            useMipMap = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        AssetDatabase.CreateAsset(modelTexture, folder + "/BookModelTexture.renderTexture");

        GameObject cameraObject = new GameObject("BookModelCamera", typeof(Camera));
        cameraObject.transform.SetParent(stage.transform, false);
        Camera modelCamera = cameraObject.GetComponent<Camera>();
        modelCamera.orthographic = true;
        modelCamera.aspect = 800f / 450f;
        modelCamera.clearFlags = CameraClearFlags.SolidColor;
        modelCamera.backgroundColor = Color.black;
        modelCamera.nearClipPlane = 0.001f;
        modelCamera.farClipPlane = 100f;
        modelCamera.cullingMask = 1 << layer;
        modelCamera.targetTexture = modelTexture;
        modelCamera.depth = -20;
        modelCamera.enabled = false;
        FrameModel(bookRenderer, modelCamera);

        // 他のCameraが3Dステージを二重描画しないように
        foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera == modelCamera || camera.gameObject.scene != controller.gameObject.scene) continue;
            Undo.RecordObject(camera, "Exclude book model layer");
            camera.cullingMask &= ~(1 << layer);
        }


        BookModelView view = stage.AddComponent<BookModelView>();
        SerializedObject viewObject = new SerializedObject(view);

        viewObject.FindProperty("m_animator").objectReferenceValue = animator;
        viewObject.FindProperty("m_pageClip").objectReferenceValue = clip;
        viewObject.FindProperty("m_bookRenderer").objectReferenceValue = bookRenderer;
        viewObject.FindProperty("m_pageRenderer").objectReferenceValue = pageRenderer;
        viewObject.FindProperty("m_modelCamera").objectReferenceValue = modelCamera;
        viewObject.FindProperty("m_modelTexture").objectReferenceValue = modelTexture;
        viewObject.ApplyModifiedProperties();
        controllerObject.FindProperty("m_modelView").objectReferenceValue = view;
        controllerObject.ApplyModifiedProperties();


        PrefabUtility.RecordPrefabInstancePropertyModifications(bookRenderer);
        PrefabUtility.RecordPrefabInstancePropertyModifications(pageRenderer);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        Selection.activeGameObject = stage;
        Debug.Log("3D本を接続しました。シーンを保存してGameビューでPlayしてください。生成先: " + folder, stage);
    }

    private static int EnsureLayer(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);

        if (layer >= 0) return layer;

        SerializedObject tags = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tags.FindProperty("layers");

        for (int layerIndex = 8; layerIndex < layers.arraySize; layerIndex++)
        {
            SerializedProperty entry = layers.GetArrayElementAtIndex(layerIndex);
            if (!string.IsNullOrEmpty(entry.stringValue)) continue;
            entry.stringValue = layerName;
            tags.ApplyModifiedProperties();
            return layerIndex;
        }

        Debug.LogError("BookModel用の空きUser Layerがありません。");
        return -1;
    }

    private static string CreateOutputFolder()
    {
        string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/Book3DGenerated");
        string guid = AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        return AssetDatabase.GUIDToAssetPath(guid);
    }

    private static void FrameModel(Renderer renderer, Camera camera)
    {
        Mesh mesh = renderer is SkinnedMeshRenderer skinnedRenderer
            ? skinnedRenderer.sharedMesh : renderer.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Vector2[] coordinates = mesh.uv;
        Vector3 right = Vector3.zero;
        Vector3 up = Vector3.zero;
        Vector3 lowU = Vector3.zero, highU = Vector3.zero;
        Vector3 lowV = Vector3.zero, highV = Vector3.zero;
        int lowUCount = 0, highUCount = 0, lowVCount = 0, highVCount = 0;


        for (int index = 0; index < coordinates.Length; index++)
        {
            Vector2 uv = coordinates[index];
            if (uv.x < 0.17733f || uv.x > 0.42605f || uv.y < 0.34029f || uv.y > 0.65621f) continue;
            Vector3 point = renderer.transform.TransformPoint(vertices[index]);
            if (uv.x < 0.178f) { lowU += point; lowUCount++; }
            if (uv.x > 0.425f) { highU += point; highUCount++; }
            if (uv.y < 0.341f) { lowV += point; lowVCount++; }
            if (uv.y > 0.656f) { highV += point; highVCount++; }
        }


        if (lowUCount == 0 || highUCount == 0 || lowVCount == 0 || highVCount == 0)
            throw new InvalidOperationException("提供モデルのページUVを検出できません。元のbook_mdl.fbxを使用してください。");
        right = (highU / highUCount - lowU / lowUCount).normalized;
        up = (highV / highVCount - lowV / lowVCount).normalized;
        up = (up - right * Vector3.Dot(up, right)).normalized;
        Vector3 normal = Vector3.Cross(right, up).normalized;
        Vector3 center = renderer.bounds.center;
        float horizontalExtent = 0f, verticalExtent = 0f;


        foreach (Vector3 vertex in vertices)
        {
            Vector3 offset = renderer.transform.TransformPoint(vertex) - center;
            horizontalExtent = Mathf.Max(horizontalExtent, Mathf.Abs(Vector3.Dot(offset, right)));
            verticalExtent = Mathf.Max(verticalExtent, Mathf.Abs(Vector3.Dot(offset, up)));
        }


        float distance = Mathf.Max(1f, renderer.bounds.size.magnitude * 3f);
        camera.transform.SetPositionAndRotation(center - normal * distance,
            Quaternion.LookRotation(normal, up));
        camera.orthographicSize = Mathf.Max(verticalExtent, horizontalExtent / camera.aspect) * 1.12f;
        camera.farClipPlane = distance * 3f;
    }
}
