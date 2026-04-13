using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public sealed class CharacterProjectileShooter25D : MonoBehaviour
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
    [SerializeField] private float maxShotsPerSecond = 6f;
    [SerializeField] private bool spawnPendingShotInLateUpdate = true;

    [Header("Shoot Lock After Jumps")]
    [SerializeField] private bool blockShootingAfterSingleJump = true;
    [SerializeField] private float singleJumpShootLockTime = 0.1f;
    [SerializeField] private bool blockShootingAfterDoubleJump = true;
    [SerializeField] private float doubleJumpShootLockTime = 0.14f;
    [SerializeField] private bool blockShootingAfterWallJump = true;
    [SerializeField] private float wallJumpShootLockTime = 0.12f;

    [Header("Impulse (Optional)")]
    [SerializeField] private bool generateImpulseOnShot = false;
    [SerializeField] private float impulseMagnitude = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool debugDrawAim;
    [SerializeField] private float debugRayLength = 1.25f;

    private const float InvalidPastTime = -999f;

    private InputActionMap resolvedActionMap;
    private InputAction moveAction;
    private InputAction attackAction;

    private float nextAllowedShotTime = InvalidPastTime;
    private float currentAimAngleZ;
    private Vector3 currentShotDirection = Vector3.right;

    private bool hasPendingShot;
    private Vector3 pendingShotDirection = Vector3.right;
    private bool pendingShotUsesLeftHand;
    private Vector2 pendingShotRawMove;

    private float shootBlockedUntilTime = InvalidPastTime;
    private int lastObservedSelfJumpVersion = -1;

    public float CurrentAimAngleZ => currentAimAngleZ;
    public Vector3 CurrentShotDirection => currentShotDirection;
    public bool CurrentShotUsesLeftHand => aimRig != null ? aimRig.UsesLeftRig : true;

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

    private void ClampSettings()
    {
        moveAimDeadzone = Mathf.Clamp01(moveAimDeadzone);
        impulseMagnitude = Mathf.Max(0f, impulseMagnitude);
        singleJumpShootLockTime = Mathf.Max(0f, singleJumpShootLockTime);
        doubleJumpShootLockTime = Mathf.Max(0f, doubleJumpShootLockTime);
        wallJumpShootLockTime = Mathf.Max(0f, wallJumpShootLockTime);
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

        if (aimRig != null)
            aimRig.NotifyShotDirection(shotDirection);

        if (spawnPendingShotInLateUpdate)
        {
            hasPendingShot = true;
            pendingShotDirection = shotDirection;
            pendingShotUsesLeftHand = shotUsesLeftHand;
            pendingShotRawMove = rawMoveAtShot;
        }
        else
        {
            ApplyAllFirePivotWorldTransforms();
            ExecuteShot(shotDirection, shotUsesLeftHand);
        }

        nextAllowedShotTime = Time.time + GetMinShotInterval();
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
        ExecuteShot(pendingShotDirection, pendingShotUsesLeftHand);
    }

    private void ExecuteShot(Vector3 shotDirection, bool usesLeftHand)
    {
        SpawnProjectile(shotDirection, usesLeftHand);
        GenerateImpulseIfNeeded(shotDirection);
        NotifyAirShotPhysicsIfNeeded(shotDirection);
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

    private bool TryGetRawMove(out Vector2 move)
    {
        move = ReadRawMove();
        return move.sqrMagnitude > moveAimDeadzone * moveAimDeadzone;
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

    private void SpawnProjectile(Vector3 shotDirection, bool usesLeftHand)
    {
        if (projectilePrefab == null)
            return;

        Transform sourcePoint = usesLeftHand ? leftFirePoint : rightFirePoint;
        if (sourcePoint == null)
            return;

        Quaternion shotRotation = Quaternion.Euler(0f, 0f, DirectionToWorldZAngleDeg(shotDirection));
        StraightProjectile projectileInstance = Instantiate(projectilePrefab, sourcePoint.position, shotRotation, projectileParent);
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
