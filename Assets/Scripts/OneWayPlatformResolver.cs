using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class OneWayPlatformResolver : MonoBehaviour
{
    [Header("Search")]
    [Tooltip("Слой, на котором лежат one-way платформы.")]
    [SerializeField] private LayerMask oneWayPlatformMask;

    [Tooltip("Насколько расширять поиск платформ по X вокруг героя.")]
    [SerializeField] private float searchPaddingX = 0.2f;

    [Tooltip("Насколько расширять поиск платформ по Y вокруг героя.")]
    [SerializeField] private float searchPaddingY = 0.6f;

    [Tooltip("Насколько расширять поиск платформ по Z вокруг героя.")]
    [SerializeField] private float searchPaddingZ = 0.2f;

    [Header("Solid / Pass Through")]
    [Tooltip("Пока герой летит вверх быстрее этого значения, платформа не станет твёрдой даже если герой уже выше её верха.")]
    [SerializeField] private float maxUpwardSpeedToSolidify = 0.1f;

    [Tooltip("Небольшой запас для сравнения высот, чтобы убрать дёрганье на границе.")]
    [SerializeField] private float bottomYEpsilon = 0.005f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugPreview = true;
    [SerializeField] private bool drawDebugWhileSelected = true;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private readonly Collider[] overlapHits = new Collider[32];
    private readonly List<OneWayBoxPlatform> nearbyPlatforms = new List<OneWayBoxPlatform>(16);
    private readonly HashSet<OneWayBoxPlatform> trackedPlatforms = new HashSet<OneWayBoxPlatform>();
    private readonly List<OneWayBoxPlatform> trackedBuffer = new List<OneWayBoxPlatform>(16);

    public Rigidbody RigidbodyComponent => rb != null ? rb : (rb = GetComponent<Rigidbody>());
    public CapsuleCollider CapsuleColliderComponent => capsule != null ? capsule : (capsule = GetComponent<CapsuleCollider>());

    private void Awake()
    {
        CacheComponents();
        ClampSettings();
    }

    private void OnValidate()
    {
        ClampSettings();
        CacheComponents();
    }

    private void FixedUpdate()
    {
        CacheComponents();
        CollectNearbyPlatforms();
        ResolveNearbyPlatforms();
        ReleaseFarPlatforms();
        DrawRuntimeDebug();
    }

    private void OnDisable()
    {
        RestoreAllTrackedPlatformsToSolid();
    }

    private void OnDestroy()
    {
        RestoreAllTrackedPlatformsToSolid();
    }

    private void CollectNearbyPlatforms()
    {
        nearbyPlatforms.Clear();

        Bounds playerBounds = capsule.bounds;
        Vector3 center = playerBounds.center;
        Vector3 halfExtents = new Vector3(
            playerBounds.extents.x + searchPaddingX,
            playerBounds.extents.y + searchPaddingY,
            playerBounds.extents.z + searchPaddingZ
        );

        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            overlapHits,
            Quaternion.identity,
            oneWayPlatformMask,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapHits[i];
            if (hit == null)
                continue;

            OneWayBoxPlatform platform = hit.GetComponent<OneWayBoxPlatform>();
            if (platform == null)
                platform = hit.GetComponentInParent<OneWayBoxPlatform>();

            if (platform == null)
                continue;

            if (!platform.SupportsCollider(hit))
                continue;

            if (!nearbyPlatforms.Contains(platform))
                nearbyPlatforms.Add(platform);
        }
    }

    private void ResolveNearbyPlatforms()
    {
        float playerBottomY = capsule.bounds.min.y;
        float verticalSpeed = rb.linearVelocity.y;

        for (int i = 0; i < nearbyPlatforms.Count; i++)
        {
            OneWayBoxPlatform platform = nearbyPlatforms[i];
            if (platform == null)
                continue;

            trackedPlatforms.Add(platform);

            float passThroughY = platform.PassThroughThresholdY;
            float solidifyY = platform.SolidifyThresholdY;

            if (playerBottomY < passThroughY - bottomYEpsilon)
            {
                platform.SetPassThrough(true);
                continue;
            }

            if (playerBottomY > solidifyY + bottomYEpsilon && verticalSpeed <= maxUpwardSpeedToSolidify)
            {
                platform.SetSolid(true);
            }
        }
    }

    private void ReleaseFarPlatforms()
    {
        if (trackedPlatforms.Count == 0)
            return;

        trackedBuffer.Clear();
        trackedBuffer.AddRange(trackedPlatforms);

        for (int i = 0; i < trackedBuffer.Count; i++)
        {
            OneWayBoxPlatform tracked = trackedBuffer[i];
            if (tracked == null)
            {
                trackedPlatforms.Remove(tracked);
                continue;
            }

            if (nearbyPlatforms.Contains(tracked))
                continue;

            tracked.SetSolid(true);
            trackedPlatforms.Remove(tracked);
        }
    }

    private void RestoreAllTrackedPlatformsToSolid()
    {
        if (trackedPlatforms.Count == 0)
            return;

        trackedBuffer.Clear();
        trackedBuffer.AddRange(trackedPlatforms);

        for (int i = 0; i < trackedBuffer.Count; i++)
        {
            OneWayBoxPlatform tracked = trackedBuffer[i];
            if (tracked != null)
                tracked.SetSolid(true);
        }

        trackedPlatforms.Clear();
        trackedBuffer.Clear();
    }

    private void CacheComponents()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (capsule == null)
            capsule = GetComponent<CapsuleCollider>();
    }

    private void ClampSettings()
    {
        searchPaddingX = Mathf.Max(0f, searchPaddingX);
        searchPaddingY = Mathf.Max(0f, searchPaddingY);
        searchPaddingZ = Mathf.Max(0f, searchPaddingZ);
        maxUpwardSpeedToSolidify = Mathf.Max(0f, maxUpwardSpeedToSolidify);
        bottomYEpsilon = Mathf.Max(0f, bottomYEpsilon);
    }

    private void DrawRuntimeDebug()
    {
        if (!drawDebugPreview)
            return;

        Bounds playerBounds = capsule.bounds;
        Vector3 center = playerBounds.center;
        Vector3 halfExtents = new Vector3(
            playerBounds.extents.x + searchPaddingX,
            playerBounds.extents.y + searchPaddingY,
            playerBounds.extents.z + searchPaddingZ
        );

        DrawWireBox(center, halfExtents, Color.white);

        float playerBottomY = playerBounds.min.y;
        Vector3 bottomA = new Vector3(center.x - playerBounds.extents.x, playerBottomY, center.z);
        Vector3 bottomB = new Vector3(center.x + playerBounds.extents.x, playerBottomY, center.z);
        Debug.DrawLine(bottomA, bottomB, Color.magenta);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugWhileSelected)
            return;

        CacheComponents();
        if (capsule == null)
            return;

        Bounds playerBounds = capsule.bounds;
        Vector3 center = playerBounds.center;
        Vector3 size = new Vector3(
            (playerBounds.extents.x + searchPaddingX) * 2f,
            (playerBounds.extents.y + searchPaddingY) * 2f,
            (playerBounds.extents.z + searchPaddingZ) * 2f
        );

        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(
            new Vector3(center.x - playerBounds.extents.x, playerBounds.min.y, center.z),
            new Vector3(center.x + playerBounds.extents.x, playerBounds.min.y, center.z)
        );
    }

    private static void DrawWireBox(Vector3 center, Vector3 halfExtents, Color color)
    {
        Vector3 p0 = center + new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
        Vector3 p1 = center + new Vector3(halfExtents.x, -halfExtents.y, -halfExtents.z);
        Vector3 p2 = center + new Vector3(halfExtents.x, -halfExtents.y, halfExtents.z);
        Vector3 p3 = center + new Vector3(-halfExtents.x, -halfExtents.y, halfExtents.z);

        Vector3 p4 = center + new Vector3(-halfExtents.x, halfExtents.y, -halfExtents.z);
        Vector3 p5 = center + new Vector3(halfExtents.x, halfExtents.y, -halfExtents.z);
        Vector3 p6 = center + new Vector3(halfExtents.x, halfExtents.y, halfExtents.z);
        Vector3 p7 = center + new Vector3(-halfExtents.x, halfExtents.y, halfExtents.z);

        Debug.DrawLine(p0, p1, color);
        Debug.DrawLine(p1, p2, color);
        Debug.DrawLine(p2, p3, color);
        Debug.DrawLine(p3, p0, color);

        Debug.DrawLine(p4, p5, color);
        Debug.DrawLine(p5, p6, color);
        Debug.DrawLine(p6, p7, color);
        Debug.DrawLine(p7, p4, color);

        Debug.DrawLine(p0, p4, color);
        Debug.DrawLine(p1, p5, color);
        Debug.DrawLine(p2, p6, color);
        Debug.DrawLine(p3, p7, color);
    }
}
