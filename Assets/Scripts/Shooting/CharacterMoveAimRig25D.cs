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
    [SerializeField] private Rig leftHandRig;
    [SerializeField] private Rig rightHandRig;
    [SerializeField] private Transform leftArmAimObject;
    [SerializeField] private Transform rightArmAimObject;

    [Header("Action Lookup")]
    [SerializeField] private bool useCurrentActionMap = true;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";

    [Header("Aim")]
    [SerializeField, Range(0f, 1f)] private float moveAimDeadzone = 0.1f;
    [SerializeField] private float leftLocalAngleOffsetX = 0f;
    [SerializeField] private float rightLocalAngleOffsetX = 0f;

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
    private bool hasLeftArmBaseLocalEuler;
    private bool hasRightArmBaseLocalEuler;

    private bool currentUsesLeftRig;
    private bool hasHandSelection;

    private float primaryVisibleWeight;
    private float leftRuntimeWeight;
    private float rightRuntimeWeight;

    private bool lockStanceShotPulseActive;
    private float lockStanceShotPulseStartedAt;

    public float CurrentAimAngleDeg => currentAimAngleDeg;
    public bool UsesLeftRig => currentUsesLeftRig;
    public bool UsesRightRig => !currentUsesLeftRig;
    public float CurrentPrimaryVisibleWeight => primaryVisibleWeight;
    public float LeftCurrentRigWeight => leftRuntimeWeight;
    public float RightCurrentRigWeight => rightRuntimeWeight;

    private void Reset()
    {
        if (character == null)
            character = GetComponent<RBCharacter25D>();

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();
    }

    private void Awake()
    {
        if (character == null)
            character = GetComponent<RBCharacter25D>();

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        ClampSettings();
        CacheBaseArmRotations();
        ApplyRigWeightsImmediate(0f, 0f);
    }

    private void OnValidate()
    {
        ClampSettings();

        if (character == null)
            character = GetComponent<RBCharacter25D>();

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (string.IsNullOrWhiteSpace(actionMapName))
            actionMapName = "Player";

        if (string.IsNullOrWhiteSpace(moveActionName))
            moveActionName = "Move";

        CacheBaseArmRotations();
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
        ApplyRigWeightsImmediate(0f, 0f);
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
        bool hasMoveAim = TryGetRawMoveAim(out Vector2 moveAim);
        bool isLockStance = character != null && character.IsLockStanceMovementActive;
        bool isLockStanceAimActive = isLockStance && hasMoveAim;

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
        currentAimAngleDeg = DirectionToVisualAngleDeg(aimDirection);
        heldAimAngleDeg = currentAimAngleDeg;
        heldShotDirection = aimDirection;

        EvaluateHandForDirection(aimDirection, true);
        UpdatePrimaryVisibleWeight(isLockStance, isLockStanceAimActive);
        ApplyVisualAimAndWeights(currentAimAngleDeg);
    }

    public void NotifyShotDirection(Vector3 shotDirection)
    {
        if (shotDirection.sqrMagnitude <= 0.0001f)
            shotDirection = GetFallbackShotDirection();

        shotDirection = ResolveDirectionForCurrentState(shotDirection);

        currentAimAngleDeg = DirectionToVisualAngleDeg(shotDirection);
        heldAimAngleDeg = currentAimAngleDeg;
        heldShotDirection = shotDirection;

        EvaluateHandForDirection(shotDirection, true);
        primaryVisibleWeight = 1f;

        bool hasMoveAim = TryGetRawMoveAim(out _);
        bool isLockStanceAimActive = character != null && character.IsLockStanceMovementActive && hasMoveAim;

        if (isLockStanceAimActive)
        {
            lockStanceShotPulseActive = true;
            lockStanceShotPulseStartedAt = Time.time;
        }
        else
        {
            lockStanceShotPulseActive = false;
        }

        ApplyVisualAimAndWeights(currentAimAngleDeg);
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
            currentUsesLeftRig = selectedUsesLeft;
            hasHandSelection = true;
        }

        return selectedUsesLeft;
    }

    private void UpdatePrimaryVisibleWeight(bool isLockStance, bool isLockStanceAimActive)
    {
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

    private void CacheBaseArmRotations()
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
    }

    private void ApplyVisualAimAndWeights(float angleDeg)
    {
        ApplyArmLocalX(leftArmAimObject, ref hasLeftArmBaseLocalEuler, ref leftArmBaseLocalEuler, angleDeg + leftLocalAngleOffsetX);
        ApplyArmLocalX(rightArmAimObject, ref hasRightArmBaseLocalEuler, ref rightArmBaseLocalEuler, angleDeg + rightLocalAngleOffsetX);

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

        ApplyRigWeightsImmediate(leftRuntimeWeight, rightRuntimeWeight);
    }

    private static void ApplyArmLocalX(Transform target, ref bool hasBaseEuler, ref Vector3 baseEuler, float xAngle)
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
        target.localRotation = Quaternion.Euler(euler);
    }

    private void ApplyRigWeightsImmediate(float leftWeight, float rightWeight)
    {
        leftRuntimeWeight = Mathf.Clamp01(leftWeight);
        rightRuntimeWeight = Mathf.Clamp01(rightWeight);

        if (leftHandRig != null)
            leftHandRig.weight = leftRuntimeWeight;

        if (rightHandRig != null)
            rightHandRig.weight = rightRuntimeWeight;
    }
}
