using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public sealed class RBCharacter25DPlayerInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RBCharacter25D character;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private PlayerControlLock25D controlLock;
    [SerializeField] private CharacterCrouch25D crouch;

    [Header("Action Lookup")]
    [Tooltip("Если включено, Move/Jump будут искаться в текущей active action map у PlayerInput.")]
    [SerializeField] private bool useCurrentActionMap = true;

    [Tooltip("Используется только если Use Current Action Map выключен или у PlayerInput ещё нет currentActionMap.")]
    [SerializeField] private string actionMapName = "Player";

    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string lockStanceActionName = "LockStance";

    [Header("Move")]
    [Tooltip("Если включено, X будет приводиться к -1 / 0 / 1. Удобно для платформера.")]
    [SerializeField] private bool snapMoveToDigital = true;

    [Tooltip("Если snapMoveToDigital выключен, то значения X с модулем ниже порога считаются нулём.")]
    [SerializeField, Range(0f, 1f)] private float analogDeadzoneX = 0.1f;

    private InputActionMap resolvedActionMap;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction lockStanceAction;
    private bool wasControlLockedLastFrame;

    public float CurrentMoveX { get; private set; }
    public float CurrentMoveY { get; private set; }
    public Vector2 CurrentRawMove { get; private set; }
    public bool CurrentLockStanceHeld { get; private set; }

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

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();

        if (crouch == null)
            crouch = GetComponent<CharacterCrouch25D>();

        analogDeadzoneX = Mathf.Clamp01(analogDeadzoneX);
    }

    private void OnValidate()
    {
        analogDeadzoneX = Mathf.Clamp01(analogDeadzoneX);

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

        if (string.IsNullOrWhiteSpace(jumpActionName))
            jumpActionName = "Jump";

        if (string.IsNullOrWhiteSpace(lockStanceActionName))
            lockStanceActionName = "LockStance";
    }

    private void OnEnable()
    {
        ResolveActions(forceResubscribe: true);
        SyncImmediateState();
    }

    private void OnDisable()
    {
        UnsubscribeJumpCallbacks();
        CurrentMoveX = 0f;
        CurrentMoveY = 0f;
        CurrentRawMove = Vector2.zero;
        CurrentLockStanceHeld = false;
        wasControlLockedLastFrame = false;

        if (character != null)
        {
            character.SetLockStanceHeld(false);
            character.ClearExternalInputState();
        }
    }

    private void Update()
    {
        ResolveActions(forceResubscribe: false);

        bool isControlLocked = controlLock != null && controlLock.IsControlLocked;
        if (isControlLocked && !wasControlLockedLastFrame && character != null)
            character.ClearExternalInputState();
        wasControlLockedLastFrame = isControlLocked;

        Vector2 move = isControlLocked ? Vector2.zero : ReadMoveVector();
        CurrentRawMove = move;
        float moveX = ResolveMoveX(move);
        float moveY = ResolveMoveY(move);

        if (ShouldBlockHorizontalMovementWhileCrouching())
            moveX = 0f;

        bool lockStanceHeld = !isControlLocked && ReadLockStanceHeld();
        bool jumpHeld = !isControlLocked && ReadJumpHeld();

        CurrentMoveX = moveX;
        CurrentMoveY = moveY;
        CurrentLockStanceHeld = lockStanceHeld;

        if (character == null)
            return;

        character.SetMoveInput(moveX, moveY);
        character.SetJumpHeld(jumpHeld);
        character.SetLockStanceHeld(lockStanceHeld);

        if (isControlLocked)
            character.ClearExternalInputState(clearMove: false);
    }


    private bool ShouldBlockHorizontalMovementWhileCrouching()
    {
        return crouch != null && crouch.IsCrouching && crouch.DisableHorizontalMovementWhileCrouching;
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

        UnsubscribeJumpCallbacks();

        resolvedActionMap = targetMap;
        moveAction = resolvedActionMap != null ? resolvedActionMap.FindAction(moveActionName, false) : null;
        jumpAction = resolvedActionMap != null ? resolvedActionMap.FindAction(jumpActionName, false) : null;
        lockStanceAction = resolvedActionMap != null ? resolvedActionMap.FindAction(lockStanceActionName, false) : null;

        SubscribeJumpCallbacks();
    }

    private void SubscribeJumpCallbacks()
    {
        if (jumpAction == null)
            return;

        jumpAction.started -= OnJumpStarted;
        jumpAction.canceled -= OnJumpCanceled;

        jumpAction.started += OnJumpStarted;
        jumpAction.canceled += OnJumpCanceled;
    }

    private void UnsubscribeJumpCallbacks()
    {
        if (jumpAction == null)
            return;

        jumpAction.started -= OnJumpStarted;
        jumpAction.canceled -= OnJumpCanceled;
    }

    private void SyncImmediateState()
    {
        bool isControlLocked = controlLock != null && controlLock.IsControlLocked;
        Vector2 move = isControlLocked ? Vector2.zero : ReadMoveVector();
        CurrentRawMove = move;
        float moveX = ResolveMoveX(move);
        float moveY = ResolveMoveY(move);

        if (ShouldBlockHorizontalMovementWhileCrouching())
            moveX = 0f;

        bool lockStanceHeld = !isControlLocked && ReadLockStanceHeld();
        bool jumpHeld = !isControlLocked && ReadJumpHeld();

        CurrentMoveX = moveX;
        CurrentMoveY = moveY;
        CurrentLockStanceHeld = lockStanceHeld;

        if (character == null)
            return;

        if (isControlLocked)
            character.ClearExternalInputState();

        character.SetMoveInput(moveX, moveY);
        character.SetJumpHeld(jumpHeld);
        character.SetLockStanceHeld(lockStanceHeld);
    }

    private void OnJumpStarted(InputAction.CallbackContext context)
    {
        if (character == null)
            return;
        if (controlLock != null && controlLock.IsControlLocked)
            return;

        character.SetJumpHeld(true);
        character.QueueJumpPressed();
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        if (character == null)
            return;
        if (controlLock != null && controlLock.IsControlLocked)
            return;

        character.SetJumpHeld(false);
        character.QueueJumpReleased();
    }

    private Vector2 ReadMoveVector()
    {
        if (moveAction == null || !moveAction.enabled)
            return Vector2.zero;

        return moveAction.ReadValue<Vector2>();
    }

    private float ResolveMoveX(Vector2 move)
    {
        float x = Mathf.Clamp(move.x, -1f, 1f);

        if (snapMoveToDigital)
        {
            if (Mathf.Abs(x) <= analogDeadzoneX)
                return 0f;

            return Mathf.Sign(x);
        }

        if (Mathf.Abs(x) <= analogDeadzoneX)
            return 0f;

        return x;
    }

    private static float ResolveMoveY(Vector2 move)
    {
        return Mathf.Clamp(move.y, -1f, 1f);
    }

    private bool ReadJumpHeld()
    {
        return jumpAction != null && jumpAction.enabled && jumpAction.IsPressed();
    }

    private bool ReadLockStanceHeld()
    {
        return lockStanceAction != null && lockStanceAction.enabled && lockStanceAction.IsPressed();
    }
}
