using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HeadlessStudio;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(HeadlessMovieCapture))]
public class HeadlessMovieCaptureEditor : Editor
{
    private class SafeEditorVerticalLayout : IDisposable
    {
        public SafeEditorVerticalLayout(string style)
        {
            EditorGUILayout.BeginVertical(style);
        }

        public void Dispose()
        {
            EditorGUILayout.EndVertical();
        }
    }

    private static bool IsPowerOfTwo(int n) =>
        n != 0 && ((int)Math.Ceiling(Math.Log(n) / Math.Log(2)) == (int)Math.Floor(Math.Log(n) / Math.Log(2)));


    [Serializable]
    public class OptionsList
    {
        public List<OptionPreset> presets = new List<OptionPreset>();
    }

    [Serializable]
    public class OptionPreset
    {
        public string name;
        public string options;
        public string codec;
        public string preset;
        public string keyframeinterval;
        public string videobitrate;
        public string audiobitrate;
        public string streaming;
        public string streamingAddress;

        public Action<SerializedObject> GetAction()
        {
            return (m) =>
                         {
                             if (Enum.TryParse(preset, out FFmpegPreset presetEnum))
                             {
                                 m.FindProperty(nameof(HeadlessMovieCapture.encodingPreset)).enumValueIndex = Enum.GetNames(typeof(FFmpegPreset)).ToList().IndexOf(presetEnum.ToString());
                             }
                             if (Enum.TryParse(codec, out FFmpegOutput codecEnum))
                             {
                                 m.FindProperty(nameof(HeadlessMovieCapture.outputFormat)).enumValueIndex = Enum.GetNames(typeof(FFmpegOutput)).ToList().IndexOf(codecEnum.ToString());
                             }
                             if (!string.IsNullOrWhiteSpace(keyframeinterval))
                             {
                                 m.FindProperty(nameof(HeadlessMovieCapture.keyframeInterval)).stringValue = keyframeinterval;
                             }
                             if (!string.IsNullOrWhiteSpace(videobitrate))
                             {
                                 m.FindProperty(nameof(HeadlessMovieCapture.videoBitrate)).stringValue = videobitrate;
                             }
                             if (!string.IsNullOrWhiteSpace(audiobitrate))
                             {
                                 m.FindProperty(nameof(HeadlessMovieCapture.audioBitrate)).stringValue = audiobitrate;
                             }
                             if (bool.TryParse(streaming, out bool streamingEnabled))
                             {
                                 m.FindProperty(nameof(HeadlessMovieCapture.streaming)).boolValue = streamingEnabled;
                             }
                             if (!string.IsNullOrWhiteSpace(streamingAddress))
                             {
                                 m.FindProperty(nameof(HeadlessMovieCapture.streamingAddress)).stringValue = streamingAddress;
                             }
                             m.FindProperty(nameof(HeadlessMovieCapture.extraOptions)).stringValue = options;
                         };
        }
    }

    private VisualElement rootElement;

    public void OnEnable()
    {
        rootElement = new VisualElement();

        var visualTree = Resources.Load<VisualTreeAsset>("MovieCaptureUIEditor"); //AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/HeadlessStudio/HeadlessMovieCapture/Editor/HmcUIEditor.uxml");
        StyleSheet styleSheet = Resources.Load<StyleSheet>("MovieCaptureUIEditorStyles"); //AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/HeadlessStudio/HeadlessMovieCapture/Editor/Resources/HmcUIEditor.uss");
        rootElement.styleSheets.Add(styleSheet);
        visualTree.CloneTree(rootElement);
    }

    private void VerifyCaptureState()
    {
        if (!EditorApplication.isPlaying) return;
        var movieCapture = (HeadlessMovieCapture)target;
        if (movieCapture.IsCapturing && rootElement.Q<Button>("btnCapture").text == "CAPTURE")
        {
            rootElement.Q<Button>("btnCapture").text = "STOP";
            rootElement.Q<Button>("btnCapture").EnableInClassList("capture", false);
            rootElement.Q<Button>("btnCapture").EnableInClassList("recording", true);
        }
        else if(!movieCapture.IsCapturing && rootElement.Q<Button>("btnCapture").text == "STOP")
        {
            rootElement.Q<Button>("btnCapture").text = "CAPTURE";
            rootElement.Q<Button>("btnCapture").EnableInClassList("capture", true);
            rootElement.Q<Button>("btnCapture").EnableInClassList("recording", false);
        }
    }

