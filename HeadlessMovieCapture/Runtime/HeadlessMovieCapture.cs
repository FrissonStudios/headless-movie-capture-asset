using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace HeadlessStudio
{
    [RequireComponent(typeof(Camera))]
    public partial class HeadlessMovieCapture : MonoBehaviour
    {
        private const string FLIP_MSG = "Flips or unflips the result. This is requires due to some Unity quirks on capturing on the AfterEverthingEvent.";
        private const string USEGPU_MSG = "Uses async GPU calls to retrieve the render textures. Can save some CPU cycles. Only works with DirectX, which makes this Windows only feature.";
        private const string FPS_MSG = "Shows the average FPS during recording. Please note that when recording there is always some overhead, which are shown in this FPS counter.";
        private const string FRAMERATE_MSG = "Sets the Application.targetFramerate, and is the framerate for the output video.";
        private const string REALTIME_MSG = "Captures as fast as possible, but with frame drops, and maay not respect the requested framerate.\nNote when disable, it will render at the requested framerate, but it's not a good option for any realtime gameplay capture.";
        private const string OUTPUTFORMAT_MSG = "FFmpeg output formats. Only a few are enable, but it's possible to download a new ffmpeg build from " +
            "https://ffmpeg.org/download.html#build-windows and use either the custom format passing extra options defining, or use one of the comment formats in the file HeadlessMovieCaptureNative.";
        private const string TOKENDATE_MSG = "Date format when using the date token. Please consult C# custom date format options at https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings.";
        private const string FFMPEGQUALITY_MSG = "This controls the quality of the output, VerySlow is the best quality but takes more time.";
        private const string EXTRAOPTIONS_MSG = "Extra options to pass to ffmpeg.";
        private const string TIMELINE_MSG = "Capture only during the duration of the timeline asset.";
        private const string OUTPUTPATH_MSG = "The output folder. Tokens supported $camera and $date.";
        private const string OUTPUTFILE_MSG = "The output filename with no extension. Tokens supported $camera and $date";
        private const string OPEN_MSG = "If the file is created and Headless Movie Capture is running on the UnityEditor, it will open the output folder.";
        private const string CAPTURE_MSG = "When not set, there will be no output.";
        private const string FRAMETIMING_MSG = "Uses the same approach as Unity recorder frame timing. It may work better in some situations. The default is our custom timming solution.";
        private const string SCREENSHOT_MSG = "Uses the Unity Screenshot utility method to capture the game output. Normally doesn't improve performance/overhead.";
        private const string NATIVELOG_MSG = "When enable creates a diagnostic log file called HeadlessMovieCapture.log on the root of the project or game.";
        private const string LOG_MSG = "Logs some operations of the component to the Unity Console. If disable only, warnings and errors are logged.";

        [HideInInspector]
        public bool showHelp;

        [Help(CAPTURE_MSG)]
        [Tooltip(CAPTURE_MSG)]
        public bool capture = true;

        private bool usePostProcessingStackEffect;


        [Header("General Options")]
        [Help(FLIP_MSG)]
        [Tooltip(FLIP_MSG)]
        public bool flipResult;

        [Help(USEGPU_MSG)]
        [Tooltip(USEGPU_MSG)]
        public bool useAsyncGPUCalls = true;

        [Help(TOKENDATE_MSG)]
        [Tooltip(TOKENDATE_MSG)]
        public string tokenDateFormat = "yyyy-mm-dd-HHmm";

        [Header("Experimental")]
        [Help(FRAMETIMING_MSG)]
        [Tooltip(FRAMETIMING_MSG)]
        public bool useUnityRecorderTiming;

        [Help(SCREENSHOT_MSG)]
        [Tooltip(SCREENSHOT_MSG)]
        public bool useUnityScreenshotAPI;

        [Header("Logging")]
        [Help(FPS_MSG)]
        [Tooltip(FPS_MSG)]
        public bool showFPS = false;

        [Help(NATIVELOG_MSG)]
        [Tooltip(NATIVELOG_MSG)]
        public bool logNativeToFile = false;

        [Help(LOG_MSG)]
        [Tooltip(LOG_MSG)]
        public bool log = true;

        [Header("Output options")]
        [Help(FRAMERATE_MSG)]
        [Tooltip(FRAMERATE_MSG)]
        //public int unityTargetFrameRate = 90;
        public int recordingFrameRate = 60;
        [Help(REALTIME_MSG)]
        [Tooltip(REALTIME_MSG)]
        public bool realtime = true;
        public bool useAudio = true;
        [Space]

        // This is just so we new what preset was applied.
        public string preset;

        [Help(OUTPUTFORMAT_MSG)]
        [Tooltip(OUTPUTFORMAT_MSG)]
        public FFmpegOutput outputFormat = FFmpegOutput.H264;

        [Help(FFMPEGQUALITY_MSG)]
        [Tooltip(FFMPEGQUALITY_MSG)]
        public FFmpegPreset encodingPreset = FFmpegPreset.UltraFast;

        public string videoBitrate = "2500k";
        public string audioBitrate = "128k";

        [Help(EXTRAOPTIONS_MSG)]
        [Tooltip(EXTRAOPTIONS_MSG)]
        public string extraOptions = "";

        [Space]
        [Help(TIMELINE_MSG)]
        [Tooltip(TIMELINE_MSG)]
        public PlayableDirector timeline;

        [Space]
        public bool streaming;

        [Help(OUTPUTPATH_MSG)]
        [Tooltip(OUTPUTPATH_MSG)]
        public string outputFolder = "recordings/";
        [Help(OUTPUTFILE_MSG)]
        [Tooltip(OUTPUTFILE_MSG)]
        public string outputFile = "capture-$date";

        public string keyframeInterval = "0";
        public string streamingAddress = "rtmp://127.0.0.1:8889/live/app";

        [Space]
        [Help(OPEN_MSG)]
        [Tooltip(OPEN_MSG)]
        public bool openOutputFolder = true;

        private IntPtr _session;
        private RenderTexture _captureTexture;
        private Camera _camera;
        private CommandBuffer _cmd;

        private Material _blitMaterial;
        private int _frameCount;
        private float _fpsTimeStart;
        private int _frameDropCount;
        private bool _firstFrame;
        private bool _safeFlag;

        // Framerate related variables
        private int _initialFrame = 0;
        private int _frameCounter = 0;
        private float _timeCounter = 0.0f;
        private float _lastFramerate = 0.0f;
        private float _refreshTime = 0.5f;
        private float _averageFramerate;

        private bool _initDone = false;
        private bool _errorOccur = false;
        private float _fpsNextTimeStart;
        private int _fpsNextFrameCount;
        private float _lastFrameTime;
        private Dictionary<string, string> _fileTokens = new Dictionary<string, string>();
        private Dictionary<string, string> _ffmpegTokens = new Dictionary<string, string>();

        private float FrameTime => _fpsTimeStart + (_frameCount - 0.5f) / recordingFrameRate;

        public bool IsCapturing => _initDone;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            _safeFlag = capture;
            if (!_safeFlag) return;
            StartRecording();
        }

        public void StartRecording()
        {
            if (_initDone) return;
            _errorOccur = false;
            var height = _camera.pixelHeight;
            var width = _camera.pixelWidth;

            SetupTokens();

            // Location of the untouched ffmpeg executable for each platform.
            var ffmpegLocation = HeadlessMovieCaptureNative.GetLocation();

            // Creates a session in native
            string preset = FFmpegOptionsConvert.GetPreset(outputFormat, encodingPreset);
            var options = "";
            if (!string.IsNullOrWhiteSpace(preset) && !extraOptions.Contains("-preset"))
            {
                options += $"-preset {preset}";
            }

            var extra = GetExtraOptions();
            if (!string.IsNullOrEmpty(extra))
            {
                options += $" {extra}";
            }

            string outputPath = GetOutputPath();
            Debug.LogError($"FPS ={recordingFrameRate}");
            _session = HeadlessMovieCaptureNative.SessionCreate(ffmpegLocation, outputPath, options, (uint)outputFormat, (uint)width,
                (uint)height, recordingFrameRate, useAudio, AudioSampleRate(), AudioChannelCount(), streaming, logNativeToFile);

            if (_session == IntPtr.Zero)
            {
                LogError("Failed to create session pipe.");
                enabled = false;
                return;
            }

            // Create our temporary RenderTexture that will be called by async read.
            // This avoids too much stutter.
            _captureTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);

            void disableMe()
            {
                enabled = false;
                HeadlessMovieCaptureNative.SessionFree(_session);
                _session = IntPtr.Zero;

            }

            if (!useUnityScreenshotAPI)
            {
                Component hd;
                if (hd = _camera.GetComponent("HDAdditionalCameraData"))
                {
                    if (hd.GetType().FullName.Contains("Experimental"))
                    {
                        LogInfo("Found old experimental HDRP...");
#if HEADLESS_POST_PROCESSING_STACK

                    var volume = FindObjectOfType<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
                    if (volume)
                    {
                        if (!volume.profile.GetSetting<HeadlessMovieCaptureSettings>())
                        {
                            Debug.Log("Setting up Post Process Volume to support Headless Movie Capture...");
                            volume.profile.AddSettings<HeadlessMovieCaptureSettings>();
                        }
                        usePostProcessingStackEffect = true;
                    }
                    else
                    {
                        disableMe();
                        return;
                    }
#endif
                    }
                    else
                    {
#if HEADLESS_MOVIE_CAPTURE_HDRP
                        LogInfo("Found release version of HDRP...");
                        
                        var volume = Array.Find(FindObjectsOfType<Volume>(), x => x.isGlobal);
                        if (volume == null)
                        {
                            LogError("Please add a global volume. With this we can't capture.");
                            disableMe();
                            return;
                        }
                        if (!volume.profile.TryGet<HeadlessMovieCapturePostProcess>(out var hmc))
                        {
                            LogInfo("Adding Headless Movie Capture to Global PostProcess Volume.");
                            hmc = volume.profile.Add<HeadlessMovieCapturePostProcess>();
                        }
                        usePostProcessingStackEffect = true;
#endif
                    }
                }
#if HEADLESS_MOVIE_CAPTURE_URP
                else if (_camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() != null)
                {
                    LogInfo("Found URP...");
                    usePostProcessingStackEffect = true;
                }
#endif
                else
                {
                    LogInfo("Standard Unity Pipeline...");
                    if (_camera.GetCommandBuffers(CameraEvent.AfterEverything).All(x => x.name != "HeadlessMovieCapture"))
                    {
                        _cmd = new CommandBuffer
                        {
                            name = "HeadlessMovieCapture"
                        };
                        _cmd.Blit(BuiltinRenderTextureType.CurrentActive, _captureTexture);
                        _camera.AddCommandBuffer(CameraEvent.AfterEverything, _cmd);
                    }
                }
            }
            Application.runInBackground = true;
            if (!realtime)
            {
                Time.captureFramerate = recordingFrameRate;
            }
            _frameCount = 0;
            _firstFrame = true;
            _initDone = true;
            StartAudioRecording();
        }

        public void StopRecording()
        {
            if (_session != IntPtr.Zero)
            {
                StopAudioRecording();
                HeadlessMovieCaptureNative.SessionFree(_session);
            }
            _session = IntPtr.Zero;
            var dt = 60 - 1f / (1f / 60f + _updateDt / 1000f);
            string message = $"Capture overhead average was {_updateDt:0.0} ms around {dt:0} frames";
            if (recordingFrameRate <= _averageFramerate)
            {
                LogInfo($"Requested framerate was {recordingFrameRate} and the average framerate was {_averageFramerate}.");
                LogInfo(message);
            }
            else
            {
                LogWarn($"Requested recording framerate was {recordingFrameRate} and the average recorded framerate was {_averageFramerate}. This may cause the video to be at a greater framerate then the true captured framerate, making it look accelerated.");
                LogWarn(message);
            }
            _captureTexture.Release();
            //Application.targetFrameRate = _oldFrameRate;
            Time.captureFramerate = 0;
            _updateDt = 0;
            _initDone = false;
            _firstFrame = false;
        }

        private void SetupTokens(bool editor = false)
        {
            _ffmpegTokens.Clear();
            _fileTokens.Clear();
            _fileTokens.Add("$date", DateTime.Now.ToString(tokenDateFormat));
            if (editor)
            {
                _fileTokens.Add("$camera", "MainCamera");
            }
            else
            {
                _fileTokens.Add("$camera", _camera.name.Replace(":", "").Replace("/", ""));
            }
            _ffmpegTokens.Add("$vb", videoBitrate);
            _ffmpegTokens.Add("$ab", audioBitrate);
            _ffmpegTokens.Add("$framerate", recordingFrameRate.ToString());
            _ffmpegTokens.Add("$fpsmul2", (recordingFrameRate * 2).ToString());
            _ffmpegTokens.Add("$fpsdiv2", (recordingFrameRate / 2).ToString());
        }

        private string Parse(string valueString)
        {
            foreach (var token in _ffmpegTokens.Keys)
            {
                valueString = valueString.Replace(token, _ffmpegTokens[token]);
            }
            return valueString;
        }

        private string GetExtraOptions()
        {
            var options = string.Empty;
            var bitrateRegex = new Regex(@"\d+(k|M)?", RegexOptions.IgnoreCase);
            var audioBitrateMatch = bitrateRegex.Match(Parse(audioBitrate));
            if (audioBitrateMatch.Success && useAudio && audioBitrateMatch.Value != "0")
            {
                options += $" -b:a {audioBitrateMatch.Value}";
            }
            var videoBitrateMatch = bitrateRegex.Match(Parse(videoBitrate));
            if (videoBitrateMatch.Success && videoBitrateMatch.Value != "0")
            {
                options += $" -b:v {videoBitrateMatch}";
            }

            var kfiMatch = Regex.Match(Parse(keyframeInterval), "\\d+");
            if (kfiMatch.Success)
            {
                options += $" -g {kfiMatch.Value}";
            }
            options += $" {extraOptions}";
            foreach (var token in _ffmpegTokens.Keys)
            {
                options = options.Replace(token, _ffmpegTokens[token]);
            }
            return options;
        }

        public string GetOutputPath(bool editor = false)
        {
            if (editor)
            {
                _fileTokens.Clear();
                _fileTokens = new Dictionary<string, string>();
                SetupTokens(editor);
            }
            string outputPath;
            if (streaming)
            {
                outputPath = streamingAddress;
                foreach (var token in _fileTokens.Keys)
                {
                    outputPath = outputPath.Replace(token, _fileTokens[token]);
                }
                outputPath = outputPath.Replace("$", "");
            }
            else
            {

                var folder = outputFolder;
                var file = outputFile;
                foreach (var token in _fileTokens.Keys)
                {
                    folder = folder.Replace(token, _fileTokens[token]);
                    file = file.Replace(token, _fileTokens[token]);
                }
                folder = folder.Replace("$", "");
                file = file.Replace("$", "");

                outputPath = Path.Combine(folder, file);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
            }

            return outputPath;
        }

        private void OnDisable()
        {
            if (!_initDone) return;

            // Cleanup our native plugin
            StopRecording();
#if UNITY_EDITOR
            var outputPath = GetOutputPath();
            if (File.Exists(outputPath + ".mp4") && openOutputFolder && !streaming)
            {
                if (!Path.IsPathRooted(outputPath))
                {
                    outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputPath + ".mp4"));
                }
                outputPath = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(outputPath))
                    Process.Start(outputPath);
            }
            _safeFlag = false;
