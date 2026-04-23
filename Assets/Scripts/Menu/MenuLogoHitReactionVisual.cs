using UnityEngine;

[DisallowMultipleComponent]
public sealed class MenuLogoHitReactionVisual : MonoBehaviour
{
    [Header("Base Pose")]
    [SerializeField] private bool captureBaseLocalPoseOnStart = true;

    [Header("Position Reaction")]
    [SerializeField, Min(0f)] private float maxPushDistance = 0.12f;
    [SerializeField, Min(0f)] private float positionImpulseStrength = 1f;
    [SerializeField, Min(0f)] private float positionReturnSpeed = 8f;
    [SerializeField] private Vector3 pushLocalAxis = new Vector3(0f, 0f, -1f);

    [Header("Rotation Reaction")]
    [SerializeField, Min(0f)] private float maxYawRotation = 8f;
    [SerializeField, Min(0f)] private float rotationImpulseStrength = 1f;
    [SerializeField, Min(0f)] private float rotationReturnSpeed = 8f;
    [SerializeField, Range(0f, 1f)] private float centerDeadZoneNormalized = 0.2f;

    [Header("Hit Cooldown")]
    [SerializeField] private bool useHitCooldown = false;
    [SerializeField, Min(0f)] private float hitCooldown = 0.05f;

    [Header("Clamping")]
    [SerializeField] private bool clampReaction = true;

    [Header("Timing")]
    [SerializeField] private bool useUnscaledTime = true;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private Vector3 baseLocalEuler;

    private Vector3 currentPositionOffset;
    private Vector3 currentRotationOffsetEuler;
    private float nextAllowedHitTime;
    private bool initialized;

    private void Awake()
    {
        ValidateFields();
        if (captureBaseLocalPoseOnStart)
            CaptureBaseLocalPose();
    }

    private void OnEnable()
    {
        if (!initialized && captureBaseLocalPoseOnStart)
            CaptureBaseLocalPose();
    }

    private void OnValidate()
    {
        ValidateFields();
    }

    private void LateUpdate()
    {
        if (!initialized)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f)
            return;

        float posT = positionReturnSpeed <= 0f ? 1f : 1f - Mathf.Exp(-positionReturnSpeed * dt);
        float rotT = rotationReturnSpeed <= 0f ? 1f : 1f - Mathf.Exp(-rotationReturnSpeed * dt);

        currentPositionOffset = Vector3.Lerp(currentPositionOffset, Vector3.zero, posT);
        currentRotationOffsetEuler = Vector3.Lerp(currentRotationOffsetEuler, Vector3.zero, rotT);

