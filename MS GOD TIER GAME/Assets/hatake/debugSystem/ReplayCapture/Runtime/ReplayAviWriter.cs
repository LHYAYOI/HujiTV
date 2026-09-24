using System;
using System.Collections.Generic;
using System.IO;

namespace Laboratory.ReplayCapture
{
    // Minimal RIFF AVI 1.0 muxer. Frames are complete JPEG files (MJPG), without audio.
    internal static class ReplayAviWriter
    {
        internal static void Write(string path, IReadOnlyList<byte[]> frames, int width, int height, int fps)
        {
            if (frames == null || frames.Count == 0) throw new ArgumentException("No frames to export.");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(stream))
            {
                FourCC(writer, "RIFF"); var riffSize = stream.Position; writer.Write(0); FourCC(writer, "AVI ");
                FourCC(writer, "LIST"); var hdrlSize = stream.Position; writer.Write(0); FourCC(writer, "hdrl");
                FourCC(writer, "avih"); writer.Write(56);
                writer.Write(1000000 / fps); writer.Write(0); writer.Write(0); writer.Write(0x10);
                writer.Write(frames.Count); writer.Write(0); writer.Write(1); writer.Write(0);
                writer.Write(width); writer.Write(height);
                for (int i = 0; i < 4; i++) writer.Write(0);
                FourCC(writer, "LIST"); var strlSize = stream.Position; writer.Write(0); FourCC(writer, "strl");
                FourCC(writer, "strh"); writer.Write(56); FourCC(writer, "vids"); FourCC(writer, "MJPG");
                writer.Write(0); writer.Write((short)0); writer.Write((short)0); writer.Write(0);
                writer.Write(1); writer.Write(fps); writer.Write(0); writer.Write(frames.Count);
                writer.Write(0); writer.Write(-1); writer.Write(0);
                writer.Write((short)0); writer.Write((short)0); writer.Write((short)width); writer.Write((short)height);
                FourCC(writer, "strf"); writer.Write(40); writer.Write(40); writer.Write(width); writer.Write(height);
                writer.Write((short)1); writer.Write((short)24); FourCC(writer, "MJPG");
                writer.Write(width * height * 3); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
                PatchSize(writer, strlSize); PatchSize(writer, hdrlSize);
                FourCC(writer, "LIST"); var moviSize = stream.Position; writer.Write(0); FourCC(writer, "movi");
                var offsets = new int[frames.Count];
                for (int i = 0; i < frames.Count; i++)
                {
                    offsets[i] = checked((int)(stream.Position - moviSize - 4));
                    FourCC(writer, "00dc"); writer.Write(frames[i].Length); writer.Write(frames[i]);
                    if ((frames[i].Length & 1) != 0) writer.Write((byte)0);
                }
                PatchSize(writer, moviSize);
                FourCC(writer, "idx1"); writer.Write(frames.Count * 16);
                for (int i = 0; i < frames.Count; i++)
                {
                    FourCC(writer, "00dc"); writer.Write(0x10); writer.Write(offsets[i]); writer.Write(frames[i].Length);
                }
                PatchSize(writer, riffSize);
            }
        }

        private static void FourCC(BinaryWriter writer, string value)
        {
            foreach (char c in value) writer.Write((byte)c);
        }

        private static void PatchSize(BinaryWriter writer, long sizePosition)
        {
            long end = writer.BaseStream.Position;
            writer.BaseStream.Position = sizePosition;
            writer.Write(checked((int)(end - sizePosition - 4)));
            writer.BaseStream.Position = end;
        }
    }
}
