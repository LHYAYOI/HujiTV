using UnityEditor;
using UnityEngine;

/// <summary>LightMood のインスペクター。ライトの取り込みと切り替えのボタンを上に出す。</summary>
[CustomEditor(typeof(LightMood)), CanEditMultipleObjects]
public sealed class LightMoodEditor : Editor
{
    public override void OnInspectorGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("エリア内のライトを全部取り込む"))
            {
                foreach (Object item in targets)
                {
                    ((LightMood)item).CollectLightsInArea();
                }
            }
            if (GUILayout.Button("今の値を取り込む"))
            {
                foreach (Object item in targets)
                {
                    ((LightMood)item).CaptureCurrentValues();
                }
            }
        }
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("このムードにする（Play 中）"))
            {
                foreach (Object item in targets)
                {
                    ((LightMood)item).Apply();
                }
            }
        }
        EditorGUILayout.HelpBox("ライトと太陽を好みの状態にしてから「取り込む」を押すと、このエリアの雰囲気として記録されます。太陽（Directional Light）はゴッドレイの光源なので、どのムードでも消さずに色・明るさ・向きを切り替えます。", MessageType.None);
        DrawDefaultInspector();
    }
}
