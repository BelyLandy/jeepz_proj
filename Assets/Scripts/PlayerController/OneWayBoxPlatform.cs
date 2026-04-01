using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshCollider))]
public sealed class OneWayBoxPlatform : MonoBehaviour
{
    [Header("One Way")]
    [Tooltip("Если низ капсулы героя ниже этой линии, герой может пройти платформу снизу вверх.")]
    [SerializeField] private float passThroughMargin = 0.05f;

    [Tooltip("Если низ капсулы героя поднялся выше этой линии и герой уже не летит вверх быстро, коллизия снова включается.")]
    [SerializeField] private float solidifyMargin = 0.03f;

    [Header("Horizontal Check")]
    [Tooltip("Небольшое расширение по X для проверки, находится ли герой над/под верхней площадью платформы.")]
    [SerializeField] private float horizontalPaddingX = 0.02f;

    [Tooltip("Небольшое расширение по Z для проверки, находится ли герой над/под верхней площадью платформы.")]
    [SerializeField] private float horizontalPaddingZ = 0.02f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;
    [SerializeField] private bool drawRuntimePhaseOutline = true;
    [SerializeField] private bool drawThresholdGuides = true;
    [SerializeField] private bool drawPhaseLabel = true;
    [SerializeField] private float debugLineHalfWidth = 0.75f;
    [SerializeField] private Color fallbackIdleColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    private MeshCollider meshCollider;

    [Tooltip("Коллайдер one-way платформы. Для mesh-варианта используй MeshCollider только с верхней поверхностью без боков и низа.")]
    public MeshCollider Mesh => meshCollider != null ? meshCollider : (meshCollider = GetComponent<MeshCollider>());
    public Collider PlatformCollider => Mesh;
    public Bounds WorldBounds => PlatformCollider.bounds;
    public float TopY => WorldBounds.max.y;
    public float PassThroughThresholdY => TopY - passThroughMargin;
    public float SolidifyThresholdY => TopY + solidifyMargin;
    public float HorizontalPaddingX => horizontalPaddingX;
    public float HorizontalPaddingZ => horizontalPaddingZ;

    private void Awake()
    {
        CacheComponents();
        EnsureNonTriggerCollider();
    }

    private void Reset()
    {
        CacheComponents();
        ClampSettings();
        EnsureNonTriggerCollider();
    }

    private void OnValidate()
    {
        ClampSettings();
        CacheComponents();

        if (!Application.isPlaying)
            EnsureNonTriggerCollider();
    }

    public bool HasHorizontalSupportOverlap(Bounds actorBounds)
    {
        Bounds platformBounds = WorldBounds;

        float platformMinX = platformBounds.min.x - horizontalPaddingX;
        float platformMaxX = platformBounds.max.x + horizontalPaddingX;
        float platformMinZ = platformBounds.min.z - horizontalPaddingZ;
        float platformMaxZ = platformBounds.max.z + horizontalPaddingZ;

        bool overlapX = actorBounds.max.x >= platformMinX && actorBounds.min.x <= platformMaxX;
        bool overlapZ = actorBounds.max.z >= platformMinZ && actorBounds.min.z <= platformMaxZ;

        return overlapX && overlapZ;
    }

    private void CacheComponents()
    {
        if (meshCollider == null)
            meshCollider = GetComponent<MeshCollider>();
    }

    private void ClampSettings()
    {
        passThroughMargin = Mathf.Max(0f, passThroughMargin);
        solidifyMargin = Mathf.Max(0f, solidifyMargin);
        horizontalPaddingX = Mathf.Max(0f, horizontalPaddingX);
        horizontalPaddingZ = Mathf.Max(0f, horizontalPaddingZ);
        debugLineHalfWidth = Mathf.Max(0.05f, debugLineHalfWidth);
    }

    private void EnsureNonTriggerCollider()
    {
        if (meshCollider != null)
            meshCollider.isTrigger = false;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmos)
            return;

        DrawPlatformDebug();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos || !drawPhaseLabel)
            return;

#if UNITY_EDITOR
        CacheComponents();
        if (meshCollider == null)
            return;

        Bounds bounds = meshCollider.bounds;
        OneWayPlatformRuntimePhase phase = ResolveDebugPhase(out bool hasRuntimePhase);
        Color phaseColor = hasRuntimePhase ? OneWayPlatformUtility.GetPhaseColor(phase) : fallbackIdleColor;

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = phaseColor;

        string label = hasRuntimePhase
            ? OneWayPlatformUtility.GetPhaseLabel(phase)
            : "No Controller State";

        Vector3 labelPosition = bounds.center + Vector3.up * (bounds.extents.y + 0.15f);
        Handles.Label(labelPosition, label, style);
#endif
    }

    private void DrawPlatformDebug()
    {
        CacheComponents();
        if (meshCollider == null)
            return;

        Bounds bounds = meshCollider.bounds;
        OneWayPlatformRuntimePhase phase = ResolveDebugPhase(out bool hasRuntimePhase);
        Color phaseColor = hasRuntimePhase ? OneWayPlatformUtility.GetPhaseColor(phase) : fallbackIdleColor;

        if (drawRuntimePhaseOutline)
        {
            Gizmos.color = phaseColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        if (drawThresholdGuides)
        {
            float halfWidth = Mathf.Max(debugLineHalfWidth, bounds.extents.x);
            float z = bounds.center.z;

            Vector3 topA = new Vector3(bounds.center.x - halfWidth, TopY, z);
            Vector3 topB = new Vector3(bounds.center.x + halfWidth, TopY, z);

            Vector3 passA = new Vector3(bounds.center.x - halfWidth, PassThroughThresholdY, z);
            Vector3 passB = new Vector3(bounds.center.x + halfWidth, PassThroughThresholdY, z);

            Vector3 solidA = new Vector3(bounds.center.x - halfWidth, SolidifyThresholdY, z);
            Vector3 solidB = new Vector3(bounds.center.x + halfWidth, SolidifyThresholdY, z);

            Gizmos.color = phaseColor;
            Gizmos.DrawLine(topA, topB);

            Gizmos.color = new Color(1f, 0.75f, 0f, 1f);
            Gizmos.DrawLine(passA, passB);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(solidA, solidB);
        }

    }

    private OneWayPlatformRuntimePhase ResolveDebugPhase(out bool hasRuntimePhase)
    {
        OneWayPlatformController controller = OneWayPlatformController.DebugActiveController;
        if (controller != null && controller.TryGetPlatformPhase(this, out OneWayPlatformRuntimePhase phase))
        {
            hasRuntimePhase = true;
            return phase;
        }

        hasRuntimePhase = false;
        return OneWayPlatformRuntimePhase.Unknown;
    }
}
