using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RBCharacter25D), typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class RBCharacter25DVaulting_old : MonoBehaviour
{
    private const float InvalidPastTime = -999f;
    private const float Skin = 0.01f;

    private struct VaultCandidate
    {
        public Vector3 startBodyCenter;
        public Vector3 targetBodyCenter;
        public Vector3 startRbPosition;
        public Vector3 targetRbPosition;
        public RaycastHit frontHit;
        public RaycastHit topHit;
        public float obstacleHeight;
        public int directionSign;
    }

    private struct VaultDebugState
    {
        public bool initialized;
        public Bounds bounds;
        public int directionSign;
        public float castDistance;
        public float frontRadius;
        public float upperRadius;
        public Vector3 lowerOrigin;
        public Vector3 upperOrigin;
        public Vector3 heightWindowBase;
        public float minHeightY;
        public float maxHeightY;
        public bool hasFrontHit;
        public RaycastHit frontHit;
        public bool upperBlocked;
        public RaycastHit upperHit;
        public Vector3 topProbeOrigin;
        public float topProbeDistance;
        public bool hasTopHit;
        public RaycastHit topHit;
        public bool hasTarget;
        public bool targetIsFree;
        public Vector3 targetBodyCenter;
        public Vector3 targetRbPosition;
        public bool candidateValid;
    }

    [Header("Vault")]
    [SerializeField] private bool enableVaulting = true;
    [SerializeField] private bool allowVaultFromGround = true;
    [SerializeField] private bool allowVaultWhileFalling = true;
    [SerializeField] private bool onlyBoxColliders = true;
    [SerializeField] private float vaultCooldown = 0.08f;
    [SerializeField] private float maxStartUpwardSpeed = 0.35f; // legacy field: больше не ограничивает старт vault на подлёте

    [Header("Vault Height")]
    [SerializeField] private float minVaultHeight = 0.35f;
    [SerializeField] private float maxVaultHeight = 1.35f;
    [SerializeField] private float maxStartDistance = 0.16f;

    [Header("Detection")]
    [SerializeField] private float lowerProbeHeight = 0.45f;
    [SerializeField] private float upperProbeHeight = 1.15f;
    [SerializeField] private float ledgeProbeExtraHeight = 0.30f;
    [SerializeField] private float ledgeForwardProbeOffset = 0.08f;
    [SerializeField] private float frontProbeRadius = 0.06f;
    [SerializeField, Range(0f, 1f)] private float frontMinNormalX = 0.7f;
    [SerializeField, Range(0f, 1f)] private float frontMaxNormalY = 0.2f;
    [SerializeField, Range(0f, 89f)] private float maxTopSurfaceAngle = 8f;

    [Header("Target Pose")]
    [SerializeField] private float landingForwardOffset = 0.08f;
    [SerializeField] private float landingUpOffset = 0.03f;
    [SerializeField] private float capsuleCheckShrink = 0.03f;

    [Header("Motion")]
    [SerializeField] private float vaultDuration = 0.18f;
    [SerializeField] private float arcHeight = 0.30f;
    [SerializeField]
    private AnimationCurve heightCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 3.0f),
        new Keyframe(0.45f, 1f),
        new Keyframe(1f, 0f, -2.5f, 0f)
    );

    [Header("Debug")]
    [SerializeField] private bool drawDebugPreview = true;
    [SerializeField] private bool drawDebugWhileSelected = true;
    [SerializeField] private bool drawTargetCapsule = true;
    [SerializeField] private float debugHitMarkerRadius = 0.05f;
    [SerializeField] private int debugCircleSegments = 14;

    private RBCharacter25D controller;
    private Rigidbody rb;
    private CapsuleCollider col;

    private readonly RaycastHit[] castHits = new RaycastHit[16];
    private readonly Collider[] overlapHits = new Collider[16];

    private VaultCandidate activeCandidate;
    private bool isVaulting;
    private bool previousUseGravity;
    private float vaultElapsed;
    private float vaultBlockedUntilTime = InvalidPastTime;

    private VaultDebugState debugState;

    public bool IsVaulting => isVaulting;

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

    public bool TryStartVault()
    {
        CacheComponents();

        if (controller == null || rb == null || col == null)
            return false;

        int directionSign = controller.VaultFacingSignFromInput;
        RefreshBaseDebugState(directionSign);

        if (!enableVaulting || isVaulting)
        {
            DrawRuntimeDebugPreview();
            return false;
        }

        if (Time.time < vaultBlockedUntilTime)
        {
            DrawRuntimeDebugPreview();
            return false;
        }

        bool grounded = controller.IsGroundedNow;

        if (grounded)
        {
            if (!allowVaultFromGround)
            {
                DrawRuntimeDebugPreview();
                return false;
            }
        }
        else
        {
            // ВАЖНО:
            // Оставляем старое имя поля для совместимости с уже выставленными значениями в инспекторе,
            // но по факту оно теперь означает "разрешить vault в воздухе вообще",
            // и на подлёте вверх, и на падении.
            if (!allowVaultWhileFalling)
            {
                DrawRuntimeDebugPreview();
                return false;
            }
        }

        if (directionSign == 0)
        {
            DrawRuntimeDebugPreview();
            return false;
        }

        bool foundCandidate = TryFindVault(directionSign, out VaultCandidate candidate);
        DrawRuntimeDebugPreview();

        if (!foundCandidate)
            return false;

        BeginVault(candidate);
        return true;
    }

    public void StepActiveVault()
    {
        if (!isVaulting)
            return;

        vaultElapsed += Time.fixedDeltaTime;

        float duration = Mathf.Max(0.0001f, vaultDuration);
        float t = Mathf.Clamp01(vaultElapsed / duration);

        Vector3 nextPosition = Vector3.Lerp(activeCandidate.startRbPosition, activeCandidate.targetRbPosition, t);
        float arcOffset = heightCurve != null ? heightCurve.Evaluate(t) * arcHeight : 0f;
        nextPosition.y += arcOffset;

        if (controller != null && controller.UsesLockedZ)
            nextPosition.z = controller.LockedZPosition;

        rb.linearVelocity = Vector3.zero;
        rb.MovePosition(nextPosition);

        DrawActiveVaultRuntimeDebug();

        if (t >= 1f)
            FinishVault();
    }

    private void BeginVault(VaultCandidate candidate)
    {
        activeCandidate = candidate;
        isVaulting = true;
        vaultElapsed = 0f;

        previousUseGravity = rb.useGravity;
        rb.useGravity = false;

        controller.NotifyVaultStarted();
    }

    private void FinishVault()
    {
        Vector3 finalPosition = activeCandidate.targetRbPosition;

        if (controller != null && controller.UsesLockedZ)
            finalPosition.z = controller.LockedZPosition;

        rb.MovePosition(finalPosition);
        rb.linearVelocity = Vector3.zero;
        rb.useGravity = previousUseGravity;

        isVaulting = false;
        vaultElapsed = 0f;
        vaultBlockedUntilTime = Time.time + vaultCooldown;

        controller.NotifyVaultFinished();
    }

    private bool TryFindVault(int directionSign, out VaultCandidate candidate)
    {
        candidate = default;

        Bounds bounds = col.bounds;
        Vector3 bodyCenterOffsetFromRb = bounds.center - rb.position;

        Vector3 lowerOrigin = debugState.lowerOrigin;
        Vector3 upperOrigin = debugState.upperOrigin;
        Vector3 forward = Vector3.right * directionSign;

        if (!TrySphereCastClosest(lowerOrigin, frontProbeRadius, forward, debugState.castDistance, out RaycastHit frontHit))
            return false;

        debugState.hasFrontHit = true;
        debugState.frontHit = frontHit;

        if (!IsValidFrontWall(frontHit, forward))
            return false;

        if (onlyBoxColliders && !(frontHit.collider is BoxCollider))
            return false;

        if (TrySphereCastClosest(upperOrigin, debugState.upperRadius, forward, debugState.castDistance, out RaycastHit upperHit))
        {
            if (upperHit.collider != null && upperHit.collider.attachedRigidbody != rb)
            {
                debugState.upperBlocked = true;
                debugState.upperHit = upperHit;
                return false;
            }
        }

        float feetY = bounds.min.y;
        float topProbeStartY = Mathf.Max(feetY + maxVaultHeight + ledgeProbeExtraHeight, frontHit.point.y + ledgeProbeExtraHeight);
        Vector3 topProbeOrigin = new Vector3(
            frontHit.point.x + directionSign * ledgeForwardProbeOffset,
            topProbeStartY,
            GetEffectiveZ(bounds)
        );

        float downDistance = topProbeStartY - feetY + maxVaultHeight + 1f;
        debugState.topProbeOrigin = topProbeOrigin;
        debugState.topProbeDistance = downDistance;

        if (!Physics.Raycast(topProbeOrigin, Vector3.down, out RaycastHit topHit, downDistance, controller.GroundMask, QueryTriggerInteraction.Ignore))
            return false;

        debugState.hasTopHit = true;
        debugState.topHit = topHit;

        if (topHit.collider == null || topHit.collider.attachedRigidbody == rb)
            return false;

        if (Vector3.Angle(topHit.normal, Vector3.up) > maxTopSurfaceAngle)
            return false;

        float obstacleHeight = topHit.point.y - feetY;
        if (obstacleHeight < minVaultHeight || obstacleHeight > maxVaultHeight)
            return false;

        float targetBodyCenterZ = controller.UsesLockedZ
            ? controller.LockedZPosition + bodyCenterOffsetFromRb.z
            : bounds.center.z;

        Vector3 targetBodyCenter = new Vector3(
            frontHit.point.x + directionSign * (bounds.extents.x + landingForwardOffset),
            topHit.point.y + bounds.extents.y + landingUpOffset,
            targetBodyCenterZ
        );

        debugState.hasTarget = true;
        debugState.targetBodyCenter = targetBodyCenter;
        debugState.targetRbPosition = targetBodyCenter - bodyCenterOffsetFromRb;

        bool canOccupy = CanOccupyBodyCenter(targetBodyCenter, bounds);
        debugState.targetIsFree = canOccupy;
        if (!canOccupy)
            return false;

        candidate.startBodyCenter = bounds.center;
        candidate.targetBodyCenter = targetBodyCenter;
        candidate.startRbPosition = rb.position;
        candidate.targetRbPosition = targetBodyCenter - bodyCenterOffsetFromRb;
        candidate.frontHit = frontHit;
        candidate.topHit = topHit;
        candidate.obstacleHeight = obstacleHeight;
        candidate.directionSign = directionSign;

        if (controller.UsesLockedZ)
            candidate.targetRbPosition.z = controller.LockedZPosition;

        debugState.candidateValid = true;
        return true;
    }

    private void RefreshBaseDebugState(int directionSign)
    {
        Bounds bounds = col.bounds;
        directionSign = directionSign == 0 ? 1 : (directionSign < 0 ? -1 : 1);

        debugState = new VaultDebugState
        {
            initialized = true,
            bounds = bounds,
            directionSign = directionSign,
            castDistance = maxStartDistance + frontProbeRadius + Skin,
            frontRadius = frontProbeRadius,
            upperRadius = frontProbeRadius * 0.9f,
            lowerOrigin = GetFrontProbeOrigin(bounds, directionSign, lowerProbeHeight),
            upperOrigin = GetFrontProbeOrigin(bounds, directionSign, upperProbeHeight),
            heightWindowBase = new Vector3(
                bounds.center.x + directionSign * (bounds.extents.x + maxStartDistance),
                bounds.min.y,
                GetEffectiveZ(bounds)
            ),
            minHeightY = bounds.min.y + minVaultHeight,
            maxHeightY = bounds.min.y + maxVaultHeight
        };
    }

    private Vector3 GetFrontProbeOrigin(Bounds bounds, int directionSign, float heightFromFeet)
    {
        float probeInset = Mathf.Max(0f, bounds.extents.x - frontProbeRadius - Skin);

        return new Vector3(
            bounds.center.x + directionSign * probeInset,
            bounds.min.y + heightFromFeet,
            GetEffectiveZ(bounds)
        );
    }

    private float GetEffectiveZ(Bounds bounds)
    {
        return controller != null && controller.UsesLockedZ
            ? controller.LockedZPosition
            : bounds.center.z;
    }

    private bool TrySphereCastClosest(
        Vector3 origin,
        float radius,
        Vector3 direction,
        float distance,
        out RaycastHit bestHit)
    {
        bestHit = default;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0.001f, radius),
            direction,
            castHits,
            distance,
            controller.GroundMask,
            QueryTriggerInteraction.Ignore
        );

        bool found = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = castHits[i];

            if (hit.collider == null)
                continue;

            if (hit.collider.attachedRigidbody == rb)
                continue;

            if (!found || hit.distance < bestDistance)
            {
                found = true;
                bestDistance = hit.distance;
                bestHit = hit;
            }
        }

        return found;
    }

    private bool IsValidFrontWall(RaycastHit frontHit, Vector3 forward)
    {
        if (frontHit.collider == null)
            return false;

        if (Vector3.Dot(frontHit.normal, forward) >= -0.1f)
            return false;

        return Mathf.Abs(frontHit.normal.x) >= frontMinNormalX &&
               Mathf.Abs(frontHit.normal.y) <= frontMaxNormalY;
    }

    private bool CanOccupyBodyCenter(Vector3 bodyCenter, Bounds currentBounds)
    {
        float radius = Mathf.Max(0.02f, Mathf.Min(currentBounds.extents.x, currentBounds.extents.z) - capsuleCheckShrink);
        float halfHeight = Mathf.Max(radius, currentBounds.extents.y - capsuleCheckShrink);
        float cylinderHalf = Mathf.Max(0f, halfHeight - radius);

        Vector3 top = bodyCenter + Vector3.up * cylinderHalf;
        Vector3 bottom = bodyCenter - Vector3.up * cylinderHalf;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            top,
            bottom,
            radius,
            overlapHits,
            controller.GroundMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapHits[i];

            if (hit == null)
                continue;

            if (hit == col)
                continue;

            return false;
        }

        return true;
    }

    private void DrawRuntimeDebugPreview()
    {
        if (!drawDebugPreview || !Application.isPlaying || !debugState.initialized)
            return;

        Color lowerColor = new Color(1f, 0.92f, 0.16f, 1f);
        Color upperColor = new Color(0.2f, 0.85f, 1f, 1f);
        Color heightColor = new Color(1f, 0.45f, 0.15f, 1f);
        Color frontHitColor = new Color(1f, 0.2f, 0.8f, 1f);
        Color topHitColor = new Color(0.35f, 1f, 0.35f, 1f);
        Color blockedColor = Color.red;
        Color targetColor = debugState.targetIsFree ? Color.green : blockedColor;

        Vector3 castDir = Vector3.right * debugState.directionSign;
        DrawWireSphereDebug(debugState.lowerOrigin, debugState.frontRadius, lowerColor);
        DrawWireSphereDebug(debugState.upperOrigin, debugState.upperRadius, upperColor);
        Debug.DrawLine(debugState.lowerOrigin, debugState.lowerOrigin + castDir * debugState.castDistance, lowerColor);
        Debug.DrawLine(debugState.upperOrigin, debugState.upperOrigin + castDir * debugState.castDistance, upperColor);

        Vector3 heightBottom = new Vector3(debugState.heightWindowBase.x, debugState.minHeightY, debugState.heightWindowBase.z);
        Vector3 heightTop = new Vector3(debugState.heightWindowBase.x, debugState.maxHeightY, debugState.heightWindowBase.z);
        Debug.DrawLine(heightBottom, heightTop, heightColor);
        Debug.DrawLine(heightBottom + Vector3.left * 0.06f, heightBottom + Vector3.right * 0.06f, heightColor);
        Debug.DrawLine(heightTop + Vector3.left * 0.06f, heightTop + Vector3.right * 0.06f, heightColor);

        if (debugState.hasFrontHit)
        {
            DrawWireSphereDebug(debugState.frontHit.point, debugHitMarkerRadius, frontHitColor);
            Debug.DrawLine(debugState.frontHit.point, debugState.frontHit.point + debugState.frontHit.normal * 0.25f, frontHitColor);
        }

        if (debugState.upperBlocked)
        {
            DrawWireSphereDebug(debugState.upperHit.point, debugHitMarkerRadius, blockedColor);
            Debug.DrawLine(debugState.upperHit.point, debugState.upperHit.point + debugState.upperHit.normal * 0.25f, blockedColor);
        }

        if (debugState.topProbeDistance > 0.001f)
        {
            Debug.DrawLine(debugState.topProbeOrigin, debugState.topProbeOrigin + Vector3.down * debugState.topProbeDistance, topHitColor);
            DrawWireSphereDebug(debugState.topProbeOrigin, 0.03f, topHitColor);
        }

        if (debugState.hasTopHit)
        {
            DrawWireSphereDebug(debugState.topHit.point, debugHitMarkerRadius, topHitColor);
            Debug.DrawLine(debugState.topHit.point, debugState.topHit.point + debugState.topHit.normal * 0.25f, topHitColor);
        }

        if (debugState.hasTarget)
        {
            DrawWireSphereDebug(debugState.targetBodyCenter, 0.085f, targetColor);
            if (drawTargetCapsule)
                DrawCapsuleDebug(debugState.targetBodyCenter, debugState.bounds, targetColor);
        }

        if (debugState.candidateValid)
            DrawArcDebug(debugState.bounds.center, debugState.targetBodyCenter, new Color(0.9f, 1f, 0.3f, 1f));
    }

    private void DrawActiveVaultRuntimeDebug()
    {
        if (!drawDebugPreview || !Application.isPlaying)
            return;

        Color motionColor = new Color(1f, 0.75f, 0.15f, 1f);
        DrawArcDebug(activeCandidate.startBodyCenter, activeCandidate.targetBodyCenter, motionColor);
        DrawWireSphereDebug(activeCandidate.targetBodyCenter, 0.085f, Color.green);
    }

    private void DrawWireSphereDebug(Vector3 center, float radius, Color color)
    {
        int segments = Mathf.Max(8, debugCircleSegments);
        DrawCircleDebug(center, Vector3.up, Vector3.forward, radius, color, segments);
        DrawCircleDebug(center, Vector3.up, Vector3.right, radius, color, segments);
        DrawCircleDebug(center, Vector3.right, Vector3.forward, radius, color, segments);
    }

    private void DrawCircleDebug(Vector3 center, Vector3 axisA, Vector3 axisB, float radius, Color color, int segments)
    {
        Vector3 previous = center + axisA * radius;

        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 next = center + (axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle)) * radius;
            Debug.DrawLine(previous, next, color);
            previous = next;
        }
    }

    private void DrawCapsuleDebug(Vector3 bodyCenter, Bounds currentBounds, Color color)
    {
        float radius = Mathf.Max(0.02f, Mathf.Min(currentBounds.extents.x, currentBounds.extents.z) - capsuleCheckShrink);
        float halfHeight = Mathf.Max(radius, currentBounds.extents.y - capsuleCheckShrink);
        float cylinderHalf = Mathf.Max(0f, halfHeight - radius);

        Vector3 topCenter = bodyCenter + Vector3.up * cylinderHalf;
        Vector3 bottomCenter = bodyCenter - Vector3.up * cylinderHalf;
        Vector3 side = Vector3.right * radius;
        Vector3 forward = Vector3.forward * radius;

        DrawWireSphereDebug(topCenter, radius, color);
        DrawWireSphereDebug(bottomCenter, radius, color);

        Debug.DrawLine(topCenter + side, bottomCenter + side, color);
        Debug.DrawLine(topCenter - side, bottomCenter - side, color);
        Debug.DrawLine(topCenter + forward, bottomCenter + forward, color);
        Debug.DrawLine(topCenter - forward, bottomCenter - forward, color);
    }

    private void DrawArcDebug(Vector3 startBodyCenter, Vector3 targetBodyCenter, Color color)
    {
        int segments = Mathf.Max(8, debugCircleSegments);
        Vector3 previous = startBodyCenter;

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 point = Vector3.Lerp(startBodyCenter, targetBodyCenter, t);
            float arcOffset = heightCurve != null ? heightCurve.Evaluate(t) * arcHeight : 0f;
            point.y += arcOffset;
            Debug.DrawLine(previous, point, color);
            previous = point;
        }
    }

    private void CacheComponents()
    {
        if (controller == null) controller = GetComponent<RBCharacter25D>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (col == null) col = GetComponent<CapsuleCollider>();
    }

    private void ClampSettings()
    {
        vaultCooldown = Mathf.Max(0f, vaultCooldown);
        maxStartUpwardSpeed = Mathf.Max(0f, maxStartUpwardSpeed);

        minVaultHeight = Mathf.Max(0.01f, minVaultHeight);
        maxVaultHeight = Mathf.Max(minVaultHeight, maxVaultHeight);
        maxStartDistance = Mathf.Max(0.01f, maxStartDistance);

        lowerProbeHeight = Mathf.Max(0.01f, lowerProbeHeight);
        upperProbeHeight = Mathf.Max(lowerProbeHeight + 0.05f, upperProbeHeight);
        ledgeProbeExtraHeight = Mathf.Max(0.05f, ledgeProbeExtraHeight);
        ledgeForwardProbeOffset = Mathf.Max(0.01f, ledgeForwardProbeOffset);
        frontProbeRadius = Mathf.Max(0.01f, frontProbeRadius);
        frontMinNormalX = Mathf.Clamp01(frontMinNormalX);
        frontMaxNormalY = Mathf.Clamp01(frontMaxNormalY);
        maxTopSurfaceAngle = Mathf.Clamp(maxTopSurfaceAngle, 0f, 89f);

        landingForwardOffset = Mathf.Max(0f, landingForwardOffset);
        landingUpOffset = Mathf.Max(0f, landingUpOffset);
        capsuleCheckShrink = Mathf.Max(0f, capsuleCheckShrink);

        vaultDuration = Mathf.Max(0.01f, vaultDuration);
        arcHeight = Mathf.Max(0f, arcHeight);
        debugHitMarkerRadius = Mathf.Max(0.01f, debugHitMarkerRadius);
        debugCircleSegments = Mathf.Max(8, debugCircleSegments);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugWhileSelected || !debugState.initialized)
            return;

        Gizmos.color = new Color(1f, 0.92f, 0.16f, 0.9f);
        Gizmos.DrawWireSphere(debugState.lowerOrigin, debugState.frontRadius);

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireSphere(debugState.upperOrigin, debugState.upperRadius);

        Gizmos.color = new Color(1f, 0.45f, 0.15f, 0.9f);
        Vector3 heightBottom = new Vector3(debugState.heightWindowBase.x, debugState.minHeightY, debugState.heightWindowBase.z);
        Vector3 heightTop = new Vector3(debugState.heightWindowBase.x, debugState.maxHeightY, debugState.heightWindowBase.z);
        Gizmos.DrawLine(heightBottom, heightTop);

        if (debugState.hasFrontHit)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.8f, 0.95f);
            Gizmos.DrawWireSphere(debugState.frontHit.point, debugHitMarkerRadius);
            Gizmos.DrawRay(debugState.frontHit.point, debugState.frontHit.normal * 0.25f);
        }

        if (debugState.upperBlocked)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(debugState.upperHit.point, debugHitMarkerRadius);
        }

        if (debugState.topProbeDistance > 0.001f)
        {
            Gizmos.color = new Color(0.35f, 1f, 0.35f, 0.95f);
            Gizmos.DrawLine(debugState.topProbeOrigin, debugState.topProbeOrigin + Vector3.down * debugState.topProbeDistance);
        }

        if (debugState.hasTopHit)
        {
            Gizmos.color = new Color(0.35f, 1f, 0.35f, 0.95f);
            Gizmos.DrawWireSphere(debugState.topHit.point, debugHitMarkerRadius);
        }

        if (debugState.hasTarget)
        {
            Gizmos.color = debugState.targetIsFree ? Color.green : Color.red;
            Gizmos.DrawWireSphere(debugState.targetBodyCenter, 0.085f);
        }
    }
}
