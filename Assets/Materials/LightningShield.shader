Shader "Olympus/LightningShield"
{
    Properties
    {
        [HDR] _BoltColour("Shield Colour", Color) = (0, 0.5, 1, 1)
        _FlickerSpeed("Flicker Speed", Float) = 10.0
        _FresnelPower("Glow Sharpness", Float) = 3.0
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
                float3 normal : NORMAL;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            float4 _BoltColour;
            float _FlickerSpeed;
            float _FresnelPower;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            // Add a noise function (like the one we used for the bolt)
            float noise(float3 p) {
                return frac(sin(dot(p, float3(12.9898, 78.233, 45.164))) * 43758.5453);
            }

            fixed4 frag (v2f i) : SV_Target {
                // 1. Fresnel for the edge glow
                float fresnel = 1.0 - saturate(dot(normalize(i.worldNormal), normalize(i.viewDir)));
                fresnel = pow(fresnel, _FresnelPower);

                // 2. Procedural "Energy Veins"
                // We use world position and time to scroll lightning patterns across the sphere
                float3 worldPos = i.worldNormal * 5.0; // Scale the pattern
                float lightningPattern = noise(worldPos + _Time.y * 2.0);
                
                // 3. Cutoff: Only show the brightest parts of the noise
                float cut = smoothstep(0.4, 0.5, lightningPattern);

                // 4. Combine: Fresnel glow + sharp flickering veins
                return _BoltColour * (fresnel + cut * 1.5);
            }
            ENDHLSL
        }
    }
}