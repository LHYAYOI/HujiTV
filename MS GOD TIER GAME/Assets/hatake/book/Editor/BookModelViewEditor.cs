
using UnityEditor;
using UnityEngine;
        

[CustomEditor(typeof(BookModelView))]
public sealed class BookModelViewEditor : Editor
{
    private bool m_preview;
    private float m_progress = 1.0f;
    private BookModelView BookModelView => (BookModelView)target;

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Undo.undoRedoPerformed += RefreshPreview;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        Undo.undoRedoPerformed -= RefreshPreview;
        StopPreview();
    }

    public override void OnInspectorGUI()
    {
        bool changed = DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "ジョイント調整プレビュー", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(
            EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (!m_preview)
            {
                BookModelView view = (BookModelView)target;

                using (new EditorGUI.DisabledScope(
                    !view.CanPreviewPose || AnimationMode.InAnimationMode()))
                {
                    if (GUILayout.Button("姿勢プレビュー開始"))
                    {
                        AnimationMode.StartAnimationMode();
                        m_preview = true;
                        RefreshPreview();
                    }
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                m_progress = EditorGUILayout.Slider(
                    "クリップ位置", m_progress, 0f, 1f);

                if (EditorGUI.EndChangeCheck() || changed)
                    RefreshPreview();

                if (GUILayout.Button("終了して元の姿勢に戻す"))
                    StopPreview();
            }
        }

        EditorGUILayout.HelpBox(
            "0 = クリップ先頭、1 = クリップ末尾。\n" +
            "Joint Offsetsにジョイントを登録し、Start / End Offsetを調整します。\n" +
            "逆再生の着地位置は0です。確認はSceneビューで行ってください。",
            MessageType.Info);
    }

    private void RefreshPreview()
    {
        if (!m_preview || target == null ||
            !AnimationMode.InAnimationMode())
            return;

        BookModelView view = (BookModelView)target;


        view.SampleEditorPose(m_progress);

        SceneView.RepaintAll();
        Repaint();
    }

    private void StopPreview()
    {
        if (!m_preview)
            return;

        if (target != null)
        {
            BookModelView view = (BookModelView)target;
            view.ClearEditorPoseOffsets();
        }

        AnimationMode.StopAnimationMode();
        m_preview = false;
        SceneView.RepaintAll();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            StopPreview();
    }
}