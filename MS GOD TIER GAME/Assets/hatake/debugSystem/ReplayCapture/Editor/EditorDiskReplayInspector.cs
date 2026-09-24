#if UNITY_EDITOR_WIN
using UnityEditor;
using UnityEngine;

namespace Laboratory.ReplayCapture.Editor
{
    [CustomEditor(typeof(EditorDiskReplayRecorder))]
    public sealed class EditorDiskReplayInspector : UnityEditor.Editor
    {
        [MenuItem("Tools/Replay Capture/Add Editor MP4 Recorder")]
        private static void Add()
        {
            var existing=Object.FindFirstObjectByType<EditorDiskReplayRecorder>();
            if(existing!=null) { Selection.activeGameObject=existing.gameObject; return; }
            var obj=new GameObject("Editor MP4 Replay");
            Undo.RegisterCreatedObjectUndo(obj,"Add Editor MP4 Replay");
            Undo.AddComponent<EditorDiskReplayRecorder>(obj);
            Selection.activeGameObject=obj;
        }
        public override void OnInspectorGUI()
        {
            var recorder=(EditorDiskReplayRecorder)target;
            EditorGUILayout.HelpBox("Windows Editor / H.264 + Unity音声(AAC)。旧ReplayRecorderは無効にしてください。録画設定の変更は停止後に行ってください。",MessageType.Info);
            using(new EditorGUI.DisabledScope(recorder.IsRecording)) DrawDefaultInspector();
            if(Application.isPlaying)
            {
                if(!recorder.IsRecording && GUILayout.Button("Start recording")) recorder.StartRecording();
                if(recorder.IsRecording && GUILayout.Button("Stop recording")) recorder.StopRecording();
                using(new EditorGUI.DisabledScope(!recorder.IsRecording || recorder.IsExporting))
                    if(GUILayout.Button("Save recent MP4 (F9)")) recorder.SaveRecentVideo();
            }
            if(!string.IsNullOrEmpty(recorder.LastError)) EditorGUILayout.HelpBox(recorder.LastError,MessageType.Error);
        }
    }
}
#endif
