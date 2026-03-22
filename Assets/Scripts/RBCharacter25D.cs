using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class RBCharacter25D : MonoBehaviour
{
    private const float InputEpsilon = 0.01f;
    private const float SpeedEpsilon = 0.0001f;
    private const float DotEpsilon = 1e-5f;
    private const float HitDistanceTieEpsilon = 0.0025f;
    private const float InvalidPastTime = -999f;

    private const int WallSideNone = 0;
    private const int WallSideLeft = -1;
    private const int WallSideRight = +1;

    private struct GroundInfo
    {
        public bool grounded;
        public RaycastHit hit;
        public bool onSlope;
        public Vector3 slopeTangent;
        public float slopeAngle;
        public float downhillSign;
    }

    private struct WallInfo
    {
        public bool blockedLeft;
        public bool blockedRight;
        public RaycastHit leftHit;
        public RaycastHit rightHit;
    }

    [Header("2.5D Constraint")]
    [SerializeField] private bool lockZ = true;
    [SerializeField] private float lockedZ = 0f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 40f;
    [SerializeField] private float deceleration = 60f;
    [SerializeField, Range(0f, 1f)] private float airControl = 0.8f;

    [Header("Air Movement")]
    [Tooltip("Насколько быстро гасится горизонтальная скорость в воздухе после отпускания кнопки направления.")]
    [SerializeField] private float airDeceleration = 4f;

    [Header("Double Jump Speed Boost")]
    [Tooltip("Включить временный бонус к moveSpeed после double jump.")]
    [SerializeField] private bool enableDoubleJumpSpeedBoost = true;

    [Tooltip("На сколько увеличить moveSpeed после double jump.")]
    [SerializeField] private float doubleJumpMoveSpeedBonus = 2f;

    [Header("Jump Cooldown After Double Jump Landing")]
    [Tooltip("После приземления из double jump включать очень короткий кулдаун на новый прыжок.")]
    [SerializeField] private bool enableJumpCooldownAfterDoubleJumpLanding = true;

    [Tooltip("Длительность маленького кулдауна на прыжок после приземления из double jump.")]
    [SerializeField] private float jumpCooldownAfterDoubleJumpLanding = 0.08f;

    [Header("Move Ramp / Blend Tree")]
    [Tooltip("Плавность набора направления от 0 к 1. Чем меньше значение, тем мягче старт.")]
    [SerializeField] private float inputAcceleration = 8f;

    [Tooltip("Плавность отпускания/торможения направления к 0.")]
    [SerializeField] private float inputDeceleration = 12f;

    [Header("Slope Handling")]
    [SerializeField] private bool enableSlopeHandling = true;

    [Tooltip("Начиная с какого угла считать поверхность именно склоном.")]
    [SerializeField, Range(0f, 89f)] private float slopeMinAngle = 1f;

    [Tooltip("Торможение после отпускания кнопки при движении вниз по склону. Меньше значение = дальше скольжение.")]
    [SerializeField] private float downhillSlideDeceleration = 12f;

    [Tooltip("Минимальная скорость вдоль склона, чтобы после отпускания был заметный доскольз.")]
    [SerializeField] private float downhillSlideMinSpeed = 1f;

    [Tooltip("После вертикального прыжка с места на склоне кратко фиксировать героя при приземлении.")]
    [SerializeField] private bool stickToSlopeAfterVerticalJump = true;

    [Tooltip("Максимальная скорость вдоль склона, при которой прыжок считается 'с места'.")]
    [SerializeField] private float slopeLandingStickSpeed = 1.25f;

    [Tooltip("Опциональный жёсткий fallback: FreezePositionX, когда герой стоит на склоне без ввода.")]
    [SerializeField] private bool freezeXWhenIdleOnSlope = true;

    [Tooltip("Порог скорости вдоль склона, ниже которого можно жёстко фиксировать X.")]
    [SerializeField] private float slopeIdleLockSpeed = 0.05f;

    [Tooltip("На сколько секунд после вертикального прыжка с места на склоне держать жёсткую фиксацию.")]
    [SerializeField] private float slopeLandingLockTime = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool debugAcceleration = false;
    [Tooltip("Как часто писать лог разгона.")]
    [SerializeField] private float debugLogInterval = 0.1f;

    [Header("Jump")]
    [Tooltip("Импульс прыжка (будет перезаписан авто-настройкой, если autoTuneJump=true).")]
    [SerializeField] private float jumpImpulse = 12f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBuffer = 0.1f;
    [SerializeField] private bool cutJumpOnRelease = false;

    [Header("Double Jump")]
    [SerializeField] private bool enableDoubleJump = true;
    [SerializeField] private bool doubleJumpOnlyInAir = true;

    [Tooltip("Импульс второго прыжка (будет перезаписан авто-настройкой, если autoTuneJump=true).")]
    [SerializeField] private float doubleJumpImpulse = 12f;

    [Header("Wall Stop")]
    [Tooltip("Дистанция проверки стены в сторону движения (world units).")]
    [SerializeField] private float wallCheckDistance = 0.5f;

    [Tooltip("Насколько 'высоко' по телу проверяем стену (0 = центр коллайдера).")]
    [SerializeField] private float wallCheckHeightOffset = 0.0f;

    [Tooltip("Радиус для SphereCast (обычно чуть меньше половины толщины коллайдера).")]
    [SerializeField] private float wallCheckRadius = 0.08f;

    [Header("Wall Slide / Wall Jump")]
    [SerializeField] private bool enableWallSlide = true;

    [Tooltip("Чтобы начать зацеп за стену, нужно удерживать направление в сторону стены.")]
    [SerializeField] private bool requireInputToLatchWall = true;

    [Tooltip("Если во время зацепа удерживать направление от стены, герой сможет отцепиться.")]
    [SerializeField] private bool detachFromWallOnOppositeInput = true;

    [Tooltip("Сколько времени нужно удерживать направление от стены, чтобы отцепиться.")]
    [SerializeField] private float wallDetachHoldTime = 0.3f;

    [Tooltip("Максимальная скорость скольжения вниз по стене.")]
    [SerializeField] private float wallSlideSpeed = 2.5f;

    [Tooltip("Разрешить герою после прыжка в стену ещё немного скользить вверх по ней, плавно замедляясь.")]
    [SerializeField] private bool allowWallSlideUpwardMomentum = true;

    [Tooltip("Насколько быстро гасится движение вверх вдоль стены после зацепа.")]
    [SerializeField] private float wallUpwardDeceleration = 18f;

    [Tooltip("Горизонтальная скорость отскока от стены.")]
    [SerializeField] private float wallJumpHorizontalSpeed = 8f;

    [Tooltip("Вертикальная скорость прыжка от стены.")]
    [SerializeField] private float wallJumpVerticalSpeed = 10f;

    [Tooltip("Сохранять ли возможность одного двойного прыжка после wall jump.")]
    [SerializeField] private bool allowDoubleJumpAfterWallJump = true;

    [Tooltip("Включить маленький кулдаун перед повторным прилипанием к той же стене.")]
    [SerializeField] private bool enableWallReattachCooldown = true;

    [Tooltip("Длительность кулдауна перед повторным прилипанием к той же стене.")]
    [SerializeField] private float wallReattachCooldown = 0.12f;

    [Tooltip("Насколько нормаль должна смотреть по X, чтобы считаться стеной.")]
    [SerializeField, Range(0f, 1f)] private float wallMinNormalX = 0.7f;

    [Tooltip("Насколько мала должна быть составляющая нормали по Y, чтобы считаться стеной.")]
    [SerializeField, Range(0f, 1f)] private float wallMaxNormalY = 0.2f;

    [Header("Gravity")]
    [Tooltip("Используется только если autoTuneJump=false. Аналог gravityScale для 3D.")]
    [SerializeField] private float manualGravityScale = 1f;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask;

    [Header("Ground Probe")]
    [Tooltip("Насколько глубоко вниз ищем землю под капсулой.")]
    [SerializeField] private float groundProbeDistance = 0.08f;

    [Tooltip("Небольшой отступ вверх для старта SphereCast, чтобы избежать дрожания.")]
    [SerializeField] private float groundProbeStartOffset = 0.02f;

    [Tooltip("Насколько уменьшаем радиус проверки земли, чтобы не цепляться краями.")]
    [SerializeField] private float groundProbeInset = 0.05f;

    [Tooltip("Если при первом касании пола скорость Y ниже этого значения, она обнуляется.")]
    [SerializeField] private float landingClampMinY = 0f;

    [Header("Auto Tune Jump")]
    [SerializeField] private bool autoTuneJump = true;
    [SerializeField] private float pixelsPerUnit = 32f;
    [SerializeField] private float jumpHeightPixels = 128f;
    [SerializeField] private float timeToApex = 0.4265f;
    [SerializeField] private float doubleJumpHeightPixels = 128f;

    private Rigidbody rb;
    private CapsuleCollider col;
    private RBCharacter25DVaulting vaulting;

    private float inputX;
    private float smoothedInputX;
    private float currentHorizontalSpeedAbs;
    private float lastNonZeroInputX = 1f;

    private float lastGroundedTime = InvalidPastTime;
    private float lastJumpPressedTime = InvalidPastTime;
    private float lastJumpExecutedTime = InvalidPastTime;
    private float lastDebugLogTime = InvalidPastTime;

    private bool isGrounded;
    private bool wasGroundedLastFixed;
    private int jumpsRemaining;

    private float runtimeGravityScale = 1f;
    private bool pendingSlopeStickAfterJump;
    private float slopeLockUntilTime = InvalidPastTime;
    private bool slopeXLocked;

    private bool isWallSliding;
    private int wallSlideSide = WallSideNone;
    private float wallReattachLockUntilTime = InvalidPastTime;
    private int wallReattachLockedSide = WallSideNone;
    private float wallDetachHoldTimer;

    private bool usedDoubleJumpSinceLastGrounded;
    private bool isDoubleJumpSpeedBoostActive;
    private float jumpBlockedUntilTime = InvalidPastTime;

    private readonly RaycastHit[] castHits = new RaycastHit[16];

    public bool IsGroundedNow => isGrounded;
    public bool IsWallSliding => isWallSliding;
    public bool IsVaultingNow => vaulting != null && vaulting.IsVaulting;
    public int WallSlideSide => wallSlideSide;
    public float LastJumpTime => lastJumpExecutedTime;
    public float RuntimeGravityScale => runtimeGravityScale;
    public float SmoothedInputX => smoothedInputX;
    public float MoveBlend01 => Mathf.Abs(smoothedInputX);
    public float HorizontalSpeedAbs => currentHorizontalSpeedAbs;
    public float EffectiveMoveSpeed => GetEffectiveMoveSpeed();
    public float SpeedNormalized => EffectiveMoveSpeed > DotEpsilon
        ? Mathf.Clamp01(currentHorizontalSpeedAbs / EffectiveMoveSpeed)
        : 0f;

    public Rigidbody RigidbodyComponent => rb;
    public CapsuleCollider CapsuleColliderComponent => col;
    public LayerMask GroundMask => groundMask;
    public bool UsesLockedZ => lockZ;
    public float LockedZPosition => lockedZ;
    public int VaultFacingSignFromInput => lastNonZeroInputX < 0f ? -1 : 1;

    private void Awake()
    {
        CacheComponents();
        InitializeRuntimeState();
    }

    private void OnValidate()
    {
        ClampSettings();

        if (!Application.isPlaying)
            return;

        CacheComponents();
        RecalculateRuntimeSettings();
        ApplyManagedConstraints(slopeXLocked);
    }

    private void Update()
    {
        ReadInput();
        HandleJumpReleaseCut();
    }

    private void FixedUpdate()
    {
        if (IsVaultingNow)
        {
            vaulting.StepActiveVault();
            wasGroundedLastFixed = false;
            return;
        }

        ApplyExtraGravity();

        if (!enableDoubleJumpSpeedBoost)
            isDoubleJumpSpeedBoostActive = false;

        GroundInfo ground = ProbeGround();
        UpdateGroundedState(ground, out bool justLandedThisFixed, out bool leftGroundThisFixed);

        if (leftGroundThisFixed)
        {
            slopeLockUntilTime = InvalidPastTime;
            UpdateSlopeXConstraint(false);
        }

        if (!isGrounded && Mathf.Abs(inputX) > InputEpsilon)
            pendingSlopeStickAfterJump = false;

        if (vaulting != null && vaulting.TryStartVault())
        {
            currentHorizontalSpeedAbs = 0f;
            wasGroundedLastFixed = false;
            return;
        }

        WallInfo wall = CheckWalls();
        bool blockedLeft = wall.blockedLeft;
        bool blockedRight = wall.blockedRight;

        UpdateSmoothedInput();
        UpdateWallSlideState(wall);

        if (TryConsumeWallJump())
        {
            currentHorizontalSpeedAbs = Mathf.Abs(rb.linearVelocity.x);
            wasGroundedLastFixed = isGrounded;
            UpdateSlopeXConstraint(false);
            return;
        }

        Vector3 currentVelocity = rb.linearVelocity;
        float currentPlanarSpeed = GetCurrentPlanarSpeed(currentVelocity, ground, justLandedThisFixed);

        if (justLandedThisFixed)
        {
            TryStartSlopeLandingLock(ground, currentPlanarSpeed);

            if (ground.onSlope && Mathf.Abs(inputX) <= InputEpsilon && IsSlopeLandingLockActive())
                currentPlanarSpeed = 0f;
        }

        ApplySlopeAntiSlide(ground);

        bool noRawInput = Mathf.Abs(inputX) <= InputEpsilon;
        float targetPlanarSpeed = GetTargetPlanarSpeed(blockedLeft, blockedRight);
        float downhillSignedSpeed = ground.onSlope ? currentPlanarSpeed * ground.downhillSign : 0f;

        bool shouldLockSlopeX = ShouldLockSlopeX(ground, noRawInput, currentPlanarSpeed, downhillSignedSpeed);
        UpdateSlopeXConstraint(shouldLockSlopeX);

        float resolvedPlanarSpeed = ResolvePlanarSpeed(
            targetPlanarSpeed,
            currentPlanarSpeed,
            downhillSignedSpeed,
            noRawInput,
            blockedLeft,
            blockedRight,
            ground.onSlope
        );

        currentHorizontalSpeedAbs = Mathf.Abs(resolvedPlanarSpeed);

        LogAccelerationDebug(
            currentPlanarSpeed,
            targetPlanarSpeed,
            resolvedPlanarSpeed,
            blockedLeft,
            blockedRight,
            ground,
            downhillSignedSpeed
        );

        ApplyFinalVelocity(ground, currentVelocity, resolvedPlanarSpeed, shouldLockSlopeX, noRawInput);
        ApplyWallSlideVelocity();
        TryConsumeBufferedJump(ground, currentPlanarSpeed);

        wasGroundedLastFixed = isGrounded;

        if (!isGrounded)
            UpdateSlopeXConstraint(false);
    }

    private void CacheComponents()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (col == null) col = GetComponent<CapsuleCollider>();
        if (vaulting == null) vaulting = GetComponent<RBCharacter25DVaulting>();
    }

    private void InitializeRuntimeState()
    {
        rb.useGravity = true;

        if (lockZ)
            lockedZ = transform.position.z;

        RecalculateRuntimeSettings();
        ResetJumpCounter();
        ClearDoubleJumpRuntimeState();
        ApplyManagedConstraints(false);
    }

    private void RecalculateRuntimeSettings()
    {
        if (autoTuneJump)
            RecalculateJumpAndGravity();
        else
            runtimeGravityScale = Mathf.Max(0f, manualGravityScale);
    }

    private void ClampSettings()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        airControl = Mathf.Clamp01(airControl);
        airDeceleration = Mathf.Max(0f, airDeceleration);

        doubleJumpMoveSpeedBonus = Mathf.Max(0f, doubleJumpMoveSpeedBonus);
        jumpCooldownAfterDoubleJumpLanding = Mathf.Max(0f, jumpCooldownAfterDoubleJumpLanding);

        inputAcceleration = Mathf.Max(0f, inputAcceleration);
        inputDeceleration = Mathf.Max(0f, inputDeceleration);

        slopeMinAngle = Mathf.Clamp(slopeMinAngle, 0f, 89f);
        downhillSlideDeceleration = Mathf.Max(0f, downhillSlideDeceleration);
        downhillSlideMinSpeed = Mathf.Max(0f, downhillSlideMinSpeed);
        slopeLandingStickSpeed = Mathf.Max(0f, slopeLandingStickSpeed);
        slopeIdleLockSpeed = Mathf.Max(0f, slopeIdleLockSpeed);
        slopeLandingLockTime = Mathf.Max(0f, slopeLandingLockTime);

        debugLogInterval = Mathf.Max(0.01f, debugLogInterval);

        jumpImpulse = Mathf.Max(0f, jumpImpulse);
        doubleJumpImpulse = Mathf.Max(0f, doubleJumpImpulse);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        jumpBuffer = Mathf.Max(0f, jumpBuffer);

        wallCheckDistance = Mathf.Max(0f, wallCheckDistance);
        wallCheckRadius = Mathf.Max(0.001f, wallCheckRadius);

        wallDetachHoldTime = Mathf.Max(0f, wallDetachHoldTime);
        wallSlideSpeed = Mathf.Max(0f, wallSlideSpeed);
        wallUpwardDeceleration = Mathf.Max(0f, wallUpwardDeceleration);
        wallJumpHorizontalSpeed = Mathf.Max(0f, wallJumpHorizontalSpeed);
        wallJumpVerticalSpeed = Mathf.Max(0f, wallJumpVerticalSpeed);
        wallReattachCooldown = Mathf.Max(0f, wallReattachCooldown);
        wallMinNormalX = Mathf.Clamp01(wallMinNormalX);
        wallMaxNormalY = Mathf.Clamp01(wallMaxNormalY);

        manualGravityScale = Mathf.Max(0f, manualGravityScale);

        groundProbeDistance = Mathf.Max(0.001f, groundProbeDistance);
        groundProbeStartOffset = Mathf.Max(0f, groundProbeStartOffset);
        groundProbeInset = Mathf.Max(0f, groundProbeInset);

        pixelsPerUnit = Mathf.Max(0.0001f, pixelsPerUnit);
        jumpHeightPixels = Mathf.Max(0f, jumpHeightPixels);
        timeToApex = Mathf.Max(0.0001f, timeToApex);
        doubleJumpHeightPixels = Mathf.Max(0f, doubleJumpHeightPixels);
    }

    private void ReadInput()
    {
        inputX = Input.GetAxisRaw("Horizontal");

        if (Mathf.Abs(inputX) > InputEpsilon)
            lastNonZeroInputX = Mathf.Sign(inputX);

        if (IsVaultingNow)
            return;

        if (Input.GetButtonDown("Jump"))
        {
            if (!IsJumpTemporarilyBlocked())
                lastJumpPressedTime = Time.time;
        }
    }

    private void HandleJumpReleaseCut()
    {
        if (IsVaultingNow)
            return;

        if (!cutJumpOnRelease || !Input.GetButtonUp("Jump"))
            return;

        Vector3 velocity = rb.linearVelocity;
        if (velocity.y <= 0f)
            return;

        rb.linearVelocity = new Vector3(
            velocity.x,
            velocity.y * 0.5f,
            lockZ ? 0f : velocity.z
        );
    }

    private void UpdateGroundedState(GroundInfo ground, out bool justLandedThisFixed, out bool leftGroundThisFixed)
    {
        isGrounded = ground.grounded;

        justLandedThisFixed = !wasGroundedLastFixed && isGrounded;
        leftGroundThisFixed = wasGroundedLastFixed && !isGrounded;

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
            ClearWallSlideState();

            if (justLandedThisFixed)
            {
                HandleLandingAfterAirborneState();
                ResetJumpCounter();
                ClampLandingVerticalVelocity();
            }
        }
        else if (leftGroundThisFixed)
        {
            Vector3 velocity = rb.linearVelocity;
            bool recentlyJumped = (Time.time - lastJumpExecutedTime) <= (Time.fixedDeltaTime * 1.5f);

            if (!recentlyJumped && velocity.y <= 0.01f)
                jumpsRemaining = enableDoubleJump ? Mathf.Min(jumpsRemaining, 1) : 0;
        }
    }

    private void HandleLandingAfterAirborneState()
    {
        ApplyJumpCooldownAfterDoubleJumpLandingIfNeeded();
        isDoubleJumpSpeedBoostActive = false;
        usedDoubleJumpSinceLastGrounded = false;

        if (IsJumpTemporarilyBlocked())
            lastJumpPressedTime = InvalidPastTime;
    }

    private void ApplyJumpCooldownAfterDoubleJumpLandingIfNeeded()
    {
        if (enableJumpCooldownAfterDoubleJumpLanding &&
            usedDoubleJumpSinceLastGrounded &&
            jumpCooldownAfterDoubleJumpLanding > 0f)
        {
            jumpBlockedUntilTime = Time.time + jumpCooldownAfterDoubleJumpLanding;
        }
        else
        {
            jumpBlockedUntilTime = InvalidPastTime;
        }
    }

    private bool IsJumpTemporarilyBlocked()
    {
        return Time.time < jumpBlockedUntilTime;
    }

    private void ResetJumpCounter()
    {
        jumpsRemaining = enableDoubleJump ? 2 : 1;
    }

    private void ClampLandingVerticalVelocity()
    {
        Vector3 velocity = rb.linearVelocity;

        if (velocity.y < landingClampMinY)
        {
            rb.linearVelocity = new Vector3(
                velocity.x,
                0f,
                lockZ ? 0f : velocity.z
            );
        }
    }

    private void UpdateSmoothedInput()
    {
        float desiredInput = inputX;
        float rampSpeed = Mathf.Abs(desiredInput) > Mathf.Abs(smoothedInputX)
            ? inputAcceleration
            : inputDeceleration;

        bool directionChanged =
            Mathf.Abs(desiredInput) > InputEpsilon &&
            Mathf.Abs(smoothedInputX) > InputEpsilon &&
            Mathf.Sign(desiredInput) != Mathf.Sign(smoothedInputX);

        if (directionChanged)
            rampSpeed = inputDeceleration;

        smoothedInputX = Mathf.MoveTowards(
            smoothedInputX,
            desiredInput,
            rampSpeed * Time.fixedDeltaTime
        );
    }

    private float GetTargetPlanarSpeed(bool blockedLeft, bool blockedRight)
    {
        if (isWallSliding)
            return 0f;

        float targetPlanarSpeed = smoothedInputX * GetEffectiveMoveSpeed();

        if (targetPlanarSpeed > 0f && blockedRight)
            targetPlanarSpeed = 0f;

        if (targetPlanarSpeed < 0f && blockedLeft)
            targetPlanarSpeed = 0f;

        return targetPlanarSpeed;
    }

    private float ResolvePlanarSpeed(
        float targetPlanarSpeed,
        float currentPlanarSpeed,
        float downhillSignedSpeed,
        bool noRawInput,
        bool blockedLeft,
        bool blockedRight,
        bool onSlope)
    {
        if (isWallSliding)
            return 0f;

        float resolvedPlanarSpeed;

        if (isGrounded && onSlope && noRawInput)
        {
            if (IsSlopeLandingLockActive())
            {
                resolvedPlanarSpeed = 0f;
                smoothedInputX = 0f;
            }
            else if (downhillSignedSpeed > downhillSlideMinSpeed)
            {
                resolvedPlanarSpeed = Mathf.MoveTowards(
                    currentPlanarSpeed,
                    0f,
                    downhillSlideDeceleration * Time.fixedDeltaTime
                );
            }
            else
            {
                resolvedPlanarSpeed = 0f;
                smoothedInputX = 0f;
            }
        }
        else
        {
            float moveRate;

            if (isGrounded)
            {
                moveRate = Mathf.Abs(targetPlanarSpeed) > InputEpsilon
                    ? acceleration
                    : deceleration;
            }
            else
            {
                moveRate = noRawInput
                    ? airDeceleration
                    : acceleration * airControl;
            }

            resolvedPlanarSpeed = Mathf.MoveTowards(
                currentPlanarSpeed,
                targetPlanarSpeed,
                moveRate * Time.fixedDeltaTime
            );
        }

        if (resolvedPlanarSpeed > 0f && blockedRight)
            resolvedPlanarSpeed = 0f;

        if (resolvedPlanarSpeed < 0f && blockedLeft)
            resolvedPlanarSpeed = 0f;

        return resolvedPlanarSpeed;
    }

    private void ApplyFinalVelocity(
        GroundInfo ground,
        Vector3 currentVelocity,
        float resolvedPlanarSpeed,
        bool shouldLockSlopeX,
        bool noRawInput)
    {
        if (isGrounded && ground.onSlope)
        {
            Vector3 finalVelocity;

            if (shouldLockSlopeX || (noRawInput && Mathf.Abs(resolvedPlanarSpeed) <= SpeedEpsilon))
            {
                finalVelocity = Vector3.zero;
            }
            else
            {
                finalVelocity = ground.slopeTangent * resolvedPlanarSpeed;
            }

            if (lockZ)
                finalVelocity.z = 0f;

            rb.linearVelocity = finalVelocity;
            return;
        }

        rb.linearVelocity = new Vector3(
            resolvedPlanarSpeed,
            currentVelocity.y,
            lockZ ? 0f : currentVelocity.z
        );
    }

    private void ApplyWallSlideVelocity()
    {
        if (!isWallSliding)
            return;

        Vector3 velocity = rb.linearVelocity;
        float newY = velocity.y;

        if (newY > 0f)
        {
            if (allowWallSlideUpwardMomentum)
                newY = Mathf.MoveTowards(newY, 0f, wallUpwardDeceleration * Time.fixedDeltaTime);
            else
                newY = 0f;
        }

        if (newY < -wallSlideSpeed)
            newY = -wallSlideSpeed;

        rb.linearVelocity = new Vector3(
            0f,
            newY,
            lockZ ? 0f : velocity.z
        );
    }

    private bool TryConsumeWallJump()
    {
        if (!isWallSliding)
            return false;

        bool buffered = (Time.time - lastJumpPressedTime) <= jumpBuffer;
        if (!buffered)
            return false;

        int jumpedFromWallSide = wallSlideSide;
        float jumpDirectionX = jumpedFromWallSide == WallSideLeft ? 1f : -1f;

        pendingSlopeStickAfterJump = false;
        slopeLockUntilTime = InvalidPastTime;
        UpdateSlopeXConstraint(false);
        jumpBlockedUntilTime = InvalidPastTime;

        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(
            jumpDirectionX * wallJumpHorizontalSpeed,
            wallJumpVerticalSpeed,
            lockZ ? 0f : velocity.z
        );

        if (enableDoubleJump)
            jumpsRemaining = allowDoubleJumpAfterWallJump ? 1 : 0;
        else
            jumpsRemaining = 0;

        if (enableWallReattachCooldown)
        {
            wallReattachLockedSide = jumpedFromWallSide;
            wallReattachLockUntilTime = Time.time + wallReattachCooldown;
        }
        else
        {
            wallReattachLockedSide = WallSideNone;
            wallReattachLockUntilTime = InvalidPastTime;
        }

        ClearWallSlideState();

        lastJumpPressedTime = InvalidPastTime;
        lastJumpExecutedTime = Time.time;

        return true;
    }

    private void TryConsumeBufferedJump(GroundInfo ground, float currentPlanarSpeed)
    {
        bool buffered = (Time.time - lastJumpPressedTime) <= jumpBuffer;
        if (!buffered)
            return;

        if (IsJumpTemporarilyBlocked())
            return;

        bool canCoyote = (Time.time - lastGroundedTime) <= coyoteTime;

        bool groundJumpAllowed = (isGrounded || canCoyote) && jumpsRemaining > 0;
        bool airJumpAllowed = enableDoubleJump && jumpsRemaining > 0 && (!isGrounded && !canCoyote);

        if (!doubleJumpOnlyInAir)
            airJumpAllowed = enableDoubleJump && jumpsRemaining > 0 && !isGrounded;

        if (!groundJumpAllowed && !airJumpAllowed)
            return;

        bool isFirstJump = !enableDoubleJump || jumpsRemaining >= 2;
        bool isDoubleJump = !isFirstJump;

        float impulse = isFirstJump ? jumpImpulse : doubleJumpImpulse;

        bool jumpedFromStandstillOnSlope =
            stickToSlopeAfterVerticalJump &&
            isGrounded &&
            ground.onSlope &&
            Mathf.Abs(inputX) <= InputEpsilon &&
            Mathf.Abs(currentPlanarSpeed) <= slopeLandingStickSpeed;

        pendingSlopeStickAfterJump = jumpedFromStandstillOnSlope;

        slopeLockUntilTime = InvalidPastTime;
        UpdateSlopeXConstraint(false);
        jumpBlockedUntilTime = InvalidPastTime;

        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(
            velocity.x,
            0f,
            lockZ ? 0f : velocity.z
        );

        rb.AddForce(Vector3.up * impulse, ForceMode.Impulse);

        if (isDoubleJump)
        {
            usedDoubleJumpSinceLastGrounded = true;
            ActivateDoubleJumpSpeedBoostIfNeeded();
        }

        jumpsRemaining = Mathf.Max(0, jumpsRemaining - 1);
        lastJumpPressedTime = InvalidPastTime;
        lastJumpExecutedTime = Time.time;
    }

    private float GetCurrentPlanarSpeed(Vector3 velocity, GroundInfo ground, bool justLanded)
    {
        if (!ground.onSlope)
            return velocity.x;

        if (justLanded)
            return ConvertWorldXToSlopeSpeed(velocity.x, ground.slopeTangent);

        return Vector3.Dot(velocity, ground.slopeTangent);
    }

    private float ConvertWorldXToSlopeSpeed(float worldX, Vector3 slopeTangent)
    {
        if (Mathf.Abs(slopeTangent.x) <= DotEpsilon)
            return 0f;

        return worldX / slopeTangent.x;
    }

    private float GetEffectiveMoveSpeed()
    {
        if (!enableDoubleJumpSpeedBoost || !isDoubleJumpSpeedBoostActive)
            return moveSpeed;

        return moveSpeed + doubleJumpMoveSpeedBonus;
    }

    private void ActivateDoubleJumpSpeedBoostIfNeeded()
    {
        if (!enableDoubleJumpSpeedBoost)
            return;

        isDoubleJumpSpeedBoostActive = true;
    }

    private void ClearDoubleJumpRuntimeState()
    {
        usedDoubleJumpSinceLastGrounded = false;
        isDoubleJumpSpeedBoostActive = false;
        jumpBlockedUntilTime = InvalidPastTime;
    }

    private void ApplyManagedConstraints(bool freezeX)
    {
        if (rb == null)
            return;

        RigidbodyConstraints preserved =
            rb.constraints &
            ~RigidbodyConstraints.FreezeRotation &
            ~RigidbodyConstraints.FreezePositionZ &
            ~RigidbodyConstraints.FreezePositionX;

        RigidbodyConstraints managed = RigidbodyConstraints.FreezeRotation;

        if (lockZ)
            managed |= RigidbodyConstraints.FreezePositionZ;

        if (freezeX && lockZ && freezeXWhenIdleOnSlope)
            managed |= RigidbodyConstraints.FreezePositionX;

        rb.constraints = preserved | managed;
    }

    private void UpdateSlopeXConstraint(bool shouldLock)
    {
        if (!freezeXWhenIdleOnSlope || !lockZ)
            shouldLock = false;

        if (slopeXLocked == shouldLock)
            return;

        slopeXLocked = shouldLock;
        ApplyManagedConstraints(slopeXLocked);
    }

    private void TryStartSlopeLandingLock(GroundInfo ground, float landingPlanarSpeed)
    {
        bool requestedByVerticalJumpStick = pendingSlopeStickAfterJump;
        pendingSlopeStickAfterJump = false;

        if (!stickToSlopeAfterVerticalJump)
            return;

        if (!ground.onSlope)
            return;

        if (Mathf.Abs(inputX) > InputEpsilon)
            return;

        if (requestedByVerticalJumpStick || Mathf.Abs(landingPlanarSpeed) <= slopeLandingStickSpeed)
        {
            slopeLockUntilTime = Time.time + slopeLandingLockTime;
            smoothedInputX = 0f;
            currentHorizontalSpeedAbs = 0f;
        }
    }

    private bool IsSlopeLandingLockActive()
    {
        return Time.time < slopeLockUntilTime;
    }

    private bool ShouldLockSlopeX(
        GroundInfo ground,
        bool noRawInput,
        float currentPlanarSpeed,
        float downhillSignedSpeed)
    {
        bool canHardLockBySpeed =
            IsSlopeLandingLockActive() ||
            Mathf.Abs(currentPlanarSpeed) <= slopeIdleLockSpeed ||
            (downhillSignedSpeed >= 0f && downhillSignedSpeed <= downhillSlideMinSpeed);

        return
            freezeXWhenIdleOnSlope &&
            lockZ &&
            isGrounded &&
            ground.onSlope &&
            noRawInput &&
            canHardLockBySpeed;
    }

    private void RecalculateJumpAndGravity()
    {
        float globalGravity = Mathf.Abs(Physics.gravity.y);
        if (globalGravity < DotEpsilon)
            globalGravity = 9.81f;

        float firstJumpHeightUnits = jumpHeightPixels / Mathf.Max(DotEpsilon, pixelsPerUnit);
        float apexTime = Mathf.Max(DotEpsilon, timeToApex);

        float requiredGravity = (2f * firstJumpHeightUnits) / (apexTime * apexTime);
        float firstJumpVelocity = requiredGravity * apexTime;

        runtimeGravityScale = requiredGravity / globalGravity;
        jumpImpulse = rb.mass * firstJumpVelocity;

        float secondJumpHeightUnits = doubleJumpHeightPixels / Mathf.Max(DotEpsilon, pixelsPerUnit);
        float secondJumpVelocity = Mathf.Sqrt(Mathf.Max(0f, 2f * requiredGravity * secondJumpHeightUnits));
        doubleJumpImpulse = rb.mass * secondJumpVelocity;
    }

    private float GetEffectiveGravityScale()
    {
        return autoTuneJump
            ? runtimeGravityScale
            : Mathf.Max(0f, manualGravityScale);
    }

    private void ApplyExtraGravity()
    {
        float gravityScale = GetEffectiveGravityScale();
        float extraScale = gravityScale - 1f;

        if (Mathf.Abs(extraScale) < 1e-4f)
            return;

        rb.AddForce(Physics.gravity * extraScale, ForceMode.Acceleration);
    }

    private void ApplySlopeAntiSlide(GroundInfo ground)
    {
        if (!enableSlopeHandling)
            return;

        if (!isGrounded || !ground.onSlope || ground.hit.collider == null)
            return;

        Vector3 effectiveGravity = Physics.gravity * GetEffectiveGravityScale();
        Vector3 gravityAlongSlope = Vector3.ProjectOnPlane(effectiveGravity, ground.hit.normal);

        rb.AddForce(-gravityAlongSlope, ForceMode.Acceleration);
    }

    private GroundInfo ProbeGround()
    {
        GroundInfo ground = default;
        ground.slopeTangent = Vector3.right;
        ground.downhillSign = 1f;

        if (!TryGetGroundHit(out RaycastHit hit))
            return ground;

        ground.grounded = true;
        ground.hit = hit;

        FillSlopeData(ref ground);
        return ground;
    }

    private void FillSlopeData(ref GroundInfo ground)
    {
        if (!enableSlopeHandling || !ground.grounded || ground.hit.collider == null)
            return;

        ground.slopeAngle = Vector3.Angle(ground.hit.normal, Vector3.up);
        if (ground.slopeAngle < slopeMinAngle || ground.slopeAngle >= 89f)
            return;

        Vector3 tangent;

        if (lockZ)
        {
            tangent = Vector3.Cross(Vector3.forward, ground.hit.normal);
            tangent.z = 0f;
        }
        else
        {
            tangent = Vector3.ProjectOnPlane(Vector3.right, ground.hit.normal);
        }

        float magnitude = tangent.magnitude;
        if (magnitude < DotEpsilon)
            return;

        tangent /= magnitude;

        if (Vector3.Dot(tangent, Vector3.right) < 0f)
            tangent = -tangent;

        Vector3 downhill = Vector3.ProjectOnPlane(Physics.gravity, ground.hit.normal);

        ground.onSlope = true;
        ground.slopeTangent = tangent;
        ground.downhillSign = Vector3.Dot(tangent, downhill) >= 0f ? 1f : -1f;
    }

    private bool TryGetGroundHit(out RaycastHit bestHit)
    {
        bestHit = default;

        Bounds bounds = col.bounds;
        float baseRadius = Mathf.Min(bounds.extents.x, bounds.extents.z);
        float probeRadius = Mathf.Max(0.02f, baseRadius - groundProbeInset);

        float z = lockZ ? lockedZ : bounds.center.z;

        Vector3 origin = new Vector3(
            bounds.center.x,
            bounds.min.y + probeRadius + groundProbeStartOffset,
            z
        );

        float castDistance = groundProbeDistance + groundProbeStartOffset;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            probeRadius,
            Vector3.down,
            castHits,
            castDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        bool found = false;
        float bestDistance = float.MaxValue;
        float bestNormalY = -1f;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = castHits[i];

            if (hit.collider == null) continue;
            if (hit.collider.attachedRigidbody == rb) continue;
            if (hit.normal.y <= 0.05f) continue;

            bool betterHit =
                !found ||
                hit.distance < bestDistance - HitDistanceTieEpsilon ||
                (
                    Mathf.Abs(hit.distance - bestDistance) <= HitDistanceTieEpsilon &&
                    hit.normal.y > bestNormalY
                );

            if (!betterHit)
                continue;

            found = true;
            bestDistance = hit.distance;
            bestNormalY = hit.normal.y;
            bestHit = hit;
        }

        return found;
    }

    private WallInfo CheckWalls()
    {
        WallInfo wall = default;

        Bounds bounds = col.bounds;
        float z = lockZ ? lockedZ : bounds.center.z;

        Vector3 origin = new Vector3(
            bounds.center.x,
            bounds.center.y + wallCheckHeightOffset,
            z
        );

        float radius = Mathf.Max(0.001f, wallCheckRadius);

        wall.blockedRight = TryGetWallHit(origin, radius, Vector3.right, wallCheckDistance, out wall.rightHit);
        wall.blockedLeft = TryGetWallHit(origin, radius, Vector3.left, wallCheckDistance, out wall.leftHit);

        return wall;
    }

    private bool TryGetWallHit(
        Vector3 origin,
        float radius,
        Vector3 direction,
        float distance,
        out RaycastHit bestHit)
    {
        bestHit = default;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            radius,
            direction,
            castHits,
            distance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        bool found = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = castHits[i];

            if (hit.collider == null) continue;
            if (hit.collider.attachedRigidbody == rb) continue;

            if (Vector3.Dot(hit.normal, direction) >= -0.1f)
                continue;

            if (!IsWallNormal(hit.normal))
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

    private bool IsWallNormal(Vector3 normal)
    {
        return Mathf.Abs(normal.x) >= wallMinNormalX && Mathf.Abs(normal.y) <= wallMaxNormalY;
    }

    private void UpdateWallSlideState(WallInfo wall)
    {
        if (!enableWallSlide)
        {
            ClearWallSlideState();
            return;
        }

        if (isGrounded)
        {
            ClearWallSlideState();
            return;
        }

        if (isWallSliding)
        {
            bool stillTouchingCurrentWall = IsTouchingWallSide(wall, wallSlideSide);
            if (!stillTouchingCurrentWall)
            {
                ClearWallSlideState();
                TryStartWallLatch(wall);
                return;
            }

            if (ShouldDetachFromCurrentWallByHold())
            {
                ClearWallSlideState();
                return;
            }

            return;
        }

        TryStartWallLatch(wall);
    }

    private void TryStartWallLatch(WallInfo wall)
    {
        bool canLatchLeft =
            wall.blockedLeft &&
            CanLatchToWallSide(WallSideLeft);

        bool canLatchRight =
            wall.blockedRight &&
            CanLatchToWallSide(WallSideRight);

        if (!canLatchLeft && !canLatchRight)
            return;

        if (canLatchLeft && !canLatchRight)
        {
            SetWallSlideState(WallSideLeft);
            return;
        }

        if (canLatchRight && !canLatchLeft)
        {
            SetWallSlideState(WallSideRight);
            return;
        }

        if (inputX < -InputEpsilon)
        {
            SetWallSlideState(WallSideLeft);
            return;
        }

        if (inputX > InputEpsilon)
        {
            SetWallSlideState(WallSideRight);
            return;
        }

        if (rb.linearVelocity.x < 0f)
            SetWallSlideState(WallSideLeft);
        else
            SetWallSlideState(WallSideRight);
    }

    private bool CanLatchToWallSide(int side)
    {
        if (IsWallReattachLockedForSide(side))
            return false;

        if (!requireInputToLatchWall)
            return true;

        return WantsToLatchSide(side);
    }

    private bool WantsToLatchSide(int side)
    {
        if (side == WallSideLeft)
            return inputX < -InputEpsilon;

        if (side == WallSideRight)
            return inputX > InputEpsilon;

        return false;
    }

    private bool WantsToDetachFromCurrentWall()
    {
        if (wallSlideSide == WallSideLeft)
            return inputX > InputEpsilon;

        if (wallSlideSide == WallSideRight)
            return inputX < -InputEpsilon;

        return false;
    }

    private bool ShouldDetachFromCurrentWallByHold()
    {
        if (!detachFromWallOnOppositeInput)
        {
            wallDetachHoldTimer = 0f;
            return false;
        }

        if (!WantsToDetachFromCurrentWall())
        {
            wallDetachHoldTimer = 0f;
            return false;
        }

        wallDetachHoldTimer += Time.fixedDeltaTime;
        return wallDetachHoldTimer >= wallDetachHoldTime;
    }

    private bool IsTouchingWallSide(WallInfo wall, int side)
    {
        if (side == WallSideLeft)
            return wall.blockedLeft;

        if (side == WallSideRight)
            return wall.blockedRight;

        return false;
    }

    private void SetWallSlideState(int side)
    {
        isWallSliding = true;
        wallSlideSide = side;
        wallDetachHoldTimer = 0f;
    }

    private void ClearWallSlideState()
    {
        isWallSliding = false;
        wallSlideSide = WallSideNone;
        wallDetachHoldTimer = 0f;
    }

    private bool IsWallReattachLockedForSide(int side)
    {
        if (!enableWallReattachCooldown)
            return false;

        if (wallReattachLockedSide != side)
            return false;

        return Time.time < wallReattachLockUntilTime;
    }

    public void NotifyVaultStarted()
    {
        pendingSlopeStickAfterJump = false;
        slopeLockUntilTime = InvalidPastTime;
        jumpBlockedUntilTime = InvalidPastTime;
        lastJumpPressedTime = InvalidPastTime;
        smoothedInputX = 0f;
        currentHorizontalSpeedAbs = 0f;

        ClearWallSlideState();
        UpdateSlopeXConstraint(false);

        if (rb != null)
        {
            Vector3 velocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(
                0f,
                0f,
                lockZ ? 0f : velocity.z
            );
        }
    }

    public void NotifyVaultFinished()
    {
        pendingSlopeStickAfterJump = false;
        slopeLockUntilTime = InvalidPastTime;
        jumpBlockedUntilTime = InvalidPastTime;
        currentHorizontalSpeedAbs = 0f;
        lastGroundedTime = Time.time;

        ResetJumpCounter();
        ClearDoubleJumpRuntimeState();
        ClearWallSlideState();
        UpdateSlopeXConstraint(false);
    }

    private void LogAccelerationDebug(
        float currentPlanarSpeed,
        float targetPlanarSpeed,
        float resolvedPlanarSpeed,
        bool blockedLeft,
        bool blockedRight,
        GroundInfo ground,
        float downhillSignedSpeed)
    {
        if (!debugAcceleration)
            return;

        if (Time.time - lastDebugLogTime < debugLogInterval)
            return;

        lastDebugLogTime = Time.time;

        Debug.Log(
            $"[RBCharacter25D] " +
            $"rawInput={inputX:0.00} " +
            $"smoothInput={smoothedInputX:0.00} " +
            $"planarBefore={currentPlanarSpeed:0.00} " +
            $"planarTarget={targetPlanarSpeed:0.00} " +
            $"planarAfter={resolvedPlanarSpeed:0.00} " +
            $"speedAbs={currentHorizontalSpeedAbs:0.00} " +
            $"speedNorm={SpeedNormalized:0.00} " +
            $"effectiveMoveSpeed={EffectiveMoveSpeed:0.00} " +
            $"boostActive={isDoubleJumpSpeedBoostActive} " +
            $"doubleJumpUsed={usedDoubleJumpSinceLastGrounded} " +
            $"jumpBlocked={IsJumpTemporarilyBlocked()} " +
            $"grounded={isGrounded} " +
            $"wallSliding={isWallSliding} " +
            $"wallSide={wallSlideSide} " +
            $"velY={rb.linearVelocity.y:0.00} " +
            $"onSlope={ground.onSlope} " +
            $"slopeAngle={ground.slopeAngle:0.0} " +
            $"downhillSigned={downhillSignedSpeed:0.00} " +
            $"xLocked={slopeXLocked} " +
            $"blockedL={blockedLeft} " +
            $"blockedR={blockedRight}",
            this
        );
    }

    private void OnDrawGizmosSelected()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null)
            return;

        Bounds bounds = capsule.bounds;
        float baseRadius = Mathf.Min(bounds.extents.x, bounds.extents.z);
        float probeRadius = Mathf.Max(0.02f, baseRadius - groundProbeInset);

        float z = lockZ ? (Application.isPlaying ? lockedZ : transform.position.z) : bounds.center.z;

        Vector3 probeOrigin = new Vector3(
            bounds.center.x,
            bounds.min.y + probeRadius + groundProbeStartOffset,
            z
        );

        float probeDepth = groundProbeDistance + groundProbeStartOffset;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(probeOrigin, probeRadius);
        Gizmos.DrawLine(probeOrigin, probeOrigin + Vector3.down * probeDepth);
        Gizmos.DrawWireSphere(probeOrigin + Vector3.down * probeDepth, probeRadius);

        Gizmos.color = Color.cyan;
        Vector3 wallOrigin = new Vector3(
            bounds.center.x,
            bounds.center.y + wallCheckHeightOffset,
            z
        );

        Gizmos.DrawLine(wallOrigin, wallOrigin + Vector3.right * wallCheckDistance);
        Gizmos.DrawLine(wallOrigin, wallOrigin + Vector3.left * wallCheckDistance);
        Gizmos.DrawWireSphere(wallOrigin + Vector3.right * wallCheckDistance, wallCheckRadius);
        Gizmos.DrawWireSphere(wallOrigin + Vector3.left * wallCheckDistance, wallCheckRadius);
    }
}