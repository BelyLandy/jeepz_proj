using UnityEngine;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
public class FootSurfaceRotationIK : MonoBehaviour
{
    public enum IdleSolveMode
    {
        AlwaysSolve,
        FreezeLastValid,
        FadeOutConstraint
    }

    [Header("Constraint")]
    [SerializeField] private MultiRotationConstraint rotationConstraint;
    [SerializeField] private Transform constrainedFoot;

    [Tooltip("Обязательно лучше задать сюда root/hips/объект, который определяет направление персонажа.")]
    [SerializeField] private Transform characterForward;

    [SerializeField] private bool captureOffsetOnStart = true;
    [SerializeField] private Vector3 additionalRotationOffsetEuler = Vector3.zero;

    [Header("Foot points")]
    [Tooltip("Точка под пяткой")]
    [SerializeField] private Transform heelPoint;

    [Tooltip("Доп. точка между пяткой и носком, лучше чуть ближе к пятке")]
    [SerializeField] private Transform supportPoint;

    [Tooltip("Точка под носком")]
    [SerializeField] private Transform toePoint;

    [Header("Ground detection")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0.01f)] private float rayStartHeight = 0.35f;
    [SerializeField, Min(0.05f)] private float rayLength = 1.0f;
    [SerializeField, Min(0f)] private float sphereCastRadius = 0.03f;
    [SerializeField, Min(0f)] private float soleOffset = 0.015f;

    [Header("Surface solve")]
    [Range(0f, 1f)]
    [SerializeField] private float normalInfluence = 1f;

    [Tooltip("Вес дополнительной точки supportPoint при усреднении нормали.")]
    [SerializeField, Min(0f)] private float supportNormalWeight = 1.25f;

    [Tooltip("Обычно лучше оставить false. Тогда yaw не будет дёргаться на ровной поверхности.")]
    [SerializeField] private bool solveYawFromGround = false;

    [Range(0f, 1f)]
    [SerializeField] private float groundForwardInfluence = 0.2f;

    [Tooltip("Начиная с какого наклона поверхности разрешать подмешивать forward от heel->toe.")]
    [SerializeField, Range(0f, 89f)] private float minSlopeToUseGroundForward = 10f;

    [Header("Smoothing")]
    [SerializeField, Min(0f)] private float normalSmoothingSpeed = 20f;
    [SerializeField, Min(0f)] private float rotationLerpSpeed = 18f;
    [SerializeField, Min(0f)] private float weightLerpSpeed = 10f;
    [SerializeField, Min(0f)] private float maxDegreesPerSecond = 720f;
    [SerializeField, Min(0f)] private float rotationDeadZoneDegrees = 0.2f;

    [SerializeField] private bool keepLastValidWhenNoGround = true;

    [Header("Idle handling")]
    [SerializeField] private IdleSolveMode idleSolveMode = IdleSolveMode.FreezeLastValid;
    [SerializeField] private Rigidbody movementRigidbody;
    [SerializeField] private Transform movementReference;
    [SerializeField] private bool ignoreVerticalVelocity = true;
    [SerializeField, Min(0f)] private float moveThreshold = 0.05f;

    [Tooltip("Если хочешь из другого скрипта вручную задавать, движется ли персонаж")]
    [SerializeField] private bool useExternalMovingFlag = false;
    [SerializeField] private bool externalIsMoving = true;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;

    private struct FootSample
    {
        public bool valid;
        public Vector3 origin;
        public Vector3 point;
        public Vector3 normal;
    }

    private Quaternion capturedOffset = Quaternion.identity;

    private Quaternion lastSolvedRotation;
    private bool hasLastSolvedRotation;

    private Vector3 smoothedSurfaceUp;
    private bool hasSmoothedSurfaceUp;

    private Vector3 lastMovePos;
    private bool hasMovePos;

    private void Reset()
    {
        rotationConstraint = GetComponent<MultiRotationConstraint>();
    }

    private void Awake()
    {
        CacheRefs();

        if (captureOffsetOnStart)
            CaptureRotationOffset();
    }

    private void OnEnable()
    {
        hasMovePos = false;
        hasSmoothedSurfaceUp = false;

        if (captureOffsetOnStart)
            CaptureRotationOffset();
    }

    private void OnValidate()
    {
        CacheRefs();
    }

    [ContextMenu("Capture Rotation Offset")]
    private void CaptureRotationOffset()
    {
        Vector3 up = GetReferenceUp();
        Vector3 forward = GetReferenceForward(up);

        Quaternion baseRotation = BuildRotationFromUpForward(up, forward);
        capturedOffset = Quaternion.Inverse(baseRotation) * transform.rotation;
    }

    public void SetExternalMoving(bool isMoving)
    {
        externalIsMoving = isMoving;
    }

    private void Update()
    {
        CacheRefs();

        bool isMoving = IsMoving();

        if (!isMoving)
        {
            if (idleSolveMode == IdleSolveMode.FadeOutConstraint)
            {
                UpdateConstraintWeight(0f);
                return;
            }

            if (idleSolveMode == IdleSolveMode.FreezeLastValid && hasLastSolvedRotation)
            {
                ApplyRotation(lastSolvedRotation);
                UpdateConstraintWeight(1f);
                return;
            }
        }

        if (TrySolveRotation(out Quaternion solvedRotation))
        {
            lastSolvedRotation = solvedRotation;
            hasLastSolvedRotation = true;

            ApplyRotation(solvedRotation);
            UpdateConstraintWeight(1f);
        }
        else if (keepLastValidWhenNoGround && hasLastSolvedRotation)
        {
            ApplyRotation(lastSolvedRotation);
            UpdateConstraintWeight(1f);
        }
        else
        {
            UpdateConstraintWeight(0f);
        }
    }

    private void CacheRefs()
    {
        if (rotationConstraint == null)
            rotationConstraint = GetComponent<MultiRotationConstraint>();
    }

    private bool TrySolveRotation(out Quaternion solvedRotation)
    {
        solvedRotation = transform.rotation;

        FootSample heel = SampleGround(heelPoint);
        FootSample support = SampleGround(supportPoint);
        FootSample toe = SampleGround(toePoint);

        if (!heel.valid && !support.valid && !toe.valid)
            return false;

        Vector3 referenceUp = GetReferenceUp();
        Vector3 rawSurfaceUp = ComputeRawSurfaceUp(heel, support, toe, referenceUp);

        if (!hasSmoothedSurfaceUp)
        {
            smoothedSurfaceUp = rawSurfaceUp;
            hasSmoothedSurfaceUp = true;
        }
        else
        {
            float tNormal = GetDampFactor(normalSmoothingSpeed);
            smoothedSurfaceUp = Vector3.Slerp(smoothedSurfaceUp, rawSurfaceUp, tNormal).normalized;
        }

        Vector3 solvedUp = Vector3.Slerp(referenceUp, smoothedSurfaceUp, normalInfluence).normalized;

        // По умолчанию yaw берём от персонажа, а не от heel->toe.
        Vector3 finalForward = GetReferenceForward(solvedUp);

        // Опционально можно немного подмешивать forward от земли на заметных наклонах.
        if (solveYawFromGround && TryGetGroundForward(heel, support, toe, solvedUp, out Vector3 groundForward))
        {
            if (Vector3.Dot(groundForward, finalForward) < 0f)
                groundForward = -groundForward;

            float slope = Vector3.Angle(referenceUp, solvedUp);
            float slopeFactor = Mathf.InverseLerp(minSlopeToUseGroundForward, 45f, slope);
            float blend = groundForwardInfluence * slopeFactor;

            finalForward = Vector3.Slerp(finalForward, groundForward, blend).normalized;
        }

        Quaternion surfaceRotation = BuildRotationFromUpForward(solvedUp, finalForward);
        solvedRotation = surfaceRotation * capturedOffset * Quaternion.Euler(additionalRotationOffsetEuler);

        return true;
    }

    private Vector3 ComputeRawSurfaceUp(FootSample heel, FootSample support, FootSample toe, Vector3 fallbackUp)
    {
        Vector3 sum = Vector3.zero;
        float totalWeight = 0f;

        if (heel.valid)
        {
            sum += heel.normal;
            totalWeight += 1f;
        }

        if (toe.valid)
        {
            sum += toe.normal;
            totalWeight += 1f;
        }

        if (support.valid)
        {
            sum += support.normal * supportNormalWeight;
            totalWeight += supportNormalWeight;
        }

        if (totalWeight <= 0f)
            return fallbackUp;

        Vector3 up = (sum / totalWeight).normalized;

        if (up.sqrMagnitude < 0.000001f)
            up = fallbackUp;

        return up;
    }

    private bool TryGetGroundForward(FootSample heel, FootSample support, FootSample toe, Vector3 up, out Vector3 groundForward)
    {
        groundForward = Vector3.zero;

        if (heel.valid && toe.valid)
        {
            Vector3 v = Vector3.ProjectOnPlane(toe.point - heel.point, up);
            if (v.sqrMagnitude > 0.000001f)
            {
                groundForward = v.normalized;
                return true;
            }
        }

        if (heel.valid && support.valid)
        {
            Vector3 v = Vector3.ProjectOnPlane(support.point - heel.point, up);
            if (v.sqrMagnitude > 0.000001f)
            {
                groundForward = v.normalized;
                return true;
            }
        }

        if (support.valid && toe.valid)
        {
            Vector3 v = Vector3.ProjectOnPlane(toe.point - support.point, up);
            if (v.sqrMagnitude > 0.000001f)
            {
                groundForward = v.normalized;
                return true;
            }
        }

        return false;
    }

    private Quaternion BuildRotationFromUpForward(Vector3 targetUp, Vector3 targetForward)
    {
        targetUp = targetUp.normalized;
        targetForward = Vector3.ProjectOnPlane(targetForward, targetUp);

        if (targetForward.sqrMagnitude < 0.000001f)
            targetForward = Vector3.ProjectOnPlane(GetReferenceForward(targetUp), targetUp);

        if (targetForward.sqrMagnitude < 0.000001f)
            targetForward = Vector3.ProjectOnPlane(Vector3.forward, targetUp);

        targetForward.Normalize();

        // 1. Мировой up -> нужный up
        Quaternion upRotation = Quaternion.FromToRotation(Vector3.up, targetUp);

        // 2. После этого доворачиваем forward по плоскости поверхности
        Vector3 forwardAfterUp = Vector3.ProjectOnPlane(upRotation * Vector3.forward, targetUp);

        if (forwardAfterUp.sqrMagnitude < 0.000001f)
            forwardAfterUp = Vector3.ProjectOnPlane(upRotation * Vector3.right, targetUp);

        forwardAfterUp.Normalize();

        Quaternion forwardRotation = Quaternion.FromToRotation(forwardAfterUp, targetForward);

        return forwardRotation * upRotation;
    }

    private FootSample SampleGround(Transform point)
    {
        FootSample sample = default;

        if (point == null)
            return sample;

        Vector3 up = GetReferenceUp();
        Vector3 origin = point.position + up * rayStartHeight;
        Vector3 direction = -up;

        sample.origin = origin;

        bool hasHit;
        RaycastHit hit;

        if (sphereCastRadius > 0.0001f)
        {
            hasHit = Physics.SphereCast(
                origin,
                sphereCastRadius,
                direction,
                out hit,
                rayLength,
                groundMask,
                QueryTriggerInteraction.Ignore
            );
        }
        else
        {
            hasHit = Physics.Raycast(
                origin,
                direction,
                out hit,
                rayLength,
                groundMask,
                QueryTriggerInteraction.Ignore
            );
        }

        if (hasHit)
        {
            sample.valid = true;
            sample.point = hit.point + hit.normal * soleOffset;
            sample.normal = hit.normal.normalized;

            if (drawDebug)
            {
                Debug.DrawLine(origin, hit.point, Color.green);
                Debug.DrawRay(hit.point, hit.normal * 0.12f, Color.cyan);
            }
        }
        else if (drawDebug)
        {
            Debug.DrawLine(origin, origin + direction * rayLength, Color.red);
        }

        return sample;
    }

    private bool IsMoving()
    {
        if (useExternalMovingFlag)
            return externalIsMoving;

        Vector3 up = GetReferenceUp();

        if (movementRigidbody != null)
        {
            Vector3 velocity = GetRigidbodyVelocity(movementRigidbody);

            if (ignoreVerticalVelocity)
                velocity = Vector3.ProjectOnPlane(velocity, up);

            return velocity.magnitude > moveThreshold;
        }

        if (movementReference != null)
        {
            if (!hasMovePos)
            {
                lastMovePos = movementReference.position;
                hasMovePos = true;
                return false;
            }

            Vector3 delta = movementReference.position - lastMovePos;
            lastMovePos = movementReference.position;

            if (ignoreVerticalVelocity)
                delta = Vector3.ProjectOnPlane(delta, up);

            float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            return speed > moveThreshold;
        }

        return true;
    }

    private void ApplyRotation(Quaternion targetRotation)
    {
        if (!Application.isPlaying)
        {
            transform.rotation = targetRotation;
            return;
        }

        float angleToTarget = Quaternion.Angle(transform.rotation, targetRotation);

        if (angleToTarget <= rotationDeadZoneDegrees)
            return;

        Quaternion limitedTarget = targetRotation;

        if (maxDegreesPerSecond > 0f)
        {
            limitedTarget = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                maxDegreesPerSecond * Time.deltaTime
            );
        }

        if (rotationLerpSpeed > 0f)
        {
            float t = GetDampFactor(rotationLerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, limitedTarget, t);
        }
        else
        {
            transform.rotation = limitedTarget;
        }
    }

    private void UpdateConstraintWeight(float targetWeight)
    {
        if (rotationConstraint == null)
            return;

        if (!Application.isPlaying || weightLerpSpeed <= 0f)
        {
            rotationConstraint.weight = targetWeight;
            return;
        }

        float t = GetDampFactor(weightLerpSpeed);
        rotationConstraint.weight = Mathf.Lerp(rotationConstraint.weight, targetWeight, t);
    }

    private float GetDampFactor(float speed)
    {
        return 1f - Mathf.Exp(-speed * Time.deltaTime);
    }

    private Vector3 GetReferenceUp()
    {
        if (characterForward != null)
            return characterForward.up;

        return Vector3.up;
    }

    private Vector3 GetReferenceForward(Vector3 up)
    {
        Vector3 forward;

        if (characterForward != null)
            forward = characterForward.forward;
        else if (transform.parent != null)
            forward = transform.parent.forward;
        else
            forward = Vector3.forward;

        forward = Vector3.ProjectOnPlane(forward, up);

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(Vector3.forward, up);

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(Vector3.right, up);

        return forward.normalized;
    }

    private Vector3 GetRigidbodyVelocity(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }
}