Shader "Custom/Planet_Atmosphere_RimBoost_URP"
{
    Properties
    {
        [Header(Atmosphere)]
        _AtmosphereColor("Atmosphere Color", Color) = (0.25, 0.78, 1.0, 1)
        _AtmosphereAlpha("Atmosphere Alpha", Range(0.0, 1.0)) = 0.42

        [Header(Rim)]
        _RimPower("Rim Power", Range(0.5, 8.0)) = 2.75
        _RimIntensity("Rim Intensity", Range(0.0, 4.0)) = 1.15

        [Header(Band_Mask)]
        _BandInnerStart("Band Inner Start", Range(0.0, 1.0)) = 0.18
        _BandInnerEnd("Band Inner End", Range(0.0, 1.0)) = 0.38
        _BandOuterStart("Band Outer Start", Range(0.0, 1.0)) = 0.72
        _BandOuterEnd("Band Outer End", Range(0.0, 1.0)) = 0.94

        [Header(Light_Side_Boost)]
        _LightBoostColor("Light Boost Color", Color) = (0.65, 0.92, 1.0, 1)
        _LightBoostStrength("Light Boost Strength", Range(0.0, 4.0)) = 0.65
        _LightBoostPower("Light Boost Power", Range(0.5, 8.0)) = 2.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }
        LOD 150

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _AtmosphereColor;
                half _AtmosphereAlpha;
                half _RimPower;
                half _RimIntensity;
                half _BandInnerStart;
                half _BandInnerEnd;
                half _BandOuterStart;
                half _BandOuterEnd;
                half4 _LightBoostColor;
                half _LightBoostStrength;
                half _LightBoostPower;
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
                half3 normalWS : TEXCOORD0;
                half3 viewDirWS : TEXCOORD1;
                float fogCoord : TEXCOORD2;
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
                Light mainLight = GetMainLight();
                half3 lightDir = normalize(mainLight.direction);

                half edge = saturate(1.0h - dot(normalWS, viewDirWS));
                half rim = pow(edge, _RimPower);
                half rimComponent = rim * _RimIntensity;

                half innerRise = smoothstep(_BandInnerStart, _BandInnerEnd, edge);
                half outerFade = 1.0h - smoothstep(_BandOuterStart, _BandOuterEnd, edge);
                half bandMask = saturate(innerRise * outerFade);

                half ndl = saturate(dot(normalWS, lightDir));
                half lightBoost = pow(ndl, _LightBoostPower) * _LightBoostStrength;
                lightBoost *= bandMask;

                half3 color = (_AtmosphereColor.rgb * rimComponent) + (_LightBoostColor.rgb * lightBoost);
                color *= bandMask;
                color *= mainLight.color;

                half alpha = saturate(bandMask * _AtmosphereAlpha);

                half4 finalColor = half4(color, alpha);
                finalColor.rgb = MixFog(finalColor.rgb, input.fogCoord);
                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
