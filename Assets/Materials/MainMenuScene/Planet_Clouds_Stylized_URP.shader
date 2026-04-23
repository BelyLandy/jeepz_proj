Shader "Custom/Planet_Clouds_Stylized_URP"
{
    Properties
    {
        [Header(Cloud_Color)]
        _CloudColor("Cloud Color", Color) = (0.93, 0.97, 1.0, 1)
        _CloudRimColor("Cloud Rim Color", Color) = (0.82, 0.92, 1.0, 1)

        [Header(Cloud_Mask)]
        [Enum(Off,0,Front,1,Back,2)] _CullMode("Cull Mode", Float) = 2
        _CloudOffset("Cloud Offset", Vector) = (0, 0, 0, 0)
        _CloudWarpOffset("Cloud Warp Offset", Vector) = (0, 0, 0, 0)
        _CloudScale("Cloud Scale", Range(0.1, 16.0)) = 2.6
        _CloudWarpScale("Cloud Warp Scale", Range(0.1, 24.0)) = 6.0
        _CloudWarpStrength("Cloud Warp Strength", Range(0.0, 1.0)) = 0.18
        _CloudCoverage("Cloud Coverage", Range(0.0, 1.0)) = 0.54
        _CloudSoftness("Cloud Softness", Range(0.001, 0.35)) = 0.12
        _CloudAlpha("Cloud Alpha", Range(0.0, 1.0)) = 0.62

        [Header(Cloud_Rim)]
        _CloudRimPower("Cloud Rim Power", Range(0.5, 8.0)) = 2.8
        _CloudRimStrength("Cloud Rim Strength", Range(0.0, 2.0)) = 0.35

        [Header(Cloud_Lighting)]
        _CloudLightStrength("Cloud Light Strength", Range(0.0, 2.0)) = 1.0
        _CloudShadowStrength("Cloud Shadow Strength", Range(0.0, 1.0)) = 0.28
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }
        LOD 200

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_CullMode]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CloudColor;
                half4 _CloudRimColor;
                half _CullMode;
                float4 _CloudOffset;
                float4 _CloudWarpOffset;
                half _CloudScale;
                half _CloudWarpScale;
                half _CloudWarpStrength;
                half _CloudCoverage;
                half _CloudSoftness;
                half _CloudAlpha;
                half _CloudRimPower;
                half _CloudRimStrength;
                half _CloudLightStrength;
                half _CloudShadowStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 viewDirWS : TEXCOORD3;
                float fogCoord : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Slightly smoother value noise built from hash corners.
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float ValueNoise2D(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i + float2(0.0, 0.0));
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float TriplanarValueNoise(float3 sphereDir, float scale, float3 offset)
            {
                float3 blend = abs(sphereDir);
                blend = pow(blend, 4.0);
                float sum = blend.x + blend.y + blend.z;
                if (sum <= 0.0001)
                {
                    return 0.0;
                }

                blend /= sum;
                float3 p = sphereDir * scale + offset;

                float noiseX = ValueNoise2D(p.yz + float2(113.17, 57.41));
                float noiseY = ValueNoise2D(p.xz + float2(73.93, 19.31));
                float noiseZ = ValueNoise2D(p.xy + float2(41.07, 89.63));

                return noiseX * blend.x + noiseY * blend.y + noiseZ * blend.z;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                float3 sphereDirOS = normalize(input.positionOS);

                float warpNoise = TriplanarValueNoise(sphereDirOS, _CloudWarpScale, float3(13.1, -7.3, 4.7) + _CloudWarpOffset.xyz);
                float3 warpedDir = normalize(sphereDirOS + ((warpNoise - 0.5) * _CloudWarpStrength));
                float baseNoise = TriplanarValueNoise(warpedDir, _CloudScale, float3(2.3, 8.1, -5.4) + _CloudOffset.xyz);
                float breakupNoise = TriplanarValueNoise(sphereDirOS, _CloudWarpScale * 1.37, float3(-9.2, 11.7, 6.4) + _CloudWarpOffset.xyz);

                float combined = saturate(baseNoise + (breakupNoise - 0.5) * (_CloudWarpStrength * 0.7));
                float threshold = 1.0 - _CloudCoverage;
                float cloudMask = smoothstep(threshold - _CloudSoftness, threshold + _CloudSoftness, combined);

                Light mainLight = GetMainLight();
                half3 lightDir = normalize(mainLight.direction);
                half ndl = saturate(dot(normalWS, lightDir));
                half lightFactor = lerp(1.0h - _CloudShadowStrength, 1.0h + _CloudLightStrength * 0.35h, ndl);
                half3 ambient = SampleSH(normalWS) * 0.45h;

                half rim = pow(saturate(1.0h - dot(normalWS, viewDirWS)), _CloudRimPower) * _CloudRimStrength;
                half3 color = _CloudColor.rgb * lightFactor;
                color += ambient * _CloudColor.rgb;
                color += _CloudRimColor.rgb * rim;
                color *= mainLight.color;

                half alpha = saturate(cloudMask * _CloudAlpha * (0.65h + 0.35h * ndl));

                half4 finalColor = half4(color, alpha);
                finalColor.rgb = MixFog(finalColor.rgb, input.fogCoord);
                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
