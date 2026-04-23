using UnityEngine;

[DisallowMultipleComponent]
public sealed class MenuLogoCursorLocalRotation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MenuPointerController pointerController;

    [Header("Base Rotation")]
    [SerializeField] private bool captureBaseLocalRotationOnStart = true;

    [Header("Horizontal Influence")]
    [SerializeField] private float pitchFromHorizontal = 1.919f;
    [SerializeField] private float yawFromHorizontal = -28.997f;
    [SerializeField] private float rollFromHorizontal = -7.44f;

    [Header("Vertical Influence")]
    [SerializeField] private float pitchFromVertical = 0f;
    [SerializeField] private float yawFromVertical = 0f;
    [SerializeField] private float rollFromVertical = 0f;

    [Header("Smoothing")]
    [SerializeField, Min(0f)] private float rotationSmoothSpeed = 10f;
    [SerializeField] private bool useUnscaledTime = true;

    private Quaternion baseLocalRotation;
    private Vector3 baseLocalEuler;
    private bool initialized;

    private void Reset()
    {
        if (pointerController == null)
            pointerController = FindFirstObjectByType<MenuPointerController>();
    }

    private void Awake()
    {
        rotationSmoothSpeed = Mathf.Max(0f, rotationSmoothSpeed);
        if (captureBaseLocalRotationOnStart)
            CaptureBaseLocalRotation();
    }

    private void OnValidate()
    {
        rotationSmoothSpeed = Mathf.Max(0f, rotationSmoothSpeed);
    }

    private void OnEnable()
    {
        if (!initialized && captureBaseLocalRotationOnStart)
            CaptureBaseLocalRotation();
    }

    private void LateUpdate()
    {
        if (pointerController == null || !initialized)
            return;

        Vector2 cursor = pointerController.CursorNormalized;
        float offsetX = cursor.x * pitchFromHorizontal + cursor.y * pitchFromVertical;
        float offsetY = cursor.x * yawFromHorizontal + cursor.y * yawFromVertical;
        float offsetZ = cursor.x * rollFromHorizontal + cursor.y * rollFromVertical;

        Vector3 targetEuler = baseLocalEuler + new Vector3(offsetX, offsetY, offsetZ);
        Quaternion targetRotation = Quaternion.Euler(targetEuler);

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (rotationSmoothSpeed <= 0f || dt <= 0f)
        {
            transform.localRotation = targetRotation;
            return;
        }

        float t = 1f - Mathf.Exp(-rotationSmoothSpeed * dt);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, t);
    }

    [ContextMenu("Capture Base Local Rotation")]
    public void CaptureBaseLocalRotation()
    {
        baseLocalRotation = transform.localRotation;
        baseLocalEuler = transform.localEulerAngles;
        initialized = true;
    }

    public void SetBaseLocalRotation(Quaternion rotation)
    {
        baseLocalRotation = rotation;
        baseLocalEuler = rotation.eulerAngles;
        initialized = true;
    }
}