    public override VisualElement CreateInspectorGUI()
    {
        var movieCapture = (HeadlessMovieCapture)target;

        UpdateTopMessages();

        CheckCaptureState(movieCapture.capture);

        var scheduler = rootElement.schedule.Execute(VerifyCaptureState);
        scheduler.Every(100);

        rootElement.Q<Button>("btnCapture").RegisterCallback<MouseUpEvent>(x =>
        {
            if (x.button == 0)
            {
                if (!EditorApplication.isPlaying)
                {
                    ((HeadlessMovieCapture)target).capture = true;
                }
                rootElement.Q<Button>("btnCapture").text = "STOP";
                rootElement.Q<Button>("btnCapture").EnableInClassList("capture", false);
                rootElement.Q<Button>("btnCapture").EnableInClassList("recording", true);
                EditorApplication.isPlaying = !EditorApplication.isPlaying;
            }
        });

        rootElement.Q<Toggle>("tglCapture").bindingPath = nameof(HeadlessMovieCapture.capture);

        rootElement.Q<Toggle>("tglCapture").RegisterValueChangedCallback(x =>
        {
            CheckCaptureState(x.newValue);
            rootElement.schedule.Execute(UpdateTopMessages);
        });

        // General Options
        rootElement.Q<Toggle>("tglFlip").bindingPath = nameof(HeadlessMovieCapture.flipResult);
        rootElement.Q<Toggle>("tglAsync").bindingPath = nameof(HeadlessMovieCapture.useAsyncGPUCalls);
        rootElement.Q<TextField>("txtTokenFormat").bindingPath = nameof(HeadlessMovieCapture.tokenDateFormat);
        // Capture
        rootElement.Q<TextField>("txtFPS").bindingPath = nameof(HeadlessMovieCapture.recordingFrameRate);
        rootElement.Q<Toggle>("tglRealtime").bindingPath = nameof(HeadlessMovieCapture.realtime);
        rootElement.Q<Toggle>("tglAudio").bindingPath = nameof(HeadlessMovieCapture.useAudio);
        // Encoding
        rootElement.Q<EnumField>("cmbFormat").bindingPath = nameof(HeadlessMovieCapture.outputFormat);
        rootElement.Q<EnumField>("cmbEncoderPreset").bindingPath = nameof(HeadlessMovieCapture.encodingPreset);
        rootElement.Q<TextField>("txtVideoBitrate").bindingPath = nameof(HeadlessMovieCapture.videoBitrate);
        rootElement.Q<TextField>("txtAudioBitrate").bindingPath = nameof(HeadlessMovieCapture.audioBitrate);
        // Output
        rootElement.Q<TextField>("txtOutputFolder").bindingPath = nameof(HeadlessMovieCapture.outputFolder);
        rootElement.Q<TextField>("txtOutputFolder").RegisterValueChangedCallback(_ => rootElement.schedule.Execute(UpdateTopMessages));
        rootElement.Q<TextField>("txtOutputFile").bindingPath = nameof(HeadlessMovieCapture.outputFile);
        rootElement.Q<TextField>("txtOutputFile").RegisterValueChangedCallback(_ => rootElement.schedule.Execute(UpdateTopMessages));
        rootElement.Q<Toggle>("tglOpenOutput").bindingPath = nameof(HeadlessMovieCapture.openOutputFolder);
        // Streaming
        rootElement.Q<Toggle>("tglStreaming").bindingPath = nameof(HeadlessMovieCapture.streaming);
        rootElement.Q<Toggle>("tglStreaming").RegisterValueChangedCallback(_ => rootElement.schedule.Execute(UpdateTopMessages));
        rootElement.Q<TextField>("txtKeyframeInterval").bindingPath = nameof(HeadlessMovieCapture.keyframeInterval);
        rootElement.Q<TextField>("txtStreamingURL").bindingPath = nameof(HeadlessMovieCapture.streamingAddress);
        rootElement.Q<TextField>("txtStreamingURL").RegisterValueChangedCallback(_ => rootElement.schedule.Execute(UpdateTopMessages));
        // Experimental
        rootElement.Q<Toggle>("tglUnityRecorder").bindingPath = nameof(HeadlessMovieCapture.useUnityRecorderTiming);
        rootElement.Q<Toggle>("tglUnityScreenshot").bindingPath = nameof(HeadlessMovieCapture.useUnityScreenshotAPI);
        rootElement.Q<ObjectField>("tmlTimeline").bindingPath = nameof(HeadlessMovieCapture.timeline);
        // Logging
        rootElement.Q<Toggle>("tglShowFPS").bindingPath = nameof(HeadlessMovieCapture.showFPS);
        rootElement.Q<Toggle>("tglShowMessages").bindingPath = nameof(HeadlessMovieCapture.log);
        rootElement.Q<Toggle>("tglLogFile").bindingPath = nameof(HeadlessMovieCapture.logNativeToFile);

        // Presets
        var options = GetOptionsPresets();
        var captureGroup = rootElement.Q("ui-capture-group");
        VisualElement presetElement;
        if (options.Count > 0)
        {
            var labels = options.ConvertAll(x => x.name);
            var selectedId = labels.IndexOf(movieCapture.preset);
            if (selectedId >= labels.Count || selectedId < 0)
            {
                // Apply first case
                selectedId = 0;
                options[selectedId].GetAction()(serializedObject);
                serializedObject.FindProperty(nameof(HeadlessMovieCapture.preset)).stringValue = options[selectedId].name;
                serializedObject.ApplyModifiedProperties();
            }
            var presets = new PopupField<string>(labels, selectedId)
            {
                label = "Presets"
            };
            presets.bindingPath = nameof(HeadlessMovieCapture.preset);
            presets.RegisterValueChangedCallback((ev) =>
            {
                var newId = labels.IndexOf(ev.newValue);
                if (newId != -1)
                {
                    options[newId].GetAction()(serializedObject);
                    serializedObject.ApplyModifiedProperties();
                }
            });
            presetElement = presets;
        }
        else
        {
            presetElement = new Label("Missing or invalid presets.json.");
            presetElement.style.color = Color.red;
            presetElement.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
        captureGroup.parent.RemoveAt(1);
        captureGroup.parent.Insert(1, presetElement);

        return rootElement;
    }

    private void CheckCaptureState(bool captureOn)
    {
        if (EditorApplication.isPlaying && captureOn)
        {
            rootElement.Q<Button>("btnCapture").text = "STOP";
            rootElement.Q<Button>("btnCapture").EnableInClassList("capture", false);
            rootElement.Q<Button>("btnCapture").EnableInClassList("recording", true);
        }
        else
        {
            rootElement.Q<Button>("btnCapture").text = "CAPTURE";
            rootElement.Q<Button>("btnCapture").EnableInClassList("capture", true);
            rootElement.Q<Button>("btnCapture").EnableInClassList("recording", false);
        }
    }

    private void UpdateTopMessages()
    {
        var messageArea = rootElement.Q<VisualElement>("ui-message-area");
        messageArea.Clear();
        var messages = GetMessages();
        messageArea.Add(messages);
    }

    private VisualElement GetMessages()
    {
        var labels = new VisualElement();
        labels.style.fontSize = 10;
        labels.style.flexWrap = Wrap.Wrap;
        labels.style.flexDirection = FlexDirection.Column;

        var movieCapture = (HeadlessMovieCapture)target;
        var camera = movieCapture.GetComponent<Camera>();
        var width = camera.pixelWidth;
        var height = camera.pixelHeight;

        Label child = new Label($"Rendering Path: {camera.actualRenderingPath}");
        child.style.color = Color.white;
        labels.Add(child);

        Component hd;
        if (hd = camera.GetComponent("HDAdditionalCameraData"))
        {
            if (hd.GetType().FullName.Contains("Experimental"))
            {
                child = new Label("Old HDRP version detected");
                child.style.color = Color.yellow;
            }
            else
            {
                child = new Label("HDRP version detected");
                child.style.color = Color.white;
            }
            labels.Add(child);
        }

#if HEADLESS_MOVIE_CAPTURE_URP
        child = new Label("URP version detected");
        child.style.color = Color.white;
        labels.Add(child);
#endif
        int errors = 0;
        int errorsCount = 3;

        child = new Label($"Resolution: {width}x{height}");
        child.style.color = Color.white;
        labels.Add(child);

        if (IsPowerOfTwo(width))
        {
            child = new Label($"ERROR: Width is not a power of two ({width}).");
            child.style.color = Color.red;
            labels.Add(child);
            errorsCount--;
            errors |= 1;
        }
        if (IsPowerOfTwo(height))
        {
            child = new Label($"ERROR: Height is not a power of two ({height}).");
            child.style.color = Color.red;
            labels.Add(child);
            errorsCount--;
            errors |= 1;
        }

        if (movieCapture.outputFormat == FFmpegOutput.Custom && !movieCapture.extraOptions.Contains("-c:v"))
        {
            child = new Label("ERROR: Output format is Custom and there is no '-c:v codec' option. This may cause problems.");
            child.style.color = Color.red;
            labels.Add(child);
            errorsCount--;
            errors |= 1;
        }

        if (movieCapture.streaming && !movieCapture.extraOptions.Contains("-f"))
        {
            child = new Label("WARNING: Streaming is enabled and there is no '-f format' option. This may cause problems.");
            child.style.color = Color.yellow;
            labels.Add(child);
            errorsCount--;
            errors |= 2;
        }

        if (movieCapture.capture)
        {
            if (string.IsNullOrEmpty(movieCapture.outputFile))
            {
                child = new Label("WARNING: Save is on, and there is no output path set. Files will be save on project root.");
                child.style.color = Color.yellow;
                labels.Add(child);
                errorsCount--;
                errors |= 2;
            }
            else
            {
                var outputPath = movieCapture.GetOutputPath(true);
                string path;
                if (movieCapture.streaming)
                {
                    path = outputPath;
                }
                else
                {
                    var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputPath + ".mp4"));
                    path = Path.IsPathRooted(outputPath)
                        ? outputPath
                        : output.Replace(Path.DirectorySeparatorChar, '/');
                }
                child = new Label($"Output Path: \"{path}\"");
                child.style.flexWrap = Wrap.Wrap;
                child.style.color = Color.white;
                labels.Add(child);
            }
        }
        else
        {
            child = new Label("Output Path: NOT CAPTURING");
            child.style.color = Color.white;
            labels.Add(child);
        }

        for(var i = 0; i < errorsCount; i++)
        {
            child = new Label(" ");
            labels.Add(child);
        }

        var layout = new VisualElement();
        layout.style.flexDirection = FlexDirection.Row;
        child = new Label("STATUS: ");
        child.style.color = Color.white;
        layout.Add(child);
        if ((errors & 1) == 1)
        {
            child = new Label("There are errors. Please check them.");
            child.style.color = Color.red;
            layout.Add(child);
        }
        if ((errors & 2) == 2)
        {
            child = new Label("There are warnings. Please check them.");
            child.style.color = Color.yellow;
            layout.Add(child);
        }
        if (errors == 0)
        {
            child = new Label("OK");
            child.style.color = Color.green;
            layout.Add(child);
        }
        labels.Insert(0, layout);

        return labels;
    }

    private List<OptionPreset> GetOptionsPresets()
    {
        TextAsset json = Resources.Load<TextAsset>("presets");
        if (json == null)
        {
            return new List<OptionPreset>();
        }
        var options = JsonUtility.FromJson<OptionsList>(json.text);
        return options.presets;
    }
}