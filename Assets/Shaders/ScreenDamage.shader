Shader "Custom/ScreenDamage"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _DamageAmount ("Damage Amount", Range(0, 1)) = 0
        _VignetteColor ("Vignette Color", Color) = (1, 0, 0, 0.8)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
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
            float _DamageAmount;
            float4 _VignetteColor;

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

                // Calculate distance from center for vignette
                float2 center = float2(0.5, 0.5);
                float2 dist = i.uv - center;

                // Elliptical distance (wider horizontally for widescreen)
                float vignette = length(dist * float2(1.0, 0.8));

                // Shape the vignette curve
                // At 0 damage: no effect. At 1 damage: strong vignette from edges
                float vignetteInner = lerp(1.0, 0.2, _DamageAmount);
                float vignetteOuter = lerp(1.5, 0.6, _DamageAmount);
                float vignetteMask = smoothstep(vignetteInner, vignetteOuter, vignette);

                // Apply damage intensity
                vignetteMask *= _DamageAmount;

                // Slight pulsating effect using time (subtle)
                float pulse = 1.0 + sin(_Time.y * 4.0) * 0.1 * _DamageAmount;
                vignetteMask *= pulse;

                vignetteMask = saturate(vignetteMask);

                // Composite the vignette color over the source
                fixed4 result;
                result.rgb = lerp(col.rgb, _VignetteColor.rgb, vignetteMask);
                result.a = saturate(col.a + vignetteMask * _VignetteColor.a);

                return result;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
