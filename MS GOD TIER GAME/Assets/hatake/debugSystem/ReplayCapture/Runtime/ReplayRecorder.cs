using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Laboratory.ReplayCapture
{
    /// <summary>Bounded, video-only instant replay. Add to one scene object and invoke SaveRecentVideo().</summary>
    public sealed class ReplayRecorder : MonoBehaviour
    {
        [SerializeField, Range(1, 120)] private int seconds = 15;
        [SerializeField, Range(1, 30)] private int framesPerSecond = 10;
        [SerializeField, Range(160, 1920)] private int width = 640;
        [SerializeField, Range(90, 1080)] private int height = 360;
        [SerializeField, Range(1, 100)] private int jpegQuality = 65;
        [SerializeField, Range(4, 256)] private int maxCompressedMegabytes = 32;
        [SerializeField] private bool startOnEnable = true;
        [SerializeField] private bool persistAcrossScenes = true;
        [SerializeField] private KeyCode saveKey = KeyCode.F9;
        [SerializeField] private string outputDirectory = "";

        private struct Frame { public double Time; public byte[] Jpeg; }
        private readonly Queue<Frame> frames = new Queue<Frame>();
        private readonly object gate = new object();
        private RenderTexture target;
        private RenderTexture fullScreen;
        private readonly HashSet<RenderTexture> deferredDestroy = new HashSet<RenderTexture>();
        private Coroutine captureLoop;
        private volatile int generation;
        private volatile bool busy;
        private bool readbackPending;
        private bool exporting;
        private long bytes;
        private long captured;
        private long skipped;
        private string lastError;
        private string lastPath;

        public int BufferedFrames { get { lock (gate) return frames.Count; } }
        public long BufferedBytes { get { lock (gate) return bytes; } }
        public double BufferedSpanSeconds
        {
            get
            {
                lock (gate)
                {
                    if (frames.Count < 2) return 0;
                    double last = 0;
                    foreach (var frame in frames) last = frame.Time;
                    return last - frames.Peek().Time;
                }
            }
        }
        public long CapturedFrames { get { lock (gate) return captured; } }
        public long SkippedFrames { get { lock (gate) return skipped; } }
        public string LastError { get { lock (gate) return lastError; } }
        public string LastSavedPath { get { lock (gate) return lastPath; } }
        public bool IsExporting { get { lock (gate) return exporting; } }
        public int Width => width;
        public int Height => height;
        public int FramesPerSecond => framesPerSecond;
        public int SourceWidth => fullScreen != null ? fullScreen.width : Screen.width;
        public int SourceHeight => fullScreen != null ? fullScreen.height : Screen.height;
        public RenderTexture CaptureTexture => target;
        public string OutputDirectory { get => outputDirectory; set => outputDirectory = value; }

        /// <summary>Starts a fresh recording window with bounded settings. Call between exports.</summary>
        public void Configure(int durationSeconds, int fps, int captureWidth, int captureHeight, int quality, int memoryMegabytes)
        {
            if (IsExporting) throw new InvalidOperationException("Cannot reconfigure during export.");
            StopRecording();
            seconds = Mathf.Clamp(durationSeconds, 1, 120);
            framesPerSecond = Mathf.Clamp(fps, 1, 30);
            width = Mathf.Clamp(captureWidth, 160, 1920);
            height = Mathf.Clamp(captureHeight, 90, 1080);
            jpegQuality = Mathf.Clamp(quality, 1, 100);
            maxCompressedMegabytes = Mathf.Clamp(memoryMegabytes, 4, 256);
            lock (gate)
            {
                frames.Clear();
                bytes = captured = skipped = 0;
                lastPath = lastError = null;
            }
            if (isActiveAndEnabled) StartRecording();
        }

        private void OnEnable()
        {
            if (persistAcrossScenes) DontDestroyOnLoad(gameObject);
            if (startOnEnable) StartRecording();
        }

        private void OnDisable() => StopRecording();
        private void OnDestroy() => StopRecording();

        private void Update()
        {
            if (ReplaySaveInput.WasPressed(saveKey)) SaveRecentVideo();
        }

        public void StartRecording()
        {
            if (captureLoop != null) return;
            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                lock (gate) lastError = "Async GPU Readback is unsupported on this device.";
                return;
            }
            width = Mathf.Max(2, width & ~1);
            height = Mathf.Max(2, height & ~1);
            target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            target.Create();
            captureLoop = StartCoroutine(CaptureLoop());
        }

        public void StopRecording()
        {
            generation++;
            if (captureLoop != null) StopCoroutine(captureLoop);
            captureLoop = null;
            if (target != null)
            {
                if (readbackPending) deferredDestroy.Add(target);
                else { target.Release(); Destroy(target); }
                target = null;
            }
            if (fullScreen != null) { fullScreen.Release(); Destroy(fullScreen); fullScreen = null; }
            busy = false;
        }

        private void EnsureFullScreenTexture()
        {
            int sourceWidth = Mathf.Max(1, Screen.width);
            int sourceHeight = Mathf.Max(1, Screen.height);
            if (fullScreen != null && fullScreen.width == sourceWidth && fullScreen.height == sourceHeight) return;
            if (fullScreen != null) { fullScreen.Release(); Destroy(fullScreen); }
            fullScreen = new RenderTexture(sourceWidth, sourceHeight, 0, RenderTextureFormat.ARGB32);
            fullScreen.Create();
        }

        private IEnumerator CaptureLoop()
        {
            var endOfFrame = new WaitForEndOfFrame();
            double next = 0;
            while (true)
            {
                yield return endOfFrame;
                double now = Time.realtimeSinceStartupAsDouble;
                if (now < next) continue;
                next = now + 1.0 / framesPerSecond;
                if (busy || target == null) { lock (gate) skipped++; continue; }
                busy = true;
                int requestGeneration = generation;
                try
                {
                    EnsureFullScreenTexture();
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(fullScreen);
                    Graphics.Blit(fullScreen, target);
                    readbackPending = true;
                    RenderTexture requestedTexture = target;
                    AsyncGPUReadback.Request(requestedTexture, 0, TextureFormat.RGB24, request => OnReadback(request, requestedTexture, requestGeneration, now));
                }
                catch (Exception ex)
                {
                    busy = false;
                    readbackPending = false;
                    lock (gate) lastError = ex.Message;
                }
            }
        }

        private void OnReadback(AsyncGPUReadbackRequest request, RenderTexture requestedTexture, int requestGeneration, double timestamp)
        {
            if (deferredDestroy.Remove(requestedTexture)) { requestedTexture.Release(); Destroy(requestedTexture); }
            if (requestGeneration == generation) readbackPending = false;
            if (requestGeneration != generation) return;
            if (request.hasError) { busy = false; lock (gate) { skipped++; lastError = "GPU readback failed."; } return; }
            byte[] raw;
            try { raw = request.GetData<byte>().ToArray(); }
            catch (Exception ex) { busy = false; lock (gate) { skipped++; lastError = ex.Message; } return; }
            int w = width, h = height, quality = jpegQuality;
            bool flipRows = SystemInfo.graphicsUVStartsAtTop;
            Task.Run(() =>
            {
                try
                {
                    if (flipRows)
                    {
                        int stride = w * 3;
                        var row = new byte[stride];
                        for (int y = 0; y < h / 2; y++)
                        {
                            int top = y * stride, bottom = (h - 1 - y) * stride;
                            Buffer.BlockCopy(raw, top, row, 0, stride);
                            Buffer.BlockCopy(raw, bottom, raw, top, stride);
                            Buffer.BlockCopy(row, 0, raw, bottom, stride);
                        }
                    }
                    byte[] jpeg = ImageConversion.EncodeArrayToJPG(raw, GraphicsFormat.R8G8B8_UNorm, (uint)w, (uint)h, 0, quality);
                    if (requestGeneration != generation) return;
                    lock (gate)
                    {
                        frames.Enqueue(new Frame { Time = timestamp, Jpeg = jpeg });
                        bytes += jpeg.Length;
                        captured++;
                        long limit = (long)maxCompressedMegabytes * 1024 * 1024;
                        while (frames.Count > 0 && (frames.Count > seconds * framesPerSecond || bytes > limit || timestamp - frames.Peek().Time > seconds))
                            bytes -= frames.Dequeue().Jpeg.Length;
                    }
                }
                catch (Exception ex) { lock (gate) { skipped++; lastError = ex.Message; } }
                finally { if (requestGeneration == generation) busy = false; }
            });
        }

        /// <summary>Writes the current compressed ring to a new AVI file. Recording continues.</summary>
        public async void SaveRecentVideo() => await SaveRecentVideoAsync();

        public async Task<string> SaveRecentVideoAsync()
        {
            byte[][] snapshot;
            lock (gate)
            {
                if (exporting) return null;
                if (frames.Count == 0) { lastError = "No frames have been captured yet."; return null; }
                exporting = true;
                var list = new List<byte[]>(frames.Count);
                foreach (var frame in frames) list.Add(frame.Jpeg);
                snapshot = list.ToArray();
            }
            string directory = string.IsNullOrWhiteSpace(outputDirectory) ? Application.persistentDataPath : outputDirectory;
            string path = Path.Combine(directory, "replay_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".avi");
            try
            {
                Directory.CreateDirectory(directory);
                await Task.Run(() => ReplayAviWriter.Write(path, snapshot, width, height, framesPerSecond));
                lock (gate) { lastPath = path; lastError = null; }
                Debug.Log("Replay saved: " + path, this);
                return path;
            }
            catch (Exception ex)
            {
                lock (gate) lastError = ex.Message;
                Debug.LogException(ex, this);
                return null;
            }
            finally { lock (gate) exporting = false; }
        }
    }
}
