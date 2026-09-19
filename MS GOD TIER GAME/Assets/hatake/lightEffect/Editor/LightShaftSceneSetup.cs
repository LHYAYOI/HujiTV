using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;


/// <summary>
/// 誰でも簡単LightShaftセットアップ！

namespace LightShaftLab.Editor
{
    public static class LightShaftSceneSetup
    {
        const string Profiles = "Assets/hatake/lightEffect/SceneProfiles";
        [MenuItem("Tools/LightShaft/LightShaftシーン初期セットアップ", false, 0)]
        public static void SetupMenu()
        {
            try
            {
                var volume = Setup();
                Selection.activeGameObject = volume.gameObject;
                EditorGUIUtility.PingObject(volume.gameObject);
                LightShaftScenePanel.Show(volume);
                EditorUtility.DisplayDialog("LightShaft 初期セットアップ完了",
                    "全体の空気感を設定するVolumeを作成しました。\n\nSpotライトに「LightShaftEmitter」を追加すると光柱が出ます。\n太陽のGod Rayは「Directional Light」で作れます。\n\n現在のURPのRenderer・深度・影設定も有効化しました（同じURPを使う他シーンにも共有されます）。既存の霧の調整値は維持しています。\n\n最後にシーンを保存することを忘れずに！Ctr + S。", "閉じる");
            }
            catch (Exception exception) { Debug.LogException(exception); EditorUtility.DisplayDialog("セットアップできませんでした", exception.Message, "閉じる"); }
        }

        [MenuItem("Tools/LightShaft/LightShaftシーン初期セットアップ", true)]
        static bool CanSetup() => !EditorApplication.isPlayingOrWillChangePlaymode && PrefabStageUtility.GetCurrentPrefabStage() == null;

        public static Volume Setup()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("通常のシーンを開き、Playを停止して実行してください。");
            var pipeline = (QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;


            if (!pipeline) throw new InvalidOperationException("URPの3Dプロジェクトが必要です。使用するURP AssetをGraphics/Quality Settingsに指定してください。");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/hatake/lightEffect/Shaders/LightShaft.shader");



            if (!shader) throw new InvalidOperationException("LightShaft.shaderが見つかりません。LightShaft一式をインポートしてください。");
            var pipelineObject = new SerializedObject(pipeline);
            var rendererList = pipelineObject.FindProperty("m_RendererDataList");
            var renderers = Enumerable.Range(0, rendererList.arraySize)
                .Select(i => rendererList.GetArrayElementAtIndex(i).objectReferenceValue as UniversalRendererData).Where(r => r).Distinct().ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("現在のURPに3D用のUniversal Rendererがありません。2D Rendererは対象外です。");



            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("LightShaft シーン初期セットアップ");
            foreach (var renderer in renderers) Install(renderer, shader);
            Undo.RecordObject(pipeline, "LightShaft URP設定");
            pipeline.supportsCameraDepthTexture = true;



            if (pipeline.shadowDistance <= 0) pipeline.shadowDistance = 35;
            pipelineObject.Update();
            pipelineObject.FindProperty("m_MainLightRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            pipelineObject.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            pipelineObject.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            pipelineObject.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            pipelineObject.ApplyModifiedProperties(); EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssetIfDirty(pipeline);

            var cameras = InScene<Camera>(scene).Where(c => c.isActiveAndEnabled).ToArray();


            if (cameras.Length == 0)
            {
                var camera = Undo.AddComponent<Camera>(Create("Main Camera")); camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0, 2, -10); cameras = new[] { camera };
            }


            foreach (var camera in cameras)
            {
                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                if (!data) data = Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
                Undo.RecordObject(data, "LightShaft カメラ設定"); data.requiresDepthTexture = true;
                
                data.volumeLayerMask |= 1; EditorUtility.SetDirty(data);
                PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            }

            var volume = InScene<Volume>(scene).FirstOrDefault(v => v.isGlobal && v.isActiveAndEnabled && v.sharedProfile && v.sharedProfile.Has<LightShaftVolume>());
            LightShaftVolume settings;


            if (!volume)
            {
                Directory.CreateDirectory(Profiles); AssetDatabase.Refresh();
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                string name = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
                foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                AssetDatabase.CreateAsset(profile, AssetDatabase.GenerateUniqueAssetPath($"{Profiles}/{name}_Atmosphere.asset"));
                settings = profile.Add<LightShaftVolume>(true); AssetDatabase.AddObjectToAsset(settings, profile);
                settings.m_enableFlag.Override(true); settings.m_enableSpatialGodRaysFlag.Override(true);
                settings.m_density.Override(.012f); settings.m_spatialGodRayIntensity.Override(.7f);
                ShaftQualityPresets.Apply(settings, ShaftQualityPreset.Balanced);
                volume = Undo.AddComponent<Volume>(Create("LightShaft シーン設定"));
                volume.isGlobal = true; volume.sharedProfile = profile;
                EditorUtility.SetDirty(settings); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            }
            else
            {
                volume.sharedProfile.TryGet(out settings);
                Undo.RecordObject(settings, "LightShaft 有効化"); settings.m_enableFlag.Override(true); settings.active = true;
                EditorUtility.SetDirty(settings); EditorUtility.SetDirty(volume.sharedProfile); AssetDatabase.SaveAssetIfDirty(volume.sharedProfile);
            }


            Undo.RecordObject(volume, "LightShaft 全体Volume"); volume.weight = 1; PrefabUtility.RecordPrefabInstancePropertyModifications(volume);
            foreach (var camera in cameras)
            {
                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                Undo.RecordObject(data, "LightShaft Volumeレイヤー"); data.volumeLayerMask |= 1 << volume.gameObject.layer;
                PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            }


            var sun = RenderSettings.sun;
            if (!sun || sun.type != LightType.Directional || !sun.isActiveAndEnabled)
                sun = InScene<Light>(scene).FirstOrDefault(l => l.type == LightType.Directional && l.isActiveAndEnabled);


            if (!sun)
            {
                sun = Undo.AddComponent<Light>(Create("LightShaft 太陽")); sun.type = LightType.Directional;
                sun.intensity = 1; sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            }


            Undo.RecordObject(sun, "LightShaft 太陽の影");


            if (sun.shadows == LightShadows.None) sun.shadows = LightShadows.Soft;


            PrefabUtility.RecordPrefabInstancePropertyModifications(sun); RenderSettings.sun = sun;
            EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(group);
            return volume;
        }

