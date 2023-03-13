using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace HeadlessStudio
{
    public static class FFmpegOptionsConvert
    {
        private static readonly Dictionary<FFmpegPreset, string> NvidiaPresets = new Dictionary<FFmpegPreset, string>{
            {FFmpegPreset.UltraFast, "llhp" },
            {FFmpegPreset.VeryFast, "ll" },
            {FFmpegPreset.Medium, "medium" },
            {FFmpegPreset.VerySlow, "llhq" },
            {FFmpegPreset.None, string.Empty },
        };
        public static string GetPreset(FFmpegOutput codec, FFmpegPreset preset)
        {
            switch (codec)
            {
                case FFmpegOutput.H264:
                    if (preset != FFmpegPreset.None)
                        return preset.ToString().ToLowerInvariant();
                    return string.Empty;
                case FFmpegOutput.GIF:
                    return string.Empty;
                case FFmpegOutput.H264Nvidia:
                    if (NvidiaPresets.TryGetValue(preset, out var newPreset))
                        return newPreset;
                    return string.Empty;
                case FFmpegOutput.Custom:
                    return string.Empty;
            }
            return "";
        }
    }

    public enum FFmpegPreset
    {
        UltraFast, //llhp
        VeryFast, //ll
        Medium, //medium
        VerySlow, //llhq
        None
    }

    /// <summary>
    /// Other builds of ffmpeg can be found on the official site (https://ffmpeg.org/download.html#build-windows).
    /// The one included is a gpl version which doesn't includes all the codecs.
    /// Custom codec doesn't pass codec parameters to FFmpeg, so extra options are required to setup the output codec.
    /// </summary>
    public enum FFmpegOutput
    {
        H264 = 0,
        GIF = 1,
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        H264Nvidia = 2,
#endif
        //H264Lossless420 = 3,
        //H264Lossless444 = 4,
        //HevcDefault = 5,
        //HevcNvidia = 6,
        //ProRes422 = 7,
        //ProRes4444 = 8,
        //VP8Default = 9,
        //VP9Default = 10,
        //Hap = 11,
        //HapAlpha = 12,
        //HapQ = 13,
        Custom = 14,
    }

    public static class HeadlessMovieCaptureNative
    {
        [DllImport("headlessmoviecapture")]
        private static extern IntPtr session_create(string location, string outputName, string options, byte preset, uint width,
            uint height, float fps, bool use_audio, uint sample_rate, byte channels, bool streaming, bool log);

        [DllImport("headlessmoviecapture")]
        private unsafe static extern bool session_update_video(IntPtr session, void* data, int len);

        [DllImport("headlessmoviecapture")]
        private unsafe static extern bool session_update_audio(IntPtr session, void* data, int len);

        [DllImport("headlessmoviecapture")]
        private static extern void session_free(IntPtr session);

        public static string GetLocation()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            var ffmpegLocation = Path.Combine(Application.streamingAssetsPath, "FFmpeg", "Windows", "ffmpeg.exe");
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_MAC
        var ffmpegLocation = Path.Combine(Application.streamingAssetsPath, "FFmpeg", "macOS", "ffmpeg");
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        var ffmpegLocation = Path.Combine(Application.streamingAssetsPath, "FFmpeg", "Linux", "ffmpeg");
#endif
            return ffmpegLocation;
        }

        private static int _sessions = 0;

        public static IntPtr SessionCreate(string location, string outputName, string options, uint preset, uint width,
            uint height, float fps, bool audio, int sampleRate, int channels, bool streaming, bool log)
        {
            if (_sessions > 0)
            {
                Debug.LogWarning("Running multiple Headless Movie Capture components at the same time isn't supported, use at your own risk.");
            }
            var result = session_create(location, outputName, options, (byte)preset, width, height, fps, audio, (uint)sampleRate, (byte)channels, streaming, log);
            if (result != IntPtr.Zero)
            {
                _sessions++;
            }
            return result;
        }

        public static void SessionFree(IntPtr session)
        {
            if (session != IntPtr.Zero)
            {
                session_free(session);
                _sessions--;
            }
        }

        public unsafe static bool SessionUpdateVideo(IntPtr session, NativeArray<byte> bytes)
        {
            if (session == IntPtr.Zero)
            {
                return false;
            }

            var arrayPtr = NativeArrayUnsafeUtility.GetUnsafePtr(bytes);
            return session_update_video(session, arrayPtr, bytes.Length);
        }

        public unsafe static bool SessionUpdateAudio<T>(IntPtr session, NativeArray<T> data) where T : struct
        {
            if (session == IntPtr.Zero)
            {
                return false;
            }
            var size = data.Length * UnsafeUtility.SizeOf<T>();
            var arrayPtr = NativeArrayUnsafeUtility.GetUnsafePtr(data);
            return session_update_audio(session, arrayPtr, size);
        }

    }
}