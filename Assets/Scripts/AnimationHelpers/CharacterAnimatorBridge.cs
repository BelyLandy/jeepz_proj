using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterAnimatorBridge : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private RotationAnim rotationAnim;
    [SerializeField] private RBCharacter25D character;
    [SerializeField] private CharacterCrouch25D crouch;
    [SerializeField] private PlayerControlLock25D controlLock;
    [SerializeField] private Transform rotationSource;

    [Header("Source Options")]
    [SerializeField] private bool useNormalizedSpeed = true;

    [Header("Animator Float Params")]
    [SerializeField] private string facingBlendParam = "FacingBlend";
    [SerializeField] private float facingBlendDampTime = 0.1f;
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private float speedDampTime = 0.05f;
    [SerializeField] private string verticalSpeedParam = "VerticalSpeed";
    [SerializeField] private float verticalSpeedDampTime = 0.03f;

    [Header("Animator Bool Params")]
    [SerializeField] private string isGroundedParam = "IsGrounded";
    [SerializeField] private string isCrouchingParam = "IsCrouching";
    [SerializeField] private string isWallSlidingParam = "IsWallSliding";
    [SerializeField] private string isVaultingParam = "IsVaulting";
    [SerializeField] private string isLockStanceParam = "IsLockStance";
    [SerializeField] private string isControlLockedParam = "IsControlLocked";
    [SerializeField] private string isGroundKnockbackParam = "IsGroundKnockback";
    [SerializeField] private string isDiagonalKnockbackParam = "IsDiagonalKnockback";

    [Header("Animator Trigger Params")]
    [SerializeField] private string jumpSingleTriggerParam = "JumpSingle";
    [SerializeField] private string jumpDoubleTriggerParam = "JumpDouble";
    [SerializeField] private string knockbackGroundTriggerParam = "KnockbackGround";
    [SerializeField] private string knockbackDiagonalTriggerParam = "KnockbackDiagonal";

    private Animator cachedAnimatorForParams;
    private readonly Dictionary<int, AnimatorControllerParameterType> parameterTypesByHash =
        new Dictionary<int, AnimatorControllerParameterType>();

    private int lastObservedJumpStateVersion;
    private bool jumpStateVersionInitialized;
    private bool wasGroundKnockbackActive;
    private bool wasDiagonalKnockbackActive;

    private void Reset()
    {
        CacheReferences();
        RebuildParameterCache();
        SyncObservedJumpVersion();
        SyncObservedKnockbackState();
    }

    private void Awake()
    {
        CacheReferences();
        RebuildParameterCache();
        SyncObservedJumpVersion();
        SyncObservedKnockbackState();
    }

    private void OnEnable()
    {
        CacheReferences();
        SyncObservedJumpVersion();
        SyncObservedKnockbackState();
    }

    private void OnValidate()
    {
        CacheReferences();
        RebuildParameterCache();
    }

    private void LateUpdate()
    {
        CacheReferences();

        if (animator == null)
            return;

        EnsureParameterCache();

        ApplyFacingBlend();
        ApplyMovementFloats();
        ApplyStateBools();
        ApplyKnockbackParams();
        ApplyJumpTriggers();
    }

    private void CacheReferences()
    {
        if (rotationAnim == null)
            rotationAnim = GetComponent<RotationAnim>();

        if (character == null)
            character = GetComponent<RBCharacter25D>();

        if (crouch == null)
            crouch = GetComponent<CharacterCrouch25D>();

        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (rotationSource == null)
            rotationSource = transform;
    }

    private void EnsureParameterCache()
    {
        if (cachedAnimatorForParams != animator)
            RebuildParameterCache();
    }

    private void RebuildParameterCache()
    {
        parameterTypesByHash.Clear();
        cachedAnimatorForParams = animator;

        if (animator == null)
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
            parameterTypesByHash[parameters[i].nameHash] = parameters[i].type;
    }

    private void SyncObservedJumpVersion()
    {
        if (character == null)
        {
            jumpStateVersionInitialized = false;
            lastObservedJumpStateVersion = 0;
            return;
        }

        lastObservedJumpStateVersion = character.LastSelfJumpStateVersion;
        jumpStateVersionInitialized = true;
    }

    private void SyncObservedKnockbackState()
    {
        if (controlLock == null)
        {
            wasGroundKnockbackActive = false;
            wasDiagonalKnockbackActive = false;
            return;
        }

        wasGroundKnockbackActive = controlLock.IsHorizontalLockActive;
        wasDiagonalKnockbackActive = controlLock.IsDiagonalLockActive;
    }

    private void ApplyFacingBlend()
    {
        if (rotationAnim == null || rotationSource == null)
            return;

        float currentYaw = rotationSource.eulerAngles.y;
        float facingBlend = YawToFacingBlend(
            currentYaw,
            rotationAnim.leftYaw,
            rotationAnim.rightYaw
        );

        SetAnimatorFloat(facingBlendParam, facingBlend, facingBlendDampTime);
    }

    private void ApplyMovementFloats()
    {
        float speed = 0f;
        float verticalSpeed = 0f;

        if (character != null)
        {
            speed = useNormalizedSpeed
                ? character.SpeedNormalized
                : Mathf.Clamp01(character.HorizontalSpeedAbs);

            Rigidbody body = character.RigidbodyComponent;
            if (body != null)
                verticalSpeed = body.linearVelocity.y;
        }

        SetAnimatorFloat(speedParam, speed, speedDampTime);
        SetAnimatorFloat(verticalSpeedParam, verticalSpeed, verticalSpeedDampTime);
    }

    private void ApplyStateBools()
    {
        SetAnimatorBool(isGroundedParam, character != null && character.IsGroundedNow);
        SetAnimatorBool(isCrouchingParam, crouch != null && crouch.IsCrouching);
        SetAnimatorBool(isWallSlidingParam, character != null && character.IsWallSliding);
        SetAnimatorBool(isVaultingParam, character != null && character.IsVaultingNow);
        SetAnimatorBool(isLockStanceParam, character != null && character.IsLockStanceGroundActive);
        SetAnimatorBool(isControlLockedParam, controlLock != null && controlLock.IsControlLocked);
    }

    private void ApplyKnockbackParams()
    {
        bool isGroundKnockback = controlLock != null && controlLock.IsHorizontalLockActive;
        bool isDiagonalKnockback = controlLock != null && controlLock.IsDiagonalLockActive;

        SetAnimatorBool(isGroundKnockbackParam, isGroundKnockback);
        SetAnimatorBool(isDiagonalKnockbackParam, isDiagonalKnockback);

        if (isGroundKnockback && !wasGroundKnockbackActive)
            SetAnimatorTrigger(knockbackGroundTriggerParam);

        if (isDiagonalKnockback && !wasDiagonalKnockbackActive)
            SetAnimatorTrigger(knockbackDiagonalTriggerParam);

        wasGroundKnockbackActive = isGroundKnockback;
        wasDiagonalKnockbackActive = isDiagonalKnockback;
    }

    private void ApplyJumpTriggers()
    {
        if (character == null)
        {
            jumpStateVersionInitialized = false;
            return;
        }

        if (!jumpStateVersionInitialized)
        {
            SyncObservedJumpVersion();
            return;
        }

        int currentVersion = character.LastSelfJumpStateVersion;
        if (currentVersion == lastObservedJumpStateVersion)
            return;

        lastObservedJumpStateVersion = currentVersion;

        switch (character.LastSelfJumpType)
        {
            case RBCharacter25D.SelfJumpKind.SingleJump:
            case RBCharacter25D.SelfJumpKind.WallJump:
                SetAnimatorTrigger(jumpSingleTriggerParam);
                break;

            case RBCharacter25D.SelfJumpKind.DoubleJump:
                SetAnimatorTrigger(jumpDoubleTriggerParam);
                break;
        }
    }

    private void SetAnimatorFloat(string parameterName, float value, float dampTime)
    {
        if (!TryGetParameterType(parameterName, out AnimatorControllerParameterType parameterType) ||
            parameterType != AnimatorControllerParameterType.Float)
        {
            return;
        }

        if (dampTime > 0f)
        {
            animator.SetFloat(parameterName, value, dampTime, Time.deltaTime);
            return;
        }

        animator.SetFloat(parameterName, value);
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (!TryGetParameterType(parameterName, out AnimatorControllerParameterType parameterType) ||
            parameterType != AnimatorControllerParameterType.Bool)
        {
            return;
        }

        animator.SetBool(parameterName, value);
    }

    private void SetAnimatorTrigger(string parameterName)
    {
        if (!TryGetParameterType(parameterName, out AnimatorControllerParameterType parameterType) ||
            parameterType != AnimatorControllerParameterType.Trigger)
        {
            return;
        }

        animator.SetTrigger(parameterName);
    }

    private bool TryGetParameterType(
        string parameterName,
        out AnimatorControllerParameterType parameterType
    )
    {
        parameterType = default;

        if (animator == null || string.IsNullOrEmpty(parameterName))
            return false;

        return parameterTypesByHash.TryGetValue(Animator.StringToHash(parameterName), out parameterType);
    }

    private float YawToFacingBlend(float currentYaw, float leftYaw, float rightYaw)
    {
        float totalDelta = Mathf.DeltaAngle(rightYaw, leftYaw);

        if (Mathf.Abs(totalDelta) < 0.0001f)
            return 1f;

        float currentDelta = Mathf.DeltaAngle(rightYaw, currentYaw);
        float t = Mathf.Clamp01(currentDelta / totalDelta);

        return Mathf.Lerp(1f, -1f, t);
    }
}
