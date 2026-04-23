using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyPerception25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform eyeOrigin;
    [SerializeField] private Transform explicitTarget;
    [SerializeField] private EnemyCharacter25D character;

    [Header("Detection")]
    [SerializeField, Min(0f)] private float detectionRadius = 18f;
    [SerializeField, Range(1f, 179f)] private float fieldOfView = 120f;
    [SerializeField] private LayerMask obstructionMask = ~0;
    [SerializeField, Min(0f)] private float memoryDuration = 2f;
    [SerializeField, Min(0f)] private float loseTargetGraceTime = 0.2f;
    [SerializeField] private bool requireLineOfSightToAcquire = true;
    [SerializeField] private Vector3 targetAimOffset = new Vector3(0f, 0.9f, 0f);

    [Header("Eye Tracking")]
    [SerializeField, Min(0f)] private float eyeTurnSpeedDegreesPerSecond = 240f;
    [SerializeField] private float idleEyeAngleWhenFacingRight = 0f;
    [SerializeField] private float idleEyeAngleWhenFacingLeft = 180f;
    [SerializeField] private float eyeForwardAngleOffsetDegrees = 0f;

    [Header("Body Turn From Look")]
    [SerializeField] private bool enableBodyTurnFromEyeTracking = true;
    [SerializeField, Range(0f, 179f)] private float bodyTurnThresholdDegrees = 100f;
    [SerializeField, Min(0f)] private float bodyTurnHysteresisDegrees = 10f;
    [SerializeField, Min(0f)] private float bodyTurnCooldown = 0.15f;

    [Header("Gizmos")]
    [SerializeField] private bool drawPerceptionGizmos = true;
    [SerializeField] private bool drawLastKnownPositionGizmo = true;

    private Transform currentTarget;
    private bool targetVisible;
    private bool hasLineOfSight;
    private bool hasLastKnownPosition;
    private bool hasTrackedTarget;
    private Vector3 lastKnownTargetPosition;
    private Vector3 targetVelocityEstimate;
    private Vector3 previousTargetPosition;
    private bool hadPreviousSample;
    private float lastSeenTime = float.NegativeInfinity;
    private float timeOutsideCone;
    private float nextBodyTurnAllowedTime = float.NegativeInfinity;

    public Transform CurrentTarget => currentTarget;
    public bool HasTarget => currentTarget != null;
    public bool IsTargetVisible => targetVisible;
    public bool HasLineOfSight => hasLineOfSight;
    public bool HasLastKnownPosition => hasLastKnownPosition;
    public bool HasTrackedTarget => hasTrackedTarget;
    public Vector3 LastKnownTargetPosition => lastKnownTargetPosition;
    public Vector3 TargetVelocityEstimate => targetVelocityEstimate;
    public LayerMask ObstructionMask => obstructionMask;
    public float TimeSinceLastSeen => targetVisible ? 0f : (Time.time - lastSeenTime);
    public Vector3 EyePosition => eyeOrigin != null ? eyeOrigin.position : transform.position;
    public Vector3 CurrentTargetAimPosition => GetTargetAimPosition(currentTarget);
    public float CurrentEyeAngleDegrees => GetCurrentEyeAngle();
    public Vector3 EyeForwardDirection => GetEyeForwardDirection(GetCurrentEyeAngle());

    private void Reset()
    {
        if (eyeOrigin == null)
            eyeOrigin = transform;
        if (character == null)
            character = GetComponent<EnemyCharacter25D>();

        ClampSettings();
    }

    private void Awake()
    {
        if (eyeOrigin == null)
            eyeOrigin = transform;
        if (character == null)
            character = GetComponent<EnemyCharacter25D>();

        ClampSettings();
        ResolveTargetReference();

        if (eyeOrigin != null)
            SetCurrentEyeAngle(GetIdleEyeAngleForFacing());
    }

    private void OnValidate()
    {
        if (eyeOrigin == null)
            eyeOrigin = transform;
        if (character == null)
            character = GetComponent<EnemyCharacter25D>();

        ClampSettings();
    }

    private void Update()
    {
        ResolveTargetReference();
        UpdateVelocityEstimate();
        EvaluateVisibility();
        UpdateEyeTracking(Time.deltaTime);
        UpdateBodyTurnFromEyeTracking();
    }

    public Vector3 GetAimPosition()
    {
        if (hasTrackedTarget && currentTarget != null)
            return GetTargetAimPosition(currentTarget);

        return hasLastKnownPosition ? lastKnownTargetPosition : transform.position;
    }

    public void ClearLastKnownPosition()
    {
        hasLastKnownPosition = false;
        hasTrackedTarget = false;
        targetVisible = false;
        timeOutsideCone = 0f;
        lastSeenTime = float.NegativeInfinity;
    }

    private void ResolveTargetReference()
    {
        if (explicitTarget != null)
        {
            currentTarget = explicitTarget;
            return;
        }

        if (currentTarget != null)
            return;

        HeroHurtbox25D heroHurtbox = Object.FindAnyObjectByType<HeroHurtbox25D>();
        if (heroHurtbox != null)
        {
            currentTarget = heroHurtbox.transform.root;
            return;
        }

        HeroHealth25D heroHealth = Object.FindAnyObjectByType<HeroHealth25D>();
        if (heroHealth != null)
        {
            currentTarget = heroHealth.transform.root;
            return;
        }

        RBCharacter25D heroCharacter = Object.FindAnyObjectByType<RBCharacter25D>();
        if (heroCharacter != null)
            currentTarget = heroCharacter.transform.root;
    }

    private void UpdateVelocityEstimate()
    {
        if (currentTarget == null)
        {
            targetVelocityEstimate = Vector3.zero;
            hadPreviousSample = false;
            return;
        }

        Vector3 currentPosition = GetTargetAimPosition(currentTarget);
        float dt = Time.deltaTime;
        if (hadPreviousSample && dt > 0.0001f)
            targetVelocityEstimate = (currentPosition - previousTargetPosition) / dt;
        else
            targetVelocityEstimate = Vector3.zero;

        previousTargetPosition = currentPosition;
        hadPreviousSample = true;
    }

    private void EvaluateVisibility()
    {
        targetVisible = false;
        hasLineOfSight = false;

        if (currentTarget == null)
        {
            hasTrackedTarget = false;
            timeOutsideCone = 0f;
            if (Time.time - lastSeenTime > memoryDuration)
                hasLastKnownPosition = false;
            return;
        }

        Vector3 eyePos = EyePosition;
        Vector3 targetPos = GetTargetAimPosition(currentTarget);
        Vector3 toTarget = targetPos - eyePos;
        toTarget.z = 0f;
        float distance = toTarget.magnitude;

        if (distance <= 0.0001f)
        {
            targetVisible = true;
            hasTrackedTarget = true;
            hasLineOfSight = true;
            timeOutsideCone = 0f;
            lastSeenTime = Time.time;
            hasLastKnownPosition = true;
            lastKnownTargetPosition = targetPos;
            return;
        }

        if (distance > detectionRadius)
        {
            hasTrackedTarget = false;
            timeOutsideCone = 0f;
            if (Time.time - lastSeenTime > memoryDuration)
                hasLastKnownPosition = false;
            return;
        }

        Vector3 eyeForward = EyeForwardDirection;
        bool inCone = IsWithinEyeCone(eyeForward, toTarget);
        hasLineOfSight = ComputeLineOfSight(eyePos, currentTarget, toTarget, distance);

        if (!hasTrackedTarget)
        {
            if (inCone && (!requireLineOfSightToAcquire || hasLineOfSight))
            {
                hasTrackedTarget = true;
                targetVisible = true;
                timeOutsideCone = 0f;
                lastSeenTime = Time.time;
                hasLastKnownPosition = true;
                lastKnownTargetPosition = targetPos;
            }
            else if (Time.time - lastSeenTime > memoryDuration)
            {
                hasLastKnownPosition = false;
            }

            return;
        }

        if (inCone)
        {
            targetVisible = true;
            timeOutsideCone = 0f;
            lastSeenTime = Time.time;
            hasLastKnownPosition = true;
            lastKnownTargetPosition = targetPos;
            return;
        }

        timeOutsideCone += Time.deltaTime;
        if (timeOutsideCone < loseTargetGraceTime)
        {
            targetVisible = true;
            return;
        }

        hasTrackedTarget = false;
        targetVisible = false;
        timeOutsideCone = 0f;
        if (Time.time - lastSeenTime > memoryDuration)
            hasLastKnownPosition = false;
    }

    private void UpdateEyeTracking(float deltaTime)
    {
        if (eyeOrigin == null)
            return;

        float targetAngle = GetIdleEyeAngleForFacing();
        Vector3 eyePos = EyePosition;

        if (hasTrackedTarget && currentTarget != null)
        {
            targetAngle = GetAngleToWorldPosition(GetTargetAimPosition(currentTarget), eyePos);
        }
        else if (hasLastKnownPosition)
        {
            targetAngle = GetAngleToWorldPosition(lastKnownTargetPosition, eyePos);
        }

        float currentAngle = GetCurrentEyeAngle();
        float nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, eyeTurnSpeedDegreesPerSecond * deltaTime);
        SetCurrentEyeAngle(nextAngle);
    }

    private void UpdateBodyTurnFromEyeTracking()
    {
        if (!enableBodyTurnFromEyeTracking || character == null || Time.time < nextBodyTurnAllowedTime)
            return;

        if (character.HasManualFacingOverride)
            return;

        if (!hasTrackedTarget && !hasLastKnownPosition)
            return;

        Vector3 lookPos;
        if (hasTrackedTarget && currentTarget != null)
            lookPos = GetTargetAimPosition(currentTarget);
        else
            lookPos = lastKnownTargetPosition;

        float deltaX = lookPos.x - EyePosition.x;
        if (Mathf.Abs(deltaX) <= 0.01f)
            return;

        int desiredFacing = deltaX >= 0f ? 1 : -1;
        if (desiredFacing == character.FacingSign)
            return;

        float currentEyeAngle = GetCurrentEyeAngle();
        float baseAngle = GetIdleEyeAngleForFacing(character.FacingSign);
        float relativeAngle = Mathf.Abs(Mathf.DeltaAngle(baseAngle, currentEyeAngle));
        float threshold = Mathf.Clamp(bodyTurnThresholdDegrees + Mathf.Max(0f, bodyTurnHysteresisDegrees), 0f, 179f);
        if (relativeAngle < threshold)
            return;

        character.ForceFacingSign(desiredFacing);
        nextBodyTurnAllowedTime = Time.time + bodyTurnCooldown;
    }

    private float GetIdleEyeAngleForFacing()
    {
        int facing = character != null ? character.FacingSign : 1;
        return GetIdleEyeAngleForFacing(facing);
    }

    private float GetIdleEyeAngleForFacing(int facingSign)
    {
        return facingSign >= 0 ? idleEyeAngleWhenFacingRight : idleEyeAngleWhenFacingLeft;
    }

    private float GetCurrentEyeAngle()
    {
        if (eyeOrigin == null)
            return 0f;

        return NormalizeAngle180(eyeOrigin.localEulerAngles.z);
    }

    private void SetCurrentEyeAngle(float angleDegrees)
    {
        if (eyeOrigin == null)
            return;

        Vector3 euler = eyeOrigin.localEulerAngles;
        euler.z = angleDegrees;
        eyeOrigin.localEulerAngles = euler;
    }

    private Vector3 GetEyeForwardDirection(float eyeAngleDegrees)
    {
        float angle = (eyeAngleDegrees + eyeForwardAngleOffsetDegrees) * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector3.right;
        return direction.normalized;
    }

    private bool IsWithinEyeCone(Vector3 eyeForward, Vector3 toTarget)
    {
        if (toTarget.sqrMagnitude <= 0.0001f)
            return true;

        float angle = Vector3.Angle(eyeForward, toTarget.normalized);
        return angle <= fieldOfView * 0.5f;
    }

    private bool ComputeLineOfSight(Vector3 eyePos, Transform target, Vector3 toTarget, float distance)
    {
        if (distance <= 0.0001f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(
            eyePos,
            toTarget.normalized,
            distance,
            obstructionMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return true;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null)
                continue;

            Transform hitTransform = hits[i].collider.transform;
            if (target != null && (hitTransform == target || hitTransform.IsChildOf(target)))
                continue;

            return false;
        }

        return true;
    }

    private float GetAngleToWorldPosition(Vector3 worldPosition, Vector3 fromPosition)
    {
        Vector3 direction = worldPosition - fromPosition;
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return GetCurrentEyeAngle();

        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - eyeForwardAngleOffsetDegrees;
    }

    private static float NormalizeAngle180(float angleDegrees)
    {
        return Mathf.DeltaAngle(0f, angleDegrees);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawPerceptionGizmos)
            return;

        Vector3 eyePos = EyePosition;
        float eyeAngle = Application.isPlaying ? GetCurrentEyeAngle() : NormalizeAngle180(eyeOrigin != null ? eyeOrigin.localEulerAngles.z : 0f);
        Vector3 eyeForward = GetEyeForwardDirection(eyeAngle);
        float rayLength = Mathf.Max(0.25f, detectionRadius);

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.85f);
        Gizmos.DrawWireSphere(eyePos, detectionRadius);

        Gizmos.color = Color.white;
        Gizmos.DrawSphere(eyePos, 0.06f);

        float halfFov = fieldOfView * 0.5f;
        Vector3 leftRay = Quaternion.AngleAxis(halfFov, Vector3.forward) * eyeForward;
        Vector3 rightRay = Quaternion.AngleAxis(-halfFov, Vector3.forward) * eyeForward;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawLine(eyePos, eyePos + eyeForward * rayLength);
        Gizmos.DrawLine(eyePos, eyePos + leftRay * rayLength);
        Gizmos.DrawLine(eyePos, eyePos + rightRay * rayLength);

        if (currentTarget != null)
        {
            Vector3 targetPos = GetTargetAimPosition(currentTarget);
            Gizmos.color = targetVisible ? Color.green : (hasLineOfSight ? new Color(1f, 0.8f, 0f, 1f) : Color.red);
            Gizmos.DrawLine(eyePos, targetPos);
            Gizmos.DrawSphere(targetPos, 0.05f);
        }
        else if (drawLastKnownPositionGizmo && hasLastKnownPosition)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(eyePos, lastKnownTargetPosition);
        }

        if (drawLastKnownPositionGizmo && hasLastKnownPosition)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastKnownTargetPosition, 0.14f);
            Gizmos.DrawLine(lastKnownTargetPosition + Vector3.left * 0.12f, lastKnownTargetPosition + Vector3.right * 0.12f);
            Gizmos.DrawLine(lastKnownTargetPosition + Vector3.up * 0.12f, lastKnownTargetPosition + Vector3.down * 0.12f);
        }
    }

    private Vector3 GetTargetAimPosition(Transform target)
    {
        if (target == null)
            return transform.position;

        Vector3 position = target.position + targetAimOffset;
        position.z = EyePosition.z;
        return position;
    }

    private void ClampSettings()
    {
        detectionRadius = Mathf.Max(0f, detectionRadius);
        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
        memoryDuration = Mathf.Max(0f, memoryDuration);
        loseTargetGraceTime = Mathf.Max(0f, loseTargetGraceTime);
        eyeTurnSpeedDegreesPerSecond = Mathf.Max(0f, eyeTurnSpeedDegreesPerSecond);
        bodyTurnThresholdDegrees = Mathf.Clamp(bodyTurnThresholdDegrees, 0f, 179f);
        bodyTurnHysteresisDegrees = Mathf.Max(0f, bodyTurnHysteresisDegrees);
        bodyTurnCooldown = Mathf.Max(0f, bodyTurnCooldown);
    }
}
