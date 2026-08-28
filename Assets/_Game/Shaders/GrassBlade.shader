// Stylized grass blade shader for URP.
// Vertex stage: reads GrassMask RT via UV2 to collapse blade height.
//   Mask 1.0 = full height | 0.0 = stubble (~5% height, yellowed)
// Fragment: cartoon-stylized colour, no PBR.
Shader "Fields/GrassBlade"
{
    Properties
    {
        _MainTex     ("Grass Billboard", 2D)    = "white" {}
        _Cutoff      ("Alpha Cutoff", Range(0,1)) = 0.35
        _GrassMask   ("Grass Mask RT", 2D)     = "white" {}
        _GrassTop    ("Grass Top Colour", Color)   = (0.42, 0.72, 0.22, 1)
        _GrassBottom ("Grass Bottom Colour", Color) = (0.28, 0.50, 0.14, 1)
        _StubbleColor("Stubble Colour", Color)      = (0.72, 0.68, 0.32, 1)
        _BladeHeight ("Blade Height", Float)    = 0.35
        _StubbleFraction("Stubble Height Fraction", Range(0.01, 0.15)) = 0.05
        _WindStrength("Wind Strength", Float)   = 0.04
        _WindSpeed   ("Wind Speed", Float)      = 1.3
        _TrampleRadius  ("Trample Radius", Float)        = 1.8
        _TrampleStrength("Trample Strength", Float)      = 1.2
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        AlphaToMask On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ COLORBLIND_DEUTERANOPIA COLORBLIND_PROTANOPIA COLORBLIND_TRITANOPIA
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_GrassMask);
            SAMPLER(sampler_GrassMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _GrassTop;
                float4 _GrassBottom;
                float4 _StubbleColor;
                float  _BladeHeight;
                float  _StubbleFraction;
                float  _WindStrength;
                float  _WindSpeed;
                float  _Cutoff;
                float  _TrampleRadius;
                float  _TrampleStrength;
            CBUFFER_END

            // Global uniform set each frame by PlayerController
            float3 _PlayerFeetWS;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;  // blade-local UV (v=height fraction 0=base 1=tip)
                float2 fieldUV    : TEXCOORD1;  // per-blade field UV for mask
                float2 groundYUV  : TEXCOORD2;  // x = terrain ground height in chunk local space
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float2 fieldUV     : TEXCOORD1;
                float  heightFrac  : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Sample mask in vertex stage (LOD 0 avoids gradient issues)
                float maskVal = SAMPLE_TEXTURE2D_LOD(_GrassMask, sampler_GrassMask,
                                                     IN.fieldUV, 0).r;

                // Collapse height: uncut=1→full, cut=0→stubble fraction
                float heightScale = lerp(_StubbleFraction, 1.0, maskVal);

                float3 pos = IN.positionOS.xyz;
                float  tipFrac = IN.uv.y; // 0=base, 1=tip

                // pos.y is ground-relative (0=base, bladeHeight=tip); add groundY after scaling
                // so heightScale only compresses the blade above ground, not the ground offset itself
                float groundY = IN.groundYUV.x;
                pos.y = groundY + pos.y * heightScale;

                // Wind — tip sways, base anchored
                float windOffset = sin(_Time.y * _WindSpeed + pos.x * 3.1 + pos.z * 2.7)
                                   * _WindStrength * tipFrac;
                pos.x += windOffset;

                // Trampling: bend uncut blade tips away from the player's feet.
                // Base stays anchored (tipFrac=0), tip bends outward (tipFrac=1).
                // Spring-back is free — as player moves away dist grows and falloff→0.
                float3 bladeBaseWS = TransformObjectToWorld(float3(IN.positionOS.x, groundY, IN.positionOS.z));
                float2 trampleDiff = bladeBaseWS.xz - _PlayerFeetWS.xz;
                float  trampleDist = length(trampleDiff);
                float  trampleFalloff = saturate(1.0 - trampleDist / max(_TrampleRadius, 0.001));
                trampleFalloff = trampleFalloff * trampleFalloff; // quadratic — sharp near feet, soft at edge
                float2 pushDir = trampleDist > 0.001 ? (trampleDiff / trampleDist) : float2(1.0, 0.0);
                float  bendAmount = _TrampleStrength * trampleFalloff * tipFrac * maskVal;
                pos.x += pushDir.x * bendAmount;
                pos.z += pushDir.y * bendAmount;

                OUT.positionHCS = TransformObjectToHClip(pos);
                OUT.uv          = IN.uv;
                OUT.fieldUV     = IN.fieldUV;
                OUT.heightFrac  = heightScale;
                OUT.fogFactor   = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Billboard texture — alpha clips the grass silhouette shape
                half4 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(texSample.a - _Cutoff);

                // Blade colour: gradient from bottom to top
                half4 grassCol = lerp(_GrassBottom, _GrassTop, IN.uv.y);

                // Stubble: shift colour toward stubble colour when collapsed
                float stubbleFrac = 1.0 - saturate((IN.heightFrac - _StubbleFraction)
                                         / (1.0 - _StubbleFraction));
                half4 col = lerp(grassCol, _StubbleColor, stubbleFrac);

                // Modulate with texture grayscale for natural shape detail; fade on stubble
                col.rgb *= lerp(texSample.rgb, half3(1.0, 1.0, 1.0), stubbleFrac * 0.6);

                // Flat cartoon shading — no normal maps, just a small ambient + directional
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(float3(0, 1, 0), mainLight.direction) * 0.5 + 0.6);
                col.rgb *= NdotL * mainLight.color;

                col.rgb = MixFog(col.rgb, IN.fogFactor);

                // Colorblind compensation — shift hue of stubble to visually separate it from grass
#if defined(COLORBLIND_DEUTERANOPIA) || defined(COLORBLIND_PROTANOPIA)
                // Add blue tint proportional to stubble amount so it reads distinct from green
                col.b = saturate(col.b + stubbleFrac * 0.4);
#elif defined(COLORBLIND_TRITANOPIA)
                // Boost luminance difference instead of hue shift
                col.rgb = lerp(col.rgb, half3(0.9, 0.9, 0.9) * dot(col.rgb, half3(0.299, 0.587, 0.114)), stubbleFrac * 0.5);
#endif

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
