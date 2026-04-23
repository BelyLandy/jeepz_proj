Shader "Custom/Planet_Surface_Blocky_Stylized_URP"
{
    Properties
    {
        [Header(Lighting)]
        _ShadowThreshold("Shadow Threshold", Range(0.0, 1.0)) = 0.32
        _MidThreshold("Mid Threshold", Range(0.0, 1.0)) = 0.70
        _BandSoftness("Band Softness", Range(0.001, 0.25)) = 0.05
        _ShadowColor("Shadow Color", Color) = (0.58, 0.68, 0.86, 1)
        _MidToneColor("Mid Tone Color", Color) = (0.86, 0.92, 1.0, 1)

        [Header(Cell_Edges)]
        _CellEdgeWidth("Cell Edge Width", Range(0.0, 0.45)) = 0.0
        _CellEdgeDarkness("Cell Edge Darkness", Range(0.0, 1.0)) = 0.0
        _CellEdgeSoftness("Cell Edge Softness", Range(0.001, 0.25)) = 0.001

        [Header(Surface_Rim)]
        _SurfaceRimColor("Surface Rim Color", Color) = (0.65, 0.85, 1.0, 1)
        _SurfaceRimPower("Surface Rim Power", Range(0.5, 8.0)) = 3.0
        _SurfaceRimStrength("Surface Rim Strength", Range(0.0, 1.0)) = 0.0

        [Header(Water)]
        _OceanSmoothness("Ocean Smoothness", Range(0.0, 1.0)) = 0.7
        _OceanRimStrength("Ocean Rim Strength", Range(0.0, 2.0)) = 0.3
        _OceanRimColor("Ocean Rim Color", Color) = (0.45, 0.80, 1.0, 1)

        [Header(Lava)]
        _LavaEmissionColor("Lava Emission Color", Color) = (1.0, 0.482, 0.922, 1)
        _LavaEmissionStrength("Lava Emission Strength", Range(0.0, 4.0)) = 1.2
        _LavaRimStrength("Lava Rim Strength", Range(0.0, 2.0)) = 0.45
        _LavaLightBoost("Lava Light Boost", Range(0.0, 1.0)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _ShadowThreshold;
                half _MidThreshold;
                half _BandSoftness;
                half4 _ShadowColor;
                half4 _MidToneColor;

                half _CellEdgeWidth;
                half _CellEdgeDarkness;
                half _CellEdgeSoftness;

                half4 _SurfaceRimColor;
                half _SurfaceRimPower;
                half _SurfaceRimStrength;

                half _OceanSmoothness;
                half _OceanRimStrength;
                half4 _OceanRimColor;

                half4 _LavaEmissionColor;
                half _LavaEmissionStrength;
                half _LavaRimStrength;
                half _LavaLightBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float2 uv : TEXCOORD2;
                float4 uv2 : TEXCOORD3;
                half3 viewDirWS : TEXCOORD4;
                float fogCoord : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.uv = input.uv;
                output.uv2 = input.uv2;
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half3 ApplyToonLighting(half3 baseColor, half3 normalWS, half3 viewDirWS, half waterMask, half lavaMask)
            {
                Light mainLight = GetMainLight();
                half3 lightDir = normalize(mainLight.direction);
                half ndl = saturate(dot(normalWS, lightDir));

                half shadowToMid = smoothstep(_ShadowThreshold - _BandSoftness, _ShadowThreshold + _BandSoftness, ndl);
                half midToLight = smoothstep(_MidThreshold - _BandSoftness, _MidThreshold + _BandSoftness, ndl);

                half3 shadowCol = baseColor * _ShadowColor.rgb;
                half3 midCol = baseColor * _MidToneColor.rgb;
                half3 lightCol = baseColor;

                half3 lit = lerp(shadowCol, midCol, shadowToMid);
                lit = lerp(lit, lightCol, midToLight);
                lit *= mainLight.color;

                half3 ambient = SampleSH(normalWS) * baseColor;
                lit += ambient * 0.55h;

                half fres = pow(saturate(1.0h - dot(normalWS, normalize(viewDirWS))), 4.0h);
                half oceanBoost = (0.3h + 0.7h * _OceanSmoothness) * waterMask;
                lit += _OceanRimColor.rgb * (fres * _OceanRimStrength * oceanBoost);

                lit += baseColor * (_LavaLightBoost * lavaMask);
                return lit;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                half waterMask = saturate(input.color.a);
                half lavaMask = saturate(input.uv2.x);
                half3 baseColor = input.color.rgb;

                half2 clampedUV = saturate(input.uv);
                half2 minToEdge2 = min(clampedUV, 1.0h - clampedUV);
                half minToEdge = min(minToEdge2.x, minToEdge2.y);
                half edgeInterior = smoothstep(_CellEdgeWidth, _CellEdgeWidth + _CellEdgeSoftness, minToEdge);
                half edgeFactor = lerp(1.0h - _CellEdgeDarkness, 1.0h, edgeInterior);
                baseColor *= edgeFactor;

                baseColor = lerp(baseColor, saturate(baseColor * (1.0h + 0.12h * _OceanSmoothness)), waterMask);

                half3 lit = ApplyToonLighting(baseColor, normalWS, viewDirWS, waterMask, lavaMask);

                half rim = pow(saturate(1.0h - dot(normalWS, viewDirWS)), _SurfaceRimPower) * _SurfaceRimStrength;
                lit += _SurfaceRimColor.rgb * rim;

                half lavaRim = pow(saturate(1.0h - dot(normalWS, viewDirWS)), 2.2h) * _LavaRimStrength * lavaMask;
                half3 lavaEmission = _LavaEmissionColor.rgb * (_LavaEmissionStrength * lavaMask);
                lit += _LavaEmissionColor.rgb * lavaRim;
                lit += lavaEmission;

                half4 finalColor = half4(lit, 1.0h);
                finalColor.rgb = MixFog(finalColor.rgb, input.fogCoord);
                return finalColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
