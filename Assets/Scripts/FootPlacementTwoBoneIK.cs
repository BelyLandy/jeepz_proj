using UnityEngine;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
public class FootPlacementTwoBoneIK : MonoBehaviour
{
    public enum IdleSolveMode
    {
        AlwaysSolve,
        FreezeLastValid,
        FadeOutIK
    }

    private struct FootSample
    {
        public bool valid;
        public Vector3 origin;
        public Vector3 point;
        public Vector3 normal;
    }

    [Header("IK")]
    [SerializeField] private TwoBoneIKConstraint legIK;
    [SerializeField] private Transform ikTarget;
    [SerializeField] private Transform animatedFoot;
    [SerializeField] private Transform characterForward;

    [Header("Character State")]
    [Tooltip("���� ����� ������ � RBCharacter25D.")]
    [SerializeField] private RBCharacter25D characterMotor;

    [SerializeField] private Transform airbornePoseReference;

    [SerializeField] private Transform heelPoint;
    [SerializeField] private Transform supportPoint;
    [SerializeField] private Transform toePoint;

    [SerializeField] private Transform plantPoint;

    [Header("Capture")]
    [SerializeField] private bool captureOffsetsOnStart = true;
    [SerializeField] private Vector3 additionalRotationOffsetEuler = Vector3.zero;
    [SerializeField] private Vector3 additionalTargetLocalOffset = Vector3.zero;

