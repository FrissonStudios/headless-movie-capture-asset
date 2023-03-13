using System.Diagnostics;
using System.IO;
using System.Net;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

public class FFmpegInstaller : EditorWindow
{
    private static bool opened;

    [MenuItem("Window/Headless Studio/FFmpeg Installer")]
    public static void FFmpegInstallerWindow()
    {
        if (opened) return;
        FFmpegInstaller wnd = GetWindow<FFmpegInstaller>(true);
        wnd.maxSize = new Vector2(400, 150);
        wnd.minSize = wnd.maxSize;
        wnd.titleContent = new GUIContent("Headless Studio Installer");

        opened = true;
    }

    private void OnDestroy()
    {
        opened = false;
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // Import UXML
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/HeadlessStudio/HeadlessMovieCapture/Editor/FFmpegInstaller.uxml");
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/HeadlessStudio/HeadlessMovieCapture/Editor/FFmpegInstallerStyles.uss");
        root.styleSheets.Add(styleSheet);
        visualTree.CloneTree(root);

        var downloadProgressLocation = root.Q<VisualElement>("ui-download");
        var downloadProgress = new ProgressBar();
        downloadProgress.name = "ui-download-progress";
        downloadProgress.style.height = 20;
        downloadProgress.style.display = DisplayStyle.None;
        downloadProgressLocation.Add(downloadProgress);

        var downloadButton = root.Q<Button>("ui-download-button");
        downloadButton.RegisterCallback<MouseUpEvent>(x =>
        {
            if (x.button == 0)
            {
                downloadProgress.title = "Downloading";
                downloadProgress.style.display = DisplayStyle.Flex;
                downloadButton.style.display = DisplayStyle.None;
                StartDownload();
            }
        });
    }

    private const string url = "https://unity.headless.studio/headlessmoviecapture/ffmpeg.zip";

    private void StartDownload()
    {
        var www = new WebClient();
        www.DownloadProgressChanged += DownloadProgress;
        www.DownloadDataCompleted += DownloadComplete;
        www.DownloadDataAsync(new System.Uri(url));
    }

    private void DownloadComplete(object sender, DownloadDataCompletedEventArgs e)
    {
        var www = (WebClient)sender;
        www.DownloadProgressChanged -= DownloadProgress;
        www.DownloadDataCompleted -= DownloadComplete;
        var progress = rootVisualElement.Q<ProgressBar>("ui-download-progress");
        progress.style.display = DisplayStyle.None;

        var downloadProgressLocation = rootVisualElement.Q<VisualElement>("ui-download");
        var installed = new Label("Unpacking FFmpeg");
        installed.style.unityTextAlign = TextAnchor.MiddleCenter;
        downloadProgressLocation.Add(installed);

        string path = Path.Combine(Application.dataPath, "..", "temp", "ffmpeg.zip");
        File.WriteAllBytes(path, e.Result);
        UnpackFFmpeg(path);
        File.Delete(path);

        installed.text = "FFmpeg Installed";
        installed.style.fontSize = 14;
    }

    private void DownloadProgress(object sender, DownloadProgressChangedEventArgs e)
    {
        var progress = rootVisualElement.Q<ProgressBar>("ui-download-progress");
        progress.value = e.ProgressPercentage;
    }

    private void UnpackFFmpeg(string zipPath)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        var ffmpegLocation = Path.Combine(Application.streamingAssetsPath, "FFmpeg", "Windows", "ffmpeg.exe");
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_MAC
        var ffmpegLocation = Path.Combine(Application.streamingAssetsPath, "FFmpeg", "macOS", "ffmpeg");
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        var ffmpegLocation = Path.Combine(Application.streamingAssetsPath, "FFmpeg", "Linux", "ffmpeg");
#endif

        // Setup FFMpeg
        if (File.Exists(zipPath))
        {
            Debug.Log("[<color=yellow>Headless.Studio.MovieCapture</color>] Installing FFmpeg on StreamingAssets.");
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            var lzma = Path.Combine(EditorApplication.applicationContentsPath, "Tools", "7z");
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            var lzma = Path.Combine(EditorApplication.applicationContentsPath, "Tools", "7za");
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_MAC
            var lzma = Path.Combine(EditorApplication.applicationContentsPath, "Tools", "7za");
#endif
            Process.Start(new ProcessStartInfo
            {
                FileName = lzma,
                Arguments = $"x \"{zipPath}\"",
                WorkingDirectory = Application.dataPath,
                CreateNoWindow = true
            })?.WaitForExit();
            AssetDatabase.Refresh();
        }
    }
}