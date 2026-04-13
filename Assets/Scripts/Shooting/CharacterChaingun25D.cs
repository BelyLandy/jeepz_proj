using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public sealed class CharacterChaingun25D : MonoBehaviour
{
    private const float InvalidPastTime = -999f;

    private enum SpinStage
    {
        Idle = 0,
        Spin1 = 1,
        Spin2 = 2,
        Spin3 = 3
    }

    [Header("References")]
    [SerializeField] private RBCharacter25D character;
    [SerializeField] private CharacterFacingResolver25D facingResolver;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private CharacterMoveAimRig25D aimRig;
    [SerializeField] private PlayerControlLock25D controlLock;
    [SerializeField] private Transform leftFirePivot;
    [SerializeField] private Transform leftFirePoint;
    [SerializeField] private Transform rightFirePivot;
    [SerializeField] private Transform rightFirePoint;
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Header("Weapon State")]
    [SerializeField] private bool weaponEnabled = true;

    [Header("Action Lookup")]
    [SerializeField] private bool useCurrentActionMap = true;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string attackActionName = "Attack";

    [Header("Aim")]
    [SerializeField, Range(0f, 1f)] private float moveAimDeadzone = 0.1f;
    [SerializeField] private bool forceFirePointsLocalRotationIdentity = true;

