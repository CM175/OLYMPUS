Shader "Olympus/TidalWater"
{
    Properties
    {
        _DeepColor    ("Deep Color",    Color) = (0.02, 0.28, 0.55, 0.80)
        _ShallowColor ("Shallow Color", Color) = (0.12, 0.85, 0.95, 0.55)
        _FoamColor    ("Foam Color",    Color) = (0.93, 1.00, 1.00, 1.00)
        _RimColor     ("Rim Color",     Color) = (0.65, 1.00, 1.00, 1.00)
        _RimPower     ("Rim Power",     Range(0.5, 8)) = 2.5
        _Brightness   ("Brightness",    Range(0.5, 3)) = 1.3
        _FoamStart    ("Crest Foam Start (V)", Range(0, 1)) = 0.72
        _FoamSoftness ("Foam Softness", Range(0.01, 0.5)) = 0.15
        _NoiseScale   ("Noise Scale",   Float) = 4
        _FlowSpeed    ("Flow Speed",    Float) = 1.5
        _Wobble       ("Vertex Wobble", Range(0, 0.3)) = 0.05
        _NormalJitter ("Surface Ripple", Range(0, 0.8)) = 0.3
        _SpecPower    ("Glint Power",   Range(8, 256)) = 64
        _SpecIntensity("Glint Intensity", Range(0, 2)) = 0.8
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _FoamColor;
                half4 _RimColor;
                float _RimPower;
                float _Brightness;
                float _FoamStart;
                float _FoamSoftness;
                float _NoiseScale;
                float _FlowSpeed;
                float _Wobble;
                float _NormalJitter;
                float _SpecPower;
                float _SpecIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float FBM(float2 p)
            {
                return VNoise(p) * 0.6 + VNoise(p * 2.1 + 7.3) * 0.4;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                // Gentle surface wobble, strongest near the crest (uv.y = 1).
                float wob = sin(v.positionOS.x * 3.0 + _Time.y * 5.0)
                          * cos(v.positionOS.y * 2.0 + _Time.y * 3.0);
                float3 posOS = v.positionOS.xyz + v.normalOS * wob * _Wobble * v.uv.y;

                VertexPositionInputs p = GetVertexPositionInputs(posOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.uv         = v.uv;
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float t = _Time.y;

                // Upward-rushing streaks + a slower broad noise for foam breakup.
                float n  = FBM(float2(i.uv.x * _NoiseScale * 2.5,
                                      i.uv.y * _NoiseScale - t * _FlowSpeed));
                float n2 = FBM(float2(i.uv.x * _NoiseScale * 1.3 + t * 0.3,
                                      i.uv.y * _NoiseScale * 0.7 - t * _FlowSpeed * 0.6) + 11.7);

                float isFront = IS_FRONT_VFACE(facing, 1.0, -1.0);
                float backness = saturate(-isFront);

                // Deep at the base, bright and translucent toward the crest.
                half4 col = lerp(_DeepColor, _ShallowColor, saturate(i.uv.y + (n - 0.5) * 0.3));

                // Streak highlights.
                col.rgb += smoothstep(0.62, 0.9, n) * _ShallowColor.rgb * 0.35;

                // Normal with stylised ripples; flipped on back faces so lighting is right inside the volume.
                float3 nrm  = normalize(i.normalWS) * isFront;
                nrm = normalize(nrm + (float3(n, n2, n - n2) - 0.5) * _NormalJitter);
                float3 vdir = normalize(GetWorldSpaceViewDir(i.positionWS));

                // Wrapped diffuse from the main light gives the wave form and depth.
                float3 L = normalize(_MainLightPosition.xyz);
                float ndl = dot(nrm, L) * 0.5 + 0.5;
                col.rgb *= lerp(0.55, 1.2, ndl);

                // Interior (back) faces read as deeper, denser water.
                col.rgb = lerp(col.rgb, _DeepColor.rgb, backness * 0.45);
                col.a   = saturate(col.a + backness * 0.25);

                // Fresnel rim.
                float fres = pow(1.0 - saturate(abs(dot(nrm, vdir))), _RimPower);
                col.rgb = lerp(col.rgb, _RimColor.rgb, fres * 0.6);
                col.a   = saturate(col.a + fres * 0.4);

                // Foam: crest, base, and side edges.
                float crestFoam = smoothstep(_FoamStart, _FoamStart + _FoamSoftness,
                                              i.uv.y + (n2 - 0.5) * 0.35);
                float baseFoam  = (1.0 - smoothstep(0.0, 0.14, i.uv.y + (n2 - 0.5) * 0.1)) * 0.8;
                float edgeDist  = min(i.uv.x, 1.0 - i.uv.x);
                float edgeFoam  = (1.0 - smoothstep(0.0, 0.08, edgeDist + (n2 - 0.5) * 0.05)) * 0.7;
                float foam = saturate(crestFoam + baseFoam + edgeFoam);

                col.rgb = lerp(col.rgb, _FoamColor.rgb, foam);
                col.a   = lerp(col.a, 1.0, foam * 0.9);

                // Specular glints on the water surface (not on foam).
                float3 hv = normalize(L + vdir);
                float spec = pow(saturate(dot(nrm, hv)), _SpecPower) * _SpecIntensity;
                col.rgb += spec * _MainLightColor.rgb * (1.0 - foam);

                col.rgb *= _Brightness;
                return col;
            }
            ENDHLSL
        }
    }
}
