Shader "Custom/Outline2D"
{
    Properties
    {
        _OutlineWidth ("Outline Thickness (px)", Range(1, 10)) = 2.5
        _GlowSpread ("Glow Spread (px)", Range(0, 20)) = 8.0
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1.8
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "OutlinePass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Fullscreen triangle generation (matches Blitter.BlitCameraTexture)
                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv  = GetFullScreenTriangleTexCoord(input.vertexID);

                output.positionCS = pos;
                output.texcoord = uv;
                return output;
            }

            TEXTURE2D_X(_BlitTexture);
            float4 _BlitTexture_TexelSize;

            TEXTURE2D(_TeamAMaskTex);
            SAMPLER(sampler_TeamAMaskTex);

            TEXTURE2D(_TeamBMaskTex);
            SAMPLER(sampler_TeamBMaskTex);

            float4 _TeamAColor;
            float4 _TeamBColor;
            float _OutlineWidth;
            float _GlowSpread;
            float _GlowIntensity;

            float2 SampleMaskEdgeAndGlow(TEXTURE2D_PARAM(maskTex, maskSamp), float2 uv, float2 texelSize)
            {
                float center = SAMPLE_TEXTURE2D(maskTex, maskSamp, uv).r;
                if (center > 0.5) return float2(0.0, 0.0);

                float maxOutline = 0.0;
                float glowAccum = 0.0;

                const int DIRECTIONS = 12;
                const float PI2 = 6.2831853;

                [unroll]
                for (int i = 0; i < DIRECTIONS; i++)
                {
                    float angle = (PI2 * i) / DIRECTIONS;
                    float2 dir = float2(cos(angle), sin(angle));
                    float s = SAMPLE_TEXTURE2D(maskTex, maskSamp, uv + dir * texelSize * _OutlineWidth).r;
                    maxOutline = max(maxOutline, s);
                }

                [unroll]
                for (int j = 0; j < DIRECTIONS; j++)
                {
                    float angle = (PI2 * j) / DIRECTIONS;
                    float2 dir = float2(cos(angle), sin(angle));
                    float sMid = SAMPLE_TEXTURE2D(maskTex, maskSamp, uv + dir * texelSize * (_GlowSpread * 0.5)).r;
                    float sFar = SAMPLE_TEXTURE2D(maskTex, maskSamp, uv + dir * texelSize * _GlowSpread).r;
                    glowAccum += (sMid * 0.65 + sFar * 0.35);
                }

                glowAccum /= DIRECTIONS;
                return float2(saturate(maxOutline), saturate(glowAccum));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 texelSize = _BlitTexture_TexelSize.xy;

                float2 teamA = SampleMaskEdgeAndGlow(TEXTURE2D_ARGS(_TeamAMaskTex, sampler_TeamAMaskTex), uv, texelSize);
                float2 teamB = SampleMaskEdgeAndGlow(TEXTURE2D_ARGS(_TeamBMaskTex, sampler_TeamBMaskTex), uv, texelSize);

                half3 finalColor = sceneColor.rgb;

                float alphaA = saturate(teamA.x + teamA.y * _GlowIntensity);
                finalColor = lerp(finalColor, _TeamAColor.rgb * (1.0 + teamA.y * _GlowIntensity), alphaA);

                float alphaB = saturate(teamB.x + teamB.y * _GlowIntensity);
                finalColor = lerp(finalColor, _TeamBColor.rgb * (1.0 + teamB.y * _GlowIntensity), alphaB);

                return half4(finalColor, sceneColor.a);
            }
            ENDHLSL
        }
    }
}