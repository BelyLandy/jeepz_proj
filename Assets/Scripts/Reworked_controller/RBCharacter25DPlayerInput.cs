using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public sealed class RBCharacter25DPlayerInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RBCharacter25D character;
    [SerializeField] private PlayerInput playerInput;

    [Header("Action Lookup")]
    [Tooltip("Если включено, Move/Jump будут искаться в текущей active action map у PlayerInput.")]
    [SerializeField] private bool useCurrentActionMap = true;

    [Tooltip("Используется только если Use Current Action Map выключен или у PlayerInput ещё нет currentActionMap.")]
    [SerializeField] private string actionMapName = "Player";

    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string jumpActionName = "Jump";

    [Header("Move")]
    [Tooltip("Если включено, X будет приводиться к -1 / 0 / 1. Удобно для платформера.")]
    [SerializeField] private bool snapMoveToDigital = true;

    [Tooltip("Если snapMoveToDigital выключен, то значения X с модулем ниже порога считаются нулём.")]
    [SerializeField, Range(0f, 1f)] private float analogDeadzoneX = 0.1f;

    private InputActionMap resolvedActionMap;
    private InputAction moveAction;
    private InputAction jumpAction;

    public float CurrentMoveX { get; private set; }

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

        analogDeadzoneX = Mathf.Clamp01(analogDeadzoneX);
    }

    private void OnValidate()
    {
        analogDeadzoneX = Mathf.Clamp01(analogDeadzoneX);

        if (character == null)
            character = GetComponent<RBCharacter25D>();

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (string.IsNullOrWhiteSpace(actionMapName))
            actionMapName = "Player";

        if (string.IsNullOrWhiteSpace(moveActionName))
            moveActionName = "Move";

        if (string.IsNullOrWhiteSpace(jumpActionName))
            jumpActionName = "Jump";
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

        if (character != null)
            character.ClearExternalInputState();
    }

    private void Update()
    {
        ResolveActions(forceResubscribe: false);

        float moveX = ReadMoveX();
        CurrentMoveX = moveX;

        if (character == null)
            return;

        character.SetMoveInput(moveX);
        character.SetJumpHeld(ReadJumpHeld());
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
        float moveX = ReadMoveX();
        CurrentMoveX = moveX;

        if (character == null)
            return;

        character.SetMoveInput(moveX);
        character.SetJumpHeld(ReadJumpHeld());
    }

    private void OnJumpStarted(InputAction.CallbackContext context)
    {
        if (character == null)
            return;

        character.SetJumpHeld(true);
        character.QueueJumpPressed();
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        if (character == null)
            return;

        character.SetJumpHeld(false);
        character.QueueJumpReleased();
    }

    private float ReadMoveX()
    {
        if (moveAction == null || !moveAction.enabled)
            return 0f;

        Vector2 move = moveAction.ReadValue<Vector2>();
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

    private bool ReadJumpHeld()
    {
        return jumpAction != null && jumpAction.enabled && jumpAction.IsPressed();
    }
}
