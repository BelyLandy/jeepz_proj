using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SlimeLink2D : MonoBehaviour
{
    [Header("Endpoints")]
    public Transform A;
    public Transform B;

    [Tooltip("If enabled, radii are taken from SpriteRenderer.bounds.extents.x (good for circles).")]
    public bool AutoRadius = true;
    public SpriteRenderer ASprite;
    public SpriteRenderer BSprite;

    [Min(0.0001f)] public float RadiusA = 0.5f;
    [Min(0.0001f)] public float RadiusB = 0.5f;

    [Header("Visibility")]
    [Min(2)] public int Segments = 60;
    [Min(0.01f)] public float MaxDistance = 3.0f;
    [Min(0.01f)] public float DistancePower = 0.7f;

    [Header("Rounded Caps")]
    public bool RoundedCaps = true;
    [Range(2, 32)] public int CapSegments = 16;

    [Header("Shape")]
    [Range(1f, 89f)] public float MaxAttachAngleDeg = 80f;
    [Range(0.5f, 45f)] public float MinAttachAngleDeg = 18f;

    [Range(0f, 2f)] public float HandleStrength = 0.85f;
    [Range(0f, 2f)] public float InwardPull = 1.2f;

    [Tooltip("1 = no neck pinch, lower = stronger pinch")]
    [Range(0.3f, 1f)] public float NeckTightness = 0.9f;

    [Header("Neck center (where it is narrowest)")]
    [Tooltip("0 = at A, 1 = at B. Default center.")]
    [Range(0f, 1f)] public float NeckCenter01 = 0.5f;

    [Tooltip("Width of influence around NeckCenter01. Smaller = more local pinch.")]
    [Range(0.2f, 2f)] public float NeckSpread = 1.0f;

    [Tooltip("Thickness profile around NeckCenter01 (can go above 1).")]
    public AnimationCurve ThicknessOverT =
        new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.5f, 0.7f), new Keyframe(1, 1));

    [Header("Merge when close (become one blob)")]
    public bool EnableMerge = true;
    [Range(0.5f, 2.0f)] public float MergeStartFactor = 2.0f;
    [Range(0.4f, 2.0f)] public float MergeEndFactor = 1.0f;
    [Range(60f, 89f)] public float MergeMaxAngleDeg = 89f;
    [Range(0f, 2f)] public float MergeInwardPull = 0.15f;
    [Range(1f, 2.5f)] public float MergeThicknessMultiplier = 1.35f;

    [Header("Jiggle (spring)")]
    public bool SimulateInEditMode = true;
    [Min(0f)] public float Stiffness = 40f;
    [Min(0f)] public float Damping = 6f;

    [Header("Gizmos")]
    public bool DrawNeckCenterGizmo = true;

    Mesh _mesh;

    Vector2 _midSim;
    Vector2 _midVel;
    bool _midInited;

#if UNITY_EDITOR
    double _lastEditorTime;
#endif

    void OnEnable()
    {
        EnsureMesh();

#if UNITY_EDITOR
        _lastEditorTime = EditorApplication.timeSinceStartup;
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
    }

    void Update()
    {
        if (Application.isPlaying)
            Step(Time.deltaTime);
        else
            EnsureMesh();
    }

#if UNITY_EDITOR
    void EditorTick()
    {
        if (!SimulateInEditMode) return;
        if (Application.isPlaying) return;

        double t = EditorApplication.timeSinceStartup;
        float dt = (float)(t - _lastEditorTime);
        _lastEditorTime = t;

        dt = Mathf.Clamp(dt, 0f, 0.05f);
        Step(dt);
    }
