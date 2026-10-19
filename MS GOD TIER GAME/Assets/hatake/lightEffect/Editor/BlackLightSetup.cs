using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LightShaftLab.Editor
{
    /// <summary>ブラックライトの導入メニュー。既存のRendererへBlackLightFeatureを追加する（Rendererは新規作成しない）。</summary>
    public static class BlackLightSetup
    {
        const string DEMO_FOLDER = "Assets/hatake/lightEffect/demoScenedata/BlackLight";
        const string SAMPLE_FOLDER = "Assets/LightShaft/Demo/BlackLightSample";
        const string LIT_SHADER_NAME = "hatake/BlackLight/Fluorescent Lit", UNLIT_SHADER_NAME = "hatake/BlackLight/Fluorescent Unlit";

        [MenuItem("Tools/LightShaft/ブラックライト/現在のRendererに登録", false, 40)]
        public static void InstallMenu()
        {
            try
            {
                int count = Install();
                EditorUtility.DisplayDialog("ブラックライト", $"現在のURPが使うRenderer {count} 個にBlackLightFeatureを登録しました（登録済みのものはそのまま）。", "閉じる");
            }
            catch (Exception exception) { Debug.LogException(exception); EditorUtility.DisplayDialog("登録できませんでした", exception.Message, "閉じる"); }
        }

        /// <summary>現在のQualityで使うURP Assetの全Universal Rendererへ登録する。戻り値は対象Renderer数。</summary>
        public static int Install()
        {
            var pipeline = (QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (!pipeline) throw new InvalidOperationException("URPの3Dプロジェクトが必要です。使用するURP AssetをGraphics/Quality Settingsに指定してください。");
            var pipelineObject = new SerializedObject(pipeline);
            var rendererList = pipelineObject.FindProperty("m_RendererDataList");
            var renderers = Enumerable.Range(0, rendererList.arraySize)
                .Select(i => rendererList.GetArrayElementAtIndex(i).objectReferenceValue as UniversalRendererData).Where(rendererData => rendererData).Distinct().ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("現在のURPに3D用のUniversal Rendererがありません。");
            foreach (var renderer in renderers) Install(renderer);
            return renderers.Length;
        }

        static void Install(UniversalRendererData renderer)
        {
            var feature = renderer.rendererFeatures.OfType<BlackLightFeature>().FirstOrDefault();
            if (!feature)
            {
                Undo.RecordObject(renderer, "ブラックライト Renderer登録");
                feature = ScriptableObject.CreateInstance<BlackLightFeature>(); feature.name = "Black Light";
                AssetDatabase.AddObjectToAsset(feature, renderer); Undo.RegisterCreatedObjectUndo(feature, "BlackLightFeature作成");
                renderer.rendererFeatures.Add(feature);
            }
            if (!feature.m_decalShader)
            {
                Undo.RecordObject(feature, "蛍光デカールのシェーダー設定");
                feature.m_decalShader = Shader.Find(BlackLightFeature.DECAL_SHADER_NAME);
            }
            feature.SetActive(true); feature.Create();
            var serialized = new SerializedObject(renderer); var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < map.arraySize; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string guid, out long localId);
                map.GetArrayElementAtIndex(i).longValue = localId;
            }
            serialized.ApplyModifiedProperties(); renderer.SetDirty(); EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(feature);
            AssetDatabase.SaveAssetIfDirty(renderer);
        }

        [MenuItem("Tools/LightShaft/ブラックライト/選択ライトをブラックライトにする", false, 41)]
        public static void Attach()
        {
            foreach (var selectedObject in Selection.gameObjects)
            {
                if (!selectedObject.TryGetComponent<Light>(out var light) || (light.type != LightType.Spot && light.type != LightType.Point)) continue;
                Undo.RecordObject(light, "ブラックライト化");
                if (!selectedObject.GetComponent<BlackLightEmitter>()) Undo.AddComponent<BlackLightEmitter>(selectedObject);
                // 光柱はSpotのみ対応。
                if (light.type == LightType.Spot && !selectedObject.GetComponent<LightShaftEmitter>()) Undo.AddComponent<LightShaftEmitter>(selectedObject);
            }
        }

        [MenuItem("Tools/LightShaft/ブラックライト/選択ライトをブラックライトにする", true)]
        static bool CanAttach() => Selection.gameObjects.Any(selectedObject => selectedObject.TryGetComponent<Light>(out var light) && (light.type == LightType.Spot || light.type == LightType.Point));

        [MenuItem("Tools/LightShaft/ブラックライト/デモを現在のシーンに作成", false, 42)]
        public static void BuildDemoMenu()
        {
            try
            {
                var root = BuildDemo();
                Selection.activeGameObject = root; EditorGUIUtility.PingObject(root);
                EditorUtility.DisplayDialog("ブラックライト デモ", "「BlackLight Demo」を作成しました。\n\n周囲が明るいと蛍光が目立ちません。「現在のシーンを暗室にする」で太陽と環境光を消して確認してください。\nライトの BlackLightEmitter で出力と波長、マテリアルの「蛍光（ブラックライト）」で色と効率を調整できます。", "閉じる");
            }
            catch (Exception exception) { Debug.LogException(exception); EditorUtility.DisplayDialog("デモを作成できませんでした", exception.Message, "閉じる"); }
        }

        [MenuItem("Tools/LightShaft/ブラックライト/現在のシーンを暗室にする", false, 44)]
        public static void MakeDarkRoom()
        {
            if (!EditorUtility.DisplayDialog("暗室にする", "ブラックライトが分かるよう、現在のシーンを暗くします。\n\n・Directional Lightを無効化（Undo可）\n・環境光を色指定のほぼ黒、環境反射を0.05にする（Lighting設定。Undoでは戻りません）", "暗くする", "キャンセル")) return;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("暗室にする");
            foreach (var light in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(rootObject => rootObject.GetComponentsInChildren<Light>()))
            {
                if (light.type != LightType.Directional || !light.enabled) continue;
                Undo.RecordObject(light, "暗室にする"); light.enabled = false;
            }
            // 暗い部屋：環境光は暗順応した目で物の輪郭がかろうじて分かる程度。
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.006f, 0.005f, 0.012f);
            RenderSettings.reflectionIntensity = 0.05f;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); Undo.CollapseUndoOperations(group);
        }

        [MenuItem("Tools/LightShaft/ブラックライト/現在のシーンを暗室にする", true)]
        static bool CanMakeDarkRoom() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/LightShaft/ブラックライト/デモを現在のシーンに作成", true)]
        static bool CanBuildDemo() => !EditorApplication.isPlayingOrWillChangePlaymode && PrefabStageUtility.GetCurrentPrefabStage() == null;

        public static GameObject BuildDemo()
        {
            var lit = Shader.Find(LIT_SHADER_NAME); var unlit = Shader.Find(UNLIT_SHADER_NAME);
            if (!lit || !unlit) throw new InvalidOperationException("Fluorescent Lit / Unlit シェーダーが見つかりません（コンパイルエラーがないか確認してください）。");
            Install();
            Directory.CreateDirectory(DEMO_FOLDER); AssetDatabase.Refresh();
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("ブラックライト デモ作成");

            var root = new GameObject("BlackLight Demo"); Undo.RegisterCreatedObjectUndo(root, "ブラックライト デモ作成");

            // 蛍光しない床と箱：ブラックライトの下ではほぼ暗い紫にしか見えない。
            var plain = LoadOrCreate("Plain.mat", Shader.Find("Universal Render Pipeline/Lit"), material => material.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.55f)));
            CreatePrimitive(root, PrimitiveType.Plane, "Floor (no phosphor)", new Vector3(0, 0, 0), new Vector3(1.2f, 1, 1.2f), plain);
            CreatePrimitive(root, PrimitiveType.Cube, "Plain Cube (no phosphor)", new Vector3(-2.4f, 0.5f, 1.2f), Vector3.one, plain);

            CreatePrimitive(root, PrimitiveType.Sphere, "Fluorescent Green", new Vector3(-1.2f, 0.5f, 0), Vector3.one,
                LoadOrCreate("FluorGreen.mat", lit, material => SetupFluorescent(material, new Color(0.35f, 0.4f, 0.35f), new Color(0.15f, 1, 0.3f), 1.5f)));
            CreatePrimitive(root, PrimitiveType.Capsule, "Fluorescent Orange", new Vector3(0, 1, 0.6f), Vector3.one,
                LoadOrCreate("FluorOrange.mat", lit, material => SetupFluorescent(material, new Color(0.45f, 0.35f, 0.3f), new Color(1, 0.35f, 0.05f), 1.5f)));
            CreatePrimitive(root, PrimitiveType.Cube, "Fluorescent Cyan", new Vector3(1.2f, 0.5f, 0), Vector3.one,
                LoadOrCreate("FluorCyan.mat", lit, material => SetupFluorescent(material, new Color(0.3f, 0.4f, 0.45f), new Color(0.1f, 0.8f, 1), 1.5f)));
            // 蛍光増白剤入りの白い布・紙のイメージ。
            CreatePrimitive(root, PrimitiveType.Cylinder, "Whitened Fabric", new Vector3(2.4f, 0.75f, 1.2f), new Vector3(0.7f, 0.75f, 0.7f),
                LoadOrCreate("FluorWhite.mat", lit, material => SetupFluorescent(material, new Color(0.9f, 0.9f, 0.9f), new Color(0.55f, 0.7f, 1), 0.8f)));
            var sign = CreatePrimitive(root, PrimitiveType.Quad, "Fluorescent Unlit Sign", new Vector3(0, 1.3f, 2.4f), new Vector3(2.5f, 0.6f, 1),
                LoadOrCreate("FluorSignUnlit.mat", unlit, material => SetupFluorescent(material, new Color(0.03f, 0.03f, 0.03f), new Color(1, 0.1f, 0.6f), 2)));
            sign.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // 蛍光デカール：床の蛍光塗料の飛び散り、透明インクの星（通常光では見えない）、蛍光ペンの線（通常光でも黄色）。
            var splatter = AssetDatabase.LoadAssetAtPath<Texture2D>(SAMPLE_FOLDER + "/FluorDecal_Splatter.png");
            var star = AssetDatabase.LoadAssetAtPath<Texture2D>(SAMPLE_FOLDER + "/FluorDecal_Star.png");
            CreateDecal(root, "Fluorescent Decal (Paint Splatter)", new Vector3(-0.3f, 0, -1.3f), Quaternion.Euler(90, 20, 0), new Vector3(1.6f, 1.6f, 0.3f),
                splatter, new Color(0.9f, 0.15f, 1), 2, Color.white, 0);
            CreateDecal(root, "Fluorescent Decal (Invisible Ink)", new Vector3(-1.9f, 0, -0.6f), Quaternion.Euler(90, 0, 0), new Vector3(0.8f, 0.8f, 0.3f),
                star, new Color(0.2f, 0.9f, 1), 2, Color.white, 0);
            CreateDecal(root, "Fluorescent Decal (Highlighter)", new Vector3(1.6f, 0, -1.4f), Quaternion.Euler(90, -15, 0), new Vector3(1.6f, 0.25f, 0.3f),
                null, new Color(0.7f, 1, 0.1f), 1.5f, new Color(1, 0.95f, 0.35f), 0.6f);

            var lamp = new GameObject("Black Light (Spot)"); lamp.transform.SetParent(root.transform, false);
            lamp.transform.position = new Vector3(0, 4.5f, -1.5f); lamp.transform.rotation = Quaternion.Euler(70, 0, 0);
            var light = lamp.AddComponent<Light>(); light.type = LightType.Spot; light.range = 12; light.spotAngle = 70; light.innerSpotAngle = 45; light.shadows = LightShadows.Soft;
            var emitter = lamp.AddComponent<BlackLightEmitter>(); emitter.m_power = 40; emitter.m_wavelength = 400; emitter.Apply();
            lamp.AddComponent<LightShaftEmitter>();

            // このデモ専用のVolume：Bloom、霧（光柱）、ブラックライト全体設定。
            string profilePath = $"{DEMO_FOLDER}/BlackLightDemoProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath);
                var bloom = profile.Add<Bloom>(true); AssetDatabase.AddObjectToAsset(bloom, profile);
                bloom.threshold.Override(0.9f); bloom.intensity.Override(1f); bloom.scatter.Override(0.6f);
                var shaft = profile.Add<LightShaftVolume>(true); AssetDatabase.AddObjectToAsset(shaft, profile);
                shaft.m_enableFlag.Override(true); shaft.m_density.Override(0.03f); shaft.m_intensity.Override(1f); shaft.m_ambient.Override(0f);
                var black = profile.Add<BlackLightVolume>(true); AssetDatabase.AddObjectToAsset(black, profile);
                black.m_darkAdaptation.Override(0.6f); black.m_fluorescenceGain.Override(1f); black.m_fogFluorescence.Override(0.04f);
                EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            }
            var volumeObject = new GameObject("BlackLight Demo Volume"); volumeObject.transform.SetParent(root.transform, false);
            var volume = volumeObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = profile;

            // BloomにはカメラのPost Processingが必要。
            foreach (var camera in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(rootObject => rootObject.GetComponentsInChildren<Camera>()))
            {
                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                if (!data) data = Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
                Undo.RecordObject(data, "ブラックライト カメラ設定");
                data.renderPostProcessing = true; data.requiresDepthTexture = true; data.volumeLayerMask |= 1 << volumeObject.layer;
                EditorUtility.SetDirty(data);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); Undo.CollapseUndoOperations(group);
            return root;
        }

        /// <summary>デモ用の蛍光デカールを作成する。マスクがなければ長方形で付ける。</summary>
        static void CreateDecal(GameObject root, string name, Vector3 position, Quaternion rotation, Vector3 size, Texture2D mask, Color color, float efficiency, Color daylightTint, float daylightOpacity)
        {
            var decalObject = new GameObject(name); decalObject.transform.SetParent(root.transform, false);
            decalObject.transform.localPosition = position; decalObject.transform.localRotation = rotation;
            var decal = decalObject.AddComponent<FluorescentDecal>();
            decal.m_size = size;
            decal.m_mask = mask;
            decal.m_shape = mask ? FluorescentDecal.SHAPE.TEXTURE : FluorescentDecal.SHAPE.RECTANGLE;
            decal.m_fluorescentColor = color; decal.m_efficiency = efficiency;
            decal.m_daylightTint = daylightTint; decal.m_daylightOpacity = daylightOpacity;
        }

        /// <summary>蛍光シェーダーのマテリアルに基本色・蛍光色・変換効率を設定する。</summary>
        static void SetupFluorescent(Material material, Color baseColor, Color fluorescentColor, float efficiency)
        {
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_FluorColor", fluorescentColor);
            material.SetFloat("_FluorEfficiency", efficiency);
        }

        /// <summary>デモフォルダーのマテリアルを読み込む。なければ作成して設定し、保存する。</summary>
        static Material LoadOrCreate(string fileName, Shader shader, Action<Material> configure)
        {
            string path = $"{DEMO_FOLDER}/{fileName}";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(shader); configure(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject CreatePrimitive(GameObject root, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            var primitive = GameObject.CreatePrimitive(type); primitive.name = name;
            primitive.transform.SetParent(root.transform, false); primitive.transform.localPosition = position; primitive.transform.localScale = scale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            return primitive;
        }
    }
}
