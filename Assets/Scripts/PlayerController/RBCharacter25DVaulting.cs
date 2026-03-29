using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RBCharacter25D), typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class RBCharacter25DVaulting : MonoBehaviour
{
    private const float InvalidPastTime = -999f;
    private const float Skin = 0.01f;
    private const float DirectionSignEpsilon = 0.05f;
    private const float VelocitySignEpsilon = 0.15f;

    private struct VaultCandidate
    {
        public Vector3 StartBodyCenter;
        public Vector3 TargetBodyCenter;
        public Vector3 StartRbPosition;
        public Vector3 TargetRbPosition;
        public RaycastHit FrontHit;
        public RaycastHit TopHit;
        public float ObstacleHeight;
        public int DirectionSign;
        public bool IsUnderPlatformCatch;
    }

    private struct VaultDebugState
    {
        public bool Initialized;
        public Bounds Bounds;
        public int DirectionSign;
        public float RegularCastDistance;
        public Vector3 RegularWindowMin;
        public Vector3 RegularWindowMax;
        public Vector3 UnderWindowCenter;
        public Vector3 UnderWindowHalfExtents;
        public bool HasFrontHit;
        public RaycastHit FrontHit;
        public bool HasTopHit;
        public RaycastHit TopHit;
        public Vector3 TopProbeOrigin;
        public float TopProbeDistance;
        public bool HasTarget;
        public bool TargetIsFree;
        public Vector3 TargetBodyCenter;
        public bool CandidateValid;
        public bool IsUnderPlatformCandidate;
    }

    [Header("Vault")]
    [SerializeField] private bool enableVaulting = true;
    [SerializeField] private bool allowVaultFromGround = true;
    [SerializeField] private bool allowVaultWhileFalling = true;
    [SerializeField] private bool onlyBoxColliders = true;
    [SerializeField] private float vaultCooldown = 0.08f;

    [Header("Vault Height")]
    [SerializeField] private float minVaultHeight = 0.35f;
    [SerializeField] private float maxVaultHeight = 1.8f;
    [SerializeField] private float maxStartDistance = 0.22f;

    [Header("Regular Ledge Detection")]
    [Tooltip("Нижняя граница широкой зоны обычного vault, считая от ступней героя.")]
    [SerializeField] private float vaultDetectBottom = 0.65f;

    [Tooltip("Верхняя граница широкой зоны обычного vault, считая от ступней героя.")]
    [SerializeField] private float vaultDetectTop = 1.85f;

    [Tooltip("Сколько уровней по высоте проверять для обычного vault.")]
    [SerializeField, Range(2, 8)] private int vaultVerticalSamples = 5;

    [SerializeField] private float ledgeProbeExtraHeight = 0.30f;
    [SerializeField] private float ledgeForwardProbeOffset = 0.14f;
    [SerializeField] private float frontProbeRadius = 0.06f;
    [SerializeField, Range(0f, 1f)] private float frontMinNormalX = 0.7f;
    [SerializeField, Range(0f, 1f)] private float frontMaxNormalY = 0.2f;
    [SerializeField, Range(0f, 89f)] private float maxTopSurfaceAngle = 8f;

    [Header("Top Surface Detection")]
    [SerializeField] private float topProbeRadius = 0.07f;
    [SerializeField, Range(3, 9)] private int topProbeSampleCount = 5;
    [SerializeField] private float ledgeBackSearch = 0.04f;

    [Header("Under Platform Catch")]
    [SerializeField] private bool enableUnderPlatformCatch = true;
    [SerializeField] private float underCatchBottom = 1.00f;
    [SerializeField] private float underCatchTop = 1.95f;
    [SerializeField, Range(2, 6)] private int underCatchVerticalSamples = 3;
    [SerializeField] private float underPlatformBottomAboveHeadMargin = 0.04f;
    [SerializeField] private float underPlatformMaxPenetration = 0.12f;
    [SerializeField] private float underPlatformMinUpwardSpeed = 0.08f;
    [SerializeField] private float suspendedCheckDepth = 0.55f;
    [SerializeField] private float suspendedCheckInset = 0.04f;
    [SerializeField] private float underPlatformHorizontalInset = 0.06f;
    [SerializeField] private float underPlatformExtraUpOffset = 0.08f;

    [Header("Target Pose")]
    [SerializeField] private float landingForwardOffset = 0.14f;
    [SerializeField] private float landingUpOffset = 0.05f;
    [SerializeField] private float capsuleCheckShrink = 0.03f;

    [Header("Motion")]
    [SerializeField] private float vaultDuration = 0.18f;
    [SerializeField] private float arcHeight = 0.45f;
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

    private readonly RaycastHit[] castHits = new RaycastHit[24];
    private readonly Collider[] overlapHits = new Collider[24];
    private readonly Collider[] boxOverlapHits = new Collider[24];

    private VaultCandidate activeCandidate;
    private VaultDebugState debugState;
    private bool isVaulting;
    private bool previousUseGravity;
    private bool previousIsKinematic;
    private bool previousColliderEnabled;
    private Collider ignoredVaultColliderA;
    private Collider ignoredVaultColliderB;
    private float vaultElapsed;
    private float vaultBlockedUntilTime = InvalidPastTime;

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

    private void OnDisable()
    {
        if (rb != null)
        {
            rb.useGravity = previousUseGravity;
            rb.isKinematic = previousIsKinematic;
        }

        if (col != null)
            col.enabled = previousColliderEnabled;

        RestoreVaultCollisionIgnores();
        isVaulting = false;
        vaultElapsed = 0f;
    }

    private void Reset()
    {
        enableVaulting = true;
        allowVaultFromGround = true;
        allowVaultWhileFalling = true;
        onlyBoxColliders = true;
        vaultCooldown = 0.08f;

        minVaultHeight = 0.35f;
        maxVaultHeight = 1.8f;
        maxStartDistance = 0.22f;

        vaultDetectBottom = 0.65f;
        vaultDetectTop = 1.85f;
        vaultVerticalSamples = 5;
        ledgeProbeExtraHeight = 0.30f;
        ledgeForwardProbeOffset = 0.14f;
        frontProbeRadius = 0.06f;
        frontMinNormalX = 0.7f;
        frontMaxNormalY = 0.2f;
        maxTopSurfaceAngle = 8f;

        topProbeRadius = 0.07f;
        topProbeSampleCount = 5;
        ledgeBackSearch = 0.04f;

        enableUnderPlatformCatch = true;
        underCatchBottom = 1.00f;
        underCatchTop = 1.95f;
        underCatchVerticalSamples = 3;
        underPlatformBottomAboveHeadMargin = 0.04f;
        underPlatformMaxPenetration = 0.12f;
        underPlatformMinUpwardSpeed = 0.08f;
        suspendedCheckDepth = 0.55f;
        suspendedCheckInset = 0.04f;
        underPlatformHorizontalInset = 0.06f;
        underPlatformExtraUpOffset = 0.08f;

        landingForwardOffset = 0.14f;
        landingUpOffset = 0.05f;
        capsuleCheckShrink = 0.03f;

        vaultDuration = 0.18f;
        arcHeight = 0.45f;

        drawDebugPreview = true;
        drawDebugWhileSelected = true;
        drawTargetCapsule = true;
        debugHitMarkerRadius = 0.05f;
        debugCircleSegments = 14;

        ClampSettings();
    }

    public bool TryStartVault()
    {
        CacheComponents();

        if (controller == null || rb == null || col == null)
            return false;

        int directionSign = GetPreferredDirectionSign();
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
        else if (!allowVaultWhileFalling)
        {
            DrawRuntimeDebugPreview();
            return false;
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

        Vector3 nextPosition = Vector3.Lerp(activeCandidate.StartRbPosition, activeCandidate.TargetRbPosition, t);
        float arcOffset = heightCurve != null ? heightCurve.Evaluate(t) * arcHeight : 0f;
        nextPosition.y += arcOffset;

        if (controller != null && controller.UsesLockedZ)
            nextPosition.z = controller.LockedZPosition;

        rb.position = nextPosition;
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
        previousIsKinematic = rb.isKinematic;
        previousColliderEnabled = col != null && col.enabled;

        float preVaultVelocityX = rb.linearVelocity.x;
        controller.NotifyVaultStarted(preVaultVelocityX);

        rb.useGravity = false;
        rb.isKinematic = true;

        if (col != null)
            col.enabled = false;

        SetVaultCollisionIgnore(candidate.TopHit.collider, true, ref ignoredVaultColliderA);
        if (candidate.FrontHit.collider != null && candidate.FrontHit.collider != candidate.TopHit.collider)
            SetVaultCollisionIgnore(candidate.FrontHit.collider, true, ref ignoredVaultColliderB);
    }

    private void FinishVault()
    {
        Vector3 finalPosition = activeCandidate.TargetRbPosition;
        if (controller != null && controller.UsesLockedZ)
            finalPosition.z = controller.LockedZPosition;

        rb.position = finalPosition;
        rb.useGravity = previousUseGravity;
        rb.isKinematic = previousIsKinematic;

        if (col != null)
            col.enabled = previousColliderEnabled;

        RestoreVaultCollisionIgnores();

        isVaulting = false;
        vaultElapsed = 0f;
        vaultBlockedUntilTime = Time.time + vaultCooldown;

        controller.NotifyVaultFinished();
    }

    private bool TryFindVault(int directionSign, out VaultCandidate candidate)
    {
        candidate = default;

        if (TryFindBestRegularVault(directionSign, out candidate))
            return true;

        if (!enableUnderPlatformCatch)
            return false;

        return TryFindUnderPlatformCatch(directionSign, out candidate);
    }

    private bool TryFindBestRegularVault(int directionSign, out VaultCandidate bestCandidate)
    {
        bestCandidate = default;

        Bounds bounds = col.bounds;
        Vector3 bodyCenterOffsetFromRb = bounds.center - rb.position;
        Vector3 forward = Vector3.right * directionSign;
        int sampleCount = Mathf.Max(2, vaultVerticalSamples);
        bool found = false;
        float bestScore = float.MaxValue;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1 ? 0f : i / (float)(sampleCount - 1);
            float probeHeight = Mathf.Lerp(vaultDetectBottom, vaultDetectTop, t);
            Vector3 origin = GetFrontProbeOrigin(bounds, directionSign, probeHeight, frontProbeRadius);
            float castDistance = maxStartDistance + ledgeForwardProbeOffset + frontProbeRadius + Skin;

            if (!TrySphereCastClosest(origin, frontProbeRadius, forward, castDistance, out RaycastHit frontHit))
                continue;

            if (!IsValidFrontWall(frontHit, forward))
                continue;

            if (onlyBoxColliders && !(frontHit.collider is BoxCollider))
                continue;

            if (!TryResolveTopSurfaceForRegular(bounds, directionSign, frontHit, out RaycastHit topHit, out float nearEdgeX, out float farEdgeX))
                continue;

            float obstacleHeight = topHit.point.y - bounds.min.y;
            if (obstacleHeight < minVaultHeight || obstacleHeight > maxVaultHeight)
                continue;

            Vector3 targetBodyCenter = BuildRegularTargetBodyCenter(bounds, bodyCenterOffsetFromRb, directionSign, topHit, nearEdgeX, farEdgeX);
            bool canOccupy = CanOccupyTarget(targetBodyCenter, bounds, topHit.collider, nearEdgeX, farEdgeX);
            if (!canOccupy)
                continue;

            float heightCenter = Mathf.Lerp(vaultDetectBottom, vaultDetectTop, 0.5f);
            float score = frontHit.distance * 2.0f + Mathf.Abs(probeHeight - heightCenter) * 0.35f + Mathf.Abs(obstacleHeight - 0.95f) * 0.1f;
            if (!found || score < bestScore)
            {
                found = true;
                bestScore = score;

                bestCandidate = new VaultCandidate
                {
                    StartBodyCenter = bounds.center,
                    TargetBodyCenter = targetBodyCenter,
                    StartRbPosition = rb.position,
                    TargetRbPosition = targetBodyCenter - bodyCenterOffsetFromRb,
                    FrontHit = frontHit,
                    TopHit = topHit,
                    ObstacleHeight = obstacleHeight,
                    DirectionSign = directionSign,
                    IsUnderPlatformCatch = false,
                };

                if (controller.UsesLockedZ)
                    bestCandidate.TargetRbPosition.z = controller.LockedZPosition;

                debugState.HasFrontHit = true;
                debugState.FrontHit = frontHit;
                debugState.HasTopHit = true;
                debugState.TopHit = topHit;
                debugState.HasTarget = true;
                debugState.TargetIsFree = true;
                debugState.TargetBodyCenter = targetBodyCenter;
                debugState.CandidateValid = true;
                debugState.IsUnderPlatformCandidate = false;
            }
        }

        return found;
    }

    private bool TryFindUnderPlatformCatch(int directionSign, out VaultCandidate candidate)
    {
        candidate = default;

        Bounds bounds = col.bounds;
        if (rb.linearVelocity.y < underPlatformMinUpwardSpeed)
            return false;

        Vector3 bodyCenterOffsetFromRb = bounds.center - rb.position;
        Vector3 boxCenter = GetUnderDetectionCenter(bounds, directionSign);
        Vector3 boxHalfExtents = GetUnderDetectionHalfExtents(bounds);
        float zoneMinX = boxCenter.x - boxHalfExtents.x;
        float zoneMaxX = boxCenter.x + boxHalfExtents.x;
        float headY = bounds.max.y;
        float frontX = directionSign > 0 ? bounds.max.x : bounds.min.x;
        float z = GetEffectiveZ(bounds);

        int count = Physics.OverlapBoxNonAlloc(
            boxCenter,
            boxHalfExtents,
            boxOverlapHits,
            Quaternion.identity,
            controller.GroundMask,
            QueryTriggerInteraction.Ignore);

        if (count <= 0)
            return false;

        bool found = false;
        float bestScore = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider hit = boxOverlapHits[i];
            if (hit == null || hit == col)
                continue;
            if (hit.attachedRigidbody == rb)
                continue;
            if (onlyBoxColliders && !(hit is BoxCollider))
                continue;

            Bounds hb = hit.bounds;
            bool overlapsDetectionX = hb.max.x >= zoneMinX && hb.min.x <= zoneMaxX;
            bool overlapsBodyX = hb.max.x >= bounds.min.x && hb.min.x <= bounds.max.x;
            if (!overlapsDetectionX || !overlapsBodyX)
                continue;

            if (hb.min.y <= headY + underPlatformBottomAboveHeadMargin)
                continue;

            float nearEdgeX = directionSign > 0 ? hb.min.x : hb.max.x;
            float farEdgeX = directionSign > 0 ? hb.max.x : hb.min.x;
            float penetration = directionSign > 0 ? frontX - nearEdgeX : nearEdgeX - frontX;
            if (penetration < -frontProbeRadius)
                continue;
            if (penetration > underPlatformMaxPenetration)
                continue;

            if (HasSupportBelowNearEdge(hb, directionSign, z))
                continue;

            if (!HasUnderCatchSampleHit(bounds, directionSign, hit))
                continue;

            if (!TryResolveTopSurfaceForCollider(hit, hb, directionSign, z, out RaycastHit topHit, out float resolvedNearEdgeX, out float resolvedFarEdgeX))
                continue;

            float obstacleHeight = topHit.point.y - bounds.min.y;
            if (obstacleHeight < minVaultHeight || obstacleHeight > maxVaultHeight)
                continue;

            Vector3 targetBodyCenter = BuildUnderCatchTargetBodyCenter(bounds, bodyCenterOffsetFromRb, directionSign, topHit, resolvedNearEdgeX, resolvedFarEdgeX);
            bool canOccupy = CanOccupyTarget(targetBodyCenter, bounds, topHit.collider, resolvedNearEdgeX, resolvedFarEdgeX);
            if (!canOccupy)
                continue;

            float score = Mathf.Abs(penetration) * 1.5f + Mathf.Abs(hb.min.y - headY);
            if (!found || score < bestScore)
            {
                found = true;
                bestScore = score;

                candidate = new VaultCandidate
                {
                    StartBodyCenter = bounds.center,
                    TargetBodyCenter = targetBodyCenter,
                    StartRbPosition = rb.position,
                    TargetRbPosition = targetBodyCenter - bodyCenterOffsetFromRb,
                    FrontHit = default,
                    TopHit = topHit,
                    ObstacleHeight = obstacleHeight,
                    DirectionSign = directionSign,
                    IsUnderPlatformCatch = true,
                };

                if (controller.UsesLockedZ)
                    candidate.TargetRbPosition.z = controller.LockedZPosition;

                debugState.HasTopHit = true;
                debugState.TopHit = topHit;
                debugState.HasTarget = true;
                debugState.TargetIsFree = true;
                debugState.TargetBodyCenter = targetBodyCenter;
                debugState.CandidateValid = true;
                debugState.IsUnderPlatformCandidate = true;
            }
        }

        return found;
    }

    private Vector3 BuildRegularTargetBodyCenter(
        Bounds bounds,
        Vector3 bodyCenterOffsetFromRb,
        int directionSign,
        RaycastHit topHit,
        float nearEdgeX,
        float farEdgeX)
    {
        float targetSurfaceX = ComputeRegularTargetSurfaceX(nearEdgeX, farEdgeX, directionSign, bounds.center.x);
        float targetBodyCenterZ = controller.UsesLockedZ
            ? controller.LockedZPosition + bodyCenterOffsetFromRb.z
            : bounds.center.z;

        return new Vector3(
            targetSurfaceX,
            topHit.point.y + bounds.extents.y + landingUpOffset + Skin,
            targetBodyCenterZ);
    }

    private Vector3 BuildUnderCatchTargetBodyCenter(
        Bounds bounds,
        Vector3 bodyCenterOffsetFromRb,
        int directionSign,
        RaycastHit topHit,
        float nearEdgeX,
        float farEdgeX)
    {
        float targetSurfaceX = ComputeUnderCatchTargetSurfaceX(nearEdgeX, farEdgeX, directionSign, bounds.center.x);
        float targetBodyCenterZ = controller.UsesLockedZ
            ? controller.LockedZPosition + bodyCenterOffsetFromRb.z
            : bounds.center.z;

        return new Vector3(
            targetSurfaceX,
            topHit.point.y + bounds.extents.y + landingUpOffset + underPlatformExtraUpOffset + Skin,
            targetBodyCenterZ);
    }

    private float ComputeRegularTargetSurfaceX(float nearEdgeX, float farEdgeX, int directionSign, float startBodyCenterX)
    {
        float bodyHalfWidth = Mathf.Max(0.05f, col.bounds.extents.x - capsuleCheckShrink);
        float supportCenter = 0.5f * (nearEdgeX + farEdgeX);
        float minSupportX = Mathf.Min(nearEdgeX, farEdgeX) + bodyHalfWidth + Skin;
        float maxSupportX = Mathf.Max(nearEdgeX, farEdgeX) - bodyHalfWidth - Skin;

        if (minSupportX > maxSupportX)
            return supportCenter;

        float desired = nearEdgeX + directionSign * (bodyHalfWidth + landingForwardOffset);
        return Mathf.Clamp(desired, minSupportX, maxSupportX);
    }

    private float ComputeUnderCatchTargetSurfaceX(float nearEdgeX, float farEdgeX, int directionSign, float startBodyCenterX)
    {
        float bodyHalfWidth = Mathf.Max(0.05f, col.bounds.extents.x - capsuleCheckShrink);
        float supportCenter = 0.5f * (nearEdgeX + farEdgeX);
        float minSupportX = Mathf.Min(nearEdgeX, farEdgeX) + bodyHalfWidth + Skin;
        float maxSupportX = Mathf.Max(nearEdgeX, farEdgeX) - bodyHalfWidth - Skin;

        if (minSupportX > maxSupportX)
            return supportCenter;

        float desired = nearEdgeX + directionSign * (bodyHalfWidth + underPlatformHorizontalInset);
        return Mathf.Clamp(desired, minSupportX, maxSupportX);
    }

    private bool TryResolveTopSurfaceForRegular(
        Bounds bodyBounds,
        int directionSign,
        RaycastHit frontHit,
        out RaycastHit topHit,
        out float nearEdgeX,
        out float farEdgeX)
    {
        if (frontHit.collider == null)
        {
            topHit = default;
            nearEdgeX = 0f;
            farEdgeX = 0f;
            return false;
        }

        Bounds colliderBounds = frontHit.collider.bounds;
        float z = GetEffectiveZ(bodyBounds);
        return TryResolveTopSurfaceForCollider(frontHit.collider, colliderBounds, directionSign, z, out topHit, out nearEdgeX, out farEdgeX);
    }

    private bool TryResolveTopSurfaceForCollider(
        Collider targetCollider,
        Bounds colliderBounds,
        int directionSign,
        float z,
        out RaycastHit bestTopHit,
        out float nearEdgeX,
        out float farEdgeX)
    {
        bestTopHit = default;
        nearEdgeX = directionSign > 0 ? colliderBounds.min.x : colliderBounds.max.x;
        farEdgeX = directionSign > 0 ? colliderBounds.max.x : colliderBounds.min.x;

        float startY = colliderBounds.max.y + ledgeProbeExtraHeight + topProbeRadius + Skin;
        float distance = startY - colliderBounds.min.y + maxVaultHeight + 0.6f;
        int sampleCount = Mathf.Max(3, topProbeSampleCount);
        bool found = false;
        float bestScore = float.MaxValue;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1 ? 0f : i / (float)(sampleCount - 1);
            float x;
            if (directionSign > 0)
                x = Mathf.Lerp(colliderBounds.min.x + suspendedCheckInset, colliderBounds.max.x - suspendedCheckInset, t);
            else
                x = Mathf.Lerp(colliderBounds.max.x - suspendedCheckInset, colliderBounds.min.x + suspendedCheckInset, t);

            Vector3 origin = new Vector3(x, startY, z);
            if (!TrySphereCastClosest(origin, topProbeRadius, Vector3.down, distance, out RaycastHit hit))
                continue;
            if (hit.collider != targetCollider)
                continue;
            if (!IsValidTopSurface(hit))
                continue;

            float edgeDistance = Mathf.Abs(x - nearEdgeX);
            float score = edgeDistance + Mathf.Abs(hit.point.y - colliderBounds.max.y) * 0.25f;
            if (!found || score < bestScore)
            {
                found = true;
                bestScore = score;
                bestTopHit = hit;
                debugState.TopProbeOrigin = origin;
                debugState.TopProbeDistance = distance;
            }
        }

        return found;
    }

    private bool CanOccupyTarget(Vector3 targetBodyCenter, Bounds currentBounds, Collider supportCollider, float nearEdgeX, float farEdgeX)
    {
        bool insideSupportBounds = IsSupportedByTopSurfaceBounds(targetBodyCenter, currentBounds, nearEdgeX, farEdgeX);
        bool hasSupportCast = HasSupportBelowTarget(targetBodyCenter, currentBounds, supportCollider);
        bool canOccupy = CanOccupyBodyCenter(targetBodyCenter, currentBounds, supportCollider);
        debugState.TargetIsFree = canOccupy && (insideSupportBounds || hasSupportCast);
        return debugState.TargetIsFree;
    }

    private bool IsSupportedByTopSurfaceBounds(Vector3 bodyCenter, Bounds currentBounds, float nearEdgeX, float farEdgeX)
    {
        float bodyHalfWidth = Mathf.Max(0.02f, currentBounds.extents.x - capsuleCheckShrink);
        float margin = Mathf.Max(0.01f, bodyHalfWidth * 0.55f);
        float minX = Mathf.Min(nearEdgeX, farEdgeX) + margin;
        float maxX = Mathf.Max(nearEdgeX, farEdgeX) - margin;

        if (maxX <= minX)
        {
            float centerX = 0.5f * (nearEdgeX + farEdgeX);
            return Mathf.Abs(bodyCenter.x - centerX) <= bodyHalfWidth * 0.45f + Skin;
        }

        return bodyCenter.x >= minX - Skin && bodyCenter.x <= maxX + Skin;
    }

    private bool CanOccupyBodyCenter(Vector3 bodyCenter, Bounds currentBounds, Collider supportCollider)
    {
        float radius = Mathf.Max(0.02f, Mathf.Min(currentBounds.extents.x, currentBounds.extents.z) - capsuleCheckShrink);
        float halfHeight = Mathf.Max(radius, currentBounds.extents.y - capsuleCheckShrink);
        float cylinderHalf = Mathf.Max(0f, halfHeight - radius);

        Vector3 clearanceLift = Vector3.up * Mathf.Max(Skin * 2f, landingUpOffset + 0.005f);
        Vector3 top = bodyCenter + Vector3.up * cylinderHalf + clearanceLift;
        Vector3 bottom = bodyCenter - Vector3.up * cylinderHalf + clearanceLift;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            top,
            bottom,
            radius,
            overlapHits,
            controller.GroundMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapHits[i];
            if (hit == null)
                continue;
            if (hit == col)
                continue;
            if (supportCollider != null && hit == supportCollider)
                continue;
            return false;
        }

        return true;
    }

    private bool HasSupportBelowTarget(Vector3 bodyCenter, Bounds currentBounds, Collider expectedCollider)
    {
        float supportStartHeight = currentBounds.extents.y + landingUpOffset + 0.12f;
        float supportDistance = supportStartHeight + 0.65f;
        float radius = Mathf.Max(0.025f, Mathf.Max(frontProbeRadius, topProbeRadius) * 0.85f);
        float sideOffset = Mathf.Min(0.08f, Mathf.Max(0.035f, currentBounds.extents.x * 0.45f));

        Vector3[] origins =
        {
            bodyCenter + Vector3.up * supportStartHeight,
            bodyCenter + Vector3.up * supportStartHeight + Vector3.right * sideOffset,
            bodyCenter + Vector3.up * supportStartHeight + Vector3.left * sideOffset,
        };

        for (int i = 0; i < origins.Length; i++)
        {
            if (!TrySphereCastClosest(origins[i], radius, Vector3.down, supportDistance, out RaycastHit supportHit))
                continue;
            if (supportHit.collider == null)
                continue;
            if (expectedCollider != null && supportHit.collider != expectedCollider)
                continue;
            if (IsValidTopSurface(supportHit))
                return true;
        }

        return false;
    }

    private Vector3 GetUnderDetectionCenter(Bounds bounds, int directionSign)
    {
        float xCenter = bounds.center.x + directionSign * (bounds.extents.x + maxStartDistance * 0.5f);
        float yMin = bounds.min.y + underCatchBottom;
        float yMax = bounds.min.y + underCatchTop;
        float yCenter = (yMin + yMax) * 0.5f;
        return new Vector3(xCenter, yCenter, GetEffectiveZ(bounds));
    }

    private Vector3 GetUnderDetectionHalfExtents(Bounds bounds)
    {
        float halfWidth = Mathf.Max(0.02f, maxStartDistance * 0.5f + frontProbeRadius);
        float halfHeight = Mathf.Max(0.02f, (underCatchTop - underCatchBottom) * 0.5f);
        return new Vector3(halfWidth, halfHeight, Mathf.Max(0.02f, bounds.extents.z));
    }

    private bool HasSupportBelowNearEdge(Bounds platformBounds, int directionSign, float z)
    {
        float sampleX = directionSign > 0
            ? platformBounds.min.x + Mathf.Max(0.005f, suspendedCheckInset)
            : platformBounds.max.x - Mathf.Max(0.005f, suspendedCheckInset);

        Vector3 origin = new Vector3(sampleX, platformBounds.min.y - Skin, z);
        return Physics.Raycast(origin, Vector3.down, suspendedCheckDepth, controller.GroundMask, QueryTriggerInteraction.Ignore);
    }

    private bool HasUnderCatchSampleHit(Bounds bodyBounds, int directionSign, Collider expectedCollider)
    {
        Vector3 forward = Vector3.right * directionSign;
        int sampleCount = Mathf.Max(2, underCatchVerticalSamples);
        float castDistance = maxStartDistance + ledgeForwardProbeOffset + frontProbeRadius + Skin;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1 ? 0f : i / (float)(sampleCount - 1);
            float probeHeight = Mathf.Lerp(underCatchBottom, underCatchTop, t);
            Vector3 origin = GetFrontProbeOrigin(bodyBounds, directionSign, probeHeight, frontProbeRadius);
            if (!TrySphereCastClosest(origin, frontProbeRadius, forward, castDistance, out RaycastHit hit))
                continue;
            if (hit.collider == expectedCollider)
                return true;
        }

        return false;
    }

    private int GetPreferredDirectionSign()
    {
        if (controller != null)
        {
            float smoothedInput = controller.SmoothedInputX;
            if (Mathf.Abs(smoothedInput) > DirectionSignEpsilon)
                return smoothedInput < 0f ? -1 : 1;
        }

        if (rb != null)
        {
            float velocityX = rb.linearVelocity.x;
            if (Mathf.Abs(velocityX) > VelocitySignEpsilon)
                return velocityX < 0f ? -1 : 1;
        }

        return controller != null ? controller.VaultFacingSignFromInput : 1;
    }

    private Vector3 GetFrontProbeOrigin(Bounds bounds, int directionSign, float heightFromFeet, float radius)
    {
        float probeInset = Mathf.Max(0f, bounds.extents.x - radius - Skin);
        return new Vector3(
            bounds.center.x + directionSign * probeInset,
            bounds.min.y + heightFromFeet,
            GetEffectiveZ(bounds));
    }

    private float GetEffectiveZ(Bounds bounds)
    {
        return controller != null && controller.UsesLockedZ ? controller.LockedZPosition : bounds.center.z;
    }

    private bool TrySphereCastClosest(Vector3 origin, float radius, Vector3 direction, float distance, out RaycastHit bestHit)
    {
        bestHit = default;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0.001f, radius),
            direction,
            castHits,
            distance,
            controller.GroundMask,
            QueryTriggerInteraction.Ignore);

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

    private bool IsValidTopSurface(RaycastHit hit)
    {
        if (hit.collider == null)
            return false;
        if (hit.collider.attachedRigidbody == rb)
            return false;
        return Vector3.Angle(hit.normal, Vector3.up) <= maxTopSurfaceAngle;
    }

    private void SetVaultCollisionIgnore(Collider target, bool ignore, ref Collider slot)
    {
        if (col == null || target == null)
            return;

        Physics.IgnoreCollision(col, target, ignore);
        slot = ignore ? target : null;
    }

    private void RestoreVaultCollisionIgnores()
    {
        if (col == null)
            return;

        if (ignoredVaultColliderA != null)
        {
            Physics.IgnoreCollision(col, ignoredVaultColliderA, false);
            ignoredVaultColliderA = null;
        }

        if (ignoredVaultColliderB != null)
        {
            Physics.IgnoreCollision(col, ignoredVaultColliderB, false);
            ignoredVaultColliderB = null;
        }
    }

    private void RefreshBaseDebugState(int directionSign)
    {
        Bounds bounds = col.bounds;
        directionSign = directionSign == 0 ? 1 : (directionSign < 0 ? -1 : 1);

        float frontX = bounds.center.x + directionSign * bounds.extents.x;
        debugState = new VaultDebugState
        {
            Initialized = true,
            Bounds = bounds,
            DirectionSign = directionSign,
            RegularCastDistance = maxStartDistance + ledgeForwardProbeOffset + frontProbeRadius + Skin,
            RegularWindowMin = new Vector3(frontX, bounds.min.y + vaultDetectBottom, GetEffectiveZ(bounds)),
            RegularWindowMax = new Vector3(frontX, bounds.min.y + vaultDetectTop, GetEffectiveZ(bounds)),
            UnderWindowCenter = GetUnderDetectionCenter(bounds, directionSign),
            UnderWindowHalfExtents = GetUnderDetectionHalfExtents(bounds),
        };
    }

    private void DrawRuntimeDebugPreview()
    {
        if (!drawDebugPreview || !Application.isPlaying || !debugState.Initialized)
            return;

        Color regularColor = new Color(0.2f, 0.85f, 1f, 1f);
        Color underColor = new Color(1f, 0.92f, 0.16f, 1f);
        Color topColor = new Color(0.35f, 1f, 0.35f, 1f);
        Color frontColor = new Color(1f, 0.2f, 0.8f, 1f);
        Color targetColor = debugState.TargetIsFree ? Color.green : Color.red;

        DrawVerticalWindow(debugState.RegularWindowMin, debugState.RegularWindowMax, frontProbeRadius, regularColor);
        DrawBoxDebug(debugState.UnderWindowCenter, debugState.UnderWindowHalfExtents, underColor);

        if (debugState.HasFrontHit)
        {
            DrawWireSphereDebug(debugState.FrontHit.point, debugHitMarkerRadius, frontColor);
            Debug.DrawLine(debugState.FrontHit.point, debugState.FrontHit.point + debugState.FrontHit.normal * 0.25f, frontColor);
        }

        if (debugState.TopProbeDistance > 0.001f)
        {
            Debug.DrawLine(debugState.TopProbeOrigin, debugState.TopProbeOrigin + Vector3.down * debugState.TopProbeDistance, topColor);
            DrawWireSphereDebug(debugState.TopProbeOrigin, 0.03f, topColor);
        }

        if (debugState.HasTopHit)
        {
            DrawWireSphereDebug(debugState.TopHit.point, debugHitMarkerRadius, topColor);
            Debug.DrawLine(debugState.TopHit.point, debugState.TopHit.point + debugState.TopHit.normal * 0.25f, topColor);
        }

        if (debugState.HasTarget)
        {
            DrawWireSphereDebug(debugState.TargetBodyCenter, 0.085f, targetColor);
            if (drawTargetCapsule)
                DrawCapsuleDebug(debugState.TargetBodyCenter, debugState.Bounds, targetColor);
        }

        if (debugState.CandidateValid)
        {
            Color arcColor = debugState.IsUnderPlatformCandidate
                ? new Color(0.8f, 1f, 1f, 1f)
                : new Color(0.9f, 1f, 0.3f, 1f);
            DrawArcDebug(debugState.Bounds.center, debugState.TargetBodyCenter, arcColor);
        }
    }

    private void DrawActiveVaultRuntimeDebug()
    {
        if (!drawDebugPreview || !Application.isPlaying)
            return;

        Color motionColor = activeCandidate.IsUnderPlatformCatch
            ? new Color(0.7f, 1f, 1f, 1f)
            : new Color(1f, 0.75f, 0.15f, 1f);

        DrawArcDebug(activeCandidate.StartBodyCenter, activeCandidate.TargetBodyCenter, motionColor);
        DrawWireSphereDebug(activeCandidate.TargetBodyCenter, 0.085f, Color.green);
    }

    private void DrawVerticalWindow(Vector3 minPoint, Vector3 maxPoint, float radius, Color color)
    {
        Debug.DrawLine(minPoint, maxPoint, color);
        DrawWireSphereDebug(minPoint, radius, color);
        DrawWireSphereDebug(maxPoint, radius, color);
    }

    private void DrawBoxDebug(Vector3 center, Vector3 halfExtents, Color color)
    {
        Vector3 a = center + new Vector3(-halfExtents.x, -halfExtents.y, 0f);
        Vector3 b = center + new Vector3(halfExtents.x, -halfExtents.y, 0f);
        Vector3 c = center + new Vector3(halfExtents.x, halfExtents.y, 0f);
        Vector3 d = center + new Vector3(-halfExtents.x, halfExtents.y, 0f);
        Debug.DrawLine(a, b, color);
        Debug.DrawLine(b, c, color);
        Debug.DrawLine(c, d, color);
        Debug.DrawLine(d, a, color);
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

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugWhileSelected || Application.isPlaying)
            return;

        CacheComponents();
        if (controller == null || rb == null || col == null)
            return;

        int directionSign = GetPreferredDirectionSign();
        RefreshBaseDebugState(directionSign);
        DrawRuntimeDebugPreview();
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
        minVaultHeight = Mathf.Max(0f, minVaultHeight);
        maxVaultHeight = Mathf.Max(minVaultHeight + 0.01f, maxVaultHeight);
        maxStartDistance = Mathf.Max(0.01f, maxStartDistance);

        vaultDetectBottom = Mathf.Max(0f, vaultDetectBottom);
        vaultDetectTop = Mathf.Max(vaultDetectBottom + 0.01f, vaultDetectTop);
        vaultVerticalSamples = Mathf.Clamp(vaultVerticalSamples, 2, 8);

        ledgeProbeExtraHeight = Mathf.Max(0f, ledgeProbeExtraHeight);
        ledgeForwardProbeOffset = Mathf.Max(0f, ledgeForwardProbeOffset);
        frontProbeRadius = Mathf.Max(0.01f, frontProbeRadius);
        frontMinNormalX = Mathf.Clamp01(frontMinNormalX);
        frontMaxNormalY = Mathf.Clamp01(frontMaxNormalY);
        maxTopSurfaceAngle = Mathf.Clamp(maxTopSurfaceAngle, 0f, 89f);

        topProbeRadius = Mathf.Max(0.01f, topProbeRadius);
        topProbeSampleCount = Mathf.Clamp(topProbeSampleCount, 3, 9);
        ledgeBackSearch = Mathf.Max(0f, ledgeBackSearch);

        underCatchBottom = Mathf.Max(0f, underCatchBottom);
        underCatchTop = Mathf.Max(underCatchBottom + 0.01f, underCatchTop);
        underCatchVerticalSamples = Mathf.Clamp(underCatchVerticalSamples, 2, 6);
        underPlatformBottomAboveHeadMargin = Mathf.Max(0f, underPlatformBottomAboveHeadMargin);
        underPlatformMaxPenetration = Mathf.Max(0f, underPlatformMaxPenetration);
        underPlatformMinUpwardSpeed = Mathf.Max(0f, underPlatformMinUpwardSpeed);
        suspendedCheckDepth = Mathf.Max(0.01f, suspendedCheckDepth);
        suspendedCheckInset = Mathf.Max(0f, suspendedCheckInset);
        underPlatformHorizontalInset = Mathf.Max(0f, underPlatformHorizontalInset);
        underPlatformExtraUpOffset = Mathf.Max(0f, underPlatformExtraUpOffset);

        landingForwardOffset = Mathf.Max(0f, landingForwardOffset);
        landingUpOffset = Mathf.Max(0f, landingUpOffset);
        capsuleCheckShrink = Mathf.Max(0f, capsuleCheckShrink);

        vaultDuration = Mathf.Max(0.01f, vaultDuration);
        arcHeight = Mathf.Max(0f, arcHeight);
        debugHitMarkerRadius = Mathf.Max(0.005f, debugHitMarkerRadius);
        debugCircleSegments = Mathf.Clamp(debugCircleSegments, 8, 32);
    }
}
