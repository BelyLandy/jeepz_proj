using System;
using System.Text;
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

    [Header("Deferred Last Known")]
    [SerializeField] private bool deferLastKnownUntilTargetLands = true;
    [SerializeField, Min(0f)] private float airborneLastKnownResolveDelay = 0.25f;
    [SerializeField] private bool projectUnresolvedAirborneLastKnownToGround = true;
    [SerializeField] private bool setImmediateProjectedWorkingLastKnownForAirborneLoss = true;
    [SerializeField, Min(0f)] private float lastKnownGroundProjectUpOffset = 0.5f;
    [SerializeField, Min(0.01f)] private float lastKnownGroundProjectDownDistance = 6f;
    [SerializeField] private LayerMask lastKnownGroundMask = ~0;

    [Header("Last Known Grounded Target Position")]
    [SerializeField] private bool useGroundPointForGroundedLastKnown = true;

    [Header("Combat Navigation Target")]
    [SerializeField] private bool useGroundPointForVisibleCombatNavigation = true;
    [SerializeField] private bool projectVisibleAirborneCombatNavigationToGround = true;

    [Header("Last Known Facing Hint")]
    [SerializeField] private bool useLastKnownFacingHint = true;
    [SerializeField] private bool preferMovementDirectionForFacingHint = true;
    [SerializeField, Min(0f)] private float facingHintMovementVelocityThreshold = 0.1f;
    [SerializeField] private bool searchFacingHintInTargetChildren = true;
    [SerializeField] private bool searchFacingHintInTargetParents = true;
    [SerializeField] private bool logLastKnownFacingHint = true;
    [SerializeField] private bool writeLastKnownFacingHintLogsToFile = true;
    [SerializeField] private bool drawLastKnownFacingHintGizmo = true;
    [SerializeField, Min(0f)] private float lastKnownFacingHintGizmoLength = 1.25f;
    [SerializeField] private Color lastKnownFacingHintGizmoColor = new Color(1f, 0.65f, 0.1f, 0.95f);

    [Header("Target Grounded Detection")]
    [SerializeField] private bool useTargetGroundedFlag = true;
    [SerializeField] private bool searchGroundedFlagInTargetChildren = true;
    [SerializeField] private bool searchGroundedFlagInTargetParents = true;
    [SerializeField]
    private string[] targetGroundedBoolMemberNames =
    {
        "IsGroundedNow",
        "IsGrounded",
        "isGrounded",
        "Grounded",
        "grounded",
        "IsOnGround",
        "isOnGround",
        "OnGround",
        "onGround"
    };
    [SerializeField] private bool useCharacterControllerGroundedFallback = true;
    [SerializeField] private bool useShortGroundProbeFallback = true;
    [SerializeField, Min(0f)] private float targetGroundedProbeUpOffset = 0.05f;
    [SerializeField, Min(0.01f)] private float targetGroundedProbeDistance = 0.18f;
    [SerializeField, Min(0f)] private float targetGroundedProbeRadius = 0.08f;
    [SerializeField] private LayerMask targetGroundedProbeMask = 0;
    [SerializeField] private bool logTargetGroundedDetection = false;
    [SerializeField, Min(0f)] private float targetGroundedDetectionLogCooldown = 0.25f;

    [Header("Last Known Console Debug")]
    [SerializeField] private bool logLastKnownUpdates = false;
    [SerializeField] private bool logProjectedLastKnownUpdates = true;
    [SerializeField] private bool logPendingAirborneLastKnown = false;
    [SerializeField] private bool writeLastKnownLogsToFile = true;
    [SerializeField, Min(0f)] private float lastKnownLogCooldown = 0.1f;

    [Header("Eye Tracking")]
    [SerializeField, Min(0f)] private float eyeTurnSpeedDegreesPerSecond = 240f;
    [SerializeField] private float idleEyeAngleWhenFacingRight = 0f;
    [SerializeField] private float idleEyeAngleWhenFacingLeft = 180f;
    [SerializeField] private float eyeForwardAngleOffsetDegrees = 0f;

    [Header("Calm / Alert Look")]
    [SerializeField] private bool useCalmHorizontalOnlyFacing = true;
    [SerializeField] private bool useAlertFacingSweep = true;
    [SerializeField, Min(0f)] private float alertStateDuration = 2f;
    [SerializeField, Min(1f)] private float alertMoveSpeedMultiplierHint = 1.25f;
    [SerializeField, Min(0f)] private float calmFacingReturnSpeed = 1080f;

    [Header("Rear Awareness")]
    [SerializeField] private bool enableRearAwareness = true;
    [SerializeField, Min(0f)] private float rearAwarenessRange = 1.5f;
    [SerializeField, Min(0f)] private float rearAwarenessHorizontalRange = 1.75f;
    [SerializeField, Min(0f)] private float rearAwarenessVerticalTolerance = 2.25f;
    [SerializeField, Range(0f, 89f)] private float rearAwarenessHalfAngle = 40f;
    [SerializeField] private bool rearAwarenessRequiresNoObstacle = true;
    [SerializeField] private bool rearAwarenessTriggersAlertOnly = true;
    [SerializeField, Min(0f)] private float rearAwarenessCooldown = 0.5f;

    [Header("Body Turn From Look")]
    [SerializeField] private bool enableBodyTurnFromEyeTracking = true;
    [SerializeField, Range(0f, 179f)] private float bodyTurnThresholdDegrees = 100f;
    [SerializeField, Min(0f)] private float bodyTurnHysteresisDegrees = 10f;
    [SerializeField, Min(0f)] private float bodyTurnCooldown = 0.15f;

    [Header("Gizmos")]
    [SerializeField] private bool drawPerceptionGizmos = true;
    [SerializeField] private bool drawLastKnownPositionGizmo = true;
    [SerializeField] private bool drawLastVisibleSnapshotGizmo = true;
    [SerializeField] private Color lastKnownPositionGizmoColor = Color.cyan;
    [SerializeField] private Color lastVisibleSnapshotGizmoColor = new Color(0.15f, 1f, 1f, 0.9f);
    [SerializeField, Min(0.01f)] private float lastKnownPositionWireRadius = 0.3f;
    [SerializeField, Min(0.01f)] private float lastVisibleSnapshotSphereRadius = 0.08f;
    [SerializeField] private bool drawPendingAirborneResolveGizmo = true;
    [SerializeField] private Color pendingAirborneResolveGizmoColor = new Color(1f, 0.3f, 1f, 0.9f);
    [SerializeField, Min(0.01f)] private float pendingAirborneResolveSphereRadius = 0.07f;
    [SerializeField] private bool drawProjectedLastKnownGroundGizmo = true;
    [SerializeField] private bool drawProjectedLastKnownGroundRay = true;
    [SerializeField] private Color projectedLastKnownGroundGizmoColor = new Color(0.2f, 1f, 0.25f, 0.95f);
    [SerializeField] private Color projectedLastKnownGroundSourceGizmoColor = new Color(1f, 0.85f, 0.15f, 0.95f);
    [SerializeField, Min(0.01f)] private float projectedLastKnownGroundSphereRadius = 0.35f;
    [SerializeField, Min(0.01f)] private float projectedLastKnownGroundWireRadius = 0.5f;
    [SerializeField, Min(0.01f)] private float projectedLastKnownGroundSourceSphereRadius = 0.12f;
    [SerializeField] private bool drawRearAwarenessGizmos = true;
    [SerializeField] private bool drawRearAwarenessOnlyWhenSelected = true;
    [SerializeField] private Color rearAwarenessGizmoColor = new Color(1f, 0.6f, 0.1f, 0.8f);
    [SerializeField] private Color rearAwarenessTriggeredGizmoColor = new Color(1f, 0.2f, 0.1f, 0.95f);

    private Transform currentTarget;
    private bool targetVisible;
    private bool hasLineOfSight;
    private bool hasLastKnownPosition;
    private bool hasTrackedTarget;
    private Vector3 lastKnownTargetPosition;
    private int lastKnownPositionVersion;
    private string lastKnownUpdateReason = "None";
    private bool hasLastKnownFacingHint;
    private int lastKnownFacingSign = 1;
    private string lastKnownFacingSource = "None";
    private string lastKnownFacingMode = "None";
    private Vector3 targetVelocityEstimate;
    private Vector3 previousTargetPosition;
    private bool hadPreviousSample;
    private float lastSeenTime = float.NegativeInfinity;
    private float timeOutsideCone;
    private float nextBodyTurnAllowedTime = float.NegativeInfinity;

    private bool isAlert;
    private float alertEndTime;
    private bool rearAwarenessTriggeredThisFrame;
    private float nextRearAwarenessAllowedTime = float.NegativeInfinity;
    private bool hadVisibleTargetLastFrame;
    private bool hasRecentlyLostTarget;
    private float lastTargetLostTime = float.NegativeInfinity;
    private bool isTargetInRearAwarenessNow;
    private int lastKnownTargetSideSign = 1;
    private Vector3 lastVisibleTargetPosition;
    private Vector3 lastVisibleTargetAimPosition;
    private Vector3 lastVisibleTargetGroundedAwarePosition;
    private bool hasLastVisibleTargetPosition;
    private bool hasLastVisibleTargetGroundedState;
    private bool lastVisibleTargetWasGrounded;
    private bool lastVisibleTargetGroundedFlagFound;
    private bool lastVisibleTargetGroundedStateFound;
    private string lastVisibleTargetGroundedSource = "None";
    private bool hasLastVisibleTargetFacingHint;
    private int lastVisibleTargetFacingSign = 1;
    private string lastVisibleTargetFacingSource = "None";
    private string lastVisibleTargetFacingMode = "None";
    private Component cachedGroundedComponent;
    private System.Reflection.PropertyInfo cachedGroundedProperty;
    private System.Reflection.FieldInfo cachedGroundedField;
    private Transform cachedGroundedReaderRoot;
    private string cachedGroundedSource;
    private float lastTargetGroundedDetectionLogTime = float.NegativeInfinity;
    private int lastTargetGroundedDetectionLogFrame = -1;
    private bool justLostVisibleTargetThisFrame;
    private bool justReacquiredVisibleTargetThisFrame;
    private bool pendingAirborneLastKnownResolve;
    private Vector3 pendingAirborneLostSnapshotPosition;
    private bool hasPendingAirborneImmediateProjection;
    private Vector3 pendingAirborneImmediateProjectedPosition;
    private bool hasPendingAirborneFacingHint;
    private int pendingAirborneFacingSign = 1;
    private string pendingAirborneFacingSource = "None";
    private string pendingAirborneFacingMode = "None";
    private float pendingAirborneResolveDeadline = float.NegativeInfinity;
    private float pendingAirborneResolveStartedTime = float.NegativeInfinity;
    private bool lastKnownResolvedFromLanding;
    private bool lastKnownResolvedFromGroundProjection;
    private bool hasLastKnownGroundProjectionDebug;
    private Vector3 lastKnownGroundProjectionSourcePosition;
    private Vector3 lastKnownGroundProjectionHitPosition;
    private float lastKnownDebugLogTime = float.NegativeInfinity;
    private int lastKnownDebugLogFrame = -1;

    public Transform CurrentTarget => currentTarget;
    public bool HasTarget => currentTarget != null;
    public bool IsTargetVisible => targetVisible;
    public bool HasLineOfSight => hasLineOfSight;
    public bool HasLastKnownPosition => hasLastKnownPosition;
    public bool HasTrackedTarget => hasTrackedTarget;
    public Vector3 LastKnownTargetPosition => lastKnownTargetPosition;
    public int LastKnownPositionVersion => lastKnownPositionVersion;
    public string LastKnownUpdateReason => lastKnownUpdateReason;
    public bool HasLastKnownFacingHint => hasLastKnownFacingHint;
    public int LastKnownFacingSign => lastKnownFacingSign;
    public string LastKnownFacingSource => lastKnownFacingSource;
    public string LastKnownFacingMode => lastKnownFacingMode;
    public bool IsLastKnownUpdateSearchRelevant => IsSearchRelevantLastKnownReason(lastKnownUpdateReason);
    public Vector3 TargetVelocityEstimate => targetVelocityEstimate;
    public LayerMask ObstructionMask => obstructionMask;
    public float TimeSinceLastSeen => targetVisible ? 0f : (Time.time - lastSeenTime);
    public Vector3 EyePosition => eyeOrigin != null ? eyeOrigin.position : transform.position;
    public Vector3 CurrentTargetAimPosition => GetTargetAimPosition(currentTarget);
    public float CurrentEyeAngleDegrees => GetCurrentEyeAngle();
    public Vector3 EyeForwardDirection => GetEyeForwardDirection(GetCurrentEyeAngle());
    public bool IsAlert => isAlert;
    public float AlertRemaining => Mathf.Max(0f, alertEndTime - Time.time);
    public bool RearAwarenessTriggeredThisFrame => rearAwarenessTriggeredThisFrame;
    public bool HasRecentlyLostTarget => hasRecentlyLostTarget;
    public float AlertMoveSpeedMultiplierHint => alertMoveSpeedMultiplierHint;
    public bool UseCalmHorizontalOnlyFacing => useCalmHorizontalOnlyFacing;
    public bool UseAlertFacingSweep => useAlertFacingSweep;
    public bool IsTargetInRearAwarenessNow => isTargetInRearAwarenessNow;
    public int LastKnownTargetSideSign => lastKnownTargetSideSign;
    public bool JustLostVisibleTargetThisFrame => justLostVisibleTargetThisFrame;
    public bool JustReacquiredVisibleTargetThisFrame => justReacquiredVisibleTargetThisFrame;
    public bool HasLastVisibleTargetPosition => hasLastVisibleTargetPosition;
    public Vector3 LastVisibleTargetPosition => lastVisibleTargetPosition;
    public bool IsAirborneLastKnownResolvePending => pendingAirborneLastKnownResolve;
    public Vector3 PendingAirborneLostSnapshotPosition => pendingAirborneLostSnapshotPosition;
    public bool LastKnownResolvedFromLanding => lastKnownResolvedFromLanding;
    public bool LastKnownResolvedFromGroundProjection => lastKnownResolvedFromGroundProjection;

    public float DetectionRadiusForVisionCone => detectionRadius;
    public float FieldOfViewDegreesForVisionCone => fieldOfView;
    public Transform EyeOriginTransform => eyeOrigin;
    public int CurrentFacingSign => character != null ? character.FacingSign : (transform.right.x >= 0f ? 1 : -1);
    public Vector3 CurrentVisionForward => EyeForwardDirection;

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
        rearAwarenessTriggeredThisFrame = false;
        isTargetInRearAwarenessNow = false;
        justLostVisibleTargetThisFrame = false;
        justReacquiredVisibleTargetThisFrame = false;

        UpdateVelocityEstimate();
        EvaluateVisibility();
        UpdateAlertStateFromVisibility();
        TickPendingAirborneLastKnownResolve();
        CheckRearAwareness();
        TickAlertState();
        UpdateEyeTracking(Time.deltaTime);
        UpdateBodyTurnFromEyeTracking();
        hadVisibleTargetLastFrame = targetVisible;
    }

    public Vector3 GetAimPosition()
    {
        if (targetVisible && currentTarget != null)
            return GetTargetAimPosition(currentTarget);

        return hasLastKnownPosition ? lastKnownTargetPosition : transform.position;
    }

    public Vector3 GetCombatNavigationPosition()
    {
        if (targetVisible && currentTarget != null)
            return GetVisibleTargetCombatNavigationPosition(currentTarget);

        return hasLastKnownPosition ? lastKnownTargetPosition : transform.position;
    }

    private Vector3 GetVisibleTargetCombatNavigationPosition(Transform target)
    {
        Vector3 aimPosition = target != null ? GetTargetAimPosition(target) : transform.position;

        if (!useGroundPointForVisibleCombatNavigation || target == null)
            return aimPosition;

        if (TryGetTargetGroundedState(target, out bool grounded, out _) && grounded)
            return GetTargetGroundedPosition(target);

        if (projectVisibleAirborneCombatNavigationToGround && TryProjectPointToGround(aimPosition, out Vector3 projectedGround))
            return projectedGround;

        return aimPosition;
    }

    public void ClearLastKnownPosition()
    {
        if (hasLastKnownPosition)
            lastKnownPositionVersion++;
        hasLastKnownPosition = false;
        lastKnownUpdateReason = "None";
        ClearLastKnownFacingHint();
        hasTrackedTarget = false;
        targetVisible = false;
        hasLastVisibleTargetGroundedState = false;
        lastVisibleTargetWasGrounded = false;
        lastVisibleTargetGroundedFlagFound = false;
        lastVisibleTargetGroundedStateFound = false;
        lastVisibleTargetGroundedSource = "None";
        lastVisibleTargetAimPosition = Vector3.zero;
        lastVisibleTargetGroundedAwarePosition = Vector3.zero;
        hasLastVisibleTargetFacingHint = false;
        lastVisibleTargetFacingSign = 1;
        lastVisibleTargetFacingSource = "None";
        lastVisibleTargetFacingMode = "None";
        timeOutsideCone = 0f;
        lastSeenTime = float.NegativeInfinity;
        pendingAirborneLastKnownResolve = false;
        hasPendingAirborneImmediateProjection = false;
        pendingAirborneImmediateProjectedPosition = Vector3.zero;
        ClearPendingAirborneFacingHint();
        pendingAirborneResolveDeadline = float.NegativeInfinity;
        pendingAirborneResolveStartedTime = float.NegativeInfinity;
        lastKnownResolvedFromLanding = false;
        lastKnownResolvedFromGroundProjection = false;
        ClearLastKnownGroundProjectionDebug();
    }

    public void BeginAlertState()
    {
        isAlert = true;
        alertEndTime = Mathf.Max(alertEndTime, Time.time + alertStateDuration);
    }

    public void ClearAlertState()
    {
        isAlert = false;
        hasRecentlyLostTarget = false;
        alertEndTime = 0f;
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

        HeroHurtbox25D heroHurtbox = UnityEngine.Object.FindAnyObjectByType<HeroHurtbox25D>();
        if (heroHurtbox != null)
        {
            currentTarget = heroHurtbox.transform.root;
            return;
        }

        HeroHealth25D heroHealth = UnityEngine.Object.FindAnyObjectByType<HeroHealth25D>();
        if (heroHealth != null)
        {
            currentTarget = heroHealth.transform.root;
            return;
        }

        RBCharacter25D heroCharacter = UnityEngine.Object.FindAnyObjectByType<RBCharacter25D>();
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
        float timeSinceLastSeen = Time.time - lastSeenTime;

        if (distance <= 0.0001f)
        {
            targetVisible = true;
            hasTrackedTarget = true;
            hasLineOfSight = true;
            timeOutsideCone = 0f;
            lastSeenTime = Time.time;
            RecordVisibleTargetSnapshot(targetPos);
            UpdateLastKnownTargetMemory(lastVisibleTargetGroundedAwarePosition, true, "VisibleTarget", hasLastVisibleTargetFacingHint, lastVisibleTargetFacingSign, lastVisibleTargetFacingSource, lastVisibleTargetFacingMode);
            return;
        }

        if (distance > detectionRadius)
        {
            targetVisible = false;
            timeOutsideCone = 0f;
            hasTrackedTarget = timeSinceLastSeen <= memoryDuration && hasLastKnownPosition;
            if (!hasTrackedTarget && timeSinceLastSeen > memoryDuration)
                hasLastKnownPosition = false;
            return;
        }

        Vector3 eyeForward = EyeForwardDirection;
        bool inCone = IsWithinEyeCone(eyeForward, toTarget);
        hasLineOfSight = ComputeLineOfSight(eyePos, currentTarget, toTarget, distance);
        bool canAcquireNow = inCone && (!requireLineOfSightToAcquire || hasLineOfSight);
        bool canMaintainVisibility = inCone && hasLineOfSight;

        if (!hasTrackedTarget)
        {
            if (canAcquireNow)
            {
                hasTrackedTarget = true;
                targetVisible = true;
                timeOutsideCone = 0f;
                lastSeenTime = Time.time;
                RecordVisibleTargetSnapshot(targetPos);
                UpdateLastKnownTargetMemory(lastVisibleTargetGroundedAwarePosition, true, "VisibleTarget", hasLastVisibleTargetFacingHint, lastVisibleTargetFacingSign, lastVisibleTargetFacingSource, lastVisibleTargetFacingMode);
            }
            else if (timeSinceLastSeen > memoryDuration)
            {
                hasLastKnownPosition = false;
            }

            return;
        }

        if (canMaintainVisibility)
        {
            targetVisible = true;
            hasTrackedTarget = true;
            timeOutsideCone = 0f;
            lastSeenTime = Time.time;
            RecordVisibleTargetSnapshot(targetPos);
            UpdateLastKnownTargetMemory(lastVisibleTargetGroundedAwarePosition, true, "VisibleTarget", hasLastVisibleTargetFacingHint, lastVisibleTargetFacingSign, lastVisibleTargetFacingSource, lastVisibleTargetFacingMode);
            return;
        }

        timeOutsideCone += Time.deltaTime;
        targetVisible = false;
        hasTrackedTarget = hasLastKnownPosition && (timeOutsideCone < loseTargetGraceTime || timeSinceLastSeen <= memoryDuration);
        if (!hasTrackedTarget && timeSinceLastSeen > memoryDuration)
            hasLastKnownPosition = false;
    }

    private void UpdateAlertStateFromVisibility()
    {
        if (targetVisible && pendingAirborneLastKnownResolve)
        {
            pendingAirborneLastKnownResolve = false;
            pendingAirborneResolveDeadline = float.NegativeInfinity;
            pendingAirborneResolveStartedTime = float.NegativeInfinity;
            lastKnownResolvedFromLanding = false;
            lastKnownResolvedFromGroundProjection = false;
            ClearLastKnownGroundProjectionDebug();
        }

        if (targetVisible)
        {
            if (!hadVisibleTargetLastFrame)
                justReacquiredVisibleTargetThisFrame = true;

            isAlert = false;
            hasRecentlyLostTarget = false;
            alertEndTime = 0f;
            return;
        }

        if (hadVisibleTargetLastFrame && !targetVisible)
        {
            justLostVisibleTargetThisFrame = true;
            hasRecentlyLostTarget = true;
            lastTargetLostTime = Time.time;

            if (hasLastVisibleTargetPosition)
            {
                bool willBeginPendingAirborneResolve = false;
                string fallbackReason = "None";

                if (!deferLastKnownUntilTargetLands)
                {
                    fallbackReason = "DeferLastKnownUntilTargetLandsDisabled";
                }
                else if (!hasLastVisibleTargetGroundedState)
                {
                    willBeginPendingAirborneResolve = true;
                    fallbackReason = "NoLastVisibleGroundedStateTreatAsAirborne";
                }
                else if (!lastVisibleTargetWasGrounded)
                {
                    willBeginPendingAirborneResolve = true;
                }
                else
                {
                    fallbackReason = "TargetConsideredGrounded";
                }

                LogLostSightLastKnownDecision(lastVisibleTargetAimPosition, willBeginPendingAirborneResolve, fallbackReason);

                if (willBeginPendingAirborneResolve)
                {
                    BeginPendingAirborneLastKnownResolve(lastVisibleTargetAimPosition);
                }
                else
                {
                    UpdateLastKnownTargetMemory(lastVisibleTargetGroundedAwarePosition, false, "LostSightLastVisibleSnapshot", hasLastVisibleTargetFacingHint, lastVisibleTargetFacingSign, lastVisibleTargetFacingSource, lastVisibleTargetFacingMode);
                }
            }

            BeginAlertState();
        }
    }

    private void RecordVisibleTargetSnapshot(Vector3 targetPosition)
    {
        lastVisibleTargetPosition = targetPosition;
        lastVisibleTargetAimPosition = targetPosition;
        hasLastVisibleTargetPosition = true;

        lastVisibleTargetGroundedStateFound = TryGetTargetGroundedState(currentTarget, out bool grounded, out string groundedSource);
        lastVisibleTargetGroundedFlagFound = IsGroundedSourceFlag(groundedSource);
        lastVisibleTargetWasGrounded = lastVisibleTargetGroundedStateFound && grounded;
        lastVisibleTargetGroundedSource = groundedSource;
        hasLastVisibleTargetGroundedState = lastVisibleTargetGroundedStateFound;
        lastVisibleTargetGroundedAwarePosition = GetLastKnownPositionForVisibleTarget(
            currentTarget,
            targetPosition,
            lastVisibleTargetGroundedStateFound,
            lastVisibleTargetWasGrounded);

        CaptureLastVisibleTargetFacingHint(currentTarget, "VisibleTarget");

        LogTargetGroundedDetectionDebug(targetPosition, lastVisibleTargetGroundedStateFound, grounded, groundedSource);
    }

    private Vector3 GetLastKnownPositionForVisibleTarget(Transform target, Vector3 aimPosition, bool groundedStateFound, bool grounded)
    {
        if (!useGroundPointForGroundedLastKnown)
            return aimPosition;

        if (target != null && groundedStateFound && grounded)
            return GetTargetGroundedPosition(target);

        return aimPosition;
    }

    private void BeginPendingAirborneLastKnownResolve(Vector3 snapshotPosition)
    {
        pendingAirborneLastKnownResolve = true;
        pendingAirborneLostSnapshotPosition = snapshotPosition;
        pendingAirborneResolveStartedTime = Time.time;
        pendingAirborneResolveDeadline = Time.time + airborneLastKnownResolveDelay;
        hasPendingAirborneImmediateProjection = false;
        pendingAirborneImmediateProjectedPosition = Vector3.zero;
        lastKnownResolvedFromLanding = false;
        lastKnownResolvedFromGroundProjection = false;
        CapturePendingAirborneFacingHintFromLastVisible();
        ClearLastKnownGroundProjectionDebug();

        if (logPendingAirborneLastKnown)
        {
            LogPendingAirborneLastKnownDebug(snapshotPosition);
        }

        if (setImmediateProjectedWorkingLastKnownForAirborneLoss &&
            projectUnresolvedAirborneLastKnownToGround &&
            TryProjectPointToGround(snapshotPosition, out Vector3 projectedFallback))
        {
            hasPendingAirborneImmediateProjection = true;
            pendingAirborneImmediateProjectedPosition = projectedFallback;
            UpdateLastKnownTargetMemory(projectedFallback, false, "AirborneProjectedWorkingFallback", hasPendingAirborneFacingHint, pendingAirborneFacingSign, pendingAirborneFacingSource, pendingAirborneFacingMode);
            lastKnownResolvedFromLanding = false;
            lastKnownResolvedFromGroundProjection = true;
            MarkLastKnownGroundProjectionDebug(snapshotPosition, projectedFallback);
            LogImmediateProjectedWorkingLastKnownDebug(snapshotPosition, projectedFallback, true);
        }
        else if (!hasLastKnownPosition)
        {
            UpdateLastKnownTargetMemory(snapshotPosition, false, "AirborneImmediateProjectionFailedUsingSnapshot", hasPendingAirborneFacingHint, pendingAirborneFacingSign, pendingAirborneFacingSource, pendingAirborneFacingMode);
            lastKnownResolvedFromLanding = false;
            lastKnownResolvedFromGroundProjection = false;
            ClearLastKnownGroundProjectionDebug();
            LogImmediateProjectedWorkingLastKnownDebug(snapshotPosition, snapshotPosition, false);
        }
    }

    private void TickPendingAirborneLastKnownResolve()
    {
        if (!pendingAirborneLastKnownResolve)
            return;

        if (currentTarget != null && IsTargetGrounded(currentTarget))
        {
            Vector3 groundedPosition = GetTargetGroundedPosition(currentTarget);
            bool hasLandingFacingHint = TryBuildTargetFacingHint(currentTarget, out int landingFacingSign, out string landingFacingMode, out string landingFacingSource);
            if (hasLandingFacingHint)
                landingFacingSource = "LandingResolve:" + landingFacingSource;
            UpdateLastKnownTargetMemory(
                groundedPosition,
                false,
                "AirborneResolvedByTargetLanding",
                hasLandingFacingHint || hasPendingAirborneFacingHint,
                hasLandingFacingHint ? landingFacingSign : pendingAirborneFacingSign,
                hasLandingFacingHint ? landingFacingSource : pendingAirborneFacingSource,
                hasLandingFacingHint ? landingFacingMode : pendingAirborneFacingMode);
            MarkLastKnownGroundProjectionDebug(pendingAirborneLostSnapshotPosition, groundedPosition);
            pendingAirborneLastKnownResolve = false;
            hasPendingAirborneImmediateProjection = false;
            pendingAirborneImmediateProjectedPosition = Vector3.zero;
            ClearPendingAirborneFacingHint();
            pendingAirborneResolveDeadline = float.NegativeInfinity;
            pendingAirborneResolveStartedTime = float.NegativeInfinity;
            lastKnownResolvedFromLanding = true;
            lastKnownResolvedFromGroundProjection = false;
            return;
        }

        if (Time.time < pendingAirborneResolveDeadline)
            return;

        Vector3 resolvedPoint = pendingAirborneLostSnapshotPosition;
        bool resolvedFromGroundProjection = false;
        string resolveReason = "AirborneResolveTimeoutFallback";
        string projectionLogReason = "AirborneResolveTimeout";

        if (hasPendingAirborneImmediateProjection)
        {
            resolvedPoint = pendingAirborneImmediateProjectedPosition;
            resolvedFromGroundProjection = true;
            resolveReason = "AirborneProjectionConfirmedByTimeout";
            projectionLogReason = "AirborneProjectionConfirmedByTimeout";
        }
        else if (projectUnresolvedAirborneLastKnownToGround && TryProjectPointToGround(pendingAirborneLostSnapshotPosition, out Vector3 groundedPoint))
        {
            resolvedPoint = groundedPoint;
            resolvedFromGroundProjection = true;
            resolveReason = "AirborneProjectedToGround";
            projectionLogReason = "AirborneResolveTimeout";
        }
        else if (hasLastVisibleTargetPosition)
        {
            resolvedPoint = lastVisibleTargetPosition;
        }

        UpdateLastKnownTargetMemory(
            resolvedPoint,
            false,
            resolveReason,
            hasPendingAirborneFacingHint || hasLastKnownFacingHint,
            hasPendingAirborneFacingHint ? pendingAirborneFacingSign : lastKnownFacingSign,
            hasPendingAirborneFacingHint ? pendingAirborneFacingSource : lastKnownFacingSource,
            hasPendingAirborneFacingHint ? pendingAirborneFacingMode : lastKnownFacingMode);
        if (resolvedFromGroundProjection)
            MarkLastKnownGroundProjectionDebug(pendingAirborneLostSnapshotPosition, resolvedPoint);
        else
            ClearLastKnownGroundProjectionDebug();

        pendingAirborneLastKnownResolve = false;
        hasPendingAirborneImmediateProjection = false;
        pendingAirborneImmediateProjectedPosition = Vector3.zero;
        ClearPendingAirborneFacingHint();
        pendingAirborneResolveDeadline = float.NegativeInfinity;
        pendingAirborneResolveStartedTime = float.NegativeInfinity;
        lastKnownResolvedFromLanding = false;
        lastKnownResolvedFromGroundProjection = resolvedFromGroundProjection;

        if (resolvedFromGroundProjection && logProjectedLastKnownUpdates)
        {
            LogProjectedLastKnownDebug(pendingAirborneLostSnapshotPosition, resolvedPoint, projectionLogReason);
        }
    }

    private bool IsTargetGrounded(Transform target)
    {
        return TryGetTargetGroundedState(target, out bool grounded, out _) && grounded;
    }

    private bool TryGetTargetGroundedState(Transform target, out bool grounded, out string source)
    {
        grounded = false;
        source = "NoTarget";

        if (target == null)
            return false;

        if (useTargetGroundedFlag && TryReadGroundedFlag(target, out grounded, out source))
            return true;

        if (useCharacterControllerGroundedFallback && TryReadCharacterControllerGrounded(target, out grounded, out source))
            return true;

        if (useShortGroundProbeFallback && TryShortGroundedProbe(target, out grounded, out source))
            return true;

        source = "NoGroundedSource";
        return false;
    }

    private Vector3 GetTargetGroundedPosition(Transform target)
    {
        Vector3 source = target != null ? GetTargetAimPosition(target) : pendingAirborneLostSnapshotPosition;
        if (TryProjectPointToGround(source, out Vector3 groundedPoint))
            return groundedPoint;

        return source;
    }


    private void CaptureLastVisibleTargetFacingHint(Transform target, string reason)
    {
        if (TryBuildTargetFacingHint(target, out int sign, out string mode, out string source))
        {
            hasLastVisibleTargetFacingHint = true;
            lastVisibleTargetFacingSign = NormalizeFacingSign(sign, lastVisibleTargetFacingSign);
            lastVisibleTargetFacingMode = string.IsNullOrEmpty(mode) ? "Unknown" : mode;
            lastVisibleTargetFacingSource = string.IsNullOrEmpty(source) ? reason : source;
            return;
        }

        hasLastVisibleTargetFacingHint = false;
        lastVisibleTargetFacingSign = 1;
        lastVisibleTargetFacingMode = "None";
        lastVisibleTargetFacingSource = "None";
    }

    private void CapturePendingAirborneFacingHintFromLastVisible()
    {
        hasPendingAirborneFacingHint = hasLastVisibleTargetFacingHint;
        pendingAirborneFacingSign = NormalizeFacingSign(lastVisibleTargetFacingSign, pendingAirborneFacingSign);
        pendingAirborneFacingMode = hasLastVisibleTargetFacingHint ? lastVisibleTargetFacingMode : "None";
        pendingAirborneFacingSource = hasLastVisibleTargetFacingHint ? "LostSight:" + lastVisibleTargetFacingSource : "None";
    }

    private void ClearPendingAirborneFacingHint()
    {
        hasPendingAirborneFacingHint = false;
        pendingAirborneFacingSign = 1;
        pendingAirborneFacingMode = "None";
        pendingAirborneFacingSource = "None";
    }

    private void ClearLastKnownFacingHint()
    {
        hasLastKnownFacingHint = false;
        lastKnownFacingSign = 1;
        lastKnownFacingMode = "None";
        lastKnownFacingSource = "None";
    }

    private bool TryBuildTargetFacingHint(Transform target, out int facingSign, out string mode, out string source)
    {
        facingSign = 0;
        mode = "None";
        source = "None";

        if (!useLastKnownFacingHint || target == null)
            return false;

        if (TryGetTargetStrictFacingSign(target, out facingSign, out source))
        {
            mode = "StrictFacing";
            return true;
        }

        return false;
    }

    private bool TryGetTargetMovementFacingSign(Transform target, out int sign, out string source)
    {
        sign = 0;
        source = "None";

        if (target == null)
            return false;

        if (TryReadRigidbodyVelocityX(target, out float rbVelocityX, out source) && Mathf.Abs(rbVelocityX) >= facingHintMovementVelocityThreshold)
        {
            sign = rbVelocityX >= 0f ? 1 : -1;
            return true;
        }

        if (Mathf.Abs(targetVelocityEstimate.x) >= facingHintMovementVelocityThreshold)
        {
            sign = targetVelocityEstimate.x >= 0f ? 1 : -1;
            source = "PositionDelta.x";
            return true;
        }

        return false;
    }

    private bool TryReadRigidbodyVelocityX(Transform target, out float velocityX, out string source)
    {
        velocityX = 0f;
        source = "None";

        Rigidbody rb = FindComponentForHint<Rigidbody>(target);
        if (rb != null && TryReadVectorXMember(rb, "linearVelocity", out velocityX))
        {
            source = rb.GetType().Name + ".linearVelocity.x";
            return true;
        }
        if (rb != null && TryReadVectorXMember(rb, "velocity", out velocityX))
        {
            source = rb.GetType().Name + ".velocity.x";
            return true;
        }

        Rigidbody2D rb2d = FindComponentForHint<Rigidbody2D>(target);
        if (rb2d != null && TryReadVectorXMember(rb2d, "linearVelocity", out velocityX))
        {
            source = rb2d.GetType().Name + ".linearVelocity.x";
            return true;
        }
        if (rb2d != null && TryReadVectorXMember(rb2d, "velocity", out velocityX))
        {
            source = rb2d.GetType().Name + ".velocity.x";
            return true;
        }

        return false;
    }

    private T FindComponentForHint<T>(Transform target) where T : Component
    {
        if (target == null)
            return null;

        T component = target.GetComponent<T>();
        if (component != null)
            return component;

        if (searchFacingHintInTargetParents)
        {
            component = target.GetComponentInParent<T>();
            if (component != null)
                return component;
        }

        if (searchFacingHintInTargetChildren)
        {
            component = target.GetComponentInChildren<T>();
            if (component != null)
                return component;
        }

        return null;
    }

    private bool TryReadVectorXMember(object instance, string memberName, out float x)
    {
        x = 0f;
        if (instance == null || string.IsNullOrEmpty(memberName))
            return false;

        Type type = instance.GetType();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        System.Reflection.PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null)
        {
            try
            {
                object value = property.GetValue(instance, null);
                return TryExtractX(value, out x);
            }
            catch { }
        }

        System.Reflection.FieldInfo field = type.GetField(memberName, flags);
        if (field != null)
        {
            try
            {
                object value = field.GetValue(instance);
                return TryExtractX(value, out x);
            }
            catch { }
        }

        return false;
    }

    private bool TryExtractX(object value, out float x)
    {
        x = 0f;
        if (value is Vector3 vector3)
        {
            x = vector3.x;
            return true;
        }
        if (value is Vector2 vector2)
        {
            x = vector2.x;
            return true;
        }
        if (value is float floatValue)
        {
            x = floatValue;
            return true;
        }
        if (value is int intValue)
        {
            x = intValue;
            return true;
        }
        return false;
    }

    private bool TryGetTargetStrictFacingSign(Transform target, out int sign, out string source)
    {
        sign = 0;
        source = "None";

        if (target == null)
            return false;

        if (TryReadFacingSignFromComponents(target.GetComponents<Component>(), out sign, out source))
            return true;

        if (searchFacingHintInTargetParents && TryReadFacingSignFromComponents(target.GetComponentsInParent<Component>(), out sign, out source))
            return true;

        if (searchFacingHintInTargetChildren && TryReadFacingSignFromComponents(target.GetComponentsInChildren<Component>(), out sign, out source))
            return true;

        if (Mathf.Abs(target.localScale.x) > 0.001f)
        {
            sign = target.localScale.x >= 0f ? 1 : -1;
            source = "Transform.localScale.x";
            return true;
        }

        return false;
    }

    private bool TryReadFacingSignFromComponents(Component[] components, out int sign, out string source)
    {
        sign = 0;
        source = "None";

        if (components == null)
            return false;

        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            if (TryReadBoolFacingMember(component, out bool facingRight, out string boolSource))
            {
                sign = facingRight ? 1 : -1;
                source = boolSource;
                return true;
            }

            if (TryReadSignedFacingMember(component, out sign, out string signedSource))
            {
                source = signedSource;
                return true;
            }
        }

        return false;
    }

    private bool TryReadBoolFacingMember(Component component, out bool value, out string source)
    {
        value = false;
        source = "None";
        string[] names = { "IsFacingRight", "FacingRight", "isFacingRight", "facingRight" };
        for (int i = 0; i < names.Length; i++)
        {
            if (TryReadMember(component, names[i], out object rawValue) && rawValue is bool boolValue)
            {
                value = boolValue;
                source = component.GetType().Name + "." + names[i];
                return true;
            }
        }
        return false;
    }

    private bool TryReadSignedFacingMember(Component component, out int sign, out string source)
    {
        sign = 0;
        source = "None";
        string[] names =
        {
            "ResolvedFacingSign", "FacingSign", "facingSign", "DirectionSign", "directionSign",
            "LookDirectionSign", "lookDirectionSign", "FacingDirectionX", "facingDirectionX",
            "LookDirectionX", "lookDirectionX"
        };

        for (int i = 0; i < names.Length; i++)
        {
            if (!TryReadMember(component, names[i], out object rawValue))
                continue;

            if (rawValue is int intValue && intValue != 0)
            {
                sign = intValue > 0 ? 1 : -1;
                source = component.GetType().Name + "." + names[i];
                return true;
            }
            if (rawValue is float floatValue && Mathf.Abs(floatValue) > 0.001f)
            {
                sign = floatValue > 0f ? 1 : -1;
                source = component.GetType().Name + "." + names[i];
                return true;
            }
            if (rawValue is Vector2 vector2Value && Mathf.Abs(vector2Value.x) > 0.001f)
            {
                sign = vector2Value.x > 0f ? 1 : -1;
                source = component.GetType().Name + "." + names[i] + ".x";
                return true;
            }
            if (rawValue is Vector3 vector3Value && Mathf.Abs(vector3Value.x) > 0.001f)
            {
                sign = vector3Value.x > 0f ? 1 : -1;
                source = component.GetType().Name + "." + names[i] + ".x";
                return true;
            }
        }

        return false;
    }

    private bool TryReadMember(Component component, string memberName, out object value)
    {
        value = null;
        if (component == null || string.IsNullOrEmpty(memberName))
            return false;

        Type type = component.GetType();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        System.Reflection.PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null && property.GetIndexParameters().Length == 0)
        {
            try
            {
                value = property.GetValue(component, null);
                return true;
            }
            catch { }
        }

        System.Reflection.FieldInfo field = type.GetField(memberName, flags);
        if (field != null)
        {
            try
            {
                value = field.GetValue(component);
                return true;
            }
            catch { }
        }

        return false;
    }

    private int NormalizeFacingSign(int sign, int fallback)
    {
        if (sign > 0)
            return 1;
        if (sign < 0)
            return -1;
        if (fallback != 0)
            return fallback > 0 ? 1 : -1;
        return 1;
    }


    private bool TryReadGroundedFlag(Transform target, out bool grounded, out string source)
    {
        grounded = false;
        source = "NoGroundedFlag";

        if (target == null)
            return false;

        Transform root = target.root != null ? target.root : target;
        if (cachedGroundedComponent != null && cachedGroundedReaderRoot == root)
        {
            if (TryReadCachedGroundedMember(out grounded))
            {
                source = string.IsNullOrEmpty(cachedGroundedSource) ? cachedGroundedComponent.GetType().Name : cachedGroundedSource;
                return true;
            }

            ClearCachedGroundedReader();
        }

        if (TryFindGroundedMember(target, out Component component, out System.Reflection.PropertyInfo property, out System.Reflection.FieldInfo field, out source))
        {
            cachedGroundedComponent = component;
            cachedGroundedProperty = property;
            cachedGroundedField = field;
            cachedGroundedReaderRoot = root;
            cachedGroundedSource = source;
            return TryReadCachedGroundedMember(out grounded);
        }

        source = "NoGroundedFlag";
        return false;
    }

    private bool TryReadCachedGroundedMember(out bool grounded)
    {
        grounded = false;

        if (cachedGroundedComponent == null)
            return false;

        try
        {
            if (cachedGroundedProperty != null)
            {
                grounded = (bool)cachedGroundedProperty.GetValue(cachedGroundedComponent, null);
                return true;
            }

            if (cachedGroundedField != null)
            {
                grounded = (bool)cachedGroundedField.GetValue(cachedGroundedComponent);
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private bool TryFindGroundedMember(
        Transform target,
        out Component groundedComponent,
        out System.Reflection.PropertyInfo groundedProperty,
        out System.Reflection.FieldInfo groundedField,
        out string source)
    {
        groundedComponent = null;
        groundedProperty = null;
        groundedField = null;
        source = "NoGroundedFlag";

        if (target == null)
            return false;

        if (TryFindGroundedMemberInComponents(target.GetComponents<Component>(), out groundedComponent, out groundedProperty, out groundedField, out source))
            return true;

        if (searchGroundedFlagInTargetParents &&
            TryFindGroundedMemberInComponents(target.GetComponentsInParent<Component>(true), out groundedComponent, out groundedProperty, out groundedField, out source))
        {
            return true;
        }

        if (searchGroundedFlagInTargetChildren &&
            TryFindGroundedMemberInComponents(target.GetComponentsInChildren<Component>(true), out groundedComponent, out groundedProperty, out groundedField, out source))
        {
            return true;
        }

        return false;
    }

    private bool TryFindGroundedMemberInComponents(
        Component[] components,
        out Component groundedComponent,
        out System.Reflection.PropertyInfo groundedProperty,
        out System.Reflection.FieldInfo groundedField,
        out string source)
    {
        groundedComponent = null;
        groundedProperty = null;
        groundedField = null;
        source = "NoGroundedFlag";

        if (components == null)
            return false;

        const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            System.Type type = component.GetType();
            string[] memberNames = GetTargetGroundedBoolMemberNames();

            for (int index = 0; index < memberNames.Length; index++)
            {
                string memberName = memberNames[index];
                if (string.IsNullOrEmpty(memberName))
                    continue;

                System.Reflection.PropertyInfo property = type.GetProperty(memberName, Flags);
                if (property != null && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
                {
                    groundedComponent = component;
                    groundedProperty = property;
                    groundedField = null;
                    source = $"{type.Name}.{property.Name}";
                    return true;
                }

                System.Reflection.FieldInfo field = type.GetField(memberName, Flags);
                if (field != null && field.FieldType == typeof(bool))
                {
                    groundedComponent = component;
                    groundedProperty = null;
                    groundedField = field;
                    source = $"{type.Name}.{field.Name}";
                    return true;
                }
            }
        }

        return false;
    }

    private string[] GetTargetGroundedBoolMemberNames()
    {
        string[] builtInNames =
        {
            "IsGroundedNow",
            "IsGrounded",
            "isGrounded",
            "Grounded",
            "grounded",
            "IsOnGround",
            "isOnGround",
            "OnGround",
            "onGround"
        };

        if (targetGroundedBoolMemberNames == null || targetGroundedBoolMemberNames.Length == 0)
            return builtInNames;

        // Always include built-in names first. Existing prefab instances may have
        // an older serialized array that does not contain newly supported names
        // such as RBCharacter25D.IsGroundedNow.
        string[] mergedNames = new string[builtInNames.Length + targetGroundedBoolMemberNames.Length];
        for (int i = 0; i < builtInNames.Length; i++)
            mergedNames[i] = builtInNames[i];

        for (int i = 0; i < targetGroundedBoolMemberNames.Length; i++)
            mergedNames[builtInNames.Length + i] = targetGroundedBoolMemberNames[i];

        return mergedNames;
    }

    private void ClearCachedGroundedReader()
    {
        cachedGroundedComponent = null;
        cachedGroundedProperty = null;
        cachedGroundedField = null;
        cachedGroundedReaderRoot = null;
        cachedGroundedSource = null;
    }

    private bool TryReadCharacterControllerGrounded(Transform target, out bool grounded, out string source)
    {
        grounded = false;
        source = "NoCharacterController";

        if (target == null)
            return false;

        CharacterController controller = target.GetComponent<CharacterController>();
        if (controller == null && searchGroundedFlagInTargetParents)
            controller = target.GetComponentInParent<CharacterController>();
        if (controller == null && searchGroundedFlagInTargetChildren)
            controller = target.GetComponentInChildren<CharacterController>();

        if (controller == null)
            return false;

        grounded = controller.isGrounded;
        source = "CharacterController.isGrounded";
        return true;
    }

    private bool TryShortGroundedProbe(Transform target, out bool grounded, out string source)
    {
        grounded = false;
        source = "ShortGroundProbe:miss";

        if (target == null)
            return false;

        Vector3 origin = target.position + Vector3.up * targetGroundedProbeUpOffset;
        origin.z = EyePosition.z;

        int mask = targetGroundedProbeMask.value != 0 ? targetGroundedProbeMask.value : lastKnownGroundMask.value;
        RaycastHit[] hits;
        if (targetGroundedProbeRadius > 0f)
        {
            hits = Physics.SphereCastAll(
                origin,
                targetGroundedProbeRadius,
                Vector3.down,
                targetGroundedProbeDistance,
                mask,
                QueryTriggerInteraction.Ignore);
        }
        else
        {
            hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                targetGroundedProbeDistance,
                mask,
                QueryTriggerInteraction.Ignore);
        }

        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                    continue;

                Transform hitTransform = hitCollider.transform;
                if (hitTransform == target || hitTransform.IsChildOf(target) || target.IsChildOf(hitTransform))
                    continue;

                grounded = true;
                source = $"ShortGroundProbe:hit:{hitCollider.name}";
                return true;
            }
        }

        grounded = false;
        source = "ShortGroundProbe:miss";
        return true;
    }

    private static bool IsGroundedSourceFlag(string source)
    {
        if (string.IsNullOrEmpty(source))
            return false;

        return source != "NoGroundedFlag" &&
               source != "NoGroundedSource" &&
               source != "NoTarget" &&
               source != "NoCharacterController" &&
               !source.StartsWith("ShortGroundProbe", System.StringComparison.Ordinal);
    }

    private bool TryProjectPointToGround(Vector3 sourcePoint, out Vector3 groundedPoint)
    {
        Vector3 origin = sourcePoint + Vector3.up * lastKnownGroundProjectUpOffset;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, lastKnownGroundProjectDownDistance, lastKnownGroundMask, QueryTriggerInteraction.Ignore))
        {
            groundedPoint = hit.point;
            groundedPoint.z = EyePosition.z;
            return true;
        }

        groundedPoint = sourcePoint;
        return false;
    }

    private void TickAlertState()
    {
        if (!isAlert)
            return;

        if (targetVisible)
            return;

        if (Time.time < alertEndTime)
            return;

        isAlert = false;
        hasRecentlyLostTarget = false;
        alertEndTime = 0f;
    }

    private void CheckRearAwareness()
    {
        if (!enableRearAwareness || currentTarget == null)
            return;

        Vector3 targetPos = GetTargetAimPosition(currentTarget);
        isTargetInRearAwarenessNow = IsPointInRearAwarenessSector(targetPos);
        if (!isTargetInRearAwarenessNow)
            return;

        if (Time.time < nextRearAwarenessAllowedTime)
            return;

        rearAwarenessTriggeredThisFrame = true;
        nextRearAwarenessAllowedTime = Time.time + rearAwarenessCooldown;
        BeginAlertState();
        hasRecentlyLostTarget = true;
        lastTargetLostTime = Time.time;
        UpdateLastKnownTargetMemory(targetPos, false, "RearAwareness");
        if (rearAwarenessTriggersAlertOnly)
        {
            hasTrackedTarget = false;
            timeOutsideCone = 0f;
        }
    }

    private bool IsPointInRearAwarenessSector(Vector3 targetPosition)
    {
        Vector3 origin = EyePosition;
        Vector3 toTarget = targetPosition - origin;
        toTarget.z = 0f;

        float horizontalDistance = Mathf.Abs(toTarget.x);
        float verticalDistance = Mathf.Abs(toTarget.y);
        float effectiveHorizontalRange = rearAwarenessHorizontalRange > 0f ? rearAwarenessHorizontalRange : rearAwarenessRange;
        if (horizontalDistance <= 0.0001f && verticalDistance <= Mathf.Max(0.05f, rearAwarenessVerticalTolerance))
            return true;
        if (horizontalDistance > effectiveHorizontalRange)
            return false;
        if (verticalDistance > rearAwarenessVerticalTolerance)
            return false;

        float planarDistance = toTarget.magnitude;
        if (planarDistance <= 0.0001f)
            return true;

        Vector3 rearDirection = -GetFacingDirection();
        float angle = Vector3.Angle(rearDirection, toTarget.normalized);
        if (angle > rearAwarenessHalfAngle)
            return false;

        if (rearAwarenessRequiresNoObstacle)
            return ComputeLineOfSight(origin, currentTarget, toTarget, planarDistance);

        return true;
    }


    private void UpdateLastKnownTargetMemory(Vector3 targetPos, bool refreshSeenTime, string reason = "Unspecified")
    {
        UpdateLastKnownTargetMemory(targetPos, refreshSeenTime, reason, false, 0, "None", "None");
    }

    private void UpdateLastKnownTargetMemory(
        Vector3 targetPos,
        bool refreshSeenTime,
        string reason,
        bool hasFacingHint,
        int facingSign,
        string facingSource,
        string facingMode)
    {
        hasLastKnownPosition = true;
        lastKnownTargetPosition = targetPos;
        lastKnownUpdateReason = string.IsNullOrEmpty(reason) ? "Unknown" : reason;
        lastKnownPositionVersion++;
        lastKnownTargetSideSign = ResolveSideSign(targetPos.x - EyePosition.x, lastKnownTargetSideSign);

        if (hasFacingHint)
        {
            hasLastKnownFacingHint = true;
            lastKnownFacingSign = NormalizeFacingSign(facingSign, lastKnownFacingSign);
            lastKnownFacingSource = string.IsNullOrEmpty(facingSource) ? "Unknown" : facingSource;
            lastKnownFacingMode = string.IsNullOrEmpty(facingMode) ? "Unknown" : facingMode;
        }
        else
        {
            ClearLastKnownFacingHint();
        }

        if (refreshSeenTime)
            lastSeenTime = Time.time;

        lastKnownResolvedFromLanding = false;
        lastKnownResolvedFromGroundProjection = false;
        ClearLastKnownGroundProjectionDebug();

        if (logLastKnownUpdates)
        {
            LogLastKnownUpdatedDebug(reason, targetPos, refreshSeenTime, false);
        }

        if (logLastKnownFacingHint && hasLastKnownFacingHint)
            LogLastKnownFacingHintDebug(reason, targetPos);
    }

    private void LogTargetGroundedDetectionDebug(Vector3 targetPosition, bool stateFound, bool grounded, string source)
    {
        if (!logTargetGroundedDetection)
            return;

        if (lastTargetGroundedDetectionLogFrame == Time.frameCount)
            return;

        if (targetGroundedDetectionLogCooldown > 0f && Time.time < lastTargetGroundedDetectionLogTime + targetGroundedDetectionLogCooldown)
            return;

        lastTargetGroundedDetectionLogFrame = Time.frameCount;
        lastTargetGroundedDetectionLogTime = Time.time;

        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine("[EnemyPerception25D] Target grounded detection");
        sb.AppendLine($"Enemy: {name}");
        sb.AppendLine($"TargetPosition: {FormatVector3ForLog(targetPosition)}");
        sb.AppendLine($"GroundedStateFound: {stateFound}");
        sb.AppendLine($"Grounded: {grounded}");
        sb.AppendLine($"GroundedSource: {source}");
        sb.AppendLine($"UseTargetGroundedFlag: {useTargetGroundedFlag}");
        sb.AppendLine($"UseCharacterControllerGroundedFallback: {useCharacterControllerGroundedFallback}");
        sb.AppendLine($"UseShortGroundProbeFallback: {useShortGroundProbeFallback}");

        WriteLastKnownDebugLog("PerceptionTargetGroundedDetection", sb.ToString(), true, true);
    }

    private void LogLostSightLastKnownDecision(Vector3 snapshotPosition, bool willBeginPendingAirborneResolve, string fallbackReason)
    {
        if (!logLastKnownUpdates && !logPendingAirborneLastKnown)
            return;

        StringBuilder sb = new StringBuilder(768);
        sb.AppendLine("[EnemyPerception25D] Lost sight last known decision");
        sb.AppendLine($"Enemy: {name}");
        sb.AppendLine($"LastVisiblePosition: {FormatVector3ForLog(snapshotPosition)}");
        sb.AppendLine($"LastVisibleAimPosition: {FormatVector3ForLog(lastVisibleTargetAimPosition)}");
        sb.AppendLine($"LastVisibleGroundedAwarePosition: {FormatVector3ForLog(lastVisibleTargetGroundedAwarePosition)}");
        sb.AppendLine($"HasLastVisibleGroundedState: {hasLastVisibleTargetGroundedState}");
        sb.AppendLine($"GroundedStateFound: {lastVisibleTargetGroundedStateFound}");
        sb.AppendLine($"GroundedFlagFound: {lastVisibleTargetGroundedFlagFound}");
        sb.AppendLine($"GroundedSource: {lastVisibleTargetGroundedSource}");
        sb.AppendLine($"LastVisibleTargetWasGrounded: {lastVisibleTargetWasGrounded}");
        sb.AppendLine($"HasLastVisibleFacingHint: {hasLastVisibleTargetFacingHint}");
        sb.AppendLine($"LastVisibleFacingSign: {FormatFacingSignForLog(lastVisibleTargetFacingSign)}");
        sb.AppendLine($"LastVisibleFacingMode: {lastVisibleTargetFacingMode}");
        sb.AppendLine($"LastVisibleFacingSource: {lastVisibleTargetFacingSource}");
        sb.AppendLine($"DeferLastKnownUntilTargetLands: {deferLastKnownUntilTargetLands}");
        sb.AppendLine($"WillBeginPendingAirborneResolve: {willBeginPendingAirborneResolve}");
        sb.AppendLine($"FallbackReason: {fallbackReason}");
        sb.AppendLine($"ResolveDelay: {airborneLastKnownResolveDelay:F2}");
        sb.AppendLine($"ProjectToGroundIfUnresolved: {projectUnresolvedAirborneLastKnownToGround}");

        WriteLastKnownDebugLog("PerceptionLastKnownDecision", sb.ToString(), true, true);
    }

    private void LogImmediateProjectedWorkingLastKnownDebug(Vector3 sourcePosition, Vector3 projectedPosition, bool committedAsWorkingLastKnown)
    {
        if (!logProjectedLastKnownUpdates && !logPendingAirborneLastKnown)
            return;

        StringBuilder sb = new StringBuilder(768);
        sb.AppendLine("[EnemyPerception25D] Immediate projected working last known");
        sb.AppendLine($"Enemy: {name}");
        sb.AppendLine("Reason: AirborneLostSightImmediateProjection");
        sb.AppendLine($"SourceSnapshot: {FormatVector3ForLog(sourcePosition)}");
        sb.AppendLine($"ProjectedFallback: {FormatVector3ForLog(projectedPosition)}");
        sb.AppendLine($"WorkingLastKnown: {FormatVector3ForLog(lastKnownTargetPosition)}");
        sb.AppendLine($"CommittedAsWorkingLastKnown: {committedAsWorkingLastKnown}");
        sb.AppendLine($"PendingResolveStillActive: {pendingAirborneLastKnownResolve}");
        sb.AppendLine("WillReplaceIfTargetLands: True");
        sb.AppendLine($"LastKnownVersion: {lastKnownPositionVersion}");
        sb.AppendLine($"HasFacingHint: {hasLastKnownFacingHint}");
        sb.AppendLine($"FacingSign: {FormatFacingSignForLog(lastKnownFacingSign)}");
        sb.AppendLine($"FacingMode: {lastKnownFacingMode}");
        sb.AppendLine($"FacingSource: {lastKnownFacingSource}");

        WriteLastKnownDebugLog("PerceptionImmediateProjectedLastKnown", sb.ToString(), true, true);
    }

    private void LogPendingAirborneLastKnownDebug(Vector3 snapshotPosition)
    {
        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine("[EnemyPerception25D] Pending airborne last known started");
        sb.AppendLine($"Enemy: {name}");
        sb.AppendLine($"Snapshot: {FormatVector3ForLog(snapshotPosition)}");
        sb.AppendLine($"ResolveDelay: {airborneLastKnownResolveDelay:F2}");
        sb.AppendLine($"ProjectToGroundIfUnresolved: {projectUnresolvedAirborneLastKnownToGround}");
        sb.AppendLine($"ProjectUpOffset: {lastKnownGroundProjectUpOffset:F2}");
        sb.AppendLine($"ProjectDownDistance: {lastKnownGroundProjectDownDistance:F2}");

        WriteLastKnownDebugLog("PerceptionPendingAirborneLastKnown", sb.ToString(), true, true);
    }

    private void LogProjectedLastKnownDebug(Vector3 sourcePosition, Vector3 projectedPosition, string reason)
    {
        StringBuilder sb = new StringBuilder(768);
        sb.AppendLine("[EnemyPerception25D] Projected airborne last known to ground");
        sb.AppendLine($"Enemy: {name}");
        sb.AppendLine($"Reason: {reason}");
        sb.AppendLine($"Source: {FormatVector3ForLog(sourcePosition)}");
        sb.AppendLine($"Projected: {FormatVector3ForLog(projectedPosition)}");
        sb.AppendLine($"FinalLastKnown: {FormatVector3ForLog(lastKnownTargetPosition)}");
        sb.AppendLine($"HasLastKnown: {hasLastKnownPosition}");
        sb.AppendLine($"TargetVisible: {targetVisible}");
        sb.AppendLine($"TargetGrounded: {(currentTarget != null ? IsTargetGrounded(currentTarget) : false)}");
        sb.AppendLine($"ProjectUpOffset: {lastKnownGroundProjectUpOffset:F2}");
        sb.AppendLine($"ProjectDownDistance: {lastKnownGroundProjectDownDistance:F2}");
        sb.AppendLine($"LastKnownVersion: {lastKnownPositionVersion}");
        sb.AppendLine($"HasFacingHint: {hasLastKnownFacingHint}");
        sb.AppendLine($"FacingSign: {FormatFacingSignForLog(lastKnownFacingSign)}");
        sb.AppendLine($"FacingMode: {lastKnownFacingMode}");
        sb.AppendLine($"FacingSource: {lastKnownFacingSource}");

        WriteLastKnownDebugLog("PerceptionProjectedLastKnown", sb.ToString(), true, true);
    }

    private void LogLastKnownUpdatedDebug(string reason, Vector3 position, bool refreshSeenTime, bool force)
    {
        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine("[EnemyPerception25D] Last known updated");
        sb.AppendLine($"Enemy: {name}");
        sb.AppendLine($"Reason: {reason}");
        sb.AppendLine($"Position: {FormatVector3ForLog(position)}");
        sb.AppendLine($"HasLastKnown: {hasLastKnownPosition}");
        sb.AppendLine($"TargetVisible: {targetVisible}");
        sb.AppendLine($"RefreshSeenTime: {refreshSeenTime}");
        sb.AppendLine($"TimeSinceLastSeen: {TimeSinceLastSeen:F2}");
        sb.AppendLine($"LastKnownVersion: {lastKnownPositionVersion}");
        sb.AppendLine($"HasFacingHint: {hasLastKnownFacingHint}");
        sb.AppendLine($"FacingSign: {FormatFacingSignForLog(lastKnownFacingSign)}");
        sb.AppendLine($"FacingMode: {lastKnownFacingMode}");
        sb.AppendLine($"FacingSource: {lastKnownFacingSource}");

        WriteLastKnownDebugLog("PerceptionLastKnown", sb.ToString(), true, force);
    }

    private void LogLastKnownFacingHintDebug(string reason, Vector3 position)
    {
        if (!logLastKnownFacingHint || !hasLastKnownFacingHint)
            return;

        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine("[EnemyPerception25D] Last known facing hint captured");
        sb.AppendLine($"Enemy: {name}");
        sb.AppendLine($"Reason: {reason}");
        sb.AppendLine($"Position: {FormatVector3ForLog(position)}");
        sb.AppendLine($"HasFacingHint: {hasLastKnownFacingHint}");
        sb.AppendLine($"FacingSign: {FormatFacingSignForLog(lastKnownFacingSign)}");
        sb.AppendLine($"FacingMode: {lastKnownFacingMode}");
        sb.AppendLine($"FacingSource: {lastKnownFacingSource}");

        bool previousWriteSetting = writeLastKnownLogsToFile;
        if (writeLastKnownFacingHintLogsToFile)
            writeLastKnownLogsToFile = true;
        WriteLastKnownDebugLog("PerceptionLastKnownFacing", sb.ToString(), true, true);
        writeLastKnownLogsToFile = previousWriteSetting;
    }

    private void WriteLastKnownDebugLog(string category, string message, bool allowConsole, bool force)
    {
        if (!force)
        {
            if (lastKnownDebugLogFrame == Time.frameCount)
                return;

            if (lastKnownLogCooldown > 0f && Time.time < lastKnownDebugLogTime + lastKnownLogCooldown)
                return;
        }

        lastKnownDebugLogTime = Time.time;
        lastKnownDebugLogFrame = Time.frameCount;

        if (allowConsole)
            Debug.Log(message, this);

        if (writeLastKnownLogsToFile)
            EnemyDebugFileLogger25D.Write(category, message, this);
    }

    private static bool IsSearchRelevantLastKnownReason(string reason)
    {
        switch (reason)
        {
            case "LostSightLastVisibleSnapshot":
            case "AirborneProjectedWorkingFallback":
            case "AirborneResolvedByTargetLanding":
            case "AirborneProjectionConfirmedByTimeout":
            case "AirborneProjectedToGround":
            case "AirborneProjectionFailedUsingSnapshot":
            case "AirborneImmediateProjectionFailedUsingSnapshot":
                return true;
            default:
                return false;
        }
    }

    private static string FormatFacingSignForLog(int sign)
    {
        if (sign > 0)
            return "Right";
        if (sign < 0)
            return "Left";
        return "None";
    }

    private static string FormatVector3ForLog(Vector3 value)
    {
        return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
    }

    private void MarkLastKnownGroundProjectionDebug(Vector3 sourcePosition, Vector3 hitPosition)
    {
        hasLastKnownGroundProjectionDebug = true;
        lastKnownGroundProjectionSourcePosition = sourcePosition;
        lastKnownGroundProjectionHitPosition = hitPosition;
    }

    private void ClearLastKnownGroundProjectionDebug()
    {
        hasLastKnownGroundProjectionDebug = false;
        lastKnownGroundProjectionSourcePosition = Vector3.zero;
        lastKnownGroundProjectionHitPosition = Vector3.zero;
    }

    private int ResolveSideSign(float deltaX, int fallback)
    {
        if (Mathf.Abs(deltaX) > 0.01f)
            return deltaX >= 0f ? 1 : -1;

        if (fallback != 0)
            return fallback > 0 ? 1 : -1;

        if (character != null)
            return character.FacingSign >= 0 ? 1 : -1;

        return 1;
    }

    private void UpdateEyeTracking(float deltaTime)
    {
        if (eyeOrigin == null)
            return;

        float targetAngle = GetIdleEyeAngleForFacing();
        Vector3 eyePos = EyePosition;
        bool hasLookFocus = false;

        if (targetVisible && currentTarget != null)
        {
            targetAngle = GetAngleToWorldPosition(GetTargetAimPosition(currentTarget), eyePos);
            hasLookFocus = true;
        }
        else if (hasLastKnownPosition)
        {
            targetAngle = GetAngleToWorldPosition(lastKnownTargetPosition, eyePos);
            hasLookFocus = true;
        }

        if (useCalmHorizontalOnlyFacing && !isAlert && !hasLookFocus)
        {
            float currentAngle = GetCurrentEyeAngle();
            float nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, calmFacingReturnSpeed * deltaTime);
            SetCurrentEyeAngle(nextAngle);
            return;
        }

        if (isAlert && !useAlertFacingSweep && !hasLookFocus)
        {
            SetCurrentEyeAngle(targetAngle);
            return;
        }

        float current = GetCurrentEyeAngle();
        float next = Mathf.MoveTowardsAngle(current, targetAngle, eyeTurnSpeedDegreesPerSecond * deltaTime);
        SetCurrentEyeAngle(next);
    }

    private void UpdateBodyTurnFromEyeTracking()
    {
        if (!enableBodyTurnFromEyeTracking || character == null || Time.time < nextBodyTurnAllowedTime)
            return;

        if (character.HasManualFacingOverride)
            return;

        if (!targetVisible && !hasLastKnownPosition)
            return;

        Vector3 lookPos = targetVisible && currentTarget != null ? GetTargetAimPosition(currentTarget) : lastKnownTargetPosition;
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

    private Vector3 GetFacingDirection()
    {
        if (character != null)
            return character.FacingSign >= 0 ? Vector3.right : Vector3.left;

        return transform.right.x >= 0f ? Vector3.right : Vector3.left;
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

    private void OnDrawGizmos()
    {
        if (drawRearAwarenessOnlyWhenSelected)
            return;

        DrawPerceptionAndRearAwarenessGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        DrawPerceptionAndRearAwarenessGizmos();
    }

    private void DrawPerceptionAndRearAwarenessGizmos()
    {
        if (drawPerceptionGizmos)
            DrawPerceptionGizmos();

        if (drawRearAwarenessGizmos && enableRearAwareness)
            DrawRearAwarenessGizmos();
    }

    private void DrawPerceptionGizmos()
    {
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
            Gizmos.color = lastKnownPositionGizmoColor;
            Gizmos.DrawLine(eyePos, lastKnownTargetPosition);
        }

        if (drawLastVisibleSnapshotGizmo && hasLastVisibleTargetPosition && (!targetVisible || hasLastKnownPosition || pendingAirborneLastKnownResolve))
        {
            Gizmos.color = lastVisibleSnapshotGizmoColor;
            Gizmos.DrawSphere(lastVisibleTargetPosition, lastVisibleSnapshotSphereRadius);
            Gizmos.DrawLine(eyePos, lastVisibleTargetPosition);
        }

        if (drawPendingAirborneResolveGizmo && pendingAirborneLastKnownResolve)
        {
            Gizmos.color = pendingAirborneResolveGizmoColor;
            Gizmos.DrawSphere(pendingAirborneLostSnapshotPosition, pendingAirborneResolveSphereRadius);
            Gizmos.DrawLine(eyePos, pendingAirborneLostSnapshotPosition);
        }

        if (drawProjectedLastKnownGroundGizmo && hasLastKnownPosition && (lastKnownResolvedFromGroundProjection || lastKnownResolvedFromLanding) && hasLastKnownGroundProjectionDebug)
        {
            if (drawProjectedLastKnownGroundRay)
            {
                Gizmos.color = projectedLastKnownGroundSourceGizmoColor;
                Gizmos.DrawSphere(lastKnownGroundProjectionSourcePosition, projectedLastKnownGroundSourceSphereRadius);
                Gizmos.DrawLine(lastKnownGroundProjectionSourcePosition, lastKnownGroundProjectionHitPosition);
            }

            Gizmos.color = projectedLastKnownGroundGizmoColor;
            Gizmos.DrawSphere(lastKnownTargetPosition, projectedLastKnownGroundSphereRadius);
            Gizmos.DrawWireSphere(lastKnownTargetPosition, projectedLastKnownGroundWireRadius);
            Gizmos.DrawLine(lastKnownTargetPosition + Vector3.left * projectedLastKnownGroundWireRadius, lastKnownTargetPosition + Vector3.right * projectedLastKnownGroundWireRadius);
            Gizmos.DrawLine(lastKnownTargetPosition + Vector3.up * projectedLastKnownGroundWireRadius, lastKnownTargetPosition + Vector3.down * projectedLastKnownGroundWireRadius);
        }

        if (drawLastKnownPositionGizmo && hasLastKnownPosition)
        {
            Gizmos.color = lastKnownPositionGizmoColor;
            Gizmos.DrawWireSphere(lastKnownTargetPosition, lastKnownPositionWireRadius);
            Gizmos.DrawLine(lastKnownTargetPosition + Vector3.left * 0.12f, lastKnownTargetPosition + Vector3.right * 0.12f);
            Gizmos.DrawLine(lastKnownTargetPosition + Vector3.up * 0.12f, lastKnownTargetPosition + Vector3.down * 0.12f);
        }

        if (drawLastKnownFacingHintGizmo && hasLastKnownPosition && hasLastKnownFacingHint)
        {
            Vector3 start = lastKnownTargetPosition + Vector3.up * 0.18f;
            Vector3 end = start + Vector3.right * lastKnownFacingSign * Mathf.Max(0f, lastKnownFacingHintGizmoLength);
            Gizmos.color = lastKnownFacingHintGizmoColor;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawLine(end, end + (Vector3.left * lastKnownFacingSign + Vector3.up * 0.35f) * 0.25f);
            Gizmos.DrawLine(end, end + (Vector3.left * lastKnownFacingSign + Vector3.down * 0.35f) * 0.25f);
        }
    }

    private void DrawRearAwarenessGizmos()
    {
        Vector3 origin = EyePosition;
        Vector3 rearDirection = -GetFacingDirection();
        float radius = Mathf.Max(0f, rearAwarenessHorizontalRange > 0f ? rearAwarenessHorizontalRange : rearAwarenessRange);
        Color color = isTargetInRearAwarenessNow ? rearAwarenessTriggeredGizmoColor : rearAwarenessGizmoColor;

        Gizmos.color = color;
        Gizmos.DrawSphere(origin, 0.035f);
        Gizmos.DrawLine(origin, origin + rearDirection * radius);

        Vector3 left = Quaternion.AngleAxis(-rearAwarenessHalfAngle, Vector3.forward) * rearDirection;
        Vector3 right = Quaternion.AngleAxis(rearAwarenessHalfAngle, Vector3.forward) * rearDirection;
        Gizmos.DrawLine(origin, origin + left * radius);
        Gizmos.DrawLine(origin, origin + right * radius);

        const int segments = 16;
        Vector3 prev = origin + right * radius;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(rearAwarenessHalfAngle, -rearAwarenessHalfAngle, t);
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.forward) * rearDirection;
            Vector3 next = origin + dir * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
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
        alertStateDuration = Mathf.Max(0f, alertStateDuration);
        alertMoveSpeedMultiplierHint = Mathf.Max(1f, alertMoveSpeedMultiplierHint);
        calmFacingReturnSpeed = Mathf.Max(0f, calmFacingReturnSpeed);
        rearAwarenessRange = Mathf.Max(0f, rearAwarenessRange);
        rearAwarenessHorizontalRange = Mathf.Max(0f, rearAwarenessHorizontalRange);
        rearAwarenessVerticalTolerance = Mathf.Max(0f, rearAwarenessVerticalTolerance);
        rearAwarenessHalfAngle = Mathf.Clamp(rearAwarenessHalfAngle, 0f, 89f);
        rearAwarenessCooldown = Mathf.Max(0f, rearAwarenessCooldown);
        bodyTurnThresholdDegrees = Mathf.Clamp(bodyTurnThresholdDegrees, 0f, 179f);
        bodyTurnHysteresisDegrees = Mathf.Max(0f, bodyTurnHysteresisDegrees);
        bodyTurnCooldown = Mathf.Max(0f, bodyTurnCooldown);
        airborneLastKnownResolveDelay = Mathf.Max(0f, airborneLastKnownResolveDelay);
        lastKnownLogCooldown = Mathf.Max(0f, lastKnownLogCooldown);
        targetGroundedDetectionLogCooldown = Mathf.Max(0f, targetGroundedDetectionLogCooldown);
        targetGroundedProbeUpOffset = Mathf.Max(0f, targetGroundedProbeUpOffset);
        targetGroundedProbeDistance = Mathf.Max(0.01f, targetGroundedProbeDistance);
        targetGroundedProbeRadius = Mathf.Max(0f, targetGroundedProbeRadius);
        lastKnownGroundProjectUpOffset = Mathf.Max(0f, lastKnownGroundProjectUpOffset);
        lastKnownGroundProjectDownDistance = Mathf.Max(0.01f, lastKnownGroundProjectDownDistance);
        lastKnownPositionWireRadius = Mathf.Max(0.01f, lastKnownPositionWireRadius);
        lastVisibleSnapshotSphereRadius = Mathf.Max(0.01f, lastVisibleSnapshotSphereRadius);
        pendingAirborneResolveSphereRadius = Mathf.Max(0.01f, pendingAirborneResolveSphereRadius);
        projectedLastKnownGroundSphereRadius = Mathf.Max(0.01f, projectedLastKnownGroundSphereRadius);
        projectedLastKnownGroundWireRadius = Mathf.Max(0.01f, projectedLastKnownGroundWireRadius);
        projectedLastKnownGroundSourceSphereRadius = Mathf.Max(0.01f, projectedLastKnownGroundSourceSphereRadius);
    }
}
