Shader "Custom/SpriteOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1, 0, 0, 1)
        _OutlineSize ("Outline Size", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _OutlineColor;
            float _OutlineSize;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                // Sample adjacent pixels to detect edges
                float2 texelSize = _MainTex_TexelSize.xy * _OutlineSize;

                float alphaUp    = tex2D(_MainTex, i.uv + float2(0,  texelSize.y)).a;
                float alphaDown  = tex2D(_MainTex, i.uv + float2(0, -texelSize.y)).a;
                float alphaLeft  = tex2D(_MainTex, i.uv + float2(-texelSize.x, 0)).a;
                float alphaRight = tex2D(_MainTex, i.uv + float2( texelSize.x, 0)).a;

                // Diagonal samples for smoother outline
                float alphaUL = tex2D(_MainTex, i.uv + float2(-texelSize.x,  texelSize.y)).a;
                float alphaUR = tex2D(_MainTex, i.uv + float2( texelSize.x,  texelSize.y)).a;
                float alphaDL = tex2D(_MainTex, i.uv + float2(-texelSize.x, -texelSize.y)).a;
                float alphaDR = tex2D(_MainTex, i.uv + float2( texelSize.x, -texelSize.y)).a;

                float neighborAlpha = max(max(max(alphaUp, alphaDown), max(alphaLeft, alphaRight)),
                                          max(max(alphaUL, alphaUR), max(alphaDL, alphaDR)));

                // If this pixel is transparent but a neighbor is not, draw outline
                if (col.a < 0.1 && neighborAlpha > 0.1)
                {
                    return _OutlineColor;
                }

                return col;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
