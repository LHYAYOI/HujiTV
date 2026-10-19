using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace LightShaftLab.Editor
{
    /// <summary>蛍光デカールのインスペクター。シーン上で箱の大きさをハンドルで変更できる。</summary>
    [CustomEditor(typeof(FluorescentDecal)), CanEditMultipleObjects]
    public sealed class FluorescentDecalEditor : UnityEditor.Editor
    {
        readonly BoxBoundsHandle m_handle = new BoxBoundsHandle();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var shape = serializedObject.FindProperty("m_shape");
            var iterator = serializedObject.GetIterator();
            for (bool enterChildrenFlag = true; iterator.NextVisible(enterChildrenFlag); enterChildrenFlag = false)
            {
                if (iterator.name == "m_Script") continue;
                bool useTextureFlag = shape.hasMultipleDifferentValues || shape.enumValueIndex == (int)FluorescentDecal.SHAPE.TEXTURE;
                if (!useTextureFlag && (iterator.name == "m_mask" || iterator.name == "m_maskChannel" || iterator.name == "m_tiling" || iterator.name == "m_offset")) continue;
                if (useTextureFlag && iterator.name == "m_edgeSoftness") continue;
                EditorGUILayout.PropertyField(iterator, true);
            }
            serializedObject.ApplyModifiedProperties();

            var decal = (FluorescentDecal)target;
            if (decal.m_shape == FluorescentDecal.SHAPE.TEXTURE && !decal.m_mask)
                EditorGUILayout.HelpBox("テクスチャが未設定のため、箱全体（長方形）に付きます。", MessageType.Info);
            EditorGUILayout.HelpBox("不透明な面（深度に書き込む面）にだけ付きます。ブラックライト（BlackLightEmitter）の紫外線を受けたときに光り、通常の光では「通常光での見た目」だけが見えます。", MessageType.None);
        }

        void OnSceneGUI()
        {
            var decal = (FluorescentDecal)target;
            using (new Handles.DrawingScope(new Color(0.65f, 0.4f, 1, 1), decal.transform.localToWorldMatrix))
            {
                m_handle.center = Vector3.zero;
                m_handle.size = decal.m_size;
                EditorGUI.BeginChangeCheck();
                m_handle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(new Object[] { decal, decal.transform }, "蛍光デカールの大きさ");
                    // 片側だけ動かしたときは、反対側の面が動かないよう中心も移動する。
                    decal.transform.position = decal.transform.TransformPoint(m_handle.center);
                    decal.m_size = Vector3.Max(m_handle.size, new Vector3(0.001f, 0.001f, 0.001f));
                    EditorUtility.SetDirty(decal);
                }
            }
        }

        [MenuItem("GameObject/LightShaft/蛍光デカール", false, 10)]
        [MenuItem("Tools/LightShaft/ブラックライト/蛍光デカールを作成", false, 43)]
        static void Create(MenuCommand command)
        {
            var decalObject = new GameObject("Fluorescent Decal");
            var parent = command.context as GameObject;
            if (parent) GameObjectUtility.SetParentAndAlign(decalObject, parent);
            else Place(decalObject.transform);
            decalObject.AddComponent<FluorescentDecal>();
            Undo.RegisterCreatedObjectUndo(decalObject, "蛍光デカール作成");
            Selection.activeGameObject = decalObject;
        }

        /// <summary>シーンビューの中央に見えている面へ、面に向けて配置する。</summary>
        static void Place(Transform transform)
        {
            var view = SceneView.lastActiveSceneView;
            if (!view || !view.camera) return;
            var ray = view.camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            if (Physics.Raycast(ray, out var hit, 1000))
            {
                transform.position = hit.point;
                transform.rotation = Quaternion.LookRotation(-hit.normal, Mathf.Abs(hit.normal.y) > 0.9f ? view.camera.transform.up : Vector3.up);
            }
            else
            {
                transform.position = view.pivot;
                transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            }
        }
    }
}