[Header("Shot Tracers")]
    [SerializeField] private bool useShotTracers = true;
    [SerializeField] private HitscanTracer25D tracerPrefab;
    [SerializeField] private Transform tracerParent;
    [SerializeField] private int tracerPrewarmCount = 24;
    [SerializeField] private int tracerMaxPoolCount = 96;
    [SerializeField] private bool tracerFallbackInstantiateWhenPoolEmpty = true;

    [Header("World Z Lock")]
    [SerializeField] private bool lockFirePivotWorldZ = false;
    [SerializeField] private float firePivotWorldZ = 0f;

    [Header("Spin")]
    [Tooltip("Если следующее нажатие Attack пришло до истечения этого окна, текущая раскрутка считается поддержанной.")]
    [SerializeField] private float spinSupportWindow = 0.4f;
    [Tooltip("Через какое время без поддержки раскрутка падает на одну ступень.")]
    [SerializeField] private float spinDecayStepTime = 0.25f;
    [Tooltip("Базовый интервал оружейного тика. На каждом тике оружие может выпустить 1/2/3 hitscan-пули в зависимости от стадии раскрутки.")]
    [SerializeField] private float fireTickInterval = 0.1f;
    [Tooltip("При первом нажатии из Idle оружие сразу делает первый fire tick.")]
    [SerializeField] private bool fireImmediatelyFromIdle = true;

    [Header("Shots Per Tick")]
    [SerializeField] private int spin1ShotsPerTick = 1;
    [SerializeField] private int spin2ShotsPerTick = 2;
    [SerializeField] private int spin3ShotsPerTick = 3;

    [Header("Spread (Degrees)")]
    [SerializeField] private float spin1SpreadDeg = 6f;
    [SerializeField] private float spin2SpreadDeg = 3.5f;
    [SerializeField] private float spin3SpreadDeg = 1.5f;

    [Header("Hitscan")]
    [SerializeField] private int damagePerBullet = 8;
    [SerializeField] private float range = 50f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private float impactForce = 0f;

    [Header("Ammo (Optional)")]
    [SerializeField] private bool useFiniteAmmo;
    [SerializeField] private int currentAmmo = 200;
    [SerializeField] private int ammoPerBullet = 1;

    [Header("Shoot Lock After Jumps")]
    [SerializeField] private bool blockShootingAfterSingleJump = true;
    [SerializeField] private float singleJumpShootLockTime = 0.1f;
    [SerializeField] private bool blockShootingAfterDoubleJump = true;
    [SerializeField] private float doubleJumpShootLockTime = 0.14f;
    [SerializeField] private bool blockShootingAfterWallJump = true;
    [SerializeField] private float wallJumpShootLockTime = 0.12f;

    [Header("Camera Impulse (Optional)")]
    [SerializeField] private bool generateImpulseOnFireTick;
    [SerializeField] private float spin1ImpulseMagnitude = 0.08f;
    [SerializeField] private float spin2ImpulseMagnitude = 0.12f;
    [SerializeField] private float spin3ImpulseMagnitude = 0.16f;

    [Header("Debug")]
    [SerializeField] private bool debugDrawShots;
    [SerializeField] private bool debugLogStateChanges;
    [SerializeField] private float debugRayDuration = 0.08f;

    private InputActionMap resolvedActionMap;
    private InputAction moveAction;
    private InputAction attackAction;

    private SpinStage currentSpinStage;
    private float supportExpireTime = InvalidPastTime;
    private float nextDecayStepTime = InvalidPastTime;
    private float nextFireTickTime = InvalidPastTime;
    private bool queuedImmediateFireTick;

    private Vector3 currentAimDirection = Vector3.right;
    private float currentAimAngleZ;
    private bool currentUsesLeftHand = true;

    private float shootBlockedUntilTime = InvalidPastTime;
    private int lastObservedSelfJumpVersion = -1;

    private readonly Queue<HitscanTracer25D> tracerPool = new Queue<HitscanTracer25D>();
    private readonly HashSet<HitscanTracer25D> tracerInstances = new HashSet<HitscanTracer25D>();
    private Transform tracerPoolRoot;

    public bool WeaponEnabled => weaponEnabled;
    public int CurrentSpinStage => (int)currentSpinStage;
    public Vector3 CurrentAimDirection => currentAimDirection;
    public bool CurrentUsesLeftHand => currentUsesLeftHand;
    public int CurrentAmmo => currentAmmo;

    private void Reset()
    {
        if (character == null)
            character = GetComponent<RBCharacter25D>();

        CacheFacingResolver();

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (aimRig == null)
            aimRig = GetComponent<CharacterMoveAimRig25D>();

        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();
    }

    private void Awake()
    {
        if (character == null)
            character = GetComponent<RBCharacter25D>();

        CacheFacingResolver();

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (aimRig == null)
            aimRig = GetComponent<CharacterMoveAimRig25D>();

        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();

        ClampSettings();
        InitializeTracerPool();
        SyncAimTransformImmediately();
    }

    private void OnValidate()
    {
        ClampSettings();

        if (character == null)
            character = GetComponent<RBCharacter25D>();

        CacheFacingResolver();

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (aimRig == null)
            aimRig = GetComponent<CharacterMoveAimRig25D>();

        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();

        if (string.IsNullOrWhiteSpace(actionMapName))
            actionMapName = "Player";
        if (string.IsNullOrWhiteSpace(moveActionName))
            moveActionName = "Move";
        if (string.IsNullOrWhiteSpace(attackActionName))
            attackActionName = "Attack";
    }

    private void OnEnable()
    {
        ResolveActions(forceResubscribe: true);
        InitializeTracerPool();
        SyncAimTransformImmediately();
    }

    private void OnDisable()
    {
        UnsubscribeAttackCallbacks();
        queuedImmediateFireTick = false;
        currentSpinStage = SpinStage.Idle;
        ReclaimActiveTracers();
    }

    private void Update()
    {
        ResolveActions(forceResubscribe: false);
        SyncJumpShootLockState();

        if (controlLock != null && controlLock.IsControlLocked)
        {
            StopForControlLock();
            return;
        }

        UpdateAimState();
        UpdateSpinDecay();
    }

    private void LateUpdate()
    {
        ApplyAllFirePivotWorldTransforms();

        if (controlLock != null && controlLock.IsControlLocked)
        {
            StopForControlLock();
            return;
        }

        if (!weaponEnabled)
            return;

        if (queuedImmediateFireTick)
        {
            queuedImmediateFireTick = false;
            TryExecuteFireTick();
        }

        if (currentSpinStage == SpinStage.Idle)
            return;

        if (fireTickInterval <= 0f)
        {
            TryExecuteFireTick();
            return;
        }

        while (Time.time >= nextFireTickTime)
        {
            TryExecuteFireTick();
            nextFireTickTime += fireTickInterval;

            if (currentSpinStage == SpinStage.Idle)
                break;
        }
    }

    public void SetWeaponEnabled(bool enabled)
    {
        if (weaponEnabled == enabled)
            return;

        weaponEnabled = enabled;
        if (!weaponEnabled)
            StopForControlLock();
    }

    public void StopForControlLock()
    {
        currentSpinStage = SpinStage.Idle;
        queuedImmediateFireTick = false;
        supportExpireTime = InvalidPastTime;
        nextDecayStepTime = InvalidPastTime;
        nextFireTickTime = InvalidPastTime;
    }

    public void SetCurrentAmmo(int ammo)
    {
        currentAmmo = Mathf.Max(0, ammo);
    }

    public void AddAmmo(int amount)
    {
        currentAmmo = Mathf.Max(0, currentAmmo + amount);
    }

    private void ClampSettings()
    {
        moveAimDeadzone = Mathf.Clamp01(moveAimDeadzone);
        tracerPrewarmCount = Mathf.Max(0, tracerPrewarmCount);
        tracerMaxPoolCount = Mathf.Max(tracerPrewarmCount, tracerMaxPoolCount);
        spinSupportWindow = Mathf.Max(0.01f, spinSupportWindow);
        spinDecayStepTime = Mathf.Max(0.01f, spinDecayStepTime);
        fireTickInterval = Mathf.Max(0.01f, fireTickInterval);
        spin1ShotsPerTick = Mathf.Max(1, spin1ShotsPerTick);
        spin2ShotsPerTick = Mathf.Max(1, spin2ShotsPerTick);
        spin3ShotsPerTick = Mathf.Max(1, spin3ShotsPerTick);
        spin1SpreadDeg = Mathf.Max(0f, spin1SpreadDeg);
        spin2SpreadDeg = Mathf.Max(0f, spin2SpreadDeg);
        spin3SpreadDeg = Mathf.Max(0f, spin3SpreadDeg);
        damagePerBullet = Mathf.Max(0, damagePerBullet);
        range = Mathf.Max(0.01f, range);
        currentAmmo = Mathf.Max(0, currentAmmo);
        ammoPerBullet = Mathf.Max(1, ammoPerBullet);
        singleJumpShootLockTime = Mathf.Max(0f, singleJumpShootLockTime);
        doubleJumpShootLockTime = Mathf.Max(0f, doubleJumpShootLockTime);
        wallJumpShootLockTime = Mathf.Max(0f, wallJumpShootLockTime);
        spin1ImpulseMagnitude = Mathf.Max(0f, spin1ImpulseMagnitude);
        spin2ImpulseMagnitude = Mathf.Max(0f, spin2ImpulseMagnitude);
        spin3ImpulseMagnitude = Mathf.Max(0f, spin3ImpulseMagnitude);
        debugRayDuration = Mathf.Max(0f, debugRayDuration);
    }

    private void InitializeTracerPool()
    {
        if (!useShotTracers || tracerPrefab == null)
            return;

        if (tracerPoolRoot == null)
        {
            GameObject root = new GameObject("[ChaingunTracerPool]");
            root.hideFlags = HideFlags.HideInHierarchy;
            tracerPoolRoot = root.transform;
            if (tracerParent != null)
                tracerPoolRoot.SetParent(tracerParent, false);
            else
                tracerPoolRoot.SetParent(transform, false);
        }

        while (tracerInstances.Count < tracerPrewarmCount && tracerInstances.Count < tracerMaxPoolCount)
        {
            HitscanTracer25D tracer = CreateTracerInstance();
            if (tracer == null)
                break;
            ReturnTracerToPool(tracer);
        }
    }

    private void ReclaimActiveTracers()
    {
        foreach (HitscanTracer25D tracer in tracerInstances)
        {
            if (tracer == null)
                continue;
            tracer.StopAndHide(clearTrail: true);
            tracer.gameObject.SetActive(false);
            if (tracerPoolRoot != null)
                tracer.transform.SetParent(tracerPoolRoot, false);
        }

        tracerPool.Clear();
        foreach (HitscanTracer25D tracer in tracerInstances)
        {
            if (tracer != null)
                tracerPool.Enqueue(tracer);
        }
    }

    private HitscanTracer25D CreateTracerInstance()
    {
        if (tracerPrefab == null)
            return null;

        Transform parent = tracerPoolRoot != null ? tracerPoolRoot : tracerParent;
        HitscanTracer25D tracer = Instantiate(tracerPrefab, parent);
        tracer.ConfigurePool(ReturnTracerToPool);
        tracer.StopAndHide(clearTrail: true);
        tracer.gameObject.SetActive(false);
        tracerInstances.Add(tracer);
        return tracer;
    }

    private HitscanTracer25D GetTracerFromPool()
    {
        if (!useShotTracers || tracerPrefab == null)
            return null;

        while (tracerPool.Count > 0)
        {
            HitscanTracer25D tracer = tracerPool.Dequeue();
            if (tracer != null)
                return tracer;
        }

        if (tracerInstances.Count < tracerMaxPoolCount || tracerFallbackInstantiateWhenPoolEmpty)
            return CreateTracerInstance();

        return null;
    }

    private void ReturnTracerToPool(HitscanTracer25D tracer)
    {
        if (tracer == null)
            return;

        tracer.StopAndHide(clearTrail: true);
        tracer.gameObject.SetActive(false);

        if (tracerPoolRoot != null)
            tracer.transform.SetParent(tracerPoolRoot, false);
        else if (tracerParent != null)
            tracer.transform.SetParent(tracerParent, false);

        if (!tracerPool.Contains(tracer))
            tracerPool.Enqueue(tracer);
    }

    private void PlayShotTracer(Vector3 start, Vector3 end)
    {
        HitscanTracer25D tracer = GetTracerFromPool();
        if (tracer == null)
            return;

        if (tracerParent != null)
            tracer.transform.SetParent(tracerParent, true);
        else
            tracer.transform.SetParent(null, true);

        tracer.Play(start, end);
    }

    private void ResolveActions(bool forceResubscribe)
    {
        if (playerInput == null)
            return;

        InputActionMap targetMap = null;

        if (useCurrentActionMap && playerInput.currentActionMap != null)
            targetMap = playerInput.currentActionMap;

        if (targetMap == null && playerInput.actions != null && !string.IsNullOrWhiteSpace(actionMapName))
            targetMap = playerInput.actions.FindActionMap(actionMapName, false);

        bool mapChanged = targetMap != resolvedActionMap;
        if (!mapChanged && !forceResubscribe)
            return;

        UnsubscribeAttackCallbacks();

        resolvedActionMap = targetMap;
        moveAction = resolvedActionMap != null ? resolvedActionMap.FindAction(moveActionName, false) : null;
        attackAction = resolvedActionMap != null ? resolvedActionMap.FindAction(attackActionName, false) : null;

        SubscribeAttackCallbacks();
    }

    private void SubscribeAttackCallbacks()
    {
        if (attackAction == null)
            return;

        attackAction.started -= OnAttackStarted;
        attackAction.started += OnAttackStarted;
    }

    private void UnsubscribeAttackCallbacks()
    {
        if (attackAction == null)
            return;

        attackAction.started -= OnAttackStarted;
    }

    private void OnAttackStarted(InputAction.CallbackContext context)
    {
        if (!weaponEnabled)
            return;
        if (controlLock != null && controlLock.IsControlLocked)
            return;

        SyncJumpShootLockState();
        if (Time.time < shootBlockedUntilTime)
            return;
        if (useFiniteAmmo && currentAmmo <= 0)
            return;

        bool wasIdle = currentSpinStage == SpinStage.Idle;
        bool withinSupportWindow = Time.time <= supportExpireTime;

        if (currentSpinStage == SpinStage.Idle)
        {
            SetSpinStage(SpinStage.Spin1);
        }
        else if (withinSupportWindow)
        {
            PromoteSpinStage();
        }

        supportExpireTime = Time.time + spinSupportWindow;
        nextDecayStepTime = supportExpireTime + spinDecayStepTime;

        if (wasIdle)
        {
            nextFireTickTime = Time.time + fireTickInterval;
            if (fireImmediatelyFromIdle)
                queuedImmediateFireTick = true;
        }
    }

    private void PromoteSpinStage()
    {
        if (currentSpinStage == SpinStage.Spin1)
            SetSpinStage(SpinStage.Spin2);
        else if (currentSpinStage == SpinStage.Spin2)
            SetSpinStage(SpinStage.Spin3);
    }

    private void UpdateAimState()
    {
        currentAimDirection = ResolveBaseShotDirection(ReadRawMove());
        currentAimAngleZ = DirectionToWorldZAngleDeg(currentAimDirection);

        currentUsesLeftHand = aimRig != null
            ? aimRig.EvaluateHandForDirection(currentAimDirection, true)
            : true;
    }

    private void SyncAimTransformImmediately()
    {
        UpdateAimState();
        ApplyAllFirePivotWorldTransforms();
    }

    private void UpdateSpinDecay()
    {
        if (!weaponEnabled)
            return;
        if (currentSpinStage == SpinStage.Idle)
            return;
        if (Time.time <= supportExpireTime)
            return;
        if (Time.time < nextDecayStepTime)
            return;

        if (currentSpinStage == SpinStage.Spin3)
            SetSpinStage(SpinStage.Spin2);
        else if (currentSpinStage == SpinStage.Spin2)
            SetSpinStage(SpinStage.Spin1);
        else
            SetSpinStage(SpinStage.Idle);

        if (currentSpinStage != SpinStage.Idle)
            nextDecayStepTime = Time.time + spinDecayStepTime;
        else
            nextDecayStepTime = InvalidPastTime;
    }

    private void SetSpinStage(SpinStage newStage)
    {
        if (currentSpinStage == newStage)
            return;

        currentSpinStage = newStage;

        if (debugLogStateChanges)
            Debug.Log($"[CharacterChaingun25D] Spin stage -> {currentSpinStage}", this);
    }

    private bool TryExecuteFireTick()
    {
        if (Time.time < shootBlockedUntilTime)
            return false;
        if (currentSpinStage == SpinStage.Idle)
            return false;

        int requestedShots = GetShotsPerTick(currentSpinStage);
        if (requestedShots <= 0)
            return false;

        int shotsToFire = requestedShots;
        if (useFiniteAmmo)
        {
            int ammoLimitedShots = currentAmmo / ammoPerBullet;
            shotsToFire = Mathf.Min(shotsToFire, ammoLimitedShots);
            if (shotsToFire <= 0)
            {
                SetSpinStage(SpinStage.Idle);
                return false;
            }
        }

        Vector2 rawMoveAtTick = ReadRawMove();
        Vector3 baseDirection = ResolveBaseShotDirection(rawMoveAtTick);
        bool usesLeftHand = aimRig != null
            ? aimRig.EvaluateHandForDirection(baseDirection, true)
            : currentUsesLeftHand;

        currentUsesLeftHand = usesLeftHand;
        currentAimDirection = baseDirection;
        currentAimAngleZ = DirectionToWorldZAngleDeg(baseDirection);

        ApplyAllFirePivotWorldTransforms();

        if (aimRig != null)
            aimRig.NotifyShotDirection(baseDirection);

        Transform firePoint = usesLeftHand ? leftFirePoint : rightFirePoint;
        if (firePoint == null)
            firePoint = usesLeftHand ? leftFirePivot : rightFirePivot;
        if (firePoint == null)
            return false;

        float spread = GetSpreadForStage(currentSpinStage);
        Vector3 recoilDirectionAccumulator = Vector3.zero;
        int bulletsActuallyFired = 0;

        for (int i = 0; i < shotsToFire; i++)
        {
            Vector3 shotDirection = ApplySpread(baseDirection, spread);
            if (FireSingleHitscan(firePoint.position, shotDirection))
            {
                // intentionally empty; side-effects handled in FireSingleHitscan
            }

            bulletsActuallyFired++;
            recoilDirectionAccumulator += shotDirection;

        }

        if (bulletsActuallyFired <= 0)
            return false;

        if (useFiniteAmmo)
            currentAmmo = Mathf.Max(0, currentAmmo - bulletsActuallyFired * ammoPerBullet);

        Vector3 recoilDirection = recoilDirectionAccumulator.sqrMagnitude > 0.0001f
            ? recoilDirectionAccumulator.normalized
            : baseDirection;

        GenerateImpulseIfNeeded(recoilDirection);

        NotifyAirShotPhysicsIfNeeded(recoilDirection);

        return true;
    }

    private bool FireSingleHitscan(Vector3 origin, Vector3 shotDirection)
    {
        shotDirection.z = 0f;
        if (shotDirection.sqrMagnitude <= 0.0001f)
            shotDirection = currentAimDirection.sqrMagnitude > 0.0001f ? currentAimDirection : Vector3.right;
        shotDirection.Normalize();

        origin.z = lockFirePivotWorldZ ? firePivotWorldZ : origin.z;

        Ray ray = new Ray(origin, shotDirection);
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, range, hitMask, queryTriggerInteraction);
        Vector3 tracerEnd = hasHit ? hit.point : origin + shotDirection * range;
        tracerEnd.z = lockFirePivotWorldZ ? firePivotWorldZ : tracerEnd.z;

        if (hasHit)
        {
            if (impactForce > 0f && hit.rigidbody != null && !hit.rigidbody.isKinematic)
                hit.rigidbody.AddForceAtPosition(shotDirection * impactForce, hit.point, ForceMode.Impulse);

            IHitscanDamageReceiver25D damageReceiver = hit.collider.GetComponentInParent<IHitscanDamageReceiver25D>();
            if (damageReceiver != null)
                damageReceiver.ReceiveHitscanDamage(damagePerBullet, hit.point, shotDirection, gameObject);

            if (debugDrawShots)
                Debug.DrawLine(origin, hit.point, Color.yellow, debugRayDuration);
        }
        else if (debugDrawShots)
        {
            Debug.DrawRay(origin, shotDirection * range, Color.yellow, debugRayDuration);
        }

        if (useShotTracers)
            PlayShotTracer(origin, tracerEnd);

        return hasHit;
    }

    private void GenerateImpulseIfNeeded(Vector3 shotDirection)
    {
        if (!generateImpulseOnFireTick || impulseSource == null)
            return;

        float magnitude = GetImpulseMagnitudeForStage(currentSpinStage);
        if (magnitude <= 0f)
            return;

        Vector3 recoilVelocity = -shotDirection.normalized * magnitude;
        recoilVelocity.z = 0f;

        impulseSource.DefaultVelocity = recoilVelocity;
        impulseSource.GenerateImpulseAtPositionWithVelocity(impulseSource.transform.position, recoilVelocity);
    }

    private void NotifyAirShotPhysicsIfNeeded(Vector3 shotDirection)
    {
        if (character == null)
            return;

        character.NotifyAirShotPhysics(shotDirection);
    }

    private Vector3 ResolveBaseShotDirection(Vector2 rawMove)
    {
        Vector3 direction;

        if (rawMove.sqrMagnitude > moveAimDeadzone * moveAimDeadzone)
        {
            direction = new Vector3(rawMove.x, rawMove.y, 0f);
            if (direction.sqrMagnitude > 0.0001f)
                direction.Normalize();
            else
                direction = GetFallbackShotDirection();
        }
        else
        {
            direction = GetFallbackShotDirection();
        }

        if (aimRig != null)
            direction = aimRig.ResolveShotDirectionForCurrentState(direction);

        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = GetFallbackShotDirection();

        return direction.normalized;
    }

    private Vector3 GetFallbackShotDirection()
    {
        int facingSign = GetResolvedFacingSignFallback();
        return facingSign >= 0 ? Vector3.right : Vector3.left;
    }

    private int GetResolvedFacingSignFallback()
    {
        if (facingResolver != null)
            return facingResolver.ResolvedFacingSign;

        if (character != null && character.IsWallSliding)
        {
            if (character.WallSlideSide < 0)
                return +1;
            if (character.WallSlideSide > 0)
                return -1;
        }

        return character != null ? character.VaultFacingSignFromInput : +1;
    }

    private void CacheFacingResolver()
    {
        if (facingResolver != null)
            return;

        if (character != null && character.FacingResolverComponent != null)
        {
            facingResolver = character.FacingResolverComponent;
            return;
        }

        facingResolver = GetComponent<CharacterFacingResolver25D>();
    }

    private Vector3 ApplySpread(Vector3 baseDirection, float spreadDeg)
    {
        float randomAngle = spreadDeg > 0f ? Random.Range(-spreadDeg, spreadDeg) : 0f;
        Vector3 spreadDirection = Quaternion.Euler(0f, 0f, randomAngle) * baseDirection;

        if (aimRig != null)
            spreadDirection = aimRig.ResolveDirectionForCurrentState(spreadDirection);

        spreadDirection.z = 0f;
        if (spreadDirection.sqrMagnitude <= 0.0001f)
            spreadDirection = baseDirection;

        return spreadDirection.normalized;
    }

    private void SyncJumpShootLockState()
    {
        if (character == null)
            return;

        int currentVersion = character.LastSelfJumpStateVersion;
        if (currentVersion == lastObservedSelfJumpVersion)
            return;

        lastObservedSelfJumpVersion = currentVersion;

        float lockDuration = 0f;
        switch (character.LastSelfJumpType)
        {
            case RBCharacter25D.SelfJumpKind.SingleJump:
                if (blockShootingAfterSingleJump)
                    lockDuration = singleJumpShootLockTime;
                break;

            case RBCharacter25D.SelfJumpKind.DoubleJump:
                if (blockShootingAfterDoubleJump)
                    lockDuration = doubleJumpShootLockTime;
                break;

            case RBCharacter25D.SelfJumpKind.WallJump:
                if (blockShootingAfterWallJump)
                    lockDuration = wallJumpShootLockTime;
                break;
        }

        if (lockDuration > 0f)
            shootBlockedUntilTime = Mathf.Max(shootBlockedUntilTime, Time.time + lockDuration);
    }

    private void ApplyAllFirePivotWorldTransforms()
    {
        ApplySingleFirePivotWorldTransform(leftFirePivot);
        ApplySingleFirePivotWorldTransform(rightFirePivot);

        if (forceFirePointsLocalRotationIdentity)
        {
            if (leftFirePoint != null)
                leftFirePoint.localRotation = Quaternion.identity;

            if (rightFirePoint != null)
                rightFirePoint.localRotation = Quaternion.identity;
        }
    }

    private void ApplySingleFirePivotWorldTransform(Transform firePivot)
    {
        if (firePivot == null)
            return;

        Vector3 position = firePivot.position;
        if (lockFirePivotWorldZ)
            position.z = firePivotWorldZ;
        firePivot.position = position;
        firePivot.rotation = Quaternion.Euler(0f, 0f, currentAimAngleZ);
    }

    private Vector2 ReadRawMove()
    {
        if (moveAction == null || !moveAction.enabled)
            return Vector2.zero;

        return moveAction.ReadValue<Vector2>();
    }

    private int GetShotsPerTick(SpinStage stage)
    {
        switch (stage)
        {
            case SpinStage.Spin1:
                return spin1ShotsPerTick;
            case SpinStage.Spin2:
                return spin2ShotsPerTick;
            case SpinStage.Spin3:
                return spin3ShotsPerTick;
            default:
                return 0;
        }
    }

    private float GetSpreadForStage(SpinStage stage)
    {
        switch (stage)
        {
            case SpinStage.Spin1:
                return spin1SpreadDeg;
            case SpinStage.Spin2:
                return spin2SpreadDeg;
            case SpinStage.Spin3:
                return spin3SpreadDeg;
            default:
                return 0f;
        }
    }

    private float GetImpulseMagnitudeForStage(SpinStage stage)
    {
        switch (stage)
        {
            case SpinStage.Spin1:
                return spin1ImpulseMagnitude;
            case SpinStage.Spin2:
                return spin2ImpulseMagnitude;
            case SpinStage.Spin3:
                return spin3ImpulseMagnitude;
            default:
                return 0f;
        }
    }

    private static float DirectionToWorldZAngleDeg(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return 0f;

        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }
}
