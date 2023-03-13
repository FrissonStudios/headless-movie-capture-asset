using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace HeadlessStudio
{
    public partial class HeadlessMovieCapture
    {
        private bool _audioRecording;

        public int AudioSampleRate()
        {
            return AudioSettings.outputSampleRate;
        }

        public int AudioChannelCount()
        {
            switch (AudioSettings.speakerMode)
            {
                case AudioSpeakerMode.Mono: return 1;
                case AudioSpeakerMode.Stereo: return 2;
                case AudioSpeakerMode.Quad: return 4;
                case AudioSpeakerMode.Surround: return 5;
                case AudioSpeakerMode.Mode5point1: return 6;
                case AudioSpeakerMode.Mode7point1: return 7;
                case AudioSpeakerMode.Prologic: return 2;
                default: return 1;
            }
        }

        private List<short> _buffer = new List<short>();

        private void StartAudioRecording()
        {
            if (!_audioRecording && useAudio)
            {
                _buffer.Clear();
            }
            _audioRecording = true;
        }

        private void StopAudioRecording()
        {
            if (_audioRecording && useAudio)
            {
                _buffer.Clear();
            }
            _audioRecording = false;
        }


        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (useAudio && data.Length > 0)
            {
                if (_buffer.Count > data.Length * 4) _buffer.Clear();
                _buffer.AddRange(data.Select(x => (short)(x*(float)short.MaxValue)).ToList());
            }
        }

        private void RenderAudio()
        {
            if (useAudio && _audioRecording && _buffer.Count > 0)
            {
                var lastBuffer = _buffer.ToArray();
                var audioBuffer = new NativeArray<short>(lastBuffer, Allocator.Temp);
                HeadlessMovieCaptureNative.SessionUpdateAudio(_session, audioBuffer);
                audioBuffer.Dispose();
                _buffer.Clear();
            }
        }
    }
}
