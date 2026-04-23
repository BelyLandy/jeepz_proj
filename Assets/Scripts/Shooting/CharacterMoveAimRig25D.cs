using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public sealed class CharacterMoveAimRig25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RBCharacter25D character;
    [SerializeField] private CharacterFacingResolver25D facingResolver;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerControlLock25D controlLock;
    [SerializeField] private CharacterPlasmaGlove25D plasmaGlove;
    [SerializeField] private CharacterCrouch25D crouch;
    [SerializeField] private Rig leftHandRig;
    [SerializeField] private Rig rightHandRig;
    [SerializeField] private Rig headAimRig;
    [SerializeField] private MultiRotationConstraint headAimConstraint;
    [SerializeField] private Transform leftArmAimObject;
    [SerializeField] private Transform rightArmAimObject;
    [SerializeField] private Transform headAimObject;
    [SerializeField] private MultiRotationConstraint neckAlternateHandConstraint;
    [SerializeField] private HeadTargetTracking25D headTargetTracking;

    [Header("Action Lookup")]
    [SerializeField] private bool useCurrentActionMap = true;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";

    [Header("Aim")]
    [SerializeField, Range(0f, 1f)] private float moveAimDeadzone = 0.1f;
    [SerializeField] private float leftLocalAngleOffsetX = 0f;
    [SerializeField] private float rightLocalAngleOffsetX = 0f;
    [SerializeField] private float headForwardLocalZ = -59.614f;
    [SerializeField] private float headLocalAngleOffsetZ = 0f;
    [SerializeField] private float headAimSwitchBlendTime = 0.08f;
    [SerializeField] private float neckAlternateHandConstraintOffsetX = 60f;
    [SerializeField, Min(0.001f)] private float neckAlternateHandBlendTime = 0.12f;
    [SerializeField] private float leftArmLockStanceRestLocalY = 67.469f;
    [SerializeField] private float rightArmLockStanceRestLocalY = -67.469f;

    [Header("Movement Shot Fade")]
    [Tooltip("Вне LockStance после выстрела активная рука уходит из 1 в 0 за это время.")]
    [SerializeField] private float movementShotFadeOutTime = 0.22f;

    [Header("Crouch Shot")]
    [Tooltip("Если герой сидит на авторизованном slope и смотрит вверх по склону, crouch-shot без LockStance будет идти вдоль подъёма склона.")]
    [SerializeField] private bool enableSlopeAdjustedCrouchShot = true;

    [Header("Hand Switching")]
    [Tooltip("Какая рука считается передней, когда герой смотрит вправо. При взгляде влево логика зеркалится автоматически.")]
    [SerializeField] private bool frontHemisphereUsesLeftRigWhenFacingRight = true;
    [Tooltip("Запас около границы 90° / -90°, чтобы рука не дёргалась туда-сюда на переходе через полуплоскость.")]
    [SerializeField, Range(0f, 1f)] private float hemisphereSwitchHysteresis = 0.1f;
    [Tooltip("За сколько уже неактивная рука плавно уходит из текущего веса в 0 после переключения активной руки.")]
    [SerializeField] private float inactiveHandFadeOutTime = 0.12f;

    [Header("Head Look")]
    [SerializeField] private float headLookFadeInTime = 0.22f;
    [SerializeField] private float headLookFadeOutTime = 0.08f;
    [SerializeField] private float headLookResumeDelay = 0.16f;
    [SerializeField] private float headLookAngleBlendTime = 0.12f;
    [SerializeField] private float headLookShotMovementSpeedThreshold = 0.15f;

    [Header("LockStance")]
    [Tooltip("Если в LockStance игрок полностью отпустил стик, активная рука уходит из текущего веса в 0 за это время.")]
    [SerializeField] private float lockStanceReleaseFadeOutTime = 0.18f;
    [Tooltip("При выстреле в LockStance во время прицеливания активная рука быстро опускается до этого веса, а потом возвращается в 1.")]
    [SerializeField, Range(0f, 1f)] private float lockStanceShotDipWeight = 0.5f;
    [SerializeField] private float lockStanceShotDipDownTime = 0.05f;
    [SerializeField] private float lockStanceShotRecoverTime = 0.08f;

    private InputActionMap resolvedActionMap;
    private InputAction moveAction;

    private float currentAimAngleDeg = -90f;
    private float heldAimAngleDeg = -90f;
    private Vector3 heldShotDirection = Vector3.right;

    private Vector3 leftArmBaseLocalEuler;
    private Vector3 rightArmBaseLocalEuler;
    private Vector3 headBaseLocalEuler;
    private bool hasLeftArmBaseLocalEuler;
    private bool hasRightArmBaseLocalEuler;
    private bool hasHeadBaseLocalEuler;

    private bool currentUsesLeftRig;
    private bool hasHandSelection;

    private float primaryVisibleWeight;
    private float leftRuntimeWeight;
    private float rightRuntimeWeight;
    private float headRuntimeWeight;
    private float neckAlternateRuntimeWeight;
    private float neckAlternateLatchedOffsetX;
    private bool hasNeckAlternateLatchedOffset;

    private bool lockStanceShotPulseActive;
    private float lockStanceShotPulseStartedAt;

    private bool freezeLeftArmPose;
    private bool freezeRightArmPose;
    private Quaternion frozenLeftArmLocalRotation;
    private Quaternion frozenRightArmLocalRotation;

    private bool freezeHeadPose;
    private Quaternion frozenHeadLocalRotation;
    private float currentHeadRelativeAimAngleZ;
    private bool hasCurrentHeadRelativeAimAngle;
    private bool wasLockStyleAimActiveLastFrame;

    private float headLookRuntimeWeight;
    private float headLookLatchedRelativeAimAngleZ;
    private bool hasHeadLookLatchedRelativeAimAngle;
    private float headLookSuppressedUntilTime = float.NegativeInfinity;

    public float CurrentAimAngleDeg => currentAimAngleDeg;
    public bool UsesLeftRig => currentUsesLeftRig;
    public bool UsesRightRig => !currentUsesLeftRig;
    public float CurrentPrimaryVisibleWeight => primaryVisibleWeight;
    public float LeftCurrentRigWeight => leftRuntimeWeight;
    public float RightCurrentRigWeight => rightRuntimeWeight;
    public float HeadCurrentRigWeight => headRuntimeWeight;

    private void Reset()
    {
        if (character == null)
            character = GetComponent<RBCharacter25D>();

        CacheFacingResolver();

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();

        if (plasmaGlove == null)
            plasmaGlove = GetComponent<CharacterPlasmaGlove25D>();

        if (crouch == null)
            crouch = GetComponent<CharacterCrouch25D>();

        if (headTargetTracking == null)
            headTargetTracking = GetComponent<HeadTargetTracking25D>();
    }

    private void Awake()
    {
        if (character == null)
            character = GetComponent<RBCharacter25D>();

        CacheFacingResolver();
        ResetNeckAlternateHandConstraint();

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();

        if (plasmaGlove == null)
            plasmaGlove = GetComponent<CharacterPlasmaGlove25D>();

        if (crouch == null)
            crouch = GetComponent<CharacterCrouch25D>();

        if (headTargetTracking == null)
            headTargetTracking = GetComponent<HeadTargetTracking25D>();

        ClampSettings();
        CacheBaseAimRotations();
        TryAutoAssignHeadAimConstraint();
        ApplyRigWeightsImmediate(0f, 0f, 0f);
    }

    private void OnValidate()
    {
        ClampSettings();

        if (character == null)
            character = GetComponent<RBCharacter25D>();

        CacheFacingResolver();

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        CacheFacingResolver();

        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();

        if (plasmaGlove == null)
            plasmaGlove = GetComponent<CharacterPlasmaGlove25D>();

        if (crouch == null)
            crouch = GetComponent<CharacterCrouch25D>();

        if (headTargetTracking == null)
            headTargetTracking = GetComponent<HeadTargetTracking25D>();

        if (string.IsNullOrWhiteSpace(actionMapName))
            actionMapName = "Player";

        if (string.IsNullOrWhiteSpace(moveActionName))
            moveActionName = "Move";

        CacheBaseAimRotations();
        TryAutoAssignHeadAimConstraint();
    }

    private void OnEnable()
    {
        ResolveActions(forceResubscribe: true);
        UpdateAimVisual();
    }

    private void OnDisable()
    {
        lockStanceShotPulseActive = false;
        primaryVisibleWeight = 0f;
        leftRuntimeWeight = 0f;
        rightRuntimeWeight = 0f;
        headRuntimeWeight = 0f;
        neckAlternateRuntimeWeight = 0f;
        hasNeckAlternateLatchedOffset = false;
        neckAlternateLatchedOffsetX = 0f;
        ClearArmPoseFreezes();
        ClearHeadPoseFreeze();
        hasCurrentHeadRelativeAimAngle = false;
        currentHeadRelativeAimAngleZ = 0f;
        wasLockStyleAimActiveLastFrame = false;
        headLookRuntimeWeight = 0f;
        headLookLatchedRelativeAimAngleZ = 0f;
        hasHeadLookLatchedRelativeAimAngle = false;
        headLookSuppressedUntilTime = float.NegativeInfinity;
        ApplyRigWeightsImmediate(0f, 0f, 0f);
        ResetNeckAlternateHandConstraint();
    }

    private void Update()
    {
        ResolveActions(forceResubscribe: false);
        UpdateAimVisual();
    }

    private void ClampSettings()
    {
        moveAimDeadzone = Mathf.Clamp01(moveAimDeadzone);
        movementShotFadeOutTime = Mathf.Max(0.0001f, movementShotFadeOutTime);
        inactiveHandFadeOutTime = Mathf.Max(0.0001f, inactiveHandFadeOutTime);
        lockStanceReleaseFadeOutTime = Mathf.Max(0.0001f, lockStanceReleaseFadeOutTime);
        hemisphereSwitchHysteresis = Mathf.Clamp01(hemisphereSwitchHysteresis);
        lockStanceShotDipWeight = Mathf.Clamp01(lockStanceShotDipWeight);
        lockStanceShotDipDownTime = Mathf.Max(0.0001f, lockStanceShotDipDownTime);
        lockStanceShotRecoverTime = Mathf.Max(0.0001f, lockStanceShotRecoverTime);
        headAimSwitchBlendTime = Mathf.Max(0.0001f, headAimSwitchBlendTime);
        neckAlternateHandConstraintOffsetX = Mathf.Max(0f, neckAlternateHandConstraintOffsetX);
        neckAlternateHandBlendTime = Mathf.Max(0.001f, neckAlternateHandBlendTime);
        headLookFadeInTime = Mathf.Max(0.0001f, headLookFadeInTime);
        headLookFadeOutTime = Mathf.Max(0.0001f, headLookFadeOutTime);
        headLookResumeDelay = Mathf.Max(0f, headLookResumeDelay);
        headLookAngleBlendTime = Mathf.Max(0.0001f, headLookAngleBlendTime);
        headLookShotMovementSpeedThreshold = Mathf.Max(0f, headLookShotMovementSpeedThreshold);
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

        resolvedActionMap = targetMap;
        moveAction = resolvedActionMap != null ? resolvedActionMap.FindAction(moveActionName, false) : null;
    }

    private bool CanPresentWeaponPoseNow()
    {
        return plasmaGlove == null || plasmaGlove.CanPresentWeaponPoseNow;
    }

    private bool IsGroundLockStylePoseActive()
    {
        return character != null && character.IsLockStanceMovementActive;
    }

    private bool IsWallSlideLockStylePoseCandidate()
    {
        return character != null && character.IsWallSliding && character.IsLockStanceHeld;
    }

    private bool UseLockStylePose()
    {
        return IsGroundLockStylePoseActive() || IsWallSlideLockStylePoseCandidate();
    }

    private void UpdateAimVisual()
    {
        bool isControlLocked = controlLock != null && controlLock.IsControlLocked;
        bool isGroundLockStylePose = IsGroundLockStylePoseActive();
        bool isWallSlideLockStylePose = IsWallSlideLockStylePoseCandidate();
        bool useLockStylePose = isGroundLockStylePose || isWallSlideLockStylePose;
        bool isCrouching = crouch != null && crouch.IsCrouching;
        bool suppressVisualAimWhileCrouching = isCrouching && !useLockStylePose;

        Vector2 moveAim = Vector2.zero;
        bool hasMoveAim = !isControlLocked && !suppressVisualAimWhileCrouching && TryGetRawMoveAim(out moveAim);
        bool weaponPoseAllowed = CanPresentWeaponPoseNow();
        bool hasWeaponPoseAim = hasMoveAim && weaponPoseAllowed;
        bool isLockStyleAimActive = useLockStylePose && hasWeaponPoseAim;

        Vector3 aimDirection;
        if (hasMoveAim)
        {
            aimDirection = new Vector3(moveAim.x, moveAim.y, 0f).normalized;
        }
        else
        {
            aimDirection = heldShotDirection.sqrMagnitude > 0.0001f ? heldShotDirection : GetFallbackShotDirection();
            aimDirection.z = 0f;
            if (aimDirection.sqrMagnitude <= 0.0001f)
                aimDirection = GetFallbackShotDirection();
            aimDirection.Normalize();
        }

        aimDirection = ResolveDirectionForCurrentState(aimDirection);

        if (!isControlLocked)
            EvaluateHandForDirection(aimDirection, true);

        currentAimAngleDeg = DirectionToVisualAngleDeg(aimDirection);
        float combatAimAngleZ = DirectionToCombatAimAngleZ(aimDirection);
        float headRelativeAimAngleZ = DirectionToHeadRelativeAimAngleZ(aimDirection, GetHeadAimReferenceFacingSign());
        heldAimAngleDeg = currentAimAngleDeg;
        heldShotDirection = aimDirection;

        UpdatePrimaryVisibleWeight(useLockStylePose, isLockStyleAimActive);
        bool hasExplicitHeadAim = isLockStyleAimActive || hasWeaponPoseAim;
        ApplyVisualAimAndWeights(currentAimAngleDeg, combatAimAngleZ, headRelativeAimAngleZ, hasExplicitHeadAim, isControlLocked, useLockStylePose, isLockStyleAimActive, weaponPoseAllowed);
    }

    public void NotifyShotDirection(Vector3 shotDirection)
    {
        if (!CanPresentWeaponPoseNow())
            return;

        if (shotDirection.sqrMagnitude <= 0.0001f)
            shotDirection = GetFallbackShotDirection();

        shotDirection = ResolveShotDirectionForCurrentState(shotDirection);

        EvaluateHandForDirection(shotDirection, true);

        currentAimAngleDeg = DirectionToVisualAngleDeg(shotDirection);
        float combatAimAngleZ = DirectionToCombatAimAngleZ(shotDirection);
        float headRelativeAimAngleZ = DirectionToHeadRelativeAimAngleZ(shotDirection, GetHeadAimReferenceFacingSign());
        heldAimAngleDeg = currentAimAngleDeg;
        heldShotDirection = shotDirection;

        primaryVisibleWeight = 1f;

        bool isControlLocked = controlLock != null && controlLock.IsControlLocked;
        bool isGroundLockStylePose = IsGroundLockStylePoseActive();
        bool isWallSlideLockStylePose = IsWallSlideLockStylePoseCandidate();
        bool useLockStylePose = isGroundLockStylePose || isWallSlideLockStylePose;

        bool isCrouching = crouch != null && crouch.IsCrouching;
        bool suppressVisualAimWhileCrouching = isCrouching && !useLockStylePose;

        bool hasMoveAim = !isControlLocked && !suppressVisualAimWhileCrouching && TryGetRawMoveAim(out _);
        bool weaponPoseAllowed = CanPresentWeaponPoseNow();
        bool hasWeaponPoseAim = hasMoveAim && weaponPoseAllowed;
        bool isLockStyleAimActive = useLockStylePose && hasWeaponPoseAim;

        if (isLockStyleAimActive)
        {
            lockStanceShotPulseActive = true;
            lockStanceShotPulseStartedAt = Time.time;
        }
        else
        {
            lockStanceShotPulseActive = false;
        }

        if (ShouldSuppressHeadLookForShot())
            headLookSuppressedUntilTime = Time.time + headLookResumeDelay;

        ApplyVisualAimAndWeights(currentAimAngleDeg, combatAimAngleZ, headRelativeAimAngleZ, true, isControlLocked, useLockStylePose, isLockStyleAimActive, true);
    }

    public bool EvaluateHandForDirection(Vector3 direction, bool updateState)
    {
        direction = ResolveShotDirectionForCurrentState(direction);

        Vector2 aimDirection = new Vector2(direction.x, direction.y);
        if (aimDirection.sqrMagnitude <= 0.0001f)
            aimDirection = GetFacingDirection2D();
        else
            aimDirection.Normalize();

        bool frontUsesLeft = GetFrontHemisphereUsesLeftForCurrentFacing();
        bool selectedUsesLeft;

        if (IsWallSlideFacingOverrideActive())
        {
            selectedUsesLeft = frontUsesLeft;
        }
        else
        {
            Vector2 facingDirection = GetFacingDirection2D();
            float frontDot = Vector2.Dot(aimDirection, facingDirection);

            if (!hasHandSelection)
            {
                selectedUsesLeft = frontDot >= 0f ? frontUsesLeft : !frontUsesLeft;
            }
            else
            {
                bool currentIsFrontHand = currentUsesLeftRig == frontUsesLeft;

                if (currentIsFrontHand)
                    selectedUsesLeft = frontDot < -hemisphereSwitchHysteresis ? !frontUsesLeft : frontUsesLeft;
                else
                    selectedUsesLeft = frontDot > hemisphereSwitchHysteresis ? frontUsesLeft : !frontUsesLeft;
            }
        }

        if (updateState)
        {
            bool hadSelectionBefore = hasHandSelection;
            bool previousUsesLeftRig = currentUsesLeftRig;

            currentUsesLeftRig = selectedUsesLeft;
            hasHandSelection = true;

            if (ShouldFreezeOutgoingHandOnSwitch(hadSelectionBefore, previousUsesLeftRig, selectedUsesLeft))
                BeginInactiveHandPoseFreeze(previousUsesLeftRig);

            ClearFreezeForActiveHand();
        }

        return selectedUsesLeft;
    }

    private void UpdatePrimaryVisibleWeight(bool useLockStylePose, bool isLockStyleAimActive)
    {
        if (isLockStyleAimActive)
        {
            if (lockStanceShotPulseActive)
                primaryVisibleWeight = EvaluateLockStanceShotPulseWeight();
            else
                primaryVisibleWeight = 1f;
            return;
        }

        lockStanceShotPulseActive = false;

        float fadeDuration = useLockStylePose ? lockStanceReleaseFadeOutTime : movementShotFadeOutTime;
        primaryVisibleWeight = MoveTowardsByDuration(primaryVisibleWeight, 0f, fadeDuration);
    }

    private float EvaluateLockStanceShotPulseWeight()
    {
        float elapsed = Time.time - lockStanceShotPulseStartedAt;
        float total = lockStanceShotDipDownTime + lockStanceShotRecoverTime;

        if (elapsed <= 0f)
            return 1f;

        if (elapsed >= total)
        {
            lockStanceShotPulseActive = false;
            return 1f;
        }

        if (elapsed <= lockStanceShotDipDownTime)
        {
            float t = lockStanceShotDipDownTime <= 0.0001f ? 1f : elapsed / lockStanceShotDipDownTime;
            return Mathf.Lerp(1f, lockStanceShotDipWeight, t);
        }

        float recoverElapsed = elapsed - lockStanceShotDipDownTime;
        float recoverT = lockStanceShotRecoverTime <= 0.0001f ? 1f : recoverElapsed / lockStanceShotRecoverTime;
        return Mathf.Lerp(lockStanceShotDipWeight, 1f, recoverT);
    }

    private float MoveTowardsByDuration(float current, float target, float duration)
    {
        if (Mathf.Approximately(current, target))
            return target;

        if (duration <= 0.0001f)
            return target;

        float maxDelta = Time.deltaTime / duration;
        return Mathf.MoveTowards(current, target, maxDelta);
    }

    private float GetNeckAlternateHandBlendTime()
    {
        if (neckAlternateHandBlendTime > 0.0001f)
            return neckAlternateHandBlendTime;

        return Mathf.Max(0.0001f, lockStanceReleaseFadeOutTime);
    }

    private bool TryGetRawMoveAim(out Vector2 move)
    {
        move = Vector2.zero;

        if (moveAction == null || !moveAction.enabled)
            return false;

        move = moveAction.ReadValue<Vector2>();
        return move.sqrMagnitude > moveAimDeadzone * moveAimDeadzone;
    }

    public Vector3 ResolveDirectionForCurrentState(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            direction = GetFallbackShotDirection();

        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = GetFallbackShotDirection();

        direction.Normalize();

        if (!IsWallSlideFacingOverrideActive())
            return direction;

        Vector2 facingDirection = GetFacingDirection2D();
        Vector2 aimDirection = new Vector2(direction.x, direction.y);
        float frontDot = Vector2.Dot(aimDirection, facingDirection);
        if (frontDot >= 0f)
            return direction;

        Vector2 clamped = aimDirection - facingDirection * frontDot;
        if (clamped.sqrMagnitude <= 0.0001f)
            clamped = facingDirection;
        else
            clamped.Normalize();

        return new Vector3(clamped.x, clamped.y, 0f);
    }

    public Vector3 ResolveShotDirectionForCurrentState(Vector3 direction)
    {
        bool isLockStance = character != null && character.IsLockStanceMovementActive;
        bool shouldForceCrouchForwardShot = crouch != null && crouch.IsCrouching && !isLockStance;

        if (shouldForceCrouchForwardShot)
        {
            if (!TryGetSlopeAdjustedCrouchShotDirection(out direction))
                direction = GetFallbackShotDirection();
        }

        return ResolveDirectionForCurrentState(direction);
    }

    private bool TryGetSlopeAdjustedCrouchShotDirection(out Vector3 direction)
    {
        direction = default;

        if (!enableSlopeAdjustedCrouchShot || character == null)
            return false;
        if (!character.IsGroundedNow)
            return false;

        SurfaceContacts25D contacts = character.LastSurfaceContacts;
        if (!contacts.OnSlope || !contacts.IsSlopeSurfaceAuthorized)
            return false;

        Vector3 uphillDirection = contacts.SlopeTangent * -contacts.DownhillSign;
        uphillDirection.z = 0f;
        if (uphillDirection.sqrMagnitude <= 0.0001f)
            return false;

        int uphillFacingSign = uphillDirection.x >= 0f ? +1 : -1;
        if (GetEffectiveFacingSign() != uphillFacingSign)
            return false;

        direction = uphillDirection.normalized;
        return true;
    }

    private Vector3 GetFallbackShotDirection()
    {
        int facingSign = GetEffectiveFacingSign();
        return facingSign >= 0 ? Vector3.right : Vector3.left;
    }

    private bool GetFrontHemisphereUsesLeftForCurrentFacing()
    {
        int facingSign = GetEffectiveFacingSign();
        return facingSign >= 0
            ? frontHemisphereUsesLeftRigWhenFacingRight
            : !frontHemisphereUsesLeftRigWhenFacingRight;
    }

    private Vector2 GetFacingDirection2D()
    {
        int facingSign = GetEffectiveFacingSign();
        return facingSign >= 0 ? Vector2.right : Vector2.left;
    }

    private int GetEffectiveFacingSign()
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

    private bool IsWallSlideFacingOverrideActive()
    {
        return character != null && character.IsWallSliding && character.WallSlideSide != 0;
    }

    private static float DirectionToVisualAngleDeg(Vector3 direction)
    {
        return MoveToVisualAngleDeg(new Vector2(direction.x, direction.y));
    }

    private static float DirectionToCombatAimAngleZ(Vector3 direction)
    {
        Vector2 aim = new Vector2(direction.x, direction.y);
        if (aim.sqrMagnitude <= 0.0001f)
            return 0f;

        return Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
    }

    private static float DirectionToHeadRelativeAimAngleZ(Vector3 direction, int facingSign)
    {
        Vector2 aim = new Vector2(direction.x, direction.y);
        if (aim.sqrMagnitude <= 0.0001f)
            return 0f;

        aim.Normalize();
        float relativeX = facingSign >= 0 ? aim.x : -aim.x;
        return Mathf.Atan2(aim.y, relativeX) * Mathf.Rad2Deg;
    }

    private static float MoveToVisualAngleDeg(Vector2 move)
    {
        float angle = Mathf.Atan2(-move.x, -move.y) * Mathf.Rad2Deg;
        return NormalizeSignedAngleDeg(angle);
    }

    private static float NormalizeSignedAngleDeg(float angleDeg)
    {
        angleDeg %= 360f;

        if (angleDeg <= -180f)
            angleDeg += 360f;
        else if (angleDeg > 180f)
            angleDeg -= 360f;

        if (Mathf.Abs(angleDeg - 180f) <= 0.001f)
            return -180f;

        return angleDeg;
    }

    private float DetermineDesiredDisplayedHeadRelativeAimAngle(float baseHeadRelativeAimAngleZ, bool isControlLocked, bool useLockStylePose, bool isLockStyleAimActive)
    {
        bool shouldUseHeadLook = TryGetDesiredHeadLookRelativeAimAngle(isControlLocked, useLockStylePose, isLockStyleAimActive, out float targetHeadLookRelativeAimAngleZ);
        UpdateHeadLookRuntime(shouldUseHeadLook, targetHeadLookRelativeAimAngleZ);

        if (isLockStyleAimActive)
            return baseHeadRelativeAimAngleZ;

        if (shouldUseHeadLook && hasHeadLookLatchedRelativeAimAngle)
            return headLookLatchedRelativeAimAngleZ;

        if (headLookRuntimeWeight > 0.001f && hasHeadLookLatchedRelativeAimAngle)
            return Mathf.LerpAngle(baseHeadRelativeAimAngleZ, headLookLatchedRelativeAimAngleZ, headLookRuntimeWeight);

        return baseHeadRelativeAimAngleZ;
    }

    private bool TryGetDesiredHeadLookRelativeAimAngle(bool isControlLocked, bool useLockStylePose, bool isLockStyleAimActive, out float headLookRelativeAimAngleZ)
    {
        headLookRelativeAimAngleZ = 0f;

        if (headTargetTracking == null)
            return false;

        if (isControlLocked || useLockStylePose || isLockStyleAimActive)
            return false;

        if (Time.time < headLookSuppressedUntilTime)
            return false;

        int facingSign = GetEffectiveFacingSign();
        if (!headTargetTracking.TryGetLookDirection(facingSign, out Vector3 lookDirection))
            return false;

        headLookRelativeAimAngleZ = DirectionToHeadRelativeAimAngleZ(lookDirection, facingSign);
        return true;
    }

    private void UpdateHeadLookRuntime(bool shouldUseHeadLook, float targetHeadLookRelativeAimAngleZ)
    {
        float targetWeight = shouldUseHeadLook ? 1f : 0f;
        float duration = targetWeight > headLookRuntimeWeight ? headLookFadeInTime : headLookFadeOutTime;
        headLookRuntimeWeight = MoveTowardsByDuration(headLookRuntimeWeight, targetWeight, duration);

        if (shouldUseHeadLook)
        {
            if (!hasHeadLookLatchedRelativeAimAngle)
            {
                headLookLatchedRelativeAimAngleZ = targetHeadLookRelativeAimAngleZ;
                hasHeadLookLatchedRelativeAimAngle = true;
            }
            else
            {
                headLookLatchedRelativeAimAngleZ = SmoothTowardsAngleByDuration(headLookLatchedRelativeAimAngleZ, targetHeadLookRelativeAimAngleZ, headLookAngleBlendTime);
            }

            return;
        }

        if (headLookRuntimeWeight <= 0.001f)
        {
            headLookRuntimeWeight = 0f;
            headLookLatchedRelativeAimAngleZ = 0f;
            hasHeadLookLatchedRelativeAimAngle = false;
        }
    }

    private bool ShouldSuppressHeadLookForShot()
    {
        if (headLookResumeDelay <= 0f)
            return false;

        return GetCurrentHeadLookMovementSpeed() >= headLookShotMovementSpeedThreshold;
    }

    private float GetCurrentHeadLookMovementSpeed()
    {
        float speed = 0f;

        if (character != null)
        {
            speed = Mathf.Max(speed, character.HorizontalSpeedAbs);
            speed = Mathf.Max(speed, Mathf.Abs(character.SmoothedInputX));

            Rigidbody characterRb = character.RigidbodyComponent;
            if (characterRb != null)
                speed = Mathf.Max(speed, Mathf.Abs(characterRb.linearVelocity.x));
        }

        return speed;
    }

    private void TryAutoAssignHeadAimConstraint()
    {
        if (headAimConstraint != null)
            return;

        if (headAimRig == null)
            return;

        MultiRotationConstraint[] candidates = headAimRig.GetComponentsInChildren<MultiRotationConstraint>(true);
        if (candidates == null || candidates.Length == 0)
            return;

        if (headAimObject != null)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] == null)
                    continue;

                if (candidates[i].data.constrainedObject == headAimObject)
                {
                    headAimConstraint = candidates[i];
                    return;
                }
            }
        }

        if (candidates.Length == 1)
            headAimConstraint = candidates[0];
    }

    private void CacheBaseAimRotations()
    {
        if (leftArmAimObject != null)
        {
            leftArmBaseLocalEuler = leftArmAimObject.localEulerAngles;
            hasLeftArmBaseLocalEuler = true;
        }

        if (rightArmAimObject != null)
        {
            rightArmBaseLocalEuler = rightArmAimObject.localEulerAngles;
            hasRightArmBaseLocalEuler = true;
        }

        if (headAimObject != null)
        {
            headBaseLocalEuler = headAimObject.localEulerAngles;
            hasHeadBaseLocalEuler = true;
        }

    }

    private void ApplyVisualAimAndWeights(float angleDeg, float combatAimAngleZ, float headRelativeAimAngleZ, bool hasExplicitHeadAim, bool isControlLocked, bool useLockStylePose, bool isLockStyleAimActive, bool weaponPoseAllowed)
    {
        int facingSign = GetEffectiveFacingSign();
        float? leftLocalYOverride = GetArmLocalYOverride(useLockStylePose, weaponPoseAllowed, facingSign, manageLeftArm: true);
        float? rightLocalYOverride = GetArmLocalYOverride(useLockStylePose, weaponPoseAllowed, facingSign, manageLeftArm: false);

        ApplyArmLocalRotationWithOptionalFreeze(leftArmAimObject, ref hasLeftArmBaseLocalEuler, ref leftArmBaseLocalEuler, angleDeg + leftLocalAngleOffsetX, leftLocalYOverride, freezeLeftArmPose, frozenLeftArmLocalRotation);
        ApplyArmLocalRotationWithOptionalFreeze(rightArmAimObject, ref hasRightArmBaseLocalEuler, ref rightArmBaseLocalEuler, angleDeg + rightLocalAngleOffsetX, rightLocalYOverride, freezeRightArmPose, frozenRightArmLocalRotation);

        float baseDisplayedHeadRelativeAimAngleZ = hasExplicitHeadAim ? headRelativeAimAngleZ : 0f;
        float desiredDisplayedHeadRelativeAimAngleZ = DetermineDesiredDisplayedHeadRelativeAimAngle(baseDisplayedHeadRelativeAimAngleZ, isControlLocked, useLockStylePose, isLockStyleAimActive);
        UpdateHeadPoseState(desiredDisplayedHeadRelativeAimAngleZ, useLockStylePose, isLockStyleAimActive);
        float appliedHeadRelativeAimAngleZ = GetAppliedHeadRelativeAimAngleZ(desiredDisplayedHeadRelativeAimAngleZ);
        ApplyHeadLocalRotationWithOptionalFreeze(headAimObject, ref hasHeadBaseLocalEuler, ref headBaseLocalEuler, 0f, headForwardLocalZ - appliedHeadRelativeAimAngleZ + headLocalAngleOffsetZ, freezeHeadPose, frozenHeadLocalRotation);
        ApplyNeckAlternateHandConstraint(isLockStyleAimActive);

        if (currentUsesLeftRig)
        {
            leftRuntimeWeight = primaryVisibleWeight;
            rightRuntimeWeight = MoveTowardsByDuration(rightRuntimeWeight, 0f, inactiveHandFadeOutTime);
        }
        else
        {
            rightRuntimeWeight = primaryVisibleWeight;
            leftRuntimeWeight = MoveTowardsByDuration(leftRuntimeWeight, 0f, inactiveHandFadeOutTime);
        }

        headRuntimeWeight = Mathf.Max(primaryVisibleWeight, headLookRuntimeWeight);
        UpdateArmPoseFreezeState();
        UpdateHeadPoseFreezeState();
        ApplyRigWeightsImmediate(leftRuntimeWeight, rightRuntimeWeight, headRuntimeWeight);
    }


    private int GetHeadAimReferenceFacingSign()
    {
        int facingSign = GetEffectiveFacingSign();

        bool useLockStylePose = UseLockStylePose();
        if (!useLockStylePose || !hasHandSelection)
            return facingSign;

        bool frontUsesLeft = GetFrontHemisphereUsesLeftForCurrentFacing();
        bool isAlternateHand = currentUsesLeftRig != frontUsesLeft;
        return isAlternateHand ? -facingSign : facingSign;
    }

    private float? GetArmLocalYOverride(bool useLockStylePose, bool weaponPoseAllowed, int facingSign, bool manageLeftArm)
    {
        bool shouldManageThisArm = facingSign >= 0 ? !manageLeftArm : manageLeftArm;
        if (!shouldManageThisArm)
            return null;

        float restY = manageLeftArm ? leftArmLockStanceRestLocalY : rightArmLockStanceRestLocalY;
        if (!useLockStylePose || !hasHandSelection || !weaponPoseAllowed)
            return restY;

        bool activeManagedArm = manageLeftArm ? currentUsesLeftRig : !currentUsesLeftRig;
        return activeManagedArm ? 0f : restY;
    }

    private static void ApplyArmLocalRotation(Transform target, ref bool hasBaseEuler, ref Vector3 baseEuler, float xAngle, float? yAngleOverride)
    {
        if (target == null)
            return;

        if (!hasBaseEuler)
        {
            baseEuler = target.localEulerAngles;
            hasBaseEuler = true;
        }

        Vector3 euler = baseEuler;
        euler.x = xAngle;
        if (yAngleOverride.HasValue)
            euler.y = yAngleOverride.Value;
        target.localRotation = Quaternion.Euler(euler);
    }

    private static void ApplyArmLocalRotationWithOptionalFreeze(Transform target, ref bool hasBaseEuler, ref Vector3 baseEuler, float xAngle, float? yAngleOverride, bool useFrozenRotation, Quaternion frozenRotation)
    {
        if (target == null)
            return;

        if (useFrozenRotation)
        {
            target.localRotation = frozenRotation;
            return;
        }

        ApplyArmLocalRotation(target, ref hasBaseEuler, ref baseEuler, xAngle, yAngleOverride);
    }

    private static void ApplyHeadLocalRotation(Transform target, ref bool hasBaseEuler, ref Vector3 baseEuler, float extraYAngle, float zAngle)
    {
        if (target == null)
            return;

        if (!hasBaseEuler)
        {
            baseEuler = target.localEulerAngles;
            hasBaseEuler = true;
        }

        Vector3 euler = baseEuler;
        euler.y = baseEuler.y + extraYAngle;
        euler.z = zAngle;
        target.localRotation = Quaternion.Euler(euler);
    }

    private static void ApplyHeadLocalRotationWithOptionalFreeze(Transform target, ref bool hasBaseEuler, ref Vector3 baseEuler, float extraYAngle, float zAngle, bool useFrozenRotation, Quaternion frozenRotation)
    {
        if (target == null)
            return;

        if (useFrozenRotation)
        {
            target.localRotation = frozenRotation;
            return;
        }

        ApplyHeadLocalRotation(target, ref hasBaseEuler, ref baseEuler, extraYAngle, zAngle);
    }

    private void UpdateHeadPoseState(float desiredHeadRelativeAimAngleZ, bool useLockStylePose, bool isLockStyleAimActive)
    {
        if (useLockStylePose && wasLockStyleAimActiveLastFrame && !isLockStyleAimActive)
            BeginHeadPoseFreeze();

        if (!useLockStylePose || isLockStyleAimActive)
            ClearHeadPoseFreeze();

        if (!hasCurrentHeadRelativeAimAngle)
        {
            currentHeadRelativeAimAngleZ = desiredHeadRelativeAimAngleZ;
            hasCurrentHeadRelativeAimAngle = true;
        }
        else if (!freezeHeadPose)
        {
            float duration = isLockStyleAimActive ? headAimSwitchBlendTime : headLookAngleBlendTime;
            currentHeadRelativeAimAngleZ = SmoothTowardsAngleByDuration(currentHeadRelativeAimAngleZ, desiredHeadRelativeAimAngleZ, duration);
        }

        wasLockStyleAimActiveLastFrame = isLockStyleAimActive;
    }

    private float GetAppliedHeadRelativeAimAngleZ(float desiredHeadRelativeAimAngleZ)
    {
        if (!hasCurrentHeadRelativeAimAngle)
            return desiredHeadRelativeAimAngleZ;

        return currentHeadRelativeAimAngleZ;
    }

    private static float SmoothTowardsAngleByDuration(float current, float target, float duration)
    {
        if (duration <= 0.0001f)
            return target;

        if (Mathf.Abs(Mathf.DeltaAngle(current, target)) <= 0.001f)
            return target;

        float t = 1f - Mathf.Exp(-Time.deltaTime / duration);
        return Mathf.LerpAngle(current, target, t);
    }


    private bool ShouldFreezeOutgoingHandOnSwitch(bool hadSelectionBefore, bool previousUsesLeftRig, bool newUsesLeftRig)
    {
        if (!hadSelectionBefore || previousUsesLeftRig == newUsesLeftRig)
            return false;

        return UseLockStylePose();
    }

    private void BeginInactiveHandPoseFreeze(bool outgoingUsesLeftRig)
    {
        if (outgoingUsesLeftRig)
        {
            if (leftArmAimObject == null)
                return;

            frozenLeftArmLocalRotation = leftArmAimObject.localRotation;
            freezeLeftArmPose = true;
            return;
        }

        if (rightArmAimObject == null)
            return;

        frozenRightArmLocalRotation = rightArmAimObject.localRotation;
        freezeRightArmPose = true;
    }

    private void ClearFreezeForActiveHand()
    {
        if (!hasHandSelection)
            return;

        if (currentUsesLeftRig)
            freezeLeftArmPose = false;
        else
            freezeRightArmPose = false;
    }

    private void UpdateArmPoseFreezeState()
    {
        ClearFreezeForActiveHand();

        if (freezeLeftArmPose && (leftArmAimObject == null || leftRuntimeWeight <= 0.001f))
            freezeLeftArmPose = false;

        if (freezeRightArmPose && (rightArmAimObject == null || rightRuntimeWeight <= 0.001f))
            freezeRightArmPose = false;
    }

    private void ClearArmPoseFreezes()
    {
        freezeLeftArmPose = false;
        freezeRightArmPose = false;
        frozenLeftArmLocalRotation = Quaternion.identity;
        frozenRightArmLocalRotation = Quaternion.identity;
    }

    private void BeginHeadPoseFreeze()
    {
        if (headAimObject == null)
            return;

        frozenHeadLocalRotation = headAimObject.localRotation;
        freezeHeadPose = true;
    }

    private void ClearHeadPoseFreeze()
    {
        freezeHeadPose = false;
        frozenHeadLocalRotation = Quaternion.identity;
    }

    private void UpdateHeadPoseFreezeState()
    {
        if (freezeHeadPose && (headAimObject == null || headRuntimeWeight <= 0.001f))
            ClearHeadPoseFreeze();
    }

    private void ApplyNeckAlternateHandConstraint(bool isLockStyleAimActive)
    {
        if (neckAlternateHandConstraint == null)
            return;

        bool shouldUseAlternateNeckPose = false;
        float targetOffsetX = 0f;

        if (isLockStyleAimActive && hasHandSelection)
        {
            bool frontUsesLeft = GetFrontHemisphereUsesLeftForCurrentFacing();
            bool isAlternateHand = currentUsesLeftRig != frontUsesLeft;
            if (isAlternateHand)
            {
                shouldUseAlternateNeckPose = true;
                int facingSign = GetEffectiveFacingSign();
                targetOffsetX = facingSign >= 0 ? -neckAlternateHandConstraintOffsetX : neckAlternateHandConstraintOffsetX;
                neckAlternateLatchedOffsetX = targetOffsetX;
                hasNeckAlternateLatchedOffset = true;
            }
        }

        float targetWeight = shouldUseAlternateNeckPose ? 1f : 0f;
        neckAlternateRuntimeWeight = MoveTowardsByDuration(neckAlternateRuntimeWeight, targetWeight, GetNeckAlternateHandBlendTime());

        float appliedOffsetX = 0f;
        if (shouldUseAlternateNeckPose)
        {
            appliedOffsetX = targetOffsetX;
        }
        else if (neckAlternateRuntimeWeight > 0.001f && hasNeckAlternateLatchedOffset)
        {
            appliedOffsetX = neckAlternateLatchedOffsetX;
        }
        else
        {
            hasNeckAlternateLatchedOffset = false;
            neckAlternateLatchedOffsetX = 0f;
            neckAlternateRuntimeWeight = 0f;
        }

        var data = neckAlternateHandConstraint.data;
        data.offset = new Vector3(appliedOffsetX, 0f, 0f);
        neckAlternateHandConstraint.data = data;
        neckAlternateHandConstraint.weight = neckAlternateRuntimeWeight;
    }

    private void ResetNeckAlternateHandConstraint()
    {
        if (neckAlternateHandConstraint == null)
            return;

        neckAlternateRuntimeWeight = 0f;
        hasNeckAlternateLatchedOffset = false;
        neckAlternateLatchedOffsetX = 0f;

        var data = neckAlternateHandConstraint.data;
        data.offset = Vector3.zero;
        neckAlternateHandConstraint.data = data;
        neckAlternateHandConstraint.weight = 0f;
    }

    private void ApplyRigWeightsImmediate(float leftWeight, float rightWeight, float headWeight)
    {
        leftRuntimeWeight = Mathf.Clamp01(leftWeight);
        rightRuntimeWeight = Mathf.Clamp01(rightWeight);
        headRuntimeWeight = Mathf.Clamp01(headWeight);

        if (leftHandRig != null)
            leftHandRig.weight = leftRuntimeWeight;

        if (rightHandRig != null)
            rightHandRig.weight = rightRuntimeWeight;

        if (headAimConstraint != null)
        {
            if (headAimRig != null)
                headAimRig.weight = 1f;

            headAimConstraint.weight = headRuntimeWeight;
            return;
        }

        if (headAimRig != null)
            headAimRig.weight = headRuntimeWeight;
    }
}
