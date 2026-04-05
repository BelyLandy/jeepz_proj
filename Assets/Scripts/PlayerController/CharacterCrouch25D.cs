using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterCrouch25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RBCharacter25D character;
    [SerializeField] private RBCharacter25DPlayerInput playerInput;
    [SerializeField] private PlayerControlLock25D controlLock;
    [SerializeField] private Rigidbody heroRb;

    [Header("Hurtboxes")]
    [SerializeField] private GameObject normalHurtboxRoot;
    [SerializeField] private GameObject crouchHurtboxRoot;

    [Header("Input")]
    [SerializeField, Range(-1f, 0f)] private float crouchInputThreshold = -0.5f;
    [SerializeField] private bool disableHorizontalMovementWhileCrouching = true;

    [Header("Headroom")]
    [SerializeField] private Transform headroomCheckOrigin;
    [SerializeField] private float headroomCheckRadius = 0.2f;
    [SerializeField] private float headroomCheckHeight = 0.9f;
    [SerializeField] private LayerMask headroomBlockMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool debugCrouchState;

    private bool isCrouching;
    private bool pendingStandWhenHeadroomClear;
    private bool lastLoggedIsCrouching;
    private bool lastLoggedPendingStand;

    public bool IsCrouching => isCrouching;
    public bool IsStanding => !isCrouching;
    public bool WantsCrouchNow => GetRawMoveY() <= crouchInputThreshold;
    public bool DisableHorizontalMovementWhileCrouching => disableHorizontalMovementWhileCrouching;
    public bool IsCrouchLockedByHeadroom => pendingStandWhenHeadroomClear;
    public bool CanEnterCrouchNow => EvaluateCanEnterCrouch();
    public bool CanStandUpNow => EvaluateCanStandUp();

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        ClampSettings();
        ApplyHurtboxState();
    }

    private void OnValidate()
    {
        ClampSettings();
        CacheReferences();
        ApplyHurtboxState();
    }

    private void Update()
    {
        CacheReferences();

        if (pendingStandWhenHeadroomClear && EvaluateCanStandUp())
            FinishStandUp();

        if (controlLock != null && controlLock.IsControlLocked)
        {
            LogStateIfNeeded();
            return;
        }

        if (character == null)
        {
            LogStateIfNeeded();
            return;
        }

        if (!character.IsGroundedNow && isCrouching && !pendingStandWhenHeadroomClear && !character.IsLockStanceMovementActive)
        {
            FinishStandUp();
            LogStateIfNeeded();
            return;
        }

        if (!isCrouching)
        {
            if (EvaluateCanEnterCrouch() && WantsCrouchNow)
                EnterCrouch();

            LogStateIfNeeded();
            return;
        }

        if (character.IsLockStanceMovementActive)
        {
            LogStateIfNeeded();
            return;
        }

        if (pendingStandWhenHeadroomClear)
        {
            LogStateIfNeeded();
            return;
        }

        if (!WantsCrouchNow)
            TryExitCrouch();

        LogStateIfNeeded();
    }

    public bool TryEnterCrouch()
    {
        if (!EvaluateCanEnterCrouch())
            return false;

        EnterCrouch();
        return true;
    }

    public bool TryExitCrouch()
    {
        if (!isCrouching)
            return true;
        if (character != null && character.IsLockStanceMovementActive)
            return false;
        if (!EvaluateCanStandUp())
        {
            pendingStandWhenHeadroomClear = true;
            ApplyHurtboxState();
            return false;
        }

        FinishStandUp();
        return true;
    }

    public void ForceExitCrouchFromHit()
    {
        if (!isCrouching)
            return;

        pendingStandWhenHeadroomClear = false;

        if (EvaluateCanStandUp())
        {
            FinishStandUp();
            return;
        }

        pendingStandWhenHeadroomClear = true;
        ApplyHurtboxState();
    }

    private void EnterCrouch()
    {
        isCrouching = true;
        pendingStandWhenHeadroomClear = false;

        if (character != null)
            character.ClearLocomotionDrive();

        if (heroRb != null && !heroRb.isKinematic)
        {
            Vector3 velocity = heroRb.linearVelocity;
            heroRb.linearVelocity = new Vector3(0f, velocity.y, character != null && character.UsesLockedZ ? 0f : velocity.z);
        }

        ApplyHurtboxState();
    }

    private void FinishStandUp()
    {
        isCrouching = false;
        pendingStandWhenHeadroomClear = false;
        ApplyHurtboxState();
    }

    private bool EvaluateCanEnterCrouch()
    {
        if (character == null)
            return false;
        if (controlLock != null && controlLock.IsControlLocked)
            return false;
        if (!character.IsGroundedNow)
            return false;
        if (character.IsWallSliding)
            return false;
        if (character.IsVaultingNow)
            return false;
        if (character.IsLockStanceMovementActive && !isCrouching)
            return false;
        return true;
    }

    private bool EvaluateCanStandUp()
    {
        if (headroomCheckOrigin == null)
            return true;

        Vector3 bottom = headroomCheckOrigin.position;
        Vector3 top = bottom + Vector3.up * Mathf.Max(0f, headroomCheckHeight);

        return !Physics.CheckCapsule(
            bottom,
            top,
            Mathf.Max(0.001f, headroomCheckRadius),
            headroomBlockMask,
            QueryTriggerInteraction.Ignore);
    }

    private float GetRawMoveY()
    {
        return playerInput != null ? playerInput.CurrentRawMove.y : 0f;
    }

    private void ApplyHurtboxState()
    {
        if (normalHurtboxRoot != null)
            normalHurtboxRoot.SetActive(!isCrouching);

        if (crouchHurtboxRoot != null)
            crouchHurtboxRoot.SetActive(isCrouching);
    }

    private void CacheReferences()
    {
        if (character == null)
            character = GetComponent<RBCharacter25D>();
        if (playerInput == null)
            playerInput = GetComponent<RBCharacter25DPlayerInput>();
        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();
        if (heroRb == null && character != null)
            heroRb = character.RigidbodyComponent;
        else if (heroRb == null)
            heroRb = GetComponent<Rigidbody>();
    }

    private void ClampSettings()
    {
        crouchInputThreshold = Mathf.Clamp(crouchInputThreshold, -1f, 0f);
        headroomCheckRadius = Mathf.Max(0.001f, headroomCheckRadius);
        headroomCheckHeight = Mathf.Max(0f, headroomCheckHeight);
    }

    private void LogStateIfNeeded()
    {
        if (!debugCrouchState)
            return;

        if (lastLoggedIsCrouching == isCrouching && lastLoggedPendingStand == pendingStandWhenHeadroomClear)
            return;

        lastLoggedIsCrouching = isCrouching;
        lastLoggedPendingStand = pendingStandWhenHeadroomClear;

        Debug.Log($"[CharacterCrouch25D] crouching={isCrouching} pendingStand={pendingStandWhenHeadroomClear} grounded={(character != null && character.IsGroundedNow)} lockStance={(character != null && character.IsLockStanceMovementActive)} controlLock={(controlLock != null && controlLock.IsControlLocked)}", this);
    }
}
