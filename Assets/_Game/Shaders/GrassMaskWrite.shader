// Hidden shader used by GrassField CommandBuffer to write cut areas to the R8 RenderTexture.
// Min-blend: already-black pixels stay black (never "uncut" again).
// Receives: circle (IsCircle=1) or capsule (IsCircle=0).
Shader "Hidden/Fields/GrassMaskWrite"
{
    Properties
    {
        _MainTex ("Source RT", 2D) = "white" {}
        _Center  ("Center (xy=from, zw=to)", Vector) = (0.5, 0.5, 0.5, 0.5)
        _Radius  ("Radius (UV space)", Float) = 0.05
        _IsCircle("Is Circle (1=yes, 0=capsule)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Center;
            float  _Radius;
            float  _IsCircle;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f    { float4 pos : SV_POSITION;  float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            // Signed distance to a line segment (all in UV space)
            float sdSegment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float  h  = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            float4 frag(v2f i) : SV_Target
            {
                float existing = tex2D(_MainTex, i.uv).r;

                float dist;
                if (_IsCircle > 0.5)
                    dist = length(i.uv - _Center.xy);
                else
                    dist = sdSegment(i.uv, _Center.xy, _Center.zw);

                // If inside radius → write 0 (cut), else keep existing value.
                // min() ensures we never "uncut" a cell.
                float written = dist < _Radius ? 0.0 : 1.0;
                return min(existing, written);
            }
            ENDHLSL
        }
    }
}
