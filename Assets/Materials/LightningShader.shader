Shader "Olympus/ProceduralLightning"
{
    Properties
    {
        [HDR] _BoltColour("Bolt Colour", Color) = (0, 0.5, 1, 1)
        _ScrollSpeed("Energy Speed", Float) = 5.0
        _Complexity("Complexity", Range(1, 10)) = 5.0
        _Thickness("Thickness", Range(0, 1)) = 0.4
        _Jitter("Jitter Amount", Float) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _BoltColour;
            float _ScrollSpeed;
            float _Complexity;
            float _Thickness;
            float _Jitter;

            // Simple hash for noise
            float hash(float n) { return frac(sin(n) * 43758.5453); }

            // 1D Noise function for procedural energy
            float noise(float x) {
                float i = floor(x);
                float f = frac(x);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(hash(i), hash(i + 1.0), u);
            }

            v2f vert (appdata v) {
                v2f o;
                // Add "Heroic Jitter" to vertices
                float jitter = (hash(v.uv.y + _Time.y) - 0.5) * _Jitter;
                v.vertex.x += jitter;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // Procedural Scrolling Noise
                float val = noise(i.uv.y * _Complexity - _Time.y * _ScrollSpeed);
                
                // Sharpen the noise into a bolt shape
                float alpha = smoothstep(_Thickness, _Thickness + 0.1, val);

                return _BoltColour * alpha;
            }
            ENDHLSL
        }
    }
}