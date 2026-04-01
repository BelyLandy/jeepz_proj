using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(CollisionIgnoreCoordinator))]
public sealed class OneWayPlatformController : MonoBehaviour
{
    public static OneWayPlatformController DebugActiveController { get; private set; }

    [Header("Lifecycle")]
    [Tooltip("Включает новый controller как основной оркестратор one-way runtime-state.")]
    [SerializeField] private bool controllerEnabled = true;

    [Tooltip("Если включено, controller реально управляет pass-through через CollisionIgnoreCoordinator с причиной UpwardCross.")]
    [SerializeField] private bool drivePhysicsFromRuntimeState = true;

    [Header("Search")]
    [Tooltip("Слой, на котором лежат one-way платформы для нового controller.")]
    [SerializeField] private LayerMask oneWayPlatformMask;

    [Tooltip("Насколько расширять поиск платформ по X вокруг героя.")]
    [SerializeField] private float searchPaddingX = 0.2f;

    [Tooltip("Насколько расширять поиск платформ по Y вокруг героя.")]
    [SerializeField] private float searchPaddingY = 0.6f;

    [Tooltip("Насколько расширять поиск платформ по Z вокруг героя.")]
    [SerializeField] private float searchPaddingZ = 0.2f;

    [Tooltip("Если nearby-платформ в буфере оказалось слишком много, будет предупреждение в консоль.")]
    [SerializeField] private bool warnIfHitBufferIsFull = true;

    [Header("Crossing Logic")]
    [Tooltip("Минимальная вертикальная скорость, чтобы считать вход в one-way платформу настоящим проходом снизу вверх.")]
    [SerializeField] private float minUpwardSpeedToEnterPassing = 0.01f;

    [Tooltip("Пока вертикальная скорость выше этого значения, controller удерживает платформу в состоянии PassingUp даже если bottomY уже над зоной solidify.")]
    [SerializeField] private float maxUpwardSpeedToKeepPassing = 0.1f;

    [Tooltip("Небольшой запас по Y для сравнения входа/выхода из зон around top-plane платформы.")]
    [SerializeField] private float bottomYEpsilon = 0.005f;

    [Tooltip("Небольшой запас по top-plane платформы при определении состояния снизу на прошлом fixed-кадре.")]
    [SerializeField] private float previousBelowTopMargin = 0.005f;

    [Header("Drop Down")]
    [Tooltip("Разрешает временно переводить текущую support one-way платформу в ghost по явной команде drop-down.")]
    [SerializeField] private bool enableDropDown = true;

    [Tooltip("Минимальная длительность подавления one-way платформы при drop-down.")]
    [SerializeField] private float dropDownDuration = 0.18f;

    [Tooltip("Насколько низ капсулы может быть ниже top-plane платформы и всё ещё считаться корректным Supported. Нужен, чтобы убрать ложный recapture support в полупроваленном состоянии.")]
    [SerializeField] private float supportBottomTolerance = 0.02f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugPreview = true;
    [SerializeField] private bool drawDebugWhileSelected = true;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private RBCharacter25D character;
    private CollisionIgnoreCoordinator ignoreCoordinator;

    private readonly Collider[] overlapHits = new Collider[32];
    private readonly List<OneWayBoxPlatform> nearbyPlatforms = new List<OneWayBoxPlatform>(16);
    private readonly Dictionary<OneWayBoxPlatform, OneWayPlatformRuntimeState> runtimeStates = new Dictionary<OneWayBoxPlatform, OneWayPlatformRuntimeState>(16);
    private readonly List<OneWayBoxPlatform> runtimeStateBuffer = new List<OneWayBoxPlatform>(16);

    private OneWayActorSnapshot currentActorSnapshot;
    private OneWayActorSnapshot previousActorSnapshot;
    private bool hasPreviousActorSnapshot;
    private OneWaySupportInfo currentSupportInfo;

