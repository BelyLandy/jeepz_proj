using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public sealed class CharacterPlasmaGlove25D : MonoBehaviour
{
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
    [SerializeField] private StraightProjectile projectilePrefab;
    [SerializeField] private Transform projectileParent;
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Header("Action Lookup")]
    [SerializeField] private bool useCurrentActionMap = true;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string attackActionName = "Attack";

    [Header("Aim")]
    [SerializeField, Range(0f, 1f)] private float moveAimDeadzone = 0.1f;
    [SerializeField] private bool forceFirePointsLocalRotationIdentity = true;

    [Header("World Z Lock")]
    [SerializeField] private bool lockFirePivotWorldZ = false;
    [SerializeField] private float firePivotWorldZ = 0f;

    [Header("Attack Rate Limit")]
    [Tooltip("Максимум допустимых нажатий выстрела в секунду. 0 или меньше = без ограничения.")]
    [SerializeField] private float maxShotsPerSecond = 8f;
    [SerializeField] private bool spawnPendingShotInLateUpdate = true;

    [Header("Shoot Lock After Jumps")]
    [SerializeField] private bool blockShootingAfterSingleJump = true;
    [SerializeField] private float singleJumpShootLockTime = 0.1f;
    [SerializeField] private bool blockShootingAfterDoubleJump = true;
    [SerializeField] private float doubleJumpShootLockTime = 0.14f;
    [SerializeField] private bool blockShootingAfterWallJump = true;
    [SerializeField] private float wallJumpShootLockTime = 0.12f;

    [Header("Ammo")]
    [SerializeField] private int maxAmmo = 30;
    [SerializeField] private bool startWithFullAmmo = true;
    [SerializeField] private int currentAmmo = 30;
    [SerializeField] private int ammoPerShot = 1;

    [Header("Heat")]
    [SerializeField] private float currentHeat = 0f;
    [SerializeField] private float heatPerShot = 0.15f;
    [SerializeField] private float passiveCoolRate = 0.30f;
    [SerializeField] private float overheatThreshold = 1f;
    [SerializeField] private float overheatLockDuration = 1.25f;

    [Header("Projectile Hit Payload Override")]
    [SerializeField] private bool overrideProjectileHitPayload = false;
    [SerializeField, Min(0f)] private float projectileDamage = 10f;
    [SerializeField, Min(0f)] private float projectileStunDuration = 0.2f;
    [SerializeField, Min(0f)] private float projectileKnockbackHorizontal = 5f;
    [SerializeField, Min(0f)] private float projectileKnockbackVertical = 0f;
    [SerializeField] private bool projectileKnockbackHorizontalOnly = true;
    [SerializeField] private bool preserveTargetVerticalVelocityOnProjectileHit = true;

    [Header("Impulse (Optional)")]
    [SerializeField] private bool generateImpulseOnShot = true;
    [SerializeField] private float impulseMagnitude = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool debugDrawAim;
    [SerializeField] private float debugRayLength = 1.25f;

    private const float InvalidPastTime = -999f;
    private const float HeatZeroEpsilon = 0.0001f;

    private enum ShotBlockReason
    {
        None,
        NoAmmo,
        Overheated,
        MissingProjectilePrefab,
        MissingFirePoint,
    }

    private InputActionMap resolvedActionMap;
    private InputAction moveAction;
    private InputAction attackAction;

    private float nextAllowedShotTime = InvalidPastTime;
    private float currentAimAngleZ;
    private Vector3 currentShotDirection = Vector3.right;

    private bool hasPendingShot;
    private Vector3 pendingShotDirection = Vector3.right;
    private bool pendingShotUsesLeftHand;

    private float shootBlockedUntilTime = InvalidPastTime;
    private int lastObservedSelfJumpVersion = -1;

    private bool isOverheated;
    private float overheatStartedAt = InvalidPastTime;
    private float overheatEndsAt = InvalidPastTime;
    private float overheatStartHeat;

    private int dryFireEventVersion;

    public float CurrentAimAngleZ => currentAimAngleZ;
    public Vector3 CurrentShotDirection => currentShotDirection;
    public bool CurrentShotUsesLeftHand => aimRig != null ? aimRig.UsesLeftRig : true;
    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => maxAmmo;
    public bool IsAmmoFullNow => currentAmmo >= maxAmmo;
    public float AmmoNormalized => maxAmmo > 0 ? Mathf.Clamp01((float)currentAmmo / maxAmmo) : 0f;
    public float CurrentHeat => currentHeat;
    public float CurrentHeatNormalized => overheatThreshold > HeatZeroEpsilon ? Mathf.Clamp01(currentHeat / overheatThreshold) : 0f;
    public bool IsOutOfAmmoNow => currentAmmo < ammoPerShot;
    public bool IsOverheatedNow => isOverheated;
    public bool CanPresentWeaponPoseNow => !IsOutOfAmmoNow && !IsOverheatedNow;
    public int DryFireEventVersion => dryFireEventVersion;

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

        ClampSettings();
        if (startWithFullAmmo)
            currentAmmo = maxAmmo;
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
        if (startWithFullAmmo)
            currentAmmo = maxAmmo;

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

        if (!Application.isPlaying && startWithFullAmmo)
            currentAmmo = maxAmmo;
    }

    private void OnEnable()
    {
        ResolveActions(forceResubscribe: true);
        SyncAimTransformImmediately();
    }

    private void OnDisable()
    {
        UnsubscribeAttackCallbacks();
        hasPendingShot = false;
    }

    private void Update()
    {
        ResolveActions(forceResubscribe: false);
        SyncJumpShootLockState();
        UpdateHeatState();

        if (controlLock != null && controlLock.IsControlLocked)
        {
            hasPendingShot = false;
            return;
        }

        UpdateAimState();
    }

    private void LateUpdate()
    {
        ApplyAllFirePivotWorldTransforms();

        if (controlLock != null && controlLock.IsControlLocked)
        {
            hasPendingShot = false;
            return;
        }

        if (!hasPendingShot)
            return;

        ExecutePendingShot();
    }

    public int AddAmmo(int amount)
    {
        if (amount <= 0)
            return 0;

        int before = currentAmmo;
        currentAmmo = Mathf.Clamp(currentAmmo + amount, 0, maxAmmo);
        return currentAmmo - before;
    }

    public void RefillAmmoFull()
    {
        currentAmmo = maxAmmo;
    }

    public bool ConsumeAmmo(int amount)
    {
        if (amount <= 0)
            return true;
        if (currentAmmo < amount)
            return false;

        currentAmmo -= amount;
        return true;
    }

    private void ClampSettings()
    {
        moveAimDeadzone = Mathf.Clamp01(moveAimDeadzone);
        impulseMagnitude = Mathf.Max(0f, impulseMagnitude);
        singleJumpShootLockTime = Mathf.Max(0f, singleJumpShootLockTime);
        doubleJumpShootLockTime = Mathf.Max(0f, doubleJumpShootLockTime);
        wallJumpShootLockTime = Mathf.Max(0f, wallJumpShootLockTime);
        maxAmmo = Mathf.Max(1, maxAmmo);
        ammoPerShot = Mathf.Max(1, ammoPerShot);
        currentAmmo = Mathf.Clamp(currentAmmo, 0, maxAmmo);
        heatPerShot = Mathf.Max(0f, heatPerShot);
        passiveCoolRate = Mathf.Max(0f, passiveCoolRate);
        overheatThreshold = Mathf.Max(HeatZeroEpsilon, overheatThreshold);
        overheatLockDuration = Mathf.Max(0f, overheatLockDuration);
        currentHeat = Mathf.Max(0f, currentHeat);
        projectileDamage = Mathf.Max(0f, projectileDamage);
        projectileStunDuration = Mathf.Max(0f, projectileStunDuration);
        projectileKnockbackHorizontal = Mathf.Max(0f, projectileKnockbackHorizontal);
        projectileKnockbackVertical = Mathf.Max(0f, projectileKnockbackVertical);
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
        SyncJumpShootLockState();

        if (controlLock != null && controlLock.IsControlLocked)
            return;
        if (Time.time < nextAllowedShotTime)
            return;
        if (Time.time < shootBlockedUntilTime)
            return;

        Vector2 rawMoveAtShot = ReadRawMove();
        Vector3 shotDirection = ResolveShotDirection(rawMoveAtShot);
        currentShotDirection = shotDirection;
        currentAimAngleZ = DirectionToWorldZAngleDeg(shotDirection);

        bool shotUsesLeftHand = aimRig != null
            ? aimRig.EvaluateHandForDirection(shotDirection, true)
            : true;

        ShotBlockReason blockReason = EvaluateShotBlockReason(shotUsesLeftHand);
        if (blockReason != ShotBlockReason.None)
        {
            HandleBlockedShot(blockReason);
            return;
        }

        if (aimRig != null)
            aimRig.NotifyShotDirection(shotDirection);

        if (spawnPendingShotInLateUpdate)
        {
            hasPendingShot = true;
            pendingShotDirection = shotDirection;
            pendingShotUsesLeftHand = shotUsesLeftHand;
        }
        else
        {
            ApplyAllFirePivotWorldTransforms();
            if (!TryCommitShot(shotDirection, shotUsesLeftHand))
                return;
        }

        nextAllowedShotTime = Time.time + GetMinShotInterval();
    }

    private void HandleBlockedShot(ShotBlockReason blockReason)
    {
        if (blockReason == ShotBlockReason.NoAmmo || blockReason == ShotBlockReason.Overheated)
            RegisterDryFireEvent();
    }

    private bool TryCommitShot(Vector3 shotDirection, bool usesLeftHand)
    {
        ShotBlockReason blockReason = EvaluateShotBlockReason(usesLeftHand);
        if (blockReason != ShotBlockReason.None)
        {
            HandleBlockedShot(blockReason);
            return false;
        }

        Transform sourcePoint = usesLeftHand ? leftFirePoint : rightFirePoint;
        if (sourcePoint == null || projectilePrefab == null)
            return false;

        if (!ConsumeAmmo(ammoPerShot))
        {
            RegisterDryFireEvent();
            return false;
        }

        SpawnProjectile(sourcePoint, shotDirection);
        GenerateImpulseIfNeeded(shotDirection);
        NotifyAirShotPhysicsIfNeeded(shotDirection);
        ApplyHeatAfterSuccessfulShot();
        return true;
    }

    private ShotBlockReason EvaluateShotBlockReason(bool usesLeftHand)
    {
        if (IsOutOfAmmoNow)
            return ShotBlockReason.NoAmmo;
        if (isOverheated)
            return ShotBlockReason.Overheated;
        if (projectilePrefab == null)
            return ShotBlockReason.MissingProjectilePrefab;

        Transform sourcePoint = usesLeftHand ? leftFirePoint : rightFirePoint;
        if (sourcePoint == null)
            return ShotBlockReason.MissingFirePoint;

        return ShotBlockReason.None;
    }

    private void ApplyHeatAfterSuccessfulShot()
    {
        currentHeat = Mathf.Max(0f, currentHeat) + heatPerShot;

        if (currentHeat > overheatThreshold)
            BeginOverheat();
    }

    private void BeginOverheat()
    {
        isOverheated = true;
        overheatStartedAt = Time.time;
        overheatEndsAt = overheatStartedAt + overheatLockDuration;
        overheatStartHeat = Mathf.Max(currentHeat, overheatThreshold);

        if (overheatLockDuration <= 0f)
        {
            currentHeat = 0f;
            isOverheated = false;
            overheatStartedAt = InvalidPastTime;
            overheatEndsAt = InvalidPastTime;
            overheatStartHeat = 0f;
        }
    }

    private void UpdateHeatState()
    {
        if (isOverheated)
        {
            if (overheatLockDuration <= 0f)
            {
                currentHeat = 0f;
                isOverheated = false;
                overheatStartedAt = InvalidPastTime;
                overheatEndsAt = InvalidPastTime;
                overheatStartHeat = 0f;
                return;
            }

            float t = Mathf.InverseLerp(overheatStartedAt, overheatEndsAt, Time.time);
            currentHeat = Mathf.Lerp(overheatStartHeat, 0f, t);

            if (Time.time >= overheatEndsAt || currentHeat <= HeatZeroEpsilon)
            {
                currentHeat = 0f;
                isOverheated = false;
                overheatStartedAt = InvalidPastTime;
                overheatEndsAt = InvalidPastTime;
                overheatStartHeat = 0f;
            }

            return;
        }

        if (currentHeat > HeatZeroEpsilon)
            currentHeat = Mathf.MoveTowards(currentHeat, 0f, passiveCoolRate * Time.deltaTime);
        else
            currentHeat = 0f;
    }

    private void RegisterDryFireEvent()
    {
        dryFireEventVersion++;
    }

    private float GetMinShotInterval()
    {
        if (maxShotsPerSecond <= 0f)
            return 0f;

        return 1f / maxShotsPerSecond;
    }

    private void UpdateAimState()
    {
        currentShotDirection = ResolveShotDirection();
        currentAimAngleZ = DirectionToWorldZAngleDeg(currentShotDirection);

        if (aimRig != null)
            aimRig.EvaluateHandForDirection(currentShotDirection, true);
    }

    private void SyncAimTransformImmediately()
    {
        currentShotDirection = ResolveShotDirection();
        currentAimAngleZ = DirectionToWorldZAngleDeg(currentShotDirection);

        if (aimRig != null)
            aimRig.EvaluateHandForDirection(currentShotDirection, true);

        ApplyAllFirePivotWorldTransforms();
    }

    private void ExecutePendingShot()
    {
        hasPendingShot = false;
        ApplyAllFirePivotWorldTransforms();
        TryCommitShot(pendingShotDirection, pendingShotUsesLeftHand);
    }

    private Vector3 ResolveShotDirection()
    {
        return ResolveShotDirection(ReadRawMove());
    }

    private Vector3 ResolveShotDirection(Vector2 rawMove)
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

    private Vector2 ReadRawMove()
    {
        if (moveAction == null || !moveAction.enabled)
            return Vector2.zero;

        return moveAction.ReadValue<Vector2>();
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

    private void SpawnProjectile(Transform sourcePoint, Vector3 shotDirection)
    {
        Quaternion shotRotation = Quaternion.Euler(0f, 0f, DirectionToWorldZAngleDeg(shotDirection));
        StraightProjectile projectileInstance = Instantiate(projectilePrefab, sourcePoint.position, shotRotation, projectileParent);
        projectileInstance.SetOwnerRoot(transform.root);

        if (overrideProjectileHitPayload)
        {
            projectileInstance.InitializeHitPayload(
                projectileDamage,
                projectileStunDuration,
                projectileKnockbackHorizontal,
                projectileKnockbackVertical,
                projectileKnockbackHorizontalOnly,
                preserveTargetVerticalVelocityOnProjectileHit);
        }

        projectileInstance.Launch(shotDirection);
    }

    private void GenerateImpulseIfNeeded(Vector3 shotDirection)
    {
        if (!generateImpulseOnShot || impulseSource == null)
            return;

        Vector3 recoilVelocity = -shotDirection * impulseMagnitude;
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

    private static float DirectionToWorldZAngleDeg(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return 0f;

        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugDrawAim)
            return;

        Transform sourcePoint = CurrentShotUsesLeftHand ? leftFirePoint : rightFirePoint;
        if (sourcePoint == null)
            sourcePoint = CurrentShotUsesLeftHand ? leftFirePivot : rightFirePivot;
        if (sourcePoint == null)
            return;

        Gizmos.color = CurrentShotUsesLeftHand ? Color.cyan : Color.magenta;
        Gizmos.DrawLine(sourcePoint.position, sourcePoint.position + currentShotDirection.normalized * Mathf.Max(0.01f, debugRayLength));
        Gizmos.DrawSphere(sourcePoint.position + currentShotDirection.normalized * Mathf.Max(0.01f, debugRayLength), 0.03f);
    }
}
