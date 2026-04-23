Shader "Custom/ProceduralShapes/StarLayer"
{
    Properties
    {
        _Color ("Color", Color) = (1.0, 0.96, 0.86, 1.0)
        _MasterOpacity ("Master Opacity", Range(0,1)) = 0.85

        _GridScaleX ("Grid Scale X", Float) = 14
        _GridScaleY ("Grid Scale Y", Float) = 8
        _FillRate ("Fill Rate", Range(0,1)) = 0.26

        _MinScale ("Min Scale", Float) = 0.24
        _MaxScale ("Max Scale", Float) = 0.52

        _EdgeSoftness ("Edge Softness", Float) = 0.045
        _CenterJitter ("Center Jitter", Float) = 0.24

        _GlobalAnimSpeed ("Global Anim Speed", Float) = 1.0
        _FadeInTime ("Fade In Time", Float) = 1.3
        _VisibleTime ("Visible Time", Float) = 2.3
        _FadeOutTime ("Fade Out Time", Float) = 1.2
        _HiddenTime ("Hidden Time", Float) = 2.5
        _AppearScaleMin ("Appear Scale Min", Range(0,1)) = 0.38

        _DriftX ("Drift X", Float) = 0.003
        _DriftY ("Drift Y", Float) = 0.0

        _UseOutline ("Use Outline", Range(0,1)) = 0
        _OutlineThickness ("Outline Thickness", Float) = 0.06

        _StarInnerRadius ("Star Inner Radius", Float) = 0.22
        _StarOuterRadius ("Star Outer Radius", Float) = 0.52
        _StarPointStretchMin ("Star Point Stretch Min", Float) = 0.93
        _StarPointStretchMax ("Star Point Stretch Max", Float) = 1.12
        _StarDeformSpeedMin ("Star Deform Speed Min", Float) = 0.55
        _StarDeformSpeedMax ("Star Deform Speed Max", Float) = 1.0
        _StarOuterIrregularity ("Star Outer Irregularity", Float) = 0.12
        _StarInnerIrregularity ("Star Inner Irregularity", Float) = 0.08
        _StarInnerWobble ("Star Inner Wobble", Float) = 0.05
        _StarJaggedness ("Star Jaggedness", Range(0,1)) = 0.18
        _StarRotSpeedMin ("Star Rotation Speed Min", Float) = -0.08
        _StarRotSpeedMax ("Star Rotation Speed Max", Float) = 0.08
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
            #define STAR_VERTS 10

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

            float _StarInnerRadius, _StarOuterRadius;
            float _StarPointStretchMin, _StarPointStretchMax;
            float _StarDeformSpeedMin, _StarDeformSpeedMax;
            float _StarOuterIrregularity, _StarInnerIrregularity;
            float _StarInnerWobble, _StarJaggedness;
            float _StarRotSpeedMin, _StarRotSpeedMax;

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

            float distToSegment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-5));
                return length(pa - ba * h);
            }

            float deformSpeedForCell(float2 cell)
            {
                return lerp(_StarDeformSpeedMin, _StarDeformSpeedMax, hash21(cell + 15.71));
            }

            float rotationForCell(float2 cell, float time)
            {
                float baseAngle = hash21(cell + 2.37) * TWO_PI;
                float rotSpeed = lerp(_StarRotSpeedMin, _StarRotSpeedMax, hash21(cell + 6.11));
                return baseAngle + time * rotSpeed;
            }

            float outerRadiusAt(float2 cell, float time, int idx, float deformSpeed)
            {
                float fi = (float)idx;
                float baseRnd = hash21(cell + float2(31.7 + fi * 1.13, 7.9 + fi * 2.71));
                float phaseRnd = hash21(cell + float2(61.3 + fi * 3.17, 12.4 + fi * 1.37));
                float anim = sin(time * deformSpeed + phaseRnd * TWO_PI);
                float anim01 = 0.5 + 0.5 * anim;
                float stretch = lerp(_StarPointStretchMin, _StarPointStretchMax, anim01);
                float irregular = lerp(1.0 - _StarOuterIrregularity, 1.0 + _StarOuterIrregularity, baseRnd);
                return _StarOuterRadius * irregular * stretch;
            }

            float innerRadiusAt(float2 cell, float time, int idx, float deformSpeed)
            {
                float fi = (float)idx;
                float baseRnd = hash21(cell + float2(91.2 + fi * 0.97, 28.5 + fi * 2.19));
                float phaseRnd = hash21(cell + float2(44.8 + fi * 2.41, 73.6 + fi * 1.61));
                float anim = sin(time * deformSpeed * 0.8 + phaseRnd * TWO_PI);
                float anim01 = 0.5 + 0.5 * anim;
                float irregular = lerp(1.0 - _StarInnerIrregularity, 1.0 + _StarInnerIrregularity, baseRnd);
                float wobble = lerp(1.0 - _StarInnerWobble, 1.0 + _StarInnerWobble, anim01);
                return _StarInnerRadius * irregular * wobble;
            }

            float vertexAngleOffset(float2 cell, int idx)
            {
                float fi = (float)idx;
                float aRnd = hash21(cell + float2(13.11 + fi * 1.91, 55.73 + fi * 1.33));
                float angleStep = TWO_PI / 10.0;
                return (aRnd - 0.5) * _StarJaggedness * angleStep * 0.35;
            }

            float2 getStarVertex(float2 cell, float time, int idx)
            {
                float deformSpeed = deformSpeedForCell(cell);
                float rotation = rotationForCell(cell, time);
                int pairIdx = idx / 2;
                bool isOuter = ((idx % 2) == 0);
                float angleStep = TWO_PI / 10.0;
                float angle = rotation + (float)idx * angleStep + vertexAngleOffset(cell, idx);
                float radius = isOuter ? outerRadiusAt(cell, time, pairIdx, deformSpeed) : innerRadiusAt(cell, time, pairIdx, deformSpeed);
                return float2(cos(angle), sin(angle)) * radius;
            }

            bool pointInPolygonStar(float2 p, float2 cell, float time)
            {
                bool inside = false;
                float2 prev = getStarVertex(cell, time, STAR_VERTS - 1);
                [unroll]
                for (int i = 0; i < STAR_VERTS; i++)
                {
                    float2 curr = getStarVertex(cell, time, i);
                    bool intersect = ((curr.y > p.y) != (prev.y > p.y)) &&
                                     (p.x < (prev.x - curr.x) * (p.y - curr.y) / max(prev.y - curr.y, 1e-5) + curr.x);
                    if (intersect)
                        inside = !inside;
                    prev = curr;
                }
                return inside;
            }

            float starSignedDistance(float2 p, float2 cell, float time)
            {
                float minDist = 1e6;
                float2 prev = getStarVertex(cell, time, STAR_VERTS - 1);
                [unroll]
                for (int i = 0; i < STAR_VERTS; i++)
                {
                    float2 curr = getStarVertex(cell, time, i);
                    minDist = min(minDist, distToSegment(p, prev, curr));
                    prev = curr;
                }

                return pointInPolygonStar(p, cell, time) ? -minDist : minDist;
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

                float sd = starSignedDistance(p, cell, time);
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