    public bool ControllerEnabled => controllerEnabled;
    public bool DrivePhysicsFromRuntimeState => drivePhysicsFromRuntimeState;
    public int RuntimeStateCount => runtimeStates.Count;
    public Rigidbody RigidbodyComponent => rb != null ? rb : (rb = GetComponent<Rigidbody>());
    public CapsuleCollider CapsuleColliderComponent => capsule != null ? capsule : (capsule = GetComponent<CapsuleCollider>());
    public RBCharacter25D CharacterComponent => character != null ? character : (character = GetComponent<RBCharacter25D>());
    public CollisionIgnoreCoordinator IgnoreCoordinator => ignoreCoordinator != null ? ignoreCoordinator : (ignoreCoordinator = GetComponent<CollisionIgnoreCoordinator>());

    private void Awake()
    {
        CacheComponents();
        ClampSettings();
        TryRegisterAsDebugController();
    }

    private void OnEnable()
    {
        TryRegisterAsDebugController();
    }

    private void OnValidate()
    {
        ClampSettings();
        CacheComponents();
    }

    private void FixedUpdate()
    {
        if (!controllerEnabled)
        {
            ReleaseDrivenReasons();
            ClearRuntimeState();
            return;
        }

        if (!EnsureRuntimeState())
            return;

        currentActorSnapshot = OneWayActorSnapshot.Capture(capsule, rb);
        currentSupportInfo = CaptureSupportInfo();

        CollectNearbyPlatforms();
        UpdateRuntimeStates();
        ApplyRuntimeReasonsToCoordinator();
        StoreCurrentActorSnapshot();
        DrawRuntimeDebug();
    }

    private void OnDisable()
    {
        if (DebugActiveController == this)
            DebugActiveController = null;

        ReleaseDrivenReasons();
        ClearRuntimeState();
    }

    private void OnDestroy()
    {
        if (DebugActiveController == this)
            DebugActiveController = null;

        ReleaseDrivenReasons();
        ClearRuntimeState();
    }

    public bool TryGetRuntimeState(OneWayBoxPlatform platform, out OneWayPlatformRuntimeState state)
    {
        if (platform == null)
        {
            state = null;
            return false;
        }

        return runtimeStates.TryGetValue(platform, out state);
    }

    public void GetRuntimeStates(List<OneWayPlatformRuntimeState> results)
    {
        if (results == null)
            return;

        results.Clear();
        foreach (OneWayPlatformRuntimeState state in runtimeStates.Values)
            results.Add(state);
    }

    public bool TryGetPlatformPhase(OneWayBoxPlatform platform, out OneWayPlatformRuntimePhase phase)
    {
        phase = OneWayPlatformRuntimePhase.Unknown;
        if (!TryGetRuntimeState(platform, out OneWayPlatformRuntimeState state) || state == null)
            return false;

        phase = state.Phase;
        return true;
    }

    public bool ShouldAcceptSensorHit(RaycastHit hit, SurfaceSensorQuery25D queryType)
    {
        if (hit.collider == null)
            return false;

        OneWayBoxPlatform platform = OneWayPlatformUtility.ResolvePlatform(hit.collider);
        if (platform == null)
            return true;

        // One-way platform никогда не должна считаться стеной для wall slide / wall block логики.
        if (queryType == SurfaceSensorQuery25D.Wall)
            return false;

        return !IsPlatformSuppressedForSensor(platform, queryType);
    }

    public bool IsColliderSuppressedForSensor(Collider collider)
    {
        OneWayBoxPlatform platform = OneWayPlatformUtility.ResolvePlatform(collider);
        return platform != null && IsPlatformSuppressedForSensor(platform, SurfaceSensorQuery25D.Support);
    }

    public bool TryStartDropDown(OneWayBoxPlatform platform, float durationOverride = -1f)
    {
        if (!controllerEnabled || !enableDropDown || platform == null || platform.PlatformCollider == null)
            return false;

        OneWayPlatformRuntimeState state = GetOrCreateRuntimeState(platform);
        state.Platform = platform;
        state.CapturePlatformSnapshot();

        float duration = durationOverride > 0f ? durationOverride : dropDownDuration;
        bool wasIgnored = state.FinalIgnoreRequested;
        state.StartDropDown(Time.time, duration);
        state.Phase = OneWayPlatformRuntimePhase.SuppressedByDropDown;
        state.FinalIgnoreRequested = true;

        if (!wasIgnored)
            state.LastBecameIgnoredTime = Time.fixedTime;

        CollisionIgnoreCoordinator coordinator = IgnoreCoordinator;
        if (coordinator != null)
            coordinator.SetReason(platform.PlatformCollider, OneWayPassThroughReason.DropDown, drivePhysicsFromRuntimeState);

        return true;
    }

