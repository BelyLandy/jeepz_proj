using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public sealed class CharacterMoveAimRig25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RBCharacter25D character;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerControlLock25D controlLock;
    [SerializeField] private CharacterCrouch25D crouch;
    [SerializeField] private Rig leftHandRig;
    [SerializeField] private Rig rightHandRig;
    [SerializeField] private Rig headAimRig;
    [SerializeField] private MultiRotationConstraint headAimConstraint;
    [SerializeField] private Transform leftArmAimObject;
    [SerializeField] private Transform rightArmAimObject;
    [SerializeField] private Transform headAimObject;
    [SerializeField] private MultiRotationConstraint neckAlternateHandConstraint;

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
    [SerializeField] private float leftArmLockStanceRestLocalY = 67.469f;
    [SerializeField] private float rightArmLockStanceRestLocalY = -67.469f;

    [Header("Movement Shot Fade")]
    [Tooltip("Вне LockStance после выстрела активная рука уходит из 1 в 0 за это время.")]
    [SerializeField] private float movementShotFadeOutTime = 0.22f;

    [Header("Hand Switching")]
    [Tooltip("Какая рука считается передней, когда герой смотрит вправо. При взгляде влево логика зеркалится автоматически.")]
    [SerializeField] private bool frontHemisphereUsesLeftRigWhenFacingRight = true;
    [Tooltip("Запас около границы 90° / -90°, чтобы рука не дёргалась туда-сюда на переходе через полуплоскость.")]
    [SerializeField, Range(0f, 1f)] private float hemisphereSwitchHysteresis = 0.1f;
    [Tooltip("За сколько уже неактивная рука плавно уходит из текущего веса в 0 после переключения активной руки.")]
    [SerializeField] private float inactiveHandFadeOutTime = 0.12f;

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
    private bool wasLockStanceAimActiveLastFrame;

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

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();

        if (crouch == null)
            crouch = GetComponent<CharacterCrouch25D>();
    }

    private void Awake()
    {
        if (character == null)
            character = GetComponent<RBCharacter25D>();

        ResetNeckAlternateHandConstraint();

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();

        if (crouch == null)
            crouch = GetComponent<CharacterCrouch25D>();

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

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();

        if (crouch == null)
            crouch = GetComponent<CharacterCrouch25D>();

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
        wasLockStanceAimActiveLastFrame = false;
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

    private void UpdateAimVisual()
    {
        bool isControlLocked = controlLock != null && controlLock.IsControlLocked;
        bool isLockStance = character != null && character.IsLockStanceMovementActive;
        bool isCrouching = crouch != null && crouch.IsCrouching;
        bool shouldForceCrouchForwardAim = isCrouching && !isLockStance;

        Vector2 moveAim = Vector2.zero;
        bool hasMoveAim = !isControlLocked && !shouldForceCrouchForwardAim && TryGetRawMoveAim(out moveAim);
        bool isLockStanceAimActive = isLockStance && hasMoveAim;

        Vector3 aimDirection;
        if (shouldForceCrouchForwardAim)
        {
            aimDirection = GetFallbackShotDirection();
        }
        else if (hasMoveAim)
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

        UpdatePrimaryVisibleWeight(isLockStance, isLockStanceAimActive);
        ApplyVisualAimAndWeights(currentAimAngleDeg, combatAimAngleZ, headRelativeAimAngleZ, isLockStance, isLockStanceAimActive);
    }

    public void NotifyShotDirection(Vector3 shotDirection)
    {
        if (shotDirection.sqrMagnitude <= 0.0001f)
            shotDirection = GetFallbackShotDirection();

        shotDirection = ResolveDirectionForCurrentState(shotDirection);

        EvaluateHandForDirection(shotDirection, true);

        currentAimAngleDeg = DirectionToVisualAngleDeg(shotDirection);
        float combatAimAngleZ = DirectionToCombatAimAngleZ(shotDirection);
        float headRelativeAimAngleZ = DirectionToHeadRelativeAimAngleZ(shotDirection, GetHeadAimReferenceFacingSign());
        heldAimAngleDeg = currentAimAngleDeg;
        heldShotDirection = shotDirection;

        primaryVisibleWeight = 1f;

        bool isLockStance = character != null && character.IsLockStanceMovementActive;
        bool hasMoveAim = TryGetRawMoveAim(out _);
        bool isLockStanceAimActive = isLockStance && hasMoveAim;

        if (isLockStanceAimActive)
        {
            lockStanceShotPulseActive = true;
            lockStanceShotPulseStartedAt = Time.time;
        }
        else
        {
            lockStanceShotPulseActive = false;
        }

        ApplyVisualAimAndWeights(currentAimAngleDeg, combatAimAngleZ, headRelativeAimAngleZ, isLockStance, isLockStanceAimActive);
    }

    public bool EvaluateHandForDirection(Vector3 direction, bool updateState)
    {
        direction = ResolveDirectionForCurrentState(direction);

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

    private void UpdatePrimaryVisibleWeight(bool isLockStance, bool isLockStanceAimActive)
    {
        bool isCrouching = crouch != null && crouch.IsCrouching;

        if (isCrouching && !isLockStance)
        {
            lockStanceShotPulseActive = false;
            primaryVisibleWeight = 1f;
            return;
        }

        if (isLockStanceAimActive)
        {
            if (lockStanceShotPulseActive)
                primaryVisibleWeight = EvaluateLockStanceShotPulseWeight();
            else
                primaryVisibleWeight = 1f;
            return;
        }

        lockStanceShotPulseActive = false;

        float fadeDuration = isLockStance ? lockStanceReleaseFadeOutTime : movementShotFadeOutTime;
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
        bool isLockStance = character != null && character.IsLockStanceMovementActive;
        bool shouldForceCrouchForwardAim = crouch != null && crouch.IsCrouching && !isLockStance;

        if (shouldForceCrouchForwardAim)
            return GetFallbackShotDirection();

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
        if (character != null && character.IsWallSliding)
        {
            if (character.WallSlideSide < 0)
                return +1;
            if (character.WallSlideSide > 0)
                return -1;
        }

        return character != null ? character.VaultFacingSignFromInput : +1;
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

    private void ApplyVisualAimAndWeights(float angleDeg, float combatAimAngleZ, float headRelativeAimAngleZ, bool isLockStance, bool isLockStanceAimActive)
    {
        int facingSign = GetEffectiveFacingSign();
        float? leftLocalYOverride = GetArmLocalYOverride(isLockStance, facingSign, manageLeftArm: true);
        float? rightLocalYOverride = GetArmLocalYOverride(isLockStance, facingSign, manageLeftArm: false);

        ApplyArmLocalRotationWithOptionalFreeze(leftArmAimObject, ref hasLeftArmBaseLocalEuler, ref leftArmBaseLocalEuler, angleDeg + leftLocalAngleOffsetX, leftLocalYOverride, freezeLeftArmPose, frozenLeftArmLocalRotation);
        ApplyArmLocalRotationWithOptionalFreeze(rightArmAimObject, ref hasRightArmBaseLocalEuler, ref rightArmBaseLocalEuler, angleDeg + rightLocalAngleOffsetX, rightLocalYOverride, freezeRightArmPose, frozenRightArmLocalRotation);

        UpdateHeadPoseState(headRelativeAimAngleZ, isLockStance, isLockStanceAimActive);
        float appliedHeadRelativeAimAngleZ = GetAppliedHeadRelativeAimAngleZ(headRelativeAimAngleZ, isLockStance, isLockStanceAimActive);
        ApplyHeadLocalRotationWithOptionalFreeze(headAimObject, ref hasHeadBaseLocalEuler, ref headBaseLocalEuler, 0f, headForwardLocalZ - appliedHeadRelativeAimAngleZ + headLocalAngleOffsetZ, freezeHeadPose, frozenHeadLocalRotation);
        ApplyNeckAlternateHandConstraint(isLockStanceAimActive);

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

        headRuntimeWeight = primaryVisibleWeight;
        UpdateArmPoseFreezeState();
        UpdateHeadPoseFreezeState();
        ApplyRigWeightsImmediate(leftRuntimeWeight, rightRuntimeWeight, headRuntimeWeight);
    }


    private int GetHeadAimReferenceFacingSign()
    {
        int facingSign = GetEffectiveFacingSign();

        bool isLockStance = character != null && character.IsLockStanceMovementActive;
        if (!isLockStance || !hasHandSelection)
            return facingSign;

        bool frontUsesLeft = GetFrontHemisphereUsesLeftForCurrentFacing();
        bool isAlternateHand = currentUsesLeftRig != frontUsesLeft;
        return isAlternateHand ? -facingSign : facingSign;
    }

    private float? GetArmLocalYOverride(bool isLockStance, int facingSign, bool manageLeftArm)
    {
        bool shouldManageThisArm = facingSign >= 0 ? !manageLeftArm : manageLeftArm;
        if (!shouldManageThisArm)
            return null;

        float restY = manageLeftArm ? leftArmLockStanceRestLocalY : rightArmLockStanceRestLocalY;
        if (!isLockStance || !hasHandSelection)
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

    private void UpdateHeadPoseState(float targetHeadRelativeAimAngleZ, bool isLockStance, bool isLockStanceAimActive)
    {
        if (isLockStance && wasLockStanceAimActiveLastFrame && !isLockStanceAimActive)
            BeginHeadPoseFreeze();

        if (isLockStanceAimActive)
            ClearHeadPoseFreeze();

        if (!hasCurrentHeadRelativeAimAngle)
        {
            currentHeadRelativeAimAngleZ = targetHeadRelativeAimAngleZ;
            hasCurrentHeadRelativeAimAngle = true;
        }
        else if (isLockStanceAimActive)
        {
            currentHeadRelativeAimAngleZ = SmoothTowardsAngleByDuration(currentHeadRelativeAimAngleZ, targetHeadRelativeAimAngleZ, headAimSwitchBlendTime);
        }
        else if (!freezeHeadPose)
        {
            currentHeadRelativeAimAngleZ = targetHeadRelativeAimAngleZ;
        }

        wasLockStanceAimActiveLastFrame = isLockStanceAimActive;
    }

    private float GetAppliedHeadRelativeAimAngleZ(float targetHeadRelativeAimAngleZ, bool isLockStance, bool isLockStanceAimActive)
    {
        if (!hasCurrentHeadRelativeAimAngle)
            return targetHeadRelativeAimAngleZ;

        if (isLockStanceAimActive)
            return currentHeadRelativeAimAngleZ;

        if (freezeHeadPose && isLockStance)
            return currentHeadRelativeAimAngleZ;

        return targetHeadRelativeAimAngleZ;
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

        return character != null && character.IsLockStanceMovementActive;
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

    private void ApplyNeckAlternateHandConstraint(bool isLockStanceAimActive)
    {
        if (neckAlternateHandConstraint == null)
            return;

        bool shouldUseAlternateNeckPose = false;
        float targetOffsetX = 0f;

        if (isLockStanceAimActive && hasHandSelection)
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
        neckAlternateRuntimeWeight = MoveTowardsByDuration(neckAlternateRuntimeWeight, targetWeight, lockStanceReleaseFadeOutTime);

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
