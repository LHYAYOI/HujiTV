using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Laboratory.ReplayCapture.Editor
{
    public sealed class ReplayDiagnosticsWindow : EditorWindow
    {
        private ReplayRecorder recorder;
        private RenderTexture selectedTexture;
        private RenderTexture previewTexture;
#if UNITY_EDITOR_WIN
        private EditorDiskReplayRecorder diskRecorder;
#endif

        private void OnDisable() => ReleasePreview();

        private void ReleasePreview()
        {
            if (previewTexture == null) return;
            previewTexture.Release();
            DestroyImmediate(previewTexture);
            previewTexture = null;
        }

        private RenderTexture GetPreviewTexture()
        {
            // Screenshot RT orientation follows the graphics backend. Arbitrary resources
            // retain their original orientation; only the recorder's screenshot is corrected.
            bool screenshot = recorder != null && selectedTexture == recorder.CaptureTexture;
#if UNITY_EDITOR_WIN
            screenshot |= diskRecorder != null && selectedTexture == diskRecorder.CaptureTexture;
#endif
            if (!screenshot || !SystemInfo.graphicsUVStartsAtTop)
                return selectedTexture;
            if (previewTexture == null || previewTexture.width != selectedTexture.width || previewTexture.height != selectedTexture.height)
            {
                ReleasePreview();
                previewTexture = new RenderTexture(selectedTexture.width, selectedTexture.height, 0, selectedTexture.format);
                previewTexture.hideFlags = HideFlags.HideAndDontSave;
                previewTexture.Create();
            }
            if (Event.current == null || Event.current.type == EventType.Repaint || Event.current.type == EventType.MouseUp)
                Graphics.Blit(selectedTexture, previewTexture, new Vector2(1, -1), new Vector2(0, 1));
            return previewTexture;
        }

        [MenuItem("Tools/Replay Capture/Diagnostics")]
        private static void Open() => GetWindow<ReplayDiagnosticsWindow>("Replay Diagnostics");

        private void OnGUI()
        {
#if UNITY_EDITOR_WIN
            diskRecorder = (EditorDiskReplayRecorder)EditorGUILayout.ObjectField("Disk recorder (MP4)", diskRecorder, typeof(EditorDiskReplayRecorder), true);
            if (diskRecorder == null && Application.isPlaying) diskRecorder = FindFirstObjectByType<EditorDiskReplayRecorder>();
            if (diskRecorder != null)
            {
                EditorGUILayout.LabelField("Backend", "Windows Media Foundation / H.264 + AAC");
                EditorGUILayout.LabelField("Capture format",diskRecorder.CaptureFormat);
                EditorGUILayout.LabelField("Encoder acceleration", "Hardware allowed; active encoder not yet identified");
                EditorGUILayout.LabelField("Buffered seconds", diskRecorder.BufferedSeconds.ToString("F2"));
                EditorGUILayout.LabelField("Closed cache", (diskRecorder.DiskBytes/1048576.0).ToString("F2")+" MiB");
                EditorGUILayout.LabelField("Encoded / duplicated", diskRecorder.EncodedFrames+" / "+diskRecorder.DuplicatedFrames);
                EditorGUILayout.LabelField("Skipped captures", diskRecorder.SkippedFrames.ToString());
                EditorGUILayout.LabelField("Encoder worker / last frame", diskRecorder.EncodeMilliseconds.ToString("F2")+" ms");
                EditorGUILayout.LabelField("Audio captured / missing frames", diskRecorder.AudioCapturedFrames+" / "+diskRecorder.AudioMissingFrames);
                EditorGUILayout.LabelField("Audio dropped blocks", diskRecorder.AudioDroppedBlocks.ToString());
                EditorGUILayout.LabelField("Evicted segments", diskRecorder.EvictedSegments.ToString());
                EditorGUILayout.LabelField("Cache folder", diskRecorder.CacheDirectory ?? "—");
                EditorGUILayout.LabelField("Last MP4", diskRecorder.LastSavedPath ?? "—");
                if(!string.IsNullOrEmpty(diskRecorder.LastError)) EditorGUILayout.HelpBox(diskRecorder.LastError,MessageType.Error);
                using(new EditorGUI.DisabledScope(!diskRecorder.IsRecording || diskRecorder.IsExporting))
                    if(GUILayout.Button("Save recent MP4")) diskRecorder.SaveRecentVideo();
                using(new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if(!diskRecorder.IsRecording && GUILayout.Button("Start MP4 recording")) diskRecorder.StartRecording();
                    if(diskRecorder.IsRecording && GUILayout.Button("Stop MP4 recording")) diskRecorder.StopRecording();
                }
                if(selectedTexture==null) selectedTexture=diskRecorder.CaptureTexture;
            }
            EditorGUILayout.Space();
#endif
            recorder = (ReplayRecorder)EditorGUILayout.ObjectField("Recorder", recorder, typeof(ReplayRecorder), true);
            if (recorder == null && Application.isPlaying) recorder = FindFirstObjectByType<ReplayRecorder>();
            if (recorder != null)
            {
                EditorGUILayout.LabelField("Frames buffered", recorder.BufferedFrames.ToString());
                EditorGUILayout.LabelField("Compressed memory", (recorder.BufferedBytes / 1048576f).ToString("F2") + " MiB");
                EditorGUILayout.LabelField("Captured / skipped", recorder.CapturedFrames + " / " + recorder.SkippedFrames);
                EditorGUILayout.LabelField("Resolution / FPS", recorder.Width + " × " + recorder.Height + " / " + recorder.FramesPerSecond);
                EditorGUILayout.LabelField("Source screen", recorder.SourceWidth + " × " + recorder.SourceHeight);
                EditorGUILayout.LabelField("Exporting", recorder.IsExporting.ToString());
                EditorGUILayout.LabelField("Last file", recorder.LastSavedPath ?? "—");
                if (!string.IsNullOrEmpty(recorder.LastError)) EditorGUILayout.HelpBox(recorder.LastError, MessageType.Warning);
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    if (GUILayout.Button("Save recent video")) recorder.SaveRecentVideo();
                if (selectedTexture == null && recorder.CaptureTexture != null) selectedTexture = recorder.CaptureTexture;
            }
            EditorGUILayout.Space();
            selectedTexture = (RenderTexture)EditorGUILayout.ObjectField("Render resource", selectedTexture, typeof(RenderTexture), false);
            if (selectedTexture == null) return;
            EditorGUILayout.LabelField("Size", selectedTexture.width + " × " + selectedTexture.height);
            EditorGUILayout.LabelField("Format", selectedTexture.graphicsFormat.ToString());
            EditorGUILayout.LabelField("Created", selectedTexture.IsCreated().ToString());
            Rect rect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true));
            var displayTexture = GetPreviewTexture();
            EditorGUI.DrawPreviewTexture(rect, displayTexture, null, ScaleMode.ScaleToFit);
            if (GUILayout.Button("Save this render texture as PNG")) SaveTexture(displayTexture);
            if (Application.isPlaying) Repaint();
        }

        private static void SaveTexture(RenderTexture source)
        {
            string path = EditorUtility.SaveFilePanel("Save render resource", Application.persistentDataPath, "replay_frame.png", "png");
            if (string.IsNullOrEmpty(path)) return;
            RenderTexture previous = RenderTexture.active;
            Texture2D texture = null;
            try
            {
                RenderTexture.active = source;
                texture = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                texture.Apply(false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log("Render resource saved: " + path);
            }
            catch (Exception ex) { Debug.LogException(ex); }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) DestroyImmediate(texture);
            }
        }
    }
}
