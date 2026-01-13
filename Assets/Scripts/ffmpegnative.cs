using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class FFmpegNative
{
    #if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    const string LIB = "libffwrap.dylib";
    #elif UNITY_STANDALONE_WIN
    const string LIB = "ffwrap";
    #endif

    public enum SampleKind : int
    {
        EOF   = 0,
        Video = 1,
        KLV   = 2
    }

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ffw_version();

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ffw_open(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string url,
        out IntPtr ctx,
        out int outWidth,
        out int outHeight);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern int ffw_read_next(
        IntPtr ctx,
        out SampleKind outKind,
        IntPtr videoDstRgba, int videoDstStride,
        out int outW, out int outH,
        IntPtr klvDst, int klvCapacity, out int outKlvLen,
        out long outPts);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ffw_close(ref IntPtr ctx);

    public static string Version => Marshal.PtrToStringAnsi(ffw_version());
}