        transform.localPosition = baseLocalPosition + currentPositionOffset;
        transform.localRotation = Quaternion.Euler(baseLocalEuler + currentRotationOffsetEuler);
    }

    public void ApplyHitReaction(Vector3 worldHitPoint, Vector3 worldHitDirection, Collider hitCollider = null)
    {
        float now = useUnscaledTime ? Time.unscaledTime : Time.time;
        if (useHitCooldown && now < nextAllowedHitTime)
            return;

        if (useHitCooldown)
            nextAllowedHitTime = now + hitCooldown;

        if (!initialized)
            CaptureBaseLocalPose();

        Vector3 localHit = transform.InverseTransformPoint(worldHitPoint);
        GetNormalizedHitCoordinates(hitCollider, localHit, out float normalizedX, out float normalizedY);

        Vector3 localPushDirection = pushLocalAxis.sqrMagnitude > 0.000001f ? pushLocalAxis.normalized : Vector3.back;
        Vector3 pushOffset = localPushDirection * (maxPushDistance * positionImpulseStrength);
        currentPositionOffset += pushOffset;

        bool isCenterHit = Mathf.Abs(normalizedX) <= centerDeadZoneNormalized && Mathf.Abs(normalizedY) <= centerDeadZoneNormalized;
        if (!isCenterHit)
        {
            float edgeFactor = Mathf.InverseLerp(centerDeadZoneNormalized, 1f, Mathf.Abs(normalizedX));
            float yawSign = normalizedX == 0f ? 0f : Mathf.Sign(normalizedX);
            float yawAmount = yawSign * edgeFactor * maxYawRotation * rotationImpulseStrength;
            currentRotationOffsetEuler.y += yawAmount;
        }

        if (clampReaction)
        {
            currentPositionOffset = Vector3.ClampMagnitude(currentPositionOffset, maxPushDistance);
            currentRotationOffsetEuler.y = Mathf.Clamp(currentRotationOffsetEuler.y, -maxYawRotation, maxYawRotation);
        }

        transform.localPosition = baseLocalPosition + currentPositionOffset;
        transform.localRotation = Quaternion.Euler(baseLocalEuler + currentRotationOffsetEuler);
    }

    [ContextMenu("Capture Base Local Pose")]
    public void CaptureBaseLocalPose()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        baseLocalEuler = baseLocalRotation.eulerAngles;
        initialized = true;
    }

    private void GetNormalizedHitCoordinates(Collider hitCollider, Vector3 localHitPoint, out float normalizedX, out float normalizedY)
    {
        if (hitCollider != null && TryGetLocalBoundsXY(hitCollider, out float minX, out float maxX, out float minY, out float maxY))
        {
            normalizedX = NormalizeToSignedRange(localHitPoint.x, minX, maxX);
            normalizedY = NormalizeToSignedRange(localHitPoint.y, minY, maxY);
            return;
        }

        normalizedX = Mathf.Clamp(localHitPoint.x, -1f, 1f);
        normalizedY = Mathf.Clamp(localHitPoint.y, -1f, 1f);
    }

    private bool TryGetLocalBoundsXY(Collider hitCollider, out float minX, out float maxX, out float minY, out float maxY)
    {
        minX = float.PositiveInfinity;
        maxX = float.NegativeInfinity;
        minY = float.PositiveInfinity;
        maxY = float.NegativeInfinity;

        Bounds bounds = hitCollider.bounds;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        for (int ix = -1; ix <= 1; ix += 2)
        {
            for (int iy = -1; iy <= 1; iy += 2)
            {
                for (int iz = -1; iz <= 1; iz += 2)
                {
                    Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(ix, iy, iz));
                    Vector3 localCorner = transform.InverseTransformPoint(worldCorner);

                    if (localCorner.x < minX) minX = localCorner.x;
                    if (localCorner.x > maxX) maxX = localCorner.x;
                    if (localCorner.y < minY) minY = localCorner.y;
                    if (localCorner.y > maxY) maxY = localCorner.y;
                }
            }
        }

        return float.IsFinite(minX) && float.IsFinite(maxX) && float.IsFinite(minY) && float.IsFinite(maxY)
            && Mathf.Abs(maxX - minX) > 0.0001f
            && Mathf.Abs(maxY - minY) > 0.0001f;
    }

    private static float NormalizeToSignedRange(float value, float min, float max)
    {
        if (Mathf.Abs(max - min) <= 0.0001f)
            return 0f;

        float t = Mathf.InverseLerp(min, max, value);
        return Mathf.Lerp(-1f, 1f, t);
    }

    private void ValidateFields()
    {
        maxPushDistance = Mathf.Max(0f, maxPushDistance);
        positionImpulseStrength = Mathf.Max(0f, positionImpulseStrength);
        positionReturnSpeed = Mathf.Max(0f, positionReturnSpeed);
        maxYawRotation = Mathf.Max(0f, maxYawRotation);
        rotationImpulseStrength = Mathf.Max(0f, rotationImpulseStrength);
        rotationReturnSpeed = Mathf.Max(0f, rotationReturnSpeed);
        centerDeadZoneNormalized = Mathf.Clamp01(centerDeadZoneNormalized);
        hitCooldown = Mathf.Max(0f, hitCooldown);
    }
}
