Shader "Custom/ProceduralShapes/CircleLayer"
{
    Properties
    {
        _Color ("Color", Color) = (0.8, 0.9, 1.0, 1.0)
        _MasterOpacity ("Master Opacity", Range(0,1)) = 0.5

        _GridScaleX ("Grid Scale X", Float) = 24
        _GridScaleY ("Grid Scale Y", Float) = 14
        _FillRate ("Fill Rate", Range(0,1)) = 0.32

        _MinScale ("Min Scale", Float) = 0.16
        _MaxScale ("Max Scale", Float) = 0.34

        _EdgeSoftness ("Edge Softness", Float) = 0.05
        _CenterJitter ("Center Jitter", Float) = 0.28

        _GlobalAnimSpeed ("Global Anim Speed", Float) = 0.9
        _FadeInTime ("Fade In Time", Float) = 1.4
        _VisibleTime ("Visible Time", Float) = 2.4
        _FadeOutTime ("Fade Out Time", Float) = 1.2
        _HiddenTime ("Hidden Time", Float) = 2.8
        _AppearScaleMin ("Appear Scale Min", Range(0,1)) = 0.45

        _DriftX ("Drift X", Float) = 0.001
        _DriftY ("Drift Y", Float) = 0.0005

        _UseOutline ("Use Outline", Range(0,1)) = 0
        _OutlineThickness ("Outline Thickness", Float) = 0.06
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float _MasterOpacity;
            float _GridScaleX, _GridScaleY, _FillRate;
            float _MinScale, _MaxScale;
            float _EdgeSoftness, _CenterJitter;
            float _GlobalAnimSpeed, _FadeInTime, _VisibleTime, _FadeOutTime, _HiddenTime, _AppearScaleMin;
            float _DriftX, _DriftY;
            float _UseOutline, _OutlineThickness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 hash22(float2 p)
            {
                float n = hash21(p);
                return float2(n, hash21(p + n + 19.19));
            }

            float lifeEnvelope(float t, float fadeIn, float visible, float fadeOut, float hidden)
            {
                float inEnd = fadeIn;
                float visEnd = inEnd + visible;
                float outEnd = visEnd + fadeOut;

                if (t < inEnd)
                    return smoothstep(0.0, max(0.0001, fadeIn), t);
                if (t < visEnd)
                    return 1.0;
                if (t < outEnd)
                    return 1.0 - smoothstep(visEnd, outEnd, t);
                return 0.0;
            }

            float animatedScaleFactor(float visibility, float appearScaleMin)
            {
                return lerp(appearScaleMin, 1.0, saturate(visibility));
            }

            float buildMaskFromSignedDistance(float sd)
            {
                float fillMask = 1.0 - smoothstep(0.0, max(0.0001, _EdgeSoftness), sd);
                float outlineMask = 1.0 - smoothstep(_OutlineThickness, _OutlineThickness + max(0.0001, _EdgeSoftness), abs(sd));
                return (_UseOutline > 0.5) ? outlineMask : fillMask;
            }

            float computeCellContribution(float2 cell, float2 gridUV, float time)
            {
                if (hash21(cell + 1.37) > _FillRate)
                    return 0.0;

                float cycleLen = _FadeInTime + _VisibleTime + _FadeOutTime + _HiddenTime;
                float lifeSpeed = _GlobalAnimSpeed * lerp(0.85, 1.15, hash21(cell + 7.21));
                float phase = hash21(cell + 11.93);
                float localT = frac((time * lifeSpeed) / max(0.0001, cycleLen) + phase) * cycleLen;
                float visibility = lifeEnvelope(localT, _FadeInTime, _VisibleTime, _FadeOutTime, _HiddenTime);
                if (visibility <= 0.0001)
                    return 0.0;

                float2 centerOffset = (hash22(cell + 4.73) - 0.5) * _CenterJitter;
                float baseScale = lerp(_MinScale, _MaxScale, hash21(cell + 9.11));
                float animatedScale = baseScale * animatedScaleFactor(visibility, _AppearScaleMin);

                float2 p = (gridUV - cell) - 0.5;
                p -= centerOffset;
                p /= max(0.0001, animatedScale);

                float sd = length(p) - 0.5;
                float mask = buildMaskFromSignedDistance(sd);
                return mask * visibility;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv + float2(_DriftX, _DriftY) * _Time.y;
                float2 gridUV = uv * float2(_GridScaleX, _GridScaleY);
                float2 baseCell = floor(gridUV);

                float total = 0.0;
                [unroll]
                for (int oy = -1; oy <= 1; oy++)
                {
                    [unroll]
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        total += computeCellContribution(baseCell + float2(ox, oy), gridUV, _Time.y);
                    }
                }

                float alpha = saturate(total * _MasterOpacity * _Color.a);
                return float4(_Color.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
