#if UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;

namespace Laboratory.ReplayCapture
{
    internal static class ReplayNativeMedia
    {
        private const string Library = "ReplayMediaFoundation";
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int RP_Initialize();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void RP_Shutdown();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr RP_Error();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)] internal static extern IntPtr RP_Open(string path, int width, int height, int fps, int bitrate, int sampleRate, int nv12);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int RP_Write(IntPtr writer, byte[] pixels, int bytes, long frame, short[] pcm, int audioFrames, int flipRows);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int RP_Close(IntPtr writer);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)] internal static extern int RP_Merge(string paths, string output);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)] internal static extern int RP_Inspect(string path, string report, string bitmap);
        internal static string Error => Marshal.PtrToStringAnsi(RP_Error());
        internal static void Check(int result) { if (result != 0) throw new InvalidOperationException(Error); }
    }
}
#endif
