using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeadTargetTracking25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform lookOrigin;
    [SerializeField] private Transform[] targets;

    [Header("Targeting")]
    [SerializeField, Range(0f, 180f)] private float fovHalfAngle = 70f;
    [SerializeField] private float viewDistance = 8f;
    [SerializeField] private float lostTargetGraceTime = 0.1f;
    [SerializeField] private bool preferCurrentTargetWhileValid = true;

    [Header("Visibility")]
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask occlusionMask = ~0;
    [SerializeField] private QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Debug")]
    [SerializeField] private bool debugLogTransitions = false;

    private Transform currentTarget;
    private Transform debugLastLoggedTarget;
    private Vector3 lastVisibleWorldDirection = Vector3.right;
    private float lastVisibleTargetTime = float.NegativeInfinity;
    private int lastEvaluationFrame = -1;
    private int lastEvaluationFacingSign = +1;
    private bool lastEvaluationHadDirection;
    private Vector3 lastEvaluatedWorldDirection = Vector3.right;
    private int previousTrackingFacingSign = +1;
    private bool hasPreviousTrackingFacingSign;

    public Transform CurrentTarget => currentTarget;
    public bool HasCurrentTarget => currentTarget != null;
    public Transform LookOrigin => lookOrigin != null ? lookOrigin : transform;

    private void Reset()
    {
        if (lookOrigin == null)
            lookOrigin = transform;
    }

    private void OnValidate()
    {
        fovHalfAngle = Mathf.Clamp(fovHalfAngle, 0f, 180f);
        viewDistance = Mathf.Max(0f, viewDistance);
        lostTargetGraceTime = Mathf.Max(0f, lostTargetGraceTime);
    }

    public bool TryGetLookDirection(int facingSign, out Vector3 worldDirection)
    {
        facingSign = facingSign < 0 ? -1 : 1;

        if (lastEvaluationFrame == Time.frameCount && lastEvaluationFacingSign == facingSign)
        {
            worldDirection = lastEvaluatedWorldDirection;
            return lastEvaluationHadDirection;
        }

        bool hasDirection = EvaluateLookDirection(facingSign, out worldDirection);

        lastEvaluationFrame = Time.frameCount;
        lastEvaluationFacingSign = facingSign;
        lastEvaluationHadDirection = hasDirection;
        lastEvaluatedWorldDirection = worldDirection;
        return hasDirection;
    }

    private bool EvaluateLookDirection(int facingSign, out Vector3 worldDirection)
    {
        worldDirection = Vector3.right;
        Vector3 origin = LookOrigin.position;
        bool facingChanged = hasPreviousTrackingFacingSign && previousTrackingFacingSign != facingSign;
        previousTrackingFacingSign = facingSign;
        hasPreviousTrackingFacingSign = true;

        Transform best = null;
        Vector3 bestDirection = Vector3.right;
        float bestDistanceSqr = float.PositiveInfinity;

        if (preferCurrentTargetWhileValid && IsTargetValid(currentTarget, facingSign, origin, out Vector3 currentDirection, out float currentDistanceSqr))
        {
            best = currentTarget;
            bestDirection = currentDirection;
            bestDistanceSqr = currentDistanceSqr;
        }
        else
        {
            Transform[] candidateTargets = targets;
            int count = candidateTargets != null ? candidateTargets.Length : 0;
            for (int i = 0; i < count; i++)
            {
                Transform candidate = candidateTargets[i];
                if (!IsTargetValid(candidate, facingSign, origin, out Vector3 candidateDirection, out float candidateDistanceSqr))
                    continue;

                if (candidateDistanceSqr < bestDistanceSqr)
                {
                    best = candidate;
                    bestDirection = candidateDirection;
                    bestDistanceSqr = candidateDistanceSqr;
                }
            }
        }

        if (best != null)
        {
            currentTarget = best;
            lastVisibleWorldDirection = bestDirection;
            lastVisibleTargetTime = Time.time;
            LogTargetTransition(best);
            worldDirection = bestDirection;
            return true;
        }

        if (!facingChanged && currentTarget != null && Time.time - lastVisibleTargetTime <= lostTargetGraceTime && lastVisibleWorldDirection.sqrMagnitude > 0.0001f)
        {
            LogTargetTransition(currentTarget);
            worldDirection = lastVisibleWorldDirection.normalized;
            return true;
        }

        if (currentTarget != null)
        {
            LogTargetTransition(null);
            currentTarget = null;
        }

        if (facingChanged)
            lastVisibleTargetTime = float.NegativeInfinity;

        return false;
    }

    private bool IsTargetValid(Transform target, int facingSign, Vector3 origin, out Vector3 worldDirection, out float distanceSqr)
    {
        worldDirection = Vector3.zero;
        distanceSqr = float.PositiveInfinity;

        if (target == null)
            return false;

        Vector3 toTarget = target.position - origin;
        toTarget.z = 0f;
        distanceSqr = toTarget.sqrMagnitude;

        if (distanceSqr <= 0.0001f)
            return false;

        float maxDistanceSqr = viewDistance * viewDistance;
        if (maxDistanceSqr > 0f && distanceSqr > maxDistanceSqr)
            return false;

        Vector2 facing = facingSign >= 0 ? Vector2.right : Vector2.left;
        Vector2 planarDirection = new Vector2(toTarget.x, toTarget.y).normalized;
        float angle = Vector2.Angle(facing, planarDirection);
        if (angle > fovHalfAngle)
            return false;

        if (requireLineOfSight)
        {
            Vector3 targetPoint = target.position;
            if (Physics.Linecast(origin, targetPoint, occlusionMask, queryTriggerInteraction))
                return false;
        }

        worldDirection = toTarget.normalized;
        return true;
    }

    private void LogTargetTransition(Transform nextTarget)
    {
        if (!debugLogTransitions)
            return;

        if (nextTarget == debugLastLoggedTarget)
            return;

        if (nextTarget != null && debugLastLoggedTarget == null)
        {
            Debug.Log($"[HeadTargetTracking25D] Saw target: {nextTarget.name}", nextTarget);
        }
        else if (nextTarget != null && debugLastLoggedTarget != null)
        {
            Debug.Log($"[HeadTargetTracking25D] Switched target: {debugLastLoggedTarget.name} -> {nextTarget.name}", nextTarget);
        }
        else if (nextTarget == null && debugLastLoggedTarget != null)
        {
            Debug.Log($"[HeadTargetTracking25D] Lost target: {debugLastLoggedTarget.name}", debugLastLoggedTarget);
        }

        debugLastLoggedTarget = nextTarget;
    }
}
