using UnityEditor;
using UnityEngine;

namespace TeamToon.Editor
{
    [CustomEditor(typeof(TeamToonMaterialBlend)), CanEditMultipleObjects]
    public sealed class TeamToonMaterialBlendEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("モデルの親に1つ追加します。Blend: 0 = A / 1 = B。再生中に色・透明度・発光を連続ブレンドします。A/Bと一致するマテリアルのスロットのみ対象です。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("materialA"), new GUIContent("A: 通常マテリアル"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("materialB"), new GUIContent("B: Toon / ゴースト"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("renderers"), new GUIContent("対象（空なら子を自動取得）"), true);
            bool rebuild = EditorGUI.EndChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("blend"), new GUIContent("Blend（モデル全体）"));
            serializedObject.ApplyModifiedProperties();
            if (rebuild && Application.isPlaying)
                foreach (var item in targets) ((TeamToonMaterialBlend)item).Rebuild();
            foreach (var item in targets)
            {
                var component = (TeamToonMaterialBlend)item;
                if (!component.materialA || !TeamToonMaterial.IsSupported(component.materialB))
                    EditorGUILayout.HelpBox("Aを指定し、BにはToonMasterマテリアルを指定してください。", MessageType.Warning);
            }
            if (Application.isPlaying && GUILayout.Button("対象・マテリアル設定を再読み込み"))
                foreach (var item in targets) ((TeamToonMaterialBlend)item).Rebuild();
        }
    }
}
