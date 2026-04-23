using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class MenuPointerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform aimPlaneAnchor;
    [SerializeField] private Transform crosshairTransform;

    [Header("Action Lookup")]
    [Tooltip("Если включено, NavigatePointer будет искаться в текущей active action map у PlayerInput.")]
    [SerializeField] private bool useCurrentActionMap = true;

    [Tooltip("Используется только если Use Current Action Map выключен или у PlayerInput ещё нет currentActionMap.")]
    [SerializeField] private string actionMapName = "UI";
    [SerializeField] private string navigateActionName = "NavigatePointer";

    [Header("Pointer Motion")]
    [SerializeField] private Vector2 initialCursorNormalized = Vector2.zero;
    [SerializeField, Min(0f)] private float pointerMoveSpeed = 2f;
    [SerializeField, Range(0f, 1f)] private float horizontalLimit = 0.85f;
    [SerializeField, Range(0f, 1f)] private float verticalLimit = 0.75f;
    [SerializeField] private bool clampInputToUnitCircle = true;

    [Header("Input Activity")]
    [SerializeField, Min(0f)] private float navigateInputDeadzone = 0.1f;

    [Header("Aim Plane")]
    [SerializeField, Min(0f)] private float aimPlaneHalfWidth = 6f;
    [SerializeField, Min(0f)] private float aimPlaneHalfHeight = 3.5f;
    [SerializeField] private bool orientCrosshairToCamera = true;

    private InputActionMap resolvedActionMap;
    private InputAction navigateAction;
    private Vector2 cursorNormalized;
    private Vector3 crosshairWorldPosition;
    private Vector2 currentNavigateInput;
    private bool hasNavigateInputThisFrame;
    private bool didCursorMoveThisFrame;

    public Vector2 CursorNormalized => cursorNormalized;
    public Vector3 CrosshairWorldPosition => crosshairWorldPosition;
    public Vector2 CurrentNavigateInput => currentNavigateInput;
    public bool HasNavigateInputThisFrame => hasNavigateInputThisFrame;
    public bool DidCursorMoveThisFrame => didCursorMoveThisFrame;

    private void Reset()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Awake()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        pointerMoveSpeed = Mathf.Max(0f, pointerMoveSpeed);
        navigateInputDeadzone = Mathf.Max(0f, navigateInputDeadzone);
        aimPlaneHalfWidth = Mathf.Max(0f, aimPlaneHalfWidth);
        aimPlaneHalfHeight = Mathf.Max(0f, aimPlaneHalfHeight);

        cursorNormalized = ClampCursor(initialCursorNormalized);
        UpdateCrosshairImmediate();
    }

    private void OnValidate()
    {
        pointerMoveSpeed = Mathf.Max(0f, pointerMoveSpeed);
        navigateInputDeadzone = Mathf.Max(0f, navigateInputDeadzone);
        horizontalLimit = Mathf.Clamp01(horizontalLimit);
        verticalLimit = Mathf.Clamp01(verticalLimit);
        aimPlaneHalfWidth = Mathf.Max(0f, aimPlaneHalfWidth);
        aimPlaneHalfHeight = Mathf.Max(0f, aimPlaneHalfHeight);

        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (string.IsNullOrWhiteSpace(actionMapName))
            actionMapName = "UI";

        if (string.IsNullOrWhiteSpace(navigateActionName))
            navigateActionName = "NavigatePointer";

        initialCursorNormalized = ClampCursor(initialCursorNormalized);
    }

    private void OnEnable()
    {
        cursorNormalized = ClampCursor(initialCursorNormalized);
        currentNavigateInput = Vector2.zero;
        hasNavigateInputThisFrame = false;
        didCursorMoveThisFrame = false;
        ResolveActions();
        UpdateCrosshairImmediate();
    }

    private void Update()
    {
        RefreshResolvedActionMapIfNeeded();

        Vector2 input = navigateAction != null ? navigateAction.ReadValue<Vector2>() : Vector2.zero;
        if (clampInputToUnitCircle && input.sqrMagnitude > 1f)
            input.Normalize();

        currentNavigateInput = input;
        float deadzoneSqr = navigateInputDeadzone * navigateInputDeadzone;
        hasNavigateInputThisFrame = input.sqrMagnitude > deadzoneSqr;

        Vector2 previousCursorNormalized = cursorNormalized;

        float dt = Time.deltaTime;
        if (dt > 0f && input.sqrMagnitude > 0.000001f)
            cursorNormalized = ClampCursor(cursorNormalized + input * (pointerMoveSpeed * dt));

        didCursorMoveThisFrame = (cursorNormalized - previousCursorNormalized).sqrMagnitude > 0.00000001f;

        UpdateCrosshairImmediate();
    }

    public void SetCursorNormalized(Vector2 normalized)
    {
        cursorNormalized = ClampCursor(normalized);
        UpdateCrosshairImmediate();
    }

    private void ResolveActions()
    {
        resolvedActionMap = ResolveActionMap();
        navigateAction = resolvedActionMap != null ? resolvedActionMap.FindAction(navigateActionName, throwIfNotFound: false) : null;
    }

    private void RefreshResolvedActionMapIfNeeded()
    {
        InputActionMap desiredMap = ResolveActionMap();
        if (desiredMap == resolvedActionMap && navigateAction != null)
            return;

        ResolveActions();
    }

    private InputActionMap ResolveActionMap()
    {
        if (playerInput == null || playerInput.actions == null)
            return null;

        if (useCurrentActionMap && playerInput.currentActionMap != null)
            return playerInput.currentActionMap;

        if (string.IsNullOrWhiteSpace(actionMapName))
            return null;

        return playerInput.actions.FindActionMap(actionMapName, throwIfNotFound: false);
    }

    private Vector2 ClampCursor(Vector2 value)
    {
        return new Vector2(
            Mathf.Clamp(value.x, -horizontalLimit, horizontalLimit),
            Mathf.Clamp(value.y, -verticalLimit, verticalLimit));
    }

    private void UpdateCrosshairImmediate()
    {
        if (aimPlaneAnchor == null)
        {
            crosshairWorldPosition = crosshairTransform != null ? crosshairTransform.position : Vector3.zero;
            return;
        }

        crosshairWorldPosition = aimPlaneAnchor.position
                               + aimPlaneAnchor.right * (cursorNormalized.x * aimPlaneHalfWidth)
                               + aimPlaneAnchor.up * (cursorNormalized.y * aimPlaneHalfHeight);

        if (crosshairTransform != null)
        {
            crosshairTransform.position = crosshairWorldPosition;

            if (orientCrosshairToCamera && targetCamera != null)
            {
                Vector3 forward = targetCamera.transform.forward;
                if (forward.sqrMagnitude > 0.0001f)
                    crosshairTransform.rotation = Quaternion.LookRotation(forward, targetCamera.transform.up);
            }
        }
    }
}
