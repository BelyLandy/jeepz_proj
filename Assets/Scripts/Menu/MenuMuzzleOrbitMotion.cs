using UnityEngine;

[DisallowMultipleComponent]
public sealed class MenuMuzzleOrbitMotion : MonoBehaviour
{
    [Header("Space / Base")]
    [SerializeField] private bool useLocalSpace = true;
    [SerializeField] private bool captureInitialPositionOnStart = true;
    [SerializeField] private Vector3 centerOffset = Vector3.zero;

    [Header("Motion Shape")]
    [SerializeField] private float radiusX = 5f;
    [SerializeField] private float radiusY = 2.5f;
    [SerializeField] private float radiusZ = 0.0f;

    [Header("Speed / Phase")]
    [SerializeField, Min(0f)] private float cyclesPerSecond = 0.4f;
    [SerializeField] private float phaseOffsetDegrees = 0f;
    [SerializeField] private float zPhaseOffsetDegrees = 0f;
    [SerializeField] private float zFrequencyMultiplier = 1f;

    [Header("Time")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Optional Rotation")]
    [SerializeField] private bool rotateToMotion = false;
    [SerializeField, Min(0f)] private float rotationSmoothness = 10f;

    private Vector3 basePosition;
    private Vector3 previousPosition;
    private float elapsed;
    private bool initialized;

    private void OnEnable()
    {
        InitializeBasePosition();
        previousPosition = GetCurrentPosition();
    }

    private void OnValidate()
    {
        cyclesPerSecond = Mathf.Max(0f, cyclesPerSecond);
        rotationSmoothness = Mathf.Max(0f, rotationSmoothness);
    }

    private void Update()
    {
        if (!initialized)
            InitializeBasePosition();

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f)
            return;

        elapsed += dt;

        float angleRadians = ((elapsed * cyclesPerSecond * 360f) + phaseOffsetDegrees) * Mathf.Deg2Rad;
        float zAngleRadians = ((elapsed * cyclesPerSecond * 360f * zFrequencyMultiplier) + zPhaseOffsetDegrees) * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Cos(angleRadians) * radiusX,
            Mathf.Sin(angleRadians) * radiusY,
            Mathf.Sin(zAngleRadians) * radiusZ);

        Vector3 targetPosition = basePosition + centerOffset + offset;
        ApplyPosition(targetPosition);

        if (rotateToMotion)
            UpdateMotionRotation(dt);

        previousPosition = GetCurrentPosition();
    }

    private void InitializeBasePosition()
    {
        basePosition = captureInitialPositionOnStart ? GetCurrentPosition() : basePosition;
        initialized = true;
    }

    private Vector3 GetCurrentPosition()
    {
        return useLocalSpace ? transform.localPosition : transform.position;
    }

    private void ApplyPosition(Vector3 value)
    {
        if (useLocalSpace)
            transform.localPosition = value;
        else
            transform.position = value;
    }

    private void UpdateMotionRotation(float dt)
    {
        Vector3 currentPosition = GetCurrentPosition();
        Vector3 movement = currentPosition - previousPosition;
        if (movement.sqrMagnitude <= 0.000001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(movement.normalized, Vector3.up);
        float lerpFactor = rotationSmoothness <= 0f ? 1f : 1f - Mathf.Exp(-rotationSmoothness * dt);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lerpFactor);
    }
}
