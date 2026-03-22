using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class OneWayBoxPlatform : MonoBehaviour
{
    [Header("One Way")]
    [Tooltip("Если true, в начале игры платформа твёрдая. Для обычного платформера почти всегда должно быть включено.")]
    [SerializeField] private bool startSolid = true;

    [Tooltip("Если низ капсулы героя ниже этой линии, платформа становится прозрачной снизу.")]
    [SerializeField] private float passThroughMargin = 0.05f;

    [Tooltip("Если низ капсулы героя поднялся выше этой линии и герой уже не летит вверх быстро, платформа снова становится твёрдой.")]
    [SerializeField] private float solidifyMargin = 0.03f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;
    [SerializeField] private float debugLineHalfWidth = 0.75f;

    private BoxCollider box;

    public BoxCollider Box => box != null ? box : (box = GetComponent<BoxCollider>());
    public Bounds WorldBounds => Box.bounds;
    public float TopY => WorldBounds.max.y;
    public float PassThroughThresholdY => TopY - passThroughMargin;
    public float SolidifyThresholdY => TopY + solidifyMargin;
    public bool IsPassThrough => Box.isTrigger;

    private void Awake()
    {
        CacheComponents();
        ApplyInitialState();
    }

    private void Reset()
    {
        CacheComponents();
        ApplyInitialState();
    }

    private void OnValidate()
    {
        passThroughMargin = Mathf.Max(0f, passThroughMargin);
        solidifyMargin = Mathf.Max(0f, solidifyMargin);
        debugLineHalfWidth = Mathf.Max(0.05f, debugLineHalfWidth);

        CacheComponents();

        if (!Application.isPlaying)
            ApplyInitialState();
    }

    public void SetPassThrough(bool passThrough)
    {
        CacheComponents();
        if (box == null)
            return;

        if (box.isTrigger == passThrough)
            return;

        box.isTrigger = passThrough;
    }

    public void SetSolid(bool solid)
    {
        SetPassThrough(!solid);
    }

    public bool SupportsCollider(Collider other)
    {
        return other != null && other == Box;
    }

    private void CacheComponents()
    {
        if (box == null)
            box = GetComponent<BoxCollider>();
    }

    private void ApplyInitialState()
    {
        if (box == null)
            return;

        box.isTrigger = !startSolid;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        CacheComponents();
        if (box == null)
            return;

        Bounds b = box.bounds;
        float halfWidth = Mathf.Max(debugLineHalfWidth, b.extents.x);
        float z = b.center.z;

        Vector3 topA = new Vector3(b.center.x - halfWidth, TopY, z);
        Vector3 topB = new Vector3(b.center.x + halfWidth, TopY, z);

        Vector3 passA = new Vector3(b.center.x - halfWidth, PassThroughThresholdY, z);
        Vector3 passB = new Vector3(b.center.x + halfWidth, PassThroughThresholdY, z);

        Vector3 solidA = new Vector3(b.center.x - halfWidth, SolidifyThresholdY, z);
        Vector3 solidB = new Vector3(b.center.x + halfWidth, SolidifyThresholdY, z);

        Gizmos.color = Box.isTrigger ? new Color(1f, 0.75f, 0f, 1f) : new Color(0.1f, 1f, 0.2f, 1f);
        Gizmos.DrawWireCube(b.center, b.size);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(topA, topB);

        Gizmos.color = new Color(1f, 0.75f, 0f, 1f);
        Gizmos.DrawLine(passA, passB);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(solidA, solidB);
    }
}
