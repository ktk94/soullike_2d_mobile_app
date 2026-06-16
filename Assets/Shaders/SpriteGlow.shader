Shader "Custom/SpriteGlow"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _GlowColor ("Glow Color", Color) = (1, 1, 0, 1)
        _GlowSize ("Glow Size", Range(1, 5)) = 2
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 1
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
            float4 _GlowColor;
            float _GlowSize;
            float _GlowIntensity;

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

                // Gaussian blur approximation for glow
                // Sample surrounding pixels in a grid pattern
                float2 texelSize = _MainTex_TexelSize.xy * _GlowSize;
                float glowAlpha = 0;
                float totalWeight = 0;

                // 9-tap Gaussian-like kernel (3x3 with weights)
                const int SAMPLES = 3;
                float weights[3] = { 0.25, 0.5, 0.25 };

                for (int x = -SAMPLES + 1; x < SAMPLES; x++)
                {
                    for (int y = -SAMPLES + 1; y < SAMPLES; y++)
                    {
                        float2 offset = float2(x, y) * texelSize;
                        float weight = weights[abs(x)] * weights[abs(y)];
                        glowAlpha += tex2D(_MainTex, i.uv + offset).a * weight;
                        totalWeight += weight;
                    }
                }

                glowAlpha /= totalWeight;

                // Additional wider samples for softer spread
                float wideAlpha = 0;
                float wideWeight = 0;
                float2 wideTexelSize = texelSize * 2.0;

                for (int wx = -2; wx <= 2; wx++)
                {
                    for (int wy = -2; wy <= 2; wy++)
                    {
                        float2 offset = float2(wx, wy) * wideTexelSize;
                        float dist = length(float2(wx, wy));
                        float w = exp(-dist * dist * 0.5);
                        wideAlpha += tex2D(_MainTex, i.uv + offset).a * w;
                        wideWeight += w;
                    }
                }

                wideAlpha /= wideWeight;

                // Combine narrow and wide glow
                float combinedGlow = max(glowAlpha, wideAlpha * 0.6);

                // Glow is visible outside the original sprite
                float glowMask = saturate(combinedGlow - col.a) * _GlowIntensity;

                // Composite: glow behind the sprite, original sprite on top
                fixed4 glowResult = fixed4(_GlowColor.rgb, glowMask * _GlowColor.a);
                fixed4 result = fixed4(
                    lerp(glowResult.rgb, col.rgb, col.a),
                    saturate(col.a + glowResult.a)
                );

                return result;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
