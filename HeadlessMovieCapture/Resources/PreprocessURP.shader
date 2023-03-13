Shader "Hidden/HeadlessMovieCapture/PreprocessURP"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100

        Pass
        {
            Name "Blit"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
        // Required to compile gles 2.0 with standard srp library
        #pragma prefer_hlslcc gles
        #pragma exclude_renderers d3d11_9x
        #pragma vertex Vertex
        #pragma fragment Fragment

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        struct Attributes
        {
            float4 positionOS   : POSITION;
            float2 uv           : TEXCOORD0;
        };

        struct Varyings
        {
            half4 positionCS    : SV_POSITION;
            half2 uv            : TEXCOORD0;
        };

        TEXTURE2D_X(_SourceTex);
        SAMPLER(sampler_SourceTex);

        Varyings Vertex(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }

        half4 Fragment(Varyings input) : SV_Target
        {
            float2 uv = input.uv;
            uv.y = 1.0 - uv.y;

            half4 col = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_SourceTex, uv);
            // Uncomment the line below if you want to make it darker.
            // For some reason the textures obtain from URP seem to have a strange colorspace.
            //col = SRGBToLinear(col);
            return col;
        }
        ENDHLSL
    }
    }
}
