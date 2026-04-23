Shader "Custom/ProceduralShapes/TriangleLayer"
{
    Properties
    {
        _Color ("Color", Color) = (0.85, 0.95, 1.0, 1.0)
        _MasterOpacity ("Master Opacity", Range(0,1)) = 0.7

        _GridScaleX ("Grid Scale X", Float) = 18
        _GridScaleY ("Grid Scale Y", Float) = 10
        _FillRate ("Fill Rate", Range(0,1)) = 0.28

        _MinScale ("Min Scale", Float) = 0.22
        _MaxScale ("Max Scale", Float) = 0.48

        _EdgeSoftness ("Edge Softness", Float) = 0.05
        _CenterJitter ("Center Jitter", Float) = 0.26

        _GlobalAnimSpeed ("Global Anim Speed", Float) = 1.0
        _FadeInTime ("Fade In Time", Float) = 1.2
        _VisibleTime ("Visible Time", Float) = 2.0
        _FadeOutTime ("Fade Out Time", Float) = 1.1
        _HiddenTime ("Hidden Time", Float) = 2.4
        _AppearScaleMin ("Appear Scale Min", Range(0,1)) = 0.4

        _DriftX ("Drift X", Float) = 0.002
        _DriftY ("Drift Y", Float) = 0.0

        _UseOutline ("Use Outline", Range(0,1)) = 0
        _OutlineThickness ("Outline Thickness", Float) = 0.06

        _RotSpeedMin ("Rotation Speed Min", Float) = 0.08
        _RotSpeedMax ("Rotation Speed Max", Float) = 0.22
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

            #define PI 3.14159265359
            #define TWO_PI 6.28318530718

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
            float _RotSpeedMin, _RotSpeedMax;

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

            float2 rotate2D(float2 p, float a)
            {
                float s = sin(a);
                float c = cos(a);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
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

            float distToSegment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-5));
                return length(pa - ba * h);
            }

            float cross2(float2 a, float2 b)
            {
                return a.x * b.y - a.y * b.x;
            }

            bool pointInTriangle(float2 p, float2 a, float2 b, float2 c)
            {
                float s1 = cross2(b - a, p - a);
                float s2 = cross2(c - b, p - b);
                float s3 = cross2(a - c, p - c);
                bool hasNeg = (s1 < 0.0) || (s2 < 0.0) || (s3 < 0.0);
                bool hasPos = (s1 > 0.0) || (s2 > 0.0) || (s3 > 0.0);
                return !(hasNeg && hasPos);
            }

            float triangleSignedDistance(float2 p)
            {
                float2 v0 = float2(0.0, 0.58);
                float2 v1 = float2(-0.50, -0.29);
                float2 v2 = float2(0.50, -0.29);

                float d0 = distToSegment(p, v0, v1);
                float d1 = distToSegment(p, v1, v2);
                float d2 = distToSegment(p, v2, v0);
                float minDist = min(d0, min(d1, d2));

                return pointInTriangle(p, v0, v1, v2) ? -minDist : minDist;
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

                float rotSpeed = lerp(_RotSpeedMin, _RotSpeedMax, hash21(cell + 13.37));
                float baseAngle = hash21(cell + 17.17) * TWO_PI;
                p = rotate2D(p, time * rotSpeed + baseAngle);

                float sd = triangleSignedDistance(p);
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
