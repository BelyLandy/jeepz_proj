using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
[RequireComponent(typeof(CharacterFacingResolver25D))]
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
        WallJump = 3,
    }

    [Header("2.5D Constraint")]
    [SerializeField] private bool lockZ = true;
    [SerializeField] private float lockedZ = 0f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 11f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float deceleration = 60f;
    [SerializeField, Range(0f, 1f)] private float airControl = 0.8f;

    [Header("Air Movement")]
    [Tooltip("Насколько быстро гасится горизонтальная скорость в воздухе после отпускания кнопки направления.")]
    [SerializeField] private float airDeceleration = 8f;

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
    [SerializeField] private float inputAcceleration = 2f;

    [Tooltip("Плавность отпускания/торможения направления к 0.")]
    [SerializeField] private float inputDeceleration = 12f;

    [Header("Landing Turn Control")]
    [Tooltip("Ослаблять ли занос при резком развороте сразу после приземления.")]
    [SerializeField] private bool reduceLandingReverseSkid = true;

    [Tooltip("Как долго после приземления действует анти-занос для разворота в противоположную сторону.")]
    [SerializeField] private float landingReverseNoSkidWindow = 0.1f;

    [Tooltip("Во сколько раз сохранить старую горизонтальную скорость при landing-развороте. 0 = полностью срезать перенос, 1 = оставить как есть.")]
    [SerializeField, Range(0f, 1f)] private float landingReverseCarryMultiplier = 0.35f;

    [Tooltip("Минимальная скорость, при которой landing-разворот считается достаточно сильным для анти-заноса.")]
    [SerializeField] private float landingReverseMinSpeed = 1.5f;

    [Header("Air Shot Physics Assist")]
    [Tooltip("Включить новую воздушную помощь от выстрелов: не прямой recoil velocity, а краткое замедление или ускорение падения.")]
    [SerializeField] private bool useAirShotPhysicsAssist = true;

    [Tooltip("Нормализовать ли вклад X/Y при диагональном air-shot. Включено = диагональ делит влияние между осями. Выключено = X и Y считаются независимо по сырым осям выстрела.")]
    [SerializeField] private bool normalizeAirShotRecoilAxes = true;

    [Tooltip("На сколько секунд обычный air-shot замедляет падение.")]
    [SerializeField] private float airShotSlowFallDuration = 0.07f;

    [Tooltip("Множитель gravity во время short slow-fall окна. Меньше = сильнее подвисание.")]
    [SerializeField, Range(0f, 1f)] private float airShotSlowFallGravityMultiplier = 0.25f;

    [Tooltip("Максимальная скорость падения вниз во время slow-fall окна.")]
    [SerializeField] private float airShotSlowFallMaxDownSpeed = 3.5f;

    [Tooltip("Начиная с какого положительного Y направления выстрел считается выстрелом вверх и включает fast-fall.")]
    [SerializeField, Range(0f, 1f)] private float airShotUpwardThreshold = 0.35f;

    [Tooltip("Начиная с какого отрицательного Y направления выстрел считается выстрелом вниз и включает slow-fall. Вблизи горизонтали по Y не происходит ничего.")]
    [SerializeField, Range(0f, 1f)] private float airShotDownwardThreshold = 0.35f;

    [Tooltip("На сколько секунд выстрел вверх включает усиленное падение.")]
    [SerializeField] private float airShotFastFallDuration = 0.06f;

    [Tooltip("Множитель gravity во время fast-fall окна после выстрела вверх.")]
    [SerializeField] private float airShotFastFallGravityMultiplier = 1.8f;

    [Tooltip("Во сколько раз сразу урезать текущий подъём, если выстрел в воздухе направлен вверх.")]
    [SerializeField, Range(0f, 1f)] private float airShotUpwardRiseCutMultiplier = 0.35f;

    [Tooltip("Начиная с какого |X| нормализованного направления выстрела включать horizontal recoil-модификатор.")]
    [SerializeField, Range(0f, 1f)] private float airShotHorizontalThreshold = 0.35f;

    [Tooltip("На сколько секунд выстрел в воздухе влияет на движение по X в сторону выстрела.")]
    [SerializeField] private float airShotHorizontalRecoilDuration = 0.07f;

    [Tooltip("Насколько быстро в воздухе гасится скорость по X в сторону выстрела во время horizontal recoil окна.")]
    [SerializeField] private float airShotHorizontalRecoilDeceleration = 45f;

    [Tooltip("Максимальная скорость по X в сторону выстрела во время horizontal recoil окна. Меньше = сильнее режется движение в сторону выстрела.")]
    [SerializeField] private float airShotHorizontalRecoilMaxSpeedTowardShot = 2f;

    [Header("Slope Handling")]
    [SerializeField] private bool enableSlopeHandling = false;
    [SerializeField] private LayerMask slopeLayerMask = 0;

    [Tooltip("Начиная с какого угла считать поверхность именно склоном.")]
    [SerializeField, Range(0f, 89f)] private float slopeMinAngle = 1f;

    [Tooltip("Торможение после отпускания кнопки при движении вниз по склону. Меньше значение = дальше скольжение.")]
    [SerializeField] private float downhillSlideDeceleration = 30f;

    [Tooltip("Минимальная скорость вдоль склона, чтобы после отпускания был заметный доскольз.")]
    [SerializeField] private float downhillSlideMinSpeed = 1f;

    [Header("Run Stop State")]
    [Tooltip("Включить runtime-state для анимации stop на плоской поверхности после отпускания направления.")]
    [SerializeField] private bool enableRunStopState = true;

    [Tooltip("Скорость, выше которой можно впервые войти в run stop.")]
    [SerializeField] private float runStopEnterSpeed = 2.2f;

    [Tooltip("Скорость, ниже которой run stop завершается.")]
    [SerializeField] private float runStopExitSpeed = 0.8f;

    [Tooltip("Как долго после реального ground-run можно ещё активировать run stop.")]
    [SerializeField] private float runStopRecentRunWindow = 0.18f;

    [Tooltip("Мёртвая зона input по X для определения, что направление отпущено.")]
    [SerializeField] private float runStopInputDeadzone = 0.1f;

    [Tooltip("Минимальная доля от effective move speed, начиная с которой движение считается полноценным ground-run для будущей stop-анимации.")]
    [SerializeField, Range(0f, 1f)] private float runStopRunQualificationSpeedNormalized = 0.35f;

    [Tooltip("Короткое окно подавления run stop сразу после приземления.")]
    [SerializeField] private float runStopSuppressAfterLandingWindow = 0.08f;

    [Tooltip("Если true, crouch не может активировать обычный run stop.")]
    [SerializeField] private bool suppressRunStopWhileCrouching = true;

    [Tooltip("Если true, любой control lock подавляет обычный run stop.")]
    [SerializeField] private bool suppressRunStopWhileControlLocked = true;

    [Tooltip("Если true, lock stance подавляет обычный run stop.")]
    [SerializeField] private bool suppressRunStopWhileLockStance = true;

    [Tooltip("Если true, stop-анимация не включается на slope-поверхностях.")]
    [SerializeField] private bool suppressRunStopOnAuthorizedSlope = true;

    [Tooltip("Если true, отсутствие input по X должно сочетаться с нейтральным input по Y.")]
    [SerializeField] private bool requireNeutralMoveYForRunStop = false;

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
    [SerializeField] private bool debugSlopeHandlingState = false;
    [Tooltip("Как часто писать лог разгона.")]
    [SerializeField] private float debugLogInterval = 0.1f;

    [Tooltip("Писать ли в консоль сообщения о выполнении Single Jump и Double Jump.")]
    [SerializeField] private bool debugJumpMessages = false;

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

    [Header("Vault Exit Speed")]
    [Tooltip("Сохранять горизонтальную скорость X после завершения vault.")]
    [SerializeField] private bool preserveHorizontalSpeedAfterVault = true;

    [Tooltip("Множитель сохранённой скорости X на выходе из vault.")]
    [SerializeField] private float vaultExitHorizontalSpeedMultiplier = 0.9f;

    [Tooltip("Возвращать скорость только если игрок всё ещё держит то же направление, что и до vault.")]
    [SerializeField] private bool requireHeldDirectionForVaultSpeedRestore = true;

    [Tooltip("Минимальный модуль скорости X, который вообще имеет смысл восстанавливать после vault.")]
    [SerializeField] private float vaultExitMinRestoreSpeed = 0.05f;

    [Header("Lock Stance")]
    [SerializeField] private bool enableLockStance = true;

    [Tooltip("Если кнопку LockStance зажали в воздухе, стойка включится при приземлении, пока кнопка все еще удерживается.")]
    [SerializeField] private bool queueLockStanceIfPressedInAir = true;

    [Tooltip("Разрешать ли прыжок, пока LockStance уже вошел в активный режим.")]
    [SerializeField] private bool allowJumpWhileLockStance = true;

    [Header("One Way Drop Down")]
    [Tooltip("Разрешить спрыгивание вниз через текущую one-way платформу по команде Down + Jump.")]
    [SerializeField] private bool enableOneWayDropDown = true;

    [Tooltip("Насколько сильно нужно нажать вниз по Y у Move input, чтобы считалось drop-down намерением.")]
    [SerializeField, Range(0f, 1f)] private float oneWayDropDownInputThreshold = 0.5f;

    [Tooltip("Минимальная длительность ignore для текущей support one-way платформы при drop-down.")]
    [SerializeField] private float oneWayDropDownDuration = 0.18f;

    [Tooltip("Минимальная вертикальная скорость вниз, которую мы задаём в момент старта drop-down, чтобы герой сразу сошёл с платформы.")]
    [SerializeField] private float oneWayDropDownDownwardSpeed = 2.5f;

    [Tooltip("Если включено, то после drop-down через one-way платформу обычный первый прыжок блокируется, и остаётся только double jump.")]
    [SerializeField] private bool onlyDoubleJumpAfterOneWayDropDown = true;

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

    [Tooltip("Какой по силе противоположный horizontal input нужен для detach от стены. Полезно против случайного дрейфа стика.")]
    [SerializeField, Range(0f, 1f)] private float wallDetachOppositeInputThreshold = 0.35f;

    [Tooltip("Максимальная скорость скольжения вниз по стене.")]
    [SerializeField] private float wallSlideSpeed = 2.5f;

    [Tooltip("Позволяет удержанием вниз ускорять скольжение по стене, если LockStance не удерживается.")]
    [SerializeField] private bool enableFastWallSlideOnDownInput = true;

    [Tooltip("Насколько сильно нужно нажать вниз, чтобы включить ускоренное скольжение по стене.")]
    [SerializeField, Range(0f, 1f)] private float fastWallSlideDownInputThreshold = 0.5f;

    [Tooltip("Максимальная скорость ускоренного скольжения вниз по стене при удержании вниз.")]
    [SerializeField] private float fastWallSlideSpeed = 5.5f;

    [Tooltip("Насколько быстро обычное скольжение разгоняется до fast wall slide при удержании вниз.")]
    [SerializeField] private float fastWallSlideAcceleration = 35f;

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

    [Tooltip("Какие слои считаются валидными именно для wall slide / wall jump / wall latch. Если маска пуста, используется GroundMask.")]
    [SerializeField] private LayerMask wallInteractionMask;

    [Header("Gravity")]
    [Tooltip("Используется только если autoTuneJump=false. Аналог gravityScale для 3D.")]
    [SerializeField] private float manualGravityScale = 1f;

    [Header("Grounding")]
    [SerializeField] private LayerMask groundMask;

    [Header("Ground Probe")]
    [Tooltip("Насколько глубоко вниз ищем землю под капсулой.")]
    [SerializeField] private float groundProbeDistance = 0.16f;

    [Tooltip("Небольшой отступ вверх для старта SphereCast, чтобы избежать дрожания.")]
    [SerializeField] private float groundProbeStartOffset = 0.03f;

    [Tooltip("Насколько уменьшаем радиус проверки земли, чтобы не цепляться краями.")]
    [SerializeField] private float groundProbeInset = 0.33f;

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
    private CharacterFacingResolver25D facingResolver;
    private CharacterCrouch25D crouchController;
    private PlayerControlLock25D controlLock;
    private RBCharacter25DSurfaceSensor surfaceSensor;
    private OneWayPlatformController oneWayPlatformController;

    private FrameInput25D currentInput;
    private SurfaceContacts25D lastContacts;
    private LocomotionState25D state;

    private float inputX;
    private float externalMoveX;
    private float externalMoveY;
    private bool externalJumpHeld;
    private bool externalLockStanceHeld;
    private bool lockStanceLatched;
    private bool lockStanceQueued;
    private bool externalJumpPressedQueued;
    private bool externalJumpReleasedQueued;
    private float smoothedInputX;
    private float currentHorizontalSpeedAbs;
    private bool isSlopeSlidingNow;
    private float slopeSlideSpeedNormalized;
    private bool isRunStoppingNow;
    private float runStopSpeedNormalized;
    private float lastGroundRunTime = InvalidPastTime;
    private float pendingVaultExitVelocityX;
    private bool hasPendingVaultExitVelocityRestore;
    private float vaultExitVelocityRestoreReadyTime = InvalidPastTime;
    private float lastNonZeroInputX = 1f;
    private float lastDebugLogTime = InvalidPastTime;
    private float runtimeGravityScale = 1f;
    private bool slopeXLocked;
    private bool lastSlopeDebugRuntimeActive;
    private bool lastSlopeDebugMasterEnabled;
    private bool lastSlopeDebugAuthorized;
    private bool lastSlopeDebugGrounded;
    private int lastSlopeDebugGroundLayer = int.MinValue;
    private float externalHorizontalLocomotionSuppressUntilTime = InvalidPastTime;
    private float externalWallSlideSuppressUntilTime = InvalidPastTime;
    private float externalVaultStartSuppressUntilTime = InvalidPastTime;
    private float airShotSlowFallUntilTime = InvalidPastTime;
    private float airShotFastFallUntilTime = InvalidPastTime;
    private float airShotHorizontalRecoilUntilTime = InvalidPastTime;
    private float airShotSlowFallWeight;
    private float airShotFastFallWeight;
    private float airShotHorizontalRecoilWeight;
    private int airShotHorizontalShotSign;

    public bool IsGroundedNow => state.IsGrounded;
    public bool IsWallSliding => state.IsWallSliding;
    public bool IsVaultingNow => vaulting != null && vaulting.IsVaulting;
    public float LastVaultFinishedTime { get; private set; } = InvalidPastTime;
    public float LastWallSlideFinishedTime { get; private set; } = InvalidPastTime;
    public int LastLandingStateVersion { get; private set; }
    public float LastLandingTime { get; private set; } = InvalidPastTime;
    public int WallSlideSide => state.WallSlideSide;
    public bool IsLockStanceHeld => externalLockStanceHeld;
    public bool IsLockStanceLatched => enableLockStance && lockStanceLatched;
    public bool IsLockStanceQueued => enableLockStance && lockStanceQueued;
    public bool IsLockStanceMovementActive => IsLockStanceLatched;
    public bool IsLockStanceGroundActive => IsLockStanceLatched && state.IsGrounded;
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
    public bool IsSlopeSlidingNow => isSlopeSlidingNow;
    public float SlopeSlideSpeedNormalized => slopeSlideSpeedNormalized;
    public bool IsRunStoppingNow => isRunStoppingNow;
    public float RunStopSpeedNormalized => runStopSpeedNormalized;

    public Rigidbody RigidbodyComponent => rb;
    public CapsuleCollider CapsuleColliderComponent => col;
    public SurfaceContacts25D LastSurfaceContacts => lastContacts;
    public bool IsSlopeSurfaceAuthorizedNow => lastContacts.HasGroundSurface && lastContacts.IsSlopeSurfaceAuthorized;
    public bool IsSlopeHandlingRuntimeActiveNow => IsSlopeHandlingRuntimeActive(lastContacts);
    public LayerMask GroundMask => groundMask;
    public LayerMask WallInteractionMask => wallInteractionMask.value != 0 ? wallInteractionMask : groundMask;
    public bool UsesLockedZ => lockZ;
    public float LockedZPosition => lockedZ;
    public int VaultFacingSignFromInput => lastNonZeroInputX < 0f ? -1 : 1;
    public int ResolvedFacingSign => facingResolver != null ? facingResolver.ResolvedFacingSign : VaultFacingSignFromInput;
    public bool HasFacingOverride => facingResolver != null && facingResolver.HasFacingOverride;
    public FacingOverrideSource25D CurrentFacingOverrideSource => facingResolver != null ? facingResolver.CurrentOverrideSource : FacingOverrideSource25D.None;
    public CharacterFacingResolver25D FacingResolverComponent => facingResolver;
    public float MoveInputY => externalMoveY;

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

        reduceLandingReverseSkid = true;
        landingReverseNoSkidWindow = 0.1f;
        landingReverseCarryMultiplier = 0.35f;
        landingReverseMinSpeed = 1.5f;

        useAirShotPhysicsAssist = true;
        normalizeAirShotRecoilAxes = true;
        airShotSlowFallDuration = 0.07f;
        airShotSlowFallGravityMultiplier = 0.25f;
        airShotSlowFallMaxDownSpeed = 3.5f;
        airShotUpwardThreshold = 0.35f;
        airShotDownwardThreshold = 0.35f;
        airShotFastFallDuration = 0.06f;
        airShotFastFallGravityMultiplier = 1.8f;
        airShotUpwardRiseCutMultiplier = 0.35f;
        airShotHorizontalThreshold = 0.35f;
        airShotHorizontalRecoilDuration = 0.07f;
        airShotHorizontalRecoilDeceleration = 45f;
        airShotHorizontalRecoilMaxSpeedTowardShot = 2f;

        enableSlopeHandling = true;

        int slopeLayer = LayerMask.NameToLayer("Slope");
        if (slopeLayer >= 0)
            slopeLayerMask = 1 << slopeLayer;

        slopeMinAngle = 1f;
        downhillSlideDeceleration = 30f;
        downhillSlideMinSpeed = 1f;
        enableRunStopState = true;
        runStopEnterSpeed = 2.2f;
        runStopExitSpeed = 0.8f;
        runStopRecentRunWindow = 0.18f;
        runStopInputDeadzone = 0.1f;
        runStopRunQualificationSpeedNormalized = 0.35f;
        runStopSuppressAfterLandingWindow = 0.08f;
        suppressRunStopWhileCrouching = true;
        suppressRunStopWhileControlLocked = true;
        suppressRunStopWhileLockStance = true;
        suppressRunStopOnAuthorizedSlope = true;
        requireNeutralMoveYForRunStop = false;
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

        enableLockStance = true;
        queueLockStanceIfPressedInAir = true;
        allowJumpWhileLockStance = true;

        enableOneWayDropDown = true;
        oneWayDropDownInputThreshold = 0.5f;
        oneWayDropDownDuration = 0.18f;
        oneWayDropDownDownwardSpeed = 2.5f;
        onlyDoubleJumpAfterOneWayDropDown = true;

        preserveHorizontalSpeedAfterVault = true;
        vaultExitHorizontalSpeedMultiplier = 0.9f;
        requireHeldDirectionForVaultSpeedRestore = true;
        vaultExitMinRestoreSpeed = 0.05f;

        wallCheckDistance = 0.5f;
        wallCheckHeightOffset = 0f;
        wallCheckRadius = 0.08f;

        enableWallSlide = true;
        requireInputToLatchWall = false;
        detachFromWallOnOppositeInput = true;
        wallDetachHoldTime = 0.3f;
        wallSlideSpeed = 2.5f;
        enableFastWallSlideOnDownInput = true;
        fastWallSlideDownInputThreshold = 0.5f;
        fastWallSlideSpeed = 5.5f;
        fastWallSlideAcceleration = 35f;
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

        wallInteractionMask = groundMask;

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

        TryApplyPendingVaultExitVelocityRestore();
        ApplyExtraGravity();
        ApplyAirShotVelocityAssist();

        if (!enableDoubleJumpSpeedBoost)
            state.IsDoubleJumpSpeedBoostActive = false;

        currentInput.RawX = inputX;
        currentInput.RawY = externalMoveY;
        currentInput.JumpHeld = externalJumpHeld && (!IsLockStanceMovementActive || allowJumpWhileLockStance);
        currentInput.JumpPressed = false;
        currentInput.JumpReleased = false;
        currentInput.FacingSign = VaultFacingSignFromInput;

        lastContacts = GatherSurfaceContacts();
        UpdateGroundedState(lastContacts, out bool justLandedThisFixed, out bool leftGroundThisFixed);
        ResolveLockStanceState();

        DebugSlopeHandlingState(lastContacts);

        bool startedOneWayDropDown = TryStartOneWayDropDown(ref lastContacts);
        if (startedOneWayDropDown)
        {
            justLandedThisFixed = false;
            leftGroundThisFixed = true;
        }

        if (leftGroundThisFixed)
        {
            state.SlopeLockUntilTime = InvalidPastTime;
            UpdateSlopeXConstraint(false);
        }

        if (!state.IsGrounded && Mathf.Abs(inputX) > InputEpsilon)
            state.PendingSlopeStickAfterJump = false;

        if (!startedOneWayDropDown && !IsLockStanceGroundActive && !IsVaultStartSuppressed() && vaulting != null && vaulting.TryStartVault())
        {
            currentHorizontalSpeedAbs = 0f;
            isSlopeSlidingNow = false;
            slopeSlideSpeedNormalized = 0f;
            isRunStoppingNow = false;
            runStopSpeedNormalized = 0f;
            state.WasGroundedLastFixed = false;
            return;
        }

        UpdateSmoothedInput();
        currentInput.SmoothedX = smoothedInputX;

        UpdateWallSlideState(lastContacts);

        VelocityCommand25D command = BuildVelocityCommand(currentInput, lastContacts, justLandedThisFixed);
        ApplyVelocityCommand(command);

        if (IsLockStanceMovementActive)
        {
            Vector3 velocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, velocity.y, lockZ ? 0f : velocity.z);

            smoothedInputX = 0f;
            currentHorizontalSpeedAbs = 0f;
        }

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
        isSlopeSlidingNow = false;
        slopeSlideSpeedNormalized = 0f;
        isRunStoppingNow = false;
        runStopSpeedNormalized = 0f;
        state.WasGroundedLastFixed = false;
        return true;
    }

    private void CacheComponents()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (col == null) col = GetComponent<CapsuleCollider>();
        if (vaulting == null) vaulting = GetComponent<RBCharacter25DVaulting>();
        if (facingResolver == null) facingResolver = GetComponent<CharacterFacingResolver25D>();
        if (facingResolver == null && Application.isPlaying)
            facingResolver = gameObject.AddComponent<CharacterFacingResolver25D>();
        if (crouchController == null) crouchController = GetComponent<CharacterCrouch25D>();
        if (controlLock == null) controlLock = GetComponent<PlayerControlLock25D>();
        if (oneWayPlatformController == null) oneWayPlatformController = GetComponent<OneWayPlatformController>();
    }

    private void EnsureSurfaceSensor()
    {
        if (surfaceSensor == null)
            surfaceSensor = new RBCharacter25DSurfaceSensor();

        surfaceSensor.Initialize(rb, col);
        surfaceSensor.SetOneWayController(oneWayPlatformController);
        SyncSurfaceSensor();
    }

    private void SyncSurfaceSensor()
    {
        if (surfaceSensor == null)
            return;

        surfaceSensor.SyncSettings(
            groundMask,
            WallInteractionMask,
            groundProbeDistance,
            groundProbeStartOffset,
            groundProbeInset,
            wallCheckDistance,
            wallCheckHeightOffset,
            wallCheckRadius,
            wallMinNormalX,
            wallMaxNormalY,
            enableSlopeHandling,
            slopeLayerMask,
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
        externalHorizontalLocomotionSuppressUntilTime = InvalidPastTime;
        smoothedInputX = 0f;
        currentHorizontalSpeedAbs = 0f;
        isSlopeSlidingNow = false;
        slopeSlideSpeedNormalized = 0f;
        isRunStoppingNow = false;
        runStopSpeedNormalized = 0f;
        lastGroundRunTime = InvalidPastTime;
        pendingVaultExitVelocityX = 0f;
        hasPendingVaultExitVelocityRestore = false;
        vaultExitVelocityRestoreReadyTime = InvalidPastTime;
        externalMoveX = 0f;
        externalLockStanceHeld = false;
        externalJumpHeld = false;
        externalJumpPressedQueued = false;
        externalJumpReleasedQueued = false;
        airShotSlowFallUntilTime = InvalidPastTime;
        airShotFastFallUntilTime = InvalidPastTime;
        airShotHorizontalRecoilUntilTime = InvalidPastTime;
        airShotSlowFallWeight = 0f;
        airShotFastFallWeight = 0f;
        airShotHorizontalRecoilWeight = 0f;
        airShotHorizontalShotSign = 0;

        if (facingResolver != null)
        {
            facingResolver.ClearAllOverrides();
            facingResolver.SetBaseFacingSign(VaultFacingSignFromInput);
        }

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
        landingReverseNoSkidWindow = Mathf.Max(0f, landingReverseNoSkidWindow);
        landingReverseCarryMultiplier = Mathf.Clamp01(landingReverseCarryMultiplier);
        landingReverseMinSpeed = Mathf.Max(0f, landingReverseMinSpeed);

        airShotSlowFallDuration = Mathf.Max(0f, airShotSlowFallDuration);
        airShotSlowFallGravityMultiplier = Mathf.Clamp01(airShotSlowFallGravityMultiplier);
        airShotSlowFallMaxDownSpeed = Mathf.Max(0f, airShotSlowFallMaxDownSpeed);
        airShotUpwardThreshold = Mathf.Clamp01(airShotUpwardThreshold);
        airShotDownwardThreshold = Mathf.Clamp01(airShotDownwardThreshold);
        airShotFastFallDuration = Mathf.Max(0f, airShotFastFallDuration);
        airShotFastFallGravityMultiplier = Mathf.Max(0f, airShotFastFallGravityMultiplier);
        airShotUpwardRiseCutMultiplier = Mathf.Clamp01(airShotUpwardRiseCutMultiplier);
        airShotHorizontalThreshold = Mathf.Clamp01(airShotHorizontalThreshold);
        airShotHorizontalRecoilDuration = Mathf.Max(0f, airShotHorizontalRecoilDuration);
        airShotHorizontalRecoilDeceleration = Mathf.Max(0f, airShotHorizontalRecoilDeceleration);
        airShotHorizontalRecoilMaxSpeedTowardShot = Mathf.Max(0f, airShotHorizontalRecoilMaxSpeedTowardShot);

        slopeMinAngle = Mathf.Clamp(slopeMinAngle, 0f, 89f);
        downhillSlideDeceleration = Mathf.Max(0f, downhillSlideDeceleration);
        downhillSlideMinSpeed = Mathf.Max(0f, downhillSlideMinSpeed);
        runStopEnterSpeed = Mathf.Max(0f, runStopEnterSpeed);
        runStopExitSpeed = Mathf.Clamp(runStopExitSpeed, 0f, runStopEnterSpeed);
        runStopRecentRunWindow = Mathf.Max(0f, runStopRecentRunWindow);
        runStopInputDeadzone = Mathf.Clamp01(runStopInputDeadzone);
        runStopRunQualificationSpeedNormalized = Mathf.Clamp01(runStopRunQualificationSpeedNormalized);
        runStopSuppressAfterLandingWindow = Mathf.Max(0f, runStopSuppressAfterLandingWindow);
        slopeLandingStickSpeed = Mathf.Max(0f, slopeLandingStickSpeed);
        slopeIdleLockSpeed = Mathf.Max(0f, slopeIdleLockSpeed);
        slopeLandingLockTime = Mathf.Max(0f, slopeLandingLockTime);

        debugLogInterval = Mathf.Max(0.01f, debugLogInterval);

        jumpImpulse = Mathf.Max(0f, jumpImpulse);
        coyoteTime = Mathf.Max(0f, coyoteTime);
        jumpBuffer = Mathf.Max(0f, jumpBuffer);

        doubleJumpImpulse = Mathf.Max(0f, doubleJumpImpulse);

        vaultExitHorizontalSpeedMultiplier = Mathf.Max(0f, vaultExitHorizontalSpeedMultiplier);
        vaultExitMinRestoreSpeed = Mathf.Max(0f, vaultExitMinRestoreSpeed);

        wallCheckDistance = Mathf.Max(0.001f, wallCheckDistance);
        wallCheckRadius = Mathf.Max(0.001f, wallCheckRadius);
        wallDetachHoldTime = Mathf.Max(0f, wallDetachHoldTime);
        wallDetachOppositeInputThreshold = Mathf.Clamp01(wallDetachOppositeInputThreshold);
        fastWallSlideDownInputThreshold = Mathf.Clamp01(fastWallSlideDownInputThreshold);
        wallSlideSpeed = Mathf.Max(0f, wallSlideSpeed);
        fastWallSlideSpeed = Mathf.Max(wallSlideSpeed, fastWallSlideSpeed);
        fastWallSlideAcceleration = Mathf.Max(0f, fastWallSlideAcceleration);
        wallUpwardDeceleration = Mathf.Max(0f, wallUpwardDeceleration);
        wallJumpHorizontalSpeed = Mathf.Max(0f, wallJumpHorizontalSpeed);
        wallJumpVerticalSpeed = Mathf.Max(0f, wallJumpVerticalSpeed);
        wallReattachCooldown = Mathf.Max(0f, wallReattachCooldown);
        wallMinNormalX = Mathf.Clamp01(wallMinNormalX);
        wallMaxNormalY = Mathf.Clamp01(wallMaxNormalY);

        if (wallInteractionMask.value == 0)
            wallInteractionMask = groundMask;

        manualGravityScale = Mathf.Max(0f, manualGravityScale);
        groundProbeDistance = Mathf.Max(0.001f, groundProbeDistance);
        groundProbeStartOffset = Mathf.Max(0f, groundProbeStartOffset);
        groundProbeInset = Mathf.Max(0f, groundProbeInset);

        oneWayDropDownInputThreshold = Mathf.Clamp01(oneWayDropDownInputThreshold);
        oneWayDropDownDuration = Mathf.Max(0.01f, oneWayDropDownDuration);
        oneWayDropDownDownwardSpeed = Mathf.Max(0f, oneWayDropDownDownwardSpeed);

        pixelsPerUnit = Mathf.Max(0.0001f, pixelsPerUnit);
        jumpHeightPixels = Mathf.Max(0f, jumpHeightPixels);
        timeToApex = Mathf.Max(0.0001f, timeToApex);
        doubleJumpHeightPixels = Mathf.Max(0f, doubleJumpHeightPixels);
    }

    private void ReadInput()
    {
        bool suppressHorizontalLocomotion = IsHorizontalLocomotionSuppressed();
        inputX = (IsLockStanceMovementActive || suppressHorizontalLocomotion) ? 0f : externalMoveX;

        if (Mathf.Abs(inputX) > InputEpsilon)
        {
            lastNonZeroInputX = Mathf.Sign(inputX);
            if (facingResolver != null)
                facingResolver.SetBaseFacingSign((int)Mathf.Sign(inputX));
        }

        if (IsVaultingNow)
        {
            externalJumpPressedQueued = false;
            return;
        }

        if (IsLockStanceMovementActive && !allowJumpWhileLockStance)
        {
            externalJumpPressedQueued = false;
            currentInput.JumpPressed = false;
            state.LastJumpPressedTime = InvalidPastTime;
            return;
        }

        if (!externalJumpPressedQueued)
            return;

        currentInput.JumpPressed = true;
        state.LastJumpPressedTime = Time.time;
        externalJumpPressedQueued = false;
    }

    private bool TryStartOneWayDropDown(ref SurfaceContacts25D contacts)
    {
        if (!enableOneWayDropDown || oneWayPlatformController == null)
            return false;

        bool jumpBuffered = (Time.time - state.LastJumpPressedTime) <= jumpBuffer;
        if (IsVaultingNow || !jumpBuffered)
            return false;

        if (!state.IsGrounded || !contacts.HasSupport || contacts.SupportHit.collider == null)
            return false;

        if (externalMoveY > -oneWayDropDownInputThreshold)
            return false;

        OneWayBoxPlatform supportPlatform = OneWayPlatformUtility.ResolvePlatform(contacts.SupportHit.collider);
        if (supportPlatform == null || supportPlatform.PlatformCollider == null)
            return false;

        if (!oneWayPlatformController.TryStartDropDown(supportPlatform, oneWayDropDownDuration))
            return false;

        currentInput.JumpPressed = false;
        currentInput.JumpHeld = false;
        state.LastJumpPressedTime = InvalidPastTime;
        state.IsGrounded = false;
        state.WasGroundedLastFixed = false;
        state.PendingSlopeStickAfterJump = false;
        state.SlopeLockUntilTime = InvalidPastTime;

        if (onlyDoubleJumpAfterOneWayDropDown)
        {
            state.LastGroundedTime = InvalidPastTime;
            state.JumpBlockedUntilTime = InvalidPastTime;

            if (enableDoubleJump)
            {
                state.JumpsRemaining = 1;
                state.UsedDoubleJumpSinceLastGrounded = false;
                state.IsDoubleJumpSpeedBoostActive = false;
            }
            else
            {
                state.JumpsRemaining = 0;
            }
        }

        UpdateSlopeXConstraint(false);

        contacts.IsGrounded = false;
        contacts.HasSupport = false;
        contacts.HasGroundSurface = false;
        contacts.OnSlope = false;
        contacts.GroundNormal = Vector3.up;
        contacts.SupportHit = default;
        contacts.GroundHit = default;

        if (rb != null && !rb.isKinematic)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.y = Mathf.Min(velocity.y, -oneWayDropDownDownwardSpeed);
            rb.linearVelocity = velocity;
        }

        return true;
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
        SetMoveInput(x, externalMoveY);
    }

    public void SetMoveInput(float x, float y)
    {
        externalMoveX = Mathf.Clamp(x, -1f, 1f);
        externalMoveY = Mathf.Clamp(y, -1f, 1f);
    }

    public void SetJumpHeld(bool held)
    {
        externalJumpHeld = held;
    }

    public void SetLockStanceHeld(bool held)
    {
        externalLockStanceHeld = held;

        if (held)
            return;

        lockStanceLatched = false;
        lockStanceQueued = false;
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
        {
            externalMoveX = 0f;
            externalMoveY = 0f;
        }

        externalJumpHeld = false;
        externalJumpPressedQueued = false;
        externalJumpReleasedQueued = false;
        externalLockStanceHeld = false;
        lockStanceLatched = false;
        lockStanceQueued = false;
    }


    public void ClearLocomotionDrive()
    {
        inputX = 0f;
        smoothedInputX = 0f;
        currentHorizontalSpeedAbs = 0f;
        state.PendingSlopeStickAfterJump = false;
        state.SlopeLockUntilTime = InvalidPastTime;
        UpdateSlopeXConstraint(false);
    }

    public void SuppressHorizontalLocomotion(float duration, bool clearDrive = true)
    {
        if (clearDrive)
            ClearLocomotionDrive();

        if (duration <= 0f)
            return;

        externalHorizontalLocomotionSuppressUntilTime = Mathf.Max(
            externalHorizontalLocomotionSuppressUntilTime,
            Time.time + duration);
    }

    public void ClearHorizontalLocomotionSuppression()
    {
        externalHorizontalLocomotionSuppressUntilTime = InvalidPastTime;
    }

    private bool IsHorizontalLocomotionSuppressed()
    {
        return Time.time < externalHorizontalLocomotionSuppressUntilTime;
    }

    public void ResetMotionForExternalHit(bool clearInput = true, bool clearCurrentWallSlide = true)
    {
        if (clearInput)
            ClearExternalInputState(clearMove: true);

        ClearLocomotionDrive();
        ClearHorizontalLocomotionSuppression();

        if (clearCurrentWallSlide && state.IsWallSliding)
            ClearWallSlideState();

        state.WallDetachHoldTimer = 0f;

        if (rb != null && !rb.isKinematic)
            rb.linearVelocity = Vector3.zero;
    }

    public void SuppressWallSlide(float duration, bool clearCurrentWallSlide = true)
    {
        if (clearCurrentWallSlide && state.IsWallSliding)
            ClearWallSlideState();

        if (duration <= 0f)
            return;

        externalWallSlideSuppressUntilTime = Mathf.Max(
            externalWallSlideSuppressUntilTime,
            Time.time + duration);
    }

    public void ClearWallSlideSuppression()
    {
        externalWallSlideSuppressUntilTime = InvalidPastTime;
    }

    public void SuppressVaultStart(float duration)
    {
        if (duration <= 0f)
            return;

        externalVaultStartSuppressUntilTime = Mathf.Max(
            externalVaultStartSuppressUntilTime,
            Time.time + duration);
    }

    public void ClearVaultStartSuppression()
    {
        externalVaultStartSuppressUntilTime = InvalidPastTime;
    }

    private bool IsWallSlideSuppressed()
    {
        return Time.time < externalWallSlideSuppressUntilTime;
    }

    private bool IsVaultStartSuppressed()
    {
        return Time.time < externalVaultStartSuppressUntilTime;
    }

    private void ResolveLockStanceState()
    {
        bool wasLatched = lockStanceLatched;

        if (!enableLockStance)
        {
            lockStanceLatched = false;
            lockStanceQueued = false;
            return;
        }

        if (!externalLockStanceHeld)
        {
            lockStanceLatched = false;
            lockStanceQueued = false;
            return;
        }

        if (lockStanceLatched)
        {
            lockStanceQueued = false;
        }
        else if (state.IsGrounded)
        {
            lockStanceLatched = true;
            lockStanceQueued = false;
        }
        else
        {
            lockStanceQueued = queueLockStanceIfPressedInAir;
        }

        if (!wasLatched && lockStanceLatched)
        {
            smoothedInputX = 0f;
            currentHorizontalSpeedAbs = 0f;

            if (state.IsWallSliding)
                ClearWallSlideState();
        }
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
                RecordLandingEvent();
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
        ClearAirShotPhysicsAssistWindows();
    }

    private void RecordLandingEvent()
    {
        LastLandingStateVersion++;
        LastLandingTime = Time.time;
    }

    private bool IsLandingReverseAntiSkidActive(float rawInputX, Vector3 velocity, SurfaceContacts25D contacts, bool justLanded)
    {
        if (!reduceLandingReverseSkid)
            return false;
        if (!state.IsGrounded)
            return false;
        if (Mathf.Abs(rawInputX) <= InputEpsilon)
            return false;

        bool insideWindow = justLanded ||
            (LastLandingTime > InvalidPastTime && (Time.time - LastLandingTime) <= landingReverseNoSkidWindow);
        if (!insideWindow)
            return false;

        float planarSpeed = GetCurrentPlanarSpeed(velocity, contacts, justLanded);
        if (Mathf.Abs(planarSpeed) < landingReverseMinSpeed)
            return false;

        return Mathf.Sign(rawInputX) != Mathf.Sign(planarSpeed);
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
        if (IsLockStanceMovementActive || IsHorizontalLocomotionSuppressed())
        {
            smoothedInputX = 0f;
            return;
        }

        float desiredInput = inputX;
        bool landingReverse = IsLandingReverseAntiSkidActive(desiredInput, rb != null ? rb.linearVelocity : Vector3.zero, lastContacts, justLanded: false);

        if (landingReverse &&
            Mathf.Abs(smoothedInputX) > InputEpsilon &&
            Mathf.Sign(smoothedInputX) != Mathf.Sign(desiredInput))
        {
            smoothedInputX = 0f;
        }

        float rampSpeed = Mathf.Abs(desiredInput) > Mathf.Abs(smoothedInputX)
            ? inputAcceleration
            : inputDeceleration;

        bool directionChanged =
            Mathf.Abs(desiredInput) > InputEpsilon &&
            Mathf.Abs(smoothedInputX) > InputEpsilon &&
            Mathf.Sign(desiredInput) != Mathf.Sign(smoothedInputX);

        if (directionChanged)
            rampSpeed = landingReverse ? inputAcceleration : inputDeceleration;

        smoothedInputX = Mathf.MoveTowards(
            smoothedInputX,
            desiredInput,
            rampSpeed * Time.fixedDeltaTime);
    }

    private void UpdateGroundRunStopState(FrameInput25D input, SurfaceContacts25D contacts, bool justLandedThisFixed, float currentPlanarSpeed, bool noRawInput)
    {
        bool isAuthorizedSlopeSurface = contacts.OnSlope && contacts.IsSlopeSurfaceAuthorized;
        bool isFlatGround = state.IsGrounded && (!suppressRunStopOnAuthorizedSlope || !isAuthorizedSlopeSurface);
        bool isCrouching = crouchController != null && crouchController.IsCrouching;
        bool isControlLocked = controlLock != null && controlLock.IsControlLocked;
        bool suppressForYInput = requireNeutralMoveYForRunStop && Mathf.Abs(input.RawY) > runStopInputDeadzone;
        float planarSpeedAbs = Mathf.Abs(currentPlanarSpeed);
        float runQualificationSpeed = Mathf.Max(runStopEnterSpeed, GetEffectiveMoveSpeed() * runStopRunQualificationSpeedNormalized);

        bool isQualifiedGroundRun =
            isFlatGround &&
            !state.IsWallSliding &&
            !IsVaultingNow &&
            Mathf.Abs(input.RawX) > runStopInputDeadzone &&
            (!suppressRunStopWhileCrouching || !isCrouching) &&
            (!suppressRunStopWhileControlLocked || !isControlLocked) &&
            (!suppressRunStopWhileLockStance || !IsLockStanceMovementActive) &&
            planarSpeedAbs >= runQualificationSpeed;

        if (isQualifiedGroundRun)
            lastGroundRunTime = Time.time;

        bool hadRecentGroundRun =
            lastGroundRunTime > InvalidPastTime &&
            (Time.time - lastGroundRunTime) <= runStopRecentRunWindow;

        bool suppressedAfterLanding =
            justLandedThisFixed ||
            (LastLandingTime > InvalidPastTime && (Time.time - LastLandingTime) <= runStopSuppressAfterLandingWindow);

        bool canBeRunStop =
            enableRunStopState &&
            isFlatGround &&
            noRawInput &&
            !state.IsWallSliding &&
            !IsVaultingNow &&
            hadRecentGroundRun &&
            !suppressedAfterLanding &&
            (!suppressRunStopWhileCrouching || !isCrouching) &&
            (!suppressRunStopWhileControlLocked || !isControlLocked) &&
            (!suppressRunStopWhileLockStance || !IsLockStanceMovementActive) &&
            !suppressForYInput;

        if (canBeRunStop)
        {
            if (isRunStoppingNow)
                isRunStoppingNow = planarSpeedAbs > runStopExitSpeed;
            else
                isRunStoppingNow = planarSpeedAbs >= runStopEnterSpeed;
        }
        else
        {
            isRunStoppingNow = false;
        }

        runStopSpeedNormalized = isRunStoppingNow
            ? Mathf.Clamp01(Mathf.InverseLerp(runStopExitSpeed, Mathf.Max(runStopExitSpeed + DotEpsilon, GetEffectiveMoveSpeed()), planarSpeedAbs))
            : 0f;
    }

    private VelocityCommand25D BuildVelocityCommand(FrameInput25D input, SurfaceContacts25D contacts, bool justLandedThisFixed)
    {
        VelocityCommand25D command = default;
        command.TargetVelocity = rb.linearVelocity;

        bool lockStanceMovementActive = IsLockStanceMovementActive;

        if (lockStanceMovementActive)
        {
            input.RawX = 0f;
            input.SmoothedX = 0f;

            if (!allowJumpWhileLockStance)
            {
                input.JumpPressed = false;
                input.JumpHeld = false;
                input.JumpReleased = false;
            }
        }

        float currentPlanarSpeed = GetCurrentPlanarSpeed(command.TargetVelocity, contacts, justLandedThisFixed);

        if (IsLandingReverseAntiSkidActive(input.RawX, command.TargetVelocity, contacts, justLandedThisFixed))
            currentPlanarSpeed *= landingReverseCarryMultiplier;

        if (justLandedThisFixed)
        {
            TryStartSlopeLandingLock(contacts, currentPlanarSpeed);

            if (contacts.OnSlope && Mathf.Abs(input.RawX) <= InputEpsilon && IsSlopeLandingLockActive())
                currentPlanarSpeed = 0f;
        }

        bool noRawInput = Mathf.Abs(input.RawX) <= InputEpsilon;
        bool suppressHorizontalLocomotion = IsHorizontalLocomotionSuppressed();
        float targetPlanarSpeed = lockStanceMovementActive
            ? 0f
            : (suppressHorizontalLocomotion ? currentPlanarSpeed : BuildTargetHorizontalSpeed(input, contacts));
        ApplyAirShotHorizontalTargetConstraint(ref targetPlanarSpeed);
        float downhillSignedSpeed = contacts.OnSlope ? currentPlanarSpeed * contacts.DownhillSign : 0f;
        bool isSlopeSlidingNowThisFrame =
            state.IsGrounded &&
            contacts.OnSlope &&
            contacts.IsSlopeSurfaceAuthorized &&
            noRawInput &&
            !IsSlopeLandingLockActive() &&
            downhillSignedSpeed > downhillSlideMinSpeed;

        isSlopeSlidingNow = isSlopeSlidingNowThisFrame;
        slopeSlideSpeedNormalized = isSlopeSlidingNowThisFrame
            ? Mathf.InverseLerp(
                downhillSlideMinSpeed,
                Mathf.Max(downhillSlideMinSpeed + DotEpsilon, GetEffectiveMoveSpeed()),
                downhillSignedSpeed)
            : 0f;

        UpdateGroundRunStopState(input, contacts, justLandedThisFixed, currentPlanarSpeed, noRawInput);

        bool shouldLockSlopeX = !suppressHorizontalLocomotion && ShouldLockSlopeX(contacts, noRawInput, currentPlanarSpeed, downhillSignedSpeed) && !IsJumpBuffered();
        UpdateSlopeXConstraint(shouldLockSlopeX);

        float resolvedPlanarSpeed = lockStanceMovementActive
            ? 0f
            : (suppressHorizontalLocomotion
                ? currentPlanarSpeed
                : ResolvePlanarSpeed(
                    targetPlanarSpeed,
                    currentPlanarSpeed,
                    downhillSignedSpeed,
                    noRawInput,
                    contacts.BlockedLeft,
                    contacts.BlockedRight,
                    contacts.OnSlope));
        ApplyAirShotHorizontalResolvedDamping(ref resolvedPlanarSpeed);

        if (suppressHorizontalLocomotion)
        {
            command.TargetVelocity.x = rb.linearVelocity.x;
            command.OverrideX = true;
        }
        else
        {
            ApplyHorizontalIntent(ref command, contacts, resolvedPlanarSpeed, shouldLockSlopeX, noRawInput);
        }

        bool blockJumpByLockStance = lockStanceMovementActive && !allowJumpWhileLockStance;

        bool consumedWallJump = TryBuildWallJump(ref command);
        if (!consumedWallJump && !blockJumpByLockStance)
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
        bool wantsFastWallSlide =
            enableFastWallSlideOnDownInput &&
            !IsLockStanceHeld &&
            externalMoveY <= -fastWallSlideDownInputThreshold;

        if (newY > 0f)
        {
            if (allowWallSlideUpwardMomentum)
                newY = Mathf.MoveTowards(newY, 0f, wallUpwardDeceleration * Time.fixedDeltaTime);
            else
                newY = 0f;
        }

        if (wantsFastWallSlide)
            newY = Mathf.MoveTowards(newY, -fastWallSlideSpeed, fastWallSlideAcceleration * Time.fixedDeltaTime);

        float targetWallSlideSpeed = wantsFastWallSlide ? fastWallSlideSpeed : wallSlideSpeed;
        if (newY < -targetWallSlideSpeed)
            newY = -targetWallSlideSpeed;

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

        // После wall jump fallback-facing для стрельбы/aim и визуальный facing
        // должны смотреть в сторону отталкивания, а не откатываться к pre-wall facing.
        int wallJumpFacingSign = jumpDirectionX >= 0f ? 1 : -1;
        lastNonZeroInputX = wallJumpFacingSign;
        if (facingResolver != null)
            facingResolver.SetBaseFacingSign(wallJumpFacingSign);

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

        LockWallReattachForSide(jumpedFromWallSide);

        ClearWallSlideState();

        // Если wall jump выполнен во время удержания LockStance, прыжок от стены
        // должен оставаться стандартным: не гасим его horizontal push.
        // Поэтому снимаем latched и, если кнопка все еще удерживается, переводим
        // стойку в queued, чтобы она вернулась уже после приземления.
        lockStanceLatched = false;
        lockStanceQueued = enableLockStance && externalLockStanceHeld && queueLockStanceIfPressedInAir;

        state.LastJumpPressedTime = InvalidPastTime;
        state.LastJumpExecutedTime = Time.time;
        RecordSelfJump(SelfJumpKind.WallJump);
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

        if (debugJumpMessages)
        {
            if (isDoubleJump)
                Debug.Log($"[RBCharacter25D] Double Jump executed by {name}", this);
            else
                Debug.Log($"[RBCharacter25D] Single Jump executed by {name}", this);
        }

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
        float baseGravityScale = autoTuneJump
            ? runtimeGravityScale
            : Mathf.Max(0f, manualGravityScale);

        return baseGravityScale * GetAirShotGravityMultiplier();
    }

    private void ApplyExtraGravity()
    {
        float gravityScale = GetEffectiveGravityScale();
        float extraScale = gravityScale - 1f;

        if (Mathf.Abs(extraScale) < 1e-4f)
            return;

        rb.AddForce(Physics.gravity * extraScale, ForceMode.Acceleration);
    }

    private float GetAirShotGravityMultiplier()
    {
        if (!useAirShotPhysicsAssist || rb == null || rb.isKinematic)
            return 1f;
        if (state.IsGrounded || state.IsWallSliding || IsVaultingNow)
            return 1f;

        if (IsAirShotFastFallActive())
            return Mathf.Lerp(1f, airShotFastFallGravityMultiplier, Mathf.Clamp01(airShotFastFallWeight));

        if (IsAirShotSlowFallActive() && rb.linearVelocity.y < -SpeedEpsilon)
            return Mathf.Lerp(1f, airShotSlowFallGravityMultiplier, Mathf.Clamp01(airShotSlowFallWeight));

        return 1f;
    }

    private void ApplyAirShotVelocityAssist()
    {
        if (!useAirShotPhysicsAssist || rb == null || rb.isKinematic)
            return;

        if (state.IsGrounded || state.IsWallSliding || IsVaultingNow || IsCrouchingNow())
        {
            ClearAirShotPhysicsAssistWindows();
            return;
        }

        if (!IsAirShotSlowFallActive())
            return;

        Vector3 velocity = rb.linearVelocity;
        float maxDownSpeed = Mathf.Max(0f, airShotSlowFallMaxDownSpeed);
        if (maxDownSpeed <= 0f)
            return;

        float slowFallWeight = Mathf.Clamp01(airShotSlowFallWeight);
        float unrestrictedDownSpeed = Mathf.Max(maxDownSpeed, 20f);
        float weightedMaxDownSpeed = Mathf.Lerp(unrestrictedDownSpeed, maxDownSpeed, slowFallWeight);
        if (velocity.y < -weightedMaxDownSpeed)
        {
            velocity.y = -weightedMaxDownSpeed;
            rb.linearVelocity = velocity;
        }
    }

    private bool IsAirShotSlowFallActive()
    {
        return airShotSlowFallUntilTime > InvalidPastTime && Time.time < airShotSlowFallUntilTime;
    }

    private bool IsAirShotFastFallActive()
    {
        return airShotFastFallUntilTime > InvalidPastTime && Time.time < airShotFastFallUntilTime;
    }

    private bool IsAirShotHorizontalRecoilActive()
    {
        return airShotHorizontalRecoilUntilTime > InvalidPastTime && Time.time < airShotHorizontalRecoilUntilTime;
    }

    private bool IsCrouchingNow()
    {
        return crouchController != null && crouchController.IsCrouching;
    }

    private void ClearAirShotPhysicsAssistWindows()
    {
        airShotSlowFallUntilTime = InvalidPastTime;
        airShotFastFallUntilTime = InvalidPastTime;
        airShotHorizontalRecoilUntilTime = InvalidPastTime;
        airShotSlowFallWeight = 0f;
        airShotFastFallWeight = 0f;
        airShotHorizontalRecoilWeight = 0f;
        airShotHorizontalShotSign = 0;
    }

    private bool TryGetActiveAirShotHorizontalRecoil(out int shotSign, out float weight)
    {
        shotSign = 0;
        weight = 0f;

        if (!useAirShotPhysicsAssist || rb == null || rb.isKinematic)
            return false;
        if (state.IsGrounded || state.IsWallSliding || IsVaultingNow || IsCrouchingNow())
            return false;
        if (!IsAirShotHorizontalRecoilActive())
            return false;
        if (airShotHorizontalShotSign == 0)
            return false;

        shotSign = airShotHorizontalShotSign;
        weight = Mathf.Clamp01(airShotHorizontalRecoilWeight);
        return weight > 0f;
    }

    private void ApplyAirShotHorizontalTargetConstraint(ref float targetPlanarSpeed)
    {
        if (!TryGetActiveAirShotHorizontalRecoil(out int shotSign, out float weight))
            return;
        if (Mathf.Abs(targetPlanarSpeed) <= SpeedEpsilon || Mathf.Sign(targetPlanarSpeed) != shotSign)
            return;

        float maxTowardShotSpeed = Mathf.Lerp(GetEffectiveMoveSpeed(), airShotHorizontalRecoilMaxSpeedTowardShot, weight);
        maxTowardShotSpeed = Mathf.Max(0f, maxTowardShotSpeed);

        if (shotSign > 0)
            targetPlanarSpeed = Mathf.Min(targetPlanarSpeed, maxTowardShotSpeed);
        else
            targetPlanarSpeed = Mathf.Max(targetPlanarSpeed, -maxTowardShotSpeed);
    }

    private void ApplyAirShotHorizontalResolvedDamping(ref float resolvedPlanarSpeed)
    {
        if (!TryGetActiveAirShotHorizontalRecoil(out int shotSign, out float weight))
            return;
        if (Mathf.Abs(resolvedPlanarSpeed) <= SpeedEpsilon || Mathf.Sign(resolvedPlanarSpeed) != shotSign)
            return;

        float horizontalDeceleration = airShotHorizontalRecoilDeceleration * weight;
        if (horizontalDeceleration > 0f)
            resolvedPlanarSpeed = Mathf.MoveTowards(resolvedPlanarSpeed, 0f, horizontalDeceleration * Time.fixedDeltaTime);

        float maxTowardShotSpeed = Mathf.Lerp(GetEffectiveMoveSpeed(), airShotHorizontalRecoilMaxSpeedTowardShot, weight);
        maxTowardShotSpeed = Mathf.Max(0f, maxTowardShotSpeed);

        if (shotSign > 0)
            resolvedPlanarSpeed = Mathf.Min(resolvedPlanarSpeed, maxTowardShotSpeed);
        else
            resolvedPlanarSpeed = Mathf.Max(resolvedPlanarSpeed, -maxTowardShotSpeed);
    }

    public void NotifyAirShotPhysics(Vector3 shotDirection)
    {
        if (!useAirShotPhysicsAssist || rb == null || rb.isKinematic)
            return;

        if (state.IsGrounded || state.IsWallSliding || IsVaultingNow || IsCrouchingNow())
        {
            if (IsCrouchingNow())
                ClearAirShotPhysicsAssistWindows();
            return;
        }

        shotDirection.z = 0f;
        if (shotDirection.sqrMagnitude <= 0.0001f)
            shotDirection = new Vector3(VaultFacingSignFromInput, 0f, 0f);

        Vector3 axisDirection = normalizeAirShotRecoilAxes
            ? shotDirection.normalized
            : new Vector3(Mathf.Clamp(shotDirection.x, -1f, 1f), Mathf.Clamp(shotDirection.y, -1f, 1f), 0f);

        Vector3 velocity = rb.linearVelocity;
        float upwardWeight = axisDirection.y > airShotUpwardThreshold
            ? Mathf.InverseLerp(airShotUpwardThreshold, 1f, axisDirection.y)
            : 0f;
        float downwardWeight = axisDirection.y < -airShotDownwardThreshold
            ? Mathf.InverseLerp(airShotDownwardThreshold, 1f, -axisDirection.y)
            : 0f;
        float horizontalWeight = Mathf.Abs(axisDirection.x) > airShotHorizontalThreshold
            ? Mathf.InverseLerp(airShotHorizontalThreshold, 1f, Mathf.Abs(axisDirection.x))
            : 0f;

        if (upwardWeight > 0f)
        {
            airShotSlowFallUntilTime = InvalidPastTime;
            airShotSlowFallWeight = 0f;
            airShotFastFallUntilTime = airShotFastFallDuration > 0f
                ? Time.time + airShotFastFallDuration
                : InvalidPastTime;
            airShotFastFallWeight = upwardWeight;

            if (velocity.y > 0f)
            {
                float weightedRiseCutMultiplier = Mathf.Lerp(1f, airShotUpwardRiseCutMultiplier, upwardWeight);
                velocity.y *= weightedRiseCutMultiplier;
                rb.linearVelocity = velocity;
            }
        }
        else
        {
            airShotFastFallUntilTime = InvalidPastTime;
            airShotFastFallWeight = 0f;

            if (airShotSlowFallDuration > 0f && downwardWeight > 0f)
            {
                airShotSlowFallUntilTime = Time.time + airShotSlowFallDuration;
                airShotSlowFallWeight = downwardWeight;
            }
            else
            {
                airShotSlowFallUntilTime = InvalidPastTime;
                airShotSlowFallWeight = 0f;
            }
        }

        if (airShotHorizontalRecoilDuration > 0f && horizontalWeight > 0f)
        {
            airShotHorizontalRecoilUntilTime = Time.time + airShotHorizontalRecoilDuration;
            airShotHorizontalRecoilWeight = horizontalWeight;
            airShotHorizontalShotSign = axisDirection.x >= 0f ? WallSideRight : WallSideLeft;
        }
        else
        {
            airShotHorizontalRecoilUntilTime = InvalidPastTime;
            airShotHorizontalRecoilWeight = 0f;
            airShotHorizontalShotSign = 0;
        }
    }

    private float GetTakeoffSpeedFromImpulse(float impulse)
    {
        float mass = rb != null ? Mathf.Max(DotEpsilon, rb.mass) : 1f;
        return impulse / mass;
    }

    private void UpdateWallSlideState(SurfaceContacts25D contacts)
    {
        if (IsVaultingNow || IsLockStanceGroundActive || !enableWallSlide || state.IsGrounded || IsWallSlideSuppressed())
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
                DetachFromCurrentWallByOppositeInput();
                return;
            }

            return;
        }

        TryStartWallLatch(contacts);
    }

    private void TryStartWallLatch(SurfaceContacts25D contacts)
    {
        bool canLatchLeft = contacts.WallInteractableLeft && CanLatchToWallSide(WallSideLeft);
        bool canLatchRight = contacts.WallInteractableRight && CanLatchToWallSide(WallSideRight);

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
        float threshold = Mathf.Max(InputEpsilon, wallDetachOppositeInputThreshold);
        float rawMoveX = externalMoveX;

        if (state.WallSlideSide == WallSideLeft)
            return rawMoveX >= threshold;
        if (state.WallSlideSide == WallSideRight)
            return rawMoveX <= -threshold;
        return false;
    }

    private bool ShouldDetachFromCurrentWallByHold()
    {
        if (!detachFromWallOnOppositeInput)
        {
            state.WallDetachHoldTimer = 0f;
            return false;
        }

        // Во время wall slide удержание LockStance отключает только detach
        // по противоположному horizontal input. Сам wall slide и wall jump
        // продолжают работать как обычно.
        if (state.IsWallSliding && IsLockStanceHeld)
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

    private void DetachFromCurrentWallByOppositeInput()
    {
        int detachedSide = state.WallSlideSide;
        LockWallReattachForSide(detachedSide);
        ClearWallSlideState();
    }

    private void LockWallReattachForSide(int side)
    {
        if (!enableWallReattachCooldown || side == WallSideNone)
        {
            state.WallReattachLockedSide = WallSideNone;
            state.WallReattachLockUntilTime = InvalidPastTime;
            return;
        }

        state.WallReattachLockedSide = side;
        state.WallReattachLockUntilTime = Time.time + wallReattachCooldown;
    }

    private bool IsTouchingWallSide(SurfaceContacts25D contacts, int side)
    {
        if (side == WallSideLeft)
            return contacts.WallInteractableLeft;
        if (side == WallSideRight)
            return contacts.WallInteractableRight;
        return false;
    }

    private void SetWallSlideState(int side)
    {
        state.IsWallSliding = true;
        state.WallSlideSide = side;
        state.WallDetachHoldTimer = 0f;

        if (facingResolver != null)
            facingResolver.SetWallSlideOverride(side < 0 ? +1 : -1);
    }

    private void ClearWallSlideState()
    {
        bool wasWallSliding = state.IsWallSliding;

        state.IsWallSliding = false;
        state.WallSlideSide = WallSideNone;
        state.WallDetachHoldTimer = 0f;

        if (facingResolver != null)
            facingResolver.ClearWallSlideOverride();

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
        float preVaultVelocityX = rb != null ? rb.linearVelocity.x : 0f;
        NotifyVaultStarted(preVaultVelocityX, VaultFacingSignFromInput);
    }

    public void NotifyVaultStarted(float preVaultVelocityX)
    {
        NotifyVaultStarted(preVaultVelocityX, VaultFacingSignFromInput);
    }

    public void NotifyVaultStarted(float preVaultVelocityX, int vaultFacingSign)
    {
        state.PendingSlopeStickAfterJump = false;
        state.SlopeLockUntilTime = InvalidPastTime;
        state.JumpBlockedUntilTime = InvalidPastTime;
        state.LastJumpPressedTime = InvalidPastTime;
        smoothedInputX = 0f;
        currentHorizontalSpeedAbs = 0f;
        isRunStoppingNow = false;
        runStopSpeedNormalized = 0f;

        hasPendingVaultExitVelocityRestore =
            preserveHorizontalSpeedAfterVault &&
            Mathf.Abs(preVaultVelocityX) >= vaultExitMinRestoreSpeed;
        pendingVaultExitVelocityX = hasPendingVaultExitVelocityRestore
            ? preVaultVelocityX * vaultExitHorizontalSpeedMultiplier
            : 0f;
        vaultExitVelocityRestoreReadyTime = InvalidPastTime;

        if (facingResolver != null)
            facingResolver.SetBaseFacingSign(vaultFacingSign);

        ClearWallSlideState();

        if (facingResolver != null)
            facingResolver.SetVaultOverride(vaultFacingSign);

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
        NotifyVaultFinished(0f);
    }

    public void NotifyVaultFinished(float velocityRestoreDelay)
    {
        state.PendingSlopeStickAfterJump = false;
        state.SlopeLockUntilTime = InvalidPastTime;
        state.JumpBlockedUntilTime = InvalidPastTime;
        state.LastGroundedTime = Time.time;

        ResetJumpCounter();
        ClearDoubleJumpRuntimeState();
        ClearLastSelfJumpState();
        ClearWallSlideState();

        if (facingResolver != null)
            facingResolver.ClearVaultOverride();

        UpdateSlopeXConstraint(false);

        bool shouldRestoreVelocity = hasPendingVaultExitVelocityRestore;

        if (shouldRestoreVelocity && requireHeldDirectionForVaultSpeedRestore)
        {
            shouldRestoreVelocity =
                Mathf.Abs(externalMoveX) > InputEpsilon &&
                Mathf.Abs(pendingVaultExitVelocityX) > vaultExitMinRestoreSpeed &&
                Mathf.Sign(externalMoveX) == Mathf.Sign(pendingVaultExitVelocityX);
        }

        if (!shouldRestoreVelocity)
        {
            pendingVaultExitVelocityX = 0f;
            hasPendingVaultExitVelocityRestore = false;
            vaultExitVelocityRestoreReadyTime = InvalidPastTime;
            smoothedInputX = 0f;
            currentHorizontalSpeedAbs = 0f;
            isRunStoppingNow = false;
            runStopSpeedNormalized = 0f;
            LastVaultFinishedTime = Time.time;
            return;
        }

        float delay = Mathf.Max(0f, velocityRestoreDelay);
        if (delay <= 0f)
        {
            ApplyPendingVaultExitVelocityRestore(force: true);
            LastVaultFinishedTime = Time.time;
            return;
        }

        SuppressHorizontalLocomotion(delay, clearDrive: true);
        vaultExitVelocityRestoreReadyTime = Time.time + delay;

        if (rb != null && !rb.isKinematic)
        {
            Vector3 velocity = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, velocity.y, lockZ ? 0f : velocity.z);
        }

        LastVaultFinishedTime = Time.time;
    }

    private void TryApplyPendingVaultExitVelocityRestore()
    {
        if (!hasPendingVaultExitVelocityRestore)
            return;
        if (vaultExitVelocityRestoreReadyTime <= InvalidPastTime)
            return;
        if (Time.time < vaultExitVelocityRestoreReadyTime)
            return;

        ApplyPendingVaultExitVelocityRestore(force: false);
    }

    private void ApplyPendingVaultExitVelocityRestore(bool force)
    {
        if (!hasPendingVaultExitVelocityRestore)
            return;

        bool shouldRestoreVelocity = true;
        if (!force && requireHeldDirectionForVaultSpeedRestore)
        {
            shouldRestoreVelocity =
                Mathf.Abs(externalMoveX) > InputEpsilon &&
                Mathf.Abs(pendingVaultExitVelocityX) > vaultExitMinRestoreSpeed &&
                Mathf.Sign(externalMoveX) == Mathf.Sign(pendingVaultExitVelocityX);
        }

        if (shouldRestoreVelocity)
        {
            float restoredVelocityX = pendingVaultExitVelocityX;
            float effectiveMoveSpeed = Mathf.Max(DotEpsilon, GetEffectiveMoveSpeed());
            smoothedInputX = Mathf.Clamp(restoredVelocityX / effectiveMoveSpeed, -1f, 1f);
            currentHorizontalSpeedAbs = Mathf.Abs(restoredVelocityX);

            if (rb != null && !rb.isKinematic)
            {
                Vector3 velocity = rb.linearVelocity;
                rb.linearVelocity = new Vector3(
                    restoredVelocityX,
                    0f,
                    lockZ ? 0f : velocity.z);
            }
        }
        else
        {
            smoothedInputX = 0f;
            currentHorizontalSpeedAbs = 0f;
            isRunStoppingNow = false;
            runStopSpeedNormalized = 0f;
        }

        pendingVaultExitVelocityX = 0f;
        hasPendingVaultExitVelocityRestore = false;
        vaultExitVelocityRestoreReadyTime = InvalidPastTime;
    }

    private bool IsSlopeHandlingRuntimeActive(SurfaceContacts25D contacts)
    {
        return
            enableSlopeHandling &&
            contacts.HasGroundSurface &&
            contacts.IsSlopeSurfaceAuthorized &&
            contacts.OnSlope;
    }

    private void DebugSlopeHandlingState(SurfaceContacts25D contacts)
    {
        if (!debugSlopeHandlingState)
            return;

        bool masterEnabled = enableSlopeHandling;
        bool authorized = contacts.HasGroundSurface && contacts.IsSlopeSurfaceAuthorized;
        bool runtimeActive = IsSlopeHandlingRuntimeActive(contacts);
        bool grounded = state.IsGrounded;
        int groundLayer = contacts.GroundHit.collider != null ? contacts.GroundHit.collider.gameObject.layer : -1;

        if (masterEnabled == lastSlopeDebugMasterEnabled &&
            authorized == lastSlopeDebugAuthorized &&
            runtimeActive == lastSlopeDebugRuntimeActive &&
            grounded == lastSlopeDebugGrounded &&
            groundLayer == lastSlopeDebugGroundLayer)
        {
            return;
        }

        lastSlopeDebugMasterEnabled = masterEnabled;
        lastSlopeDebugAuthorized = authorized;
        lastSlopeDebugRuntimeActive = runtimeActive;
        lastSlopeDebugGrounded = grounded;
        lastSlopeDebugGroundLayer = groundLayer;

        string groundLayerName = groundLayer >= 0 ? LayerMask.LayerToName(groundLayer) : "None";
        string groundColliderName = contacts.GroundHit.collider != null ? contacts.GroundHit.collider.name : "null";

        Debug.Log(
            $"[RBCharacter25D:Slope] master={masterEnabled} grounded={grounded} hasGround={contacts.HasGroundSurface} authorized={authorized} onSlope={contacts.OnSlope} runtimeActive={runtimeActive} groundCollider={groundColliderName} groundLayer={groundLayerName}",
            this);
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

        string supportName = contacts.SupportHit.collider != null ? contacts.SupportHit.collider.name : "null";
        string leftWallName = contacts.LeftWallHit.collider != null ? contacts.LeftWallHit.collider.name : "null";
        string rightWallName = contacts.RightWallHit.collider != null ? contacts.RightWallHit.collider.name : "null";

        OneWayPlatformRuntimePhase supportPhase = OneWayPlatformRuntimePhase.Unknown;
        OneWayPlatformRuntimePhase leftWallPhase = OneWayPlatformRuntimePhase.Unknown;
        OneWayPlatformRuntimePhase rightWallPhase = OneWayPlatformRuntimePhase.Unknown;

        if (oneWayPlatformController != null)
        {
            OneWayBoxPlatform supportPlatform = contacts.SupportHit.collider != null
                ? OneWayPlatformUtility.ResolvePlatform(contacts.SupportHit.collider)
                : null;
            if (supportPlatform != null)
                oneWayPlatformController.TryGetPlatformPhase(supportPlatform, out supportPhase);

            OneWayBoxPlatform leftWallPlatform = contacts.LeftWallHit.collider != null
                ? OneWayPlatformUtility.ResolvePlatform(contacts.LeftWallHit.collider)
                : null;
            if (leftWallPlatform != null)
                oneWayPlatformController.TryGetPlatformPhase(leftWallPlatform, out leftWallPhase);

            OneWayBoxPlatform rightWallPlatform = contacts.RightWallHit.collider != null
                ? OneWayPlatformUtility.ResolvePlatform(contacts.RightWallHit.collider)
                : null;
            if (rightWallPlatform != null)
                oneWayPlatformController.TryGetPlatformPhase(rightWallPlatform, out rightWallPhase);
        }

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
            $"blockedR={blockedRight} " +
            $"lockStance={IsLockStanceMovementActive} " +
            $"support={supportName} " +
            $"supportPhase={supportPhase} " +
            $"leftWall={leftWallName} " +
            $"leftWallPhase={leftWallPhase} " +
            $"rightWall={rightWallName} " +
            $"rightWallPhase={rightWallPhase}",
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
