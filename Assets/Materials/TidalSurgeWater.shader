Shader "Custom/TidalSurgeWater"
{
    Properties
    {
        [Header(Colors)]
        _DeepColor ("Deep Water Color", Color) = (0.02, 0.32, 0.65, 0.92)
        _ShallowColor ("Shallow / Crest Color", Color) = (0.12, 0.78, 0.98, 0.96)
        _FoamColor ("Foam & Highlight Color", Color) = (0.92, 0.99, 1.0, 1.0)
        _FresnelPower ("Fresnel Rim Power", Range(0.5, 8.0)) = 2.2

        [Header(Surface Motion and Ripples)]
        _WaveSpeed ("Flow Speed (X, Y)", Vector) = (0.2, -2.5, 0, 0)
        _NoiseScale ("Ripple Scale", Float) = 4.5
        _RippleStrength ("3D Vertex Ripple", Range(0.0, 0.4)) = 0.14
        _FoamThreshold ("Foam Threshold", Range(0.0, 1.0)) = 0.56
        _FoamSmoothness ("Foam Softness", Range(0.01, 0.5)) = 0.08
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos         : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 worldPos    : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
            };

            fixed4 _DeepColor;
            fixed4 _ShallowColor;
            fixed4 _FoamColor;
            float _FresnelPower;
            float4 _WaveSpeed;
            float _NoiseScale;
            float _RippleStrength;
            float _FoamThreshold;
            float _FoamSmoothness;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += amp * noise2D(p);
                    p *= 2.03;
                    amp *= 0.5;
                }
                return v;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 pos = v.vertex.xyz;

                // Animate organic ripples across the curved wave surface
                float2 waveCoord = v.uv * _NoiseScale + (_Time.y * _WaveSpeed.xy);
                float disp = (noise2D(waveCoord) - 0.5) * 2.0 * _RippleStrength;
                
                // Keep bottom edge grounded (v.uv.y == 0)
                pos += v.normal * (disp * smoothstep(0.0, 0.2, v.uv.y));

                o.uv = v.uv;
                o.pos = UnityObjectToClipPos(float4(pos, 1.0));
                o.worldPos = mul(unity_ObjectToWorld, float4(pos, 1.0)).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Dual scrolling water caustic layers mapped cleanly along the wave UVs
                float2 uv1 = i.uv * float2(_NoiseScale * 1.5, _NoiseScale) + (_Time.y * _WaveSpeed.xy);
                float2 uv2 = i.uv * float2(_NoiseScale * 2.2, _NoiseScale * 1.4) - (_Time.y * _WaveSpeed.xy * 0.65);

                float combinedWater = (fbm(uv1) + fbm(uv2)) * 0.5;

                // Vertical color ramp: deep ocean blue at base -> bright cyan at the crest
                fixed4 baseWater = lerp(_DeepColor, _ShallowColor, pow(i.uv.y, 0.8));

                // Fresnel edge glow
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fresnel = pow(1.0 - abs(dot(normalize(i.worldNormal), viewDir)), _FresnelPower);
                baseWater.rgb = lerp(baseWater.rgb, _ShallowColor.rgb * 1.35, fresnel * 0.65);

                // Churning white foam on top crest lip, base surge, and wave ridges
                float crestFoam = smoothstep(0.80, 0.98, i.uv.y);
                float baseFoam = (1.0 - smoothstep(0.0, 0.14, i.uv.y)) * 0.65;
                float ridgeFoam = smoothstep(_FoamThreshold, _FoamThreshold + _FoamSmoothness, combinedWater + (i.uv.y * 0.25));

                float totalFoam = saturate(crestFoam + baseFoam + ridgeFoam);
                fixed4 finalColor = lerp(baseWater, _FoamColor, totalFoam);

                // Softly feather the far left and right edges so there are no harsh polygon borders
                float sideFeather = smoothstep(0.0, 0.08, i.uv.x) * smoothstep(1.0, 0.92, i.uv.x);
                finalColor.a *= lerp(0.25, 1.0, sideFeather);

                return finalColor;
            }
            ENDCG
        }
    }
}