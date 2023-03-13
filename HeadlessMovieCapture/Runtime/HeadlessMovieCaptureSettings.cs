// Hack to capture on PostProcessingStack and HDRP
#if HEADLESS_POST_PROCESSING_STACK
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.PostProcessing;

namespace HeadlessStudio
{
    [Serializable]
    [PostProcess(typeof(HMCRenderer), PostProcessEvent.AfterStack, "Headless Movie Capture")]
    public sealed class HeadlessMovieCaptureSettings : PostProcessEffectSettings
    {
    }

    public class HMCRenderer : PostProcessEffectRenderer<HeadlessMovieCaptureSettings>
    {
        private IntPtr GetSessionContext(Camera camera)
        {
            var hmc = camera.GetComponent<HeadlessMovieCapture>();
            return hmc ? hmc.GetNativeSession() : IntPtr.Zero;
        }

        private HeadlessMovieCapture GetBaseComponent(Camera camera)
        {
            var hmc = camera.GetComponent<HeadlessMovieCapture>();
            return hmc;
        }

        public override void Render(PostProcessRenderContext context)
        {
            var cam = context.camera;
            var validSession = GetSessionContext(context.camera);
            if (cam == null || !Application.isPlaying || validSession == IntPtr.Zero)
            {
                context.command.BlitFullscreenTriangle(context.source, context.destination);
                return;
            }

            var hmc = GetBaseComponent(cam);
            var destination = RenderTexture.GetTemporary(cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGB32);
            context.command.BlitFullscreenTriangle(context.source, destination);
            hmc.Capture(destination);
            RenderTexture.ReleaseTemporary(destination);

            // Show finale result    
            context.command.BlitFullscreenTriangle(context.source, context.destination);

        }

    }
}
#endif