    private bool EnsureRuntimeState()
    {
        CacheComponents();
        TryRegisterAsDebugController();
        return rb != null && capsule != null;
    }

    private bool IsPlatformSuppressedForSensor(OneWayBoxPlatform platform, SurfaceSensorQuery25D queryType)
    {
        if (platform == null || platform.PlatformCollider == null)
            return false;

        CollisionIgnoreCoordinator coordinator = IgnoreCoordinator;
        if (coordinator != null && coordinator.IsIgnored(platform.PlatformCollider))
            return true;

        if (!runtimeStates.TryGetValue(platform, out OneWayPlatformRuntimeState state) || state == null)
            return false;

        if (queryType == SurfaceSensorQuery25D.Support || queryType == SurfaceSensorQuery25D.GroundSurface)
            return !IsPlatformValidForSupportSensor(state, platform);

        if (state.IsCurrentSupport)
            return false;

        if (state.FinalIgnoreRequested)
            return true;

        if (state.HasReason(OneWayPassThroughReason.UpwardCross)
            || state.HasReason(OneWayPassThroughReason.Vault)
            || state.HasReason(OneWayPassThroughReason.DropDown)
            || state.HasReason(OneWayPassThroughReason.ExternalOverride)
            || state.HasReason(OneWayPassThroughReason.LegacyResolver))
        {
            return true;
        }

        return false;
    }

    private bool IsPlatformValidForSupportSensor(OneWayPlatformRuntimeState state, OneWayBoxPlatform platform)
    {
        if (state == null || platform == null)
            return false;

        if (state.IsCurrentSupport)
            return true;

        if (state.FinalIgnoreRequested)
            return false;

        if (state.HasReason(OneWayPassThroughReason.UpwardCross)
            || state.HasReason(OneWayPassThroughReason.Vault)
            || state.HasReason(OneWayPassThroughReason.DropDown)
            || state.HasReason(OneWayPassThroughReason.ExternalOverride)
            || state.HasReason(OneWayPassThroughReason.LegacyResolver))
        {
            return false;
        }

        if (state.Phase == OneWayPlatformRuntimePhase.CandidateBelow
            || state.Phase == OneWayPlatformRuntimePhase.PassingUp
            || state.Phase == OneWayPlatformRuntimePhase.SuppressedByDropDown
            || state.Phase == OneWayPlatformRuntimePhase.SuppressedByVault)
        {
            return false;
        }

        bool bottomIsAboveTopPlane = state.ActorBottomY >= platform.TopY - supportBottomTolerance;
        bool notApproachingFromBelow = !state.IsBelowPassThroughThreshold && !state.WasBelowTopLastFixed;
        return bottomIsAboveTopPlane && notApproachingFromBelow;
    }

    private void TryRegisterAsDebugController()
    {
        if (!controllerEnabled)
        {
            if (DebugActiveController == this)
                DebugActiveController = null;
            return;
        }

        if (DebugActiveController == null || DebugActiveController == this)
            DebugActiveController = this;
    }

    private void CacheComponents()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (capsule == null)
            capsule = GetComponent<CapsuleCollider>();

        if (character == null)
            character = GetComponent<RBCharacter25D>();

