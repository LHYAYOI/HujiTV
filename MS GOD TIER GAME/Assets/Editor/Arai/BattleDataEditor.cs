using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(BattleData))]
public class BattleDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty list = serializedObject.FindProperty("m_enemyData");

        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty data = list.GetArrayElementAtIndex(i);

            SerializedProperty spawnType = data.FindPropertyRelative("m_spawnType");
            SerializedProperty spawnTime = data.FindPropertyRelative("m_spawnTime");
            SerializedProperty enemyId = data.FindPropertyRelative("m_enemyId");
            SerializedProperty splineId = data.FindPropertyRelative("m_splineId");
            SerializedProperty spawnId = data.FindPropertyRelative("m_spawnId");
            SerializedProperty destroyType = data.FindPropertyRelative("m_destroyType");
            SerializedProperty destroyTime = data.FindPropertyRelative("m_destroyTime");

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Enemy " + i, EditorStyles.boldLabel);

            // ---- スポーン ----
            EditorGUILayout.PropertyField(spawnType);

            switch ((BattleEnemyData.SPAWN_TYPE)spawnType.intValue)
            {
                case BattleEnemyData.SPAWN_TYPE.SPLINE:
                    EditorGUILayout.PropertyField(enemyId);
                    EditorGUILayout.PropertyField(spawnId);
                    EditorGUILayout.PropertyField(splineId);
                    break;

                case BattleEnemyData.SPAWN_TYPE.POPUP:
                    EditorGUILayout.PropertyField(enemyId);
                    EditorGUILayout.PropertyField(spawnId);
                    break;
            }

            // ---- 削除ボタン ----
            bool remove = GUILayout.Button("この敵を削除");

            EditorGUILayout.EndVertical();

            if (remove)
            {
                list.DeleteArrayElementAtIndex(i);
                break;
            }
        }

        if (GUILayout.Button("敵を追加"))
        {
            list.arraySize++;
        }

        serializedObject.ApplyModifiedProperties();
    }
}