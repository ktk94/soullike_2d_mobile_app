Shader "Custom/SpriteDissolve"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _DissolveTex ("Dissolve Noise Texture", 2D) = "white" {}
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _EdgeColor ("Edge Color", Color) = (1, 0, 0, 1)
        _EdgeWidth ("Edge Width", Float) = 0.05
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
            sampler2D _DissolveTex;
            float _DissolveAmount;
            float4 _EdgeColor;
            float _EdgeWidth;

            // Procedural noise fallback (used when _DissolveTex is default white)
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float proceduralNoise(float2 uv)
            {
                float2 i = floor(uv * 10.0);
                float2 f = frac(uv * 10.0);

                // Smoothstep interpolation
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

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

                // Sample the dissolve texture
                float dissolveValue = tex2D(_DissolveTex, i.uv).r;

                // If dissolve texture is effectively white (default), use procedural noise
                // We check by also sampling a slightly offset position
                float dissolveCheck = tex2D(_DissolveTex, i.uv + float2(0.1, 0.1)).r;
                if (dissolveValue > 0.99 && dissolveCheck > 0.99)
                {
                    dissolveValue = proceduralNoise(i.uv);
                }

                // Clip pixels based on dissolve amount
                float dissolveThreshold = dissolveValue - _DissolveAmount;

                // Discard fully dissolved pixels
                if (dissolveThreshold < 0)
                {
                    discard;
                }

                // Edge glow effect at the dissolve boundary
                float edgeFactor = 1.0 - saturate(dissolveThreshold / _EdgeWidth);

                // Apply edge color with emission-like brightness
                fixed3 edgeEmission = _EdgeColor.rgb * edgeFactor * 2.0;
                col.rgb += edgeEmission * col.a;

                return col;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