        static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();


        static GameObject Create(string name) { var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "LightShaft オブジェクト作成"); return go; }



        static void Install(UniversalRendererData renderer, Shader shader)
        {
            Undo.RecordObject(renderer, "LightShaft Renderer登録");
            var feature = renderer.rendererFeatures.OfType<LightShaftFeature>().FirstOrDefault();


            if (!feature)
            {
                feature = ScriptableObject.CreateInstance<LightShaftFeature>(); feature.name = "LightShaft / God Ray";
                AssetDatabase.AddObjectToAsset(feature, renderer); Undo.RegisterCreatedObjectUndo(feature, "LightShaft Feature作成");
                renderer.rendererFeatures.Add(feature);
            }
            Undo.RecordObject(feature, "LightShaft Feature設定"); feature.m_shader = shader; feature.SetActive(true); feature.Create();
            var serialized = new SerializedObject(renderer); var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;



            for (int i = 0; i < map.arraySize; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string guid, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            serialized.ApplyModifiedProperties(); renderer.SetDirty(); EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(feature);
            AssetDatabase.SaveAssetIfDirty(renderer);
        }



        [MenuItem("Tools/LightShaft/選択ライトに光柱を追加", false, 10)]
        public static void Attach()
        {
            foreach (var go in Selection.gameObjects)
                if (go.TryGetComponent<Light>(out var light) && (light.type == LightType.Spot || light.type == LightType.Directional) && !go.GetComponent<LightShaftEmitter>())
                    Undo.AddComponent<LightShaftEmitter>(go);
        }
        [MenuItem("Tools/LightShaft/選択ライトに光柱を追加", true)]
        static bool CanAttach() => Selection.gameObjects.Any(g => g.TryGetComponent<Light>(out var l) && (l.type == LightType.Spot || l.type == LightType.Directional));
        [MenuItem("Tools/LightShaft/God Ray領域を追加", false, 11)]
        public static void Highlight() => AtmosphereAuthoring.CreateHighlight();
    }

    [CustomEditor(typeof(LightShaftEmitter)), CanEditMultipleObjects]
    public sealed class LightShaftEmitterInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Spotの光柱は、初期セットアップ後にこのスクリプトを追加するだけで使えます。色・角度・照射距離はLight側で調整します。Point Lightは非対応です。", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_scattering"), new GUIContent("光柱の強さ"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_priority"), new GUIContent("表示の優先度", "1=通常。見せ場は3など。0はSpot候補から除外。"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_castShadowsFlag"), new GUIContent("影を光柱に反映", "Light本体のShadowsも有効にしてください。"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
