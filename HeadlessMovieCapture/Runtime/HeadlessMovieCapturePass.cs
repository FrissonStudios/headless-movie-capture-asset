#if HEADLESS_MOVIE_CAPTURE_URP
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HeadlessStudio
{
    [Serializable]
    public class HeadlessMovieCapturePass : ScriptableRenderPass
    {
        private IntPtr _validSession;
        private HeadlessMovieCapture _hmc;
        private Camera _currentCamera;
        private RenderTexture _mainHandle;
        internal RenderTargetIdentifier source;
        internal Material blitMaterial;

        private IntPtr GetSessionContext(Camera camera)
        {
            var hmc = camera.GetComponent<HeadlessMovieCapture>();
            return hmc ? hmc.GetNativeSession() : IntPtr.Zero;
        }

        private HeadlessMovieCapture GetBaseComponent(Camera camera)
        {
            return camera.GetComponent<HeadlessMovieCapture>();
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_currentCamera != renderingData.cameraData.camera || _validSession == IntPtr.Zero)
            {
                _currentCamera = renderingData.cameraData.camera;
                _validSession = GetSessionContext(_currentCamera);

                _hmc = GetBaseComponent(_currentCamera);
            }
            if (_currentCamera == null || _validSession == IntPtr.Zero)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("HeadlessStudioPass");
            var desc = new RenderTextureDescriptor(_currentCamera.pixelWidth, _currentCamera.pixelHeight, RenderTextureFormat.ARGB32, 0, 0);
            if (_mainHandle == null)
            {
                _mainHandle = RenderTexture.GetTemporary(desc);
            }
            cmd.Blit(source, _mainHandle);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            desc.sRGB = true;
            var rt = RenderTexture.GetTemporary(desc);
            Graphics.Blit(_mainHandle, rt, blitMaterial, 0);

            _hmc.Capture(rt);
            RenderTexture.ReleaseTemporary(rt);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            RenderTexture.ReleaseTemporary(_mainHandle);
            _mainHandle = null;
        }
    }
}
#endif