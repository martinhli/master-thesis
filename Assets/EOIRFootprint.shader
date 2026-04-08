Shader "Custom/EOIR_Circle_Footprint"
{
    Properties
    {
        _Color ("Color", Color) = (0, 1, 1, 0.5)
        _BaseColor ("Base Color", Color) = (0, 1, 1, 0.5)
        _EdgeSoftness ("Edge Softness", Range(0, 0.5)) = 0.1
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 1.0
        _OutlineColor ("Outline Color", Color) = (0.4, 1, 1, 1)
        _OutlineWidth ("Outline Width", Range(0.005, 0.2)) = 0.08
        _OutlineIntensity ("Outline Intensity", Range(0, 4)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        LOD 100

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _BaseColor;
                float _EdgeSoftness;
                float _GlowIntensity;
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv         : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 tint = (_BaseColor.a > 0.0) ? _BaseColor : _Color;

                float2 center = float2(0.5, 0.5);
                float dist = distance(input.uv, center);

                float radius = 0.5;
                float circle = 1.0 - smoothstep(radius - _EdgeSoftness, radius, dist);

                float outlineInner = max(0.0, radius - _OutlineWidth);
                float outlineOuter = radius;
                float outlineStart = smoothstep(outlineInner - _EdgeSoftness, outlineInner + _EdgeSoftness, dist);
                float outlineEnd = 1.0 - smoothstep(outlineOuter - _EdgeSoftness, outlineOuter + _EdgeSoftness, dist);
                float outline = saturate(outlineStart * outlineEnd);

                float glow = saturate((1.0 - dist * 2.0) * _GlowIntensity);
                float alpha = max(circle * tint.a, outline * _OutlineColor.a);
                float3 color = tint.rgb + (glow * 0.3);
                color += _OutlineColor.rgb * (outline * _OutlineIntensity);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}