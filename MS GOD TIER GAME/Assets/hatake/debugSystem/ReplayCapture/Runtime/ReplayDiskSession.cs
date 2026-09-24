#if UNITY_EDITOR_WIN
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Laboratory.ReplayCapture
{
    internal sealed class ReplayDiskSession : IDisposable
    {
        private sealed class Job { internal byte[] Pixels; internal long Frame; internal TaskCompletionSource<string> Save; }
        private sealed class Segment { internal string Path; internal long Frames, Bytes; internal int Pins; }
        private readonly BlockingCollection<Job> queue = new BlockingCollection<Job>(13);
        private readonly ConcurrentQueue<byte[]> pool = new ConcurrentQueue<byte[]>();
        private readonly List<Segment> segments = new List<Segment>();
        private readonly object segmentGate = new object();
        private readonly Thread worker;
        private readonly int width, height, fps, bitrate, rate, seconds, segmentSeconds;
        private readonly long diskLimit;
        private readonly int flipRows;
        private readonly bool nv12;
        private readonly string output, cache;
        private IntPtr encoder;
        private string currentPath;
        private FileStream currentPcm;
        private long currentFrames, nextFrame, closedFrames, diskBytes;
        private int fileIndex, exporting;
        private Task exportTask;
        private volatile bool preserveCache;
        internal readonly ReplayAudioRing Audio;
        internal volatile string Error, LastPath;
        internal long EncodedFrames, DuplicatedFrames, RejectedFrames, EvictedSegments;
        internal double EncodeMilliseconds;
        internal bool IsExporting => Volatile.Read(ref exporting) != 0;
        internal long DiskBytes { get { lock(segmentGate) return diskBytes; } }
        internal double BufferedSeconds { get { lock(segmentGate) return (closedFrames + Interlocked.Read(ref currentFrames)) / (double)fps; } }
        internal string CacheDirectory => cache;

        internal ReplayDiskSession(int w, int h, int frameRate, int bits, int sampleRate, int duration, int segmentDuration,
            int diskMegabytes, string cacheDirectory, string outputDirectory, double dspOrigin, bool flipVideo, bool useNv12)
        {
            width=w; height=h; fps=frameRate; bitrate=bits; rate=sampleRate; seconds=duration; segmentSeconds=segmentDuration;
            diskLimit=(long)diskMegabytes*1048576; cache=cacheDirectory; output=outputDirectory;
            flipRows=flipVideo?1:0;
            nv12=useNv12;
            Directory.CreateDirectory(cache); Directory.CreateDirectory(output);
            Audio=new ReplayAudioRing(rate,dspOrigin);
            for(int i=0;i<(nv12?14:6);i++) pool.Enqueue(new byte[checked(nv12?width*height*3/2:width*height*4)]);
            worker=new Thread(Run) { IsBackground=true, Name="Replay MP4 encoder" }; worker.Start();
        }
        internal bool Rent(out byte[] buffer) => pool.TryDequeue(out buffer);
        internal void Return(byte[] buffer) => pool.Enqueue(buffer);
        internal void Submit(byte[] pixels, long frame)
        {
            try
            {
                if(Error==null && !queue.IsAddingCompleted && queue.TryAdd(new Job { Pixels=pixels, Frame=frame })) return;
            }
            catch(InvalidOperationException) { /* Worker closed the queue after the state check. */ }
            Return(pixels); Interlocked.Increment(ref RejectedFrames);
        }
        internal Task<string> Save()
        {
            if(Error!=null) return Task.FromException<string>(new InvalidOperationException(Error));
            if(Interlocked.CompareExchange(ref exporting,1,0)!=0) return Task.FromException<string>(new InvalidOperationException("Export already in progress"));
            var completion=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                if(!queue.IsAddingCompleted && queue.TryAdd(new Job { Save=completion })) return completion.Task;
            }
            catch(InvalidOperationException) { }
            Interlocked.Exchange(ref exporting,0); completion.SetException(new InvalidOperationException("Encoder queue is full or stopped; retry after it drains."));
            return completion.Task;
        }
        private void OpenSegment()
        {
            long reserve=(long)bitrate*segmentSeconds/8*2+4*1048576;
            var drive=new DriveInfo(Path.GetPathRoot(cache));
            if(drive.AvailableFreeSpace<reserve+64*1048576) throw new IOException("Insufficient disk space for replay cache");
            lock(segmentGate)
            {
                Evict(reserve);
                if(diskBytes+reserve>diskLimit) throw new IOException("Replay cache limit reached while segments are protected for export");
            }
            currentPath=Path.Combine(cache,$"segment_{fileIndex++:D6}.mp4");
            encoder=ReplayNativeMedia.RP_Open(currentPath,width,height,fps,bitrate,rate,nv12?1:0);
            if(encoder==IntPtr.Zero) throw new InvalidOperationException(ReplayNativeMedia.Error);
            currentPcm=new FileStream(Path.ChangeExtension(currentPath,"pcm"),FileMode.CreateNew,FileAccess.Write,FileShare.Read,65536);
            currentFrames=0;
        }
        private void CloseSegment()
        {
            if(encoder==IntPtr.Zero) return;
            var handle=encoder; encoder=IntPtr.Zero;
            currentPcm?.Dispose(); currentPcm=null;
            ReplayNativeMedia.Check(ReplayNativeMedia.RP_Close(handle));
            if(currentFrames>0)
            {
                var segment=new Segment { Path=currentPath, Frames=currentFrames, Bytes=new FileInfo(currentPath).Length+new FileInfo(Path.ChangeExtension(currentPath,"pcm")).Length };
                lock(segmentGate) { segments.Add(segment); closedFrames+=segment.Frames; diskBytes+=segment.Bytes; currentFrames=0; Evict(0); }
            }
        }
        private void Evict(long reserve)
        {
            // Drop whole segments, so the exported window never exceeds the chosen duration.
            while(segments.Count>0 && (closedFrames>seconds*(long)fps || diskBytes+reserve>diskLimit))
            {
                var oldest=segments[0]; if(oldest.Pins!=0) break;
                File.Delete(oldest.Path); File.Delete(Path.ChangeExtension(oldest.Path,"pcm")); segments.RemoveAt(0); closedFrames-=oldest.Frames; diskBytes-=oldest.Bytes;
                Interlocked.Increment(ref EvictedSegments);
            }
        }
        private void StartExport(TaskCompletionSource<string> completion)
        {
            CloseSegment();
            Segment[] snapshot;
            lock(segmentGate)
            {
                snapshot=segments.ToArray();
                if(snapshot.Length==0) throw new InvalidOperationException("No frames have been encoded yet");
                foreach(var segment in snapshot) segment.Pins++;
            }
            exportTask=Task.Run(() =>
            {
                bool initialized=false;
                string final=Path.Combine(output,"replay_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N").Substring(0,6)+".mp4");
                string partial=Path.ChangeExtension(final,"partial.mp4");
                try
                {
                    if(new DriveInfo(Path.GetPathRoot(output)).AvailableFreeSpace < snapshot.Sum(s=>s.Bytes)+64*1048576)
                        throw new IOException("Insufficient disk space for replay export");
                    ReplayNativeMedia.Check(ReplayNativeMedia.RP_Initialize()); initialized=true;
                    ReplayNativeMedia.Check(ReplayNativeMedia.RP_Merge(string.Join("\n",snapshot.Select(s=>s.Path)),partial));
                    File.Move(partial,final); LastPath=final; preserveCache=false; completion.SetResult(final);
                }
                catch(Exception ex)
                {
                    preserveCache=true;
                    try { if(File.Exists(partial)) File.Delete(partial); } catch(IOException) { }
                    completion.SetException(ex);
                }
                finally
                {
                    if(initialized) ReplayNativeMedia.RP_Shutdown();
                    lock(segmentGate) { foreach(var s in snapshot) s.Pins--; }
                    Interlocked.Exchange(ref exporting,0);
                }
            });
        }
        private void Run()
        {
            bool initialized=false; byte[] previous=null;
            var pcm=new short[(rate/fps+2)*2];
            var pcmBytes=new byte[pcm.Length*2];
            try
            {
                ReplayNativeMedia.Check(ReplayNativeMedia.RP_Initialize()); initialized=true;
                foreach(var job in queue.GetConsumingEnumerable())
                {
                    if(job.Save!=null)
                    {
                        try { StartExport(job.Save); }
                        catch(Exception ex) { Interlocked.Exchange(ref exporting,0); job.Save.SetException(ex); }
                        continue;
                    }
                    if(job.Frame<nextFrame) { Return(job.Pixels); continue; }
                    // Do not try to catch up minutes of a stalled Editor by encoding duplicates.
                    if(job.Frame-nextFrame>fps*2) { Return(job.Pixels); throw new InvalidOperationException("Capture clock gap exceeded 2 seconds. Restart recording after Pause/recompile/device change."); }
                    for(;nextFrame<=job.Frame;nextFrame++)
                    {
                        if(encoder==IntPtr.Zero) OpenSegment();
                        bool duplicate=nextFrame<job.Frame;
                        byte[] pixels=duplicate && previous!=null ? previous : job.Pixels;
                        long audioStart=nextFrame*rate/fps, audioEnd=(nextFrame+1)*rate/fps;
                        // The DSP callback produces whole blocks, sometimes just after a video frame.
                        // Wait only on the encoding worker, with a bounded silence fallback.
                        int audioWaitLimit=(int)Math.Ceiling(1000.0/fps)+100;
                        for(int wait=0;Audio.LatestEnd<audioEnd && wait<audioWaitLimit && !queue.IsAddingCompleted;wait++) Thread.Sleep(1);
                        Audio.Read(audioStart,(int)(audioEnd-audioStart),pcm);
                        int pcmCount=(int)(audioEnd-audioStart)*4;
                        Buffer.BlockCopy(pcm,0,pcmBytes,0,pcmCount); currentPcm.Write(pcmBytes,0,pcmCount);
                        var timer=System.Diagnostics.Stopwatch.StartNew();
                        ReplayNativeMedia.Check(ReplayNativeMedia.RP_Write(encoder,pixels,pixels.Length,currentFrames,pcm,(int)(audioEnd-audioStart),flipRows));
                        EncodeMilliseconds=timer.Elapsed.TotalMilliseconds;
                        Interlocked.Increment(ref currentFrames); Interlocked.Increment(ref EncodedFrames);
                        if(duplicate) Interlocked.Increment(ref DuplicatedFrames);
                        if(currentFrames>=segmentSeconds*(long)fps) CloseSegment();
                    }
                    if(previous!=null) Return(previous); previous=job.Pixels;
                }
            }
            catch(Exception ex) { Error=ex.Message; }
            finally
            {
                if(previous!=null) Return(previous);
                try { CloseSegment(); } catch(Exception ex) { Error=ex.Message; }
                if(initialized) ReplayNativeMedia.RP_Shutdown();
                // Prevent producers adding jobs after the drain, including a pending save request.
                queue.CompleteAdding();
                while(queue.TryTake(out var job))
                {
                    if(job.Pixels!=null) Return(job.Pixels);
                    job.Save?.TrySetException(new InvalidOperationException(Error ?? "Recording stopped"));
                }
            }
        }
        public void Dispose()
        {
            queue.CompleteAdding(); worker.Join(); exportTask?.GetAwaiter().GetResult();
            // On errors leave files recoverable. On normal shutdown remove only files
            // created by this session, after any export has released its snapshot.
            if(Error==null && !preserveCache)
            {
                try
                {
                    lock(segmentGate) { foreach(var segment in segments) { File.Delete(segment.Path); File.Delete(Path.ChangeExtension(segment.Path,"pcm")); } }
                    if(Directory.GetFileSystemEntries(cache).Length==0) Directory.Delete(cache,false);
                }
                catch(IOException ex) { Error="Cache cleanup: "+ex.Message; }
            }
            queue.Dispose();
        }
    }
}
#endif
