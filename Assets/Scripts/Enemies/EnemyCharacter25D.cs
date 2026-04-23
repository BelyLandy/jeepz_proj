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

    [Header("Facing")]
    [SerializeField, Min(0f)] private float facingByVelocityThreshold = 0.05f;
    [SerializeField] private int startFacingSign = -1;

    [Header("Jump Traversal")]
    [SerializeField, Min(0.05f)] private float fallbackJumpFlightTime = 0.55f;
    [SerializeField, Min(0f)] private float jumpTraversalCompletionGrace = 0.05f;

    [Header("World Z Lock")]
    [SerializeField] private bool lockWorldZ = true;
    [SerializeField] private float worldZ = 0f;

    private float moveInputX;
    private int facingSign = 1;
    private bool isGrounded;
    private float verticalSpeed;

    private bool stunControlLocked;
    private bool reactionControlLocked;
    private bool traversalControlLocked;
    private bool manualFacingOverride;
    private int manualFacingSign = 1;

    private bool isJumpTraversalActive;
    private bool traversalHasLeftGround;
    private float traversalEarliestCompleteTime;
    private EnemyJumpLink25D activeJumpLink;

    public float MoveInputX => moveInputX;
    public int FacingSign => facingSign;
    public bool IsGrounded => isGrounded;
    public float VerticalSpeed => verticalSpeed;
    public float HorizontalSpeedAbs => rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
    public bool IsControlLocked => stunControlLocked || reactionControlLocked || traversalControlLocked || IsDead;
    public bool IsDead => health != null && health.IsDead;
    public Rigidbody Rigidbody => rb;
    public Collider MainCollider => mainCollider;
    public bool IsJumpTraversalActive => isJumpTraversalActive;
    public EnemyJumpLink25D ActiveJumpLink => activeJumpLink;
    public bool HasManualFacingOverride => manualFacingOverride;

    private void Reset()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (mainCollider == null)
            mainCollider = GetComponent<Collider>();

        if (groundCheckOrigin == null)
            groundCheckOrigin = transform;

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

        if (health == null)
            health = GetComponent<EnemyHealth25D>();

        ClampSettings();
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();
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

    public bool TryExecuteJumpLinkTraversal(EnemyJumpLink25D link, Vector3 desiredDestination)
    {
        if (link == null || rb == null || IsDead || isJumpTraversalActive)
            return false;

        if (!link.TryGetTraversal(transform.position, desiredDestination, out Vector3 traversalStart, out Vector3 traversalEnd))
            return false;

        float approachDistance = Mathf.Max(0.01f, link.ApproachDistance);
        if (Mathf.Abs(transform.position.x - traversalStart.x) > approachDistance)
            return false;

        float flightTime = Mathf.Max(0.05f, link.FlightTime > 0f ? link.FlightTime : fallbackJumpFlightTime);
        Vector3 startPosition = transform.position;
        startPosition.z = 0f;
        Vector3 endPosition = traversalEnd;
        endPosition.z = 0f;

        Vector3 displacement = endPosition - startPosition;
        float gravityY = Physics.gravity.y;
        if (Mathf.Abs(gravityY) < 0.0001f)
            gravityY = -9.81f;

        float velocityX = displacement.x / flightTime;
        float velocityY = (displacement.y - 0.5f * gravityY * flightTime * flightTime) / flightTime;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = velocityX;
        velocity.y = velocityY;
        velocity.z = 0f;
        rb.linearVelocity = velocity;

        moveInputX = 0f;
        traversalControlLocked = true;
        isJumpTraversalActive = true;
        traversalHasLeftGround = false;
        traversalEarliestCompleteTime = Time.time + Mathf.Max(link.MinimumAirTime, jumpTraversalCompletionGrace);
        activeJumpLink = link;
        facingSign = displacement.x >= 0f ? 1 : -1;
        return true;
    }

    private void StepHorizontalMovement(float dt)
    {
        if (rb == null)
            return;

        float targetVelocityX = moveInputX * moveSpeed;
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

    private void UpdateGroundedState()
    {
        Vector3 origin = groundCheckOrigin != null ? groundCheckOrigin.position : transform.position;
        isGrounded = Physics.CheckSphere(origin, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void UpdateVerticalSpeed()
    {
        verticalSpeed = rb != null ? rb.linearVelocity.y : 0f;
    }

    private void UpdateJumpTraversalState()
    {
        if (!isJumpTraversalActive)
            return;

        if (!isGrounded)
        {
            traversalHasLeftGround = true;
            return;
        }

        if (!traversalHasLeftGround)
            return;

        if (Time.time < traversalEarliestCompleteTime)
            return;

        isJumpTraversalActive = false;
        traversalControlLocked = false;
        activeJumpLink = null;
    }

    private void UpdateFacingFromVelocityOrInput()
    {
        if (manualFacingOverride)
        {
            facingSign = manualFacingSign >= 0 ? 1 : -1;
            return;
        }

        if (isJumpTraversalActive && rb != null && Mathf.Abs(rb.linearVelocity.x) > facingByVelocityThreshold)
        {
            facingSign = rb.linearVelocity.x >= 0f ? 1 : -1;
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

    private void ClampSettings()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        groundAcceleration = Mathf.Max(0f, groundAcceleration);
        groundDeceleration = Mathf.Max(0f, groundDeceleration);
        airControlMultiplier = Mathf.Max(0f, airControlMultiplier);
        groundCheckRadius = Mathf.Max(0f, groundCheckRadius);
        facingByVelocityThreshold = Mathf.Max(0f, facingByVelocityThreshold);
        fallbackJumpFlightTime = Mathf.Max(0.05f, fallbackJumpFlightTime);
        jumpTraversalCompletionGrace = Mathf.Max(0f, jumpTraversalCompletionGrace);
    }
}
