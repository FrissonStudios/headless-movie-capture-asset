using System;
using UnityEngine;
using UnityEngine.Profiling;
#if HEADLESS_MOVIE_CAPTURE_HDRP
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
#endif

namespace HeadlessStudio
{
#if HEADLESS_MOVIE_CAPTURE_HDRP
    [Serializable, VolumeComponentMenu("HeadlessStudio/Movie Capture")]
    public sealed class HeadlessMovieCapturePostProcess : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        private RTHandle _mainHandle;
        private Material _material;
        private Camera _currentCamera;
        private IntPtr _validSession;
        private HeadlessMovieCapture _hmc;

        private IntPtr GetSessionContext(Camera camera)
        {
            var hmc = camera.GetComponent<HeadlessMovieCapture>();
            return hmc ? hmc.GetNativeSession() : IntPtr.Zero;
        }

        private HeadlessMovieCapture GetBaseComponent(Camera camera)
        {
            return camera.GetComponent<HeadlessMovieCapture>();
        }

        public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

        public override void Setup()
        {
            _material = new Material(Shader.Find("Hidden/HeadlessMovieCapture/PreprocessHD"));
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            HDUtils.BlitCameraTexture(cmd, source, destination);

            if (!Application.isPlaying)
            {
                return;
            }

            if (_currentCamera != camera.camera || _validSession == IntPtr.Zero)
            {
                _currentCamera = camera.camera;
                _validSession = GetSessionContext(_currentCamera);

                _hmc = GetBaseComponent(_currentCamera);
            }
            if (_currentCamera == null || _validSession == IntPtr.Zero)
            {
                return;
            }

            Profiler.BeginSample("HeadlessMovieCapturePostProcess.Render");

            if (_mainHandle != null && _currentCamera.pixelWidth != _mainHandle.rt.width && _currentCamera.pixelHeight != _mainHandle.rt.height)
            {
                RTHandles.Release(_mainHandle);
            }

            if(_mainHandle == null)
            {
                _mainHandle = RTHandles.Alloc(_currentCamera.pixelWidth, _currentCamera.pixelHeight);
            }

            _material.SetTexture("_InputTexture", source);
            HDUtils.DrawFullScreen(cmd, _material, _mainHandle);
            _hmc.Capture(_mainHandle);
            Profiler.EndSample();
        }

        public override void Cleanup()
        {
            CoreUtils.Destroy(_material);
            RTHandles.Release(_mainHandle);
        }

        public bool IsActive() => true;
    }
#endif
}
