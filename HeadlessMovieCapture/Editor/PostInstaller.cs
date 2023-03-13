using System.Diagnostics;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System;
using System.Collections.Generic;
using System.Reflection;

[InitializeOnLoad]
public static class PostInstaller
{
    static PostInstaller()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        var ffmpegLocation = Path.Combine(Application.streamingAssetsPath, "FFmpeg", "Windows", "ffmpeg.exe");
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_MAC
        var ffmpegLocation = Path.Combine(Application.streamingAssetsPath, "FFmpeg", "macOS", "ffmpeg");
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        var ffmpegLocation = Path.Combine(Application.streamingAssetsPath, "FFmpeg", "Linux", "ffmpeg");
#endif

        // Setup FFMpeg
        if (!File.Exists(ffmpegLocation))
        {
            Debug.Log("[<color=yellow>Headless.Studio.MovieCapture</color>] Missing FFmpeg on StreamingAssets.");
            FFmpegInstaller.FFmpegInstallerWindow();
        }

#if HEADLESS_MOVIE_CAPTURE_URP
        Debug.Log("[<color=yellow>Headless.Studio.MovieCapture</color>] URP detected");
        UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset asset = QualitySettings.renderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (asset != null)
        {
            // HACK: This avoid the ScriptableRendererData creating any extra renderer.
            UnityEngine.Rendering.Universal.ScriptableRendererData rendererData = asset.GetType().GetProperty("scriptableRendererData", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(asset) as UnityEngine.Rendering.Universal.ScriptableRendererData;
            if (rendererData != null)
            {
                // Debug.Log($"[<color=yellow>Headless.Studio.MovieCapture</color>] Valid renderer data {rendererData} at {AssetDatabase.GetAssetPath(rendererData)}");
                if (!rendererData.rendererFeatures.Any(x => x is HeadlessStudio.HeadlessMovieCaptureFeature))
                {
                    Debug.Log($"[<color=yellow>Headless.Studio.MovieCapture</color>] Adding HeadlessMovieCaptureFeature to {rendererData.name}.");
                    if (EditorUtility.IsPersistent(rendererData))
                    {
                        var feature = ScriptableObject.CreateInstance<HeadlessStudio.HeadlessMovieCaptureFeature>();
                        feature.name = "NewHeadlessMovieCaptureFeature";
                        AssetDatabase.AddObjectToAsset(feature, rendererData);
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out var guid, out long localId);
                        rendererData.rendererFeatures.Add(feature);
                        AssetDatabase.SaveAssets();
                    }
                }
                else
                {
                    Debug.Log("[<color=yellow>Headless.Studio.MovieCapture</color>] Found HeadlessMovieCaptureFeature.");
                }
            }
            else
            {
                Debug.LogWarning("[<color=yellow>Headless.Studio.MovieCapture</color>] Failed to detect the Forward Renderer Settings. Please make sure to add the HeadlessMovieCaptureFeature to the URP Renderer features.");
            }
        }
        else
        {
            Debug.LogWarning("[<color=yellow>Headless.Studio.MovieCapture</color>] Failed to detect the Forward Renderer Settings. Please make sure to add the HeadlessMovieCaptureFeature to the URP Renderer features.");
        }
#endif
#if HEADLESS_MOVIE_CAPTURE_HDRP
        // Setup Post process order
        Debug.Log("[<color=yellow>Headless.Studio.MovieCapture</color>] HDRP detected");
        var levels = QualitySettings.names.Length;
        var setOptions = false;
        for (int i = 0; i < levels; i++)
        {
            UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset asset = QualitySettings.GetRenderPipelineAssetAt(i) as UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset;

            if (asset != null)
            {
                //Debug.Log($"[<color=yellow>Headless.Studio.MovieCapture</color>] Found default quality settings for HDRP. ({asset.name})");
                List<string> postProcesses = asset.GetType().GetField("afterPostProcessCustomPostProcesses", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(asset) as List<string>;
                if (postProcesses.Count(x => x.Contains("HeadlessMovieCapturePostProcess")) == 0)
                {
                    Debug.Log($"[<color=yellow>Headless.Studio.MovieCapture</color>] Adding HeadlessMovieCapturePostProcess to custom post process order of {asset.name}.");
                    postProcesses.Add(typeof(HeadlessStudio.HeadlessMovieCapturePostProcess).AssemblyQualifiedName);
                }
                setOptions = true;
            }
        }

        if (!setOptions)
        {
            Debug.LogWarning("[<color=yellow>Headless.Studio.MovieCapture</color>] Failed to detect the Default HDRP Settings. Please make sure to add the HeadlessMovieCapturePostProcess to the After Post Process list, or it will not capture.");
        }
        else
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
#endif
    }
}