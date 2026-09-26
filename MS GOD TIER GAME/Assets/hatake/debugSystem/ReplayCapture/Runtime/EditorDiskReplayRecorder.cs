#if UNITY_EDITOR_WIN
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Laboratory.ReplayCapture
{
    [AddComponentMenu("Replay Capture/Editor Disk Replay Recorder")]
    public sealed class EditorDiskReplayRecorder : MonoBehaviour
    {
        [Range(5,900)] public int seconds=900;
        [Range(1,60)] public int framesPerSecond=60;
        public int width=1920, height=1080;
        [Range(2,40)] public int bitrateMegabits=16;
        [Range(2,10)] public int segmentSeconds=5;
        [Range(64,8192)] public int cacheMegabytes=4096;
        public KeyCode saveKey=KeyCode.F9;
        public bool startOnEnable=true;
        public string outputDirectory="";
        private ReplayDiskSession session;
        private ReplayListenerTap tap;
        private RenderTexture screen, target;
        private ComputeShader converter;
        private ComputeBuffer nv12Buffer;
        private int kernel;
        public string CaptureFormat => nv12Buffer!=null ? "GPU NV12 (1.5 bytes/pixel)" : "BGRA (4 bytes/pixel)";
        private Coroutine capture;
        private readonly List<AsyncGPUReadbackRequest> readbacks=new List<AsyncGPUReadbackRequest>();
        private double origin;
        private double realOrigin, pauseStart, pauseDspStart;
        private long lastFrame=-1, skipped;
        private string error, lastPath;
        private int captureWidth,captureHeight,captureFps,sampleRate;
        public bool IsRecording => session!=null && session.Error==null;
        public bool IsExporting => session?.IsExporting ?? false;
        public string LastError => session?.Error ?? error;
        public string LastSavedPath => session?.LastPath ?? lastPath;
        public RenderTexture CaptureTexture => target;
        public long EncodedFrames => session?.EncodedFrames ?? 0;
        public long DuplicatedFrames => session?.DuplicatedFrames ?? 0;
        public long SkippedFrames => skipped+(session?.RejectedFrames ?? 0);
        public long AudioMissingFrames => session?.Audio.MissingFrames ?? 0;
        public long AudioCapturedFrames => session?.Audio.WrittenFrames ?? 0;
        public long AudioDroppedBlocks => session?.Audio.DroppedBlocks ?? 0;
        public long EvictedSegments => session?.EvictedSegments ?? 0;
        public long DiskBytes => session?.DiskBytes ?? 0;
        public double BufferedSeconds => session?.BufferedSeconds ?? 0;
        public double EncodeMilliseconds => session?.EncodeMilliseconds ?? 0;
        public string CacheDirectory => session?.CacheDirectory;
        private void OnEnable()
        {
            UnityEditor.EditorApplication.pauseStateChanged+=OnPauseChanged;
            if(startOnEnable && Application.isPlaying) StartRecording();
        }
        private void OnDisable()
        {
            UnityEditor.EditorApplication.pauseStateChanged-=OnPauseChanged;
            StopRecording();
        }
        private void OnPauseChanged(UnityEditor.PauseState state)
        {
            if(state==UnityEditor.PauseState.Paused)
            {
                pauseStart=Time.realtimeSinceStartupAsDouble; pauseDspStart=AudioSettings.dspTime;
                if(session!=null) session.Audio.Suspended=true;
            }
            else
            {
                realOrigin+=Time.realtimeSinceStartupAsDouble-pauseStart;
                if(session!=null) { session.Audio.AddPauseOffset(AudioSettings.dspTime-pauseDspStart); session.Audio.Suspended=false; }
            }
        }
        private void Update()
        {
            if(session!=null && AudioSettings.outputSampleRate!=sampleRate)
            { error="Audio device/sample rate changed; restart recording."; StopRecording(); }
            if(session!=null && tap==null)
            { error="AudioListener was removed; restart recording with the new scene's listener."; StopRecording(); }
            if(session!=null && ReplaySaveInput.WasPressed(saveKey)) SaveRecentVideo();
        }
        public void StartRecording()
        {
            if(session!=null && session.Error!=null) StopRecording();
            if(session!=null) return;
            try
            {
                if(!Application.isPlaying) throw new InvalidOperationException("Start recording in Editor Play mode");
                if(!SystemInfo.supportsAsyncGPUReadback) throw new NotSupportedException("AsyncGPUReadback is required");
                var listeners=FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
                AudioListener listener=null;
                foreach(var candidate in listeners) if(candidate.isActiveAndEnabled)
                { if(listener!=null) throw new InvalidOperationException("Use exactly one active AudioListener"); listener=candidate; }
                if(listener==null) throw new InvalidOperationException("An active AudioListener is required for Unity audio");
                // Destroy is deferred until the end of the frame. A stopped recorder's
                // tap can still be present here, or a saved/reloaded tap can have no ring.
                // Check every tap for an active recording before removing only orphans.
                var listenerTaps=listener.GetComponents<ReplayListenerTap>();
                foreach(var existingTap in listenerTaps)
                    if(existingTap.Ring!=null)
                        throw new InvalidOperationException("Another replay recorder is using AudioListener '"+listener.name+"'. Stop that recorder before starting this one.");
                foreach(var existingTap in listenerTaps) Destroy(existingTap);
                if(AudioSettings.speakerMode!=AudioSpeakerMode.Stereo && AudioSettings.speakerMode!=AudioSpeakerMode.Mono)
                    throw new NotSupportedException("This version supports Mono/Stereo Unity audio only");
                captureWidth=Mathf.Clamp(width,160,1920)&~3; captureHeight=Mathf.Clamp(height,90,1080)&~1;
                captureFps=Mathf.Clamp(framesPerSecond,1,60); sampleRate=AudioSettings.outputSampleRate;
                if(sampleRate!=48000 && sampleRate!=44100) throw new NotSupportedException("Use a 44100 or 48000 Hz audio output sample rate");
                string root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
                string destination=string.IsNullOrWhiteSpace(outputDirectory)?Path.Combine(root,"Recordings"):Path.GetFullPath(outputDirectory);
                origin=AudioSettings.dspTime; realOrigin=Time.realtimeSinceStartupAsDouble; error=null; skipped=0; lastFrame=-1;
                if(UnityEditor.EditorApplication.isPaused) { pauseStart=realOrigin; pauseDspStart=origin; }
                var computeAsset=SystemInfo.supportsComputeShaders?Resources.Load<ComputeShader>("ReplayNV12"):null;
                if(computeAsset!=null)
                {
                    converter=Instantiate(computeAsset); kernel=converter.FindKernel("Convert");
                    nv12Buffer=new ComputeBuffer(captureWidth*captureHeight*3/8,4);
                    converter.SetInt("Width",captureWidth); converter.SetInt("Height",captureHeight);
                    converter.SetInt("LinearColor",QualitySettings.activeColorSpace==ColorSpace.Linear?1:0);
                    converter.SetInt("FlipY", SystemInfo.graphicsUVStartsAtTop ? 0 : 1);
                    converter.SetBuffer(kernel,"Packed",nv12Buffer);
                }
                session=new ReplayDiskSession(captureWidth,captureHeight,captureFps,Mathf.Clamp(bitrateMegabits,2,40)*1000000,sampleRate,
                    Mathf.Clamp(seconds,5,900),Mathf.Clamp(segmentSeconds,2,10),Mathf.Clamp(cacheMegabytes,64,8192),
                    Path.Combine(root,"Library","ReplayCapture",Guid.NewGuid().ToString("N")),destination,origin,SystemInfo.graphicsUVStartsAtTop,nv12Buffer!=null);
                session.Audio.Suspended=UnityEditor.EditorApplication.isPaused;
                tap=listener.gameObject.AddComponent<ReplayListenerTap>(); tap.hideFlags=HideFlags.DontSave; tap.Ring=session.Audio;
                target=new RenderTexture(captureWidth,captureHeight,0,RenderTextureFormat.ARGB32); target.Create();
                capture=StartCoroutine(Capture());
            }
            catch(Exception ex) { error=ex.Message; StopRecording(); Debug.LogError("Replay: "+error,this); }
        }
        private IEnumerator Capture()
        {
            var end=new WaitForEndOfFrame();
            while(session!=null)
            {
                yield return end;
                var current=session;
                if(current==null || current.Error!=null) continue;
                // Rendering needs a smooth clock: dspTime can advance in audio-block steps.
                // Both clocks share the start epoch; Editor Pause is excluded from video time.
                long frame=(long)Math.Floor((Time.realtimeSinceStartupAsDouble-realOrigin)*captureFps);
                if(frame<=lastFrame) continue;
                readbacks.RemoveAll(r=>r.done);
                if(!current.Rent(out var pixels)) { skipped++; continue; }
                lastFrame=frame;
                try
                {
                    if(screen==null || screen.width!=Screen.width || screen.height!=Screen.height)
                    {
                        if(screen!=null) { screen.Release(); Destroy(screen); }
                        screen=new RenderTexture(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height),0,RenderTextureFormat.ARGB32); screen.Create();
                    }
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(screen); Graphics.Blit(screen,target);
                    Action<AsyncGPUReadbackRequest> callback=r=>
                    {
                        if(r.hasError || session!=current) { current.Return(pixels); skipped++; return; }
                        r.GetData<byte>().CopyTo(pixels); current.Submit(pixels,frame);
                    };
                    AsyncGPUReadbackRequest request;
                    if(nv12Buffer!=null)
                    {
                        converter.SetTexture(kernel,"Source",target);
                        converter.Dispatch(kernel,(captureWidth/4+7)/8,(captureHeight/2+7)/8,1);
                        request=AsyncGPUReadback.Request(nv12Buffer,callback);
                    }
                    else request=AsyncGPUReadback.Request(target,0,TextureFormat.BGRA32,callback);
                    readbacks.Add(request);
                }
                catch(Exception ex) { current.Return(pixels); error=ex.Message; skipped++; }
            }
        }
        public async void SaveRecentVideo() { await SaveRecentVideoAsync(); }
        public async Task<string> SaveRecentVideoAsync()
        {
            if(session==null) { error="Recording is not running"; return null; }
            try { lastPath=await session.Save(); error=null; Debug.Log("Replay MP4 saved: "+lastPath,this); return lastPath; }
            catch(Exception ex) { error=ex.Message; Debug.LogError("Replay export: "+error,this); return null; }
        }
        public void StopRecording()
        {
            if(capture!=null) { StopCoroutine(capture); capture=null; }
            var stopping=session; session=null;
            if(tap!=null) { tap.Ring=null; Destroy(tap); tap=null; }
            foreach(var r in readbacks) if(!r.done) r.WaitForCompletion();
            readbacks.Clear();
            if(stopping!=null) { stopping.Dispose(); lastPath=stopping.LastPath; if(stopping.Error!=null) error=stopping.Error; }
            if(target!=null) { target.Release(); Destroy(target); target=null; }
            if(screen!=null) { screen.Release(); Destroy(screen); screen=null; }
            if(nv12Buffer!=null) { nv12Buffer.Release(); nv12Buffer=null; }
            if(converter!=null) { Destroy(converter); converter=null; }
        }
    }
}
#endif
