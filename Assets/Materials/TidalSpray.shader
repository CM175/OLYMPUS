Shader "Olympus/TidalSpray"
{
    Properties
    {
        _Color    ("Tint", Color) = (1, 1, 1, 1)
        _Softness ("Softness", Range(0.1, 4)) = 1.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Softness;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Procedural soft circle: no texture needed.
                float2 p = i.uv * 2.0 - 1.0;
                float a = pow(saturate(1.0 - dot(p, p)), _Softness);
                return fixed4(i.color.rgb * _Color.rgb, i.color.a * _Color.a * a);
            }
            ENDCG
        }
    }
}
