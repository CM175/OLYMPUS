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

            fixed4 frag (v2f i) : SV_Target {
                // 1. Fresnel Effect calculation
                float fresnel = 1.0 - saturate(dot(normalize(i.worldNormal), normalize(i.viewDir)));
                fresnel = pow(fresnel, _FresnelPower);

                // 2. Procedural flickering logic
                float flicker = frac(sin(_Time.y * _FlickerSpeed) * 43758.5453);
                
                // 3. Final Output
                return _BoltColour * fresnel * (0.8 + flicker * 0.2);
            }
            ENDHLSL
        }
    }
}