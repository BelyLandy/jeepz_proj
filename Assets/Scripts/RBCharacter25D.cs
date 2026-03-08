using UnityEngine;

#region Rigidbody compatibility (linearVelocity/velocity)
static class RBCompat
{
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
    public static void SetLinearVelocity(Rigidbody rb, Vector3 v) => rb.linearVelocity = v;
    public static Vector3 GetLinearVelocity(Rigidbody rb) => rb.linearVelocity;
#else
    public static void SetLinearVelocity(Rigidbody rb, Vector3 v) => rb.velocity = v;
    public static Vector3 GetLinearVelocity(Rigidbody rb) => rb.velocity;
#endif
}
#endregion

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class RBCharacter25D : MonoBehaviour
{
    private const float InputEpsilon = 0.01f;
    private const float SpeedEpsilon = 0.0001f;
    private const float DotEpsilon = 1e-5f;
    private const float HitDistanceTieEpsilon = 0.0025f;

    [Header("2.5D constraint")]
    public bool lockZ = true;
    public float lockedZ = 0f;

    [Header("Movement")]
    public float moveSpeed = 7f;
    public float acceleration = 60f;
    public float deceleration = 80f;
    [Range(0f, 1f)] public float airControl = 0.6f;

    [Header("Move Ramp / Blend Tree")]
    [Tooltip("Плавность набора направления от 0 к 1. Чем меньше значение, тем мягче старт.")]
    public float inputAcceleration = 8f;

    [Tooltip("Плавность отпускания/торможения направления к 0.")]
    public float inputDeceleration = 12f;

    [Header("Slope Handling")]
    public bool enableSlopeHandling = true;

    [Tooltip("Начиная с какого угла считать поверхность именно склоном")]
    [Range(0f, 89f)] public float slopeMinAngle = 1f;

    [Tooltip("Торможение после отпускания кнопки при движении вниз по склону. Меньше значение = дальше скольжение.")]
    public float downhillSlideDeceleration = 14f;

    [Tooltip("Минимальная скорость вдоль склона, чтобы после отпускания был заметный доскольз.")]
    public float downhillSlideMinSpeed = 1f;

    [Tooltip("Оставлено для совместимости. В текущей логике не используется как основной механизм.")]
    public float uphillStopDeceleration = 140f;

    [Tooltip("После вертикального прыжка с места на склоне кратко фиксировать героя при приземлении")]
    public bool stickToSlopeAfterVerticalJump = true;

    [Tooltip("Максимальная скорость вдоль склона, при которой прыжок считается 'с места'")]
    public float slopeLandingStickSpeed = 1.25f;

    [Tooltip("Опциональный жёсткий fallback: FreezePositionX, когда герой стоит на склоне без ввода")]
    public bool freezeXWhenIdleOnSlope = true;

    [Tooltip("Порог скорости вдоль склона, ниже которого можно жёстко фиксировать X")]
    public float slopeIdleLockSpeed = 0.05f;

    [Tooltip("На сколько секунд после вертикального прыжка с места на склоне держать жёсткую фиксацию")]
    public float slopeLandingLockTime = 0.12f;

    [Header("Debug")]
    public bool debugAcceleration = true;
    [Tooltip("Как часто писать лог разгона")]
    public float debugLogInterval = 0.1f;

    [Header("Jump")]
    [Tooltip("Импульс прыжка (будет перезаписан авто-настройкой, если autoTuneJump=true)")]
    public float jumpImpulse = 12f;
    public float coyoteTime = 0.1f;
    public float jumpBuffer = 0.1f;
    public bool cutJumpOnRelease = false;

    [Header("Double Jump")]
    public bool enableDoubleJump = true;
    public bool doubleJumpOnlyInAir = true;
    [Tooltip("Импульс второго прыжка (будет перезаписан авто-настройкой, если autoTuneJump=true)")]
    public float doubleJumpImpulse = 12f;

    [Header("Gravity")]
    [Tooltip("Используется только если autoTuneJump=false. Аналог gravityScale для 3D.")]
    public float manualGravityScale = 1f;

    [Header("Grounding")]
    public LayerMask groundMask;

    [Header("Ground Probe")]
    [Tooltip("Насколько глубоко вниз ищем землю под капсулой")]
    public float groundProbeDistance = 0.08f;

    [Tooltip("Небольшой отступ вверх для старта SphereCast, чтобы избежать дрожания")]
    public float groundProbeStartOffset = 0.02f;

    [Tooltip("Насколько уменьшаем радиус проверки земли, чтобы не цепляться краями")]
    public float groundProbeInset = 0.03f;

    [Tooltip("Если при первом касании пола скорость Y ниже этого значения, она обнуляется")]
    public float landingClampMinY = 0f;

    [Header("Wall stop")]
    [Tooltip("Дистанция проверки стены в сторону движения (в world units)")]
    public float wallCheckDistance = 0.5f;

    [Tooltip("Насколько 'высоко' по телу проверяем стену (0 = центр коллайдера)")]
    public float wallCheckHeightOffset = 0.0f;

    [Tooltip("Радиус для SphereCast (обычно чуть меньше половины толщины коллайдера)")]
    public float wallCheckRadius = 0.08f;

    [Header("Corner slide (anti-stuck on box corners)")]
    public bool enableCornerSlide = true;

    [Tooltip("Минимальная скорость вниз, когда упёрлись в угол/стену (units/s)")]
    public float cornerSlideDownSpeed = 12f;

    [Tooltip("Не трогаем, если летим вверх быстрее этого (units/s)")]
    public float cornerSlideOnlyWhenVyBelow = 0.1f;

    [Tooltip("Порог 'плоской земли' по нормали. 1 = идеально плоско")]
    [Range(0.5f, 1f)] public float flatGroundNormalY = 0.9f;

    [Header("Auto tune jump")]
    public bool autoTuneJump = true;

    public float pixelsPerUnit = 32f;
    public float jumpHeightPixels = 128f;
    public float timeToApex = 0.4265f;

    public float doubleJumpHeightPixels = 128f;
    public float doubleJumpTimeToApex = 0.4265f;

    public bool allowShortHop = true;

    private Rigidbody rb;
    private CapsuleCollider col;

    private float inputX;
    private float smoothedInputX;
    private float currentHorizontalSpeedAbs;

    private float lastGroundedTime = -999f;
    private float lastJumpPressedTime = -999f;
    private bool isGrounded;

    private bool wasGroundedLastFixed;
    private int jumpsRemaining;

    private float lastJumpExecutedTime = -999f;
    private float lastDebugLogTime = -999f;

    private float runtimeGravityScale = 1f;
    private bool pendingSlopeStickAfterJump;
    private float slopeLockUntilTime = -999f;
    private bool slopeXLocked;

    private readonly RaycastHit[] castHits = new RaycastHit[16];

    public bool IsGroundedNow => isGrounded;
    public float LastJumpTime => lastJumpExecutedTime;
    public float RuntimeGravityScale => runtimeGravityScale;

    public float SmoothedInputX => smoothedInputX;
    public float MoveBlend01 => Mathf.Abs(smoothedInputX);
    public float HorizontalSpeedAbs => currentHorizontalSpeedAbs;
    public float SpeedNormalized => moveSpeed > 1e-5f ? Mathf.Clamp01(currentHorizontalSpeedAbs / moveSpeed) : 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        RefreshConstraints(false);
        rb.useGravity = true;

        if (lockZ)
            lockedZ = transform.position.z;

        if (autoTuneJump)
            RecalculateJumpAndGravity();
        else
            runtimeGravityScale = Mathf.Max(0f, manualGravityScale);

        jumpsRemaining = enableDoubleJump ? 2 : 1;
    }

    void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        airControl = Mathf.Clamp01(airControl);

        inputAcceleration = Mathf.Max(0f, inputAcceleration);
        inputDeceleration = Mathf.Max(0f, inputDeceleration);

        slopeMinAngle = Mathf.Clamp(slopeMinAngle, 0f, 89f);
        downhillSlideDeceleration = Mathf.Max(0f, downhillSlideDeceleration);
        downhillSlideMinSpeed = Mathf.Max(0f, downhillSlideMinSpeed);
        uphillStopDeceleration = Mathf.Max(0f, uphillStopDeceleration);
        slopeLandingStickSpeed = Mathf.Max(0f, slopeLandingStickSpeed);
        slopeIdleLockSpeed = Mathf.Max(0f, slopeIdleLockSpeed);
        slopeLandingLockTime = Mathf.Max(0f, slopeLandingLockTime);

        debugLogInterval = Mathf.Max(0.01f, debugLogInterval);

        jumpImpulse = Mathf.Max(0f, jumpImpulse);
        doubleJumpImpulse = Mathf.Max(0f, doubleJumpImpulse);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        jumpBuffer = Mathf.Max(0f, jumpBuffer);

        manualGravityScale = Mathf.Max(0f, manualGravityScale);

        groundProbeDistance = Mathf.Max(0.001f, groundProbeDistance);
        groundProbeStartOffset = Mathf.Max(0f, groundProbeStartOffset);
        groundProbeInset = Mathf.Max(0f, groundProbeInset);

        wallCheckDistance = Mathf.Max(0f, wallCheckDistance);
        wallCheckRadius = Mathf.Max(0.001f, wallCheckRadius);

        cornerSlideDownSpeed = Mathf.Max(0f, cornerSlideDownSpeed);

        pixelsPerUnit = Mathf.Max(0.0001f, pixelsPerUnit);
        jumpHeightPixels = Mathf.Max(0f, jumpHeightPixels);
        timeToApex = Mathf.Max(0.0001f, timeToApex);
        doubleJumpHeightPixels = Mathf.Max(0f, doubleJumpHeightPixels);
        doubleJumpTimeToApex = Mathf.Max(0.0001f, doubleJumpTimeToApex);

        if (Application.isPlaying)
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (col == null) col = GetComponent<CapsuleCollider>();

            RefreshConstraints(slopeXLocked);

            if (autoTuneJump)
                RecalculateJumpAndGravity();
            else
                runtimeGravityScale = Mathf.Max(0f, manualGravityScale);
        }
    }

    void Update()
    {
        inputX = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump"))
            lastJumpPressedTime = Time.time;

        if (allowShortHop && cutJumpOnRelease && Input.GetButtonUp("Jump"))
        {
            var vel = RBCompat.GetLinearVelocity(rb);
            if (vel.y > 0f)
            {
                RBCompat.SetLinearVelocity(
                    rb,
                    new Vector3(vel.x, vel.y * 0.5f, lockZ ? 0f : vel.z)
                );
            }
        }
    }

    void FixedUpdate()
    {
        ApplyExtraGravity();

        RaycastHit groundHit;
        isGrounded = CheckGrounded(out groundHit);

        bool justLandedThisFixed = !wasGroundedLastFixed && isGrounded;
        bool leftGroundThisFixed = wasGroundedLastFixed && !isGrounded;

        if (leftGroundThisFixed)
        {
            slopeLockUntilTime = -999f;
            UpdateSlopeXConstraint(false);
        }

        if (!isGrounded && Mathf.Abs(inputX) > InputEpsilon)
            pendingSlopeStickAfterJump = false;

        if (isGrounded)
        {
            lastGroundedTime = Time.time;

            if (justLandedThisFixed)
            {
                jumpsRemaining = enableDoubleJump ? 2 : 1;

                var landVel = RBCompat.GetLinearVelocity(rb);
                if (landVel.y < landingClampMinY)
                {
                    RBCompat.SetLinearVelocity(
                        rb,
                        new Vector3(landVel.x, 0f, lockZ ? 0f : landVel.z)
                    );
                }
            }
        }
        else
        {
            if (leftGroundThisFixed)
            {
                var velNow = RBCompat.GetLinearVelocity(rb);

                bool recentlyJumped =
                    (Time.time - lastJumpExecutedTime) <= (Time.fixedDeltaTime * 1.5f);

                if (!recentlyJumped && velNow.y <= 0.01f)
                    jumpsRemaining = enableDoubleJump ? Mathf.Min(jumpsRemaining, 1) : 0;
            }
        }

        CheckWalls(out bool blockedLeft, out bool blockedRight);

        Vector3 vel = RBCompat.GetLinearVelocity(rb);

        float desiredInput = inputX;

        float rampSpeed = (Mathf.Abs(desiredInput) > Mathf.Abs(smoothedInputX))
            ? inputAcceleration
            : inputDeceleration;

        if (Mathf.Abs(desiredInput) > InputEpsilon &&
            Mathf.Abs(smoothedInputX) > InputEpsilon &&
            Mathf.Sign(desiredInput) != Mathf.Sign(smoothedInputX))
        {
            rampSpeed = inputDeceleration;
        }

        smoothedInputX = Mathf.MoveTowards(
            smoothedInputX,
            desiredInput,
            rampSpeed * Time.fixedDeltaTime
        );

        GetSlopeData(
            groundHit,
            out bool onSlope,
            out Vector3 slopeTangent,
            out float slopeAngle,
            out float downhillSign
        );

        bool noRawInput = Mathf.Abs(inputX) <= InputEpsilon;

        float currentMoveSpeed = GetCurrentMoveSpeed(
            vel,
            isGrounded && onSlope,
            slopeTangent,
            justLandedThisFixed
        );

        if (justLandedThisFixed)
        {
            TryStartSlopeLandingLock(onSlope, noRawInput, currentMoveSpeed);

            if (isGrounded && onSlope && noRawInput && Time.time < slopeLockUntilTime)
                currentMoveSpeed = 0f;
        }

        ApplySlopeAntiSlide(groundHit, onSlope);

        float targetMoveSpeed = smoothedInputX * moveSpeed;

        if (targetMoveSpeed > 0f && blockedRight) targetMoveSpeed = 0f;
        if (targetMoveSpeed < 0f && blockedLeft) targetMoveSpeed = 0f;

        float downhillSignedSpeed = 0f;
        if (isGrounded && onSlope)
            downhillSignedSpeed = currentMoveSpeed * downhillSign;

        bool landingLockActive = Time.time < slopeLockUntilTime;

        bool canHardLockBySpeed =
            landingLockActive ||
            Mathf.Abs(currentMoveSpeed) <= slopeIdleLockSpeed ||
            (downhillSignedSpeed >= 0f && downhillSignedSpeed <= downhillSlideMinSpeed);

        bool shouldLockSlopeX =
            freezeXWhenIdleOnSlope &&
            lockZ &&
            isGrounded &&
            onSlope &&
            noRawInput &&
            canHardLockBySpeed;

        UpdateSlopeXConstraint(shouldLockSlopeX);

        float newMoveSpeed;

        if (isGrounded && onSlope && noRawInput)
        {
            if (landingLockActive)
            {
                newMoveSpeed = 0f;
                smoothedInputX = 0f;
            }
            else if (downhillSignedSpeed > downhillSlideMinSpeed)
            {
                newMoveSpeed = Mathf.MoveTowards(
                    currentMoveSpeed,
                    0f,
                    downhillSlideDeceleration * Time.fixedDeltaTime
                );
            }
            else
            {
                newMoveSpeed = 0f;
                smoothedInputX = 0f;
            }
        }
        else
        {
            float factor = isGrounded ? 1f : airControl;
            float moveRate = (Mathf.Abs(targetMoveSpeed) > InputEpsilon ? acceleration : deceleration) * factor;

            newMoveSpeed = Mathf.MoveTowards(
                currentMoveSpeed,
                targetMoveSpeed,
                moveRate * Time.fixedDeltaTime
            );
        }

        if (newMoveSpeed > 0f && blockedRight) newMoveSpeed = 0f;
        if (newMoveSpeed < 0f && blockedLeft) newMoveSpeed = 0f;

        currentHorizontalSpeedAbs = Mathf.Abs(newMoveSpeed);

        LogAccelerationDebug(
            currentMoveSpeed,
            desiredInput,
            targetMoveSpeed,
            newMoveSpeed,
            blockedLeft,
            blockedRight,
            onSlope,
            slopeAngle,
            downhillSignedSpeed
        );

        if (isGrounded && onSlope)
        {
            Vector3 finalVel;

            if (shouldLockSlopeX || (noRawInput && Mathf.Abs(newMoveSpeed) <= SpeedEpsilon))
            {
                finalVel = Vector3.zero;
            }
            else
            {
                finalVel = slopeTangent * newMoveSpeed;
            }

            if (lockZ) finalVel.z = 0f;
            RBCompat.SetLinearVelocity(rb, finalVel);
        }
        else
        {
            RBCompat.SetLinearVelocity(rb, new Vector3(newMoveSpeed, vel.y, lockZ ? 0f : vel.z));
        }

        if (enableCornerSlide)
        {
            bool pushingIntoWall =
                (smoothedInputX > InputEpsilon && blockedRight) ||
                (smoothedInputX < -InputEpsilon && blockedLeft);

            if (pushingIntoWall)
            {
                bool hasFlatGround =
                    isGrounded &&
                    groundHit.collider != null &&
                    groundHit.normal.y >= flatGroundNormalY;

                if (!hasFlatGround)
                {
                    var v = RBCompat.GetLinearVelocity(rb);

                    if (v.y <= cornerSlideOnlyWhenVyBelow)
                    {
                        float desiredY = -Mathf.Abs(cornerSlideDownSpeed);
                        if (v.y > desiredY)
                        {
                            RBCompat.SetLinearVelocity(
                                rb,
                                new Vector3(v.x, desiredY, lockZ ? 0f : v.z)
                            );
                        }
                    }
                }
            }
        }

        bool canCoyote = Time.time - lastGroundedTime <= coyoteTime;
        bool buffered = Time.time - lastJumpPressedTime <= jumpBuffer;

        if (buffered)
        {
            bool groundJumpAllowed = (isGrounded || canCoyote) && jumpsRemaining > 0;
            bool airJumpAllowed = enableDoubleJump && jumpsRemaining > 0 && (!isGrounded && !canCoyote);

            if (!doubleJumpOnlyInAir)
                airJumpAllowed = enableDoubleJump && jumpsRemaining > 0 && !isGrounded;

            if (groundJumpAllowed || airJumpAllowed)
            {
                bool isFirstJump = (!enableDoubleJump) || (jumpsRemaining >= 2);
                float impulse = isFirstJump ? jumpImpulse : doubleJumpImpulse;

                bool jumpedFromStandstillOnSlope =
                    stickToSlopeAfterVerticalJump &&
                    isGrounded &&
                    onSlope &&
                    Mathf.Abs(inputX) <= InputEpsilon &&
                    Mathf.Abs(currentMoveSpeed) <= slopeLandingStickSpeed;

                pendingSlopeStickAfterJump = jumpedFromStandstillOnSlope;

                slopeLockUntilTime = -999f;
                UpdateSlopeXConstraint(false);

                vel = RBCompat.GetLinearVelocity(rb);
                RBCompat.SetLinearVelocity(rb, new Vector3(vel.x, 0f, lockZ ? 0f : vel.z));
                rb.AddForce(Vector3.up * impulse, ForceMode.Impulse);

                jumpsRemaining = Mathf.Max(0, jumpsRemaining - 1);
                lastJumpPressedTime = -999f;
                lastJumpExecutedTime = Time.time;
            }
        }

        wasGroundedLastFixed = isGrounded;

        if (!isGrounded)
            UpdateSlopeXConstraint(false);
    }

    float GetCurrentMoveSpeed(
        Vector3 velocity,
        bool groundedOnSlope,
        Vector3 slopeTangent,
        bool justLanded)
    {
        if (!groundedOnSlope)
            return velocity.x;

        if (justLanded)
            return ConvertWorldXToSlopeSpeed(velocity.x, slopeTangent);

        return Vector3.Dot(velocity, slopeTangent);
    }

    float ConvertWorldXToSlopeSpeed(float worldX, Vector3 slopeTangent)
    {
        if (Mathf.Abs(slopeTangent.x) <= DotEpsilon)
            return 0f;

        return worldX / slopeTangent.x;
    }

    void RefreshConstraints(bool freezeX)
    {
        if (rb == null) return;

        rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (lockZ)
            rb.constraints |= RigidbodyConstraints.FreezePositionZ;

        if (freezeX && lockZ)
            rb.constraints |= RigidbodyConstraints.FreezePositionX;
    }

    void UpdateSlopeXConstraint(bool shouldLock)
    {
        if (!freezeXWhenIdleOnSlope || !lockZ)
            shouldLock = false;

        if (slopeXLocked == shouldLock)
            return;

        slopeXLocked = shouldLock;
        RefreshConstraints(slopeXLocked);
    }

    void TryStartSlopeLandingLock(bool onSlope, bool noRawInput, float landingMoveSpeed)
    {
        bool requestedByVerticalJumpStick = pendingSlopeStickAfterJump;
        pendingSlopeStickAfterJump = false;

        if (!stickToSlopeAfterVerticalJump)
            return;

        if (!onSlope)
            return;

        if (!noRawInput)
            return;

        if (requestedByVerticalJumpStick || Mathf.Abs(landingMoveSpeed) <= slopeLandingStickSpeed)
        {
            slopeLockUntilTime = Time.time + slopeLandingLockTime;
            smoothedInputX = 0f;
            currentHorizontalSpeedAbs = 0f;
        }
    }

    void RecalculateJumpAndGravity()
    {
        float globalG = Mathf.Abs(Physics.gravity.y);
        if (globalG < 1e-5f) globalG = 9.81f;

        float hUnits = jumpHeightPixels / Mathf.Max(1e-5f, pixelsPerUnit);
        float t = Mathf.Max(1e-5f, timeToApex);

        float gNeeded = (2f * hUnits) / (t * t);
        float v0 = gNeeded * t;

        runtimeGravityScale = gNeeded / globalG;
        jumpImpulse = rb.mass * v0;

        float t2 = Mathf.Max(1e-5f, doubleJumpTimeToApex);
        float v02 = gNeeded * t2;
        doubleJumpImpulse = rb.mass * v02;
    }

    float GetEffectiveGravityScale()
    {
        return autoTuneJump
            ? runtimeGravityScale
            : Mathf.Max(0f, manualGravityScale);
    }

    void ApplyExtraGravity()
    {
        if (autoTuneJump)
        {
            float diff = runtimeGravityScale - 1f;
            if (Mathf.Abs(diff) < 1e-4f) return;

            rb.AddForce(Physics.gravity * diff, ForceMode.Acceleration);
        }
        else
        {
            float scale = Mathf.Max(0f, manualGravityScale);
            float diff = scale - 1f;
            if (Mathf.Abs(diff) < 1e-4f) return;

            rb.AddForce(Physics.gravity * diff, ForceMode.Acceleration);
        }
    }

    void ApplySlopeAntiSlide(RaycastHit groundHit, bool onSlope)
    {
        if (!enableSlopeHandling) return;
        if (!isGrounded) return;
        if (!onSlope) return;
        if (groundHit.collider == null) return;

        Vector3 effectiveGravity = Physics.gravity * GetEffectiveGravityScale();
        Vector3 gravityAlongSlope = Vector3.ProjectOnPlane(effectiveGravity, groundHit.normal);

        rb.AddForce(-gravityAlongSlope, ForceMode.Acceleration);
    }

    void GetSlopeData(
        RaycastHit groundHit,
        out bool onSlope,
        out Vector3 slopeTangent,
        out float slopeAngle,
        out float downhillSign)
    {
        onSlope = false;
        slopeTangent = Vector3.right;
        slopeAngle = 0f;
        downhillSign = 1f;

        if (!enableSlopeHandling) return;
        if (!isGrounded) return;
        if (groundHit.collider == null) return;

        slopeAngle = Vector3.Angle(groundHit.normal, Vector3.up);
        if (slopeAngle < slopeMinAngle) return;
        if (slopeAngle >= 89f) return;

        Vector3 tangent;

        if (lockZ)
        {
            tangent = Vector3.Cross(Vector3.forward, groundHit.normal);
            tangent.z = 0f;
        }
        else
        {
            tangent = Vector3.ProjectOnPlane(Vector3.right, groundHit.normal);
        }

        float mag = tangent.magnitude;
        if (mag < DotEpsilon) return;

        tangent /= mag;

        if (Vector3.Dot(tangent, Vector3.right) < 0f)
            tangent = -tangent;

        slopeTangent = tangent;

        Vector3 downhill = Vector3.ProjectOnPlane(Physics.gravity, groundHit.normal);
        downhillSign = Vector3.Dot(slopeTangent, downhill) >= 0f ? 1f : -1f;

        onSlope = true;
    }

    bool CheckGrounded(out RaycastHit bestHit)
    {
        bestHit = default;

        Bounds b = col.bounds;

        float baseRadius = Mathf.Min(b.extents.x, b.extents.z);
        float radius = Mathf.Max(0.02f, baseRadius - groundProbeInset);

        float z = lockZ ? lockedZ : b.center.z;

        Vector3 origin = new Vector3(
            b.center.x,
            b.min.y + radius + groundProbeStartOffset,
            z
        );

        float castDistance = groundProbeDistance + groundProbeStartOffset;

        int count = Physics.SphereCastNonAlloc(
            origin,
            radius,
            Vector3.down,
            castHits,
            castDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        bool grounded = false;
        float bestDistance = float.MaxValue;
        float bestNormalY = -1f;

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = castHits[i];

            if (h.collider == null) continue;
            if (h.collider.attachedRigidbody == rb) continue;
            if (h.normal.y <= 0.05f) continue;

            bool betterHit =
                !grounded ||
                h.distance < bestDistance - HitDistanceTieEpsilon ||
                (
                    Mathf.Abs(h.distance - bestDistance) <= HitDistanceTieEpsilon &&
                    h.normal.y > bestNormalY
                );

            if (betterHit)
            {
                bestDistance = h.distance;
                bestNormalY = h.normal.y;
                bestHit = h;
                grounded = true;
            }
        }

        return grounded;
    }

    void CheckWalls(out bool blockedLeft, out bool blockedRight)
    {
        Bounds b = col.bounds;
        float z = lockZ ? lockedZ : b.center.z;

        Vector3 origin = new Vector3(
            b.center.x,
            b.center.y + wallCheckHeightOffset,
            z
        );

        float r = Mathf.Max(0.001f, wallCheckRadius);

        blockedRight = SphereCastHitsSomething(origin, r, Vector3.right, wallCheckDistance);
        blockedLeft = SphereCastHitsSomething(origin, r, Vector3.left, wallCheckDistance);
    }

    bool SphereCastHitsSomething(Vector3 origin, float radius, Vector3 dir, float dist)
    {
        int count = Physics.SphereCastNonAlloc(
            origin,
            radius,
            dir,
            castHits,
            dist,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = castHits[i];
            if (h.collider == null) continue;
            if (h.collider.attachedRigidbody == rb) continue;

            if (Vector3.Dot(h.normal, dir) < -0.1f)
                return true;
        }

        return false;
    }

    void LogAccelerationDebug(
        float currentMoveBefore,
        float desiredInput,
        float targetMove,
        float newMove,
        bool blockedLeft,
        bool blockedRight,
        bool onSlope,
        float slopeAngle,
        float downhillSignedSpeed)
    {
        if (!debugAcceleration) return;
        if (Time.time - lastDebugLogTime < debugLogInterval) return;

        lastDebugLogTime = Time.time;

        /*
        Debug.Log(
            $"[RBCharacter25D] " +
            $"rawInput={inputX:0.00} " +
            $"smoothInput={smoothedInputX:0.00} " +
            $"desiredInput={desiredInput:0.00} " +
            $"moveBefore={currentMoveBefore:0.00} " +
            $"targetMove={targetMove:0.00} " +
            $"moveAfter={newMove:0.00} " +
            $"speedAbs={currentHorizontalSpeedAbs:0.00} " +
            $"speedNorm={SpeedNormalized:0.00} " +
            $"grounded={isGrounded} " +
            $"onSlope={onSlope} " +
            $"slopeAngle={slopeAngle:0.0} " +
            $"downhillSignedSpeed={downhillSignedSpeed:0.00} " +
            $"xLocked={slopeXLocked} " +
            $"blockedL={blockedLeft} " +
            $"blockedR={blockedRight}",
            this
        );
        */
    }

    void OnDrawGizmosSelected()
    {
        var capsule = GetComponent<CapsuleCollider>();
        if (capsule == null) return;

        Bounds b = capsule.bounds;
        float baseRadius = Mathf.Min(b.extents.x, b.extents.z);
        float probeRadius = Mathf.Max(0.02f, baseRadius - groundProbeInset);

        float z = lockZ ? transform.position.z : b.center.z;

        Vector3 probeOrigin = new Vector3(
            b.center.x,
            b.min.y + probeRadius + groundProbeStartOffset,
            z
        );

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(probeOrigin, probeRadius);
        Gizmos.DrawWireSphere(
            probeOrigin + Vector3.down * (groundProbeDistance + groundProbeStartOffset),
            probeRadius
        );

        Gizmos.color = Color.cyan;
        Vector3 wallOrigin = new Vector3(
            b.center.x,
            b.center.y + wallCheckHeightOffset,
            z
        );

        Gizmos.DrawWireSphere(wallOrigin + Vector3.right * wallCheckDistance, wallCheckRadius);
        Gizmos.DrawWireSphere(wallOrigin + Vector3.left * wallCheckDistance, wallCheckRadius);
    }
}