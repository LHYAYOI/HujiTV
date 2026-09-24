#if UNITY_EDITOR_WIN
using System;
using System.Threading;

namespace Laboratory.ReplayCapture
{
    // Single audio producer / single encoder consumer. Per-frame sequence stamps
    // prevent reading a slot being overwritten; neither side takes a lock.
    internal sealed class ReplayAudioRing
    {
        private readonly float[] samples;
        private readonly long[] stamps;
        private readonly int rate, capacity;
        private readonly double origin;
        internal long DroppedBlocks, MissingFrames, WrittenFrames;
        private long latestEnd;
        private long pauseOffsetBits;
        internal volatile bool Suspended;
        internal void AddPauseOffset(double seconds)
        {
            double previous=BitConverter.Int64BitsToDouble(Interlocked.Read(ref pauseOffsetBits));
            Interlocked.Exchange(ref pauseOffsetBits,BitConverter.DoubleToInt64Bits(previous+Math.Max(0,seconds)));
        }
        internal long LatestEnd => Interlocked.Read(ref latestEnd);
        internal ReplayAudioRing(int sampleRate, double dspOrigin)
        {
            rate = sampleRate; origin = dspOrigin; capacity = rate * 2;
            samples = new float[capacity * 2]; stamps = new long[capacity];
            for (int i = 0; i < capacity; i++) stamps[i] = -1;
        }
        internal void Write(float[] input, int channels, double dspTime)
        {
            if (channels < 1 || Suspended) return;
                double pauseOffset=BitConverter.Int64BitsToDouble(Interlocked.Read(ref pauseOffsetBits));
                long start = (long)Math.Round((dspTime - origin - pauseOffset) * rate);
                int frames = input.Length / channels;
                for (int f = 0; f < frames; f++)
                {
                    long at = start + f;
                    if (at < 0) continue;
                    int slot = (int)(at % capacity);
                    Interlocked.Exchange(ref stamps[slot], -1);
                    samples[slot * 2] = input[f * channels];
                    samples[slot * 2 + 1] = input[f * channels + (channels > 1 ? 1 : 0)];
                    Volatile.Write(ref stamps[slot], at);
                }
                Interlocked.Add(ref WrittenFrames, frames);
                Interlocked.Exchange(ref latestEnd,start+frames);
        }
        internal void Read(long start, int frames, short[] output)
        {
                long missing = 0;
                for (int f = 0; f < frames; f++)
                {
                    long at = start + f; int slot = (int)(at % capacity);
                    bool available = Volatile.Read(ref stamps[slot]) == at;
                    float left = samples[slot * 2], right = samples[slot * 2 + 1];
                    available &= Volatile.Read(ref stamps[slot]) == at;
                    if (!available) missing++;
                    for (int c = 0; c < 2; c++)
                    {
                        float value = available ? (c == 0 ? left : right) : 0;
                        output[f * 2 + c] = (short)(Math.Max(-1f, Math.Min(1f, value)) * 32767f);
                    }
                }
                Interlocked.Add(ref MissingFrames, missing);
        }
    }
}
#endif