        if (ignoreCoordinator == null)
            ignoreCoordinator = GetComponent<CollisionIgnoreCoordinator>();
    }

    private void ClampSettings()
    {
        searchPaddingX = Mathf.Max(0f, searchPaddingX);
        searchPaddingY = Mathf.Max(0f, searchPaddingY);
        searchPaddingZ = Mathf.Max(0f, searchPaddingZ);
        minUpwardSpeedToEnterPassing = Mathf.Max(0f, minUpwardSpeedToEnterPassing);
        maxUpwardSpeedToKeepPassing = Mathf.Max(0f, maxUpwardSpeedToKeepPassing);
        bottomYEpsilon = Mathf.Max(0f, bottomYEpsilon);
        previousBelowTopMargin = Mathf.Max(0f, previousBelowTopMargin);
        dropDownDuration = Mathf.Max(0.01f, dropDownDuration);
        supportBottomTolerance = Mathf.Max(0f, supportBottomTolerance);
    }

    private OneWaySupportInfo CaptureSupportInfo()
    {
        RBCharacter25D controller = CharacterComponent;
        if (controller == null)
            return default;

        return OneWaySupportInfo.FromContacts(controller.LastSurfaceContacts);
    }

    private bool ShouldTreatAsCurrentSupport(OneWayPlatformRuntimeState state, OneWayBoxPlatform platform)
    {
        if (state == null || platform == null)
            return false;

        if (!currentSupportInfo.HasSupport || currentSupportInfo.SupportPlatform != platform)
            return false;

        if (!currentActorSnapshot.IsValid)
            return false;

        if (state.IsDropDownActive(Time.time) || state.HasReason(OneWayPassThroughReason.DropDown))
            return false;

        return state.ActorBottomY >= platform.TopY - supportBottomTolerance;
    }

    private void CollectNearbyPlatforms()
    {
        nearbyPlatforms.Clear();

        Bounds actorBounds = capsule.bounds;
        Vector3 center = actorBounds.center;
        Vector3 halfExtents = new Vector3(
            actorBounds.extents.x + searchPaddingX,
            actorBounds.extents.y + searchPaddingY,
            actorBounds.extents.z + searchPaddingZ);

        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            overlapHits,
            Quaternion.identity,
            oneWayPlatformMask,
            QueryTriggerInteraction.Ignore);

        if (warnIfHitBufferIsFull && hitCount == overlapHits.Length)
        {
            Debug.LogWarning(
                $"[{nameof(OneWayPlatformController)}] Nearby platform hit buffer is full on '{name}'. " +
                "Increase overlap buffer size if some one-way platforms are missed.",
                this);
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapHits[i];
            if (hit == null)
                continue;

            OneWayBoxPlatform platform = OneWayPlatformUtility.ResolvePlatform(hit);
            if (platform == null || platform.PlatformCollider != hit)
                continue;

            if (!nearbyPlatforms.Contains(platform))
                nearbyPlatforms.Add(platform);
        }
    }

    private void UpdateRuntimeStates()
    {
        runtimeStateBuffer.Clear();

        for (int i = 0; i < nearbyPlatforms.Count; i++)
        {
            OneWayBoxPlatform platform = nearbyPlatforms[i];
            if (platform != null && !runtimeStateBuffer.Contains(platform))
                runtimeStateBuffer.Add(platform);
        }

        for (int i = 0; i < runtimeStateBuffer.Count; i++)
        {
            OneWayBoxPlatform platform = runtimeStateBuffer[i];
            OneWayPlatformRuntimeState state = GetOrCreateRuntimeState(platform);
            bool wasIgnoredLastFrame = state.FinalIgnoreRequested;

            state.BeginFrame();
            state.Platform = platform;
            state.CapturePlatformSnapshot();
            state.CaptureActorSnapshot(currentActorSnapshot);
            if (hasPreviousActorSnapshot && previousActorSnapshot.IsValid)
                state.CapturePreviousActorSnapshot(previousActorSnapshot);

            state.IsNearby = true;
            state.HasHorizontalOverlap = currentActorSnapshot.IsValid && platform.HasHorizontalSupportOverlap(currentActorSnapshot.Bounds);
            state.WasBelowTopLastFixed = hasPreviousActorSnapshot
                && previousActorSnapshot.IsValid
                && previousActorSnapshot.BottomY < platform.TopY - previousBelowTopMargin;
            state.CrossedTopPlaneUpThisFixed = hasPreviousActorSnapshot
                && previousActorSnapshot.IsValid
                && previousActorSnapshot.BottomY < platform.TopY - bottomYEpsilon
                && currentActorSnapshot.BottomY >= platform.TopY - bottomYEpsilon
                && currentActorSnapshot.Velocity.y > minUpwardSpeedToEnterPassing;
            state.IsBelowPassThroughThreshold = currentActorSnapshot.IsValid
                && currentActorSnapshot.BottomY < platform.PassThroughThresholdY - bottomYEpsilon;
            state.IsAboveSolidifyThreshold = currentActorSnapshot.IsValid
                && currentActorSnapshot.BottomY > platform.SolidifyThresholdY + bottomYEpsilon;
            state.IsCurrentSupport = ShouldTreatAsCurrentSupport(state, platform);
            SyncCoordinatorState(state);

            UpdateCrossingState(state);
            state.Phase = DetermineRuntimePhase(state);

            if (!wasIgnoredLastFrame && state.FinalIgnoreRequested)
                state.LastBecameIgnoredTime = Time.fixedTime;
            else if (wasIgnoredLastFrame && !state.FinalIgnoreRequested)
                state.LastReturnedSolidTime = Time.fixedTime;
        }

        runtimeStateBuffer.Clear();
        runtimeStateBuffer.AddRange(runtimeStates.Keys);

        for (int i = 0; i < runtimeStateBuffer.Count; i++)
        {
            OneWayBoxPlatform platform = runtimeStateBuffer[i];
            bool keep = platform != null && nearbyPlatforms.Contains(platform);
            if (keep)
                continue;

            ReleasePlatformReason(platform, OneWayPassThroughReason.UpwardCross);
            ReleasePlatformReason(platform, OneWayPassThroughReason.DropDown);
            runtimeStates.Remove(platform);
        }

        runtimeStateBuffer.Clear();
    }

    private void SyncCoordinatorState(OneWayPlatformRuntimeState state)
    {
        if (state == null || state.Platform == null || state.Platform.PlatformCollider == null)
            return;

        CollisionIgnoreCoordinator coordinator = IgnoreCoordinator;
        if (coordinator == null)
        {
            state.LegacyIgnoreObserved = false;
            return;
        }

        if (coordinator.TryGetActiveReasons(state.Platform.PlatformCollider, out OneWayPassThroughReason observedReasons))
        {
            state.ActiveReasons |= observedReasons;
            state.FinalIgnoreRequested = state.ActiveReasons != OneWayPassThroughReason.None;
            state.LegacyIgnoreObserved = (observedReasons & OneWayPassThroughReason.LegacyResolver) != 0;
        }
        else
        {
            state.LegacyIgnoreObserved = false;
        }
    }

    private void UpdateCrossingState(OneWayPlatformRuntimeState state)
    {
        if (state == null)
            return;

        bool hasVaultOverride = state.HasReason(OneWayPassThroughReason.Vault);
        bool hasManualOverride = state.HasReason(OneWayPassThroughReason.ExternalOverride);

        bool dropDownActive = state.IsDropDownActive(Time.time);
        state.SetReason(OneWayPassThroughReason.DropDown, dropDownActive);

        if (hasVaultOverride || dropDownActive || hasManualOverride)
        {
            state.FinalIgnoreRequested = true;
            return;
        }

        bool wasPassingLastFixed = state.WasPassingUpLastFixed;
        bool enterPassing = ShouldEnterPassingUp(state);
        bool keepPassing = wasPassingLastFixed && ShouldKeepPassingUp(state);

        state.SetReason(OneWayPassThroughReason.UpwardCross, enterPassing || keepPassing);
    }

    private bool ShouldEnterPassingUp(OneWayPlatformRuntimeState state)
    {
        if (state == null || !currentActorSnapshot.IsValid || !hasPreviousActorSnapshot || !previousActorSnapshot.IsValid)
            return false;

        if (!currentActorSnapshot.CapsuleEnabled || state.IsCurrentSupport || !state.HasHorizontalOverlap)
            return false;

        bool movingUp = state.ActorVerticalSpeed > minUpwardSpeedToEnterPassing;
        if (!movingUp)
            return false;

        bool wasBelowPassBand = previousActorSnapshot.BottomY < state.PassThroughThresholdY - bottomYEpsilon;
        bool roseSinceLastFixed = currentActorSnapshot.BottomY >= previousActorSnapshot.BottomY - bottomYEpsilon;
        bool stillInsideCrossingWindow = currentActorSnapshot.BottomY <= state.SolidifyThresholdY + bottomYEpsilon;

        return wasBelowPassBand && roseSinceLastFixed && stillInsideCrossingWindow;
    }

    private bool ShouldKeepPassingUp(OneWayPlatformRuntimeState state)
    {
        if (state == null || !currentActorSnapshot.IsValid)
            return false;

        if (state.IsCurrentSupport)
            return false;

        if (!state.HasHorizontalOverlap)
            return false;

        // Держим PassingUp, пока низ капсулы не выйдет в безопасную зону above solidify.
        // Это не даёт MeshCollider платформы слишком рано вернуться в solid и вытолкнуть героя вверх
        // в апексе прыжка, когда капсула ещё геометрически пересекает top plane.
        if (state.ActorBottomY < state.SolidifyThresholdY - bottomYEpsilon)
            return true;

        // Когда герой уже выше safe-zone, можно отпустить PassingUp только если он больше не продолжает
        // заметно двигаться вверх.
        if (state.IsAboveSolidifyThreshold && state.ActorVerticalSpeed <= maxUpwardSpeedToKeepPassing)
            return false;

        if (state.ActorVerticalSpeed <= 0f)
            return false;

        return true;
    }

    private OneWayPlatformRuntimeState GetOrCreateRuntimeState(OneWayBoxPlatform platform)
    {
        if (runtimeStates.TryGetValue(platform, out OneWayPlatformRuntimeState state))
            return state;

        state = new OneWayPlatformRuntimeState
        {
            Platform = platform,
        };

        runtimeStates.Add(platform, state);
        return state;
    }

    private OneWayPlatformRuntimePhase DetermineRuntimePhase(OneWayPlatformRuntimeState state)
    {
        if (state == null)
            return OneWayPlatformRuntimePhase.Unknown;

        if (state.HasReason(OneWayPassThroughReason.DropDown))
            return OneWayPlatformRuntimePhase.SuppressedByDropDown;

        if (state.HasReason(OneWayPassThroughReason.Vault))
            return OneWayPlatformRuntimePhase.SuppressedByVault;

        if (state.IsCurrentSupport)
            return OneWayPlatformRuntimePhase.Supported;

        if (state.HasReason(OneWayPassThroughReason.UpwardCross) || state.HasReason(OneWayPassThroughReason.LegacyResolver))
            return OneWayPlatformRuntimePhase.PassingUp;

        if (state.HasHorizontalOverlap && (state.IsBelowPassThroughThreshold || state.WasBelowTopLastFixed))
            return OneWayPlatformRuntimePhase.CandidateBelow;

        return OneWayPlatformRuntimePhase.Solid;
    }

    private void ApplyRuntimeReasonsToCoordinator()
    {
        CollisionIgnoreCoordinator coordinator = IgnoreCoordinator;
        if (coordinator == null)
            return;

        foreach (OneWayPlatformRuntimeState state in runtimeStates.Values)
        {
            if (state == null || state.Platform == null || state.Platform.PlatformCollider == null)
                continue;

            bool requestUpwardCross = drivePhysicsFromRuntimeState && state.HasReason(OneWayPassThroughReason.UpwardCross);
            bool requestDropDown = drivePhysicsFromRuntimeState && state.HasReason(OneWayPassThroughReason.DropDown);
            coordinator.SetReason(state.Platform.PlatformCollider, OneWayPassThroughReason.UpwardCross, requestUpwardCross);
            coordinator.SetReason(state.Platform.PlatformCollider, OneWayPassThroughReason.DropDown, requestDropDown);
        }
    }

    private void ReleaseDrivenReasons()
    {
        CollisionIgnoreCoordinator coordinator = IgnoreCoordinator;
        if (coordinator == null)
            return;

        coordinator.ClearReasonEverywhere(OneWayPassThroughReason.UpwardCross);
        coordinator.ClearReasonEverywhere(OneWayPassThroughReason.DropDown);
    }

    private void ReleasePlatformReason(OneWayBoxPlatform platform, OneWayPassThroughReason reason)
    {
        CollisionIgnoreCoordinator coordinator = IgnoreCoordinator;
        if (coordinator == null || platform == null || platform.PlatformCollider == null)
            return;

        coordinator.SetReason(platform.PlatformCollider, reason, false);
    }

    private void StoreCurrentActorSnapshot()
    {
        previousActorSnapshot = currentActorSnapshot;
        hasPreviousActorSnapshot = currentActorSnapshot.IsValid;
    }

    private void ClearRuntimeState()
    {
        runtimeStates.Clear();
        runtimeStateBuffer.Clear();
        nearbyPlatforms.Clear();
        currentActorSnapshot = default;
        previousActorSnapshot = default;
        hasPreviousActorSnapshot = false;
        currentSupportInfo = default;
    }

    private void DrawRuntimeDebug()
    {
        if (!drawDebugPreview || capsule == null)
            return;

        Bounds actorBounds = capsule.bounds;
        Vector3 center = actorBounds.center;
        Vector3 halfExtents = new Vector3(
            actorBounds.extents.x + searchPaddingX,
            actorBounds.extents.y + searchPaddingY,
            actorBounds.extents.z + searchPaddingZ);

        DrawWireBox(center, halfExtents, Color.green);

        float actorBottomY = actorBounds.min.y;
        Vector3 bottomA = new Vector3(center.x - actorBounds.extents.x, actorBottomY, center.z);
        Vector3 bottomB = new Vector3(center.x + actorBounds.extents.x, actorBottomY, center.z);
        Debug.DrawLine(bottomA, bottomB, Color.cyan);

        foreach (OneWayPlatformRuntimeState state in runtimeStates.Values)
        {
            if (state == null || state.Platform == null)
                continue;

            DrawPlatformDebug(state);
        }
    }

    private static void DrawPlatformDebug(OneWayPlatformRuntimeState state)
    {
        Bounds bounds = state.Platform.WorldBounds;
        Color color = OneWayPlatformUtility.GetPhaseColor(state.Phase);
        DrawWireBox(bounds.center, bounds.extents, color);

        float topY = state.PlatformTopY != 0f ? state.PlatformTopY : state.Platform.TopY;
        Vector3 lineA = new Vector3(bounds.min.x, topY, bounds.center.z);
        Vector3 lineB = new Vector3(bounds.max.x, topY, bounds.center.z);
        Debug.DrawLine(lineA, lineB, color);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugWhileSelected)
            return;

        CacheComponents();
        if (capsule == null)
            return;

        Bounds actorBounds = capsule.bounds;
        Vector3 center = actorBounds.center;
        Vector3 size = new Vector3(
            (actorBounds.extents.x + searchPaddingX) * 2f,
            (actorBounds.extents.y + searchPaddingY) * 2f,
            (actorBounds.extents.z + searchPaddingZ) * 2f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            new Vector3(center.x - actorBounds.extents.x, actorBounds.min.y, center.z),
            new Vector3(center.x + actorBounds.extents.x, actorBounds.min.y, center.z));
    }

    private static void DrawWireBox(Vector3 center, Vector3 halfExtents, Color color)
    {
        Vector3[] corners = new Vector3[8];
        corners[0] = center + new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
        corners[1] = center + new Vector3(halfExtents.x, -halfExtents.y, -halfExtents.z);
        corners[2] = center + new Vector3(halfExtents.x, -halfExtents.y, halfExtents.z);
        corners[3] = center + new Vector3(-halfExtents.x, -halfExtents.y, halfExtents.z);
        corners[4] = center + new Vector3(-halfExtents.x, halfExtents.y, -halfExtents.z);
        corners[5] = center + new Vector3(halfExtents.x, halfExtents.y, -halfExtents.z);
        corners[6] = center + new Vector3(halfExtents.x, halfExtents.y, halfExtents.z);
        corners[7] = center + new Vector3(-halfExtents.x, halfExtents.y, halfExtents.z);

        Debug.DrawLine(corners[0], corners[1], color);
        Debug.DrawLine(corners[1], corners[2], color);
        Debug.DrawLine(corners[2], corners[3], color);
        Debug.DrawLine(corners[3], corners[0], color);

        Debug.DrawLine(corners[4], corners[5], color);
        Debug.DrawLine(corners[5], corners[6], color);
        Debug.DrawLine(corners[6], corners[7], color);
        Debug.DrawLine(corners[7], corners[4], color);

        Debug.DrawLine(corners[0], corners[4], color);
        Debug.DrawLine(corners[1], corners[5], color);
        Debug.DrawLine(corners[2], corners[6], color);
        Debug.DrawLine(corners[3], corners[7], color);
    }
}
