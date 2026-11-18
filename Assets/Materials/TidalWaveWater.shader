Shader "Custom/TidalWaveWater"
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

        [Header(Lighting and Depth)]
        _LightDir ("Fake Light Direction (world)", Vector) = (0.4, 0.8, 0.3, 0)
        _NormalJitter ("Surface Normal Ripple", Range(0.0, 0.8)) = 0.3
        _SpecPower ("Glint Power", Range(8, 256)) = 64
        _SpecIntensity ("Glint Intensity", Range(0.0, 2.0)) = 0.8
        _InteriorDarken ("Interior Depth Darken", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        CGINCLUDE
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
        float4 _LightDir;
        float _NormalJitter;
        float _SpecPower;
        float _SpecIntensity;
        float _InteriorDarken;

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

            // Animate organic ripples across the wave surface.
            float2 waveCoord = v.uv * _NoiseScale + (_Time.y * _WaveSpeed.xy);
            float disp = (noise2D(waveCoord) - 0.5) * 2.0 * _RippleStrength;

            // Keep the base grounded (uv.y == 0).
            pos += v.normal * (disp * smoothstep(0.0, 0.2, v.uv.y));

            o.uv = v.uv;
            o.pos = UnityObjectToClipPos(float4(pos, 1.0));
            o.worldPos = mul(unity_ObjectToWorld, float4(pos, 1.0)).xyz;
            o.worldNormal = UnityObjectToWorldNormal(v.normal);
            return o;
        }

        // backFace = 1 when drawing the far (interior) side of the wave volume.
        fixed4 shade(v2f i, float backFace)
        {
            float t = _Time.y;
            float2 uv1 = i.uv * float2(_NoiseScale * 1.5, _NoiseScale) + (t * _WaveSpeed.xy);
            float2 uv2 = i.uv * float2(_NoiseScale * 2.2, _NoiseScale * 1.4) - (t * _WaveSpeed.xy * 0.65);

            float n1 = fbm(uv1);
            float n2 = fbm(uv2);
            float combinedWater = (n1 + n2) * 0.5;

            // Vertical ramp: deep blue at the base, bright cyan at the crest.
            float4 col = lerp(_DeepColor, _ShallowColor, pow(saturate(i.uv.y), 0.8));

            // Normal (flipped on the interior pass) with stylised ripples.
            float3 N = normalize(i.worldNormal);
            N = backFace > 0.5 ? -N : N;
            N = normalize(N + (float3(n1, n2, n1 - n2) - 0.5) * _NormalJitter);
            float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
            float3 L = normalize(_LightDir.xyz);

            // Wrapped diffuse gives the wave its form.
            float ndl = dot(N, L) * 0.5 + 0.5;
            col.rgb *= lerp(0.55, 1.2, ndl);

            // Light shining through the thin crest.
            col.rgb += _ShallowColor.rgb * pow(saturate(i.uv.y), 3.0) * 0.25;

            // Fresnel edge glow.
            float fresnel = pow(1.0 - saturate(abs(dot(N, V))), _FresnelPower);
            col.rgb = lerp(col.rgb, _ShallowColor.rgb * 1.35, fresnel * 0.65);

            // Interior reads as deeper, denser water.
            col.rgb = lerp(col.rgb, _DeepColor.rgb, backFace * _InteriorDarken);
            col.a = saturate(col.a + backFace * 0.1);

            // Foam on the crest lip, base surge and ridges (calmer on the interior).
            float crestFoam = smoothstep(0.80, 0.98, i.uv.y);
            float baseFoam = (1.0 - smoothstep(0.0, 0.14, i.uv.y)) * 0.65;
            float ridgeFoam = smoothstep(_FoamThreshold, _FoamThreshold + _FoamSmoothness,
                                         combinedWater + (i.uv.y * 0.25));
            float totalFoam = saturate(crestFoam + baseFoam + ridgeFoam) * lerp(1.0, 0.5, backFace);
            col = lerp(col, _FoamColor, totalFoam);

            // Specular glints on the water, not on foam.
            float spec = pow(saturate(dot(N, normalize(L + V))), _SpecPower) * _SpecIntensity;
            col.rgb += spec * (1.0 - totalFoam) * (1.0 - backFace * 0.7);

            // Feather the left/right ends so there are no harsh borders.
            float sideFeather = smoothstep(0.0, 0.08, i.uv.x) * smoothstep(1.0, 0.92, i.uv.x);
            col.a *= lerp(0.25, 1.0, sideFeather);

            return fixed4(col);
        }

        fixed4 fragFront(v2f i) : SV_Target { return shade(i, 0.0); }
        fixed4 fragBack(v2f i) : SV_Target { return shade(i, 1.0); }
        ENDCG

        // Pass 1: far side of the volume (interior), drawn first.
        Pass
        {
            Name "Interior"
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBack
            ENDCG
        }

        // Pass 2: near side of the volume (surface), drawn on top.
        Pass
        {
            Name "Surface"
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragFront
            ENDCG
        }
    }
}
