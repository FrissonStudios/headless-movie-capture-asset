#if HEADLESS_MOVIE_CAPTURE_URP
using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HeadlessStudio
{
    [Serializable]
    public class HeadlessMovieCaptureFeature : ScriptableRendererFeature
    {
        private HeadlessMovieCapturePass _hmcPass;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var src = renderer.cameraColorTarget;
            if (_hmcPass.blitMaterial == null)
            {
                var shader = Shader.Find("Hidden/HeadlessMovieCapture/Preprocess");
                _hmcPass.blitMaterial = new Material(shader);
            }

            _hmcPass.source = src;
            _hmcPass.renderPassEvent = RenderPassEvent.AfterRendering;
            renderer.EnqueuePass(_hmcPass);
        }

        public override void Create()
        {
            _hmcPass = new HeadlessMovieCapturePass();
        }
    }
}
#endif