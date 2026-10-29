using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LightShaftLab.Editor
{
    /// <summary>距離フォグの導入メニュー。Renderer への登録と、シーンの Volume への追加。</summary>
    public static class DistanceFogSetup
    {
        [MenuItem("Tools/LightShaft/距離フォグ/現在のRendererに登録", false, 50)]
        public static void InstallMenu()
        {
            try
            {
                var pipeline = (QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
                if (!pipeline)
                {
                    throw new InvalidOperationException("URP Asset が見つかりません。");
                }
                var pipelineObject = new SerializedObject(pipeline);
                var rendererList = pipelineObject.FindProperty("m_RendererDataList");
                var renderers = Enumerable.Range(0, rendererList.arraySize)
                    .Select(i => rendererList.GetArrayElementAtIndex(i).objectReferenceValue as UniversalRendererData)
                    .Where(rendererData => rendererData).Distinct().ToArray();
                if (renderers.Length == 0)
                {
                    throw new InvalidOperationException("現在の URP に Universal Renderer がありません。");
                }
                foreach (var renderer in renderers)
                {
                    Install(renderer);
                }
                EditorUtility.DisplayDialog("距離フォグ", $"現在の URP が使う Renderer {renderers.Length} 個に DistanceFogFeature を登録しました（登録済みのものはそのまま）。", "閉じる");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("登録できませんでした", exception.Message, "閉じる");
            }
        }

        static void Install(UniversalRendererData renderer)
        {
            var feature = renderer.rendererFeatures.OfType<DistanceFogFeature>().FirstOrDefault();
            if (!feature)
            {
                Undo.RecordObject(renderer, "距離フォグ Renderer 登録");
                feature = ScriptableObject.CreateInstance<DistanceFogFeature>();
                feature.name = "Distance Fog";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                Undo.RegisterCreatedObjectUndo(feature, "DistanceFogFeature 作成");
                renderer.rendererFeatures.Add(feature);
            }
            if (!feature.m_shader)
            {
                Undo.RecordObject(feature, "距離フォグのシェーダー設定");
                feature.m_shader = Shader.Find(DistanceFogFeature.SHADER_NAME);
            }
            feature.SetActive(true);
            feature.Create();
            // Renderer が機能を見つけるための対応表を更新する
            var serialized = new SerializedObject(renderer);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < map.arraySize; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string guid, out long localId);
                map.GetArrayElementAtIndex(i).longValue = localId;
            }
            serialized.ApplyModifiedProperties();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(feature);
            AssetDatabase.SaveAssetIfDirty(renderer);
        }

        [MenuItem("Tools/LightShaft/距離フォグ/シーンのVolumeに追加", false, 51)]
        public static void AddToSceneVolume()
        {
            // LightShaft の設定が入った Global Volume を優先し、なければ最初の Global Volume
            var volumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).Where(volume => volume.isGlobal && volume.sharedProfile).ToArray();
            var target = volumes.FirstOrDefault(volume => volume.sharedProfile.Has<LightShaftVolume>()) ?? volumes.FirstOrDefault();
            if (target == null)
            {
                EditorUtility.DisplayDialog("距離フォグ", "シーンに Profile 付きの Global Volume がありません。", "閉じる");
                return;
            }
            var profile = target.sharedProfile;
            if (profile.Has<DistanceFogVolume>())
            {
                Selection.activeObject = target.gameObject;
                EditorUtility.DisplayDialog("距離フォグ", $"{target.name} の Profile にはすでに距離フォグがあります。", "閉じる");
                return;
            }
            Undo.RecordObject(profile, "距離フォグを Volume に追加");
            var fog = profile.Add<DistanceFogVolume>(true);
            fog.m_enableFlag.value = true;
            fog.name = nameof(DistanceFogVolume);
            fog.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(fog, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            Selection.activeObject = target.gameObject;
            EditorUtility.DisplayDialog("距離フォグ", $"{target.name} の Profile（{profile.name}）に距離フォグを追加しました。", "閉じる");
        }
    }
}
