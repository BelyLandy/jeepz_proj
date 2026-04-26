using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class EnemyCharacter25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider mainCollider;
    [SerializeField] private Transform groundCheckOrigin;
    [SerializeField] private EnemyHealth25D health;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
    [SerializeField, Min(0f)] private float groundAcceleration = 20f;
    [SerializeField, Min(0f)] private float groundDeceleration = 25f;
    [SerializeField, Min(0f)] private float airControlMultiplier = 0.65f;

    [Header("Ground Check")]
    [SerializeField, Min(0f)] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Edge Safety")]
    [SerializeField] private bool preventWalkingOffEdges = true;
    [SerializeField, Min(0f)] private float edgeCheckForwardDistance = 0.45f;
    [SerializeField, Min(0f)] private float edgeCheckProbeHeight = 0.2f;
    [SerializeField, Min(0f)] private float edgeCheckDownDistance = 0.6f;
    [SerializeField, Min(0f)] private float edgeCheckProbeRadius = 0.05f;

    [Header("Edge Safety Hardening")]
    [SerializeField] private bool requireEdgeProbeGroundNearCurrentLevel = true;
    [SerializeField, Min(0f)] private float edgeProbeMaxAllowedDrop = 0.25f;
    [SerializeField] private bool hardStopHorizontalVelocityAtUnsafeEdge = true;
    [SerializeField, Min(0f)] private float edgeHardStopMinVelocity = 0.01f;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawGroundProbeGizmos = true;
    [SerializeField] private bool drawEdgeProbeGizmos = true;
    [SerializeField] private Color groundProbeHitColor = new Color(0.2f, 1f, 0.2f, 0.95f);
    [SerializeField] private Color groundProbeMissColor = new Color(1f, 0.2f, 0.2f, 0.95f);
    [SerializeField] private Color edgeProbeGroundedColor = new Color(0.2f, 1f, 0.2f, 0.95f);
    [SerializeField] private Color edgeProbeVoidColor = new Color(1f, 0.2f, 0.2f, 0.95f);
    [SerializeField] private Color edgeProbeHitPointColor = new Color(0.35f, 0.85f, 1f, 0.95f);

    [Header("Facing")]
    [SerializeField, Min(0f)] private float facingByVelocityThreshold = 0.05f;
    [SerializeField] private int startFacingSign = -1;

    [Header("Jump Traversal")]
    [Tooltip("Reference point that follows EnemyJumpLink25D Start/End points during traversal. Usually assign the same child object as Ground Check Origin.")]
    [SerializeField] private Transform traversalReferenceOrigin;
    [SerializeField, Min(0.05f)] private float fallbackJumpFlightTime = 0.55f;
    [SerializeField, Min(0f)] private float jumpTraversalCompletionGrace = 0.05f;

    [Header("World Z Lock")]
    [SerializeField] private bool lockWorldZ = true;
    [SerializeField] private float worldZ = 0f;

    private float moveInputX;
    private int facingSign = 1;
    private bool isGrounded;
    private float verticalSpeed;
    private int landingEventVersion;
    private bool landingStateInitialized;
    private bool wasGroundedPreviousFrame;
    private float airborneStartedAt = -1f;
    private float lastAirborneDuration;

    private bool stunControlLocked;
    private bool reactionControlLocked;
    private bool traversalControlLocked;
    private bool manualFacingOverride;
    private int manualFacingSign = 1;
    private float externalMoveSpeedMultiplier = 1f;

    private bool isJumpTraversalActive;
    private EnemyJumpLink25D activeJumpLink;
    private float jumpTraversalElapsed;
    private float jumpTraversalDuration;
    private Vector3 jumpTraversalStart;
    private Vector3 jumpTraversalEnd;
    private Vector3 jumpTraversalBodyOffset;
    private int jumpTraversalFacingSign = 1;
    private bool allowWalkingOffEdgesForTraversal;

    public float MoveInputX => moveInputX;
    public int FacingSign => facingSign;
    public bool IsGrounded => isGrounded;
    public float VerticalSpeed => verticalSpeed;
    public int LandingEventVersion => landingEventVersion;
    public float LastAirborneDuration => lastAirborneDuration;
    public float HorizontalSpeedAbs => rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
    public bool IsControlLocked => stunControlLocked || reactionControlLocked || traversalControlLocked || IsDead;
    public bool IsDead => health != null && health.IsDead;
    public Rigidbody Rigidbody => rb;
    public Collider MainCollider => mainCollider;
    public bool IsJumpTraversalActive => isJumpTraversalActive;
    public EnemyJumpLink25D ActiveJumpLink => activeJumpLink;
    public bool HasManualFacingOverride => manualFacingOverride;
    public float ExternalMoveSpeedMultiplier => externalMoveSpeedMultiplier;
    public Vector3 TraversalReferencePosition
    {
        get
        {
            if (traversalReferenceOrigin != null)
                return traversalReferenceOrigin.position;

            if (groundCheckOrigin != null)
                return groundCheckOrigin.position;

            return transform.position;
        }
    }

    private void Reset()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (mainCollider == null)
            mainCollider = GetComponent<Collider>();

        if (groundCheckOrigin == null)
            groundCheckOrigin = transform;

        if (traversalReferenceOrigin == null)
            traversalReferenceOrigin = groundCheckOrigin;

        if (health == null)
            health = GetComponent<EnemyHealth25D>();

        ClampSettings();
        facingSign = startFacingSign >= 0 ? 1 : -1;
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (mainCollider == null)
            mainCollider = GetComponent<Collider>();

        if (groundCheckOrigin == null)
            groundCheckOrigin = transform;

        if (traversalReferenceOrigin == null)
            traversalReferenceOrigin = groundCheckOrigin;

        if (health == null)
            health = GetComponent<EnemyHealth25D>();

        ClampSettings();
        facingSign = startFacingSign >= 0 ? 1 : -1;

        if (lockWorldZ)
        {
            Vector3 p = transform.position;
            p.z = worldZ;
            transform.position = p;
        }
    }

    private void OnValidate()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (mainCollider == null)
            mainCollider = GetComponent<Collider>();

        if (groundCheckOrigin == null)
            groundCheckOrigin = transform;

        if (traversalReferenceOrigin == null)
            traversalReferenceOrigin = groundCheckOrigin;

        if (health == null)
            health = GetComponent<EnemyHealth25D>();

        ClampSettings();
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();
        UpdateLandingEventState();
        UpdateVerticalSpeed();
        UpdateJumpTraversalState();

        if (IsDead)
            moveInputX = 0f;

        if (!IsControlLocked)
            StepHorizontalMovement(Time.fixedDeltaTime);

        UpdateFacingFromVelocityOrInput();
        ApplyWorldZLock();
    }

    public void SetMoveInput(float x)
    {
        moveInputX = Mathf.Clamp(x, -1f, 1f);
    }

    public void StopMovement()
    {
        moveInputX = 0f;
    }

    public void SetControlLocked(bool locked)
    {
        SetReactionControlLocked(locked);
    }

    public void SetStunControlLocked(bool locked)
    {
        stunControlLocked = locked && !IsDead;
        if (IsControlLocked)
            moveInputX = 0f;
    }

    public void SetReactionControlLocked(bool locked)
    {
        reactionControlLocked = locked && !IsDead;
        if (IsControlLocked)
            moveInputX = 0f;
    }

    public void ForceFacingSign(int sign)
    {
        facingSign = sign >= 0 ? 1 : -1;
    }

    public void SetManualFacingOverride(bool enabled, int sign)
    {
        manualFacingOverride = enabled;
        manualFacingSign = sign >= 0 ? 1 : -1;
        if (manualFacingOverride)
            facingSign = manualFacingSign;
    }

    public void ClearManualFacingOverride()
    {
        manualFacingOverride = false;
    }

    public void SetExternalMoveSpeedMultiplier(float multiplier)
    {
        externalMoveSpeedMultiplier = Mathf.Max(0.01f, multiplier);
    }

    public void ResetExternalMoveSpeedMultiplier()
    {
        externalMoveSpeedMultiplier = 1f;
    }

    public void SetAllowWalkingOffEdgesForTraversal(bool allow)
    {
        allowWalkingOffEdgesForTraversal = allow;
    }

    public bool TryExecuteJumpLinkTraversal(EnemyJumpLink25D link, Vector3 desiredDestination)
    {
        if (link == null || rb == null || IsDead || isJumpTraversalActive)
            return false;

        Vector3 traversalReference = TraversalReferencePosition;
        if (!link.TryGetTraversal(traversalReference, desiredDestination, out Vector3 traversalStart, out Vector3 traversalEnd))
            return false;

        return TryExecuteJumpLinkTraversal(link, traversalStart, traversalEnd);
    }

    public bool TryExecuteJumpLinkTraversal(EnemyJumpLink25D link, Vector3 traversalStart, Vector3 traversalEnd)
    {
        if (link == null || rb == null || IsDead || isJumpTraversalActive)
            return false;

        Vector3 traversalReference = TraversalReferencePosition;
        if (!IsWithinTraversalStartTolerance(traversalReference, traversalStart, link))
            return false;

        jumpTraversalDuration = Mathf.Max(0.05f, link.FlightTime > 0f ? link.FlightTime : fallbackJumpFlightTime);
        jumpTraversalElapsed = 0f;
        jumpTraversalStart = traversalStart;
        jumpTraversalEnd = traversalEnd;
        jumpTraversalStart.z = lockWorldZ ? worldZ : jumpTraversalStart.z;
        jumpTraversalEnd.z = lockWorldZ ? worldZ : jumpTraversalEnd.z;
        jumpTraversalFacingSign = (jumpTraversalEnd.x - jumpTraversalStart.x) >= 0f ? 1 : -1;

        jumpTraversalBodyOffset = GetCurrentTraversalBodyOffset();
        if (lockWorldZ)
            jumpTraversalBodyOffset.z = 0f;

        Vector3 snapBodyPosition = ConvertReferencePositionToBodyPosition(jumpTraversalStart);
        if (lockWorldZ)
            snapBodyPosition.z = worldZ;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.position = snapBodyPosition;
        }
        transform.position = snapBodyPosition;

        moveInputX = 0f;
        traversalControlLocked = true;
        isJumpTraversalActive = true;
        activeJumpLink = link;
        facingSign = jumpTraversalFacingSign;
        isGrounded = false;
        verticalSpeed = 0f;
        return true;
    }

    public bool InterruptJumpLinkTraversal(Vector3 interruptVelocity)
    {
        if (!isJumpTraversalActive)
            return false;

        isJumpTraversalActive = false;
        traversalControlLocked = false;
        activeJumpLink = null;
        jumpTraversalElapsed = 0f;
        jumpTraversalDuration = 0f;
        jumpTraversalStart = Vector3.zero;
        jumpTraversalEnd = Vector3.zero;
        jumpTraversalBodyOffset = Vector3.zero;

        if (rb != null)
        {
            interruptVelocity.z = 0f;
            rb.linearVelocity = interruptVelocity;
        }

        if (Mathf.Abs(interruptVelocity.x) > 0.01f)
            facingSign = interruptVelocity.x >= 0f ? 1 : -1;

        return true;
    }

    private bool IsWithinTraversalStartTolerance(Vector3 currentPosition, Vector3 traversalStart, EnemyJumpLink25D link)
    {
        if (link == null)
            return false;

        float horizontalTolerance = Mathf.Max(0.01f, link.ApproachHorizontalTolerance);
        float verticalTolerance = Mathf.Max(0.01f, link.ApproachVerticalTolerance);
        return Mathf.Abs(currentPosition.x - traversalStart.x) <= horizontalTolerance
            && Mathf.Abs(currentPosition.y - traversalStart.y) <= verticalTolerance;
    }

    private Vector3 GetBodyPosition()
    {
        return rb != null ? rb.position : transform.position;
    }

    private Vector3 GetCurrentTraversalBodyOffset()
    {
        return GetBodyPosition() - TraversalReferencePosition;
    }

    private Vector3 ConvertReferencePositionToBodyPosition(Vector3 referencePosition)
    {
        return referencePosition + jumpTraversalBodyOffset;
    }

    private void StepHorizontalMovement(float dt)
    {
        if (rb == null)
            return;

        float targetVelocityX = moveInputX * moveSpeed * externalMoveSpeedMultiplier;
        int moveSign = targetVelocityX > 0.001f ? 1 : (targetVelocityX < -0.001f ? -1 : 0);
        bool blockedByUnsafeEdge = moveSign != 0 && !CanUseNormalMovementInDirection(moveSign);
        if (blockedByUnsafeEdge)
        {
            targetVelocityX = 0f;

            if (hardStopHorizontalVelocityAtUnsafeEdge &&
                isGrounded &&
                !isJumpTraversalActive &&
                !allowWalkingOffEdgesForTraversal &&
                Mathf.Abs(rb.linearVelocity.x) > edgeHardStopMinVelocity &&
                Mathf.Sign(rb.linearVelocity.x) == moveSign)
            {
                Vector3 hardStopVelocity = rb.linearVelocity;
                hardStopVelocity.x = 0f;
                hardStopVelocity.z = 0f;
                rb.linearVelocity = hardStopVelocity;
            }
        }

        float currentVelocityX = rb.linearVelocity.x;
        bool accelerating = Mathf.Abs(targetVelocityX) > 0.001f;
        float accel = accelerating ? groundAcceleration : groundDeceleration;
        if (!isGrounded)
            accel *= airControlMultiplier;

        float nextVelocityX = Mathf.MoveTowards(currentVelocityX, targetVelocityX, accel * dt);
        Vector3 velocity = rb.linearVelocity;
        velocity.x = nextVelocityX;
        velocity.z = 0f;
        rb.linearVelocity = velocity;
    }

    private bool CanUseNormalMovementInDirection(int moveSign)
    {
        if (!preventWalkingOffEdges || moveSign == 0)
            return true;

        if (isJumpTraversalActive || allowWalkingOffEdgesForTraversal || !isGrounded)
            return true;

        return HasGroundAhead(moveSign);
    }

    private bool HasGroundAhead(int moveSign)
    {
        if (!TryGetGroundAheadHit(moveSign, out RaycastHit hit))
            return false;

        if (requireEdgeProbeGroundNearCurrentLevel)
        {
            float referenceY = TraversalReferencePosition.y;
            if (hit.point.y < referenceY - edgeProbeMaxAllowedDrop)
                return false;
        }

        return true;
    }

    private bool TryGetGroundAheadHit(int moveSign, out RaycastHit hit)
    {
        Vector3 probeOrigin = GetEdgeProbeOrigin(moveSign);
        float castDistance = GetEdgeProbeCastDistance();
        float radius = Mathf.Max(edgeCheckProbeRadius, 0.001f);
        return Physics.SphereCast(probeOrigin, radius, Vector3.down, out hit, castDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    private Vector3 GetGroundProbeOrigin()
    {
        return groundCheckOrigin != null ? groundCheckOrigin.position : transform.position;
    }

    private Vector3 GetEdgeProbeOrigin(int moveSign)
    {
        Vector3 baseOrigin = GetGroundProbeOrigin();
        return baseOrigin + Vector3.right * (moveSign * edgeCheckForwardDistance) + Vector3.up * edgeCheckProbeHeight;
    }

    private float GetEdgeProbeCastDistance()
    {
        return Mathf.Max(edgeCheckDownDistance + edgeCheckProbeHeight, 0.01f);
    }

    private void UpdateGroundedState()
    {
        if (isJumpTraversalActive)
        {
            isGrounded = false;
            return;
        }

        Vector3 origin = GetGroundProbeOrigin();
        isGrounded = Physics.CheckSphere(origin, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void UpdateLandingEventState()
    {
        bool currentlyGrounded = isGrounded;

        if (!landingStateInitialized)
        {
            wasGroundedPreviousFrame = currentlyGrounded;
            landingStateInitialized = true;
            airborneStartedAt = currentlyGrounded ? -1f : Time.time;
            lastAirborneDuration = 0f;
            return;
        }

        if (wasGroundedPreviousFrame && !currentlyGrounded)
        {
            airborneStartedAt = Time.time;
        }
        else if (!wasGroundedPreviousFrame && currentlyGrounded)
        {
            lastAirborneDuration = airborneStartedAt >= 0f ? Mathf.Max(0f, Time.time - airborneStartedAt) : 0f;
            landingEventVersion++;
            airborneStartedAt = -1f;
        }

        wasGroundedPreviousFrame = currentlyGrounded;
    }

    private void UpdateVerticalSpeed()
    {
        if (isJumpTraversalActive)
            return;

        verticalSpeed = rb != null ? rb.linearVelocity.y : 0f;
    }

    private void UpdateJumpTraversalState()
    {
        if (!isJumpTraversalActive || activeJumpLink == null)
            return;

        jumpTraversalElapsed += Time.fixedDeltaTime;
        float duration = Mathf.Max(jumpTraversalDuration, fallbackJumpFlightTime, 0.05f);
        float t = Mathf.Clamp01(jumpTraversalElapsed / duration);

        Vector3 nextReferencePosition = activeJumpLink.EvaluateTraversalPosition(jumpTraversalStart, jumpTraversalEnd, t);
        if (lockWorldZ)
            nextReferencePosition.z = worldZ;

        Vector3 nextBodyPosition = ConvertReferencePositionToBodyPosition(nextReferencePosition);
        if (lockWorldZ)
            nextBodyPosition.z = worldZ;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.MovePosition(nextBodyPosition);
        }
        else
        {
            transform.position = nextBodyPosition;
        }

        verticalSpeed = 0f;
        isGrounded = false;

        if (t < 1f)
            return;

        CompleteJumpLinkTraversal();
    }

    private void CompleteJumpLinkTraversal()
    {
        Vector3 finalReferencePosition = jumpTraversalEnd;
        if (lockWorldZ)
            finalReferencePosition.z = worldZ;

        Vector3 finalBodyPosition = ConvertReferencePositionToBodyPosition(finalReferencePosition);
        if (lockWorldZ)
            finalBodyPosition.z = worldZ;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.position = finalBodyPosition;
        }
        transform.position = finalBodyPosition;

        isJumpTraversalActive = false;
        traversalControlLocked = false;
        activeJumpLink = null;
        jumpTraversalElapsed = 0f;
        jumpTraversalDuration = 0f;
        jumpTraversalStart = Vector3.zero;
        jumpTraversalEnd = Vector3.zero;
        jumpTraversalBodyOffset = Vector3.zero;
        verticalSpeed = 0f;
    }

    private void UpdateFacingFromVelocityOrInput()
    {
        if (manualFacingOverride)
        {
            facingSign = manualFacingSign >= 0 ? 1 : -1;
            return;
        }

        if (isJumpTraversalActive)
        {
            facingSign = jumpTraversalFacingSign >= 0 ? 1 : -1;
            return;
        }

        if (rb != null && Mathf.Abs(rb.linearVelocity.x) > facingByVelocityThreshold)
        {
            facingSign = rb.linearVelocity.x >= 0f ? 1 : -1;
            return;
        }

        if (Mathf.Abs(moveInputX) > 0.01f)
            facingSign = moveInputX >= 0f ? 1 : -1;
    }

    private void ApplyWorldZLock()
    {
        if (!lockWorldZ)
            return;

        if (rb != null)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.z = 0f;
            rb.linearVelocity = velocity;
        }

        Vector3 p = transform.position;
        if (Mathf.Abs(p.z - worldZ) > 0.0001f)
        {
            p.z = worldZ;
            transform.position = p;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (drawGroundProbeGizmos)
            DrawGroundProbeGizmo();

        if (drawEdgeProbeGizmos)
        {
            DrawEdgeProbeGizmo(-1);
            DrawEdgeProbeGizmo(1);
        }
    }

    private void DrawGroundProbeGizmo()
    {
        Vector3 origin = GetGroundProbeOrigin();
        bool hasGround = Physics.CheckSphere(origin, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
        Gizmos.color = hasGround ? groundProbeHitColor : groundProbeMissColor;
        Gizmos.DrawWireSphere(origin, groundCheckRadius);
        Gizmos.DrawSphere(origin, Mathf.Max(groundCheckRadius * 0.2f, 0.02f));
    }

    private void DrawEdgeProbeGizmo(int moveSign)
    {
        Vector3 probeOrigin = GetEdgeProbeOrigin(moveSign);
        float castDistance = GetEdgeProbeCastDistance();
        float radius = Mathf.Max(edgeCheckProbeRadius, 0.001f);
        bool rawGroundHit = TryGetGroundAheadHit(moveSign, out RaycastHit hit);
        bool hasGroundAhead = rawGroundHit && (!requireEdgeProbeGroundNearCurrentLevel || hit.point.y >= TraversalReferencePosition.y - edgeProbeMaxAllowedDrop);

        Gizmos.color = hasGroundAhead ? edgeProbeGroundedColor : edgeProbeVoidColor;
        Gizmos.DrawWireSphere(probeOrigin, radius);
        Gizmos.DrawLine(probeOrigin, probeOrigin + Vector3.down * castDistance);

        if (hasGroundAhead)
        {
            Gizmos.color = edgeProbeHitPointColor;
            float markerRadius = Mathf.Max(radius * 0.75f, 0.03f);
            Gizmos.DrawSphere(hit.point, markerRadius);
        }
    }

    private void ClampSettings()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        groundAcceleration = Mathf.Max(0f, groundAcceleration);
        groundDeceleration = Mathf.Max(0f, groundDeceleration);
        airControlMultiplier = Mathf.Max(0f, airControlMultiplier);
        groundCheckRadius = Mathf.Max(0f, groundCheckRadius);
        edgeCheckForwardDistance = Mathf.Max(0f, edgeCheckForwardDistance);
        edgeCheckProbeHeight = Mathf.Max(0f, edgeCheckProbeHeight);
        edgeCheckDownDistance = Mathf.Max(0f, edgeCheckDownDistance);
        edgeCheckProbeRadius = Mathf.Max(0f, edgeCheckProbeRadius);
        facingByVelocityThreshold = Mathf.Max(0f, facingByVelocityThreshold);
        fallbackJumpFlightTime = Mathf.Max(0.05f, fallbackJumpFlightTime);
        jumpTraversalCompletionGrace = Mathf.Max(0f, jumpTraversalCompletionGrace);
        externalMoveSpeedMultiplier = Mathf.Max(0.01f, externalMoveSpeedMultiplier);
    }
}
