// Stylized grass blade shader for URP.
// Vertex stage: reads GrassMask RT via UV2 to collapse blade height.
//   Mask 1.0 = full height | 0.0 = stubble (~5% height, yellowed)
// Fragment: cartoon-stylized colour, no PBR.
Shader "Fields/GrassBlade"
{
    Properties
    {
        _GrassMask   ("Grass Mask RT", 2D)     = "white" {}
        _GrassTop    ("Grass Top Colour", Color)   = (0.42, 0.72, 0.22, 1)
        _GrassBottom ("Grass Bottom Colour", Color) = (0.28, 0.50, 0.14, 1)
        _StubbleColor("Stubble Colour", Color)      = (0.72, 0.68, 0.32, 1)
        _BladeHeight ("Blade Height", Float)    = 0.35
        _StubbleFraction("Stubble Height Fraction", Range(0.01, 0.15)) = 0.05
        _WindStrength("Wind Strength", Float)   = 0.04
        _WindSpeed   ("Wind Speed", Float)      = 1.3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GrassMask);
            SAMPLER(sampler_GrassMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _GrassTop;
                float4 _GrassBottom;
                float4 _StubbleColor;
                float  _BladeHeight;
                float  _StubbleFraction;
                float  _WindStrength;
                float  _WindSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;  // blade-local UV (v=height)
                float2 fieldUV    : TEXCOORD1;  // per-blade field UV for mask
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

                // Apply height scale
                pos.y *= heightScale;

                // Wind — tip sways, base anchored
                float windOffset = sin(_Time.y * _WindSpeed + pos.x * 3.1 + pos.z * 2.7)
                                   * _WindStrength * tipFrac;
                pos.x += windOffset;

                OUT.positionHCS = TransformObjectToHClip(pos);
                OUT.uv          = IN.uv;
                OUT.fieldUV     = IN.fieldUV;
                OUT.heightFrac  = heightScale;
                OUT.fogFactor   = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Blade colour: gradient from bottom to top
                half4 grassCol = lerp(_GrassBottom, _GrassTop, IN.uv.y);

                // Stubble: shift colour toward stubble colour when collapsed
                float stubbleFrac = 1.0 - saturate((IN.heightFrac - _StubbleFraction)
                                         / (1.0 - _StubbleFraction));
                half4 col = lerp(grassCol, _StubbleColor, stubbleFrac);

                // Flat cartoon shading — no normal maps, just a small ambient + directional
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(float3(0, 1, 0), mainLight.direction) * 0.5 + 0.6);
                col.rgb *= NdotL * mainLight.color;

                col.rgb = MixFog(col.rgb, IN.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
