// SlimeStrip.shader
// Contains TWO shaders:
// 1) Slime/Strip (URP)      -> for URP (2D Renderer + Forward)
// 2) Slime/Strip (Built-in) -> for Built-in pipeline

///////////////////////////////////////////////////////////////
// 1) URP VERSION
///////////////////////////////////////////////////////////////
Shader "Slime/Strip (URP)"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.2, 1, 0.6, 1)
        _EdgeSoftness("Edge Softness", Range(0.0001, 0.5)) = 0.08

        _NoiseScale("Noise Scale", Float) = 8
        _NoiseStrength("Noise Strength", Range(0, 1)) = 0.15
        _NoiseSpeed("Noise Speed", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        // Pass for URP 2D Renderer
        Pass
        {
            Name "Unlit2D"
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float2 posWS       : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _EdgeSoftness;
                float  _NoiseScale;
                float  _NoiseStrength;
                float  _NoiseSpeed;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs v = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = v.positionCS;

                float3 ws = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.posWS = ws.xy;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Soft edges based on UV.y (0..1 across strip)
                float edge = min(IN.uv.y, 1.0 - IN.uv.y);
                float mask = smoothstep(0.0, _EdgeSoftness, edge);

                // Optional wobble alpha
                float t = _Time.y * _NoiseSpeed;
                float n = noise(IN.posWS * _NoiseScale + t);
                float wobble = lerp(1.0, n, _NoiseStrength);

                half4 col = _BaseColor;
                col.a *= mask * wobble;
                return col;
            }
            ENDHLSL
        }

        // Pass for URP Forward Renderer (если не 2D Renderer)
        Pass
        {
            Name "UnlitForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float2 posWS       : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _EdgeSoftness;
                float  _NoiseScale;
                float  _NoiseStrength;
                float  _NoiseSpeed;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs v = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = v.positionCS;

                float3 ws = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.posWS = ws.xy;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float edge = min(IN.uv.y, 1.0 - IN.uv.y);
                float mask = smoothstep(0.0, _EdgeSoftness, edge);

                float t = _Time.y * _NoiseSpeed;
                float n = noise(IN.posWS * _NoiseScale + t);
                float wobble = lerp(1.0, n, _NoiseStrength);

                half4 col = _BaseColor;
                col.a *= mask * wobble;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}