    [Header("Ground detection")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0.01f)] private float rayStartHeight = 0.35f;
    [SerializeField, Min(0.05f)] private float rayLength = 1.0f;
    [SerializeField, Min(0f)] private float sphereCastRadius = 0.03f;
    [SerializeField, Min(0f)] private float soleOffset = 0.01f;

    [Header("Surface rotation")]
    [Range(0f, 1f)]
    [SerializeField] private float normalInfluence = 1f;

    [SerializeField, Min(0f)] private float supportNormalWeight = 1.25f;
    [SerializeField, Min(0f)] private float plantNormalWeight = 1f;

    [SerializeField] private bool solveYawFromGround = false;

    [Range(0f, 1f)]
    [SerializeField] private float groundForwardInfluence = 0.2f;

    [SerializeField, Range(0f, 89f)] private float minSlopeToUseGroundForward = 10f;

    [Header("Surface position")]
    [SerializeField, Min(0f)] private float supportPositionWeight = 1.5f;

    [SerializeField] private bool preferHighestSample = false;

    [Range(0f, 1f)]
    [SerializeField] private float highestSampleBlend = 0.75f;

    [SerializeField, Min(0f)] private float highestSampleMinHeightDifference = 0.03f;

    [Header("Airborne handling")]
    [SerializeField] private bool disableFootPlacementInAir = true;

    [SerializeField, Min(0f)] private float airborneDisableDelay = 0.02f;

    [SerializeField] private bool forceAirborneByVerticalVelocity = true;

    [SerializeField, Min(0f)] private float airborneVerticalSpeedThreshold = 0.15f;

    [Range(0f, 1f)]
    [SerializeField] private float airborneIKWeight = 0f;

    [SerializeField] private bool returnTargetToAirPoseInAir = true;
    [SerializeField, Min(0f)] private float airbornePositionLerpMultiplier = 1.6f;
    [SerializeField, Min(0f)] private float airborneRotationLerpMultiplier = 1.6f;
    [SerializeField, Min(0f)] private float airborneWeightLerpMultiplier = 2f;

    [SerializeField] private bool snapTargetOnAirEnterIfFar = true;

    [SerializeField, Min(0f)] private float airEnterSnapDistance = 0.35f;
    [SerializeField, Min(0f)] private float airEnterSnapAngle = 50f;

    [Header("Landing recovery")]
    [SerializeField, Min(0f)] private float landingResolveTime = 0.08f;
    [SerializeField] private bool clearLastSolvedPoseOnAirEnter = true;
    [SerializeField] private bool snapToSolvedPoseOnLandingIfFar = true;
    [SerializeField, Min(0f)] private float landingSnapDistance = 0.25f;
    [SerializeField, Min(0f)] private float landingSnapAngle = 35f;

    [Header("Smoothing")]
    [SerializeField, Min(0f)] private float normalSmoothingSpeed = 20f;
    [SerializeField, Min(0f)] private float positionLerpSpeed = 20f;
    [SerializeField, Min(0f)] private float rotationLerpSpeed = 18f;
    [SerializeField, Min(0f)] private float weightLerpSpeed = 10f;
    [SerializeField, Min(0f)] private float maxMoveSpeed = 6f;
    [SerializeField, Min(0f)] private float maxDegreesPerSecond = 720f;
    [SerializeField, Min(0f)] private float positionDeadZone = 0.001f;
    [SerializeField, Min(0f)] private float rotationDeadZoneDegrees = 0.2f;

    [Header("Fallback")]
    [SerializeField] private bool keepLastValidWhenNoGround = true;
    [SerializeField] private IdleSolveMode idleSolveMode = IdleSolveMode.FreezeLastValid;

    [Header("Movement state")]
    [SerializeField] private Rigidbody movementRigidbody;
    [SerializeField] private Transform movementReference;
    [SerializeField] private bool ignoreVerticalVelocity = true;
    [SerializeField, Min(0f)] private float moveThreshold = 0.05f;

    [SerializeField] private bool useExternalMovingFlag = false;
    [SerializeField] private bool externalIsMoving = true;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;

    private Quaternion capturedRotationOffset = Quaternion.identity;
    private Vector3 capturedTargetLocalOffsetFromPlant = Vector3.zero;

    private Vector3 capturedAirLocalPositionFromReference = Vector3.zero;
    private Quaternion capturedAirLocalRotationFromReference = Quaternion.identity;

    private Vector3 smoothedSurfaceUp;
    private bool hasSmoothedSurfaceUp;

    private Vector3 lastSolvedPosition;
    private Quaternion lastSolvedRotation;
    private bool hasLastSolvedPose;

    private Vector3 lastMovePos;
    private bool hasMovePos;

    private float lastTimeCharacterGrounded = -999f;
    private bool wasAirborneLastFrame;
    private bool wasCharacterGroundedLastFrame;
    private float forceGroundResolveUntilTime = -999f;

    private void Reset()
    {
        ikTarget = transform;
    }

    private void Awake()
    {
        CacheRefs();

        if (captureOffsetsOnStart)
            CaptureOffsets();
    }

    private void OnEnable()
    {
        hasSmoothedSurfaceUp = false;
        hasMovePos = false;
        wasAirborneLastFrame = false;
        wasCharacterGroundedLastFrame = false;
        lastTimeCharacterGrounded = Time.time;
        forceGroundResolveUntilTime = -999f;

        if (captureOffsetsOnStart)
            CaptureOffsets();
    }

    private void OnValidate()
    {
        CacheRefs();

        rayStartHeight = Mathf.Max(0.01f, rayStartHeight);
        rayLength = Mathf.Max(0.05f, rayLength);
        sphereCastRadius = Mathf.Max(0f, sphereCastRadius);
        soleOffset = Mathf.Max(0f, soleOffset);

        supportNormalWeight = Mathf.Max(0f, supportNormalWeight);
        plantNormalWeight = Mathf.Max(0f, plantNormalWeight);
        minSlopeToUseGroundForward = Mathf.Clamp(minSlopeToUseGroundForward, 0f, 89f);

        supportPositionWeight = Mathf.Max(0f, supportPositionWeight);
        highestSampleMinHeightDifference = Mathf.Max(0f, highestSampleMinHeightDifference);

        airborneDisableDelay = Mathf.Max(0f, airborneDisableDelay);
        airborneVerticalSpeedThreshold = Mathf.Max(0f, airborneVerticalSpeedThreshold);
        airbornePositionLerpMultiplier = Mathf.Max(0f, airbornePositionLerpMultiplier);
        airborneRotationLerpMultiplier = Mathf.Max(0f, airborneRotationLerpMultiplier);
        airborneWeightLerpMultiplier = Mathf.Max(0f, airborneWeightLerpMultiplier);
        airEnterSnapDistance = Mathf.Max(0f, airEnterSnapDistance);
        airEnterSnapAngle = Mathf.Max(0f, airEnterSnapAngle);

        landingResolveTime = Mathf.Max(0f, landingResolveTime);
        landingSnapDistance = Mathf.Max(0f, landingSnapDistance);
        landingSnapAngle = Mathf.Max(0f, landingSnapAngle);

        normalSmoothingSpeed = Mathf.Max(0f, normalSmoothingSpeed);
        positionLerpSpeed = Mathf.Max(0f, positionLerpSpeed);
        rotationLerpSpeed = Mathf.Max(0f, rotationLerpSpeed);
        weightLerpSpeed = Mathf.Max(0f, weightLerpSpeed);
        maxMoveSpeed = Mathf.Max(0f, maxMoveSpeed);
        maxDegreesPerSecond = Mathf.Max(0f, maxDegreesPerSecond);
        positionDeadZone = Mathf.Max(0f, positionDeadZone);
        rotationDeadZoneDegrees = Mathf.Max(0f, rotationDeadZoneDegrees);

        moveThreshold = Mathf.Max(0f, moveThreshold);
    }

    [ContextMenu("Capture Offsets")]
    private void CaptureOffsets()
    {
        CacheRefs();

        if (ikTarget == null)
            ikTarget = transform;

        Vector3 up = GetReferenceUp();
        Vector3 forward = GetReferenceForward(up);

        Quaternion baseRotation = BuildRotationFromUpForward(up, forward);
        capturedRotationOffset = Quaternion.Inverse(baseRotation) * ikTarget.rotation;

        Transform basePoint = plantPoint != null ? plantPoint : supportPoint;

        if (animatedFoot != null && basePoint != null && ikTarget != null)
        {
            Vector3 pointLocal = animatedFoot.InverseTransformPoint(basePoint.position);
            Vector3 targetLocal = animatedFoot.InverseTransformPoint(ikTarget.position);
            capturedTargetLocalOffsetFromPlant = targetLocal - pointLocal;
        }
        else
        {
            capturedTargetLocalOffsetFromPlant = Vector3.zero;
        }

        Transform airRef = GetAirPoseReference();
        if (airRef != null && ikTarget != null)
        {
            capturedAirLocalPositionFromReference = airRef.InverseTransformPoint(ikTarget.position);
            capturedAirLocalRotationFromReference = Quaternion.Inverse(airRef.rotation) * ikTarget.rotation;
        }
        else
        {
            capturedAirLocalPositionFromReference = Vector3.zero;
            capturedAirLocalRotationFromReference = Quaternion.identity;
        }
    }

    public void SetExternalMoving(bool isMoving)
    {
        externalIsMoving = isMoving;
    }

    private void CacheRefs()
    {
        if (ikTarget == null)
            ikTarget = transform;

        if (movementRigidbody == null && characterMotor != null)
            movementRigidbody = characterMotor.GetComponent<Rigidbody>();
    }

    private void Update()
    {
        CacheRefs();

        bool characterGrounded = QueryCharacterGrounded();
        if (characterGrounded)
            lastTimeCharacterGrounded = Time.time;

        bool justLanded = characterGrounded && !wasCharacterGroundedLastFrame;
        bool justLeftGround = !characterGrounded && wasCharacterGroundedLastFrame;

        if (justLeftGround)
        {
            hasSmoothedSurfaceUp = false;

            if (clearLastSolvedPoseOnAirEnter)
                hasLastSolvedPose = false;
        }

        if (justLanded)
        {
            hasSmoothedSurfaceUp = false;
            forceGroundResolveUntilTime = Time.time + landingResolveTime;
        }

        float verticalSpeed = QueryVerticalSpeed();
        bool strongAirSignal =
            !characterGrounded &&
            forceAirborneByVerticalVelocity &&
            Mathf.Abs(verticalSpeed) >= airborneVerticalSpeedThreshold;

        bool airborneNow =
            disableFootPlacementInAir &&
            !characterGrounded &&
            (strongAirSignal || (Time.time - lastTimeCharacterGrounded > airborneDisableDelay));

        if (airborneNow)
        {
            if (!wasAirborneLastFrame)
            {
                hasSmoothedSurfaceUp = false;

                if (returnTargetToAirPoseInAir)
                {
                    ComputeAirPose(out Vector3 airPos, out Quaternion airRot);

                    if (snapTargetOnAirEnterIfFar)
                    {
                        float dist = Vector3.Distance(ikTarget.position, airPos);
                        float ang = Quaternion.Angle(ikTarget.rotation, airRot);

                        if (dist >= airEnterSnapDistance || ang >= airEnterSnapAngle)
                        {
                            ikTarget.position = airPos;
                            ikTarget.rotation = airRot;
                        }
                    }
                }
            }

            if (returnTargetToAirPoseInAir)
            {
                ComputeAirPose(out Vector3 airPos, out Quaternion airRot);
                ApplyPose(airPos, airRot, airbornePositionLerpMultiplier, airborneRotationLerpMultiplier);
            }

            UpdateIKWeight(airborneIKWeight, airborneWeightLerpMultiplier);
            wasAirborneLastFrame = true;
            wasCharacterGroundedLastFrame = characterGrounded;
            return;
        }

        wasAirborneLastFrame = false;

        bool forceGroundResolve =
            characterGrounded &&
            Time.time <= forceGroundResolveUntilTime;

        bool isMoving = IsMoving();

        if (!forceGroundResolve && !isMoving)
        {
            if (idleSolveMode == IdleSolveMode.FadeOutIK)
            {
                UpdateIKWeight(0f, 1f);
                wasCharacterGroundedLastFrame = characterGrounded;
                return;
            }

            if (idleSolveMode == IdleSolveMode.FreezeLastValid && hasLastSolvedPose)
            {
                ApplyPose(lastSolvedPosition, lastSolvedRotation, 1f, 1f);
                UpdateIKWeight(1f, 1f);
                wasCharacterGroundedLastFrame = characterGrounded;
                return;
            }
        }

        if (TrySolvePose(out Vector3 solvedPosition, out Quaternion solvedRotation))
        {
            lastSolvedPosition = solvedPosition;
            lastSolvedRotation = solvedRotation;
            hasLastSolvedPose = true;

            if (justLanded && snapToSolvedPoseOnLandingIfFar)
            {
                float dist = Vector3.Distance(ikTarget.position, solvedPosition);
                float ang = Quaternion.Angle(ikTarget.rotation, solvedRotation);

                if (dist >= landingSnapDistance || ang >= landingSnapAngle)
                {
                    ikTarget.position = solvedPosition;
                    ikTarget.rotation = solvedRotation;
                }
            }

            ApplyPose(solvedPosition, solvedRotation, 1f, 1f);
            UpdateIKWeight(1f, 1f);
        }
        else if (keepLastValidWhenNoGround && hasLastSolvedPose && characterGrounded)
        {
            ApplyPose(lastSolvedPosition, lastSolvedRotation, 1f, 1f);
            UpdateIKWeight(1f, 1f);
        }
        else
        {
            UpdateIKWeight(0f, 1f);
        }

        wasCharacterGroundedLastFrame = characterGrounded;
    }

    private bool QueryCharacterGrounded()
    {
        if (characterMotor != null)
            return characterMotor.IsGroundedNow;

        return true;
    }

    private float QueryVerticalSpeed()
    {
        if (movementRigidbody != null)
        {
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
            return movementRigidbody.linearVelocity.y;
#else
            return movementRigidbody.velocity.y;
#endif
        }

        return 0f;
    }

    private Transform GetAirPoseReference()
    {
        if (airbornePoseReference != null)
            return airbornePoseReference;

        if (characterForward != null)
            return characterForward;

        if (ikTarget != null && ikTarget.parent != null)
            return ikTarget.parent;

        return null;
    }

    private void ComputeAirPose(out Vector3 position, out Quaternion rotation)
    {
        Transform airRef = GetAirPoseReference();

        if (airRef != null)
        {
            position = airRef.TransformPoint(capturedAirLocalPositionFromReference);
            rotation = airRef.rotation * capturedAirLocalRotationFromReference;
            return;
        }

        position = ikTarget.position;
        rotation = ikTarget.rotation;
    }

    private bool TrySolvePose(out Vector3 solvedPosition, out Quaternion solvedRotation)
    {
        solvedPosition = ikTarget.position;
        solvedRotation = ikTarget.rotation;

        FootSample heel = SampleGround(heelPoint);
        FootSample support = SampleGround(supportPoint);
        FootSample toe = SampleGround(toePoint);
        FootSample plant = SampleGround(plantPoint);

        if (!heel.valid && !support.valid && !toe.valid && !plant.valid)
            return false;

        Vector3 referenceUp = GetReferenceUp();
        Vector3 rawSurfaceUp = ComputeRawSurfaceUp(heel, support, toe, plant, referenceUp);

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

        Vector3 finalForward = GetReferenceForward(solvedUp);

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
        solvedRotation = surfaceRotation * capturedRotationOffset * Quaternion.Euler(additionalRotationOffsetEuler);

        Vector3 plantContactPoint = ComputePlantContactPoint(plant, heel, support, toe, solvedUp);
        Vector3 localOffset = capturedTargetLocalOffsetFromPlant + additionalTargetLocalOffset;
        solvedPosition = plantContactPoint + solvedRotation * localOffset;

        return true;
    }

    private Vector3 ComputeRawSurfaceUp(FootSample heel, FootSample support, FootSample toe, FootSample plant, Vector3 fallbackUp)
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

        if (plant.valid)
        {
            sum += plant.normal * plantNormalWeight;
            totalWeight += plantNormalWeight;
        }

        if (totalWeight <= 0f)
            return fallbackUp;

        Vector3 up = (sum / totalWeight).normalized;

        if (up.sqrMagnitude < 0.000001f)
            up = fallbackUp;

        return up;
    }

    private Vector3 ComputePlantContactPoint(FootSample plant, FootSample heel, FootSample support, FootSample toe, Vector3 up)
    {
        Vector3 point;

        if (plant.valid)
        {
            point = plant.point;
        }
        else
        {
            Vector3 sum = Vector3.zero;
            float totalWeight = 0f;

            if (heel.valid)
            {
                sum += heel.point;
                totalWeight += 1f;
            }

            if (toe.valid)
            {
                sum += toe.point;
                totalWeight += 1f;
            }

            if (support.valid)
            {
                sum += support.point * supportPositionWeight;
                totalWeight += supportPositionWeight;
            }

            if (totalWeight > 0f)
                point = sum / totalWeight;
            else if (support.valid)
                point = support.point;
            else if (heel.valid)
                point = heel.point;
            else
                point = toe.point;
        }

        if (preferHighestSample &&
            TryGetHighestSamplePoint(plant, heel, support, toe, up, out Vector3 highestPoint, out _))
        {
            float baseHeight = Vector3.Dot(point, up);
            float highestHeight = Vector3.Dot(highestPoint, up);
            float delta = highestHeight - baseHeight;

            if (delta >= highestSampleMinHeightDifference)
                point += up * (delta * highestSampleBlend);
        }

        return point;
    }

    private bool TryGetHighestSamplePoint(FootSample plant, FootSample heel, FootSample support, FootSample toe, Vector3 up, out Vector3 highestPoint, out float deltaFromAverage)
    {
        highestPoint = Vector3.zero;
        deltaFromAverage = 0f;

        bool hasAny = false;
        float highest = float.NegativeInfinity;
        float average = 0f;
        int count = 0;

        if (plant.valid)
        {
            float h = Vector3.Dot(plant.point, up);
            average += h;
            count++;

            if (h > highest)
            {
                highest = h;
                highestPoint = plant.point;
                hasAny = true;
            }
        }

        if (heel.valid)
        {
            float h = Vector3.Dot(heel.point, up);
            average += h;
            count++;

            if (h > highest)
            {
                highest = h;
                highestPoint = heel.point;
                hasAny = true;
            }
        }

        if (support.valid)
        {
            float h = Vector3.Dot(support.point, up);
            average += h;
            count++;

            if (h > highest)
            {
                highest = h;
                highestPoint = support.point;
                hasAny = true;
            }
        }

        if (toe.valid)
        {
            float h = Vector3.Dot(toe.point, up);
            average += h;
            count++;

            if (h > highest)
            {
                highest = h;
                highestPoint = toe.point;
                hasAny = true;
            }
        }

        if (!hasAny || count == 0)
            return false;

        average /= count;
        deltaFromAverage = highest - average;
        return true;
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

        Quaternion upRotation = Quaternion.FromToRotation(Vector3.up, targetUp);

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
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
            Vector3 velocity = movementRigidbody.linearVelocity;
#else
            Vector3 velocity = movementRigidbody.velocity;
#endif
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

    private void ApplyPose(Vector3 targetPosition, Quaternion targetRotation, float positionSpeedMultiplier, float rotationSpeedMultiplier)
    {
        ApplyPosition(targetPosition, positionSpeedMultiplier);
        ApplyRotation(targetRotation, rotationSpeedMultiplier);
    }

    private void ApplyPosition(Vector3 targetPosition, float speedMultiplier)
    {
        if (!Application.isPlaying)
        {
            ikTarget.position = targetPosition;
            return;
        }

        float distance = Vector3.Distance(ikTarget.position, targetPosition);
        if (distance <= positionDeadZone)
            return;

        float clampedMultiplier = Mathf.Max(0f, speedMultiplier);
        Vector3 limitedTarget = targetPosition;

        if (maxMoveSpeed > 0f)
        {
            limitedTarget = Vector3.MoveTowards(
                ikTarget.position,
                targetPosition,
                maxMoveSpeed * clampedMultiplier * Time.deltaTime
            );
        }

        if (positionLerpSpeed > 0f)
        {
            float t = GetDampFactor(positionLerpSpeed * clampedMultiplier);
            ikTarget.position = Vector3.Lerp(ikTarget.position, limitedTarget, t);
        }
        else
        {
            ikTarget.position = limitedTarget;
        }
    }

    private void ApplyRotation(Quaternion targetRotation, float speedMultiplier)
    {
        if (!Application.isPlaying)
        {
            ikTarget.rotation = targetRotation;
            return;
        }

        float angle = Quaternion.Angle(ikTarget.rotation, targetRotation);
        if (angle <= rotationDeadZoneDegrees)
            return;

        float clampedMultiplier = Mathf.Max(0f, speedMultiplier);
        Quaternion limitedTarget = targetRotation;

        if (maxDegreesPerSecond > 0f)
        {
            limitedTarget = Quaternion.RotateTowards(
                ikTarget.rotation,
                targetRotation,
                maxDegreesPerSecond * clampedMultiplier * Time.deltaTime
            );
        }

        if (rotationLerpSpeed > 0f)
        {
            float t = GetDampFactor(rotationLerpSpeed * clampedMultiplier);
            ikTarget.rotation = Quaternion.Slerp(ikTarget.rotation, limitedTarget, t);
        }
        else
        {
            ikTarget.rotation = limitedTarget;
        }
    }

    private void UpdateIKWeight(float targetWeight, float speedMultiplier)
    {
        if (legIK == null)
            return;

        if (!Application.isPlaying || weightLerpSpeed <= 0f)
        {
            legIK.weight = targetWeight;
            return;
        }

        float t = GetDampFactor(weightLerpSpeed * Mathf.Max(0f, speedMultiplier));
        legIK.weight = Mathf.Lerp(legIK.weight, targetWeight, t);
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
        else if (animatedFoot != null)
            forward = animatedFoot.forward;
        else if (ikTarget != null && ikTarget.parent != null)
            forward = ikTarget.parent.forward;
        else
            forward = Vector3.forward;

        forward = Vector3.ProjectOnPlane(forward, up);

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(Vector3.forward, up);

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(Vector3.right, up);

        return forward.normalized;
    }
}