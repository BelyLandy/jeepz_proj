using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VerletCloth2D : MonoBehaviour
{
    [Header("Cloth Grid")]
    [Min(1)] public int columns = 40;          // кол-во ячеек по X
    [Min(1)] public int rows = 25;             // кол-во ячеек по Y
    [Min(0.001f)] public float spacing = 0.2f; // расстояние между точками
    public Vector2 start = new Vector2(-4f, 3f); // левый верхний угол ткани (в world)

    [Header("Pinning (верхний ряд)")]
    [Min(1)] public int pinEvery = 2;          // как в статье: пинить каждую 2-ю точку в верхнем ряду

    [Header("Simulation")]
    public Vector2 gravity = new Vector2(0, -9.81f);
    [Range(0f, 0.25f)] public float drag = 0.01f;               // "потеря энергии"
    [Range(0.1f, 1f)] public float constraintStiffness = 1f;    // жесткость (1 = максимально)
    [Min(1)] public int solverIterations = 8;                   // итерации удовлетворения constraints

    [Header("Interaction")]
    [Min(0.01f)] public float cursorRadius = 0.5f;
    [Min(0.01f)] public float cursorMin = 0.1f;
    [Min(0.01f)] public float cursorMax = 2.0f;
    [Min(0.001f)] public float cursorWheelStep = 0.15f;

    [Tooltip("Ограничение 'скорости' при перетаскивании мышью (аналог elasticity из статьи)")]
    [Min(0.001f)] public float dragElasticity = 0.6f;

    [Tooltip("Если true — по правому клику вырезаем ДЫРУ: удаляем точки + перестраиваем треугольники.")]
    public bool removeParticlesOnCut = true;

    [Tooltip("Если false — по правому клику просто рвём constraints (получится разрыв/надрез без настоящей дырки в меше).")]
    public bool breakConstraintsOnCut = true;

    [Header("Optional bounds")]
    public bool keepInsideWorldBounds = false;
    public Rect worldBounds = new Rect(-10, -5, 20, 12);

    [Header("Debug")]
    public bool drawConstraintsGizmos = false;

    struct Particle
    {
        public Vector2 pos;
        public Vector2 prevPos;
        public Vector2 initPos;
        public bool pinned;
        public bool removed;
    }

    struct Constraint
    {
        public int a, b;
        public float restLength;
        public bool active;
    }

    private Particle[] _p;
    private List<Constraint> _c;

    private Mesh _mesh;
    private Vector3[] _verts;
    private Vector2[] _uvs;
    private int[] _tris;

    private Camera _cam;
    private Vector2 _prevMouseWorld;
    private bool _meshDirtyTriangles;

    private int Idx(int x, int y) => x + y * (columns + 1);

    private void Awake()
    {
        _cam = Camera.main;
        BuildCloth();
        _prevMouseWorld = GetMouseWorld();
    }

    private void BuildCloth()
    {
        int vx = columns + 1;
        int vy = rows + 1;
        int vCount = vx * vy;

        _p = new Particle[vCount];
        _c = new List<Constraint>((columns * vy) + (rows * vx));

        for (int y = 0; y < vy; y++)
        {
            for (int x = 0; x < vx; x++)
            {
                int i = Idx(x, y);
                Vector2 pos = start + new Vector2(x * spacing, -y * spacing);

                _p[i] = new Particle
                {
                    pos = pos,
                    prevPos = pos,
                    initPos = pos,
                    pinned = (y == 0 && (x % pinEvery == 0)),
                    removed = false
                };

                if (x > 0)
                    AddConstraint(i, Idx(x - 1, y), spacing);

                if (y > 0)
                    AddConstraint(i, Idx(x, y - 1), spacing);
            }
        }

        _mesh = new Mesh { name = "VerletCloth2D_Mesh" };
        GetComponent<MeshFilter>().sharedMesh = _mesh;

        _verts = new Vector3[vCount];
        _uvs = new Vector2[vCount];

        for (int y = 0; y < vy; y++)
        {
            for (int x = 0; x < vx; x++)
            {
                int i = Idx(x, y);
                _verts[i] = _p[i].pos;
                _uvs[i] = new Vector2((float)x / columns, 1f - (float)y / rows);
            }
        }

        RebuildTriangles();

        _mesh.vertices = _verts;
        _mesh.uv = _uvs;
        _mesh.triangles = _tris;
        _mesh.RecalculateBounds();
    }

    private void AddConstraint(int a, int b, float restLength)
    {
        _c.Add(new Constraint
        {
            a = a,
            b = b,
            restLength = restLength,
            active = true
        });
    }

    private void FixedUpdate()
    {
        HandleInput();
        Simulate(Time.fixedDeltaTime);
        UpdateMesh();
    }

    private void HandleInput()
    {
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.0001f)
            cursorRadius = Mathf.Clamp(cursorRadius + wheel * cursorWheelStep, cursorMin, cursorMax);

        Vector2 mouseWorld = GetMouseWorld();
        Vector2 mouseDelta = mouseWorld - _prevMouseWorld;

        if (Input.GetMouseButton(0))
            DragAt(mouseWorld, mouseDelta);

        /*if (Input.GetMouseButtonDown(1) || Input.GetMouseButton(1))
            CutAt(mouseWorld);*/

        _prevMouseWorld = mouseWorld;
    }

    private Vector2 GetMouseWorld()
    {
        if (_cam == null) _cam = Camera.main;
        Vector3 m = Input.mousePosition;
        Vector3 w = _cam.ScreenToWorldPoint(m);
        return new Vector2(w.x, w.y);
    }

    private void DragAt(Vector2 mouseWorld, Vector2 mouseDelta)
    {
        Vector2 d = mouseDelta;
        d.x = Mathf.Clamp(d.x, -dragElasticity, dragElasticity);
        d.y = Mathf.Clamp(d.y, -dragElasticity, dragElasticity);

        float r2 = cursorRadius * cursorRadius;

        for (int i = 0; i < _p.Length; i++)
        {
            var p = _p[i];
            if (p.removed || p.pinned) continue;

            Vector2 diff = p.pos - mouseWorld;
            if (diff.sqrMagnitude <= r2)
            {
                p.prevPos = p.pos - d;
                _p[i] = p;
            }
        }
    }

    private void CutAt(Vector2 mouseWorld)
    {
        float r2 = cursorRadius * cursorRadius;
        bool anyChanged = false;

        if (removeParticlesOnCut)
        {
            for (int i = 0; i < _p.Length; i++)
            {
                var p = _p[i];
                if (p.removed || p.pinned) continue;

                Vector2 diff = p.pos - mouseWorld;
                if (diff.sqrMagnitude <= r2)
                {
                    p.removed = true;
                    _p[i] = p;
                    anyChanged = true;
                }
            }
        }

        if (breakConstraintsOnCut)
        {
            for (int i = 0; i < _c.Count; i++)
            {
                var c = _c[i];
                if (!c.active) continue;

                var pa = _p[c.a];
                var pb = _p[c.b];

                if (pa.removed || pb.removed)
                {
                    c.active = false;
                    _c[i] = c;
                    anyChanged = true;
                    continue;
                }

                bool hitA = (pa.pos - mouseWorld).sqrMagnitude <= r2;
                bool hitB = (pb.pos - mouseWorld).sqrMagnitude <= r2;

                if (hitA || hitB)
                {
                    c.active = false;
                    _c[i] = c;
                    anyChanged = true;
                }
            }
        }

        if (anyChanged)
        {
            _meshDirtyTriangles = true;
        }
    }

    private void Simulate(float dt)
    {
        float dt2 = dt * dt;
        float damp = 1f - drag;

        for (int i = 0; i < _p.Length; i++)
        {
            var p = _p[i];
            if (p.removed) continue;

            if (p.pinned)
            {
                p.pos = p.initPos;
                p.prevPos = p.initPos;
                _p[i] = p;
                continue;
            }

            Vector2 velocity = (p.pos - p.prevPos) * damp;
            Vector2 newPos = p.pos + velocity + gravity * damp * dt2;

            p.prevPos = p.pos;
            p.pos = newPos;

            if (keepInsideWorldBounds)
                KeepInside(ref p);

            _p[i] = p;
        }

        for (int iter = 0; iter < solverIterations; iter++)
            SatisfyConstraints();
    }

    private void KeepInside(ref Particle p)
    {
        Vector2 clamped = p.pos;
        clamped.x = Mathf.Clamp(clamped.x, worldBounds.xMin, worldBounds.xMax);
        clamped.y = Mathf.Clamp(clamped.y, worldBounds.yMin, worldBounds.yMax);

        if (clamped != p.pos)
        {
            p.pos = clamped;
            p.prevPos = clamped;
        }
    }

    private void SatisfyConstraints()
    {
        for (int i = 0; i < _c.Count; i++)
        {
            var c = _c[i];
            if (!c.active) continue;

            var pa = _p[c.a];
            var pb = _p[c.b];

            if (pa.removed || pb.removed)
            {
                c.active = false;
                _c[i] = c;
                continue;
            }

            Vector2 diff = pa.pos - pb.pos;
            float dist = diff.magnitude;
            if (dist < 1e-6f) continue;

            float diffFactor = (c.restLength - dist) / dist;
            Vector2 offset = diff * (diffFactor * 0.5f * constraintStiffness);

            if (!pa.pinned) pa.pos += offset;
            if (!pb.pinned) pb.pos -= offset;

            _p[c.a] = pa;
            _p[c.b] = pb;
        }
    }

    private void UpdateMesh()
    {
        if (_mesh == null) return;

        if (_meshDirtyTriangles)
        {
            RebuildTriangles();
            _mesh.triangles = _tris;
            _meshDirtyTriangles = false;
        }

        for (int i = 0; i < _p.Length; i++)
        {
            _verts[i] = new Vector3(_p[i].pos.x, _p[i].pos.y, 0f);
        }

        _mesh.vertices = _verts;
        _mesh.RecalculateBounds();
    }

    private void RebuildTriangles()
    {
        var tris = new List<int>(columns * rows * 6);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                int a = Idx(x, y);
                int b = Idx(x + 1, y);
                int c = Idx(x, y + 1);
                int d = Idx(x + 1, y + 1);

                if (!_p[a].removed && !_p[b].removed && !_p[c].removed)
                {
                    tris.Add(a);
                    tris.Add(b);
                    tris.Add(c);
                }

                if (!_p[b].removed && !_p[d].removed && !_p[c].removed)
                {
                    tris.Add(b);
                    tris.Add(d);
                    tris.Add(c);
                }
            }
        }

        _tris = tris.ToArray();
    }

    private void OnDrawGizmos()
    {
        if (!drawConstraintsGizmos || _p == null || _c == null) return;

        Gizmos.color = Color.black;
        for (int i = 0; i < _c.Count; i++)
        {
            var c = _c[i];
            if (!c.active) continue;
            var a = _p[c.a];
            var b = _p[c.b];
            if (a.removed || b.removed) continue;

            Gizmos.DrawLine(a.pos, b.pos);
        }

        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Vector2 mw = GetMouseWorld();
            Gizmos.DrawWireSphere(mw, cursorRadius);
        }
    }

    public void CutCircle(Vector2 center, float radius)
    {
        float r2 = radius * radius;
        bool anyChanged = false;

        if (removeParticlesOnCut)
        {
            for (int i = 0; i < _p.Length; i++)
            {
                var p = _p[i];
                if (p.removed || p.pinned) continue;

                if ((p.pos - center).sqrMagnitude <= r2)
                {
                    p.removed = true;
                    _p[i] = p;
                    anyChanged = true;
                }
            }
        }

        if (breakConstraintsOnCut)
        {
            for (int i = 0; i < _c.Count; i++)
            {
                var c = _c[i];
                if (!c.active) continue;

                var pa = _p[c.a];
                var pb = _p[c.b];

                if (pa.removed || pb.removed)
                {
                    c.active = false;
                    _c[i] = c;
                    anyChanged = true;
                    continue;
                }

                bool hitA = (pa.pos - center).sqrMagnitude <= r2;
                bool hitB = (pb.pos - center).sqrMagnitude <= r2;

                if (hitA || hitB)
                {
                    c.active = false;
                    _c[i] = c;
                    anyChanged = true;
                }
            }
        }

        if (anyChanged) _meshDirtyTriangles = true;
    }

}
