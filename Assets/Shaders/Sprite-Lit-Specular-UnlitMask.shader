Shader "URP/2D/Sprite-Lit-Specular-UnlitMask"
{
    Properties
    {
        [PerRendererData] _MainTex   ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _MaskTex   ("Light Mask (Secondary: MaskTex)", 2D) = "white" {}
        [PerRendererData] _NormalMap ("Normal Map (Secondary: NormalMap)", 2D) = "bump" {}
        [PerRendererData] _UnlitMask ("Unlit Mask (Secondary: UnlitMask)", 2D) = "black" {}

        _NormalStrength ("Normal Strength", Range(0,2)) = 1
        _SpecIntensity  ("Spec Intensity (approx)", Range(0,4)) = 1
        _Shininess      ("Shininess", Range(1,256)) = 128
        _SpecZ          ("Spec Z (fake height)", Range(0.001, 1)) = 0.25
        _Ambient        ("Ambient", Range(0,1)) = 0.1

        // Legacy / SpriteRenderer internal
        [HideInInspector] _Color         ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip          ("Flip", Vector) = (1,1,1,1) // оставляем для совместимости, но НЕ используем

        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        // ============================================================
        // PASS 1: URP 2D lighting (Light2D)
        // ============================================================
        Pass
        {
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightVariables.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 tangent    : TANGENT;     // ВАЖНО: tangents
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                half4  color       : COLOR;
                float2 uv          : TEXCOORD0;
                float2 lightingUV  : TEXCOORD1;

                half3  normalWS    : TEXCOORD2;
                half3  tangentWS   : TEXCOORD3;
                half3  bitangentWS : TEXCOORD4;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);     SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);     SAMPLER(sampler_MaskTex);
            TEXTURE2D(_NormalMap);   SAMPLER(sampler_NormalMap);
            TEXTURE2D(_UnlitMask);   SAMPLER(sampler_UnlitMask);

            TEXTURE2D(_AlphaTex);    SAMPLER(sampler_AlphaTex);

            float4 _MainTex_ST;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                half   _NormalStrength;
                half   _SpecIntensity;
                half   _Shininess;
                half   _SpecZ;
                half   _Ambient;
                half   _EnableExternalAlpha;
            CBUFFER_END

            float4 _RendererColor;

            inline half4 SampleSpriteRGBA(float2 uv)
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                if (_EnableExternalAlpha > 0.5h)
                {
                    half a = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv).r;
                    c.a = a;
                }
                return c;
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv         = TRANSFORM_TEX(v.uv, _MainTex);
                o.lightingUV = ComputeNormalizedDeviceCoordinates(o.positionCS.xyz / o.positionCS.w);
                o.color      = v.color * _Color * _RendererColor;

                // Базис для normal/spec (работает и при flipX, если tangents корректные)
                o.normalWS    = -GetViewForwardDir();
                o.tangentWS   = TransformObjectToWorldDir(v.tangent.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * v.tangent.w;

                return o;
            }

            inline half4 ApplyMaskFilter(half4 light, half4 mask, half4 maskFilter, half4 invertedFilter)
            {
                if (any(maskFilter))
                {
                    half4 processedMask = (1 - invertedFilter) * mask + invertedFilter * (1 - mask);
                    light *= dot(processedMask, maskFilter);
                }
                return light;
            }

            FragmentOutput frag(Varyings i)
            {
                half4 main = i.color * SampleSpriteRGBA(i.uv);
                if (main.a <= 0) discard;

                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv);

                // --- накопление света (как CombinedShapeLightShared, но без include'ов/struct'ов) ---
                half4 mod0 = 0, add0 = 0;
                half4 mod1 = 0, add1 = 0;
                half4 mod2 = 0, add2 = 0;
                half4 mod3 = 0, add3 = 0;

                #if USE_SHAPE_LIGHT_TYPE_0
                {
                    half4 l = SAMPLE_TEXTURE2D(_ShapeLightTexture0, sampler_ShapeLightTexture0, i.lightingUV);
                    l = ApplyMaskFilter(l, mask, _ShapeLightMaskFilter0, _ShapeLightInvertedFilter0);
                    mod0 = l * _ShapeLightBlendFactors0.x;
                    add0 = l * _ShapeLightBlendFactors0.y;
                }
                #endif
                #if USE_SHAPE_LIGHT_TYPE_1
                {
                    half4 l = SAMPLE_TEXTURE2D(_ShapeLightTexture1, sampler_ShapeLightTexture1, i.lightingUV);
                    l = ApplyMaskFilter(l, mask, _ShapeLightMaskFilter1, _ShapeLightInvertedFilter1);
                    mod1 = l * _ShapeLightBlendFactors1.x;
                    add1 = l * _ShapeLightBlendFactors1.y;
                }
                #endif
                #if USE_SHAPE_LIGHT_TYPE_2
                {
                    half4 l = SAMPLE_TEXTURE2D(_ShapeLightTexture2, sampler_ShapeLightTexture2, i.lightingUV);
                    l = ApplyMaskFilter(l, mask, _ShapeLightMaskFilter2, _ShapeLightInvertedFilter2);
                    mod2 = l * _ShapeLightBlendFactors2.x;
                    add2 = l * _ShapeLightBlendFactors2.y;
                }
                #endif
                #if USE_SHAPE_LIGHT_TYPE_3
                {
                    half4 l = SAMPLE_TEXTURE2D(_ShapeLightTexture3, sampler_ShapeLightTexture3, i.lightingUV);
                    l = ApplyMaskFilter(l, mask, _ShapeLightMaskFilter3, _ShapeLightInvertedFilter3);
                    mod3 = l * _ShapeLightBlendFactors3.x;
                    add3 = l * _ShapeLightBlendFactors3.y;
                }
                #endif

                half4 lit = main;
                half3 lightRGB = 0;

                #if !USE_SHAPE_LIGHT_TYPE_0 && !USE_SHAPE_LIGHT_TYPE_1 && !USE_SHAPE_LIGHT_TYPE_2 && !USE_SHAPE_LIGHT_TYPE_3
                    lit = main;
                    lightRGB = 0;
                #else
                    half4 finalMod = mod0 + mod1 + mod2 + mod3;
                    half4 finalAdd = add0 + add1 + add2 + add3;

                    lit = _HDREmulationScale * (main * finalMod + finalAdd);
                    lit.a = main.a;
                    lit = max(0, lit);

                    // “примерная яркость света” для спека
                    lightRGB = _HDREmulationScale * (finalMod.rgb + finalAdd.rgb);
                #endif

                // Unlit mask (1 = полностью без света)
                half unlitMask = saturate(SAMPLE_TEXTURE2D(_UnlitMask, sampler_UnlitMask, i.uv).r);

                // --- pseudo specular ---
                half3 specAdd = 0;
                if (dot(lightRGB, lightRGB) > 1e-8)
                {
                    half3 Nts = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                    Nts = normalize(lerp(half3(0,0,1), Nts, _NormalStrength));

                    // Перевод tangent-space normal -> world через TBN (важно для flipX!)
                    half3 Nws = normalize(i.tangentWS * Nts.x + i.bitangentWS * Nts.y + i.normalWS * Nts.z);

                    // градиент яркости света в screen-space
                    float luma = dot((float3)lightRGB, float3(0.2126, 0.7152, 0.0722));
                    float2 grad = float2(ddx(luma), ddy(luma));
                    float g2 = dot(grad, grad);

                    if (g2 > 1e-10)
                    {
                        float2 Lxy = normalize(-grad);

                        float3 viewRight = normalize(float3(UNITY_MATRIX_I_V._m00, UNITY_MATRIX_I_V._m10, UNITY_MATRIX_I_V._m20));
                        float3 viewUp    = normalize(float3(UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m21));
                        half3  viewN     = (half3)(-GetViewForwardDir());

                        half3 Lws = normalize((half3)(viewRight * Lxy.x + viewUp * Lxy.y) + viewN * _SpecZ);
                        half3 Vws = viewN;
                        half3 Hws = normalize(Lws + Vws);

                        half specCore = pow(saturate(dot(Nws, Hws)), _Shininess);
                        specAdd = specCore * _SpecIntensity * lightRGB;
                    }
                }

                // ambient как в старом
                half3 withAmbient = (lit.rgb + specAdd) + main.rgb * _Ambient;

                // unlitMask=1 => только main.rgb
                half3 finalRgb = lerp(withAmbient, main.rgb, unlitMask);

                return ToFragmentOutput(half4(finalRgb, main.a));
            }
            ENDHLSL
        }

        // ============================================================
        // PASS 2: NormalsRendering (для Light2D normal mapping)
        // ============================================================
        Pass
        {
            Tags { "LightMode"="NormalsRendering" }

            HLSLPROGRAM
            #pragma vertex   nvert
            #pragma fragment nfrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/NormalsRenderingShared.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 tangent    : TANGENT;   // ВАЖНО
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                half4  color       : COLOR;
                float2 uv          : TEXCOORD0;

                half3  normalWS    : TEXCOORD1;
                half3  tangentWS   : TEXCOORD2;
                half3  bitangentWS : TEXCOORD3;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);     SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);   SAMPLER(sampler_NormalMap);
            TEXTURE2D(_AlphaTex);    SAMPLER(sampler_AlphaTex);

            float4 _MainTex_ST;

            CBUFFER_START(UnityPerMaterial)
                half _NormalStrength;
                half _EnableExternalAlpha;
            CBUFFER_END

            inline half4 SampleSpriteRGBA(float2 uv)
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                if (_EnableExternalAlpha > 0.5h)
                {
                    half a = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv).r;
                    c.a = a;
                }
                return c;
            }

            Varyings nvert(Attributes a)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(a.positionOS);
                o.uv         = TRANSFORM_TEX(a.uv, _MainTex);
                o.color      = a.color;

                o.normalWS    = -GetViewForwardDir();
                o.tangentWS   = TransformObjectToWorldDir(a.tangent.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * a.tangent.w;

                return o;
            }

            half4 nfrag(Varyings i) : SV_Target
            {
                half4 mainTex = i.color * SampleSpriteRGBA(i.uv);

                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                normalTS = normalize(lerp(half3(0,0,1), normalTS, _NormalStrength));

                return NormalsRenderingShared(mainTex, normalTS, i.tangentWS, i.bitangentWS, i.normalWS);
            }
            ENDHLSL
        }

        // (не обязательно, но удобно) Fallback для не-2D рендерера
        Pass
        {
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex uvert
            #pragma fragment ufrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_AlphaTex); SAMPLER(sampler_AlphaTex);
            float4 _MainTex_ST;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                half   _EnableExternalAlpha;
            CBUFFER_END

            float4 _RendererColor;

            inline half4 SampleSpriteRGBA(float2 uv)
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                if (_EnableExternalAlpha > 0.5h)
                {
                    half a = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv).r;
                    c.a = a;
                }
                return c;
            }

            Varyings uvert(Attributes a)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(a.positionOS);
                o.uv = TRANSFORM_TEX(a.uv, _MainTex);
                o.color = a.color * _Color * _RendererColor;
                return o;
            }

            half4 ufrag(Varyings i) : SV_Target
            {
                return i.color * SampleSpriteRGBA(i.uv);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
