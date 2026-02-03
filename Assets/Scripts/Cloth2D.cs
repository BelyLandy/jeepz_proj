using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Cloth2D : MonoBehaviour
{
    [Header("Grid (mesh & nodes)")]
    [Min(2)] public int width = 12;
    [Min(2)] public int height = 8;
    [Min(0.01f)] public float spacing = 0.2f;

    [Header("Joints (internal cloth)")]
    public bool useSpring = false;
    [Range(0, 20)] public float springFrequency = 5f;
    [Range(0, 1)]  public float springDamping = 0.3f;
    public bool enableCollisionOnJoints = false;

    [Header("Pins (freeze edges)")]
    public bool pinTop = true;
    public bool pinLeft = false;
    public bool pinRight = false;

    [Header("Nodes")]
    [Tooltip("Если включено — узлы берут слой у родителя; иначе используется Nodes Layer.")]
    public bool useParentLayerForNodes = true;
    [Tooltip("Резервный слой для узлов, если useParentLayerForNodes = false.")]
    public int nodesLayer = 0;

    [Min(0.001f)] public float nodeRadius = 0.05f;
    public float nodeGravityScale = 1f;

    public float nodeLinearDrag = 1.2f;
    public float nodeAngularDrag = 0.4f;
    [Min(0.001f)] public float nodeMass = 0.08f;

    [Header("Stability & Tuning")]
    [Tooltip("Добавить диагональные связи (shear) для противодействия сдвигу.")]
    public bool addShearLinks = true;
    [Tooltip("Добавить «жёсткие» связи ко вторым соседям (bend) для сопротивления изгибу.")]
    public bool addBendLinks = true;
    [Tooltip("Для DistanceJoint2D: ограничивать только растяжение, позволяя сжиматься без дрожи.")]
    public bool useMaxDistanceOnly = true;
    [Range(0.1f, 4f)] public float shearSpringMul = 1.0f;
    [Range(0.1f, 4f)] public float bendSpringMul  = 1.6f;
    [Tooltip("Доп. демпфирование к нижним рядам (0 = нет).")]
    public float verticalDampingGradient = 0.8f;
    [Tooltip("Заморозить вращение узлов для предотвращения перекрутов.")]
    public bool freezeNodeRotation = true;

    [Header("Cape attach")]
    public Rigidbody2D attachTarget; 
    public bool attachOnStart = true;

    [Tooltip("Режим 'кинематических пинов' верхнего ряда к плечам героя (рекомендуется).")]
    public bool useKinematicPins = true;

    [Tooltip("Если useKinematicPins выключен — крепим джойнтами.")]
    public bool attachAsSpring = true;
    [Min(1)] public int attachStride = 2;

    [Tooltip("Локальные точки на герое (плечи).")]
    public Vector2 leftShoulderLocal  = new Vector2(-0.25f, 0.5f);
    public Vector2 rightShoulderLocal = new Vector2( 0.25f, 0.5f);

    [Header("Cape offset")]
    [Tooltip("Доп. сдвиг линии крепления плаща в ЛОКАЛЬНЫХ координатах героя (вправо/вверх положительный).")]
    public Vector2 capeOffsetLocal = Vector2.zero;

    Mesh _mesh;
    Vector3[] _verts;
    Vector2[] _uvs;
    int[] _tris;
    Rigidbody2D[,] _nodes;

    bool _pinsActive;
    Rigidbody2D _pinHero;
    Vector2 _pinLeftLocal, _pinRightLocal;

    void Start()
    {
        RebuildCloth();

        if (attachOnStart && attachTarget != null)
        {
            if (useKinematicPins)
                PinTopToHeroShoulders(attachTarget, leftShoulderLocal, rightShoulderLocal);
            else
                AttachTopToTwoPoints(attachTarget, leftShoulderLocal, rightShoulderLocal, attachAsSpring, attachStride);
        }
    }

    [ContextMenu("Rebuild Cloth")]
    public void RebuildCloth()
    {
        _pinsActive = false;
        CleanupNodes();
        BuildMesh();
        BuildNodes();
        ApplyPins();
    }

    void FixedUpdate()
    {
        if (_pinsActive && _pinHero != null && _nodes != null)
        {
            for (int x = 0; x < width; x++)
            {
                float t = (width <= 1) ? 0f : (float)x / (width - 1);
                Vector2 local = Vector2.Lerp(_pinLeftLocal, _pinRightLocal, t) + capeOffsetLocal;
                Vector2 targetPos = _pinHero.transform.TransformPoint(local);
                var rb = _nodes[x, 0];
                if (rb.bodyType != RigidbodyType2D.Kinematic) rb.bodyType = RigidbodyType2D.Kinematic;
                rb.MovePosition(targetPos);
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    void LateUpdate()
    {
        if (_mesh == null || _nodes == null) return;
        var v = _verts;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int i = y * width + x;
            Vector3 world = _nodes[x, y].position;
            v[i] = transform.InverseTransformPoint(world);
        }

        _mesh.vertices = v;
        _mesh.RecalculateBounds();
    }

    void BuildMesh()
    {
        var mf = GetComponent<MeshFilter>();
        _mesh = new Mesh { name = "Cloth2D_Mesh" };
        mf.sharedMesh = _mesh;

        _verts = new Vector3[width * height];
        _uvs   = new Vector2[width * height];
        _tris  = new int[(width - 1) * (height - 1) * 6];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int i = y * width + x;
            _verts[i] = new Vector3(x * spacing, -y * spacing, 0);
            _uvs[i]   = new Vector2((float)x / (width - 1), (float)y / (height - 1));
        }

        int t = 0;
        for (int y = 0; y < height - 1; y++)
        for (int x = 0; x < width - 1; x++)
        {
            int i = y * width + x;
            _tris[t++] = i;           _tris[t++] = i + width;   _tris[t++] = i + 1;
            _tris[t++] = i + 1;       _tris[t++] = i + width;   _tris[t++] = i + width + 1;
        }

        _mesh.vertices  = _verts;
        _mesh.uv        = _uvs;
        _mesh.triangles = _tris;
        _mesh.RecalculateBounds();
    }

    void BuildNodes()
    {
        _nodes = new Rigidbody2D[width, height];

        int targetLayer = useParentLayerForNodes ? gameObject.layer : nodesLayer;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var go = new GameObject($"n_{x}_{y}");
            go.layer = targetLayer;
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.position = transform.TransformPoint(new Vector3(x * spacing, -y * spacing, 0));

            var rb = go.AddComponent<Rigidbody2D>();
            rb.mass = nodeMass;
            rb.gravityScale = nodeGravityScale;

            float rowT = (height <= 1) ? 0f : (float)y / (height - 1);
            rb.linearDamping = nodeLinearDrag + verticalDampingGradient * rowT;
            rb.angularDamping = nodeAngularDrag + 0.5f * verticalDampingGradient * rowT;

            rb.freezeRotation = freezeNodeRotation;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = nodeRadius;

            _nodes[x, y] = rb;

            if (x > 0) Link(_nodes[x, y], _nodes[x - 1, y], 1f);
            if (y > 0) Link(_nodes[x, y], _nodes[x, y - 1], 1f);

            if (addShearLinks && x > 0 && y > 0)
                Link(_nodes[x, y], _nodes[x - 1, y - 1], shearSpringMul);
            if (addShearLinks && x < width - 1 && y > 0)
                Link(_nodes[x, y], _nodes[x + 1, y - 1], shearSpringMul);

            if (addBendLinks && x > 1)
                Link(_nodes[x, y], _nodes[x - 2, y], bendSpringMul);
            if (addBendLinks && y > 1)
                Link(_nodes[x, y], _nodes[x, y - 2], bendSpringMul);
        }
    }

    void Link(Rigidbody2D a, Rigidbody2D b, float springMul)
    {
        if (useSpring)
        {
            var j = a.gameObject.AddComponent<SpringJoint2D>();
            j.connectedBody = b;
            j.autoConfigureDistance = true;
            j.frequency = springFrequency * springMul;
            j.dampingRatio = Mathf.Clamp01(springDamping * springMul);
            j.enableCollision = enableCollisionOnJoints;
        }
        else
        {
            var j = a.gameObject.AddComponent<DistanceJoint2D>();
            j.connectedBody = b;
            j.autoConfigureDistance = true;
            j.enableCollision = enableCollisionOnJoints;
            j.maxDistanceOnly = useMaxDistanceOnly;
        }
    }

    void ApplyPins()
    {
        if (_nodes == null) return;

        for (int x = 0; x < width; x++)
        {
            if (pinTop)  _nodes[x, 0].constraints = RigidbodyConstraints2D.FreezePosition;
        }
        if (pinLeft)
            for (int y = 0; y < height; y++)
                _nodes[0, y].constraints = RigidbodyConstraints2D.FreezePosition;

        if (pinRight)
            for (int y = 0; y < height; y++)
                _nodes[width - 1, y].constraints = RigidbodyConstraints2D.FreezePosition;
    }

    void CleanupNodes()
    {
        var toDelete = new System.Collections.Generic.List<GameObject>();
        foreach (Transform c in transform) toDelete.Add(c.gameObject);
        foreach (var go in toDelete)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        width  = Mathf.Max(2, width);
        height = Mathf.Max(2, height);
        spacing = Mathf.Max(0.01f, spacing);
        nodeRadius = Mathf.Max(0.001f, nodeRadius);
        nodeMass = Mathf.Max(0.001f, nodeMass);
    }
#endif

    public void AttachTopTo(Rigidbody2D target, bool asSpring = true, int stride = 1)
    {
        if (_nodes == null || target == null) return;

        UnpinTop();
        RemoveAttachmentsTo(target);

        stride = Mathf.Max(1, stride);
        for (int x = 0; x < width; x += stride)
            AddHeroJoint(_nodes[x, 0].gameObject, target, asSpring, useAnchors:false, Vector2.zero);
    }

    public void AttachTopToTwoPoints(Rigidbody2D target, Vector2 leftLocal, Vector2 rightLocal, bool asSpring = true, int stride = 1)
    {
        if (_nodes == null || target == null) return;

        UnpinTop();
        RemoveAttachmentsTo(target);

        stride = Mathf.Max(1, stride);
        for (int x = 0; x < width; x += stride)
        {
            float t = (width <= 1) ? 0f : (float)x / (width - 1);
            Vector2 localAnchor = Vector2.Lerp(leftLocal, rightLocal, t) + capeOffsetLocal; // offset
            AddHeroJoint(_nodes[x, 0].gameObject, target, asSpring, useAnchors:true, localAnchor);
        }
    }

    void AddHeroJoint(GameObject node, Rigidbody2D target, bool asSpring, bool useAnchors, Vector2 localAnchor)
    {
        Vector2 finalAnchor = useAnchors ? localAnchor : capeOffsetLocal;

        if (asSpring)
        {
            var j = node.AddComponent<SpringJoint2D>();
            j.connectedBody = target;
            j.enableCollision = enableCollisionOnJoints;

            j.autoConfigureConnectedAnchor = false;
            j.connectedAnchor = finalAnchor;

            j.autoConfigureDistance = false;
            j.distance = 0f;
            j.frequency = springFrequency;
            j.dampingRatio = springDamping;
        }
        else
        {
            var j = node.AddComponent<DistanceJoint2D>();
            j.connectedBody = target;
            j.enableCollision = enableCollisionOnJoints;

            j.autoConfigureConnectedAnchor = false;
            j.connectedAnchor = finalAnchor;

            j.autoConfigureDistance = false;
            j.maxDistanceOnly = false;
            j.distance = 0f;
        }
    }


    public void PinTopToHeroShoulders(Rigidbody2D target, Vector2 leftLocal, Vector2 rightLocal)
    {
        if (_nodes == null || target == null) return;

        UnpinTop();
        RemoveAttachmentsTo(target);

        _pinHero = target;
        _pinLeftLocal = leftLocal;
        _pinRightLocal = rightLocal;
        _pinsActive = true;

        for (int x = 0; x < width; x++)
        {
            float t = (width <= 1) ? 0f : (float)x / (width - 1);
            Vector2 local = Vector2.Lerp(_pinLeftLocal, _pinRightLocal, t) + capeOffsetLocal;
            Vector2 pos = _pinHero.transform.TransformPoint(local);
            var rb = _nodes[x, 0];
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.position = pos;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            foreach (var j in rb.GetComponents<Joint2D>())
            {
                if (Application.isPlaying) Destroy(j);
                else DestroyImmediate(j);
            }
        }
    }

    void UnpinTop()
    {
        pinTop = false;
        if (_nodes == null) return;
        for (int x = 0; x < width; x++)
        {
            var rb = _nodes[x, 0];
            rb.constraints = RigidbodyConstraints2D.None;
            if (!_pinsActive && rb.bodyType != RigidbodyType2D.Dynamic)
                rb.bodyType = RigidbodyType2D.Dynamic;
        }
    }

    void RemoveAttachmentsTo(Rigidbody2D target)
    {
        if (_nodes == null) return;
        for (int x = 0; x < width; x++)
        {
            var joints = _nodes[x, 0].GetComponents<Joint2D>();
            foreach (var j in joints)
            {
                if (j.connectedBody == target)
                {
                    if (Application.isPlaying) Destroy(j);
                    else DestroyImmediate(j);
                }
            }
        }
    }

    [ContextMenu("Sew Top Edge To Selected Rigidbody2D (drag in Inspector)")]
    public void SewTopTo(Rigidbody2D target, bool asSpring = true)
    {
        AttachTopTo(target, asSpring, 1);
    }
}
