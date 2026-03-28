using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class RBCharacter25D : MonoBehaviour
{
    private const float InputEpsilon = 0.01f;
    private const float SpeedEpsilon = 0.0001f;
    private const float DotEpsilon = 1e-5f;
    private const float InvalidPastTime = -999f;

    private const int WallSideNone = 0;
    private const int WallSideLeft = -1;
    private const int WallSideRight = +1;

    public enum SelfJumpKind
    {
        None = 0,
        SingleJump = 1,
        DoubleJump = 2,
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
    [SerializeField] private float airDeceleration = 8f;

    [Header("Double Jump Speed Boost")]
    [Tooltip("Включить временный бонус к moveSpeed после double jump.")]
    [SerializeField] private bool enableDoubleJumpSpeedBoost = true;

    [Tooltip("На сколько увеличить moveSpeed после double jump.")]
    [SerializeField] private float doubleJumpMoveSpeedBonus = 3f;

    [Header("Jump Cooldown After Double Jump Landing")]
    [Tooltip("После приземления из double jump включать очень короткий кулдаун на новый прыжок.")]
    [SerializeField] private bool enableJumpCooldownAfterDoubleJumpLanding = true;

    [Tooltip("Длительность маленького кулдауна на прыжок после приземления из double jump.")]
    [SerializeField] private float jumpCooldownAfterDoubleJumpLanding = 0.01f;

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
    [SerializeField] private float downhillSlideDeceleration = 30f;

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
    [SerializeField] private float jumpImpulse = 18.75733f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBuffer = 0.1f;
    [SerializeField] private bool cutJumpOnRelease = false;

    [Header("Double Jump")]
    [SerializeField] private bool enableDoubleJump = true;
    [SerializeField] private bool doubleJumpOnlyInAir = true;

    [Tooltip("Импульс второго прыжка (будет перезаписан авто-настройкой, если autoTuneJump=true).")]
    [SerializeField] private float doubleJumpImpulse = 18.75733f;

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
    [SerializeField] private bool requireInputToLatchWall = false;

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
    [SerializeField] private float wallJumpHorizontalSpeed = 12f;

    [Tooltip("Вертикальная скорость прыжка от стены.")]
    [SerializeField] private float wallJumpVerticalSpeed = 15f;

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
    private RBCharacter25DSurfaceSensor surfaceSensor;

    private FrameInput25D currentInput;
    private SurfaceContacts25D lastContacts;
    private LocomotionState25D state;

    private float inputX;
    private float externalMoveX;
    private bool externalJumpHeld;
    private bool externalJumpPressedQueued;
    private bool externalJumpReleasedQueued;
    private float smoothedInputX;
    private float currentHorizontalSpeedAbs;
    private float lastNonZeroInputX = 1f;
    private float lastDebugLogTime = InvalidPastTime;
    private float runtimeGravityScale = 1f;
    private bool slopeXLocked;

    public bool IsGroundedNow => state.IsGrounded;
    public bool IsWallSliding => state.IsWallSliding;
    public bool IsVaultingNow => vaulting != null && vaulting.IsVaulting;
    public float LastVaultFinishedTime { get; private set; } = InvalidPastTime;
    public float LastWallSlideFinishedTime { get; private set; } = InvalidPastTime;
    public int WallSlideSide => state.WallSlideSide;
    public float LastJumpTime => state.LastJumpExecutedTime;
    public SelfJumpKind LastSelfJumpType => state.LastSelfJumpKind;
    public int LastSelfJumpStateVersion => state.LastSelfJumpStateVersion;
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
        EnsureSurfaceSensor();
        InitializeRuntimeState();
    }

    private void Reset()
    {
        lockZ = true;
        lockedZ = 0f;

        moveSpeed = 8f;
        acceleration = 40f;
        deceleration = 60f;
        airControl = 0.8f;
        airDeceleration = 8f;

        enableDoubleJumpSpeedBoost = true;
        doubleJumpMoveSpeedBonus = 3f;
        enableJumpCooldownAfterDoubleJumpLanding = true;
        jumpCooldownAfterDoubleJumpLanding = 0.01f;

        inputAcceleration = 8f;
        inputDeceleration = 12f;

        enableSlopeHandling = true;
        slopeMinAngle = 1f;
        downhillSlideDeceleration = 30f;
        downhillSlideMinSpeed = 1f;
        stickToSlopeAfterVerticalJump = true;
        slopeLandingStickSpeed = 1.25f;
        freezeXWhenIdleOnSlope = true;
        slopeIdleLockSpeed = 0.05f;
        slopeLandingLockTime = 0.12f;

        debugAcceleration = false;
        debugLogInterval = 0.1f;

        jumpImpulse = 18.75733f;
        coyoteTime = 0.1f;
        jumpBuffer = 0.1f;
        cutJumpOnRelease = false;

        enableDoubleJump = true;
        doubleJumpOnlyInAir = true;
        doubleJumpImpulse = 18.75733f;

        wallCheckDistance = 0.5f;
        wallCheckHeightOffset = 0f;
        wallCheckRadius = 0.08f;

        enableWallSlide = true;
        requireInputToLatchWall = false;
        detachFromWallOnOppositeInput = true;
        wallDetachHoldTime = 0.3f;
        wallSlideSpeed = 2.5f;
        allowWallSlideUpwardMomentum = true;
        wallUpwardDeceleration = 18f;
        wallJumpHorizontalSpeed = 12f;
        wallJumpVerticalSpeed = 15f;
        allowDoubleJumpAfterWallJump = true;
        enableWallReattachCooldown = true;
        wallReattachCooldown = 0.12f;
        wallMinNormalX = 0.7f;
        wallMaxNormalY = 0.2f;

        manualGravityScale = 1f;

        int terrainLayer = LayerMask.NameToLayer("Terrain");
        if (terrainLayer >= 0)
            groundMask = 1 << terrainLayer;

        groundProbeDistance = 0.08f;
        groundProbeStartOffset = 0.02f;
        groundProbeInset = 0.05f;
        landingClampMinY = 0f;

        autoTuneJump = true;
        pixelsPerUnit = 32f;
        jumpHeightPixels = 128f;
        timeToApex = 0.4265f;
        doubleJumpHeightPixels = 128f;

        ClampSettings();
    }

    private void OnValidate()
    {
        ClampSettings();

        if (!Application.isPlaying)
            return;

        CacheComponents();
        EnsureSurfaceSensor();
        RecalculateRuntimeSettings();
        SyncSurfaceSensor();
        ApplyManagedConstraints(slopeXLocked);
    }

    private void Update()
    {
        ReadInput();
        HandleJumpReleaseCut();
    }

    private void FixedUpdate()
    {
        if (StepVaultIfActive())
            return;

        ApplyExtraGravity();

        if (!enableDoubleJumpSpeedBoost)
            state.IsDoubleJumpSpeedBoostActive = false;

        currentInput.RawX = inputX;
        currentInput.JumpHeld = externalJumpHeld;
        currentInput.JumpPressed = false;
        currentInput.JumpReleased = false;
        currentInput.FacingSign = VaultFacingSignFromInput;

        lastContacts = GatherSurfaceContacts();
        UpdateGroundedState(lastContacts, out bool justLandedThisFixed, out bool leftGroundThisFixed);

        if (leftGroundThisFixed)
        {
            state.SlopeLockUntilTime = InvalidPastTime;
            UpdateSlopeXConstraint(false);
        }

        if (!state.IsGrounded && Mathf.Abs(inputX) > InputEpsilon)
            state.PendingSlopeStickAfterJump = false;

        if (vaulting != null && vaulting.TryStartVault())
        {
            currentHorizontalSpeedAbs = 0f;
            state.WasGroundedLastFixed = false;
            return;
        }

        UpdateSmoothedInput();
        currentInput.SmoothedX = smoothedInputX;

        UpdateWallSlideState(lastContacts);

        VelocityCommand25D command = BuildVelocityCommand(currentInput, lastContacts, justLandedThisFixed);
        ApplyVelocityCommand(command);

        UpdateCurrentHorizontalSpeed(lastContacts);
        state.WasGroundedLastFixed = state.IsGrounded;

        if (!state.IsGrounded)
            UpdateSlopeXConstraint(false);
    }

    private bool StepVaultIfActive()
    {
        if (!IsVaultingNow)
            return false;

        vaulting.StepActiveVault();
        state.WasGroundedLastFixed = false;
        return true;
    }

    private void CacheComponents()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (col == null) col = GetComponent<CapsuleCollider>();
        if (vaulting == null) vaulting = GetComponent<RBCharacter25DVaulting>();
    }

    private void EnsureSurfaceSensor()
    {
        if (surfaceSensor == null)
            surfaceSensor = new RBCharacter25DSurfaceSensor();

        surfaceSensor.Initialize(rb, col);
        SyncSurfaceSensor();
    }

    private void SyncSurfaceSensor()
    {
        if (surfaceSensor == null)
            return;

        surfaceSensor.SyncSettings(
            groundMask,
            groundProbeDistance,
            groundProbeStartOffset,
            groundProbeInset,
            wallCheckDistance,
            wallCheckHeightOffset,
            wallCheckRadius,
            wallMinNormalX,
            wallMaxNormalY,
            enableSlopeHandling,
            slopeMinAngle,
            lockZ,
            lockedZ);
    }

    private void InitializeRuntimeState()
    {
        if (rb != null)
            rb.useGravity = true;

        if (lockZ)
            lockedZ = transform.position.z;

        state.LastGroundedTime = InvalidPastTime;
        state.LastJumpPressedTime = InvalidPastTime;
        state.LastJumpExecutedTime = InvalidPastTime;
        state.WallReattachLockUntilTime = InvalidPastTime;
        state.JumpBlockedUntilTime = InvalidPastTime;
        state.SlopeLockUntilTime = InvalidPastTime;
        state.WallReattachLockedSide = WallSideNone;
        state.WallSlideSide = WallSideNone;
        state.IsGrounded = false;
        state.WasGroundedLastFixed = false;
        state.IsWallSliding = false;
        state.WallDetachHoldTimer = 0f;
        smoothedInputX = 0f;
        currentHorizontalSpeedAbs = 0f;
        externalMoveX = 0f;
        externalJumpHeld = false;
        externalJumpPressedQueued = false;
        externalJumpReleasedQueued = false;

        RecalculateRuntimeSettings();
        ResetJumpCounter();
        ClearDoubleJumpRuntimeState();
        ClearLastSelfJumpState();
        ApplyManagedConstraints(false);
        SyncSurfaceSensor();
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
        coyoteTime = Mathf.Max(0f, coyoteTime);
        jumpBuffer = Mathf.Max(0f, jumpBuffer);

        doubleJumpImpulse = Mathf.Max(0f, doubleJumpImpulse);

        wallCheckDistance = Mathf.Max(0.001f, wallCheckDistance);
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
        inputX = externalMoveX;

        if (Mathf.Abs(inputX) > InputEpsilon)
            lastNonZeroInputX = Mathf.Sign(inputX);

        if (IsVaultingNow)
        {
            externalJumpPressedQueued = false;
            return;
        }

        if (!externalJumpPressedQueued)
            return;

        currentInput.JumpPressed = true;
        state.LastJumpPressedTime = Time.time;
        externalJumpPressedQueued = false;
    }

    private void HandleJumpReleaseCut()
    {
        if (IsVaultingNow)
        {
            externalJumpReleasedQueued = false;
            return;
        }

        if (!cutJumpOnRelease || !externalJumpReleasedQueued)
            return;

        currentInput.JumpReleased = true;
        externalJumpReleasedQueued = false;

        Vector3 velocity = rb.linearVelocity;
        if (velocity.y <= 0f)
            return;

        rb.linearVelocity = new Vector3(
            velocity.x,
            velocity.y * 0.5f,
            lockZ ? 0f : velocity.z);
    }

    public void SetMoveInput(float x)
    {
        externalMoveX = Mathf.Clamp(x, -1f, 1f);
    }

    public void SetJumpHeld(bool held)
    {
        externalJumpHeld = held;
    }

    public void QueueJumpPressed()
    {
        externalJumpPressedQueued = true;
    }

    public void QueueJumpReleased()
    {
        externalJumpReleasedQueued = true;
    }

    public void ClearExternalInputState(bool clearMove = true)
    {
        if (clearMove)
            externalMoveX = 0f;

        externalJumpHeld = false;
        externalJumpPressedQueued = false;
        externalJumpReleasedQueued = false;
    }

    private SurfaceContacts25D GatherSurfaceContacts()
    {
        EnsureSurfaceSensor();
        SyncSurfaceSensor();
        return surfaceSensor != null ? surfaceSensor.ProbeContacts() : default;
    }

    private void UpdateGroundedState(SurfaceContacts25D contacts, out bool justLandedThisFixed, out bool leftGroundThisFixed)
    {
        state.IsGrounded = contacts.IsGrounded;
        justLandedThisFixed = !state.WasGroundedLastFixed && state.IsGrounded;
        leftGroundThisFixed = state.WasGroundedLastFixed && !state.IsGrounded;

        if (state.IsGrounded)
        {
            state.LastGroundedTime = Time.time;
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
            bool recentlyJumped = (Time.time - state.LastJumpExecutedTime) <= (Time.fixedDeltaTime * 1.5f);

            if (!recentlyJumped && velocity.y <= 0.01f)
                state.JumpsRemaining = enableDoubleJump ? Mathf.Min(state.JumpsRemaining, 1) : 0;
        }
    }

    private void HandleLandingAfterAirborneState()
    {
        ApplyJumpCooldownAfterDoubleJumpLandingIfNeeded();
        state.IsDoubleJumpSpeedBoostActive = false;
        state.UsedDoubleJumpSinceLastGrounded = false;
        ClearLastSelfJumpState();

    }

    private void ApplyJumpCooldownAfterDoubleJumpLandingIfNeeded()
    {
        if (enableJumpCooldownAfterDoubleJumpLanding &&
            state.UsedDoubleJumpSinceLastGrounded &&
            jumpCooldownAfterDoubleJumpLanding > 0f)
        {
            state.JumpBlockedUntilTime = Time.time + jumpCooldownAfterDoubleJumpLanding;
        }
        else
        {
            state.JumpBlockedUntilTime = InvalidPastTime;
        }
    }

    private bool IsJumpBuffered()
    {
        return (Time.time - state.LastJumpPressedTime) <= jumpBuffer;
    }

    private bool IsJumpTemporarilyBlocked()
    {
        return Time.time < state.JumpBlockedUntilTime;
    }

    private void ResetJumpCounter()
    {
        state.JumpsRemaining = enableDoubleJump ? 2 : 1;
    }

    private void ClampLandingVerticalVelocity()
    {
        Vector3 velocity = rb.linearVelocity;

        if (velocity.y < landingClampMinY)
        {
            rb.linearVelocity = new Vector3(
                velocity.x,
                0f,
                lockZ ? 0f : velocity.z);
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
            rampSpeed * Time.fixedDeltaTime);
    }

    private VelocityCommand25D BuildVelocityCommand(FrameInput25D input, SurfaceContacts25D contacts, bool justLandedThisFixed)
    {
        VelocityCommand25D command = default;
        command.TargetVelocity = rb.linearVelocity;

        float currentPlanarSpeed = GetCurrentPlanarSpeed(command.TargetVelocity, contacts, justLandedThisFixed);

        if (justLandedThisFixed)
        {
            TryStartSlopeLandingLock(contacts, currentPlanarSpeed);

            if (contacts.OnSlope && Mathf.Abs(input.RawX) <= InputEpsilon && IsSlopeLandingLockActive())
                currentPlanarSpeed = 0f;
        }

        bool noRawInput = Mathf.Abs(input.RawX) <= InputEpsilon;
        float targetPlanarSpeed = BuildTargetHorizontalSpeed(input, contacts);
        float downhillSignedSpeed = contacts.OnSlope ? currentPlanarSpeed * contacts.DownhillSign : 0f;

        bool shouldLockSlopeX = ShouldLockSlopeX(contacts, noRawInput, currentPlanarSpeed, downhillSignedSpeed) && !IsJumpBuffered();
        UpdateSlopeXConstraint(shouldLockSlopeX);

        float resolvedPlanarSpeed = ResolvePlanarSpeed(
            targetPlanarSpeed,
            currentPlanarSpeed,
            downhillSignedSpeed,
            noRawInput,
            contacts.BlockedLeft,
            contacts.BlockedRight,
            contacts.OnSlope);

        ApplyHorizontalIntent(ref command, contacts, resolvedPlanarSpeed, shouldLockSlopeX, noRawInput);

        bool consumedWallJump = TryBuildWallJump(ref command);
        if (!consumedWallJump)
            TryBuildGroundOrAirJump(ref command, contacts, currentPlanarSpeed, noRawInput);

        ApplyVerticalRules(ref command);

        LogAccelerationDebug(
            currentPlanarSpeed,
            targetPlanarSpeed,
            resolvedPlanarSpeed,
            contacts.BlockedLeft,
            contacts.BlockedRight,
            contacts,
            downhillSignedSpeed);

        return command;
    }

    private float BuildTargetHorizontalSpeed(FrameInput25D input, SurfaceContacts25D contacts)
    {
        if (state.IsWallSliding)
            return 0f;

        float targetPlanarSpeed = input.SmoothedX * GetEffectiveMoveSpeed();

        if (targetPlanarSpeed > 0f && contacts.BlockedRight)
            targetPlanarSpeed = 0f;
        if (targetPlanarSpeed < 0f && contacts.BlockedLeft)
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
        if (state.IsWallSliding)
            return 0f;

        float resolvedPlanarSpeed;

        if (state.IsGrounded && onSlope && noRawInput)
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
                    downhillSlideDeceleration * Time.fixedDeltaTime);
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
            if (state.IsGrounded)
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
                moveRate * Time.fixedDeltaTime);
        }

        if (resolvedPlanarSpeed > 0f && blockedRight)
            resolvedPlanarSpeed = 0f;
        if (resolvedPlanarSpeed < 0f && blockedLeft)
            resolvedPlanarSpeed = 0f;

        return resolvedPlanarSpeed;
    }

    private void ApplyHorizontalIntent(ref VelocityCommand25D command, SurfaceContacts25D contacts, float resolvedPlanarSpeed, bool shouldLockSlopeX, bool noRawInput)
    {
        if (state.IsGrounded && contacts.OnSlope)
        {
            Vector3 slopeVelocity;

            if (shouldLockSlopeX || (noRawInput && Mathf.Abs(resolvedPlanarSpeed) <= SpeedEpsilon))
            {
                slopeVelocity = Vector3.zero;
            }
            else
            {
                slopeVelocity = contacts.SlopeTangent * resolvedPlanarSpeed;
            }

            command.TargetVelocity.x = slopeVelocity.x;
            command.TargetVelocity.y = slopeVelocity.y;
            command.OverrideX = true;
            command.OverrideY = true;
            return;
        }

        command.TargetVelocity.x = resolvedPlanarSpeed;
        command.OverrideX = true;
    }

    private void ApplyVerticalRules(ref VelocityCommand25D command)
    {
        if (!state.IsWallSliding || command.ConsumedWallJump)
            return;

        float newY = command.OverrideY ? command.TargetVelocity.y : rb.linearVelocity.y;

        if (newY > 0f)
        {
            if (allowWallSlideUpwardMomentum)
                newY = Mathf.MoveTowards(newY, 0f, wallUpwardDeceleration * Time.fixedDeltaTime);
            else
                newY = 0f;
        }

        if (newY < -wallSlideSpeed)
            newY = -wallSlideSpeed;

        command.TargetVelocity.x = 0f;
        command.TargetVelocity.y = newY;
        command.OverrideX = true;
        command.OverrideY = true;
    }

    private bool TryBuildWallJump(ref VelocityCommand25D command)
    {
        if (!state.IsWallSliding)
            return false;

        bool buffered = (Time.time - state.LastJumpPressedTime) <= jumpBuffer;
        if (!buffered)
            return false;

        int jumpedFromWallSide = state.WallSlideSide;
        float jumpDirectionX = jumpedFromWallSide == WallSideLeft ? 1f : -1f;

        state.PendingSlopeStickAfterJump = false;
        state.SlopeLockUntilTime = InvalidPastTime;
        UpdateSlopeXConstraint(false);
        state.JumpBlockedUntilTime = InvalidPastTime;

        command.TargetVelocity.x = jumpDirectionX * wallJumpHorizontalSpeed;
        command.TargetVelocity.y = wallJumpVerticalSpeed;
        command.OverrideX = true;
        command.OverrideY = true;
        command.ConsumedWallJump = true;

        if (enableDoubleJump)
            state.JumpsRemaining = allowDoubleJumpAfterWallJump ? 1 : 0;
        else
            state.JumpsRemaining = 0;

        if (enableWallReattachCooldown)
        {
            state.WallReattachLockedSide = jumpedFromWallSide;
            state.WallReattachLockUntilTime = Time.time + wallReattachCooldown;
        }
        else
        {
            state.WallReattachLockedSide = WallSideNone;
            state.WallReattachLockUntilTime = InvalidPastTime;
        }

        ClearWallSlideState();
        state.LastJumpPressedTime = InvalidPastTime;
        state.LastJumpExecutedTime = Time.time;
        RecordSelfJump(SelfJumpKind.SingleJump);
        return true;
    }

    private bool TryBuildGroundOrAirJump(ref VelocityCommand25D command, SurfaceContacts25D contacts, float currentPlanarSpeed, bool noRawInput)
    {
        bool buffered = (Time.time - state.LastJumpPressedTime) <= jumpBuffer;
        if (!buffered)
            return false;

        if (IsJumpTemporarilyBlocked())
            return false;

        bool canCoyote = (Time.time - state.LastGroundedTime) <= coyoteTime;
        bool groundJumpAllowed = (state.IsGrounded || canCoyote) && state.JumpsRemaining > 0;
        bool airJumpAllowed = enableDoubleJump && state.JumpsRemaining > 0 && (!state.IsGrounded && !canCoyote);

        if (!doubleJumpOnlyInAir)
            airJumpAllowed = enableDoubleJump && state.JumpsRemaining > 0 && !state.IsGrounded;

        if (!groundJumpAllowed && !airJumpAllowed)
            return false;

        bool isFirstJump = !enableDoubleJump || state.JumpsRemaining >= 2;
        bool isDoubleJump = !isFirstJump;
        float impulse = isFirstJump ? jumpImpulse : doubleJumpImpulse;
        float takeoffSpeed = GetTakeoffSpeedFromImpulse(impulse);

        bool jumpedFromStandstillOnSlope =
            stickToSlopeAfterVerticalJump &&
            state.IsGrounded &&
            contacts.OnSlope &&
            noRawInput &&
            Mathf.Abs(currentPlanarSpeed) <= slopeLandingStickSpeed;

        state.PendingSlopeStickAfterJump = jumpedFromStandstillOnSlope;
        state.SlopeLockUntilTime = InvalidPastTime;
        UpdateSlopeXConstraint(false);
        state.JumpBlockedUntilTime = InvalidPastTime;

        command.TargetVelocity.y = takeoffSpeed;
        command.OverrideY = true;
        command.ConsumedGroundJump = true;

        if (isDoubleJump)
        {
            state.UsedDoubleJumpSinceLastGrounded = true;
            ActivateDoubleJumpSpeedBoostIfNeeded();
        }

        state.JumpsRemaining = Mathf.Max(0, state.JumpsRemaining - 1);
        state.LastJumpPressedTime = InvalidPastTime;
        state.LastJumpExecutedTime = Time.time;
        RecordSelfJump(isDoubleJump ? SelfJumpKind.DoubleJump : SelfJumpKind.SingleJump);
        return true;
    }

    private void ApplyVelocityCommand(VelocityCommand25D command)
    {
        Vector3 velocity = rb.linearVelocity;

        if (command.OverrideX)
            velocity.x = command.TargetVelocity.x;
        if (command.OverrideY)
            velocity.y = command.TargetVelocity.y;
        if (lockZ)
            velocity.z = 0f;

        rb.linearVelocity = velocity;
    }

    private void UpdateCurrentHorizontalSpeed(SurfaceContacts25D contacts)
    {
        Vector3 velocity = rb.linearVelocity;
        currentHorizontalSpeedAbs = contacts.OnSlope
            ? Mathf.Abs(Vector3.Dot(velocity, contacts.SlopeTangent))
            : Mathf.Abs(velocity.x);
    }

    private float GetCurrentPlanarSpeed(Vector3 velocity, SurfaceContacts25D contacts, bool justLanded)
    {
        if (!contacts.OnSlope)
            return velocity.x;

        if (justLanded)
            return ConvertWorldXToSlopeSpeed(velocity.x, contacts.SlopeTangent);

        return Vector3.Dot(velocity, contacts.SlopeTangent);
    }

    private float ConvertWorldXToSlopeSpeed(float worldX, Vector3 slopeTangent)
    {
        if (Mathf.Abs(slopeTangent.x) <= DotEpsilon)
            return 0f;

        return worldX / slopeTangent.x;
    }

    private float GetEffectiveMoveSpeed()
    {
        if (!enableDoubleJumpSpeedBoost || !state.IsDoubleJumpSpeedBoostActive)
            return moveSpeed;

        return moveSpeed + doubleJumpMoveSpeedBonus;
    }

    private void ActivateDoubleJumpSpeedBoostIfNeeded()
    {
        if (!enableDoubleJumpSpeedBoost)
            return;

        state.IsDoubleJumpSpeedBoostActive = true;
    }

    private void ClearDoubleJumpRuntimeState()
    {
        state.UsedDoubleJumpSinceLastGrounded = false;
        state.IsDoubleJumpSpeedBoostActive = false;
        state.JumpBlockedUntilTime = InvalidPastTime;
    }

    private void RecordSelfJump(SelfJumpKind jumpKind)
    {
        state.LastSelfJumpKind = jumpKind;
        state.LastSelfJumpStateVersion++;
    }

    private void ClearLastSelfJumpState()
    {
        state.LastSelfJumpKind = SelfJumpKind.None;
        state.LastSelfJumpStateVersion++;
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

    private void TryStartSlopeLandingLock(SurfaceContacts25D contacts, float landingPlanarSpeed)
    {
        bool requestedByVerticalJumpStick = state.PendingSlopeStickAfterJump;
        state.PendingSlopeStickAfterJump = false;

        if (!stickToSlopeAfterVerticalJump)
            return;
        if (!contacts.OnSlope)
            return;
        if (Mathf.Abs(inputX) > InputEpsilon)
            return;

        if (requestedByVerticalJumpStick || Mathf.Abs(landingPlanarSpeed) <= slopeLandingStickSpeed)
        {
            state.SlopeLockUntilTime = Time.time + slopeLandingLockTime;
            smoothedInputX = 0f;
            currentHorizontalSpeedAbs = 0f;
        }
    }

    private bool IsSlopeLandingLockActive()
    {
        return Time.time < state.SlopeLockUntilTime;
    }

    private bool ShouldLockSlopeX(SurfaceContacts25D contacts, bool noRawInput, float currentPlanarSpeed, float downhillSignedSpeed)
    {
        bool canHardLockBySpeed =
            IsSlopeLandingLockActive() ||
            Mathf.Abs(currentPlanarSpeed) <= slopeIdleLockSpeed ||
            (downhillSignedSpeed >= 0f && downhillSignedSpeed <= downhillSlideMinSpeed);

        return
            freezeXWhenIdleOnSlope &&
            lockZ &&
            state.IsGrounded &&
            contacts.OnSlope &&
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
        jumpImpulse = rb != null ? rb.mass * firstJumpVelocity : firstJumpVelocity;

        float secondJumpHeightUnits = doubleJumpHeightPixels / Mathf.Max(DotEpsilon, pixelsPerUnit);
        float secondJumpVelocity = Mathf.Sqrt(Mathf.Max(0f, 2f * requiredGravity * secondJumpHeightUnits));
        doubleJumpImpulse = rb != null ? rb.mass * secondJumpVelocity : secondJumpVelocity;
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

    private float GetTakeoffSpeedFromImpulse(float impulse)
    {
        float mass = rb != null ? Mathf.Max(DotEpsilon, rb.mass) : 1f;
        return impulse / mass;
    }

    private void UpdateWallSlideState(SurfaceContacts25D contacts)
    {
        if (IsVaultingNow || !enableWallSlide || state.IsGrounded)
        {
            ClearWallSlideState();
            return;
        }

        if (state.IsWallSliding)
        {
            bool stillTouchingCurrentWall = IsTouchingWallSide(contacts, state.WallSlideSide);
            if (!stillTouchingCurrentWall)
            {
                ClearWallSlideState();
                TryStartWallLatch(contacts);
                return;
            }

            if (ShouldDetachFromCurrentWallByHold())
            {
                ClearWallSlideState();
                return;
            }

            return;
        }

        TryStartWallLatch(contacts);
    }

    private void TryStartWallLatch(SurfaceContacts25D contacts)
    {
        bool canLatchLeft = contacts.BlockedLeft && CanLatchToWallSide(WallSideLeft);
        bool canLatchRight = contacts.BlockedRight && CanLatchToWallSide(WallSideRight);

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
        if (state.WallSlideSide == WallSideLeft)
            return inputX > InputEpsilon;
        if (state.WallSlideSide == WallSideRight)
            return inputX < -InputEpsilon;
        return false;
    }

    private bool ShouldDetachFromCurrentWallByHold()
    {
        if (!detachFromWallOnOppositeInput)
        {
            state.WallDetachHoldTimer = 0f;
            return false;
        }

        if (!WantsToDetachFromCurrentWall())
        {
            state.WallDetachHoldTimer = 0f;
            return false;
        }

        state.WallDetachHoldTimer += Time.fixedDeltaTime;
        return state.WallDetachHoldTimer >= wallDetachHoldTime;
    }

    private bool IsTouchingWallSide(SurfaceContacts25D contacts, int side)
    {
        if (side == WallSideLeft)
            return contacts.BlockedLeft;
        if (side == WallSideRight)
            return contacts.BlockedRight;
        return false;
    }

    private void SetWallSlideState(int side)
    {
        state.IsWallSliding = true;
        state.WallSlideSide = side;
        state.WallDetachHoldTimer = 0f;
    }

    private void ClearWallSlideState()
    {
        bool wasWallSliding = state.IsWallSliding;

        state.IsWallSliding = false;
        state.WallSlideSide = WallSideNone;
        state.WallDetachHoldTimer = 0f;

        if (wasWallSliding)
            LastWallSlideFinishedTime = Time.time;
    }

    private bool IsWallReattachLockedForSide(int side)
    {
        if (!enableWallReattachCooldown)
            return false;
        if (state.WallReattachLockedSide != side)
            return false;
        return Time.time < state.WallReattachLockUntilTime;
    }

    public void NotifyVaultStarted()
    {
        state.PendingSlopeStickAfterJump = false;
        state.SlopeLockUntilTime = InvalidPastTime;
        state.JumpBlockedUntilTime = InvalidPastTime;
        state.LastJumpPressedTime = InvalidPastTime;
        smoothedInputX = 0f;
        currentHorizontalSpeedAbs = 0f;

        ClearWallSlideState();
        ClearLastSelfJumpState();
        UpdateSlopeXConstraint(false);

        if (rb != null && !rb.isKinematic)
        {
            Vector3 velocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, 0f, lockZ ? 0f : velocity.z);
        }
    }

    public void NotifyVaultFinished()
    {
        state.PendingSlopeStickAfterJump = false;
        state.SlopeLockUntilTime = InvalidPastTime;
        state.JumpBlockedUntilTime = InvalidPastTime;
        currentHorizontalSpeedAbs = 0f;
        state.LastGroundedTime = Time.time;

        ResetJumpCounter();
        ClearDoubleJumpRuntimeState();
        ClearLastSelfJumpState();
        ClearWallSlideState();
        UpdateSlopeXConstraint(false);

        LastVaultFinishedTime = Time.time;
    }

    private void LogAccelerationDebug(
        float currentPlanarSpeed,
        float targetPlanarSpeed,
        float resolvedPlanarSpeed,
        bool blockedLeft,
        bool blockedRight,
        SurfaceContacts25D contacts,
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
            $"boostActive={state.IsDoubleJumpSpeedBoostActive} " +
            $"doubleJumpUsed={state.UsedDoubleJumpSinceLastGrounded} " +
            $"jumpBlocked={IsJumpTemporarilyBlocked()} " +
            $"grounded={state.IsGrounded} " +
            $"wallSliding={state.IsWallSliding} " +
            $"wallSide={state.WallSlideSide} " +
            $"velY={rb.linearVelocity.y:0.00} " +
            $"onSlope={contacts.OnSlope} " +
            $"slopeAngle={contacts.SlopeAngle:0.0} " +
            $"downhillSigned={downhillSignedSpeed:0.00} " +
            $"xLocked={slopeXLocked} " +
            $"blockedL={blockedLeft} " +
            $"blockedR={blockedRight}",
            this);
    }

    private void OnDrawGizmosSelected()
    {
        if (col == null)
            col = GetComponent<CapsuleCollider>();
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        EnsureSurfaceSensor();
        SyncSurfaceSensor();
        surfaceSensor.DrawGizmos();
    }
}
