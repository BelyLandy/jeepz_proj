Shader "URP/2D/SpecularSprite_For_Clothes"
{
    Properties
    {
        _MainTex("Main Tex", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0,2)) = 1

        _Color("Tint", Color) = (1,1,1,1)

        _LightColor("Light Color (HDR)", Color) = (1,1,1,1)
        _LightDir("Light Dir (Tangent)", Vector) = (0, 0.5, 1, 0)

        _SpecIntensity("Spec Intensity", Range(0, 4)) = 1
        _Shininess("Shininess", Range(1, 256)) = 128
        _Ambient("Ambient", Range(0, 1)) = 0.1

        // NEW: Unlit mask (1 = игнорировать свет)
        _UnlitMask("Unlit Mask (1 = ignore light)", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline"
               "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _LightColor;
            float3 _LightDir;
            float  _SpecIntensity;
            float  _Shininess;
            float  _Ambient;
            float  _NormalStrength;
            CBUFFER_END

            TEXTURE2D(_MainTex);     SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);   SAMPLER(sampler_NormalMap);
            TEXTURE2D(_UnlitMask);   SAMPLER(sampler_UnlitMask);   // NEW

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;

                // Нормаль (tangent)
                float3 N = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv));
                N = normalize(lerp(float3(0,0,1), N, _NormalStrength));

                // Освещение (diffuse + spec)
                float3 L = normalize(_LightDir);
                float3 V = float3(0, 0, 1);
                float3 H = normalize(L + V);

                float diff = saturate(dot(N, L));
                float specCore = pow(saturate(dot(N, H)), max(1.0, _Shininess));
                float3 spec = specCore * _SpecIntensity * _LightColor.rgb;

                float3 lit = albedo.rgb * (_Ambient + diff * _LightColor.rgb) + spec;

                // --- Unlit mask: 1 = показать чистый albedo без света ---
                float unlitMask = SAMPLE_TEXTURE2D(_UnlitMask, sampler_UnlitMask, IN.uv).r;
                unlitMask = saturate(unlitMask);

                float3 finalRgb = lerp(lit, albedo.rgb, unlitMask);

                return float4(finalRgb, albedo.a);
            }
            ENDHLSL
        }
    }
}
