using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;


/// <summary>
/// シーンの全体の空気感を調整することができるエディタウィンドウ
namespace LightShaftLab.Editor
{
    public sealed class LightShaftScenePanel : EditorWindow
    {
        Volume m_volume;
        [MenuItem("Tools/LightShaft/全体の空気感を調整", false, 1)]
        public static void Open() => Show(null);
        public static void Show(Volume volume)
        {
            var window = GetWindow<LightShaftScenePanel>("LightShaft 空気感");
            window.minSize = new Vector2(380, 350);
            window.m_volume = volume ? volume : SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Volume>()).FirstOrDefault(v => v.isGlobal && v.sharedProfile && v.sharedProfile.Has<LightShaftVolume>());
        }
        void OnGUI()
        {
            m_volume = (Volume)EditorGUILayout.ObjectField("シーン設定", m_volume, typeof(Volume), true);
            if (!m_volume || !m_volume.sharedProfile || !m_volume.sharedProfile.TryGet<LightShaftVolume>(out var settings))
            {
                EditorGUILayout.HelpBox("Tools → LightShaft → LightShaftシーン初期セットアップを実行してください。", MessageType.Info); return;
            }
            EditorGUILayout.HelpBox("全体はここで調整し、Spotの色・向き・照射距離はLight側で調整します。Profileを共有するシーンにも変更が反映されます。", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                EditorGUI.BeginChangeCheck();
                bool enabled = EditorGUILayout.Toggle("演出を有効にする", settings.m_enableFlag.value);
                float density = EditorGUILayout.Slider("霧の濃さ", settings.m_density.value, 0, .3f);
                float intensity = EditorGUILayout.Slider("光の散乱の強さ", settings.m_intensity.value, 0, 10);
                Color tint = EditorGUILayout.ColorField("霧の色", settings.m_scatteringTint.value);
                bool sun = EditorGUILayout.Toggle("太陽のGod Ray", settings.m_enableSpatialGodRaysFlag.value);
                float sunStrength = EditorGUILayout.Slider("太陽の光の強さ", settings.m_spatialGodRayIntensity.value, 0, 10);
                float distance = EditorGUILayout.Slider("演出を描く距離", settings.m_maxDistance.value, 1, 100);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(settings, "LightShaft 空気感を調整");
                    settings.m_enableFlag.Override(enabled); settings.m_density.Override(density); settings.m_intensity.Override(intensity);
                    settings.m_scatteringTint.Override(tint); settings.m_enableSpatialGodRaysFlag.Override(sun);
                    settings.m_spatialGodRayIntensity.Override(sunStrength); settings.m_maxDistance.Override(distance);
                    EditorUtility.SetDirty(settings); EditorUtility.SetDirty(m_volume.sharedProfile); SceneView.RepaintAll();
                }

                EditorGUILayout.Space(); EditorGUILayout.LabelField("品質", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    Preset("軽量", ShaftQualityPreset.Performance, settings);
                    Preset("標準", ShaftQualityPreset.Balanced, settings);
                    Preset("高品質", ShaftQualityPreset.NearDetail, settings);
                }
                if (GUILayout.Button("調整したProfileを保存")) AssetDatabase.SaveAssetIfDirty(m_volume.sharedProfile);
            }


            if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("この画面は停止中に調整します。Play中はデモUIを使って比較してください。", MessageType.Info);
            EditorGUILayout.HelpBox("最初は霧の濃さ0.012・標準品質がおすすめです。見えない場合はライトの向き・明るさ・照射距離と、カメラの位置を確認してください。", MessageType.None);
        }
        void Preset(string label, ShaftQualityPreset preset, LightShaftVolume settings)
        {
            if (!GUILayout.Button(label)) return;
            Undo.RecordObject(settings, "LightShaft 品質を調整"); ShaftQualityPresets.Apply(settings, preset);
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(m_volume.sharedProfile); SceneView.RepaintAll();
        }
    }
}
