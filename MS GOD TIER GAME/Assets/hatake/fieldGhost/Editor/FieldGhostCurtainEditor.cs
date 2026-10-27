using UnityEditor;
using UnityEngine;

namespace FieldGhost.EditorTools
{
    /// <summary>カーテンのインスペクタに「箱に貼り付ける」ボタンを足す。</summary>
    [CustomEditor(typeof(FieldGhostCurtain)), CanEditMultipleObjects]
    sealed class FieldGhostCurtainEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            bool anyTarget = false;
            foreach (Object item in targets)
            {
                if (((FieldGhostCurtain)item).wrapTarget)
                {
                    anyTarget = true;
                }
            }
            using (new EditorGUI.DisabledScope(!anyTarget))
            {
                if (GUILayout.Button("箱に貼り付ける（位置・向き・大きさ・折り目を合わせる）", GUILayout.Height(28)))
                {
                    foreach (Object item in targets)
                    {
                        var curtain = (FieldGhostCurtain)item;
                        if (!curtain.wrapTarget)
                        {
                            continue;
                        }
                        Undo.RecordObjects(new Object[] { curtain, curtain.transform }, "カーテンを箱に貼り付ける");
                        if (!curtain.FitToBox())
                        {
                            EditorUtility.DisplayDialog("箱に貼り付ける",
                                $"{curtain.wrapTarget.name} の大きさが分かりません。BoxCollider を付けるか、メッシュのあるオブジェクトを指定してください。", "OK");
                            continue;
                        }
                        EditorUtility.SetDirty(curtain);
                    }
                }
            }
            if (!anyTarget)
            {
                EditorGUILayout.HelpBox("「貼り付ける箱（wrapTarget）」に箱を入れると、ボタンで箱の面に合わせられます。", MessageType.Info);
            }
        }
    }
}
