using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class CharacterAnimatorBridge : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private RotationAnim rotationAnim;
    [SerializeField] private RBCharacter25D character;
    [SerializeField] private CharacterCrouch25D crouch;
    [SerializeField] private PlayerControlLock25D controlLock;
    [SerializeField] private CharacterPlasmaGlove25D plasmaGlove;
    [SerializeField] private Transform rotationSource;

    [Header("Source Options")]
    [SerializeField] private bool useNormalizedSpeed = true;

    [Header("Animator Float Params")]
    [SerializeField] private string facingBlendParam = "FacingBlend";
    [SerializeField] private float facingBlendDampTime = 0.1f;
    [SerializeField] private string speedParam = "Speed";
    [FormerlySerializedAs("speedDampTime")]
    [SerializeField] private float speedRiseDampTime = 0.05f;
    [SerializeField] private float speedFallDampTime = 0.02f;
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
    [SerializeField] private string isFallingParam = "IsFalling";
    [SerializeField] private string isSlopeSlidingParam = "IsSlopeSliding";
    [SerializeField] private string isRunStoppingParam = "IsRunStopping";
    [SerializeField] private string isPlasmaOutOfAmmoParam = "IsPlasmaOutOfAmmo";
    [SerializeField] private string isPlasmaOverheatedParam = "IsPlasmaOverheated";

    [Header("Animator Extra Float Params")]
    [SerializeField] private string slopeSlideSpeedParam = "SlopeSlideSpeed";
    [SerializeField] private float slopeSlideSpeedDampTime = 0.03f;
    [SerializeField] private string runStopSpeedParam = "RunStopSpeed";
    [SerializeField] private float runStopSpeedDampTime = 0.03f;

    [Header("Animator Trigger Params")]
    [SerializeField] private string jumpSingleTriggerParam = "JumpSingle";
    [SerializeField] private string jumpDoubleTriggerParam = "JumpDouble";
    [SerializeField] private string knockbackGroundTriggerParam = "KnockbackGround";
    [SerializeField] private string knockbackDiagonalTriggerParam = "KnockbackDiagonal";
    [SerializeField] private string landTriggerParam = "Land";
    [SerializeField] private string plasmaDryFireTriggerParam = "PlasmaDryFire";

    [Header("Landing Trigger Conditions")]
    [SerializeField] private float minLandingAirborneTime = 0.05f;
    [SerializeField] private float minLandingDownwardSpeed = 1.25f;
    [SerializeField] private float suppressLandingAfterVaultWindow = 0.12f;
    [SerializeField] private float suppressLandingAfterWallSlideWindow = 0.12f;

    [Header("Vertical Speed Filtering")]
    [SerializeField] private bool forceZeroVerticalSpeedWhenGrounded = true;
    [SerializeField] private float verticalSpeedZeroEpsilon = 0.01f;
    [SerializeField] private float fallEnterVerticalSpeed = -0.1f;

    private Animator cachedAnimatorForParams;
    private readonly Dictionary<int, AnimatorControllerParameterType> parameterTypesByHash =
        new Dictionary<int, AnimatorControllerParameterType>();

    private const float InvalidPastTime = -999f;

    private int lastObservedJumpStateVersion;
    private bool jumpStateVersionInitialized;
    private int lastObservedLandingStateVersion;
    private bool landingStateVersionInitialized;
    private bool wasGroundKnockbackActive;
    private bool wasDiagonalKnockbackActive;
    private int lastObservedPlasmaDryFireVersion;
    private bool plasmaDryFireVersionInitialized;
    private bool wasGroundedLastFrame;
    private bool hasAirborneState;
    private float airborneStartTime = InvalidPastTime;
    private float mostNegativeAirborneYSpeed;

    private void Reset()
    {
        CacheReferences();
        ClampSettings();
        RebuildParameterCache();
        SyncObservedJumpVersion();
        SyncObservedLandingState();
        SyncObservedKnockbackState();
        SyncObservedPlasmaDryFireState();
    }

    private void Awake()
    {
        CacheReferences();
        ClampSettings();
        RebuildParameterCache();
        SyncObservedJumpVersion();
        SyncObservedLandingState();
        SyncObservedKnockbackState();
        SyncObservedPlasmaDryFireState();
    }

    private void OnEnable()
    {
        CacheReferences();
        ClampSettings();
        SyncObservedJumpVersion();
        SyncObservedLandingState();
        SyncObservedKnockbackState();
        SyncObservedPlasmaDryFireState();
    }

    private void OnValidate()
    {
        CacheReferences();
        ClampSettings();
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
        ApplyPlasmaWeaponParams();
        TrackLandingAirborneState();
        ApplyKnockbackParams();
        ApplyJumpTriggers();
        ApplyLandingTrigger();
        ApplyPlasmaDryFireTrigger();
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

        if (plasmaGlove == null)
            plasmaGlove = GetComponent<CharacterPlasmaGlove25D>();

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

    private void SyncObservedLandingState()
    {
        if (character == null)
        {
            landingStateVersionInitialized = false;
            lastObservedLandingStateVersion = 0;
            wasGroundedLastFrame = false;
            ResetLandingAirborneState();
            return;
        }

        lastObservedLandingStateVersion = character.LastLandingStateVersion;
        landingStateVersionInitialized = true;
        wasGroundedLastFrame = character.IsGroundedNow;

        if (wasGroundedLastFrame)
            ResetLandingAirborneState();
        else
            BeginLandingAirborneState();
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

    private void SyncObservedPlasmaDryFireState()
    {
        if (plasmaGlove == null)
        {
            plasmaDryFireVersionInitialized = false;
            lastObservedPlasmaDryFireVersion = 0;
            return;
        }

        lastObservedPlasmaDryFireVersion = plasmaGlove.DryFireEventVersion;
        plasmaDryFireVersionInitialized = true;
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
        float slopeSlideSpeed = 0f;
        float runStopSpeed = 0f;

        if (character != null)
        {
            speed = useNormalizedSpeed
                ? character.SpeedNormalized
                : Mathf.Clamp01(character.HorizontalSpeedAbs);

            verticalSpeed = GetFilteredAnimatorVerticalSpeed();
            slopeSlideSpeed = character.SlopeSlideSpeedNormalized;
            runStopSpeed = character.RunStopSpeedNormalized;
        }

        SetAnimatorSpeedFloat(speedParam, speed);
        SetAnimatorFloat(verticalSpeedParam, verticalSpeed, verticalSpeedDampTime);
        SetAnimatorFloat(slopeSlideSpeedParam, slopeSlideSpeed, slopeSlideSpeedDampTime);
        SetAnimatorFloat(runStopSpeedParam, runStopSpeed, runStopSpeedDampTime);
    }

    private void ApplyStateBools()
    {
        bool isGrounded = character != null && character.IsGroundedNow;
        bool isWallSliding = character != null && character.IsWallSliding;
        bool isVaulting = character != null && character.IsVaultingNow;
        bool isSlopeSliding = character != null && character.IsSlopeSlidingNow;
        bool isRunStopping = character != null && character.IsRunStoppingNow;
        bool isFalling = !isGrounded && !isWallSliding && !isVaulting && GetCurrentVerticalSpeed() < fallEnterVerticalSpeed;

        SetAnimatorBool(isGroundedParam, isGrounded);
        SetAnimatorBool(isCrouchingParam, crouch != null && crouch.IsCrouching);
        SetAnimatorBool(isWallSlidingParam, isWallSliding);
        SetAnimatorBool(isVaultingParam, isVaulting);
        SetAnimatorBool(isLockStanceParam, character != null && character.IsLockStanceGroundActive);
        SetAnimatorBool(isControlLockedParam, controlLock != null && controlLock.IsControlLocked);
        SetAnimatorBool(isFallingParam, isFalling);
        SetAnimatorBool(isSlopeSlidingParam, isSlopeSliding);
        SetAnimatorBool(isRunStoppingParam, isRunStopping);
    }

    private void ApplyPlasmaWeaponParams()
    {
        bool isOutOfAmmo = plasmaGlove != null && plasmaGlove.IsOutOfAmmoNow;
        bool isOverheated = plasmaGlove != null && plasmaGlove.IsOverheatedNow;

        SetAnimatorBool(isPlasmaOutOfAmmoParam, isOutOfAmmo);
        SetAnimatorBool(isPlasmaOverheatedParam, isOverheated);
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

    private void TrackLandingAirborneState()
    {
        if (character == null)
        {
            wasGroundedLastFrame = false;
            ResetLandingAirborneState();
            return;
        }

        bool groundedNow = character.IsGroundedNow;

        if (!groundedNow)
        {
            if (wasGroundedLastFrame)
                BeginLandingAirborneState();

            UpdateLandingAirborneMotion();
        }

        wasGroundedLastFrame = groundedNow;
    }

    private void ApplyPlasmaDryFireTrigger()
    {
        if (plasmaGlove == null)
        {
            plasmaDryFireVersionInitialized = false;
            return;
        }

        if (!plasmaDryFireVersionInitialized)
        {
            SyncObservedPlasmaDryFireState();
            return;
        }

        int currentVersion = plasmaGlove.DryFireEventVersion;
        if (currentVersion == lastObservedPlasmaDryFireVersion)
            return;

        lastObservedPlasmaDryFireVersion = currentVersion;
        SetAnimatorTrigger(plasmaDryFireTriggerParam);
    }

    private void ApplyLandingTrigger()
    {
        if (character == null)
        {
            landingStateVersionInitialized = false;
            return;
        }

        if (!landingStateVersionInitialized)
        {
            SyncObservedLandingState();
            return;
        }

        int currentVersion = character.LastLandingStateVersion;
        if (currentVersion == lastObservedLandingStateVersion)
            return;

        lastObservedLandingStateVersion = currentVersion;

        bool shouldSuppressAfterVault =
            character.LastVaultFinishedTime > InvalidPastTime &&
            (Time.time - character.LastVaultFinishedTime) <= suppressLandingAfterVaultWindow;

        bool shouldSuppressAfterWallSlide =
            character.LastWallSlideFinishedTime > InvalidPastTime &&
            (Time.time - character.LastWallSlideFinishedTime) <= suppressLandingAfterWallSlideWindow;

        float airborneTime = airborneStartTime > InvalidPastTime
            ? (Time.time - airborneStartTime)
            : 0f;

        bool longEnoughAirborne = airborneTime >= minLandingAirborneTime;
        bool fastEnoughDownward = mostNegativeAirborneYSpeed <= -minLandingDownwardSpeed;

        if (!shouldSuppressAfterVault &&
            !shouldSuppressAfterWallSlide &&
            hasAirborneState &&
            (longEnoughAirborne || fastEnoughDownward))
        {
            SetAnimatorTrigger(landTriggerParam);
        }

        ResetLandingAirborneState();
        wasGroundedLastFrame = character.IsGroundedNow;
    }

    private void BeginLandingAirborneState()
    {
        hasAirborneState = true;
        airborneStartTime = Time.time;
        mostNegativeAirborneYSpeed = Mathf.Min(0f, GetCurrentVerticalSpeed());
    }

    private void UpdateLandingAirborneMotion()
    {
        if (!hasAirborneState)
            return;

        float verticalSpeed = GetCurrentVerticalSpeed();
        if (verticalSpeed < mostNegativeAirborneYSpeed)
            mostNegativeAirborneYSpeed = verticalSpeed;
    }

    private void ResetLandingAirborneState()
    {
        hasAirborneState = false;
        airborneStartTime = InvalidPastTime;
        mostNegativeAirborneYSpeed = 0f;
    }

    private float GetCurrentVerticalSpeed()
    {
        if (character == null)
            return 0f;

        Rigidbody body = character.RigidbodyComponent;
        return body != null ? body.linearVelocity.y : 0f;
    }

    private float GetFilteredAnimatorVerticalSpeed()
    {
        float verticalSpeed = GetCurrentVerticalSpeed();

        if (forceZeroVerticalSpeedWhenGrounded && character != null && character.IsGroundedNow)
            return 0f;

        if (Mathf.Abs(verticalSpeed) <= verticalSpeedZeroEpsilon)
            return 0f;

        return verticalSpeed;
    }

    private void ClampSettings()
    {
        speedRiseDampTime = Mathf.Max(0f, speedRiseDampTime);
        speedFallDampTime = Mathf.Max(0f, speedFallDampTime);
        slopeSlideSpeedDampTime = Mathf.Max(0f, slopeSlideSpeedDampTime);
        runStopSpeedDampTime = Mathf.Max(0f, runStopSpeedDampTime);
        minLandingAirborneTime = Mathf.Max(0f, minLandingAirborneTime);
        minLandingDownwardSpeed = Mathf.Max(0f, minLandingDownwardSpeed);
        suppressLandingAfterVaultWindow = Mathf.Max(0f, suppressLandingAfterVaultWindow);
        suppressLandingAfterWallSlideWindow = Mathf.Max(0f, suppressLandingAfterWallSlideWindow);
        verticalSpeedZeroEpsilon = Mathf.Max(0f, verticalSpeedZeroEpsilon);
        fallEnterVerticalSpeed = Mathf.Min(fallEnterVerticalSpeed, 0f);
    }

    private void SetAnimatorSpeedFloat(string parameterName, float targetValue)
    {
        if (!TryGetParameterType(parameterName, out AnimatorControllerParameterType parameterType) ||
            parameterType != AnimatorControllerParameterType.Float)
        {
            return;
        }

        float currentValue = animator.GetFloat(parameterName);
        float dampTime = targetValue > currentValue ? speedRiseDampTime : speedFallDampTime;

        if (dampTime > 0f)
        {
            animator.SetFloat(parameterName, targetValue, dampTime, Time.deltaTime);
            return;
        }

        animator.SetFloat(parameterName, targetValue);
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
