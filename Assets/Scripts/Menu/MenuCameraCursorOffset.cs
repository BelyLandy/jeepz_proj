using UnityEngine;

[DisallowMultipleComponent]
public sealed class MenuCameraCursorOffset : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MenuPointerController pointerController;
    [SerializeField] private Transform cameraBaseLookTarget;
    [SerializeField] private Transform cameraOffsetLookTarget;

    [Header("Offset")]
    [SerializeField] private float maxYawOffset = 3f;
    [SerializeField] private float maxPitchOffset = 2f;
    [SerializeField, Min(0.0001f)] private float smoothTime = 0.15f;
    [SerializeField] private bool invertPitch = false;

    private Vector3 neutralLocalDirection = Vector3.forward;
    private float neutralDistance = 1f;
    private float currentYaw;
    private float currentPitch;
    private float yawVelocity;
    private float pitchVelocity;

    private void Awake()
    {
        CacheNeutralOffset();
    }

    private void OnValidate()
    {
        smoothTime = Mathf.Max(0.0001f, smoothTime);
    }

    private void OnEnable()
    {
        CacheNeutralOffset();
        ApplyOffsetImmediate(0f, 0f);
    }

    private void LateUpdate()
    {
        if (pointerController == null || cameraBaseLookTarget == null || cameraOffsetLookTarget == null)
            return;

        Vector2 cursor = pointerController.CursorNormalized;
        float desiredYaw = cursor.x * maxYawOffset;
        float pitchSign = invertPitch ? -1f : 1f;
        float desiredPitch = cursor.y * maxPitchOffset * pitchSign;

        currentYaw = Mathf.SmoothDamp(currentYaw, desiredYaw, ref yawVelocity, smoothTime);
        currentPitch = Mathf.SmoothDamp(currentPitch, desiredPitch, ref pitchVelocity, smoothTime);

        ApplyOffsetImmediate(currentYaw, currentPitch);
    }

    private void CacheNeutralOffset()
    {
        if (cameraBaseLookTarget == null || cameraOffsetLookTarget == null)
            return;

        Vector3 worldOffset = cameraOffsetLookTarget.position - cameraBaseLookTarget.position;
        Vector3 localOffset = cameraBaseLookTarget.InverseTransformDirection(worldOffset);

        if (localOffset.sqrMagnitude <= 0.000001f)
            localOffset = Vector3.forward;

        neutralDistance = Mathf.Max(0.0001f, localOffset.magnitude);
        neutralLocalDirection = localOffset.normalized;
    }

    private void ApplyOffsetImmediate(float yaw, float pitch)
    {
        if (cameraBaseLookTarget == null || cameraOffsetLookTarget == null)
            return;

        Quaternion localRotation = Quaternion.Euler(-pitch, yaw, 0f);
        Vector3 rotatedLocalOffset = localRotation * (neutralLocalDirection * neutralDistance);
        cameraOffsetLookTarget.position = cameraBaseLookTarget.TransformPoint(rotatedLocalOffset);
    }
}
