Shader "Custom/SilhouetteOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0, 0.5, 1, 1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.05)) = 0.015
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent+100" }
        LOD 100

        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
            };

            float _OutlineWidth;
            fixed4 _OutlineColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                // Transform normals to view space
                float3 norm = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                float2 offset = TransformViewToProjection(norm.xy);

                // Normalize by aspect ratio and clamp projection scale
                #if defined(UNITY_REVERSED_Z)
                    offset.y *= _ScreenParams.x / _ScreenParams.y;
                #endif

                // Extrude in clip-space safely clamped
                o.pos.xy += offset * _OutlineWidth * clamp(o.pos.w, 0.5, 50.0);
                o.color = _OutlineColor;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}