#endif

    void EnsureMesh()
    {
        if (_mesh != null) return;

        var mf = GetComponent<MeshFilter>();
        _mesh = mf.sharedMesh;

        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = "SlimeLink2D_Mesh";
            _mesh.MarkDynamic();
            mf.sharedMesh = _mesh;
        }
    }

    void TryAutoRadii()
    {
        if (!AutoRadius) return;

        if (ASprite == null && A != null) ASprite = A.GetComponent<SpriteRenderer>();
        if (BSprite == null && B != null) BSprite = B.GetComponent<SpriteRenderer>();

        if (ASprite != null) RadiusA = Mathf.Max(0.0001f, ASprite.bounds.extents.x);
        if (BSprite != null) RadiusB = Mathf.Max(0.0001f, BSprite.bounds.extents.x);
    }

    void Clear()
    {
        if (_mesh != null) _mesh.Clear();
    }

    static Vector2 Perp(Vector2 v) => new Vector2(-v.y, v.x);

    static Vector2 Rotate(Vector2 v, float rad)
    {
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1f - t;
        return (u * u * u) * p0
             + (3f * u * u * t) * p1
             + (3f * u * t * t) * p2
             + (t * t * t) * p3;
    }

    static Vector2 TangentFor(Vector2 radial, Vector2 toward)
    {
        Vector2 tan = Perp(radial).normalized;
        if (Vector2.Dot(tan, toward) < 0f) tan = -tan;
        return tan;
    }

    static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    float ProjectU01(Vector2 a, Vector2 dir, float dist, Vector2 p)
    {
        return Mathf.Clamp01(Vector2.Dot(p - a, dir) / Mathf.Max(1e-5f, dist));
    }

    Vector2 MidAtT(
        Vector2 pA_top, Vector2 c1_top, Vector2 c2_top, Vector2 pB_top,
        Vector2 pA_bot, Vector2 c1_bot, Vector2 c2_bot, Vector2 pB_bot,
        float t)
    {
        Vector2 top = Bezier(pA_top, c1_top, c2_top, pB_top, t);
        Vector2 bot = Bezier(pA_bot, c1_bot, c2_bot, pB_bot, t);
        return (top + bot) * 0.5f;
    }

    float SolveTForU(
        Vector2 a, Vector2 dir, float dist, float uTarget,
        Vector2 pA_top, Vector2 c1_top, Vector2 c2_top, Vector2 pB_top,
        Vector2 pA_bot, Vector2 c1_bot, Vector2 c2_bot, Vector2 pB_bot)
    {
        float lo = 0f, hi = 1f;

        for (int it = 0; it < 18; it++)
        {
            float midT = (lo + hi) * 0.5f;
            Vector2 midP = MidAtT(
                pA_top, c1_top, c2_top, pB_top,
                pA_bot, c1_bot, c2_bot, pB_bot,
                midT);

            float u = ProjectU01(a, dir, dist, midP);

            if (u < uTarget) lo = midT;
            else hi = midT;
        }

        return (lo + hi) * 0.5f;
    }

    void Step(float dt)
    {
        EnsureMesh();

        if (A == null || B == null)
        {
            Clear();
            _midInited = false;
            return;
        }

        TryAutoRadii();

        Vector2 a = A.position;
        Vector2 b = B.position;

        float dist = Vector2.Distance(a, b);
        if (dist < 1e-5f || dist > MaxDistance)
        {
            Clear();
            _midInited = false;
            return;
        }

        Vector2 dir = (b - a) / dist;
        Vector2 normal = Perp(dir).normalized;

        float closeness = Mathf.Clamp01(1f - dist / MaxDistance);
        float distMul = Mathf.Pow(closeness, DistancePower);

        // Merge
        float mergeAmount = 0f;
        float thicknessMul = 1f;
        float inwardPullEff = InwardPull;
        float angleMaxEff = MaxAttachAngleDeg;

        if (EnableMerge)
        {
            float sumR = RadiusA + RadiusB;
            float startD = sumR * MergeStartFactor;
            float endD = sumR * MergeEndFactor;

            if (startD < endD) { float tmp = startD; startD = endD; endD = tmp; }

            float t = Mathf.InverseLerp(startD, endD, dist);
            mergeAmount = Smooth01(t);

            angleMaxEff = Mathf.Lerp(MaxAttachAngleDeg, MergeMaxAngleDeg, mergeAmount);
            inwardPullEff = Mathf.Lerp(InwardPull, MergeInwardPull, mergeAmount);
            thicknessMul = Mathf.Lerp(1f, MergeThicknessMultiplier, mergeAmount);
        }

        // spring center
        Vector2 targetMid = (a + b) * 0.5f;
        if (!_midInited)
        {
            _midSim = targetMid;
            _midVel = Vector2.zero;
            _midInited = true;
        }

        Vector2 x = _midSim - targetMid;
        Vector2 accel = -Stiffness * x - Damping * _midVel;
        _midVel += accel * dt;
        _midSim += _midVel * dt;

        Vector2 jiggleOffset = (targetMid - _midSim) * 0.35f;

        float angleDeg = Mathf.Lerp(MinAttachAngleDeg, angleMaxEff, distMul);
        angleDeg = Mathf.Min(angleDeg, 89f);
        float ang = angleDeg * Mathf.Deg2Rad;

        // A endpoints
        Vector2 rA_top = Rotate(dir, +ang);
        Vector2 rA_bot = Rotate(dir, -ang);
        Vector2 pA_top = a + rA_top * RadiusA;
        Vector2 pA_bot = a + rA_bot * RadiusA;

        // B endpoints (signs matter)
        Vector2 rB_top = Rotate(-dir, -ang);
        Vector2 rB_bot = Rotate(-dir, +ang);
        Vector2 pB_top = b + rB_top * RadiusB;
        Vector2 pB_bot = b + rB_bot * RadiusB;

        Vector2 inwardTop = -normal;
        Vector2 inwardBot = normal;

        float handleLen = HandleStrength * Mathf.Min(RadiusA, RadiusB) * Mathf.Lerp(0.25f, 1f, distMul);

        // controls top
        Vector2 vTop = (pB_top - pA_top);
        Vector2 tA_top = TangentFor(rA_top, vTop);
        Vector2 tB_top = TangentFor(rB_top, -vTop);

        Vector2 c1_top = pA_top + tA_top * handleLen + inwardTop * (handleLen * 0.6f * inwardPullEff) + jiggleOffset;
        Vector2 c2_top = pB_top - tB_top * handleLen + inwardTop * (handleLen * 0.6f * inwardPullEff) + jiggleOffset;

        // controls bottom
        Vector2 vBot = (pB_bot - pA_bot);
        Vector2 tA_bot = TangentFor(rA_bot, vBot);
        Vector2 tB_bot = TangentFor(rB_bot, -vBot);

        Vector2 c1_bot = pA_bot + tA_bot * handleLen + inwardBot * (handleLen * 0.6f * inwardPullEff) + jiggleOffset;
        Vector2 c2_bot = pB_bot - tB_bot * handleLen + inwardBot * (handleLen * 0.6f * inwardPullEff) + jiggleOffset;

        BuildMeshCapsAndStrip(a, b, dir, dist, ang,
            pA_top, pA_bot, pB_top, pB_bot,
            c1_top, c2_top, c1_bot, c2_bot,
            distMul, thicknessMul);
    }

    void BuildMeshCapsAndStrip(
        Vector2 a, Vector2 b, Vector2 dir, float dist, float ang,
        Vector2 pA_top, Vector2 pA_bot, Vector2 pB_top, Vector2 pB_bot,
        Vector2 c1_top, Vector2 c2_top, Vector2 c1_bot, Vector2 c2_bot,
        float distMul, float thicknessMul)
    {
        int s = Mathf.Max(2, Segments);

        var topCurve = new Vector2[s + 1];
        var botCurve = new Vector2[s + 1];

        // IMPORTANT: sample by u (projection along A->B), not by Bezier t.
        for (int i = 0; i <= s; i++)
        {
            float u = (float)i / s;

            float tParam;
            if (i == 0) tParam = 0f;
            else if (i == s) tParam = 1f;
            else
            {
                tParam = SolveTForU(
                    a, dir, dist, u,
                    pA_top, c1_top, c2_top, pB_top,
                    pA_bot, c1_bot, c2_bot, pB_bot
                );
            }

            Vector2 top = Bezier(pA_top, c1_top, c2_top, pB_top, tParam);
            Vector2 bot = Bezier(pA_bot, c1_bot, c2_bot, pB_bot, tParam);

            float spread = Mathf.Max(0.0001f, NeckSpread);
            float tCurve = Mathf.Clamp01((u - NeckCenter01) / spread + 0.5f);

            float thick = Mathf.Max(0f, ThicknessOverT.Evaluate(tCurve)) * thicknessMul;

            float neck = Mathf.Lerp(NeckTightness, 1f, distMul) * thick;
            if (i == 0 || i == s) neck = 1f;

            Vector2 mid = (top + bot) * 0.5f;
            top = Vector2.Lerp(mid, top, neck);
            bot = Vector2.Lerp(mid, bot, neck);

            topCurve[i] = top;
            botCurve[i] = bot;
        }

        int capInner = RoundedCaps ? Mathf.Max(0, CapSegments - 1) : 0;
        int stripVertCount = (s + 1) * 2;
        int totalVertCount = stripVertCount + (RoundedCaps ? capInner * 2 : 0);

        var verts = new Vector3[totalVertCount];
        var uvs = new Vector2[totalVertCount];
        var norms = new Vector3[totalVertCount];

        Vector3 ToLocal(Vector2 w) => transform.InverseTransformPoint(new Vector3(w.x, w.y, 0f));

        for (int i = 0; i <= s; i++)
        {
            float u = (float)i / s;

            int topIdx = i * 2;
            int botIdx = i * 2 + 1;

            verts[topIdx] = ToLocal(topCurve[i]);
            verts[botIdx] = ToLocal(botCurve[i]);

            uvs[topIdx] = new Vector2(u, 1f);
            uvs[botIdx] = new Vector2(u, 0f);

            norms[topIdx] = Vector3.back;
            norms[botIdx] = Vector3.back;
        }

        int baseA = stripVertCount;
        int baseB = stripVertCount + capInner;

        if (RoundedCaps)
        {
            for (int j = 1; j < CapSegments; j++)
            {
                float k = (float)j / CapSegments;
                float phi = Mathf.Lerp(-ang, +ang, k);
                Vector2 p = a + Rotate(dir, phi) * RadiusA;

                int idx = baseA + (j - 1);
                verts[idx] = ToLocal(p);
                uvs[idx] = new Vector2(0f, k);
                norms[idx] = Vector3.back;
            }

            for (int j = 1; j < CapSegments; j++)
            {
                float k = (float)j / CapSegments;
                float phi = Mathf.Lerp(-ang, +ang, k);
                Vector2 p = b + Rotate(-dir, phi) * RadiusB;

                int idx = baseB + (j - 1);
                verts[idx] = ToLocal(p);
                uvs[idx] = new Vector2(1f, 1f - k);
                norms[idx] = Vector3.back;
            }
        }

        var tri = new List<int>(s * 6 + 6 * CapSegments * 2);

        int startSeg = RoundedCaps ? 1 : 0;
        int endSeg = RoundedCaps ? (s - 1) : s;

        startSeg = Mathf.Clamp(startSeg, 0, s - 1);
        endSeg = Mathf.Clamp(endSeg, startSeg + 1, s);

        for (int i = startSeg; i < endSeg; i++)
        {
            int top0 = i * 2;
            int bot0 = i * 2 + 1;
            int top1 = (i + 1) * 2;
            int bot1 = (i + 1) * 2 + 1;

            tri.Add(top0); tri.Add(top1); tri.Add(bot0);
            tri.Add(top1); tri.Add(bot1); tri.Add(bot0);
        }

        if (RoundedCaps && CapSegments >= 2 && s >= 2)
        {
            // left cap poly: bot0 -> arcA(internal) -> top0 -> top1 -> bot1
            int bot0 = 1;
            int top0 = 0;
            int top1 = 2;
            int bot1 = 3;

            var leftPoly = new List<int>(2 + capInner + 2);
            leftPoly.Add(bot0);
            for (int j = 0; j < capInner; j++) leftPoly.Add(baseA + j);
            leftPoly.Add(top0);
            leftPoly.Add(top1);
            leftPoly.Add(bot1);

            TriangulateCapFanSafe(leftPoly, verts, tri);

            // right cap poly: topS -> arcB(internal) -> botS -> botS-1 -> topS-1
            int topS = s * 2;
            int botS = s * 2 + 1;
            int topSm1 = (s - 1) * 2;
            int botSm1 = (s - 1) * 2 + 1;

            var rightPoly = new List<int>(2 + capInner + 2);
            rightPoly.Add(topS);
            for (int j = 0; j < capInner; j++) rightPoly.Add(baseB + j);
            rightPoly.Add(botS);
            rightPoly.Add(botSm1);
            rightPoly.Add(topSm1);

            TriangulateCapFanSafe(rightPoly, verts, tri);
        }

        _mesh.Clear();
        _mesh.vertices = verts;
        _mesh.uv = uvs;
        _mesh.normals = norms;
        _mesh.triangles = tri.ToArray();
        _mesh.RecalculateBounds();
    }

    static void TriangulateCapFanSafe(List<int> poly, Vector3[] vertsLocal, List<int> outTris)
    {
        for (int i = poly.Count - 1; i >= 1; i--)
            if (poly[i] == poly[i - 1]) poly.RemoveAt(i);

        if (poly.Count < 3) return;

        int anchor = poly[poly.Count - 1];

        for (int i = 0; i < poly.Count - 2; i++)
        {
            int a = anchor;
            int b = poly[i];
            int c = poly[i + 1];

            Vector3 A = vertsLocal[a];
            Vector3 B = vertsLocal[b];
            Vector3 C = vertsLocal[c];
            if ((B - A).sqrMagnitude < 1e-10f || (C - A).sqrMagnitude < 1e-10f || (C - B).sqrMagnitude < 1e-10f)
                continue;

            outTris.Add(a);
            outTris.Add(b);
            outTris.Add(c);
        }
    }

    void OnDrawGizmos()
    {
        if (!DrawNeckCenterGizmo) return;
        if (A == null || B == null) return;

        Vector3 a = A.position;
        Vector3 b = B.position;
        Vector3 p = Vector3.Lerp(a, b, NeckCenter01);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(p + Vector3.up * 2f, p + Vector3.down * 2f);
        Gizmos.DrawSphere(p, 0.03f);
    }
}
