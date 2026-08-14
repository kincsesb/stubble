Shader "Hidden/HeatDistortion"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    TEXTURE2D_X(_CameraDepthTexture);
    SAMPLER(sampler_CameraDepthTexture);
    // sampler_LinearClamp is declared in Core.hlsl

    float _HeatStartDistance;
    float _HeatMaxDistance;
    float _HeatBlurRadius;    // max blur radius in pixels
    float _HeatShimmerIntensity; // UV distortion shimmer strength (subtle)
    float _HeatNoiseScale;
    float _HeatSpeed;

    // 8-tap Poisson disc for the blur kernel
    static const float2 DISC[8] =
    {
        float2( 0.000,  1.000),
        float2( 0.707,  0.707),
        float2( 1.000,  0.000),
        float2( 0.707, -0.707),
        float2( 0.000, -1.000),
        float2(-0.707, -0.707),
        float2(-1.000,  0.000),
        float2(-0.707,  0.707),
    };

    float ValueNoise(float2 p)
    {
        float2 i = floor(p);
        float2 f = frac(p);
        f = f * f * (3.0 - 2.0 * f);

        float a = frac(sin(dot(i,               float2(127.1, 311.7))) * 43758.5453);
        float b = frac(sin(dot(i + float2(1,0), float2(127.1, 311.7))) * 43758.5453);
        float c = frac(sin(dot(i + float2(0,1), float2(127.1, 311.7))) * 43758.5453);
        float d = frac(sin(dot(i + float2(1,1), float2(127.1, 311.7))) * 43758.5453);

        return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
    }

    half4 HeatFrag(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = input.texcoord;

        // Depth → linear camera-space distance
        float rawDepth    = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
        float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

        // Skip skybox (depth returns huge value when nothing is there)
        if (linearDepth > 4000.0)
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

        // Heat factor: 0 at _HeatStartDistance, 1 at _HeatMaxDistance — ease-in curve
        float range = max(1.0, _HeatMaxDistance - _HeatStartDistance);
        float heatT = saturate((linearDepth - _HeatStartDistance) / range);
        heatT = heatT * heatT;

        // Early out — pixel is too close to be affected
        if (heatT < 0.0001)
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

        float2 pixelSize = 1.0 / _ScreenParams.xy;

        // ── Shimmer: subtle animated UV warp so the blur edge feels alive ──
        float2 baseUV = uv;
        if (_HeatShimmerIntensity > 0.0001)
        {
            float st = _Time.y * _HeatSpeed;
            float2 n1uv = uv * _HeatNoiseScale + float2(0.0,    st * 0.7);
            float2 n2uv = uv * _HeatNoiseScale * 1.6 + float2(st * 0.4, st * 0.2);

            float nx = (ValueNoise(n1uv)                     - 0.5)
                     + (ValueNoise(n2uv + float2(5.3,  9.1)) - 0.5) * 0.4;
            float ny = (ValueNoise(n1uv + float2(17.3, 31.7)) - 0.5)
                     + (ValueNoise(n2uv + float2(2.7,  14.5)) - 0.5) * 0.4;

            float2 shimmerOffset = float2(nx, ny) * heatT * _HeatShimmerIntensity * pixelSize * 200.0;
            baseUV = clamp(uv + shimmerOffset, 0.001, 0.999);
        }

        // ── Blur: Poisson disc, radius in pixels, scales with heatT ──
        float blurPx = heatT * _HeatBlurRadius;

        if (blurPx < 0.5)
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, baseUV);

        float2 blurStep = blurPx * pixelSize;

        half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, baseUV);
        UNITY_UNROLL
        for (int i = 0; i < 8; i++)
        {
            float2 sampleUV = clamp(baseUV + DISC[i] * blurStep, 0.001, 0.999);
            col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV);
        }
        return col / 9.0;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "HeatDistortion"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment HeatFrag
            #pragma target 3.5
            ENDHLSL
        }
    }
}