#endif
        }

        private void Update()
        {
            CalculateFramerate();

            if (_session == IntPtr.Zero || !_safeFlag) return;
            if (usePostProcessingStackEffect) return;
            if (useUnityScreenshotAPI)
                ScreenCapture.CaptureScreenshotIntoRenderTexture(_captureTexture);
            Capture(_captureTexture);
        }

        private void OnGUI()
        {
            if (showFPS)
            {
                float diff = _averageFramerate - recordingFrameRate;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"FPS: {_averageFramerate:#.0} (");
                var oldColor = GUI.color;
                if (diff < 0)
                {
                    GUI.color = Color.red;
                }
                else
                {
                    GUI.color = Color.green;
                }
                GUILayout.Label($"{diff:0.0}");
                GUI.color = oldColor;
                GUILayout.Label($") overhead {_updateDt:0} ms");
                GUILayout.EndHorizontal();
            }
        }

        public void Capture(RenderTexture captureTexture)
        {
            if (_session == IntPtr.Zero) return;

            clock = Stopwatch.StartNew();

            if (_firstFrame)
            {
                _fpsTimeStart = Time.unscaledTime;
                _initialFrame = Time.renderedFrameCount;
                _firstFrame = false;
            }
            // HACK: Stop recording when the time is equal to the duration.
            if (timeline && Math.Abs(timeline.time - timeline.duration) < float.Epsilon)
                return;

            Profiler.BeginSample("HeadlessMovieCaptures.Capture");

            if (useUnityScreenshotAPI)
            {
                PushFrame(captureTexture);
                _frameCount++;
            }
            else if (useUnityRecorderTiming)
            {
                PushFrame(captureTexture);

                var frameCount = Time.renderedFrameCount - _initialFrame;
                var frameLen = 1.0f / recordingFrameRate;
                var elapsed = Time.unscaledTime - _fpsTimeStart;
                var target = frameLen * (frameCount + 1);
                var sleep = (int)((target - elapsed) * 1000);
                _averageFramerate = frameCount / elapsed;

                if (sleep > 2)
                {
                    //Debug.Log(string.Format("Going to fast we need to sleep => dT: {0:F1}s, Target dT: {1:F1}s, Retarding: {2}ms, fps: {3:F1}", elapsed, target, sleep, frameCount / elapsed));
                    Thread.Sleep(Math.Min(sleep, 1000));
                }
                else if (sleep < -frameLen)
                    _initialFrame--;
                // reset every 30 frames
                if (frameCount % 50 == 49)
                {
                    _fpsNextTimeStart = Time.unscaledTime;
                    _fpsNextFrameCount = Time.renderedFrameCount;
                }
                if (frameCount % 100 == 99)
                {
                    _fpsTimeStart = _fpsNextTimeStart;
                    _initialFrame = _fpsNextFrameCount;
                }

            }
            else
            {
                var gap = Time.unscaledTime - FrameTime;
                var delta = 1f / recordingFrameRate;

                if (gap < 0)
                {
                    // Update without frame data.
                    PushFrame(null);
                }
                else if (gap < delta)
                {
                    // Single-frame behind from the current time:
                    // Push the current frame to FFmpeg.
                    PushFrame(captureTexture);
                    _frameCount++;
                }
                else if (gap < delta * 2)
                {
                    // Two-frame behind from the current time:
                    // Push the current frame twice to FFmpeg. Actually this is not
                    // an efficient way to catch up. We should think about
                    // implementing frame duplication in a more proper way. #fixme
                    PushFrame(captureTexture);
                    PushFrame(captureTexture);
                    _frameCount += 2;
                }
                else
                {
                    // Show a warning message about the situation.
                    WarnFrameDrop();

                    // Push the current frame to FFmpeg.
                    PushFrame(captureTexture);

                    // Compensate the time delay.
                    _frameCount += Mathf.FloorToInt(gap * recordingFrameRate);
                }
            }

            Profiler.EndSample();
        }

        private void CalculateFramerate()
        {
            if (!realtime || Time.captureFramerate == recordingFrameRate)
            {
                _averageFramerate = recordingFrameRate;
                return;
            }

            if (_timeCounter < _refreshTime)
            {
                if (_lastFrameTime == 0)
                {
                    _lastFrameTime = Time.unscaledDeltaTime;
                }
                _timeCounter += Time.unscaledTime - _lastFrameTime;
                _frameCounter++;
                _lastFrameTime = Time.unscaledTime;
            }
            else
            {
                //This code will break if you set your m_refreshTime to 0, which makes no sense.
                float framerate = (float)_frameCounter / _timeCounter;
                if (_lastFramerate != 0)
                {
                    _averageFramerate = (_lastFramerate + framerate) / 2.0f;
                }

                _lastFramerate = framerate;
                _frameCounter = 1;
                _timeCounter = Time.unscaledTime - _lastFrameTime;
                _lastFrameTime = Time.unscaledTime;

            }
        }

        public void PushFrame(RenderTexture source)
        {
            if (_session == IntPtr.Zero) return;

            if (source != null) QueueFrame(source);
        }

        private void QueueFrame(RenderTexture source)
        {
            if (_errorOccur) return;
            Profiler.BeginSample("HeadlessMovieCapture.QueueFrame");

            // Lazy initialization of the preprocessing blit shader
            if (_blitMaterial == null)
            {
                var shader = Shader.Find("Hidden/HeadlessMovieCapture/Preprocess");
                _blitMaterial = new Material(shader);
            }

            // Blit to a temporary texture and request readback on it.
            var desc = new RenderTextureDescriptor(source.width, source.height, RenderTextureFormat.ARGB32, 0, 0);
            desc.sRGB = true;
            var rt = RenderTexture.GetTemporary(desc);
            if (flipResult)
                Graphics.Blit(source, rt, _blitMaterial, 0);
            else
                Graphics.Blit(source, rt);

#if (UNITY_EDITOR_LINUX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_MAC)
            useAsyncGPUCalls = false;
#endif
            // AsyncGPU is only supported on Windows 
            if (useAsyncGPUCalls)
            {
                AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, ReadbackDone);
            }
            else
            {
                var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                RenderTexture.active = null;
                var data = texture.GetRawTextureData<byte>();
                if (HeadlessMovieCaptureNative.SessionUpdateVideo(_session, data))
                {
                    RenderAudio();
                }
                else if (_session != IntPtr.Zero)
                {
                    _errorOccur = true;
                    LogError("An error occur. Please check the log file.");
                    StopRecording();
                }
                Destroy(texture);
            }

            RenderTexture.ReleaseTemporary(rt);
            Profiler.EndSample();
        }

        private Stopwatch clock;
        private float _updateDt;

        private void ReadbackDone(AsyncGPUReadbackRequest req)
        {
            if (_errorOccur) return;
            Profiler.BeginSample("HeadlessMovieCapture.ReadbackDone:GetData");
            var data = req.GetData<byte>();
            Profiler.EndSample();
            Profiler.BeginSample("HeadlessMovieCapture.ReadbackDone:SessionUpdate");
            if (HeadlessMovieCaptureNative.SessionUpdateVideo(_session, data))
            {
                RenderAudio();
            }
            else if(_session != IntPtr.Zero)
            {
                _errorOccur = true;
                LogError("An error occur. Please check the log file.");
                StopRecording();
            }
            Profiler.EndSample();
            if (_updateDt == 0)
            {
                _updateDt = clock.ElapsedMilliseconds;
            }
            else
            {
                _updateDt = (_updateDt + clock.ElapsedMilliseconds) / 2f;
            }
        }

        public void WarnFrameDrop()
        {
            if (++_frameDropCount != 10) return;

            LogWarn(
                "Significant frame droppping was detected. This may introduce time instability into output video. Decreasing the recording frame rate is recommended."
            );
        }

        /// <summary>
        /// Get the current native session running for this camera
        /// </summary>
        /// <returns>Pointer to the sessions struct</returns>
        public IntPtr GetNativeSession()
        {
            return _session;
        }

        /// <summary>
        /// Gets the blit material that handles flipping.
        /// </summary>
        /// <returns>Material with preprocess shader</returns>
        public Material GetBlitMaterial()
        {
            if (_blitMaterial == null)
            {
                var shader = Shader.Find("Hidden/HeadlessMovieCapture/Preprocess");
                _blitMaterial = new Material(shader);
            }

            return _blitMaterial;
        }

        public void LogInfo(string message)
        {
            if (log)
            {
                Debug.Log($"[<color=yellow>Headless.Studio.MovieCapture</color>] {message}");
            }
        }

        public void LogWarn(string message)
        {
            Debug.LogWarning($"[<color=yellow>Headless.Studio.MovieCapture</color>] {message}");
        }

        public void LogError(string message)
        {
            Debug.LogError($"[<color=yellow>Headless.Studio.MovieCapture</color>] {message}");
        }
    }
}