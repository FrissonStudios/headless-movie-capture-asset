using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HeadlessStudio.HeadlessMovieCapture))]
public class SimpleMovieCaptureControl : MonoBehaviour
{
    private HeadlessStudio.HeadlessMovieCapture _hmc;
    public bool captureComponentActive;

    void OnEnable()
    {
        // Setup frame capture (we disable the component so it deinits)
        // We also disable capture because we don't wont to start recording right away.
        _hmc = GetComponent<HeadlessStudio.HeadlessMovieCapture>();
        _hmc.enabled = false;
        _hmc.capture = false;
        _hmc.enabled = captureComponentActive;
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyUp(KeyCode.R))
        {
            // Control using the component enabled and using the StartRecording and StopRecording methods.
            if (captureComponentActive)
            {
                if (_hmc.IsCapturing)
                {
                    Debug.Log("Stop Recording");
                    _hmc.StopRecording();
                }
                else
                {
                    Debug.Log("Start Recording");
                    _hmc.StartRecording();
                }
            }
            // Control using the component enabled state. The "capture" field order in the enabled and disable is important.
            else
            {
                if (_hmc.IsCapturing)
                {
                    Debug.Log("Stop Recording");
                    _hmc.enabled = false;
                    _hmc.capture = false;
                }
                else
                {
                    // We must first set the capture state to enabled, so the component inits.
                    Debug.Log("Start Recording");
                    _hmc.capture = true;
                    _hmc.enabled = true;
                }
            }
        }

        if(Input.GetKeyUp(KeyCode.T))
        {
            if(Time.captureFramerate == 0)
            {
                Time.captureFramerate = _hmc.recordingFrameRate;
            }
            else
            {
                Time.captureFramerate = 0;
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("");
        GUILayout.Label("Press R to Start/Stop recording.");
        GUILayout.Label("Press T to Disable/Enable realtime frame capture.");
        if (Time.captureFramerate == 0)
        {
            GUILayout.Label("Realtime capture");
        }
    }
}
