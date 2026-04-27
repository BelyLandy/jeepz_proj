using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyBrainBT25D : MonoBehaviour
{
    public enum BrainState
    {
        Idle = 0,
        PatrolFixed = 1,
        PatrolDynamic = 2,
        SearchDynamic = 3,
        Combat = 4,
        Disabled = 5,
        SquadPanicFlee = 6,
    }

    public enum EnemyAction25D
    {
        None = 0,
        MoveToFixedPoint = 1,
        MoveToDynamicPoint = 2,
        WaitAtPoint = 3,
        UseJumpLink = 4,
        TakeCover = 5,
        Peek = 6,
        FirePrimary = 7,
        PrepareGrenade = 8,
        ThrowGrenade = 9,
        Recover = 10,
        Stunned = 11,
        Disabled = 12,
        PostGrenadeRetreat = 13,
        BackpedalFire = 14,
        PrepareRepel = 15,
        RepelCloseTarget = 16,
        CombatJumpLinkApproach = 17,
        OpenCombatMove = 18,
        OpenCombatHold = 19,
        HoldPlatformNoJumpLink = 20,
        MoveToSquadPanicPoint = 21,
        WaitAfterSquadPanic = 22,
    }

    private enum NodeStatus
    {
        Success,
        Failure,
        Running,
    }

    private enum RuntimePointMode
    {
        None = 0,
        DynamicPatrol = 1,
        Search = 2,
    }

    private readonly struct RuntimeSearchPoint
    {
        public readonly Vector3 WorldPosition;
        public readonly float WaitDuration;
        public readonly bool RequiresJumpLink;
        public readonly EnemyJumpLink25D JumpLink;
        public readonly float Score;
        public readonly bool HasExplicitJumpTraversal;
        public readonly Vector3 JumpTraversalStart;
        public readonly Vector3 JumpTraversalEnd;
        public readonly bool IsLastKnownInvestigationPoint;
        public readonly bool HasFacingHint;
        public readonly int FacingHintSign;
        public readonly string FacingHintMode;
        public readonly string FacingHintSource;
        public readonly int LastKnownVersionSnapshot;
        public readonly Vector3 LastKnownPositionSnapshot;
        public readonly bool HasLastKnownFacingHintSnapshot;
        public readonly int LastKnownFacingSignSnapshot;
        public readonly string LastKnownFacingModeSnapshot;
        public readonly string LastKnownFacingSourceSnapshot;
        public readonly bool HasAdjustedLastKnownSafePosition;
        public readonly Vector3 OriginalLastKnownPositionSnapshot;
        public readonly Vector3 AdjustedLastKnownSafePosition;
        public readonly string LastKnownSafeAdjustmentReason;
        public readonly float LastKnownSafeAdjustmentDistance;

        public RuntimeSearchPoint(
            Vector3 worldPosition,
            float waitDuration,
            bool requiresJumpLink,
            EnemyJumpLink25D jumpLink,
            float score,
            bool isLastKnownInvestigationPoint = false,
            bool hasFacingHint = false,
            int facingHintSign = 0,
            string facingHintMode = "None",
            string facingHintSource = "None",
            int lastKnownVersionSnapshot = -1,
            Vector3 lastKnownPositionSnapshot = default,
            bool hasLastKnownFacingHintSnapshot = false,
            int lastKnownFacingSignSnapshot = 0,
            string lastKnownFacingModeSnapshot = "None",
            string lastKnownFacingSourceSnapshot = "None",
            bool hasAdjustedLastKnownSafePosition = false,
            Vector3 originalLastKnownPositionSnapshot = default,
            Vector3 adjustedLastKnownSafePosition = default,
            string lastKnownSafeAdjustmentReason = "None",
            float lastKnownSafeAdjustmentDistance = 0f)
        {
            WorldPosition = worldPosition;
            WaitDuration = Mathf.Max(0f, waitDuration);
            RequiresJumpLink = requiresJumpLink;
            JumpLink = jumpLink;
            Score = score;
            HasExplicitJumpTraversal = false;
            JumpTraversalStart = Vector3.zero;
            JumpTraversalEnd = Vector3.zero;
            IsLastKnownInvestigationPoint = isLastKnownInvestigationPoint;
            HasFacingHint = hasFacingHint;
            FacingHintSign = facingHintSign >= 0 ? 1 : -1;
            FacingHintMode = string.IsNullOrEmpty(facingHintMode) ? "None" : facingHintMode;
            FacingHintSource = string.IsNullOrEmpty(facingHintSource) ? "None" : facingHintSource;
            LastKnownVersionSnapshot = lastKnownVersionSnapshot;
            LastKnownPositionSnapshot = lastKnownPositionSnapshot;
            HasLastKnownFacingHintSnapshot = hasLastKnownFacingHintSnapshot;
            LastKnownFacingSignSnapshot = lastKnownFacingSignSnapshot >= 0 ? 1 : -1;
            LastKnownFacingModeSnapshot = string.IsNullOrEmpty(lastKnownFacingModeSnapshot) ? "None" : lastKnownFacingModeSnapshot;
            LastKnownFacingSourceSnapshot = string.IsNullOrEmpty(lastKnownFacingSourceSnapshot) ? "None" : lastKnownFacingSourceSnapshot;
            HasAdjustedLastKnownSafePosition = hasAdjustedLastKnownSafePosition;
            OriginalLastKnownPositionSnapshot = originalLastKnownPositionSnapshot;
            AdjustedLastKnownSafePosition = adjustedLastKnownSafePosition;
            LastKnownSafeAdjustmentReason = string.IsNullOrEmpty(lastKnownSafeAdjustmentReason) ? "None" : lastKnownSafeAdjustmentReason;
            LastKnownSafeAdjustmentDistance = lastKnownSafeAdjustmentDistance;
        }

        public RuntimeSearchPoint(
            Vector3 worldPosition,
            float waitDuration,
            EnemyJumpLink25D jumpLink,
            float score,
            Vector3 jumpTraversalStart,
            Vector3 jumpTraversalEnd,
            bool isLastKnownInvestigationPoint = false,
            bool hasFacingHint = false,
            int facingHintSign = 0,
            string facingHintMode = "None",
            string facingHintSource = "None",
            int lastKnownVersionSnapshot = -1,
            Vector3 lastKnownPositionSnapshot = default,
            bool hasLastKnownFacingHintSnapshot = false,
            int lastKnownFacingSignSnapshot = 0,
            string lastKnownFacingModeSnapshot = "None",
            string lastKnownFacingSourceSnapshot = "None",
            bool hasAdjustedLastKnownSafePosition = false,
            Vector3 originalLastKnownPositionSnapshot = default,
            Vector3 adjustedLastKnownSafePosition = default,
            string lastKnownSafeAdjustmentReason = "None",
            float lastKnownSafeAdjustmentDistance = 0f)
        {
            WorldPosition = worldPosition;
            WaitDuration = Mathf.Max(0f, waitDuration);
            RequiresJumpLink = true;
            JumpLink = jumpLink;
            Score = score;
            HasExplicitJumpTraversal = jumpLink != null;
            JumpTraversalStart = jumpTraversalStart;
            JumpTraversalEnd = jumpTraversalEnd;
            IsLastKnownInvestigationPoint = isLastKnownInvestigationPoint;
            HasFacingHint = hasFacingHint;
            FacingHintSign = facingHintSign >= 0 ? 1 : -1;
            FacingHintMode = string.IsNullOrEmpty(facingHintMode) ? "None" : facingHintMode;
            FacingHintSource = string.IsNullOrEmpty(facingHintSource) ? "None" : facingHintSource;
            LastKnownVersionSnapshot = lastKnownVersionSnapshot;
            LastKnownPositionSnapshot = lastKnownPositionSnapshot;
            HasLastKnownFacingHintSnapshot = hasLastKnownFacingHintSnapshot;
            LastKnownFacingSignSnapshot = lastKnownFacingSignSnapshot >= 0 ? 1 : -1;
            LastKnownFacingModeSnapshot = string.IsNullOrEmpty(lastKnownFacingModeSnapshot) ? "None" : lastKnownFacingModeSnapshot;
            LastKnownFacingSourceSnapshot = string.IsNullOrEmpty(lastKnownFacingSourceSnapshot) ? "None" : lastKnownFacingSourceSnapshot;
            HasAdjustedLastKnownSafePosition = hasAdjustedLastKnownSafePosition;
            OriginalLastKnownPositionSnapshot = originalLastKnownPositionSnapshot;
            AdjustedLastKnownSafePosition = adjustedLastKnownSafePosition;
            LastKnownSafeAdjustmentReason = string.IsNullOrEmpty(lastKnownSafeAdjustmentReason) ? "None" : lastKnownSafeAdjustmentReason;
            LastKnownSafeAdjustmentDistance = lastKnownSafeAdjustmentDistance;
        }
    }

    private struct AnchorProjectionDebugInfo
    {
        public Vector3 RayOrigin;
        public float RayDepth;
        public bool Hit;
        public string HitObjectName;
        public int HitLayer;
        public string Reason;
    }

    private struct DynamicWalkEdgeClearanceResult
    {
        public bool IsSafe;
        public string RejectReason;
        public Vector3 Point;
        public Vector3 CenterProbeOrigin;
        public Vector3 LeftProbeOrigin;
        public Vector3 RightProbeOrigin;
        public bool CenterGroundFound;
        public bool LeftGroundFound;
        public bool RightGroundFound;
        public Vector3 CenterGroundPoint;
        public Vector3 LeftGroundPoint;
        public Vector3 RightGroundPoint;
        public string CenterGroundObject;
        public string LeftGroundObject;
        public string RightGroundObject;
        public int CenterGroundLayer;
        public int LeftGroundLayer;
        public int RightGroundLayer;
        public float LeftHeightDelta;
        public float RightHeightDelta;
        public float MinEdgeClearance;
        public float MaxGroundHeightDelta;
    }

    private struct LastKnownSafeAdjustmentResult
    {
        public bool Adjusted;
        public bool FoundSafePoint;
        public string Reason;
        public Vector3 OriginalPoint;
        public Vector3 AdjustedPoint;
        public float AdjustmentDistance;
        public string OriginalUnsafeReason;
        public string SelectedDirectionLabel;
        public int Attempts;
        public float MaxDistance;
        public float Step;
        public DynamicWalkEdgeClearanceResult OriginalEdgeResult;
        public DynamicWalkEdgeClearanceResult AdjustedEdgeResult;
    }

    private struct CombatJumpLinkCandidateDebugInfo
    {
        public EnemyJumpLink25D Link;
        public string Direction;
        public bool TraversalResolved;
        public bool Viable;
        public string RejectReason;
        public Vector3 TraversalStart;
        public Vector3 TraversalEnd;
        public Vector3 CurrentReference;
        public Vector3 TargetPosition;
        public Vector3 NavigationPosition;
        public float StartDistance;
        public float MaxStartDistance;
        public float CurrentVerticalGap;
        public float PostVerticalGap;
        public float VerticalImprovement;
        public float RequiredVerticalImprovement;
        public float SameLevelTolerance;
        public float CurrentHorizontalGap;
        public float PostHorizontalGap;
        public float AcceptablePostJumpHorizontalGap;
        public float StartToTargetGap;
        public float Score;
    }

    private abstract class Node
    {
        public abstract NodeStatus Tick();
    }

    private sealed class ConditionNode : Node
    {
        private readonly Func<bool> condition;
        public ConditionNode(Func<bool> condition) { this.condition = condition; }
        public override NodeStatus Tick() => condition != null && condition() ? NodeStatus.Success : NodeStatus.Failure;
    }

    private sealed class ActionNode : Node
    {
        private readonly Func<NodeStatus> action;
        public ActionNode(Func<NodeStatus> action) { this.action = action; }
        public override NodeStatus Tick() => action != null ? action() : NodeStatus.Failure;
    }

    private sealed class SequenceNode : Node
    {
        private readonly Node[] children;
        public SequenceNode(params Node[] children) { this.children = children; }
        public override NodeStatus Tick()
        {
            if (children == null || children.Length == 0)
                return NodeStatus.Success;

            for (int i = 0; i < children.Length; i++)
            {
                NodeStatus result = children[i].Tick();
                if (result != NodeStatus.Success)
                    return result;
            }

            return NodeStatus.Success;
        }
    }

    private sealed class SelectorNode : Node
    {
        private readonly Node[] children;
        public SelectorNode(params Node[] children) { this.children = children; }
        public override NodeStatus Tick()
        {
            if (children == null || children.Length == 0)
                return NodeStatus.Failure;

            for (int i = 0; i < children.Length; i++)
            {
                NodeStatus result = children[i].Tick();
                if (result != NodeStatus.Failure)
                    return result;
            }

            return NodeStatus.Failure;
        }
    }

    [Header("References")]
    [SerializeField] private EnemyCharacter25D character;
    [SerializeField] private EnemyHealth25D health;
    [SerializeField] private EnemyStun25D stun;
    [SerializeField] private EnemyKnockbackReceiver25D knockback;
    [SerializeField] private EnemyPerception25D perception;
    [SerializeField] private EnemyBlackboard25D blackboard;
    [SerializeField] private EnemyBallisticShooter25D shooter;
    [SerializeField] private EnemyGrenadeThrower25D grenadeThrower;
    [SerializeField] private EnemyCloseRangeRepel25D closeRangeRepel;

    [Header("Fixed Patrol")]
    [SerializeField] private bool useFixedPatrolOnStart = true;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private bool fixedPatrolLoop = true;
    [SerializeField, Min(0f)] private float patrolPointReachDistance = 0.4f;
    [SerializeField, Min(0f)] private float patrolWaitDuration = 0.75f;
    [SerializeField, Min(0f)] private float patrolMoveDeadZone = 0.1f;

    [Header("Dynamic Patrol/Search")]
    [SerializeField, Min(0f)] private float dynamicPointNearDistance = 2f;
    [SerializeField, Min(0f)] private float dynamicPointFarDistance = 4f;
    [SerializeField, Min(1)] private int dynamicPointMaxCount = 5;
    [SerializeField, Min(0f)] private float dynamicPointMinSeparation = 0.75f;
    [SerializeField] private LayerMask dynamicPointGroundMask = ~0;
    [SerializeField, Min(0f)] private float dynamicPointRaycastHeight = 3f;
    [SerializeField, Min(0f)] private float dynamicPointRaycastDepth = 8f;
    [SerializeField, Min(0f)] private float dynamicPatrolAnchorRadius = 6f;
    [SerializeField, Min(0f)] private float dynamicSearchArrivalDistance = 0.45f;
    [SerializeField, Min(0f)] private float dynamicPatrolArrivalDistance = 0.45f;
    [SerializeField, Min(0f)] private float dynamicSearchWaitDuration = 0.75f;
    [SerializeField, Min(0f)] private float dynamicPatrolWaitDuration = 0.65f;
    [SerializeField, Min(0f)] private float dynamicPatrolMinTravelDistance = 1.25f;

    [Header("Dynamic Jump Link Point Budget")]
    [SerializeField] private bool dynamicJumpLinksUseSeparateBudget = true;
    [SerializeField, Min(0)] private int dynamicJumpLinkMaxCount = 3;
    [SerializeField] private bool dynamicJumpLinksCanExceedRuntimePointMaxCount = true;
    [SerializeField] private bool preserveDynamicJumpLinksWhenTrimmingRuntimePoints = true;

    [Header("Dynamic Jump Link Exit Rules")]
    [SerializeField] private bool useSeparateDynamicJumpLinkExitRadius = true;
    [SerializeField, Min(0f)] private float dynamicJumpLinkExitAnchorRadius = 24f;
    [SerializeField] private bool allowDynamicPatrolJumpExitOutsideWalkAnchorRadius = true;

    [Header("Dynamic Point Walkability")]
    [SerializeField] private bool validateDynamicPointWalkability = true;
    [SerializeField, Min(0f)] private float dynamicPointSamePlatformVerticalTolerance = 0.45f;
    [SerializeField, Min(0.1f)] private float dynamicPointWalkabilitySampleStep = 0.5f;
    [SerializeField, Min(0.05f)] private float dynamicPointWalkabilityProbeHeight = 0.75f;
    [SerializeField, Min(0.05f)] private float dynamicPointWalkabilityProbeDepth = 1.5f;
    [SerializeField, Min(0f)] private float dynamicRegenerateMinAirborneTime = 0.12f;

    [Header("Dynamic WALK Point Edge Clearance")]
    [SerializeField] private bool requireDynamicWalkPointEdgeClearance = true;
    [SerializeField, Min(0f)] private float dynamicWalkPointMinEdgeClearance = 1f;
    [SerializeField, Min(0f)] private float dynamicWalkPointEdgeProbeUpOffset = 0.5f;
    [SerializeField, Min(0f)] private float dynamicWalkPointEdgeProbeDownDistance = 1.5f;
    [SerializeField, Min(0f)] private float dynamicWalkPointMaxGroundHeightDelta = 0.25f;
    [SerializeField] private bool applyDynamicWalkEdgeClearanceToSearchSidePoints = true;
    [SerializeField] private bool applyDynamicWalkEdgeClearanceToDynamicPatrolPoints = true;
    [SerializeField] private bool applyDynamicWalkEdgeClearanceToLocalFallbackPoints = true;
    [SerializeField] private bool warnWhenLastKnownInvestigationPointNearEdge = true;
    [SerializeField] private bool logRejectedDynamicWalkPointEdgeClearance = true;
    [SerializeField] private bool writeDynamicWalkPointEdgeClearanceLogsToFile = true;

    [Header("LastKnown Safe Adjustment")]
    [SerializeField] private bool adjustUnsafeLastKnownInvestigationPoint = true;
    [SerializeField, Min(0f)] private float lastKnownSafeAdjustmentMaxDistance = 2f;
    [SerializeField, Min(0f)] private float lastKnownSafeAdjustmentStep = 0.25f;
    [SerializeField] private bool preferLastKnownSafeAdjustmentAwayFromMissingGround = true;
    [SerializeField] private bool preserveOriginalLastKnownForDebug = true;
    [SerializeField] private bool logLastKnownSafeAdjustment = true;
    [SerializeField] private bool writeLastKnownSafeAdjustmentLogsToFile = true;

    [Header("Search")]
    [SerializeField, Min(0f)] private float searchFacingDeadZone = 0.05f;
    [SerializeField, Min(0f)] private float searchRecoveryRetryDelay = 0.3f;
    [SerializeField, Min(0f)] private float searchRecoveryMinCommitTime = 0.35f;
    [SerializeField, Min(0f)] private float searchRecoveryCompleteRadius = 0.5f;
    [SerializeField, Min(0f)] private float visibleReacquireConfirmDuration = 0.12f;
    [SerializeField, Min(0f)] private float rearAwarenessFocusDuration = 0.4f;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float desiredCombatMinRange = 5f;
    [SerializeField, Min(0f)] private float desiredCombatMaxRange = 10f;
    [SerializeField, Min(0f)] private float combatMoveDeadZone = 0.2f;
    [SerializeField] private bool allowGrenadeAttack = true;
    [SerializeField] private bool allowBackpedalFire = true;
    [SerializeField, Min(0f)] private float backpedalStartRange = 5.5f;
    [SerializeField, Min(0f)] private float backpedalSpeedMultiplier = 0.9f;
    [SerializeField] private bool allowCloseRepel = true;
    [SerializeField] private bool useAlertSearchBehavior = true;
    [SerializeField, Min(1f)] private float alertSearchSpeedMultiplier = 1.25f;

    [Header("Jump Links")]
    [SerializeField, Min(0f)] private float jumpLinkSearchRadius = 6f;
    [SerializeField] private bool allowJumpLinksInFixedPatrol = true;
    [SerializeField] private bool allowJumpLinksInDynamicSearch = true;
    [SerializeField] private bool allowJumpLinksInDynamicPatrol = true;
    [SerializeField] private bool allowCombatJumpLinkPursuit = true;
    [SerializeField, Min(0f)] private float combatJumpLinkMinVerticalDelta = 1.5f;
    [SerializeField, Min(0f)] private float combatJumpLinkAbortVerticalDelta = 0.75f;
    [SerializeField, Min(0f)] private float combatJumpLinkMaxStartDistance = 6f;
    [SerializeField, Min(0f)] private float combatJumpLinkDecisionCooldown = 0.5f;
    [SerializeField, Min(0f)] private float combatJumpLinkAbortCheckInterval = 0.1f;
    [SerializeField, Min(0f)] private float combatJumpLinkRequiredVerticalImprovement = 0.75f;
    [SerializeField, Min(0f)] private float combatJumpLinkSameLevelTolerance = 1.0f;
    [SerializeField, Min(0f)] private float combatJumpLinkAcceptablePostJumpHorizontalGap = 6.0f;
    [SerializeField] private bool abortApproachIfTargetDescends = true;

    [Header("Combat Platform-Aware Navigation")]
    [SerializeField] private bool useCombatNavigationPosition = true;
    [SerializeField] private bool requireJumpLinkForCrossPlatformCombatMovement = true;
    [SerializeField, Min(0f)] private float combatNavigationSamePlatformVerticalTolerance = 0.75f;
    [SerializeField] private bool holdPlatformWhenCombatJumpLinkUnavailable = true;
    [SerializeField] private bool logCombatPlatformNavigation = false;
    [SerializeField] private bool writeCombatPlatformNavigationLogsToFile = true;

    [Header("Combat Jump Link Diagnostics")]
    [SerializeField] private bool logCombatJumpLinkDiagnostics = true;
    [SerializeField] private bool logCombatJumpLinkRejectedCandidates = true;
    [SerializeField] private bool logCombatJumpLinkSearchSummary = true;
    [SerializeField] private bool logCombatJumpLinkDiagnosticsOnlyWhenNoLinkFound = true;
    [SerializeField] private bool writeCombatJumpLinkDiagnosticsToFile = true;
    [SerializeField, Min(0f)] private float combatJumpLinkDiagnosticCooldown = 0.25f;

    [Header("Dynamic Patrol Jump Link Selection")]
    [SerializeField, Range(0f, 1f)] private float dynamicPatrolJumpLinkSelectionWeight = 0.15f;

    [Header("Dynamic Patrol Post Jump Rules")]
    [SerializeField, Min(0)] private int dynamicPatrolWalkPointsRequiredAfterJumpLink = 1;
    [SerializeField, Min(0f)] private float dynamicPatrolDeadEndDelay = 0.75f;
    [SerializeField] private bool dynamicPatrolLogDeadEnd = true;
    [SerializeField] private bool dynamicPatrolShowBlockedJumpLinksInLogs = true;
    [SerializeField, Min(0f)] private float dynamicPatrolReturnJumpMatchTolerance = 0.35f;

    [Header("Dynamic Search Jump Link Selection")]
    [SerializeField, Min(0f)] private float dynamicSearchJumpLinkRepeatBlockDuration = 2f;
    [SerializeField, Min(0f)] private float dynamicSearchJumpLinkMinProgress = 0.5f;

    [Header("Dynamic Search Last Known Retarget")]
    [SerializeField] private bool retargetSearchRecoveryOnLastKnownUpdate = true;
    [SerializeField, Min(0f)] private float searchRetargetPositionTolerance = 0.15f;
    [SerializeField] private bool logSearchLastKnownRetargets = true;
    [SerializeField] private bool writeSearchLastKnownRetargetsToFile = true;

    [Header("Search Retarget Ignored Log Anti-Spam")]
    [SerializeField] private bool logIgnoredSearchLastKnownRetargets = false;
    [SerializeField] private bool logOnlyFirstIgnoredVisibleTargetRetargetPerSearch = true;
    [SerializeField, Min(0f)] private float ignoredSearchRetargetLogCooldown = 0.5f;

    [Header("Search LastKnown Facing Hint")]
    [SerializeField] private bool applyLastKnownFacingHintOnArrival = true;
    [SerializeField, Min(0f)] private float lastKnownInvestigationPointTolerance = 0.35f;
    [SerializeField] private bool logSearchFacingHint = true;
    [SerializeField] private bool writeSearchFacingHintLogsToFile = true;

    [Header("Search Facing Hint Diagnostics")]
    [SerializeField] private bool logSearchFacingHintDiagnostics = true;
    [SerializeField] private bool logSearchFacingHintPointBuild = true;
    [SerializeField] private bool logSearchFacingHintArrivalChecks = true;
    [SerializeField] private bool logSearchFacingHintSkipped = true;
    [SerializeField] private bool logSearchFacingHintPostApplyVerification = true;
    [SerializeField] private bool logSearchFacingHintRetargetIgnoredDetails = true;
    [SerializeField] private bool writeSearchFacingHintDiagnosticsToFile = true;
    [SerializeField, Min(0f)] private float searchFacingHintDiagnosticCooldown = 0.15f;

    [Header("Search Facing Hint Versioned Apply")]
    [SerializeField] private bool allowSearchFacingReapplyForNewLastKnownVersion = true;
    [SerializeField] private bool updateCurrentSearchPointFacingOnVisibleTargetIgnoredRetarget = true;
    [SerializeField] private bool logSearchFacingVersionedApply = true;

    [Header("Search Facing Hint Temporary Lock")]
    [SerializeField] private bool lockSearchFacingHintOnArrival = true;
    [SerializeField, Min(0f)] private float searchFacingHintLockDuration = 0.65f;
    [SerializeField] private bool clearSearchFacingLockOnSearchExit = true;
    [SerializeField] private bool clearSearchFacingLockOnSearchRetarget = false;
    [SerializeField] private bool logSearchFacingHintLock = true;
    [SerializeField] private bool writeSearchFacingHintLockLogsToFile = true;

    [Header("Search Anchor Projection Debug")]
    [SerializeField] private bool logSearchAnchorProjection = false;
    [SerializeField] private bool writeSearchAnchorProjectionLogsToFile = true;

    [Header("Search Anchor Projection Log Anti-Spam")]
    [SerializeField] private bool suppressRepeatedSearchAnchorProjectionLogs = true;
    [SerializeField, Min(0f)] private float searchAnchorProjectionLogPositionTolerance = 0.05f;
    [SerializeField] private bool logSearchAnchorProjectionOnRebuildOnly = true;

    [Header("Search Empty Recovery")]
    [SerializeField] private bool preventEmptySearchDynamicPoints = true;
    [SerializeField] private bool searchEmptyTryRouteJumpLinkFallback = true;
    [SerializeField] private bool searchEmptyAllowBridgeJumpOutsideAnchorRadius = true;
    [SerializeField] private bool searchEmptyFallbackToLocalAroundCurrentRef = true;
    [SerializeField] private bool searchEmptyFallbackToEmergencyWait = true;
    [SerializeField, Min(0f)] private float searchEmptyRouteJumpLinkMaxEntryDistance = 18f;
    [SerializeField, Min(0f)] private float searchEmptyLocalFallbackRadius = 4f;
    [SerializeField, Min(0f)] private float searchEmptyEmergencyWaitTime = 0.65f;
    [SerializeField] private bool logSearchEmptyRecovery = true;
    [SerializeField] private bool writeSearchEmptyRecoveryLogsToFile = true;

    [Header("Squad Panic Flee")]
    [SerializeField] private bool enableSquadPanicFlee = true;
    [SerializeField, Min(0f)] private float squadPanicMinFleeDistance = 2.5f;
    [SerializeField, Min(0f)] private float squadPanicPreferredFleeDistance = 5f;
    [SerializeField, Min(0f)] private float squadPanicMaxFleeDistance = 7f;
    [SerializeField, Min(0f)] private float squadPanicFleeTargetSearchStep = 0.5f;
    [SerializeField, Min(0f)] private float squadPanicReachedDistance = 0.35f;
    [SerializeField, Min(0f)] private float squadPanicEndWaitTime = 0.2f;
    [SerializeField] private bool squadPanicCanInterruptCombat = true;
    [SerializeField] private bool squadPanicCanInterruptSearch = true;
    [SerializeField] private bool squadPanicCanInterruptPatrol = true;
    [SerializeField] private bool squadPanicDoNotInterruptJumpTraversal = true;
    [SerializeField] private bool logSquadPanicFlee = true;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawNavigationGizmos = true;
    [SerializeField] private bool drawCurrentTargetPoint = true;
    [SerializeField] private bool drawGeneratedDynamicPoints = true;
    [SerializeField] private bool drawActiveTraversalLink = true;
    [SerializeField] private bool drawDynamicPatrolAnchor = true;
    [SerializeField] private bool drawOnlyWhenSelected = true;
    [SerializeField] private bool drawDynamicPointGenerationWarnings = true;
    [SerializeField] private bool logDynamicPointGenerationFailures = false;

    [Header("Dynamic Points Console Debug")]
    [SerializeField] private bool logDynamicPointListOnRebuild = false;
    [SerializeField] private bool logDynamicPointListVerbose = true;
    [SerializeField, Min(0f)] private float dynamicPointListLogCooldown = 0.25f;
    [SerializeField] private bool logDynamicPointClearEvents = false;

    [Header("Runtime Points Clear Log Anti-Spam")]
    [SerializeField] private bool suppressNoOpRuntimePointsClearLogs = true;

    [Header("Enemy File Logging")]
    [SerializeField] private bool writeEnemyDebugLogsToFile = false;
    [SerializeField] private bool logEnemyFilePathOnStart = true;
    [SerializeField] private bool writeDynamicPointLogsToFile = true;
    [SerializeField] private bool writeRejectedJumpLinkLogsToFile = true;
    [SerializeField] private bool writeRuntimePointClearLogsToFile = true;
    [SerializeField] private bool writeDynamicPatrolDeadEndLogsToFile = true;

    [Header("Dynamic Jump Link Direction Debug")]
    [SerializeField] private bool validateDynamicJumpLinkStartReachability = true;
    [SerializeField, Min(0f)] private float dynamicJumpLinkStartReachabilityExtraTolerance = 0.15f;
    [SerializeField] private bool limitOneDynamicCandidatePerJumpLink = true;
    [SerializeField] private bool logRejectedDynamicJumpLinkCandidates = false;
    [SerializeField, Min(0f)] private float rejectedDynamicJumpLinkLogCooldown = 0.25f;

    [Header("Navigation Gizmo Style")]
    [SerializeField, Min(0f)] private float gizmoPointRadius = 0.2f;
    [SerializeField, Min(0f)] private float gizmoCurrentPointRadius = 0.3f;
    [SerializeField, Min(0f)] private float gizmoLinkPointRadius = 0.22f;
    [SerializeField, Min(0f)] private float gizmoVerticalOffset = 0.15f;
    [SerializeField] private bool drawJumpLinkSearchRadiusGizmo = true;
    [SerializeField] private Color combatJumpLinkSearchRadiusColor = new Color(1f, 0.55f, 0.15f, 0.75f);
    [SerializeField] private Color recoveryJumpLinkSearchRadiusColor = new Color(0.2f, 0.85f, 1f, 0.55f);

    [Header("Cover")]
    [SerializeField] private bool useCover = true;
    [SerializeField, Min(0f)] private float coverSearchRadius = 12f;
    [SerializeField, Min(0f)] private float coverRepathInterval = 1.0f;
    [SerializeField, Min(0f)] private float coverStandReachDistance = 0.35f;
    [SerializeField, Min(0f)] private float coverPeekReachDistance = 0.35f;
    [SerializeField, Min(0f)] private float peekDurationMin = 0.55f;
    [SerializeField, Min(0f)] private float peekDurationMax = 1.15f;
    [SerializeField, Min(0f)] private float hideDurationMin = 0.45f;
    [SerializeField, Min(0f)] private float hideDurationMax = 0.95f;
    [SerializeField, Min(0f)] private float coverMinDistanceFromTarget = 2f;

    private Node rootNode;
    private BrainState currentState;
    private EnemyAction25D currentAction;
    private bool isInFixedPatrolBranch;
    private bool isInDynamicPatrolBranch;
    private bool isInDynamicSearchBranch;
    private bool lastDynamicPointGenerationSucceeded;
    private int lastGeneratedDynamicPointCount;
    private Vector3 lastDynamicGenerationAnchor;
    private bool dynamicPatrolHasValidPoints;
    private bool dynamicSearchHasValidPoints;
    private RuntimePointMode lastGeneratedPointSetType;
    private int patrolIndex;
    private float patrolWaitEndTime;
    private float runtimePointWaitEndTime;
    private float nextCoverSearchTime;
    private float nextPeekToggleTime;
    private bool isPeeking;
    private EnemyCoverPoint25D selectedCover;

    private readonly List<RuntimeSearchPoint> runtimePoints = new List<RuntimeSearchPoint>(8);
    private RuntimePointMode runtimePointMode;
    private int runtimePointIndex;
    private Vector3 runtimePointsAnchor;
    private bool hasRuntimePoints;
    private bool hasDynamicPatrolAnchor;
    private Vector3 dynamicPatrolAnchor;
    private EnemyJumpLink25D activeTraversalLink;
    private float lastJumpLinkUseTime = float.NegativeInfinity;
    private float combatEnterTime;
    private bool wasInCombatLastFrame;
    private float searchRecoveryRetryTime = float.NegativeInfinity;
    private bool searchRecoveryPending;
    private bool searchRecoveryActive;
    private Vector3 activeSearchRecoveryTarget;
    private bool activeSearchRecoveryHasFacingHint;
    private int activeSearchRecoveryFacingSign = 1;
    private string activeSearchRecoveryFacingMode = "None";
    private string activeSearchRecoveryFacingSource = "None";
    private bool activeSearchRecoveryFacingApplied;
    private int activeSearchRecoveryFacingAppliedVersion = -1;
    private float lastSearchFacingHintDiagnosticLogTime = float.NegativeInfinity;
    private int lastSearchFacingHintDiagnosticLogFrame = -1;
    private string lastSearchFacingHintDiagnosticKey = "None";
    private bool pendingSearchFacingHintPostApplyVerification;
    private int pendingSearchFacingHintVerifyFrame = -1;
    private float pendingSearchFacingHintVerifyTime = float.NegativeInfinity;
    private int pendingSearchFacingHintExpectedSign;
    private Vector3 pendingSearchFacingHintPoint;
    private string pendingSearchFacingHintSource = "None";
    private string pendingSearchFacingHintMode = "None";
    private int pendingSearchFacingHintAppliedFrame = -1;
    private float pendingSearchFacingHintAppliedTime = float.NegativeInfinity;
    private int observedSearchLastKnownVersion = -1;
    private Vector3 observedSearchLastKnownPosition;
    private int consecutiveEmptySearchRecoveries;
    private Vector3 lastSearchAnchorRawTarget;
    private Vector3 lastSearchAnchorResolved;
    private bool lastSearchAnchorWasProjected;
    private string lastSearchAnchorProjectionReason = "None";
    private string lastSearchAnchorHitName = "None";
    private int lastSearchAnchorHitLayer = -1;
    private Vector3 lastSearchAnchorRayOrigin;
    private float lastSearchAnchorRayDepth;
    private bool hasLastLoggedSearchAnchorProjection;
    private Vector3 lastLoggedSearchAnchorRawTarget;
    private Vector3 lastLoggedSearchAnchorResolvedAnchor;
    private string lastLoggedSearchAnchorProjectionReason = "None";
    private string lastLoggedSearchAnchorHitObject = "None";
    private int lastLoggedSearchAnchorFrame = -1;
    private bool forceNextSearchAnchorProjectionLog;
    private string forcedSearchAnchorProjectionLogReason = "None";
    private bool hasLoggedIgnoredVisibleTargetRetargetThisSearch;
    private float lastIgnoredSearchRetargetLogTime = float.NegativeInfinity;
    private float searchRecoveryCommitUntilTime = float.NegativeInfinity;
    private bool visibleReacquirePending;
    private float visibleReacquireConfirmStartTime = float.NegativeInfinity;
    private float rearAwarenessFocusEndTime = float.NegativeInfinity;
    private EnemyJumpLink25D activeCombatJumpLink;
    private bool isApproachingCombatJumpLink;
    private float nextCombatJumpLinkDecisionTime = float.NegativeInfinity;
    private float nextCombatJumpLinkAbortCheckTime = float.NegativeInfinity;
    private Vector3 combatJumpLinkTargetSnapshot;
    private Vector3 activeCombatJumpLinkStartPoint;
    private Vector3 activeCombatJumpLinkEndPoint;
    private int observedLandingEventVersion;
    private EnemyJumpLink25D lastCompletedDynamicJumpLink;
    private BrainState lastCompletedDynamicJumpLinkState;
    private float lastCompletedDynamicJumpLinkTime = float.NegativeInfinity;
    private Vector3 lastCompletedDynamicJumpLinkEnd;
    private int dynamicPatrolWalkPointsRemainingBeforeJumpLinkAllowed;
    private EnemyJumpLink25D lastDynamicPatrolJumpLink;
    private Vector3 lastDynamicPatrolJumpStart;
    private Vector3 lastDynamicPatrolJumpEnd;
    private bool hasLastDynamicPatrolJump;
    private bool dynamicPatrolDeadEndDelayActive;
    private float dynamicPatrolDeadEndResumeTime = float.NegativeInfinity;
    private EnemyJumpLink25D dynamicPatrolDeadEndReturnLink;
    private Vector3 dynamicPatrolDeadEndReturnStart;
    private Vector3 dynamicPatrolDeadEndReturnEnd;
    private float lastDynamicPointListLogTime = float.NegativeInfinity;
    private int lastDynamicPointListLogFrame = -1;
    private int rejectedDynamicWalkPointUnsafeEdgeCount;
    private int warnedLastKnownInvestigationPointNearEdgeCount;
    private int adjustedLastKnownSafePointCount;
    private int failedLastKnownSafeAdjustmentCount;
    private float lastRejectedDynamicJumpLinkLogTime = float.NegativeInfinity;
    private int lastRejectedDynamicJumpLinkLogFrame = -1;
    private float lastCombatJumpLinkDiagnosticLogTime = float.NegativeInfinity;
    private int lastCombatJumpLinkDiagnosticLogFrame = -1;
    private string lastCombatJumpLinkDiagnosticKey = "None";
    private bool squadPanicFleeActive;
    private Vector3 squadPanicFleeFromPosition;
    private Vector3 squadPanicFleeTargetPosition;
    private float squadPanicFleeUntilTime = float.NegativeInfinity;
    private float squadPanicWaitUntilTime = float.NegativeInfinity;
    private string squadPanicFleeReason = "None";
    private bool squadPanicHasTarget;
    private BrainState previousStateBeforeSquadPanic;
    private EnemyAction25D previousActionBeforeSquadPanic;

    private static bool hasLoggedEnemyDebugFilePathThisSession;

    public BrainState CurrentState => currentState;
    public EnemyAction25D CurrentAction => currentAction;
    public bool IsAlert => currentState == BrainState.SearchDynamic || currentState == BrainState.Combat;
    public bool IsInCombat => currentState == BrainState.Combat;
    public bool IsInSquadPanicFlee => squadPanicFleeActive;

    public bool IsInActiveCombatPressure
    {
        get
        {
            if (currentState == BrainState.Combat || currentState == BrainState.Disabled)
                return true;

            switch (currentAction)
            {
                case EnemyAction25D.FirePrimary:
                case EnemyAction25D.PrepareGrenade:
                case EnemyAction25D.ThrowGrenade:
                case EnemyAction25D.PostGrenadeRetreat:
                case EnemyAction25D.BackpedalFire:
                case EnemyAction25D.PrepareRepel:
                case EnemyAction25D.RepelCloseTarget:
                case EnemyAction25D.Recover:
                case EnemyAction25D.Disabled:
                    return true;
                default:
                    return false;
            }
        }
    }
    public bool IsTakingCover => currentState == BrainState.Combat && selectedCover != null;
    public EnemyCoverPoint25D SelectedCover => selectedCover;
    public bool IsInFixedPatrolBranch => isInFixedPatrolBranch;
    public bool IsInDynamicPatrolBranch => isInDynamicPatrolBranch;
    public bool IsInDynamicSearchBranch => isInDynamicSearchBranch;
    public bool LastDynamicPointGenerationSucceeded => lastDynamicPointGenerationSucceeded;
    public int LastGeneratedDynamicPointCount => lastGeneratedDynamicPointCount;
    public Vector3 LastDynamicGenerationAnchor => lastDynamicGenerationAnchor;
    public bool DynamicPatrolHasValidPoints => dynamicPatrolHasValidPoints;
    public bool DynamicSearchHasValidPoints => dynamicSearchHasValidPoints;
    public string LastGeneratedPointSetType => lastGeneratedPointSetType.ToString();
    public float CombatTimeElapsed => currentState == BrainState.Combat ? Mathf.Max(0f, Time.time - combatEnterTime) : 0f;
    public int RuntimePointCount => runtimePoints.Count;
    public int RuntimePointProgress => runtimePoints.Count > 0 ? Mathf.Clamp(runtimePointIndex + 1, 1, runtimePoints.Count) : 0;

    public bool TryGetCurrentRuntimePointNavigationTarget(out Vector3 target)
    {
        if (runtimePoints.Count > 0)
        {
            int index = Mathf.Clamp(runtimePointIndex, 0, runtimePoints.Count - 1);
            target = GetRuntimePointNavigationTarget(runtimePoints[index]);
            return true;
        }

        target = transform.position;
        return false;
    }

    public bool TryGetCurrentRuntimePointFinalDestination(out Vector3 destination)
    {
        if (runtimePoints.Count > 0)
        {
            int index = Mathf.Clamp(runtimePointIndex, 0, runtimePoints.Count - 1);
            destination = GetRuntimePointFinalDestination(runtimePoints[index]);
            return true;
        }

        destination = transform.position;
        return false;
    }

    public bool TryGetCurrentRuntimeJumpLinkTargets(out Vector3 walkTo, out Vector3 jumpTo, out EnemyJumpLink25D link)
    {
        if (runtimePoints.Count > 0)
        {
            int index = Mathf.Clamp(runtimePointIndex, 0, runtimePoints.Count - 1);
            RuntimeSearchPoint point = runtimePoints[index];
            if (point.RequiresJumpLink && point.JumpLink != null && point.HasExplicitJumpTraversal)
            {
                walkTo = point.JumpTraversalStart;
                jumpTo = point.JumpTraversalEnd;
                link = point.JumpLink;
                return true;
            }
        }

        walkTo = transform.position;
        jumpTo = transform.position;
        link = null;
        return false;
    }

    private void Reset()
    {
        AutoAssign();
        ClampSettings();
    }

    private void Awake()
    {
        AutoAssign();
        ClampSettings();
        observedLandingEventVersion = character != null ? character.LandingEventVersion : 0;
        BuildTree();
        SetState(BrainState.Idle);
    }

    private void OnValidate()
    {
        AutoAssign();
        ClampSettings();
    }

    private void OnDisable()
    {
        ReleaseSelectedCover();
        if (grenadeThrower != null)
        {
            grenadeThrower.CancelPrepare();
            grenadeThrower.CancelPostGrenadeRetreat();
        }
        if (closeRangeRepel != null)
            closeRangeRepel.CancelRepel();
        if (character != null)
        {
            ClearSearchFacingLockFromBrain("BrainDisabled");
            character.ClearManualFacingOverride();
            character.ResetExternalMoveSpeedMultiplier();
            character.SetAllowWalkingOffEdgesForTraversal(false);
        }
        pendingSearchFacingHintPostApplyVerification = false;
        CancelCombatJumpLinkApproach();
        ClearSquadPanicFleeRuntime("BrainDisabled");
    }

    private void Update()
    {
        AutoAssign();
        if (rootNode == null)
            BuildTree();

        ResetBranchDebugFlags();
        currentAction = EnemyAction25D.None;
        UpdateContextMovementSpeedMultiplier();
        if (character != null)
            character.SetAllowWalkingOffEdgesForTraversal(false);
        if (grenadeThrower != null)
            grenadeThrower.TickRecovery();

        HandleRearAwarenessRecovery();
        if (perception != null && perception.JustLostVisibleTargetThisFrame && perception.HasLastKnownPosition)
            BeginSearchRecovery(perception.LastKnownTargetPosition);

        UpdateVisibleReacquireState();

        if (searchRecoveryPending && Time.time >= searchRecoveryRetryTime)
        {
            searchRecoveryPending = false;
            searchRecoveryRetryTime = float.NegativeInfinity;
            ClearRuntimePoints();
        }

        if (perception != null && perception.IsTargetVisible && perception.CurrentTarget != null && !searchRecoveryActive)
        {
            hasDynamicPatrolAnchor = false;
            searchRecoveryPending = false;
            searchRecoveryRetryTime = float.NegativeInfinity;
            if (blackboard != null)
                blackboard.MarkPlayerDetectedForever();
        }

        HandleDynamicInvalidationEvents();

        rootNode.Tick();

        if (currentState != BrainState.Combat)
        {
            wasInCombatLastFrame = false;
            combatEnterTime = 0f;
            CancelCombatJumpLinkApproach();
            if (grenadeThrower != null && grenadeThrower.IsPreparingThrow)
                grenadeThrower.CancelPrepare();
            if (closeRangeRepel != null && closeRangeRepel.IsInRepelFlow)
                closeRangeRepel.CancelRepel();
            if (character != null)
                character.ClearManualFacingOverride();
        }

        UpdateBlackboard();
        UpdateSearchFacingHintPostApplyVerification();
    }

    private void HandleDynamicInvalidationEvents()
    {
        if (character == null)
            return;

        int landingVersion = character.LandingEventVersion;
        if (landingVersion == observedLandingEventVersion)
            return;

        observedLandingEventVersion = landingVersion;

        if (character.IsJumpTraversalActive)
            return;

        if (character.LastAirborneDuration < dynamicRegenerateMinAirborneTime)
            return;

        InvalidateRuntimeDynamicPointsAfterLanding();
    }

    private void InvalidateRuntimeDynamicPointsAfterLanding()
    {
        ClearRuntimePoints();

        Vector3 referencePosition = GetTraversalReferencePosition();
        referencePosition.z = 0f;

        if (currentState == BrainState.PatrolDynamic)
        {
            dynamicPatrolAnchor = referencePosition;
            hasDynamicPatrolAnchor = true;
            return;
        }

        if (currentState != BrainState.SearchDynamic)
            hasDynamicPatrolAnchor = false;
    }

    private void BuildTree()
    {
        rootNode = new SelectorNode(
            new SequenceNode(new ConditionNode(IsDead), new ActionNode(RunDeadBranch)),
            new SequenceNode(new ConditionNode(IsDisabled), new ActionNode(RunDisabledBranch)),
            new SequenceNode(new ConditionNode(ShouldRunSquadPanicFleeBranch), new ActionNode(RunSquadPanicFleeBranch)),
            new SequenceNode(new ConditionNode(ShouldRunCombatBranch), new ActionNode(RunCombatBranch)),
            new SequenceNode(new ConditionNode(ShouldRunSearchRecoveryBranch), new ActionNode(RunDynamicSearchBranch)),
            new ActionNode(RunPatrolOrIdleBranch));
    }

    private bool IsDead() => health != null && health.IsDead;
    private bool IsDisabled() => (stun != null && stun.IsStunned) || (knockback != null && (knockback.IsLaunched || knockback.IsRecovering)) || (character != null && character.IsJumpTraversalActive);
    private bool HasVisibleTarget() => perception != null && perception.IsTargetVisible && perception.CurrentTarget != null;
    private bool ShouldRunCombatBranch()
    {
        bool grenadeActive = grenadeThrower != null && grenadeThrower.IsInAnyGrenadeExclusiveState;
        bool repelActive = closeRangeRepel != null && closeRangeRepel.IsInRepelFlow;
        bool visibleTargetEligible = HasVisibleTarget();
        if (searchRecoveryActive && visibleTargetEligible)
            visibleTargetEligible = visibleReacquirePending && Time.time >= visibleReacquireConfirmStartTime + visibleReacquireConfirmDuration;
        return grenadeActive || repelActive || visibleTargetEligible;
    }
    private bool HasLastKnownTarget() => perception != null && perception.HasLastKnownPosition;
    private bool ShouldRunSearchRecoveryBranch() => searchRecoveryActive || HasLastKnownTarget();

    private bool ShouldRunSquadPanicFleeBranch()
    {
        return squadPanicFleeActive && enableSquadPanicFlee;
    }

    public bool RequestSquadPanicFlee(Vector3 fleeFromPosition, float duration, string reason = "SquadPanic")
    {
        AutoAssign();

        if (!enableSquadPanicFlee)
        {
            LogSquadPanicFleeSkipped("Disabled", fleeFromPosition, duration, reason);
            return false;
        }

        if (!isActiveAndEnabled)
            return false;

        if (duration <= 0f)
        {
            LogSquadPanicFleeSkipped("InvalidDuration", fleeFromPosition, duration, reason);
            return false;
        }

        if (IsDead())
        {
            LogSquadPanicFleeSkipped("Dead", fleeFromPosition, duration, reason);
            return false;
        }

        if (character != null && character.IsJumpTraversalActive && squadPanicDoNotInterruptJumpTraversal)
        {
            LogSquadPanicFleeSkipped("JumpTraversalActive", fleeFromPosition, duration, reason);
            return false;
        }

        if (!CanSquadPanicInterruptCurrentState())
        {
            LogSquadPanicFleeSkipped("CurrentStateNotInterruptible", fleeFromPosition, duration, reason);
            return false;
        }

        if (!TryResolveSquadPanicFleeTarget(fleeFromPosition, out Vector3 fleeTarget, out string targetReason))
        {
            LogSquadPanicFleeSkipped("NoSafeFleeTarget", fleeFromPosition, duration, reason);
            return false;
        }

        StartSquadPanicFlee(fleeFromPosition, fleeTarget, duration, $"{reason}:{targetReason}");
        return true;
    }

    private bool CanSquadPanicInterruptCurrentState()
    {
        if (currentState == BrainState.SquadPanicFlee)
            return true;

        switch (currentState)
        {
            case BrainState.Combat:
                return squadPanicCanInterruptCombat;

            case BrainState.SearchDynamic:
                return squadPanicCanInterruptSearch;

            case BrainState.PatrolDynamic:
            case BrainState.PatrolFixed:
            case BrainState.Idle:
                return squadPanicCanInterruptPatrol;

            case BrainState.Disabled:
                return false;

            default:
                return true;
        }
    }

    private bool TryResolveSquadPanicFleeTarget(Vector3 fleeFromPosition, out Vector3 fleeTarget, out string targetReason)
    {
        fleeTarget = Vector3.zero;
        targetReason = "None";

        if (character == null)
            return false;

        Vector3 current = GetTraversalReferencePosition();
        current.z = 0f;

        int fleeSign = ResolveHorizontalSign(current.x - fleeFromPosition.x, character != null ? character.FacingSign : 1);
        float minDistance = Mathf.Max(0f, squadPanicMinFleeDistance);
        float preferredDistance = Mathf.Max(minDistance, squadPanicPreferredFleeDistance);
        float maxDistance = Mathf.Max(preferredDistance, squadPanicMaxFleeDistance);
        float step = Mathf.Max(0.1f, squadPanicFleeTargetSearchStep);

        if (TryResolveSquadPanicFleeCandidate(current, fleeSign, preferredDistance, out fleeTarget, out targetReason))
            return true;

        for (float distance = minDistance; distance <= maxDistance + 0.001f; distance += step)
        {
            if (Mathf.Abs(distance - preferredDistance) <= 0.01f)
                continue;

            if (TryResolveSquadPanicFleeCandidate(current, fleeSign, distance, out fleeTarget, out targetReason))
                return true;
        }

        return false;
    }

    private bool TryResolveSquadPanicFleeCandidate(Vector3 currentReference, int fleeSign, float distance, out Vector3 fleeTarget, out string targetReason)
    {
        fleeTarget = Vector3.zero;
        targetReason = "None";

        float targetX = currentReference.x + fleeSign * Mathf.Max(0f, distance);
        if (!TryProjectSquadPanicFleePointToGround(targetX, currentReference, out Vector3 projectedPoint, out string rejectReason))
        {
            targetReason = rejectReason;
            return false;
        }

        if (!IsNormallyReachableDynamicPoint(currentReference, projectedPoint))
        {
            targetReason = "NotNormallyReachable";
            return false;
        }

        if (!IsDynamicWalkPointEdgeSafe(projectedPoint, false, out DynamicWalkEdgeClearanceResult edgeResult))
        {
            targetReason = "UnsafeEdge:" + edgeResult.RejectReason;
            return false;
        }

        fleeTarget = projectedPoint;
        targetReason = $"SafeWalkDistance{distance:0.00}";
        LogSquadPanicFleeTargetResolved(currentReference, fleeTarget, distance, targetReason);
        return true;
    }

    private bool TryProjectSquadPanicFleePointToGround(float targetX, Vector3 referencePosition, out Vector3 groundedPoint, out string rejectReason)
    {
        float rayDepth = dynamicPointRaycastDepth + dynamicPointRaycastHeight + 0.5f;
        Vector3 rayOrigin = new Vector3(targetX, referencePosition.y + dynamicPointRaycastHeight, referencePosition.z);

        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDepth, dynamicPointGroundMask, QueryTriggerInteraction.Ignore))
        {
            groundedPoint = Vector3.zero;
            rejectReason = "GroundMissing";
            return false;
        }

        groundedPoint = hit.point;
        groundedPoint.z = 0f;
        rejectReason = "None";
        return true;
    }

    private void StartSquadPanicFlee(Vector3 fleeFromPosition, Vector3 fleeTargetPosition, float duration, string reason)
    {
        previousStateBeforeSquadPanic = currentState;
        previousActionBeforeSquadPanic = currentAction;

        squadPanicFleeActive = true;
        squadPanicFleeFromPosition = fleeFromPosition;
        squadPanicFleeTargetPosition = fleeTargetPosition;
        squadPanicFleeUntilTime = Time.time + Mathf.Max(0f, duration);
        squadPanicWaitUntilTime = float.NegativeInfinity;
        squadPanicFleeReason = string.IsNullOrWhiteSpace(reason) ? "SquadPanic" : reason;
        squadPanicHasTarget = true;

        searchRecoveryPending = false;
        searchRecoveryRetryTime = float.NegativeInfinity;
        if (searchRecoveryActive)
            EndSearchRecovery(false);

        ReleaseSelectedCover();
        CancelCombatJumpLinkApproach();
        if (grenadeThrower != null)
        {
            grenadeThrower.CancelPrepare();
            grenadeThrower.CancelPostGrenadeRetreat();
        }
        if (closeRangeRepel != null)
            closeRangeRepel.CancelRepel();

        ClearRuntimePoints("SquadPanicFleeStarted");
        SetState(BrainState.SquadPanicFlee);
        currentAction = EnemyAction25D.MoveToSquadPanicPoint;
        LogSquadPanicFleeStarted(duration);
    }

    private NodeStatus RunSquadPanicFleeBranch()
    {
        SetState(BrainState.SquadPanicFlee);
        SetBranchDebugFlags();
        ReleaseSelectedCover();

        if (character == null)
        {
            FinishSquadPanicFlee("CharacterMissing");
            return NodeStatus.Running;
        }

        if (!squadPanicHasTarget)
        {
            FinishSquadPanicFlee("NoTarget");
            return NodeStatus.Running;
        }

        if (Time.time >= squadPanicFleeUntilTime)
        {
            FinishSquadPanicFlee("DurationExpired");
            return NodeStatus.Running;
        }

        Vector3 current = GetTraversalReferencePosition();
        float deltaX = squadPanicFleeTargetPosition.x - current.x;
        float deltaY = squadPanicFleeTargetPosition.y - current.y;

        if (Mathf.Abs(deltaX) <= squadPanicReachedDistance && Mathf.Abs(deltaY) <= dynamicPointSamePlatformVerticalTolerance)
        {
            currentAction = EnemyAction25D.WaitAfterSquadPanic;
            character.StopMovement();

            if (squadPanicWaitUntilTime < 0f)
                squadPanicWaitUntilTime = Time.time + squadPanicEndWaitTime;

            if (Time.time >= squadPanicWaitUntilTime)
                FinishSquadPanicFlee("ReachedTarget");

            return NodeStatus.Running;
        }

        squadPanicWaitUntilTime = float.NegativeInfinity;
        currentAction = EnemyAction25D.MoveToSquadPanicPoint;
        MoveTowardsX(squadPanicFleeTargetPosition.x);
        return NodeStatus.Running;
    }

    private void FinishSquadPanicFlee(string reason)
    {
        if (!squadPanicFleeActive)
            return;

        LogSquadPanicFleeEnded(reason);
        ClearSquadPanicFleeRuntime(reason);

        if (character != null)
            character.StopMovement();

        ResumeNormalAIAfterSquadPanic(reason);
    }

    private void ClearSquadPanicFleeRuntime(string reason)
    {
        squadPanicFleeActive = false;
        squadPanicHasTarget = false;
        squadPanicFleeReason = "None";
        squadPanicFleeUntilTime = float.NegativeInfinity;
        squadPanicWaitUntilTime = float.NegativeInfinity;
        squadPanicFleeFromPosition = Vector3.zero;
        squadPanicFleeTargetPosition = Vector3.zero;
    }

    private void ResumeNormalAIAfterSquadPanic(string reason)
    {
        if (perception != null && perception.IsTargetVisible && perception.CurrentTarget != null)
        {
            SetState(BrainState.Combat);
            return;
        }

        if (perception != null && perception.HasLastKnownPosition)
        {
            BeginSearchRecovery(perception.LastKnownTargetPosition);
            SetState(BrainState.SearchDynamic);
            return;
        }

        hasDynamicPatrolAnchor = false;
        ClearRuntimePoints("SquadPanicFinished");
        SetState(BrainState.Idle);
    }

    private void LogSquadPanicFleeStarted(float duration)
    {
        if (!logSquadPanicFlee)
            return;

        Debug.Log(
            $"[EnemyBrainBT25D] Squad panic flee started\n" +
            $"Enemy: {name}\n" +
            $"Reason: {squadPanicFleeReason}\n" +
            $"PreviousState: {previousStateBeforeSquadPanic}\n" +
            $"PreviousAction: {previousActionBeforeSquadPanic}\n" +
            $"FleeFrom: {FormatVector3ForLog(squadPanicFleeFromPosition)}\n" +
            $"FleeTarget: {FormatVector3ForLog(squadPanicFleeTargetPosition)}\n" +
            $"Duration: {duration:F2}\n" +
            $"UntilTime: {squadPanicFleeUntilTime:F2}",
            this);
    }

    private void LogSquadPanicFleeSkipped(string skipReason, Vector3 fleeFromPosition, float duration, string requestReason)
    {
        if (!logSquadPanicFlee)
            return;

        Debug.Log(
            $"[EnemyBrainBT25D] Squad panic flee skipped\n" +
            $"Enemy: {name}\n" +
            $"SkipReason: {skipReason}\n" +
            $"RequestReason: {requestReason}\n" +
            $"CurrentState: {currentState}\n" +
            $"CurrentAction: {currentAction}\n" +
            $"FleeFrom: {FormatVector3ForLog(fleeFromPosition)}\n" +
            $"Duration: {duration:F2}\n" +
            $"IsTraversalActive: {(character != null && character.IsJumpTraversalActive)}",
            this);
    }

    private void LogSquadPanicFleeEnded(string reason)
    {
        if (!logSquadPanicFlee)
            return;

        Debug.Log(
            $"[EnemyBrainBT25D] Squad panic flee ended\n" +
            $"Enemy: {name}\n" +
            $"Reason: {reason}\n" +
            $"FleeFrom: {FormatVector3ForLog(squadPanicFleeFromPosition)}\n" +
            $"FleeTarget: {FormatVector3ForLog(squadPanicFleeTargetPosition)}\n" +
            $"CurrentState: {currentState}\n" +
            $"CurrentAction: {currentAction}",
            this);
    }

    private void LogSquadPanicFleeTargetResolved(Vector3 currentReference, Vector3 target, float distance, string reason)
    {
        if (!logSquadPanicFlee)
            return;

        Debug.Log(
            $"[EnemyBrainBT25D] Squad panic flee target resolved\n" +
            $"Enemy: {name}\n" +
            $"Reason: {reason}\n" +
            $"CurrentRef: {FormatVector3ForLog(currentReference)}\n" +
            $"Target: {FormatVector3ForLog(target)}\n" +
            $"Distance: {distance:F2}",
            this);
    }

    private void BeginSearchRecovery(Vector3 targetPoint)
    {
        if (perception == null)
            return;

        searchRecoveryActive = true;
        activeSearchRecoveryTarget = targetPoint;
        LogSearchFacingHintReset("BeginSearchRecovery", runtimePoints.Count, runtimePointMode);
        CaptureActiveSearchRecoveryFacingHintFromPerception();
        observedSearchLastKnownVersion = perception != null ? perception.LastKnownPositionVersion : -1;
        observedSearchLastKnownPosition = targetPoint;
        searchRecoveryCommitUntilTime = Time.time + searchRecoveryMinCommitTime;
        searchRecoveryPending = false;
        searchRecoveryRetryTime = float.NegativeInfinity;
        visibleReacquirePending = false;
        visibleReacquireConfirmStartTime = float.NegativeInfinity;
        ResetIgnoredSearchRetargetLogState();
        ForceNextSearchAnchorProjectionLog("BeginSearchRecovery");
        ClearRuntimePoints("Begin search recovery");
        ReleaseSelectedCover();

        if (grenadeThrower != null)
        {
            grenadeThrower.CancelPrepare();
            grenadeThrower.CancelPostGrenadeRetreat();
        }

        if (closeRangeRepel != null)
            closeRangeRepel.CancelRepel();

        if (isApproachingCombatJumpLink)
            CancelCombatJumpLinkApproach();
    }

    public void NotifyProjectileHitVisibleTarget(ProjectileHitAwarenessContext context)
    {
        AutoAssign();

        if (IsDead())
        {
            LogProjectileHitAwarenessBrain("VisibleTargetCombatSkippedDead", context, Vector3.zero, "Dead");
            return;
        }

        if (character != null && character.IsJumpTraversalActive)
        {
            LogProjectileHitAwarenessBrain("VisibleTargetCombatSkippedTraversalActive", context, Vector3.zero, "TraversalActive");
            return;
        }

        if (perception == null || !perception.IsTargetVisible || perception.CurrentTarget == null)
        {
            LogProjectileHitAwarenessBrain("VisibleTargetCombatSkippedNoVisibleTarget", context, Vector3.zero, "NoVisibleTarget");
            return;
        }

        if (blackboard != null)
            blackboard.MarkPlayerDetectedForever();

        if (searchRecoveryActive)
            EndSearchRecovery(false);

        hasDynamicPatrolAnchor = false;
        searchRecoveryPending = false;
        searchRecoveryRetryTime = float.NegativeInfinity;
        SetState(BrainState.Combat);
        LogProjectileHitAwarenessBrain("VisibleTargetCombat", context, perception.CurrentTarget.position, "TargetVisible");
    }

    public void NotifyProjectileHitSearchRequested(Vector3 lastKnownPosition, string reason)
    {
        AutoAssign();

        if (IsDead())
        {
            LogProjectileHitAwarenessBrain("SearchSkippedDead", default(ProjectileHitAwarenessContext), lastKnownPosition, reason);
            return;
        }

        if (character != null && character.IsJumpTraversalActive)
        {
            LogProjectileHitAwarenessBrain("SearchSkippedTraversalActive", default(ProjectileHitAwarenessContext), lastKnownPosition, reason);
            return;
        }

        if (perception != null && perception.IsTargetVisible && perception.CurrentTarget != null)
        {
            NotifyProjectileHitVisibleTarget(default(ProjectileHitAwarenessContext));
            return;
        }

        if (blackboard != null)
            blackboard.MarkPlayerDetectedForever();

        BeginSearchRecovery(lastKnownPosition);
        SetState(BrainState.SearchDynamic);
        LogProjectileHitAwarenessBrain("SearchRequested", default(ProjectileHitAwarenessContext), lastKnownPosition, reason);
    }

    public void NotifyProjectileHitLocalAlert(ProjectileHitAwarenessContext context)
    {
        AutoAssign();

        if (IsDead())
        {
            LogProjectileHitAwarenessBrain("LocalAlertSkippedDead", context, Vector3.zero, "Dead");
            return;
        }

        if (character != null && character.IsJumpTraversalActive)
        {
            LogProjectileHitAwarenessBrain("LocalAlertSkippedTraversalActive", context, Vector3.zero, "TraversalActive");
            return;
        }

        Vector3 localPoint = context.HasHitPosition ? context.HitPosition : transform.position;
        if (Mathf.Abs(localPoint.x - transform.position.x) <= 0.1f)
            localPoint.x += character != null ? -character.FacingSign * 1.5f : 1.5f;

        BeginSearchRecovery(localPoint);
        SetState(BrainState.SearchDynamic);
        LogProjectileHitAwarenessBrain("LocalAlertFallback", context, localPoint, "NoAwarenessPoint");
    }

    private void LogProjectileHitAwarenessBrain(string eventName, ProjectileHitAwarenessContext context, Vector3 point, string reason)
    {
        string message =
            $"[EnemyBrainBT25D] Projectile hit awareness\n" +
            $"Enemy: {name}\n" +
            $"Event: {eventName}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"Reason: {reason}\n" +
            $"Point: {FormatVector3ForLog(point)}\n" +
            $"TargetVisible: {(perception != null && perception.IsTargetVisible)}\n" +
            $"HasLineOfSight: {(perception != null && perception.HasLineOfSight)}\n" +
            $"IsJumpTraversalActive: {(character != null && character.IsJumpTraversalActive)}\n" +
            $"AttackerRoot: {(context.AttackerRoot != null ? context.AttackerRoot.name : "None")}\n" +
            $"HasAttackerPosition: {context.HasAttackerPosition}\n" +
            $"AttackerPosition: {FormatVector3ForLog(context.AttackerPosition)}\n" +
            $"HasProjectileSpawnPosition: {context.HasProjectileSpawnPosition}\n" +
            $"ProjectileSpawnPosition: {FormatVector3ForLog(context.ProjectileSpawnPosition)}\n" +
            $"HasHitPosition: {context.HasHitPosition}\n" +
            $"HitPosition: {FormatVector3ForLog(context.HitPosition)}\n" +
            $"HasHitDirection: {context.HasHitDirection}\n" +
            $"HitDirection: {FormatVector3ForLog(context.HitDirection)}";

        Debug.Log(message, this);
        WriteEnemyDebugLogToFile("ProjectileHitAwareness", message);
    }

    private void EndSearchRecovery(bool clearPerceptionLastKnown)
    {
        searchRecoveryActive = false;
        activeSearchRecoveryTarget = Vector3.zero;
        LogSearchFacingHintReset("ExitSearchDynamic", runtimePoints.Count, runtimePointMode);
        ClearActiveSearchRecoveryFacingHint();
        observedSearchLastKnownVersion = -1;
        observedSearchLastKnownPosition = Vector3.zero;
        searchRecoveryCommitUntilTime = float.NegativeInfinity;
        visibleReacquirePending = false;
        visibleReacquireConfirmStartTime = float.NegativeInfinity;
        searchRecoveryPending = false;
        searchRecoveryRetryTime = float.NegativeInfinity;
        ResetIgnoredSearchRetargetLogState();

        if (clearPerceptionLastKnown && perception != null)
            perception.ClearLastKnownPosition();
    }

    private void CaptureActiveSearchRecoveryFacingHintFromPerception()
    {
        activeSearchRecoveryFacingApplied = false;
        activeSearchRecoveryFacingAppliedVersion = -1;

        if (perception != null && perception.HasLastKnownFacingHint)
        {
            activeSearchRecoveryHasFacingHint = true;
            activeSearchRecoveryFacingSign = perception.LastKnownFacingSign >= 0 ? 1 : -1;
            activeSearchRecoveryFacingMode = string.IsNullOrEmpty(perception.LastKnownFacingMode) ? "Unknown" : perception.LastKnownFacingMode;
            activeSearchRecoveryFacingSource = string.IsNullOrEmpty(perception.LastKnownFacingSource) ? "Unknown" : perception.LastKnownFacingSource;
            return;
        }

        ClearActiveSearchRecoveryFacingHint();
    }

    private void ClearActiveSearchRecoveryFacingHint()
    {
        activeSearchRecoveryHasFacingHint = false;
        activeSearchRecoveryFacingSign = 1;
        activeSearchRecoveryFacingMode = "None";
        activeSearchRecoveryFacingSource = "None";
        activeSearchRecoveryFacingApplied = false;
        activeSearchRecoveryFacingAppliedVersion = -1;
    }

    private void UpdateVisibleReacquireState()
    {
        if (!searchRecoveryActive || perception == null || perception.CurrentTarget == null)
        {
            visibleReacquirePending = false;
            visibleReacquireConfirmStartTime = float.NegativeInfinity;
            return;
        }

        if (!perception.IsTargetVisible)
        {
            visibleReacquirePending = false;
            visibleReacquireConfirmStartTime = float.NegativeInfinity;
            return;
        }

        if (!visibleReacquirePending)
        {
            visibleReacquirePending = true;
            visibleReacquireConfirmStartTime = Time.time;
            return;
        }

        if (Time.time < visibleReacquireConfirmStartTime + visibleReacquireConfirmDuration)
            return;

        EndSearchRecovery(false);
        hasDynamicPatrolAnchor = false;
        if (blackboard != null)
            blackboard.MarkPlayerDetectedForever();
    }

    private bool IsSearchRecoveryComplete()
    {
        if (!searchRecoveryActive)
            return false;

        Vector2 current = new Vector2(transform.position.x, transform.position.y);
        Vector2 target = new Vector2(activeSearchRecoveryTarget.x, activeSearchRecoveryTarget.y);
        return Vector2.Distance(current, target) <= searchRecoveryCompleteRadius;
    }

    private EnemyAction25D GetDisabledAction()
    {
        if (character != null && character.IsJumpTraversalActive)
            return EnemyAction25D.UseJumpLink;
        if (stun != null && stun.IsStunned)
            return EnemyAction25D.Stunned;
        if (knockback != null && knockback.IsRecovering)
            return EnemyAction25D.Recover;
        return EnemyAction25D.Disabled;
    }

    private bool ShouldUseFixedPatrol()
    {
        bool hasEverDetected = blackboard != null && blackboard.HasEverDetectedPlayer;
        return useFixedPatrolOnStart && !hasEverDetected && HasEnabledFixedPatrolPoints();
    }

    private bool HasEnabledFixedPatrolPoints()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return false;

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            Transform point = patrolPoints[i];
            if (point == null)
                continue;

            EnemyPatrolPoint25D meta = point.GetComponent<EnemyPatrolPoint25D>();
            if (meta == null || meta.EnabledForStartPatrol)
                return true;
        }

        return false;
    }

    private NodeStatus RunDeadBranch()
    {
        SetState(BrainState.Disabled);
        currentAction = EnemyAction25D.Disabled;
        ReleaseSelectedCover();
        ClearRuntimePoints();
        CancelCombatJumpLinkApproach();
        if (grenadeThrower != null)
        {
            grenadeThrower.CancelPrepare();
            grenadeThrower.CancelPostGrenadeRetreat();
        }
        if (closeRangeRepel != null)
            closeRangeRepel.CancelRepel();
        if (character != null)
        {
            character.ClearManualFacingOverride();
            character.StopMovement();
        }
        return NodeStatus.Running;
    }

    private NodeStatus RunDisabledBranch()
    {
        SetState(BrainState.Disabled);
        currentAction = GetDisabledAction();
        ReleaseSelectedCover();
        CancelCombatJumpLinkApproach();
        if (grenadeThrower != null)
        {
            grenadeThrower.CancelPrepare();
            grenadeThrower.CancelPostGrenadeRetreat();
        }
        if (closeRangeRepel != null)
            closeRangeRepel.CancelRepel();
        if (character != null)
        {
            character.ClearManualFacingOverride();
            character.StopMovement();
        }
        return NodeStatus.Running;
    }

    private NodeStatus RunCombatBranch()
    {
        if (!wasInCombatLastFrame)
            combatEnterTime = Time.time;

        wasInCombatLastFrame = true;
        SetState(BrainState.Combat);
        ClearRuntimePoints();

        if (character == null)
            return NodeStatus.Failure;

        if (grenadeThrower != null && grenadeThrower.IsInPostGrenadeRetreat)
        {
            grenadeThrower.TickPostGrenadeRetreat();
            currentAction = EnemyAction25D.PostGrenadeRetreat;
            if (!grenadeThrower.IsInPostGrenadeRetreat)
            {
                character.ClearManualFacingOverride();
                character.StopMovement();
                return NodeStatus.Running;
            }

            int retreatSign = grenadeThrower.PostGrenadeRetreatDirectionSign != 0 ? grenadeThrower.PostGrenadeRetreatDirectionSign : -character.FacingSign;
            character.SetManualFacingOverride(true, retreatSign);
            character.SetMoveInput(Mathf.Clamp(retreatSign * grenadeThrower.PostGrenadeRetreatMoveScale, -1f, 1f));
            return NodeStatus.Running;
        }

        if (grenadeThrower != null && grenadeThrower.IsInPostThrowRecovery)
        {
            currentAction = EnemyAction25D.Recover;
            character.ClearManualFacingOverride();
            character.StopMovement();
            return NodeStatus.Running;
        }

        if (grenadeThrower != null && grenadeThrower.IsPreparingThrow)
        {
            currentAction = EnemyAction25D.PrepareGrenade;
            if (grenadeThrower.StopMovementDuringWindup)
                character.StopMovement();

            if (grenadeThrower.TickPrepareAndTryThrow())
                currentAction = EnemyAction25D.ThrowGrenade;

            return NodeStatus.Running;
        }

        if (perception == null)
            return NodeStatus.Failure;

        Vector3 aimPosition = perception.GetAimPosition();
        Vector3 navigationPosition = useCombatNavigationPosition ? perception.GetCombatNavigationPosition() : aimPosition;
        Vector3 traversalReference = GetTraversalReferencePosition();
        float distanceToTarget = Vector3.Distance(transform.position, aimPosition);
        float distanceToNavigationTarget = Vector3.Distance(traversalReference, navigationPosition);
        int targetSign = GetTargetFacingSign(aimPosition);
        Transform currentTarget = perception.CurrentTarget;

        if (isApproachingCombatJumpLink)
        {
            currentAction = EnemyAction25D.CombatJumpLinkApproach;
            if (TickCombatJumpLinkApproach(currentTarget, navigationPosition))
                return NodeStatus.Running;
        }

        if (closeRangeRepel != null && closeRangeRepel.IsPreparingRepel)
        {
            closeRangeRepel.TickRepel();
            currentAction = closeRangeRepel.IsRepelActive ? EnemyAction25D.RepelCloseTarget : EnemyAction25D.PrepareRepel;
            character.SetManualFacingOverride(true, targetSign);
            character.StopMovement();
            return NodeStatus.Running;
        }

        if (closeRangeRepel != null && closeRangeRepel.IsRepelActive)
        {
            closeRangeRepel.TickRepel();
            currentAction = closeRangeRepel.IsRepelActive ? EnemyAction25D.RepelCloseTarget : EnemyAction25D.BackpedalFire;
            character.SetManualFacingOverride(true, targetSign);
            character.StopMovement();
            return NodeStatus.Running;
        }

        bool grenadeExclusive = grenadeThrower != null && grenadeThrower.IsInAnyGrenadeExclusiveState;
        if (allowCloseRepel && closeRangeRepel != null && !grenadeExclusive)
        {
            if (closeRangeRepel.TryBeginRepel(distanceToTarget, perception.IsTargetVisible, perception.HasLineOfSight, targetSign))
            {
                currentAction = EnemyAction25D.PrepareRepel;
                character.SetManualFacingOverride(true, targetSign);
                character.StopMovement();
                return NodeStatus.Running;
            }
        }

        if (allowGrenadeAttack && grenadeThrower != null && (closeRangeRepel == null || !closeRangeRepel.IsInRepelFlow))
        {
            if (grenadeThrower.TryBeginPrepare(CombatTimeElapsed, distanceToTarget, perception.IsTargetVisible, perception.HasLineOfSight))
            {
                currentAction = EnemyAction25D.PrepareGrenade;
                if (grenadeThrower.StopMovementDuringWindup)
                    character.StopMovement();
                return NodeStatus.Running;
            }
        }

        if (ShouldTryCombatJumpLinkPursuit(currentTarget, navigationPosition, distanceToNavigationTarget))
        {
            if (TryFindBestCombatJumpLinkToTarget(navigationPosition, out EnemyJumpLink25D bestCombatJumpLink, out Vector3 traversalStart, out Vector3 traversalEnd))
            {
                BeginCombatJumpLinkApproach(bestCombatJumpLink, navigationPosition, traversalStart, traversalEnd);
                currentAction = EnemyAction25D.CombatJumpLinkApproach;
                LogCombatPlatformNavigation("CombatJumpLinkSelected", aimPosition, navigationPosition, traversalReference, false, bestCombatJumpLink, traversalStart, traversalEnd, "CrossPlatformCombatMovement");
                return NodeStatus.Running;
            }
        }

        if (allowBackpedalFire && perception.IsTargetVisible && distanceToTarget <= backpedalStartRange)
            return RunBackpedalFire(aimPosition);

        if (useCover && TryMaintainOrAcquireCover())
            return RunCoverCombat(aimPosition);

        ReleaseSelectedCover();
        return RunOpenCombat(aimPosition, navigationPosition);
    }

    private NodeStatus RunOpenCombat(Vector3 aimPosition, Vector3 navigationPosition)
    {
        if (character != null)
            character.ClearManualFacingOverride();

        Vector3 traversalReference = GetTraversalReferencePosition();
        float distanceToNavigationTarget = Vector3.Distance(traversalReference, navigationPosition);
        bool directMovementViable = IsDirectCombatMovementViableToNavigationPosition(navigationPosition, distanceToNavigationTarget);
        bool primaryBlocked = grenadeThrower != null && grenadeThrower.IsGrenadeActionBlockingPrimaryFire;

        if (!directMovementViable && holdPlatformWhenCombatJumpLinkUnavailable)
        {
            character.StopMovement();
            currentAction = EnemyAction25D.HoldPlatformNoJumpLink;

            if (!primaryBlocked && shooter != null)
                shooter.TryFirePrimaryAtPerceivedTarget();

            LogCombatPlatformNavigation("CombatHoldPlatformNoJumpLink", aimPosition, navigationPosition, traversalReference, false, null, Vector3.zero, Vector3.zero, "NoViableDirectMovementOrJumpLink");
            return NodeStatus.Running;
        }

        float deltaX = navigationPosition.x - transform.position.x;
        float absDeltaX = Mathf.Abs(deltaX);

        float moveX = 0f;
        if (absDeltaX > desiredCombatMaxRange)
            moveX = Mathf.Sign(deltaX);
        else if (absDeltaX < desiredCombatMinRange)
            moveX = -Mathf.Sign(deltaX);

        if (Mathf.Abs(moveX) <= combatMoveDeadZone)
            moveX = 0f;

        character.SetMoveInput(moveX);

        if (moveX != 0f)
        {
            currentAction = EnemyAction25D.OpenCombatMove;
            LogCombatPlatformNavigation("CombatDirectMovementAllowed", aimPosition, navigationPosition, traversalReference, true, null, Vector3.zero, Vector3.zero, "OpenCombatMove");
            return NodeStatus.Running;
        }

        currentAction = EnemyAction25D.OpenCombatHold;
        if (!primaryBlocked && shooter != null && shooter.TryFirePrimaryAtPerceivedTarget())
            currentAction = EnemyAction25D.FirePrimary;

        return NodeStatus.Running;
    }

    private NodeStatus RunCoverCombat(Vector3 aimPosition)
    {
        if (character != null)
            character.ClearManualFacingOverride();

        if (selectedCover == null)
            return NodeStatus.Failure;

        Transform standPoint = selectedCover.StandPoint;
        Transform peekPoint = selectedCover.PeekPoint;
        if (standPoint == null)
        {
            ReleaseSelectedCover();
            return NodeStatus.Failure;
        }

        if (!IsAtPoint(standPoint.position, coverStandReachDistance))
        {
            currentAction = EnemyAction25D.TakeCover;
            MoveTowardsX(standPoint.position.x);
            return NodeStatus.Running;
        }

        UpdatePeekState();
        if (!isPeeking)
        {
            currentAction = EnemyAction25D.TakeCover;
            character.StopMovement();
            return NodeStatus.Running;
        }

        Vector3 peekPosition = peekPoint != null ? peekPoint.position : standPoint.position;
        if (!IsAtPoint(peekPosition, coverPeekReachDistance))
        {
            currentAction = EnemyAction25D.Peek;
            MoveTowardsX(peekPosition.x);
            return NodeStatus.Running;
        }

        currentAction = EnemyAction25D.Peek;
        character.StopMovement();
        bool primaryBlocked = grenadeThrower != null && grenadeThrower.IsGrenadeActionBlockingPrimaryFire;
        if (!primaryBlocked && shooter != null && shooter.TryFirePrimaryAtPerceivedTarget())
            currentAction = EnemyAction25D.FirePrimary;
        return NodeStatus.Running;
    }

    private void RefreshSearchRecoveryTargetFromLastKnownIfNeeded()
    {
        if (!retargetSearchRecoveryOnLastKnownUpdate)
            return;

        if (!searchRecoveryActive)
            return;

        if (perception == null || !perception.HasLastKnownPosition)
            return;

        int currentVersion = perception.LastKnownPositionVersion;
        if (currentVersion == observedSearchLastKnownVersion)
            return;

        if (!perception.IsLastKnownUpdateSearchRelevant)
        {
            int previousIgnoredVersion = observedSearchLastKnownVersion;
            Vector3 ignoredTarget = perception.LastKnownTargetPosition;
            observedSearchLastKnownVersion = currentVersion;
            TryUpdateCurrentSearchPointFacingSnapshotFromIgnoredRetarget(perception.LastKnownUpdateReason, previousIgnoredVersion, currentVersion);
            LogIgnoredSearchLastKnownRetarget(ignoredTarget, previousIgnoredVersion, currentVersion, perception.LastKnownUpdateReason);
            return;
        }

        Vector3 previousTarget = activeSearchRecoveryTarget;
        int previousVersion = observedSearchLastKnownVersion;
        Vector3 newTarget = perception.LastKnownTargetPosition;

        observedSearchLastKnownVersion = currentVersion;
        observedSearchLastKnownPosition = newTarget;

        if (Vector3.Distance(newTarget, previousTarget) <= searchRetargetPositionTolerance)
        {
            LogSearchFacingHintReset("SearchRetargetPositionUnchanged", runtimePoints.Count, runtimePointMode);
            CaptureActiveSearchRecoveryFacingHintFromPerception();
            return;
        }

        activeSearchRecoveryTarget = newTarget;
        LogSearchFacingHintReset("SearchRetarget", runtimePoints.Count, runtimePointMode);
        if (clearSearchFacingLockOnSearchRetarget)
            ClearSearchFacingLockFromBrain("SearchRetarget");
        CaptureActiveSearchRecoveryFacingHintFromPerception();
        searchRecoveryPending = false;
        searchRecoveryRetryTime = float.NegativeInfinity;
        ForceNextSearchAnchorProjectionLog("SearchRetarget");
        ClearRuntimePoints("Search target retargeted from LastKnown update");
        LogSearchLastKnownRetarget(previousTarget, newTarget, previousVersion, currentVersion);
    }

    private void LogSearchLastKnownRetarget(Vector3 oldTarget, Vector3 newTarget, int oldVersion, int newVersion)
    {
        if (!logSearchLastKnownRetargets)
            return;

        string message =
            $"[EnemyBrainBT25D] SearchDynamic retargeted from updated LastKnown\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"OldTarget: {FormatVector3ForLog(oldTarget)}\n" +
            $"NewTarget: {FormatVector3ForLog(newTarget)}\n" +
            $"Reason: {(perception != null ? perception.LastKnownUpdateReason : "None")}\n" +
            $"OldVersion: {oldVersion}\n" +
            $"NewVersion: {newVersion}";

        Debug.Log(message, this);

        if (writeSearchLastKnownRetargetsToFile)
            WriteEnemyDebugLogToFile("SearchRetarget", message);
    }

    private void LogIgnoredSearchLastKnownRetarget(Vector3 ignoredTarget, int oldVersion, int newVersion, string reason)
    {
        if (!ShouldLogIgnoredSearchLastKnownRetarget(reason))
            return;

        RuntimeSearchPoint currentPoint;
        bool hasCurrentPoint = TryGetCurrentRuntimePoint(out currentPoint);
        bool ignoredHasFacingHint = perception != null && perception.HasLastKnownFacingHint;
        int ignoredFacingSign = ignoredHasFacingHint && perception != null ? perception.LastKnownFacingSign : 0;
        string ignoredFacingMode = ignoredHasFacingHint && perception != null ? perception.LastKnownFacingMode : "None";
        string ignoredFacingSource = ignoredHasFacingHint && perception != null ? perception.LastKnownFacingSource : "None";
        bool differsFromCurrentPoint = hasCurrentPoint && currentPoint.HasFacingHint && ignoredHasFacingHint && currentPoint.FacingHintSign != (ignoredFacingSign >= 0 ? 1 : -1);

        string message =
            $"[EnemyBrainBT25D] SearchDynamic ignored LastKnown retarget\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"Reason: {reason}\n" +
            $"CurrentSearchTarget: {FormatVector3ForLog(activeSearchRecoveryTarget)}\n" +
            $"IgnoredLastKnown: {FormatVector3ForLog(ignoredTarget)}\n" +
            $"OldVersion: {oldVersion}\n" +
            $"NewVersion: {newVersion}" +
            (logSearchFacingHintRetargetIgnoredDetails ?
                $"\nCurrentRuntimePointAvailable: {hasCurrentPoint}" +
                $"\nCurrentRuntimeIndex: {runtimePointIndex}" +
                $"\nCurrentSearchTargetHasFacingHint: {(hasCurrentPoint && currentPoint.HasFacingHint)}" +
                $"\nCurrentSearchTargetFacingSign: {(hasCurrentPoint && currentPoint.HasFacingHint ? FormatFacingSignForLog(currentPoint.FacingHintSign) : "None")}" +
                $"\nCurrentSearchTargetFacingMode: {(hasCurrentPoint ? currentPoint.FacingHintMode : "None")}" +
                $"\nCurrentSearchTargetFacingSource: {(hasCurrentPoint ? currentPoint.FacingHintSource : "None")}" +
                $"\nCurrentSearchTargetLastKnownVersionSnapshot: {(hasCurrentPoint ? currentPoint.LastKnownVersionSnapshot : -1)}" +
                $"\nIgnoredLastKnownHasFacingHint: {ignoredHasFacingHint}" +
                $"\nIgnoredLastKnownFacingSign: {(ignoredHasFacingHint ? FormatFacingSignForLog(ignoredFacingSign) : "None")}" +
                $"\nIgnoredLastKnownFacingMode: {ignoredFacingMode}" +
                $"\nIgnoredLastKnownFacingSource: {ignoredFacingSource}" +
                $"\nDidIgnoredFacingDifferFromCurrentPoint: {differsFromCurrentPoint}" +
                $"\nActiveSearchRecoveryFacingApplied: {activeSearchRecoveryFacingApplied}" +
                $"\nSearchRecoveryFacingAppliedVersion: {activeSearchRecoveryFacingAppliedVersion}"
                : string.Empty);

        Debug.Log(message, this);
        RegisterIgnoredSearchRetargetLog(reason);

        if (writeSearchLastKnownRetargetsToFile)
            WriteEnemyDebugLogToFile("SearchRetargetIgnored", message);
    }

    private bool TryUpdateCurrentSearchPointFacingSnapshotFromIgnoredRetarget(string reason, int oldVersion, int newVersion)
    {
        if (!updateCurrentSearchPointFacingOnVisibleTargetIgnoredRetarget)
            return false;

        if (!string.Equals(reason, "VisibleTarget", StringComparison.Ordinal))
            return false;

        if (perception == null || !perception.HasLastKnownFacingHint)
            return false;

        int newFacingSign = perception.LastKnownFacingSign >= 0 ? 1 : -1;
        if (newFacingSign == 0)
            return false;

        if (runtimePoints.Count <= 0)
            return false;

        int index = Mathf.Clamp(runtimePointIndex, 0, runtimePoints.Count - 1);
        RuntimeSearchPoint oldPoint = runtimePoints[index];
        if (!oldPoint.IsLastKnownInvestigationPoint)
            return false;

        int oldPointVersion = oldPoint.LastKnownVersionSnapshot;
        int oldFacingSign = oldPoint.HasFacingHint ? oldPoint.FacingHintSign : 0;
        string oldFacingMode = oldPoint.FacingHintMode;
        string oldFacingSource = oldPoint.FacingHintSource;

        string newFacingMode = perception.LastKnownFacingMode;
        string newFacingSource = "IgnoredRetarget:" + perception.LastKnownFacingSource;
        Vector3 newPositionSnapshot = perception.HasLastKnownPosition ? perception.LastKnownTargetPosition : oldPoint.LastKnownPositionSnapshot;

        RuntimeSearchPoint updatedPoint = WithUpdatedSearchFacingSnapshot(
            oldPoint,
            true,
            newFacingSign,
            newFacingMode,
            newFacingSource,
            newVersion,
            newPositionSnapshot,
            true,
            newFacingSign,
            newFacingMode,
            newFacingSource);

        runtimePoints[index] = updatedPoint;
        LogSearchFacingSnapshotUpdated(
            index,
            oldPoint,
            updatedPoint,
            oldVersion,
            newVersion,
            oldPointVersion,
            oldFacingSign,
            oldFacingMode,
            oldFacingSource,
            newFacingSign,
            newFacingMode,
            newFacingSource,
            reason);

        return true;
    }

    private RuntimeSearchPoint WithUpdatedSearchFacingSnapshot(
        RuntimeSearchPoint point,
        bool hasFacingHint,
        int facingSign,
        string facingMode,
        string facingSource,
        int lastKnownVersionSnapshot,
        Vector3 lastKnownPositionSnapshot,
        bool hasLastKnownFacingHintSnapshot,
        int lastKnownFacingSignSnapshot,
        string lastKnownFacingModeSnapshot,
        string lastKnownFacingSourceSnapshot)
    {
        if (point.RequiresJumpLink && point.HasExplicitJumpTraversal)
        {
            return new RuntimeSearchPoint(
                point.WorldPosition,
                point.WaitDuration,
                point.JumpLink,
                point.Score,
                point.JumpTraversalStart,
                point.JumpTraversalEnd,
                point.IsLastKnownInvestigationPoint,
                hasFacingHint,
                facingSign,
                facingMode,
                facingSource,
                lastKnownVersionSnapshot,
                lastKnownPositionSnapshot,
                hasLastKnownFacingHintSnapshot,
                lastKnownFacingSignSnapshot,
                lastKnownFacingModeSnapshot,
                lastKnownFacingSourceSnapshot,
                point.HasAdjustedLastKnownSafePosition,
                point.HasAdjustedLastKnownSafePosition ? lastKnownPositionSnapshot : point.OriginalLastKnownPositionSnapshot,
                point.AdjustedLastKnownSafePosition,
                point.LastKnownSafeAdjustmentReason,
                point.LastKnownSafeAdjustmentDistance);
        }

        return new RuntimeSearchPoint(
            point.WorldPosition,
            point.WaitDuration,
            point.RequiresJumpLink,
            point.JumpLink,
            point.Score,
            point.IsLastKnownInvestigationPoint,
            hasFacingHint,
            facingSign,
            facingMode,
            facingSource,
            lastKnownVersionSnapshot,
            lastKnownPositionSnapshot,
            hasLastKnownFacingHintSnapshot,
            lastKnownFacingSignSnapshot,
            lastKnownFacingModeSnapshot,
            lastKnownFacingSourceSnapshot,
            point.HasAdjustedLastKnownSafePosition,
            point.HasAdjustedLastKnownSafePosition ? lastKnownPositionSnapshot : point.OriginalLastKnownPositionSnapshot,
            point.AdjustedLastKnownSafePosition,
            point.LastKnownSafeAdjustmentReason,
            point.LastKnownSafeAdjustmentDistance);
    }

    private void LogSearchFacingSnapshotUpdated(
        int runtimeIndex,
        RuntimeSearchPoint oldPoint,
        RuntimeSearchPoint updatedPoint,
        int oldObservedVersion,
        int newPerceptionVersion,
        int oldPointVersion,
        int oldFacingSign,
        string oldFacingMode,
        string oldFacingSource,
        int newFacingSign,
        string newFacingMode,
        string newFacingSource,
        string reason)
    {
        if (!logSearchFacingVersionedApply)
            return;

        string key = $"SnapshotUpdated|{runtimeIndex}|{oldPointVersion}|{newPerceptionVersion}|{oldFacingSign}|{newFacingSign}|{FormatVector3ForLog(updatedPoint.WorldPosition)}";
        if (!ShouldLogSearchFacingHintDiagnostic(key))
            return;

        string message =
            $"[EnemyBrainBT25D] Updated current search point facing snapshot from ignored VisibleTarget retarget\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"RuntimeIndex: {runtimeIndex}\n" +
            $"Point: {FormatVector3ForLog(updatedPoint.WorldPosition)}\n" +
            $"HasAdjustedLastKnownSafePosition: {updatedPoint.HasAdjustedLastKnownSafePosition}\n" +
            $"OriginalLastKnownPositionSnapshot: {FormatVector3ForLog(updatedPoint.OriginalLastKnownPositionSnapshot)}\n" +
            $"AdjustedLastKnownSafePosition: {FormatVector3ForLog(updatedPoint.AdjustedLastKnownSafePosition)}\n" +
            $"LastKnownSafeAdjustmentReason: {updatedPoint.LastKnownSafeAdjustmentReason}\n" +
            $"LastKnownSafeAdjustmentDistance: {updatedPoint.LastKnownSafeAdjustmentDistance:F2}\n" +
            $"OldObservedVersion: {oldObservedVersion}\n" +
            $"NewPerceptionVersion: {newPerceptionVersion}\n" +
            $"OldPointVersion: {oldPointVersion}\n" +
            $"NewPointVersion: {updatedPoint.LastKnownVersionSnapshot}\n" +
            $"OldFacingSign: {(oldPoint.HasFacingHint ? FormatFacingSignForLog(oldFacingSign) : "None")}\n" +
            $"NewFacingSign: {FormatFacingSignForLog(newFacingSign)}\n" +
            $"OldFacingMode: {oldFacingMode}\n" +
            $"NewFacingMode: {newFacingMode}\n" +
            $"OldFacingSource: {oldFacingSource}\n" +
            $"NewFacingSource: {newFacingSource}\n" +
            $"PositionChanged: False\n" +
            $"SearchTargetChanged: False\n" +
            $"Reason: {reason}IgnoredRetarget";

        Debug.Log(message, this);
        if (writeSearchFacingHintDiagnosticsToFile)
            WriteEnemyDebugLogToFile("SearchFacingSnapshotUpdated", message);
    }

    private bool ShouldLogIgnoredSearchLastKnownRetarget(string reason)
    {
        if (!logIgnoredSearchLastKnownRetargets && !(logSearchFacingHintDiagnostics && logSearchFacingHintRetargetIgnoredDetails))
            return false;

        bool isVisibleTargetReason = string.Equals(reason, "VisibleTarget", StringComparison.Ordinal);
        if (isVisibleTargetReason && logOnlyFirstIgnoredVisibleTargetRetargetPerSearch && hasLoggedIgnoredVisibleTargetRetargetThisSearch)
            return false;

        if (ignoredSearchRetargetLogCooldown > 0f && Time.time < lastIgnoredSearchRetargetLogTime + ignoredSearchRetargetLogCooldown)
            return false;

        return true;
    }

    private void RegisterIgnoredSearchRetargetLog(string reason)
    {
        lastIgnoredSearchRetargetLogTime = Time.time;

        if (string.Equals(reason, "VisibleTarget", StringComparison.Ordinal))
            hasLoggedIgnoredVisibleTargetRetargetThisSearch = true;
    }

    private void ResetIgnoredSearchRetargetLogState()
    {
        hasLoggedIgnoredVisibleTargetRetargetThisSearch = false;
        lastIgnoredSearchRetargetLogTime = float.NegativeInfinity;
    }

    private NodeStatus RunDynamicSearchBranch()
    {
        SetBranchDebugFlags(dynamicSearch: true);
        SetState(BrainState.SearchDynamic);
        ReleaseSelectedCover();
        if (grenadeThrower != null)
        {
            grenadeThrower.CancelPrepare();
            grenadeThrower.CancelPostGrenadeRetreat();
        }
        if (closeRangeRepel != null)
            closeRangeRepel.CancelRepel();
        if (character != null)
            character.ClearManualFacingOverride();

        if (character == null || perception == null)
            return NodeStatus.Failure;

        if (!searchRecoveryActive && perception.HasLastKnownPosition)
            BeginSearchRecovery(perception.LastKnownTargetPosition);

        RefreshSearchRecoveryTargetFromLastKnownIfNeeded();

        Vector3 recoveryTarget = searchRecoveryActive ? activeSearchRecoveryTarget : perception.LastKnownTargetPosition;
        Vector3 anchor = ResolveSearchAnchor(recoveryTarget);
        if (searchRecoveryPending && Time.time < searchRecoveryRetryTime)
        {
            currentAction = EnemyAction25D.WaitAtPoint;
            character.StopMovement();
            int focusSign = GetPreferredSearchSideSign(anchor);
            if (Mathf.Abs(anchor.x - transform.position.x) > searchFacingDeadZone)
                character.ForceFacingSign(focusSign);
            return NodeStatus.Running;
        }

        if (allowJumpLinksInDynamicSearch && NeedJumpTraversalForTarget(recoveryTarget))
        {
            EnemyJumpLink25D recoveryJumpLink = FindBestRecoveryJumpLinkToPoint(recoveryTarget, out Vector3 recoveryTraversalStart, out Vector3 recoveryTraversalEnd);
            if (recoveryJumpLink != null &&
                TickJumpLinkTraversal(recoveryJumpLink, recoveryTraversalStart, recoveryTraversalEnd, dynamicSearchArrivalDistance, false))
            {
                currentAction = EnemyAction25D.UseJumpLink;
                return NodeStatus.Running;
            }
        }

        EnsureRuntimePoints(anchor, RuntimePointMode.Search, allowJumpLinksInDynamicSearch, dynamicSearchWaitDuration);
        if (runtimePoints.Count == 0)
        {
            DynamicSearchHasValidPointsInternal(false);
            ScheduleSearchRecoveryRetry(anchor, false);
            currentAction = EnemyAction25D.WaitAtPoint;
            character.StopMovement();
            int focusSign = GetPreferredSearchSideSign(anchor);
            if (Mathf.Abs(anchor.x - transform.position.x) > searchFacingDeadZone)
                character.ForceFacingSign(focusSign);
            return NodeStatus.Running;
        }

        if (TickRuntimePointList(false, dynamicSearchArrivalDistance))
        {
            FinishDynamicSearch(anchor);
            return NodeStatus.Running;
        }

        return NodeStatus.Running;
    }

    private void FinishDynamicSearch(Vector3 anchor)
    {
        if (searchRecoveryActive)
        {
            bool complete = IsSearchRecoveryComplete() || (perception != null && !perception.HasLastKnownPosition);
            if (!complete)
            {
                searchRecoveryPending = true;
                searchRecoveryRetryTime = Time.time + searchRecoveryRetryDelay;
                ClearRuntimePoints();
                if (character != null)
                    character.StopMovement();
                return;
            }

            EndSearchRecovery(true);
        }
        else if (perception != null)
        {
            perception.ClearLastKnownPosition();
        }

        dynamicPatrolAnchor = ResolveDynamicPatrolAnchor(anchor);
        hasDynamicPatrolAnchor = true;
        searchRecoveryPending = false;
        searchRecoveryRetryTime = float.NegativeInfinity;
        ClearRuntimePoints();
        if (character != null)
            character.StopMovement();
    }

    private NodeStatus RunPatrolOrIdleBranch()
    {
        ReleaseSelectedCover();
        if (grenadeThrower != null)
        {
            grenadeThrower.CancelPrepare();
            grenadeThrower.CancelPostGrenadeRetreat();
        }
        if (closeRangeRepel != null)
            closeRangeRepel.CancelRepel();
        if (character != null)
            character.ClearManualFacingOverride();

        if (ShouldUseFixedPatrol())
            return RunFixedPatrolBranch();

        return RunDynamicPatrolBranch();
    }

    private NodeStatus RunFixedPatrolBranch()
    {
        if (character == null || patrolPoints == null || patrolPoints.Length == 0)
        {
            SetState(BrainState.Idle);
            currentAction = EnemyAction25D.None;
            if (character != null)
                character.StopMovement();
            return NodeStatus.Running;
        }

        SetBranchDebugFlags(fixedPatrol: true);
        SetState(BrainState.PatrolFixed);
        Transform point = GetCurrentFixedPatrolTransform();
        if (point == null)
        {
            AdvanceFixedPatrolIndex();
            return NodeStatus.Running;
        }

        EnemyPatrolPoint25D patrolMeta = point.GetComponent<EnemyPatrolPoint25D>();
        Vector3 pointPosition = point.position;

        if (allowJumpLinksInFixedPatrol)
        {
            EnemyJumpLink25D preferredLink = patrolMeta != null ? patrolMeta.PreferredJumpLink : null;
            if (preferredLink != null && NeedJumpTraversalForTarget(pointPosition))
            {
                if (TickJumpLinkTraversal(preferredLink, pointPosition, patrolPointReachDistance))
                {
                    currentAction = EnemyAction25D.UseJumpLink;
                    return NodeStatus.Running;
                }
            }
        }

        float deltaX = pointPosition.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= patrolPointReachDistance)
        {
            currentAction = EnemyAction25D.WaitAtPoint;
            character.StopMovement();
            if (patrolMeta != null && patrolMeta.FacingOverride != 0)
                character.ForceFacingSign(patrolMeta.FacingOverride);
            else if (Mathf.Abs(deltaX) > 0.01f)
                character.ForceFacingSign(deltaX >= 0f ? 1 : -1);

            float waitDuration = patrolMeta != null ? patrolMeta.WaitDuration : patrolWaitDuration;
            bool stopAndLook = patrolMeta == null || patrolMeta.StopAndLook;
            if (!stopAndLook)
            {
                AdvanceFixedPatrolIndex();
                patrolWaitEndTime = 0f;
                return NodeStatus.Running;
            }

            if (patrolWaitEndTime <= 0f)
                patrolWaitEndTime = Time.time + waitDuration;
            else if (Time.time >= patrolWaitEndTime)
            {
                patrolWaitEndTime = 0f;
                AdvanceFixedPatrolIndex();
            }
            return NodeStatus.Running;
        }

        patrolWaitEndTime = 0f;
        currentAction = EnemyAction25D.MoveToFixedPoint;
        float moveX = Mathf.Abs(deltaX) <= patrolMoveDeadZone ? 0f : Mathf.Sign(deltaX);
        character.SetMoveInput(moveX);
        return NodeStatus.Running;
    }

    private NodeStatus RunDynamicPatrolBranch()
    {
        SetBranchDebugFlags(dynamicPatrol: true);
        SetState(BrainState.PatrolDynamic);

        if (character == null)
            return NodeStatus.Failure;

        if (!hasDynamicPatrolAnchor)
        {
            dynamicPatrolAnchor = GetTraversalReferencePosition();
            dynamicPatrolAnchor.z = 0f;
            hasDynamicPatrolAnchor = true;
            ClearRuntimePoints();
        }

        Vector3 anchor = ResolveDynamicPatrolAnchor(dynamicPatrolAnchor);
        dynamicPatrolAnchor = anchor;
        if (searchRecoveryPending && Time.time < searchRecoveryRetryTime)
        {
            currentAction = EnemyAction25D.WaitAtPoint;
            character.StopMovement();
            return NodeStatus.Running;
        }

        EnsureRuntimePoints(anchor, RuntimePointMode.DynamicPatrol, allowJumpLinksInDynamicPatrol, dynamicPatrolWaitDuration);
        if (runtimePoints.Count == 0)
        {
            DynamicPatrolHasValidPointsInternal(false);
            dynamicPatrolAnchor = ResolveDynamicPatrolAnchor(GetTraversalReferencePosition());
            hasDynamicPatrolAnchor = true;
            ScheduleSearchRecoveryRetry(anchor, true);
            currentAction = EnemyAction25D.WaitAtPoint;
            character.StopMovement();
            return NodeStatus.Running;
        }

        TickRuntimePointList(true, dynamicPatrolArrivalDistance);
        return NodeStatus.Running;
    }

    private void EnsureRuntimePoints(Vector3 anchor, RuntimePointMode mode, bool includeJumpLinks, float waitDuration)
    {
        if (mode == RuntimePointMode.DynamicPatrol)
        {
            if (hasRuntimePoints && runtimePointMode == RuntimePointMode.DynamicPatrol && runtimePoints.Count > 0)
                return;

            GenerateDynamicPatrolPoints(anchor, includeJumpLinks, waitDuration);
            return;
        }

        if (mode == RuntimePointMode.Search)
        {
            if (hasRuntimePoints && runtimePointMode == RuntimePointMode.Search && runtimePoints.Count > 0 && Vector3.Distance(runtimePointsAnchor, anchor) <= 0.25f)
                return;

            GenerateSearchRuntimePoints(anchor, includeJumpLinks, waitDuration);
            return;
        }

        GenerateDynamicPatrolPoints(anchor, includeJumpLinks, waitDuration);
    }

    private void GenerateSearchRuntimePoints(Vector3 anchor, bool includeJumpLinks, float waitDuration)
    {
        BeginRuntimePointGeneration(anchor, RuntimePointMode.Search);

        float midDistance = (dynamicPointNearDistance + dynamicPointFarDistance) * 0.5f;
        int preferredSide = GetPreferredSearchSideSign(anchor);
        float[] candidateOffsets = BuildSearchCandidateOffsets(preferredSide, dynamicPointNearDistance, midDistance, dynamicPointFarDistance);

        for (int i = 0; i < candidateOffsets.Length; i++)
        {
            if (CountRuntimeWalkPoints() >= dynamicPointMaxCount)
                break;

            float targetX = anchor.x + candidateOffsets[i];
            if (TryBuildGroundedRuntimePoint(targetX, anchor, waitDuration, false, out RuntimeSearchPoint point))
            {
                bool isLastKnownInvestigationPoint = ShouldMarkRuntimePointAsLastKnownInvestigation(point.WorldPosition, anchor);
                if (isLastKnownInvestigationPoint)
                {
                    point = WithLastKnownFacingHint(point);
                    point = AdjustLastKnownInvestigationPointIfNeeded(point, "SearchDynamicLastKnownCandidate");
                }

                if (!ValidateDynamicWalkPointEdgeClearanceBeforeAdd(point, isLastKnownInvestigationPoint, "SearchSideWalkCandidate"))
                    continue;

                runtimePoints.Add(point);
                LogSearchFacingHintPointBuild(point, anchor, "SearchDynamic candidate", runtimePoints.Count - 1);
            }
        }

        if (includeJumpLinks)
            AppendNearbyJumpLinkedRuntimePoints(anchor, waitDuration);

        if (preventEmptySearchDynamicPoints && runtimePoints.Count == 0)
            RecoverFromEmptySearchDynamicPoints(anchor, waitDuration, "SearchDynamic rebuild");
        else
            ResetSearchEmptyRecoveryCounter();

        FinalizeRuntimePointGeneration(anchor);
    }

    private bool RecoverFromEmptySearchDynamicPoints(Vector3 failedAnchor, float waitDuration, string rebuildReason)
    {
        if (runtimePointMode != RuntimePointMode.Search)
            return false;

        consecutiveEmptySearchRecoveries++;
        ForceNextSearchAnchorProjectionLog("SearchEmptyRecovery");
        Vector3 currentRef = GetTraversalReferencePosition();
        currentRef.z = 0f;
        LogSearchEmptyRecoveryDetected(failedAnchor, currentRef, rebuildReason);

        if (searchEmptyTryRouteJumpLinkFallback &&
            TryAddSearchRouteJumpLinkFallback(currentRef, failedAnchor, waitDuration, out EnemyJumpLink25D routeLink, out Vector3 routeStart, out Vector3 routeEnd))
        {
            runtimePointIndex = 0;
            hasRuntimePoints = true;
            LogSearchEmptyRecoveredWithRouteJumpLink(failedAnchor, currentRef, routeLink, routeStart, routeEnd);
            return true;
        }

        if (searchEmptyFallbackToLocalAroundCurrentRef &&
            TryBuildLocalSearchFallbackAroundCurrentRef(currentRef, waitDuration, out Vector3 localAnchor, out int localPointCount))
        {
            runtimePointIndex = 0;
            hasRuntimePoints = true;
            LogSearchEmptyRecoveredWithLocalFallback(failedAnchor, currentRef, localAnchor, localPointCount, "No route JumpLink found");
            return true;
        }

        if (searchEmptyFallbackToEmergencyWait)
        {
            AddSearchEmptyEmergencyWaitPoint(currentRef);
            runtimePointIndex = 0;
            hasRuntimePoints = true;
            LogSearchEmptyRecoveredWithEmergencyWait(failedAnchor, currentRef);
            return true;
        }

        LogSearchEmptyRecoveryFailed(failedAnchor, currentRef);
        return false;
    }

    private bool TryAddSearchRouteJumpLinkFallback(
        Vector3 currentRef,
        Vector3 failedAnchor,
        float waitDuration,
        out EnemyJumpLink25D bestLink,
        out Vector3 bestTraversalStart,
        out Vector3 bestTraversalEnd)
    {
        bestLink = null;
        bestTraversalStart = Vector3.zero;
        bestTraversalEnd = Vector3.zero;

        EnemyJumpLink25D[] allLinks = FindObjectsByType<EnemyJumpLink25D>(FindObjectsSortMode.None);
        if (allLinks == null || allLinks.Length == 0)
            return false;

        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < allLinks.Length; i++)
        {
            EnemyJumpLink25D link = allLinks[i];
            if (link == null || !link.EnabledLink)
                continue;

            if (IsRecentlyCompletedDynamicJumpLink(link, BrainState.SearchDynamic))
                continue;

            Transform serializedStart = link.StartPoint;
            Transform serializedEnd = link.EndPoint;
            if (serializedStart == null || serializedEnd == null)
                continue;

            EvaluateSearchEmptyRouteJumpLinkCandidate(
                link,
                currentRef,
                failedAnchor,
                serializedStart.position,
                serializedEnd.position,
                ref bestLink,
                ref bestScore,
                ref bestTraversalStart,
                ref bestTraversalEnd);

            if (link.Bidirectional)
            {
                EvaluateSearchEmptyRouteJumpLinkCandidate(
                    link,
                    currentRef,
                    failedAnchor,
                    serializedEnd.position,
                    serializedStart.position,
                    ref bestLink,
                    ref bestScore,
                    ref bestTraversalStart,
                    ref bestTraversalEnd);
            }
        }

        if (bestLink == null)
            return false;

        Vector3 worldPosition = bestTraversalEnd;
        worldPosition.z = 0f;
        float score = Mathf.Abs(worldPosition.x - failedAnchor.x) + Mathf.Abs(worldPosition.y - failedAnchor.y) + Mathf.Max(0f, bestLink.TraversalCost);
        runtimePoints.Add(new RuntimeSearchPoint(worldPosition, waitDuration, bestLink, score, bestTraversalStart, bestTraversalEnd));
        return true;
    }

    private void EvaluateSearchEmptyRouteJumpLinkCandidate(
        EnemyJumpLink25D link,
        Vector3 currentRef,
        Vector3 failedAnchor,
        Vector3 traversalStart,
        Vector3 traversalEnd,
        ref EnemyJumpLink25D bestLink,
        ref float bestScore,
        ref Vector3 bestTraversalStart,
        ref Vector3 bestTraversalEnd)
    {
        if (link == null)
            return;

        if (!IsDynamicJumpLinkEndpointReachable(currentRef, traversalStart))
            return;

        float entryDistance = Mathf.Abs(traversalStart.x - currentRef.x);
        if (searchEmptyRouteJumpLinkMaxEntryDistance > 0f && entryDistance > searchEmptyRouteJumpLinkMaxEntryDistance)
            return;

        bool sameLevelAsAnchor = Mathf.Abs(traversalEnd.y - failedAnchor.y) <= combatJumpLinkSameLevelTolerance;
        bool improvesTarget = DoesJumpLinkImproveSearchTarget(currentRef, traversalEnd, failedAnchor);
        if (!sameLevelAsAnchor && !improvesTarget)
            return;

        if (!searchEmptyAllowBridgeJumpOutsideAnchorRadius && Mathf.Abs(traversalEnd.x - failedAnchor.x) > dynamicPatrolAnchorRadius * 1.5f)
            return;

        float currentDistance = Vector2.Distance(new Vector2(currentRef.x, currentRef.y), new Vector2(failedAnchor.x, failedAnchor.y));
        float exitDistance = Vector2.Distance(new Vector2(traversalEnd.x, traversalEnd.y), new Vector2(failedAnchor.x, failedAnchor.y));
        float improvement = currentDistance - exitDistance;
        float score = improvement * 10f;
        if (sameLevelAsAnchor)
            score += 8f;
        if (searchEmptyAllowBridgeJumpOutsideAnchorRadius)
            score += 1f;
        score -= entryDistance * 0.25f;
        score -= Mathf.Max(0f, link.TraversalCost) * 0.35f;

        if (score > bestScore)
        {
            bestScore = score;
            bestLink = link;
            bestTraversalStart = traversalStart;
            bestTraversalEnd = traversalEnd;
        }
    }

    private bool TryBuildLocalSearchFallbackAroundCurrentRef(Vector3 currentRef, float waitDuration, out Vector3 localAnchor, out int localPointCount)
    {
        localAnchor = ResolveDynamicPatrolAnchor(currentRef);
        localPointCount = 0;

        int preferredSide = GetPreferredSearchSideSign(localAnchor);
        float localRadius = Mathf.Max(0f, searchEmptyLocalFallbackRadius);
        float near = Mathf.Min(dynamicPointNearDistance, localRadius);
        float far = localRadius;
        float mid = (near + far) * 0.5f;

        List<float> offsets = new List<float>(7);
        offsets.Add(0f);
        if (preferredSide >= 0)
        {
            offsets.Add(near);
            offsets.Add(mid);
            offsets.Add(far);
            offsets.Add(-near);
            offsets.Add(-mid);
            offsets.Add(-far);
        }
        else
        {
            offsets.Add(-near);
            offsets.Add(-mid);
            offsets.Add(-far);
            offsets.Add(near);
            offsets.Add(mid);
            offsets.Add(far);
        }

        for (int i = 0; i < offsets.Count; i++)
        {
            if (CountRuntimeWalkPoints() >= dynamicPointMaxCount)
                break;

            if (Mathf.Abs(offsets[i]) <= 0.001f && i > 0)
                continue;

            float targetX = localAnchor.x + offsets[i];
            if (TryBuildGroundedRuntimePoint(targetX, localAnchor, waitDuration, false, out RuntimeSearchPoint point))
            {
                if (!ValidateDynamicWalkPointEdgeClearanceBeforeAdd(point, false, "LocalFallbackWalkCandidate"))
                    continue;

                runtimePoints.Add(point);
                localPointCount++;
            }
        }

        return localPointCount > 0;
    }

    private void AddSearchEmptyEmergencyWaitPoint(Vector3 currentRef)
    {
        Vector3 waitPoint = ResolveDynamicPatrolAnchor(currentRef);
        waitPoint.z = 0f;
        runtimePoints.Add(new RuntimeSearchPoint(waitPoint, Mathf.Max(0f, searchEmptyEmergencyWaitTime), false, null, 0f));
    }

    private void ResetSearchEmptyRecoveryCounter()
    {
        consecutiveEmptySearchRecoveries = 0;
    }

    private void LogSearchEmptyRecoveryDetected(Vector3 failedAnchor, Vector3 currentRef, string rebuildReason)
    {
        if (!logSearchEmptyRecovery)
            return;

        string message =
            $"[EnemyBrainBT25D] Empty SearchDynamic points detected\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"CurrentRef: {FormatVector3ForLog(currentRef)}\n" +
            $"FailedAnchor: {FormatVector3ForLog(failedAnchor)}\n" +
            $"Reason: {rebuildReason}\n" +
            $"PreviousSearchTarget: {FormatVector3ForLog(activeSearchRecoveryTarget)}\n" +
            $"LastKnownReason: {(perception != null ? perception.LastKnownUpdateReason : "None")}\n" +
            $"ConsecutiveEmptySearchRecoveries: {consecutiveEmptySearchRecoveries}";

        Debug.LogWarning(message, this);
        if (writeSearchEmptyRecoveryLogsToFile)
            WriteEnemyDebugLogToFile("SearchEmptyRecovery", message);
    }

    private void LogSearchEmptyRecoveredWithRouteJumpLink(Vector3 failedAnchor, Vector3 currentRef, EnemyJumpLink25D link, Vector3 walkTo, Vector3 jumpTo)
    {
        if (!logSearchEmptyRecovery)
            return;

        string linkName = link != null ? link.name : "null";
        string message =
            $"[EnemyBrainBT25D] Empty SearchDynamic recovered with route JumpLink\n" +
            $"Enemy: {name}\n" +
            $"CurrentRef: {FormatVector3ForLog(currentRef)}\n" +
            $"FailedAnchor: {FormatVector3ForLog(failedAnchor)}\n" +
            $"Link: {linkName}\n" +
            $"WalkTo: {FormatVector3ForLog(walkTo)}\n" +
            $"JumpTo: {FormatVector3ForLog(jumpTo)}\n" +
            $"AllowBridgeOutsideAnchorRadius: {searchEmptyAllowBridgeJumpOutsideAnchorRadius}\n" +
            $"PointsAfterRecovery: {runtimePoints.Count}";

        Debug.Log(message, this);
        if (writeSearchEmptyRecoveryLogsToFile)
            WriteEnemyDebugLogToFile("SearchEmptyRecovery", message);
    }

    private void LogSearchEmptyRecoveredWithLocalFallback(Vector3 failedAnchor, Vector3 currentRef, Vector3 localAnchor, int pointCount, string reason)
    {
        if (!logSearchEmptyRecovery)
            return;

        string message =
            $"[EnemyBrainBT25D] Empty SearchDynamic recovered with local fallback\n" +
            $"Enemy: {name}\n" +
            $"CurrentRef: {FormatVector3ForLog(currentRef)}\n" +
            $"FailedAnchor: {FormatVector3ForLog(failedAnchor)}\n" +
            $"FallbackAnchor: {FormatVector3ForLog(localAnchor)}\n" +
            $"Points: {pointCount}\n" +
            $"Reason: {reason}";

        Debug.Log(message, this);
        if (writeSearchEmptyRecoveryLogsToFile)
            WriteEnemyDebugLogToFile("SearchEmptyRecovery", message);
    }

    private void LogSearchEmptyRecoveredWithEmergencyWait(Vector3 failedAnchor, Vector3 currentRef)
    {
        if (!logSearchEmptyRecovery)
            return;

        string message =
            $"[EnemyBrainBT25D] Empty SearchDynamic recovered with emergency wait\n" +
            $"Enemy: {name}\n" +
            $"CurrentRef: {FormatVector3ForLog(currentRef)}\n" +
            $"FailedAnchor: {FormatVector3ForLog(failedAnchor)}\n" +
            $"WaitTime: {Mathf.Max(0f, searchEmptyEmergencyWaitTime):F2}\n" +
            $"PointsAfterRecovery: {runtimePoints.Count}";

        Debug.LogWarning(message, this);
        if (writeSearchEmptyRecoveryLogsToFile)
            WriteEnemyDebugLogToFile("SearchEmptyRecovery", message);
    }

    private void LogSearchEmptyRecoveryFailed(Vector3 failedAnchor, Vector3 currentRef)
    {
        if (!logSearchEmptyRecovery)
            return;

        string message =
            $"[EnemyBrainBT25D] Empty SearchDynamic recovery failed\n" +
            $"Enemy: {name}\n" +
            $"CurrentRef: {FormatVector3ForLog(currentRef)}\n" +
            $"FailedAnchor: {FormatVector3ForLog(failedAnchor)}\n" +
            $"RouteJumpLinkFallback: {searchEmptyTryRouteJumpLinkFallback}\n" +
            $"LocalCurrentRefFallback: {searchEmptyFallbackToLocalAroundCurrentRef}\n" +
            $"EmergencyWaitFallback: {searchEmptyFallbackToEmergencyWait}";

        Debug.LogError(message, this);
        if (writeSearchEmptyRecoveryLogsToFile)
            WriteEnemyDebugLogToFile("SearchEmptyRecovery", message);
    }

    private void GenerateDynamicPatrolPoints(Vector3 anchor, bool includeJumpLinks, float waitDuration)
    {
        BeginRuntimePointGeneration(anchor, RuntimePointMode.DynamicPatrol);

        float midDistance = (dynamicPointNearDistance + dynamicPointFarDistance) * 0.5f;
        float[] candidateOffsets = new float[]
        {
            -dynamicPointNearDistance,
            dynamicPointNearDistance,
            -midDistance,
            midDistance,
            -dynamicPointFarDistance,
            dynamicPointFarDistance,
        };

        for (int i = 0; i < candidateOffsets.Length; i++)
        {
            if (CountRuntimeWalkPoints() >= dynamicPointMaxCount)
                break;

            float targetX = anchor.x + candidateOffsets[i];
            if (TryBuildGroundedRuntimePoint(targetX, anchor, waitDuration, true, out RuntimeSearchPoint point))
            {
                if (!ValidateDynamicWalkPointEdgeClearanceBeforeAdd(point, false, "DynamicPatrolWalkCandidate"))
                    continue;

                runtimePoints.Add(point);
            }
        }

        if (includeJumpLinks)
            AppendNearbyJumpLinkedRuntimePoints(anchor, waitDuration);

        FinalizeRuntimePointGeneration(anchor);
    }


    private float[] BuildSearchCandidateOffsets(int preferredSide, float nearDistance, float midDistance, float farDistance)
    {
        if (preferredSide > 0)
        {
            return new float[]
            {
                0f,
                nearDistance,
                midDistance,
                farDistance,
                -nearDistance,
                -midDistance,
                -farDistance,
            };
        }

        if (preferredSide < 0)
        {
            return new float[]
            {
                0f,
                -nearDistance,
                -midDistance,
                -farDistance,
                nearDistance,
                midDistance,
                farDistance,
            };
        }

        return new float[]
        {
            0f,
            -nearDistance,
            nearDistance,
            -midDistance,
            midDistance,
            -farDistance,
            farDistance,
        };
    }

    private int GetPreferredSearchSideSign(Vector3 anchor)
    {
        if (perception != null)
        {
            if (Time.time < rearAwarenessFocusEndTime && perception.HasTarget)
            {
                return ResolveHorizontalSign(perception.GetAimPosition().x - transform.position.x, perception.LastKnownTargetSideSign);
            }

            if (perception.LastKnownTargetSideSign != 0)
                return perception.LastKnownTargetSideSign;
        }

        return ResolveHorizontalSign(anchor.x - transform.position.x, character != null ? character.FacingSign : 1);
    }

    private int ResolveHorizontalSign(float deltaX, int fallbackSign)
    {
        if (Mathf.Abs(deltaX) > 0.01f)
            return deltaX >= 0f ? 1 : -1;

        if (fallbackSign != 0)
            return fallbackSign > 0 ? 1 : -1;

        return 1;
    }

    private void ScheduleSearchRecoveryRetry(Vector3 anchor, bool useCurrentPositionAsPatrolAnchor)
    {
        if (!searchRecoveryPending)
        {
            searchRecoveryPending = true;
            searchRecoveryRetryTime = Time.time + searchRecoveryRetryDelay;
        }

        if (useCurrentPositionAsPatrolAnchor)
        {
            dynamicPatrolAnchor = ResolveDynamicPatrolAnchor(GetTraversalReferencePosition());
            hasDynamicPatrolAnchor = true;
        }
    }

    private void HandleRearAwarenessRecovery()
    {
        if (perception == null || !perception.RearAwarenessTriggeredThisFrame)
            return;

        rearAwarenessFocusEndTime = Time.time + rearAwarenessFocusDuration;
        searchRecoveryPending = false;
        searchRecoveryRetryTime = float.NegativeInfinity;
        ClearRuntimePoints();
        ReleaseSelectedCover();

        if (perception.HasLastKnownPosition)
            BeginSearchRecovery(perception.LastKnownTargetPosition);

        if (character != null && perception.HasTarget)
        {
            int targetSign = ResolveHorizontalSign(perception.GetAimPosition().x - transform.position.x, perception.LastKnownTargetSideSign);
            character.ForceFacingSign(targetSign);
        }
    }

    private Vector3 ResolveSearchAnchor(Vector3 desiredAnchor)
    {
        bool forceLog = ConsumeForcedSearchAnchorProjectionLog(out string forceReason);

        if (TryProjectAnchorToGroundDetailed(desiredAnchor, out Vector3 groundedAnchor, out AnchorProjectionDebugInfo projectionInfo))
        {
            RecordSearchAnchorProjectionDebug(desiredAnchor, groundedAnchor, true, projectionInfo);
            LogSearchAnchorProjectionDebug(forceLog, forceReason);
            return groundedAnchor;
        }

        Vector3 fallback = GetTraversalReferencePosition();
        fallback.z = 0f;
        RecordSearchAnchorProjectionDebug(desiredAnchor, fallback, false, projectionInfo);
        LogSearchAnchorProjectionDebug(forceLog, forceReason);
        return fallback;
    }

    private Vector3 ResolveDynamicPatrolAnchor(Vector3 desiredAnchor)
    {
        if (TryProjectAnchorToGround(desiredAnchor, out Vector3 groundedAnchor))
            return groundedAnchor;

        Vector3 fallback = GetTraversalReferencePosition();
        fallback.z = 0f;
        return fallback;
    }

    private bool TryProjectAnchorToGround(Vector3 desiredAnchor, out Vector3 groundedAnchor)
    {
        return TryProjectAnchorToGroundDetailed(desiredAnchor, out groundedAnchor, out _);
    }

    private bool TryProjectAnchorToGroundDetailed(Vector3 desiredAnchor, out Vector3 groundedAnchor, out AnchorProjectionDebugInfo projectionInfo)
    {
        groundedAnchor = desiredAnchor;
        groundedAnchor.z = 0f;
        projectionInfo = new AnchorProjectionDebugInfo
        {
            Hit = false,
            HitObjectName = "None",
            HitLayer = -1,
            Reason = "NoGroundHit"
        };

        Vector3 traversalReference = GetTraversalReferencePosition();
        float referenceY = Mathf.Max(desiredAnchor.y, traversalReference.y);
        float rayDepth = dynamicPointRaycastDepth + Mathf.Abs(desiredAnchor.y - traversalReference.y) + dynamicPointRaycastHeight + 0.5f;
        Vector3 rayOrigin = new Vector3(desiredAnchor.x, referenceY + dynamicPointRaycastHeight, traversalReference.z);
        projectionInfo.RayOrigin = rayOrigin;
        projectionInfo.RayDepth = rayDepth;

        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDepth, dynamicPointGroundMask, QueryTriggerInteraction.Ignore))
            return false;

        groundedAnchor = hit.point;
        groundedAnchor.z = 0f;
        projectionInfo.Hit = true;
        projectionInfo.HitObjectName = hit.collider != null ? hit.collider.name : "None";
        projectionInfo.HitLayer = hit.collider != null ? hit.collider.gameObject.layer : -1;
        projectionInfo.Reason = "GroundProjectionHit";
        return true;
    }

    private void RecordSearchAnchorProjectionDebug(Vector3 rawTarget, Vector3 resolvedAnchor, bool wasProjected, AnchorProjectionDebugInfo projectionInfo)
    {
        lastSearchAnchorRawTarget = rawTarget;
        lastSearchAnchorResolved = resolvedAnchor;
        lastSearchAnchorWasProjected = wasProjected;
        lastSearchAnchorProjectionReason = wasProjected ? projectionInfo.Reason : projectionInfo.Reason;
        lastSearchAnchorHitName = projectionInfo.HitObjectName;
        lastSearchAnchorHitLayer = projectionInfo.HitLayer;
        lastSearchAnchorRayOrigin = projectionInfo.RayOrigin;
        lastSearchAnchorRayDepth = projectionInfo.RayDepth;
    }

    private void LogSearchAnchorProjectionDebug(bool forceLog = false, string forceReason = "None")
    {
        if (!logSearchAnchorProjection)
            return;

        if (!ShouldLogSearchAnchorProjectionDebug(forceLog))
            return;

        string normalizedForceReason = string.IsNullOrEmpty(forceReason) ? "None" : forceReason;
        string message =
            $"[EnemyBrainBT25D] Search anchor resolved\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"LogReason: {normalizedForceReason}\n" +
            $"RawTarget: {FormatVector3ForLog(lastSearchAnchorRawTarget)}\n" +
            $"ResolvedAnchor: {FormatVector3ForLog(lastSearchAnchorResolved)}\n" +
            $"WasProjected: {lastSearchAnchorWasProjected}\n" +
            $"ProjectionReason: {lastSearchAnchorProjectionReason}\n" +
            $"HitObject: {lastSearchAnchorHitName}\n" +
            $"HitLayer: {lastSearchAnchorHitLayer}\n" +
            $"GroundMask: {dynamicPointGroundMask.value}\n" +
            $"RayOrigin: {FormatVector3ForLog(lastSearchAnchorRayOrigin)}\n" +
            $"RayDepth: {lastSearchAnchorRayDepth:F2}\n" +
            $"DynamicRaycastHeight: {dynamicPointRaycastHeight:F2}\n" +
            $"DynamicRaycastDepth: {dynamicPointRaycastDepth:F2}";

        Debug.Log(message, this);
        RememberLoggedSearchAnchorProjection();

        if (writeSearchAnchorProjectionLogsToFile)
            WriteEnemyDebugLogToFile("SearchAnchorProjection", message);
    }

    private bool ShouldLogSearchAnchorProjectionDebug(bool forceLog)
    {
        if (!suppressRepeatedSearchAnchorProjectionLogs)
            return true;

        if (forceLog)
            return true;

        if (!hasLastLoggedSearchAnchorProjection)
            return !logSearchAnchorProjectionOnRebuildOnly;

        return HasSearchAnchorProjectionLogDataChanged();
    }

    private bool HasSearchAnchorProjectionLogDataChanged()
    {
        if (Vector3.Distance(lastSearchAnchorRawTarget, lastLoggedSearchAnchorRawTarget) > searchAnchorProjectionLogPositionTolerance)
            return true;

        if (Vector3.Distance(lastSearchAnchorResolved, lastLoggedSearchAnchorResolvedAnchor) > searchAnchorProjectionLogPositionTolerance)
            return true;

        if (!string.Equals(lastSearchAnchorProjectionReason, lastLoggedSearchAnchorProjectionReason, StringComparison.Ordinal))
            return true;

        if (!string.Equals(lastSearchAnchorHitName, lastLoggedSearchAnchorHitObject, StringComparison.Ordinal))
            return true;

        return false;
    }

    private void RememberLoggedSearchAnchorProjection()
    {
        hasLastLoggedSearchAnchorProjection = true;
        lastLoggedSearchAnchorRawTarget = lastSearchAnchorRawTarget;
        lastLoggedSearchAnchorResolvedAnchor = lastSearchAnchorResolved;
        lastLoggedSearchAnchorProjectionReason = lastSearchAnchorProjectionReason;
        lastLoggedSearchAnchorHitObject = lastSearchAnchorHitName;
        lastLoggedSearchAnchorFrame = Time.frameCount;
    }

    private void ForceNextSearchAnchorProjectionLog(string reason)
    {
        forceNextSearchAnchorProjectionLog = true;
        forcedSearchAnchorProjectionLogReason = string.IsNullOrEmpty(reason) ? "Forced" : reason;
    }

    private bool ConsumeForcedSearchAnchorProjectionLog(out string reason)
    {
        if (!forceNextSearchAnchorProjectionLog)
        {
            reason = "None";
            return false;
        }

        forceNextSearchAnchorProjectionLog = false;
        reason = forcedSearchAnchorProjectionLogReason;
        forcedSearchAnchorProjectionLogReason = "None";
        return true;
    }

    private int CountRuntimeWalkPoints()
    {
        int count = 0;
        for (int i = 0; i < runtimePoints.Count; i++)
        {
            if (!runtimePoints[i].RequiresJumpLink)
                count++;
        }

        return count;
    }

    private int CountRuntimeJumpPoints()
    {
        int count = 0;
        for (int i = 0; i < runtimePoints.Count; i++)
        {
            if (runtimePoints[i].RequiresJumpLink)
                count++;
        }

        return count;
    }

    private void TrimRuntimePointsToBudgets()
    {
        if (!dynamicJumpLinksUseSeparateBudget || !preserveDynamicJumpLinksWhenTrimmingRuntimePoints)
        {
            runtimePoints.Sort((a, b) => a.Score.CompareTo(b.Score));
            if (runtimePoints.Count > dynamicPointMaxCount)
                runtimePoints.RemoveRange(dynamicPointMaxCount, runtimePoints.Count - dynamicPointMaxCount);
            return;
        }

        List<RuntimeSearchPoint> walkPoints = new List<RuntimeSearchPoint>(runtimePoints.Count);
        List<RuntimeSearchPoint> jumpPoints = new List<RuntimeSearchPoint>(runtimePoints.Count);

        for (int i = 0; i < runtimePoints.Count; i++)
        {
            RuntimeSearchPoint point = runtimePoints[i];
            if (point.RequiresJumpLink)
                jumpPoints.Add(point);
            else
                walkPoints.Add(point);
        }

        walkPoints.Sort((a, b) => a.Score.CompareTo(b.Score));
        jumpPoints.Sort((a, b) => a.Score.CompareTo(b.Score));

        if (walkPoints.Count > dynamicPointMaxCount)
            walkPoints.RemoveRange(dynamicPointMaxCount, walkPoints.Count - dynamicPointMaxCount);

        if (jumpPoints.Count > dynamicJumpLinkMaxCount)
            jumpPoints.RemoveRange(dynamicJumpLinkMaxCount, jumpPoints.Count - dynamicJumpLinkMaxCount);

        runtimePoints.Clear();
        runtimePoints.AddRange(walkPoints);
        runtimePoints.AddRange(jumpPoints);
        runtimePoints.Sort((a, b) => a.Score.CompareTo(b.Score));
    }

    private void BeginRuntimePointGeneration(Vector3 anchor, RuntimePointMode mode)
    {
        runtimePoints.Clear();
        runtimePointMode = mode;
        runtimePointIndex = 0;
        runtimePointWaitEndTime = 0f;
        runtimePointsAnchor = anchor;
        hasRuntimePoints = true;
        activeTraversalLink = null;
        rejectedDynamicWalkPointUnsafeEdgeCount = 0;
        warnedLastKnownInvestigationPointNearEdgeCount = 0;
        adjustedLastKnownSafePointCount = 0;
        failedLastKnownSafeAdjustmentCount = 0;
        CancelDynamicPatrolDeadEndDelay();

        if (mode == RuntimePointMode.Search)
            LogSearchAnchorProjectionDebug(true, "DynamicPoints rebuild");
    }

    private void FinalizeRuntimePointGeneration(Vector3 anchor)
    {
        RuntimePointMode finalizedMode = runtimePointMode;

        TrimRuntimePointsToBudgets();

        if (runtimePoints.Count == 0)
        {
            hasRuntimePoints = false;
            runtimePointIndex = 0;
            RecordDynamicPointGenerationResult(anchor, finalizedMode, 0);
            LogRuntimePointList(GetRuntimePointRebuildLogReason(finalizedMode), anchor, finalizedMode);
            return;
        }

        if (finalizedMode == RuntimePointMode.DynamicPatrol)
            runtimePointIndex = ChooseDynamicPatrolRuntimePointIndex();
        else
            runtimePointIndex = 0;

        RecordDynamicPointGenerationResult(anchor, finalizedMode, runtimePoints.Count);
        LogRuntimePointList(GetRuntimePointRebuildLogReason(finalizedMode), anchor, finalizedMode);
    }

    private int ChooseDynamicPatrolRuntimePointIndex()
    {
        if (runtimePoints.Count <= 0)
            return 0;

        Vector3 traversalReference = GetTraversalReferencePosition();
        bool hasSelectableWalkingPoint = RuntimePointsContainSelectableWalkPoint(traversalReference);
        bool hasNonReturnJumpPoint = RuntimePointsContainNonReturnJumpPoint();

        float totalWeight = 0f;
        for (int i = 0; i < runtimePoints.Count; i++)
            totalWeight += GetDynamicPatrolRuntimePointSelectionWeight(runtimePoints[i], traversalReference, hasSelectableWalkingPoint, hasNonReturnJumpPoint);

        if (totalWeight <= 0.0001f)
            return FindFallbackDynamicPatrolRuntimePointIndex(traversalReference, hasSelectableWalkingPoint, hasNonReturnJumpPoint);

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        for (int i = 0; i < runtimePoints.Count; i++)
        {
            float weight = GetDynamicPatrolRuntimePointSelectionWeight(runtimePoints[i], traversalReference, hasSelectableWalkingPoint, hasNonReturnJumpPoint);
            if (weight <= 0f)
                continue;

            roll -= weight;
            if (roll <= 0f)
                return i;
        }

        return FindFallbackDynamicPatrolRuntimePointIndex(traversalReference, hasSelectableWalkingPoint, hasNonReturnJumpPoint);
    }

    private float GetDynamicPatrolRuntimePointSelectionWeight(RuntimeSearchPoint point, Vector3 traversalReference, bool hasSelectableWalkingPoint, bool hasNonReturnJumpPoint)
    {
        if (point.RequiresJumpLink)
        {
            if (!allowJumpLinksInDynamicPatrol)
                return 0f;

            if (IsDynamicPatrolJumpPointBlockedByPostJumpRule(point, hasSelectableWalkingPoint, hasNonReturnJumpPoint))
                return 0f;

            if (IsDynamicPatrolDeadEndReturnPoint(point, hasSelectableWalkingPoint, hasNonReturnJumpPoint))
                return 1f;

            return Mathf.Clamp01(dynamicPatrolJumpLinkSelectionWeight);
        }

        float travelDistance = Mathf.Abs(point.WorldPosition.x - traversalReference.x);
        if (travelDistance < dynamicPatrolMinTravelDistance)
            return 0f;

        return 1f;
    }

    private int FindFallbackDynamicPatrolRuntimePointIndex(Vector3 traversalReference, bool hasSelectableWalkingPoint, bool hasNonReturnJumpPoint)
    {
        int bestIndex = -1;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < runtimePoints.Count; i++)
        {
            RuntimeSearchPoint point = runtimePoints[i];
            if (point.RequiresJumpLink && IsDynamicPatrolJumpPointBlockedByPostJumpRule(point, hasSelectableWalkingPoint, hasNonReturnJumpPoint))
                continue;

            float travelDistance = Mathf.Abs(GetRuntimePointNavigationTarget(point).x - traversalReference.x);
            float score = point.RequiresJumpLink ? point.Score + Mathf.Max(0f, 1f - dynamicPatrolJumpLinkSelectionWeight) * 10f : travelDistance;
            if (IsDynamicPatrolDeadEndReturnPoint(point, hasSelectableWalkingPoint, hasNonReturnJumpPoint))
                score -= 1000f;

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        if (bestIndex < 0)
            bestIndex = 0;

        return Mathf.Clamp(bestIndex, 0, Mathf.Max(0, runtimePoints.Count - 1));
    }

    private bool RuntimePointsContainSelectableWalkPoint(Vector3 traversalReference)
    {
        for (int i = 0; i < runtimePoints.Count; i++)
        {
            RuntimeSearchPoint point = runtimePoints[i];
            if (point.RequiresJumpLink)
                continue;

            if (Mathf.Abs(point.WorldPosition.x - traversalReference.x) >= dynamicPatrolMinTravelDistance)
                return true;
        }

        return false;
    }

    private bool RuntimePointsContainWalkPoint()
    {
        for (int i = 0; i < runtimePoints.Count; i++)
        {
            if (!runtimePoints[i].RequiresJumpLink)
                return true;
        }

        return false;
    }

    private bool RuntimePointsContainNonReturnJumpPoint()
    {
        for (int i = 0; i < runtimePoints.Count; i++)
        {
            RuntimeSearchPoint point = runtimePoints[i];
            if (!point.RequiresJumpLink)
                continue;

            if (!IsReturnToLastDynamicPatrolJump(point))
                return true;
        }

        return false;
    }

    private bool RuntimePointsContainReturnJumpPoint()
    {
        for (int i = 0; i < runtimePoints.Count; i++)
        {
            if (IsReturnToLastDynamicPatrolJump(runtimePoints[i]))
                return true;
        }

        return false;
    }

    private bool IsDynamicPatrolPostJumpRestrictionActive()
    {
        return currentState == BrainState.PatrolDynamic
            && runtimePointMode == RuntimePointMode.DynamicPatrol
            && dynamicPatrolWalkPointsRemainingBeforeJumpLinkAllowed > 0;
    }

    private bool IsDynamicPatrolJumpPointBlockedByPostJumpRule(RuntimeSearchPoint point)
    {
        Vector3 traversalReference = GetTraversalReferencePosition();
        return IsDynamicPatrolJumpPointBlockedByPostJumpRule(
            point,
            RuntimePointsContainSelectableWalkPoint(traversalReference),
            RuntimePointsContainNonReturnJumpPoint());
    }

    private bool IsDynamicPatrolJumpPointBlockedByPostJumpRule(RuntimeSearchPoint point, bool hasSelectableWalkingPoint, bool hasNonReturnJumpPoint)
    {
        if (!point.RequiresJumpLink)
            return false;

        if (!IsDynamicPatrolPostJumpRestrictionActive())
            return false;

        if (hasSelectableWalkingPoint)
            return true;

        if (IsReturnToLastDynamicPatrolJump(point) && hasNonReturnJumpPoint)
            return true;

        return false;
    }

    private bool IsDynamicPatrolDeadEndReturnPoint(RuntimeSearchPoint point)
    {
        Vector3 traversalReference = GetTraversalReferencePosition();
        return IsDynamicPatrolDeadEndReturnPoint(
            point,
            RuntimePointsContainSelectableWalkPoint(traversalReference),
            RuntimePointsContainNonReturnJumpPoint());
    }

    private bool IsDynamicPatrolDeadEndReturnPoint(RuntimeSearchPoint point, bool hasSelectableWalkingPoint, bool hasNonReturnJumpPoint)
    {
        if (!IsDynamicPatrolPostJumpRestrictionActive())
            return false;

        if (hasSelectableWalkingPoint || hasNonReturnJumpPoint)
            return false;

        return IsReturnToLastDynamicPatrolJump(point);
    }

    private bool IsReturnToLastDynamicPatrolJump(RuntimeSearchPoint point)
    {
        if (!hasLastDynamicPatrolJump)
            return false;

        if (!point.RequiresJumpLink || point.JumpLink == null || point.JumpLink != lastDynamicPatrolJumpLink)
            return false;

        if (!point.HasExplicitJumpTraversal)
            return false;

        float tolerance = Mathf.Max(0.01f, dynamicPatrolReturnJumpMatchTolerance);
        bool startMatchesLastEnd = Vector2.Distance(
            new Vector2(point.JumpTraversalStart.x, point.JumpTraversalStart.y),
            new Vector2(lastDynamicPatrolJumpEnd.x, lastDynamicPatrolJumpEnd.y)) <= tolerance;
        bool endMatchesLastStart = Vector2.Distance(
            new Vector2(point.JumpTraversalEnd.x, point.JumpTraversalEnd.y),
            new Vector2(lastDynamicPatrolJumpStart.x, lastDynamicPatrolJumpStart.y)) <= tolerance;

        return startMatchesLastEnd && endMatchesLastStart;
    }

    private bool TryHandleDynamicPatrolDeadEndReturn(RuntimeSearchPoint point)
    {
        if (!IsDynamicPatrolDeadEndReturnPoint(point))
            return false;

        float tolerance = Mathf.Max(0.01f, dynamicPatrolReturnJumpMatchTolerance);
        bool sameReturn = dynamicPatrolDeadEndDelayActive
            && dynamicPatrolDeadEndReturnLink == point.JumpLink
            && Vector2.Distance(new Vector2(dynamicPatrolDeadEndReturnStart.x, dynamicPatrolDeadEndReturnStart.y), new Vector2(point.JumpTraversalStart.x, point.JumpTraversalStart.y)) <= tolerance
            && Vector2.Distance(new Vector2(dynamicPatrolDeadEndReturnEnd.x, dynamicPatrolDeadEndReturnEnd.y), new Vector2(point.JumpTraversalEnd.x, point.JumpTraversalEnd.y)) <= tolerance;

        if (!dynamicPatrolDeadEndDelayActive || !sameReturn)
        {
            dynamicPatrolDeadEndDelayActive = true;
            dynamicPatrolDeadEndResumeTime = Time.time + dynamicPatrolDeadEndDelay;
            dynamicPatrolDeadEndReturnLink = point.JumpLink;
            dynamicPatrolDeadEndReturnStart = point.JumpTraversalStart;
            dynamicPatrolDeadEndReturnEnd = point.JumpTraversalEnd;

            if (dynamicPatrolLogDeadEnd)
            {
                string linkName = point.JumpLink != null ? point.JumpLink.name : "null";
                string message = $"[EnemyBrainBT25D] Тупик! No WALK points and only return JumpLink is available. Returning after delay.\nEnemy: {name}\nLink: {linkName}\nCurrentRef: {FormatVector3ForLog(GetTraversalReferencePosition())}\nWalkTo: {FormatVector3ForLog(point.JumpTraversalStart)}\nJumpTo: {FormatVector3ForLog(point.JumpTraversalEnd)}\nDelay: {dynamicPatrolDeadEndDelay:F2}";
                Debug.Log(message, this);
                if (writeDynamicPatrolDeadEndLogsToFile)
                    WriteEnemyDebugLogToFile("DynamicPatrolDeadEnd", message);
            }
        }

        if (Time.time < dynamicPatrolDeadEndResumeTime)
        {
            currentAction = EnemyAction25D.WaitAtPoint;
            character.StopMovement();
            return true;
        }

        dynamicPatrolDeadEndDelayActive = false;
        return false;
    }

    private void NotifyDynamicPatrolWalkPointCompleted()
    {
        if (currentState != BrainState.PatrolDynamic || runtimePointMode != RuntimePointMode.DynamicPatrol)
            return;

        if (dynamicPatrolWalkPointsRemainingBeforeJumpLinkAllowed <= 0)
            return;

        dynamicPatrolWalkPointsRemainingBeforeJumpLinkAllowed = Mathf.Max(0, dynamicPatrolWalkPointsRemainingBeforeJumpLinkAllowed - 1);
        if (dynamicPatrolWalkPointsRemainingBeforeJumpLinkAllowed <= 0)
            CancelDynamicPatrolDeadEndDelay();
    }

    private void ResetDynamicPatrolPostJumpRestriction()
    {
        dynamicPatrolWalkPointsRemainingBeforeJumpLinkAllowed = 0;
        hasLastDynamicPatrolJump = false;
        lastDynamicPatrolJumpLink = null;
        lastDynamicPatrolJumpStart = Vector3.zero;
        lastDynamicPatrolJumpEnd = Vector3.zero;
        CancelDynamicPatrolDeadEndDelay();
    }

    private void CancelDynamicPatrolDeadEndDelay()
    {
        dynamicPatrolDeadEndDelayActive = false;
        dynamicPatrolDeadEndResumeTime = float.NegativeInfinity;
        dynamicPatrolDeadEndReturnLink = null;
        dynamicPatrolDeadEndReturnStart = Vector3.zero;
        dynamicPatrolDeadEndReturnEnd = Vector3.zero;
    }

    private bool TryBuildGroundedRuntimePoint(float targetX, Vector3 anchor, float waitDuration, bool enforcePatrolTravelDistance, out RuntimeSearchPoint point)
    {
        point = default;

        Vector3 traversalReference = GetTraversalReferencePosition();
        float referenceY = Mathf.Max(anchor.y, traversalReference.y);
        float rayDepth = dynamicPointRaycastDepth + Mathf.Abs(anchor.y - traversalReference.y) + dynamicPointRaycastHeight + 0.5f;
        Vector3 rayOrigin = new Vector3(targetX, referenceY + dynamicPointRaycastHeight, traversalReference.z);
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDepth, dynamicPointGroundMask, QueryTriggerInteraction.Ignore))
            return false;

        Vector3 worldPosition = hit.point;
        worldPosition.z = 0f;
        if (Mathf.Abs(worldPosition.x - anchor.x) > dynamicPatrolAnchorRadius)
            return false;

        if (!IsNormallyReachableDynamicPoint(traversalReference, worldPosition))
            return false;

        if (enforcePatrolTravelDistance && Mathf.Abs(worldPosition.x - traversalReference.x) < dynamicPatrolMinTravelDistance)
            return false;

        for (int i = 0; i < runtimePoints.Count; i++)
        {
            if (Mathf.Abs(runtimePoints[i].WorldPosition.x - worldPosition.x) < dynamicPointMinSeparation && Mathf.Abs(runtimePoints[i].WorldPosition.y - worldPosition.y) < 0.5f)
                return false;
        }

        float score = Mathf.Abs(worldPosition.x - anchor.x);
        if (runtimePointMode == RuntimePointMode.Search)
        {
            int preferredSide = GetPreferredSearchSideSign(anchor);
            int pointSide = ResolveHorizontalSign(worldPosition.x - anchor.x, preferredSide);
            if (preferredSide != 0 && pointSide != preferredSide)
                score += dynamicPointNearDistance * 2f;
            else if (preferredSide != 0 && pointSide == preferredSide && Mathf.Abs(worldPosition.x - anchor.x) > 0.01f)
                score -= 0.1f;
        }

        point = new RuntimeSearchPoint(worldPosition, waitDuration, false, null, score);
        return true;
    }

    private RuntimeSearchPoint AdjustLastKnownInvestigationPointIfNeeded(RuntimeSearchPoint point, string candidateSource)
    {
        if (!point.IsLastKnownInvestigationPoint || point.RequiresJumpLink)
            return point;

        if (!adjustUnsafeLastKnownInvestigationPoint)
            return point;

        if (!TryAdjustLastKnownInvestigationPointToSafePosition(point.WorldPosition, out Vector3 adjustedPoint, out LastKnownSafeAdjustmentResult adjustmentResult))
        {
            if (!adjustmentResult.FoundSafePoint && string.Equals(adjustmentResult.Reason, "NoSafePointFoundWithinMaxDistance", StringComparison.Ordinal))
            {
                failedLastKnownSafeAdjustmentCount++;
                LogLastKnownSafeAdjustmentFailed(point, adjustmentResult, candidateSource);
            }

            return point;
        }

        adjustedLastKnownSafePointCount++;
        RuntimeSearchPoint adjustedRuntimePoint = WithLastKnownSafeAdjustment(point, adjustedPoint, adjustmentResult);
        LogLastKnownSafeAdjustment(point, adjustedRuntimePoint, adjustmentResult, candidateSource);
        return adjustedRuntimePoint;
    }

    private bool TryAdjustLastKnownInvestigationPointToSafePosition(Vector3 originalPoint, out Vector3 adjustedPoint, out LastKnownSafeAdjustmentResult result)
    {
        adjustedPoint = originalPoint;
        result = default;
        result.Adjusted = false;
        result.FoundSafePoint = true;
        result.Reason = "OriginalPointAlreadySafe";
        result.OriginalPoint = originalPoint;
        result.AdjustedPoint = originalPoint;
        result.OriginalUnsafeReason = "None";
        result.SelectedDirectionLabel = "None";
        result.MaxDistance = Mathf.Max(0f, lastKnownSafeAdjustmentMaxDistance);
        result.Step = Mathf.Max(0f, lastKnownSafeAdjustmentStep);

        if (!adjustUnsafeLastKnownInvestigationPoint)
        {
            result.Reason = "Disabled";
            return false;
        }

        if (lastKnownSafeAdjustmentMaxDistance <= 0f || lastKnownSafeAdjustmentStep <= 0f)
        {
            result.Reason = "NoAdjustmentDistance";
            return false;
        }

        if (IsDynamicWalkPointEdgeSafe(originalPoint, true, out DynamicWalkEdgeClearanceResult originalEdgeResult))
        {
            result.OriginalEdgeResult = originalEdgeResult;
            result.Reason = "OriginalPointAlreadySafe";
            return false;
        }

        result.OriginalEdgeResult = originalEdgeResult;
        result.OriginalUnsafeReason = originalEdgeResult.RejectReason;

        int[] signs = GetPreferredLastKnownSafeAdjustmentSigns(originalEdgeResult);
        float step = Mathf.Max(0.01f, lastKnownSafeAdjustmentStep);
        float maxDistance = Mathf.Max(0f, lastKnownSafeAdjustmentMaxDistance);
        int maxSteps = Mathf.Max(1, Mathf.CeilToInt(maxDistance / step));
        int attempts = 0;

        for (int i = 1; i <= maxSteps; i++)
        {
            float distance = Mathf.Min(maxDistance, i * step);
            for (int s = 0; s < signs.Length; s++)
            {
                int sign = signs[s] >= 0 ? 1 : -1;
                Vector3 candidate = originalPoint + Vector3.right * sign * distance;
                candidate.z = 0f;
                attempts++;

                if (!IsDynamicWalkPointEdgeSafe(candidate, true, out DynamicWalkEdgeClearanceResult candidateEdgeResult))
                    continue;

                Vector3 safePoint = candidateEdgeResult.CenterGroundFound ? candidateEdgeResult.CenterGroundPoint : candidate;
                safePoint.z = 0f;

                adjustedPoint = safePoint;
                result.Adjusted = true;
                result.FoundSafePoint = true;
                result.Reason = "AdjustedToNearestSafePoint";
                result.AdjustedPoint = safePoint;
                result.AdjustmentDistance = safePoint.x - originalPoint.x;
                result.SelectedDirectionLabel = sign > 0 ? "Right" : "Left";
                result.Attempts = attempts;
                result.AdjustedEdgeResult = candidateEdgeResult;
                return true;
            }
        }

        result.Adjusted = false;
        result.FoundSafePoint = false;
        result.Reason = "NoSafePointFoundWithinMaxDistance";
        result.Attempts = attempts;
        result.AdjustedPoint = originalPoint;
        adjustedPoint = originalPoint;
        return false;
    }

    private int[] GetPreferredLastKnownSafeAdjustmentSigns(DynamicWalkEdgeClearanceResult edgeResult)
    {
        if (!preferLastKnownSafeAdjustmentAwayFromMissingGround)
            return new[] { 1, -1 };

        switch (edgeResult.RejectReason)
        {
            case "LeftGroundMissing":
            case "LeftGroundHeightDeltaTooLarge":
                return new[] { 1, -1 };

            case "RightGroundMissing":
            case "RightGroundHeightDeltaTooLarge":
                return new[] { -1, 1 };

            default:
                return new[] { 1, -1 };
        }
    }

    private RuntimeSearchPoint WithLastKnownSafeAdjustment(RuntimeSearchPoint point, Vector3 adjustedPoint, LastKnownSafeAdjustmentResult result)
    {
        Vector3 originalLastKnown = preserveOriginalLastKnownForDebug && point.LastKnownVersionSnapshot > 0
            ? point.LastKnownPositionSnapshot
            : result.OriginalPoint;

        if (point.RequiresJumpLink && point.HasExplicitJumpTraversal)
        {
            return new RuntimeSearchPoint(
                adjustedPoint,
                point.WaitDuration,
                point.JumpLink,
                point.Score,
                point.JumpTraversalStart,
                point.JumpTraversalEnd,
                point.IsLastKnownInvestigationPoint,
                point.HasFacingHint,
                point.FacingHintSign,
                point.FacingHintMode,
                point.FacingHintSource,
                point.LastKnownVersionSnapshot,
                point.LastKnownPositionSnapshot,
                point.HasLastKnownFacingHintSnapshot,
                point.LastKnownFacingSignSnapshot,
                point.LastKnownFacingModeSnapshot,
                point.LastKnownFacingSourceSnapshot,
                true,
                originalLastKnown,
                adjustedPoint,
                result.Reason,
                result.AdjustmentDistance);
        }

        return new RuntimeSearchPoint(
            adjustedPoint,
            point.WaitDuration,
            point.RequiresJumpLink,
            point.JumpLink,
            point.Score,
            point.IsLastKnownInvestigationPoint,
            point.HasFacingHint,
            point.FacingHintSign,
            point.FacingHintMode,
            point.FacingHintSource,
            point.LastKnownVersionSnapshot,
            point.LastKnownPositionSnapshot,
            point.HasLastKnownFacingHintSnapshot,
            point.LastKnownFacingSignSnapshot,
            point.LastKnownFacingModeSnapshot,
            point.LastKnownFacingSourceSnapshot,
            true,
            originalLastKnown,
            adjustedPoint,
            result.Reason,
            result.AdjustmentDistance);
    }

    private bool ValidateDynamicWalkPointEdgeClearanceBeforeAdd(RuntimeSearchPoint point, bool isLastKnownInvestigationPoint, string candidateSource)
    {
        if (point.RequiresJumpLink)
            return true;

        if (!ShouldEvaluateDynamicWalkPointEdgeClearance(candidateSource, isLastKnownInvestigationPoint))
            return true;

        bool isSafe = IsDynamicWalkPointEdgeSafe(point.WorldPosition, isLastKnownInvestigationPoint, out DynamicWalkEdgeClearanceResult edgeResult);
        if (isSafe)
            return true;

        if (isLastKnownInvestigationPoint)
        {
            if (warnWhenLastKnownInvestigationPointNearEdge)
            {
                warnedLastKnownInvestigationPointNearEdgeCount++;
                LogLastKnownInvestigationPointNearEdge(point, edgeResult, candidateSource);
            }

            return true;
        }

        rejectedDynamicWalkPointUnsafeEdgeCount++;
        LogRejectedDynamicWalkPointEdgeClearance(runtimePointMode, point.WorldPosition, edgeResult, candidateSource);
        return false;
    }

    private bool ShouldEvaluateDynamicWalkPointEdgeClearance(string candidateSource, bool isLastKnownInvestigationPoint)
    {
        if (!requireDynamicWalkPointEdgeClearance)
            return false;

        if (dynamicWalkPointMinEdgeClearance <= 0f)
            return false;

        if (isLastKnownInvestigationPoint)
            return warnWhenLastKnownInvestigationPointNearEdge;

        if (runtimePointMode == RuntimePointMode.DynamicPatrol)
            return applyDynamicWalkEdgeClearanceToDynamicPatrolPoints;

        if (runtimePointMode == RuntimePointMode.Search)
        {
            if (candidateSource == "LocalFallbackWalkCandidate")
                return applyDynamicWalkEdgeClearanceToLocalFallbackPoints;

            return applyDynamicWalkEdgeClearanceToSearchSidePoints;
        }

        return false;
    }

    private bool IsDynamicWalkPointEdgeSafe(Vector3 point, bool isLastKnownInvestigationPoint, out DynamicWalkEdgeClearanceResult result)
    {
        result = default;
        result.IsSafe = true;
        result.RejectReason = "None";
        result.Point = point;
        result.CenterGroundObject = "None";
        result.LeftGroundObject = "None";
        result.RightGroundObject = "None";
        result.CenterGroundLayer = -1;
        result.LeftGroundLayer = -1;
        result.RightGroundLayer = -1;
        result.MinEdgeClearance = dynamicWalkPointMinEdgeClearance;
        result.MaxGroundHeightDelta = dynamicWalkPointMaxGroundHeightDelta;

        if (!requireDynamicWalkPointEdgeClearance || dynamicWalkPointMinEdgeClearance <= 0f)
            return true;

        float upOffset = Mathf.Max(0f, dynamicWalkPointEdgeProbeUpOffset);
        float downDistance = Mathf.Max(0.01f, dynamicWalkPointEdgeProbeDownDistance);
        float rayDistance = upOffset + downDistance;
        Vector3 centerOrigin = point + Vector3.up * upOffset;
        Vector3 leftOrigin = centerOrigin + Vector3.left * dynamicWalkPointMinEdgeClearance;
        Vector3 rightOrigin = centerOrigin + Vector3.right * dynamicWalkPointMinEdgeClearance;

        result.CenterProbeOrigin = centerOrigin;
        result.LeftProbeOrigin = leftOrigin;
        result.RightProbeOrigin = rightOrigin;

        if (!Physics.Raycast(centerOrigin, Vector3.down, out RaycastHit centerHit, rayDistance, dynamicPointGroundMask, QueryTriggerInteraction.Ignore))
        {
            result.IsSafe = false;
            result.RejectReason = "CenterGroundMissing";
            result.CenterGroundFound = false;
            return false;
        }

        result.CenterGroundFound = true;
        result.CenterGroundPoint = centerHit.point;
        result.CenterGroundPoint.z = 0f;
        result.CenterGroundObject = centerHit.collider != null ? centerHit.collider.name : "None";
        result.CenterGroundLayer = centerHit.collider != null ? centerHit.collider.gameObject.layer : -1;

        if (!Physics.Raycast(leftOrigin, Vector3.down, out RaycastHit leftHit, rayDistance, dynamicPointGroundMask, QueryTriggerInteraction.Ignore))
        {
            result.IsSafe = false;
            result.RejectReason = "LeftGroundMissing";
            result.LeftGroundFound = false;
            return false;
        }

        result.LeftGroundFound = true;
        result.LeftGroundPoint = leftHit.point;
        result.LeftGroundPoint.z = 0f;
        result.LeftGroundObject = leftHit.collider != null ? leftHit.collider.name : "None";
        result.LeftGroundLayer = leftHit.collider != null ? leftHit.collider.gameObject.layer : -1;

        if (!Physics.Raycast(rightOrigin, Vector3.down, out RaycastHit rightHit, rayDistance, dynamicPointGroundMask, QueryTriggerInteraction.Ignore))
        {
            result.IsSafe = false;
            result.RejectReason = "RightGroundMissing";
            result.RightGroundFound = false;
            return false;
        }

        result.RightGroundFound = true;
        result.RightGroundPoint = rightHit.point;
        result.RightGroundPoint.z = 0f;
        result.RightGroundObject = rightHit.collider != null ? rightHit.collider.name : "None";
        result.RightGroundLayer = rightHit.collider != null ? rightHit.collider.gameObject.layer : -1;

        result.LeftHeightDelta = Mathf.Abs(result.LeftGroundPoint.y - result.CenterGroundPoint.y);
        result.RightHeightDelta = Mathf.Abs(result.RightGroundPoint.y - result.CenterGroundPoint.y);

        float maxHeightDelta = Mathf.Max(0f, dynamicWalkPointMaxGroundHeightDelta);
        if (result.LeftHeightDelta > maxHeightDelta)
        {
            result.IsSafe = false;
            result.RejectReason = "LeftGroundHeightDeltaTooLarge";
            return false;
        }

        if (result.RightHeightDelta > maxHeightDelta)
        {
            result.IsSafe = false;
            result.RejectReason = "RightGroundHeightDeltaTooLarge";
            return false;
        }

        result.IsSafe = true;
        result.RejectReason = "None";
        return true;
    }

    private bool IsNormallyReachableDynamicPoint(Vector3 fromReferencePosition, Vector3 candidateWorldPosition)
    {
        return IsNormallyReachableDynamicPoint(fromReferencePosition, candidateWorldPosition, dynamicPointSamePlatformVerticalTolerance);
    }

    private bool IsNormallyReachableDynamicPoint(Vector3 fromReferencePosition, Vector3 candidateWorldPosition, float verticalTolerance)
    {
        float effectiveVerticalTolerance = Mathf.Max(0f, verticalTolerance);
        if (Mathf.Abs(candidateWorldPosition.y - fromReferencePosition.y) > effectiveVerticalTolerance)
            return false;

        if (!validateDynamicPointWalkability)
            return true;

        float fromX = fromReferencePosition.x;
        float toX = candidateWorldPosition.x;
        float distanceX = Mathf.Abs(toX - fromX);
        if (distanceX <= 0.01f)
            return true;

        float step = Mathf.Max(0.1f, dynamicPointWalkabilitySampleStep);
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(distanceX / step));

        for (int i = 1; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float sampleX = Mathf.Lerp(fromX, toX, t);

            if (!TrySampleDynamicGroundAtX(sampleX, fromReferencePosition, out Vector3 sampleGround))
                return false;

            if (Mathf.Abs(sampleGround.y - fromReferencePosition.y) > effectiveVerticalTolerance)
                return false;
        }

        return true;
    }

    private bool IsDynamicJumpLinkEndpointReachable(Vector3 currentReferencePosition, Vector3 endpointPosition)
    {
        if (!validateDynamicJumpLinkStartReachability)
            return true;

        float tolerance = dynamicPointSamePlatformVerticalTolerance + dynamicJumpLinkStartReachabilityExtraTolerance;
        return IsNormallyReachableDynamicPoint(currentReferencePosition, endpointPosition, tolerance);
    }

    private bool TrySampleDynamicGroundAtX(float x, Vector3 referencePosition, out Vector3 groundPoint)
    {
        Vector3 rayOrigin = new Vector3(x, referencePosition.y + dynamicPointWalkabilityProbeHeight, referencePosition.z);
        float castDistance = Mathf.Max(0.05f, dynamicPointWalkabilityProbeDepth);
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, castDistance, dynamicPointGroundMask, QueryTriggerInteraction.Ignore))
        {
            groundPoint = default;
            return false;
        }

        groundPoint = hit.point;
        groundPoint.z = 0f;
        return true;
    }

    private void AppendNearbyJumpLinkedRuntimePoints(Vector3 anchor, float waitDuration)
    {
        EnemyJumpLink25D[] allLinks = FindObjectsByType<EnemyJumpLink25D>(FindObjectsSortMode.None);
        if (allLinks == null || allLinks.Length == 0)
            return;

        for (int i = 0; i < allLinks.Length; i++)
        {
            EnemyJumpLink25D link = allLinks[i];
            if (link == null || !link.EnabledLink)
                continue;

            Transform start = link.StartPoint;
            Transform end = link.EndPoint;
            if (start == null || end == null)
                continue;

            TryAppendJumpLinkCandidate(anchor, waitDuration, link, start.position, end.position, "SerializedStart->SerializedEnd");
            if (link.Bidirectional)
                TryAppendJumpLinkCandidate(anchor, waitDuration, link, end.position, start.position, "SerializedEnd->SerializedStart");
        }
    }

    private void TryAppendJumpLinkCandidate(Vector3 anchor, float waitDuration, EnemyJumpLink25D link, Vector3 startPos, Vector3 endPos, string directionLabel)
    {
        if (link == null)
            return;

        if (!CanAddDynamicJumpLinkCandidate(out string budgetRejectReason))
        {
            LogRejectedDynamicJumpLinkCandidate(link, directionLabel, budgetRejectReason, startPos, endPos);
            return;
        }

        if (runtimePointMode == RuntimePointMode.DynamicPatrol)
        {
            if (!allowJumpLinksInDynamicPatrol)
            {
                LogRejectedDynamicJumpLinkCandidate(link, directionLabel, "jump-links disabled in dynamic patrol", startPos, endPos);
                return;
            }
        }

        Vector3 traversalReference = GetTraversalReferencePosition();
        if (!IsDynamicJumpLinkEndpointReachable(traversalReference, startPos))
        {
            LogRejectedDynamicJumpLinkCandidate(link, directionLabel, "entry endpoint not reachable from current platform", startPos, endPos);
            return;
        }

        if (runtimePointMode == RuntimePointMode.Search)
        {
            if (IsRecentlyCompletedDynamicJumpLink(link, BrainState.SearchDynamic))
            {
                LogRejectedDynamicJumpLinkCandidate(link, directionLabel, "recently completed dynamic search block", startPos, endPos);
                return;
            }
        }

        if (Mathf.Abs(startPos.x - anchor.x) > jumpLinkSearchRadius)
        {
            LogRejectedDynamicJumpLinkCandidate(link, directionLabel, "entry endpoint outside jump link search radius", startPos, endPos);
            return;
        }

        Vector3 worldPosition = endPos;
        worldPosition.z = 0f;
        if (!IsDynamicJumpLinkExitAllowed(anchor, worldPosition, out string exitRejectReason))
        {
            LogRejectedDynamicJumpLinkCandidate(link, directionLabel, exitRejectReason, startPos, endPos);
            return;
        }

        if (runtimePointMode == RuntimePointMode.Search && !DoesJumpLinkImproveSearchTarget(traversalReference, worldPosition, anchor))
        {
            LogRejectedDynamicJumpLinkCandidate(link, directionLabel, "does not improve search target", startPos, endPos);
            return;
        }

        for (int i = 0; i < runtimePoints.Count; i++)
        {
            RuntimeSearchPoint existingPoint = runtimePoints[i];
            if (existingPoint.RequiresJumpLink && existingPoint.JumpLink == link && limitOneDynamicCandidatePerJumpLink)
            {
                LogRejectedDynamicJumpLinkCandidate(link, directionLabel, "one candidate per jump link limit", startPos, endPos);
                return;
            }

            if (Mathf.Abs(existingPoint.WorldPosition.x - worldPosition.x) < dynamicPointMinSeparation && Mathf.Abs(existingPoint.WorldPosition.y - worldPosition.y) < 0.5f)
            {
                LogRejectedDynamicJumpLinkCandidate(link, directionLabel, "duplicate runtime destination", startPos, endPos);
                return;
            }
        }

        float score = Mathf.Abs(worldPosition.x - anchor.x) + Mathf.Max(0f, link.TraversalCost);
        RuntimeSearchPoint candidate = new RuntimeSearchPoint(worldPosition, waitDuration, link, score, startPos, endPos);
        runtimePoints.Add(candidate);
    }

    private bool CanAddDynamicJumpLinkCandidate(out string rejectReason)
    {
        rejectReason = "None";

        if (!dynamicJumpLinksUseSeparateBudget)
        {
            if (runtimePoints.Count >= dynamicPointMaxCount)
            {
                rejectReason = "runtime point max count reached";
                return false;
            }

            return true;
        }

        if (CountRuntimeJumpPoints() >= dynamicJumpLinkMaxCount)
        {
            rejectReason = "dynamic jump-link max count reached";
            return false;
        }

        if (!dynamicJumpLinksCanExceedRuntimePointMaxCount && runtimePoints.Count >= dynamicPointMaxCount)
        {
            rejectReason = "runtime point max count reached";
            return false;
        }

        return true;
    }

    private bool IsDynamicJumpLinkExitAllowed(Vector3 anchor, Vector3 exitPosition, out string rejectReason)
    {
        rejectReason = "None";

        float xDistanceFromAnchor = Mathf.Abs(exitPosition.x - anchor.x);
        if (runtimePointMode == RuntimePointMode.DynamicPatrol && allowDynamicPatrolJumpExitOutsideWalkAnchorRadius)
        {
            if (useSeparateDynamicJumpLinkExitRadius && dynamicJumpLinkExitAnchorRadius > 0f && xDistanceFromAnchor > dynamicJumpLinkExitAnchorRadius)
            {
                rejectReason = "exit endpoint outside dynamic jump-link exit radius";
                return false;
            }

            return true;
        }

        float defaultRadius = dynamicPatrolAnchorRadius * 1.5f;
        if (xDistanceFromAnchor > defaultRadius)
        {
            rejectReason = "exit endpoint outside dynamic anchor radius";
            return false;
        }

        return true;
    }

    private bool ShouldMarkRuntimePointAsLastKnownInvestigation(Vector3 pointPosition, Vector3 anchor)
    {
        if (!applyLastKnownFacingHintOnArrival)
            return false;

        if (runtimePointMode != RuntimePointMode.Search || !searchRecoveryActive || !activeSearchRecoveryHasFacingHint)
            return false;

        float tolerance = Mathf.Max(lastKnownInvestigationPointTolerance, dynamicSearchArrivalDistance, 0.05f);
        return Vector2.Distance(
            new Vector2(pointPosition.x, pointPosition.y),
            new Vector2(anchor.x, anchor.y)) <= tolerance;
    }

    private RuntimeSearchPoint WithLastKnownFacingHint(RuntimeSearchPoint point)
    {
        int versionSnapshot = perception != null ? perception.LastKnownPositionVersion : observedSearchLastKnownVersion;
        Vector3 positionSnapshot = perception != null && perception.HasLastKnownPosition ? perception.LastKnownTargetPosition : activeSearchRecoveryTarget;
        bool hasFacingSnapshot = activeSearchRecoveryHasFacingHint;
        int facingSnapshotSign = activeSearchRecoveryFacingSign;
        string facingSnapshotMode = activeSearchRecoveryFacingMode;
        string facingSnapshotSource = activeSearchRecoveryFacingSource;

        if (point.RequiresJumpLink && point.HasExplicitJumpTraversal)
        {
            return new RuntimeSearchPoint(
                point.WorldPosition,
                point.WaitDuration,
                point.JumpLink,
                point.Score,
                point.JumpTraversalStart,
                point.JumpTraversalEnd,
                true,
                activeSearchRecoveryHasFacingHint,
                activeSearchRecoveryFacingSign,
                activeSearchRecoveryFacingMode,
                activeSearchRecoveryFacingSource,
                versionSnapshot,
                positionSnapshot,
                hasFacingSnapshot,
                facingSnapshotSign,
                facingSnapshotMode,
                facingSnapshotSource);
        }

        return new RuntimeSearchPoint(
            point.WorldPosition,
            point.WaitDuration,
            point.RequiresJumpLink,
            point.JumpLink,
            point.Score,
            true,
            activeSearchRecoveryHasFacingHint,
            activeSearchRecoveryFacingSign,
            activeSearchRecoveryFacingMode,
            activeSearchRecoveryFacingSource,
            versionSnapshot,
            positionSnapshot,
            hasFacingSnapshot,
            facingSnapshotSign,
            facingSnapshotMode,
            facingSnapshotSource);
    }

    private void ApplyLastKnownFacingHintOnArrival(RuntimeSearchPoint point, Vector3 currentReference, float distanceToPoint, float arrivalTolerance, int pointIndex)
    {
        string blockReason = GetSearchFacingHintApplyBlockReason(point);
        bool canApply = string.Equals(blockReason, "None", StringComparison.Ordinal);
        LogSearchFacingHintArrivalCheck(point, currentReference, distanceToPoint, arrivalTolerance, pointIndex, canApply, blockReason);

        if (!canApply)
        {
            LogSearchFacingHintSkipped(point, currentReference, distanceToPoint, arrivalTolerance, pointIndex, blockReason);
            return;
        }

        int sign = point.FacingHintSign >= 0 ? 1 : -1;
        int beforeSign = character != null ? character.FacingSign : 0;
        bool appliedBefore = activeSearchRecoveryFacingApplied;
        int previousAppliedVersion = activeSearchRecoveryFacingAppliedVersion;
        int appliedVersion = point.LastKnownVersionSnapshot > 0
            ? point.LastKnownVersionSnapshot
            : (perception != null ? perception.LastKnownPositionVersion : observedSearchLastKnownVersion);

        character.ForceFacingSign(sign);
        StartSearchFacingLock(point, sign, pointIndex);
        activeSearchRecoveryFacingApplied = true;
        activeSearchRecoveryFacingAppliedVersion = appliedVersion;
        int afterSign = character != null ? character.FacingSign : 0;

        if (appliedBefore && previousAppliedVersion != appliedVersion)
            LogSearchFacingVersionedApply(point, sign, previousAppliedVersion, appliedVersion, pointIndex, currentReference);

        LogAppliedLastKnownFacingHint(point, sign, beforeSign, afterSign, pointIndex, currentReference, appliedBefore);
        ScheduleSearchFacingHintPostApplyVerification(point, sign);
    }

    private string GetSearchFacingHintApplyBlockReason(RuntimeSearchPoint point)
    {
        if (!applyLastKnownFacingHintOnArrival)
            return "SettingDisabled";

        if (runtimePointMode != RuntimePointMode.Search)
            return "NotSearchMode";

        if (character == null)
            return "CharacterMissing";

        if (!point.IsLastKnownInvestigationPoint)
            return "PointIsNotLastKnownInvestigationPoint";

        if (!point.HasFacingHint)
            return "PointHasNoFacingHint";

        if (point.FacingHintSign == 0)
            return "PointFacingSignZero";

        if (!activeSearchRecoveryFacingApplied)
            return "None";

        if (!allowSearchFacingReapplyForNewLastKnownVersion)
            return "FacingAlreadyAppliedThisSearch";

        int pointVersion = point.LastKnownVersionSnapshot;
        if (pointVersion <= 0)
            return "FacingAlreadyAppliedThisSearchUnknownVersion";

        if (activeSearchRecoveryFacingAppliedVersion >= 0 && pointVersion == activeSearchRecoveryFacingAppliedVersion)
            return "FacingAlreadyAppliedForThisVersion";

        return "None";
    }

    private void StartSearchFacingLock(RuntimeSearchPoint point, int sign, int pointIndex)
    {
        if (!lockSearchFacingHintOnArrival || searchFacingHintLockDuration <= 0f || character == null)
            return;

        character.LockFacingSign(sign, searchFacingHintLockDuration, "SearchLastKnownArrival");
        LogSearchFacingLockStarted(point, sign, pointIndex, "SearchLastKnownArrival");
    }

    private void ClearSearchFacingLockFromBrain(string reason)
    {
        if (character == null)
            return;

        bool previousActive = character.IsTemporaryFacingLockActive;
        int previousSign = character.TemporaryFacingLockSign;
        string previousReason = character.TemporaryFacingLockReason;
        float previousRemaining = character.TemporaryFacingLockRemainingTime;

        character.ClearFacingLock(reason);
        LogSearchFacingLockCleared(reason, previousActive, previousSign, previousReason, previousRemaining);
    }

    private void LogSearchFacingLockStarted(RuntimeSearchPoint point, int sign, int pointIndex, string reason)
    {
        if (!logSearchFacingHintLock)
            return;

        string key = $"LockStarted|{pointIndex}|{FormatVector3ForLog(point.WorldPosition)}|{sign}|{point.LastKnownVersionSnapshot}";
        if (!ShouldLogSearchFacingHintDiagnostic(key))
            return;

        string message =
            $"[EnemyBrainBT25D] Search facing lock started\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"RuntimeIndex: {pointIndex}\n" +
            $"Point: {FormatVector3ForLog(point.WorldPosition)}\n" +
            $"FacingSign: {FormatFacingSignForLog(sign)}\n" +
            $"Duration: {searchFacingHintLockDuration:F2}\n" +
            $"Reason: {reason}\n" +
            $"PointLastKnownVersionSnapshot: {point.LastKnownVersionSnapshot}\n" +
            $"SearchRecoveryFacingAppliedVersion: {activeSearchRecoveryFacingAppliedVersion}\n" +
            $"CharacterLockActiveAfterCall: {(character != null && character.IsTemporaryFacingLockActive)}\n" +
            $"CharacterLockedFacingSignAfterCall: {(character != null ? FormatFacingSignForLog(character.TemporaryFacingLockSign) : "None")}";

        Debug.Log(message, this);
        if (writeSearchFacingHintLockLogsToFile)
            WriteEnemyDebugLogToFile("SearchFacingLockStarted", message);
    }

    private void LogSearchFacingLockCleared(string reason, bool previousActive, int previousSign, string previousReason, float previousRemaining)
    {
        if (!logSearchFacingHintLock || !previousActive)
            return;

        string key = $"LockCleared|{reason}|{previousSign}|{previousReason}|{currentState}|{currentAction}";
        if (!ShouldLogSearchFacingHintDiagnostic(key))
            return;

        string message =
            $"[EnemyBrainBT25D] Search facing lock cleared\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"ClearReason: {reason}\n" +
            $"PreviousLockActive: {previousActive}\n" +
            $"PreviousLockedSign: {FormatFacingSignForLog(previousSign)}\n" +
            $"PreviousLockReason: {previousReason}\n" +
            $"PreviousRemainingTime: {previousRemaining:F2}";

        Debug.Log(message, this);
        if (writeSearchFacingHintLockLogsToFile)
            WriteEnemyDebugLogToFile("SearchFacingLockCleared", message);
    }

    private void LogSearchFacingVersionedApply(RuntimeSearchPoint point, int sign, int previousAppliedVersion, int newPointVersion, int pointIndex, Vector3 currentReference)
    {
        if (!logSearchFacingVersionedApply)
            return;

        string key = $"VersionedApply|{previousAppliedVersion}|{newPointVersion}|{pointIndex}|{FormatVector3ForLog(point.WorldPosition)}|{sign}";
        if (!ShouldLogSearchFacingHintDiagnostic(key))
            return;

        string message =
            $"[EnemyBrainBT25D] Applied LastKnown facing for new version\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"RuntimeIndex: {pointIndex}\n" +
            $"Point: {FormatVector3ForLog(point.WorldPosition)}\n" +
            $"CurrentRef: {FormatVector3ForLog(currentReference)}\n" +
            $"PreviousAppliedVersion: {previousAppliedVersion}\n" +
            $"NewPointVersion: {newPointVersion}\n" +
            $"FacingSign: {FormatFacingSignForLog(sign)}\n" +
            $"FacingMode: {point.FacingHintMode}\n" +
            $"FacingSource: {point.FacingHintSource}\n" +
            $"Reason: NewLastKnownVersionAllowsFacingReapply";

        Debug.Log(message, this);
        if (writeSearchFacingHintDiagnosticsToFile)
            WriteEnemyDebugLogToFile("SearchFacingVersionedApply", message);
    }

    private void LogAppliedLastKnownFacingHint(RuntimeSearchPoint point, int sign, int beforeSign, int afterSign, int pointIndex, Vector3 currentReference, bool appliedBefore)
    {
        if (!logSearchFacingHint)
            return;

        string message =
            $"[EnemyBrainBT25D] Applied LastKnown facing on arrival\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"RuntimeIndex: {pointIndex}\n" +
            $"Point: {FormatVector3ForLog(point.WorldPosition)}\n" +
            $"CurrentRef: {FormatVector3ForLog(currentReference)}\n" +
            $"FacingSign: {FormatFacingSignForLog(sign)}\n" +
            $"FacingMode: {point.FacingHintMode}\n" +
            $"FacingSource: {point.FacingHintSource}\n" +
            $"BeforeFacingSign: {FormatFacingSignForLog(beforeSign)}\n" +
            $"AfterFacingSign: {FormatFacingSignForLog(afterSign)}\n" +
            $"PerceptionLastKnownVersion: {(perception != null ? perception.LastKnownPositionVersion : -1)}\n" +
            $"ObservedSearchLastKnownVersion: {observedSearchLastKnownVersion}\n" +
            $"PointLastKnownVersion: {point.LastKnownVersionSnapshot}\n" +
            $"PointLastKnownPositionSnapshot: {FormatVector3ForLog(point.LastKnownPositionSnapshot)}\n" +
            $"ActiveSearchRecoveryFacingAppliedBefore: {appliedBefore}\n" +
            $"ActiveSearchRecoveryFacingAppliedAfter: {activeSearchRecoveryFacingApplied}\n" +
            $"SearchRecoveryFacingAppliedVersion: {activeSearchRecoveryFacingAppliedVersion}\n" +
            $"Reason: SearchDynamic reached LastKnown investigation point";

        Debug.Log(message, this);
        if (writeSearchFacingHintLogsToFile)
            WriteEnemyDebugLogToFile("SearchFacingHint", message);
    }

    private void LogSearchFacingHintPointBuild(RuntimeSearchPoint point, Vector3 anchor, string reason, int pointIndex)
    {
        if (!logSearchFacingHintDiagnostics || !logSearchFacingHintPointBuild)
            return;

        string key = $"PointBuild|{observedSearchLastKnownVersion}|{pointIndex}|{FormatVector3ForLog(point.WorldPosition)}";
        if (!ShouldLogSearchFacingHintDiagnostic(key))
            return;

        float distancePointToAnchor = Vector2.Distance(new Vector2(point.WorldPosition.x, point.WorldPosition.y), new Vector2(anchor.x, anchor.y));
        Vector3 perceptionLastKnown = perception != null && perception.HasLastKnownPosition ? perception.LastKnownTargetPosition : Vector3.zero;
        float distancePointToLastKnown = perception != null && perception.HasLastKnownPosition
            ? Vector2.Distance(new Vector2(point.WorldPosition.x, point.WorldPosition.y), new Vector2(perceptionLastKnown.x, perceptionLastKnown.y))
            : -1f;

        string message =
            $"[EnemyBrainBT25D] Search facing hint point built\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"Mode: {runtimePointMode}\n" +
            $"Reason: {reason}\n" +
            $"RuntimeIndex: {pointIndex}\n" +
            $"Point: {FormatVector3ForLog(point.WorldPosition)}\n" +
            $"Anchor: {FormatVector3ForLog(anchor)}\n" +
            $"RawLastKnown: {FormatVector3ForLog(perceptionLastKnown)}\n" +
            $"ResolvedAnchor: {FormatVector3ForLog(anchor)}\n" +
            $"IsLastKnownInvestigationPoint: {point.IsLastKnownInvestigationPoint}\n" +
            $"HasFacingHint: {point.HasFacingHint}\n" +
            $"FacingSign: {(point.HasFacingHint ? FormatFacingSignForLog(point.FacingHintSign) : "None")}\n" +
            $"FacingMode: {point.FacingHintMode}\n" +
            $"FacingSource: {point.FacingHintSource}\n" +
            $"PerceptionLastKnownVersion: {(perception != null ? perception.LastKnownPositionVersion : -1)}\n" +
            $"PointLastKnownVersionSnapshot: {point.LastKnownVersionSnapshot}\n" +
            $"PointLastKnownPositionSnapshot: {FormatVector3ForLog(point.LastKnownPositionSnapshot)}\n" +
            $"HasAdjustedLastKnownSafePosition: {point.HasAdjustedLastKnownSafePosition}\n" +
            $"OriginalLastKnownPositionSnapshot: {FormatVector3ForLog(point.OriginalLastKnownPositionSnapshot)}\n" +
            $"AdjustedLastKnownSafePosition: {FormatVector3ForLog(point.AdjustedLastKnownSafePosition)}\n" +
            $"LastKnownSafeAdjustmentReason: {point.LastKnownSafeAdjustmentReason}\n" +
            $"LastKnownSafeAdjustmentDistance: {point.LastKnownSafeAdjustmentDistance:F2}\n" +
            $"PointHasLastKnownFacingHintSnapshot: {point.HasLastKnownFacingHintSnapshot}\n" +
            $"PointLastKnownFacingSignSnapshot: {(point.HasLastKnownFacingHintSnapshot ? FormatFacingSignForLog(point.LastKnownFacingSignSnapshot) : "None")}\n" +
            $"PointLastKnownFacingModeSnapshot: {point.LastKnownFacingModeSnapshot}\n" +
            $"PointLastKnownFacingSourceSnapshot: {point.LastKnownFacingSourceSnapshot}\n" +
            $"PerceptionLastKnownReason: {(perception != null ? perception.LastKnownUpdateReason : "None")}\n" +
            $"DistancePointToAnchor: {distancePointToAnchor:F2}\n" +
            $"DistancePointToLastKnown: {distancePointToLastKnown:F2}";

        Debug.Log(message, this);
        if (writeSearchFacingHintDiagnosticsToFile)
            WriteEnemyDebugLogToFile("SearchFacingHintPointBuild", message);
    }

    private void LogSearchFacingHintArrivalCheck(RuntimeSearchPoint point, Vector3 currentReference, float distanceToPoint, float arrivalTolerance, int pointIndex, bool canApply, string blockReason)
    {
        if (!logSearchFacingHintDiagnostics || !logSearchFacingHintArrivalChecks)
            return;

        string key = $"ArrivalCheck|{pointIndex}|{FormatVector3ForLog(point.WorldPosition)}|{blockReason}";
        if (!ShouldLogSearchFacingHintDiagnostic(key))
            return;

        string message =
            $"[EnemyBrainBT25D] Search facing hint arrival check\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"RuntimeIndex: {pointIndex}\n" +
            $"Point: {FormatVector3ForLog(point.WorldPosition)}\n" +
            $"CurrentRef: {FormatVector3ForLog(currentReference)}\n" +
            $"DistanceToPoint: {distanceToPoint:F2}\n" +
            $"ReachedDistance: {arrivalTolerance:F2}\n" +
            $"IsLastKnownInvestigationPoint: {point.IsLastKnownInvestigationPoint}\n" +
            $"PointHasFacingHint: {point.HasFacingHint}\n" +
            $"PointFacingSign: {(point.HasFacingHint ? FormatFacingSignForLog(point.FacingHintSign) : "None")}\n" +
            $"PointFacingMode: {point.FacingHintMode}\n" +
            $"PointFacingSource: {point.FacingHintSource}\n" +
            $"PointLastKnownVersionSnapshot: {point.LastKnownVersionSnapshot}\n" +
            $"PointLastKnownPositionSnapshot: {FormatVector3ForLog(point.LastKnownPositionSnapshot)}\n" +
            $"ActiveSearchAnchor: {FormatVector3ForLog(runtimePointsAnchor)}\n" +
            $"ActiveSearchTarget: {FormatVector3ForLog(activeSearchRecoveryTarget)}\n" +
            $"PerceptionLastKnown: {(perception != null && perception.HasLastKnownPosition ? FormatVector3ForLog(perception.LastKnownTargetPosition) : "None")}\n" +
            $"PerceptionLastKnownVersion: {(perception != null ? perception.LastKnownPositionVersion : -1)}\n" +
            $"PerceptionHasFacingHint: {(perception != null && perception.HasLastKnownFacingHint)}\n" +
            $"PerceptionFacingSign: {(perception != null && perception.HasLastKnownFacingHint ? FormatFacingSignForLog(perception.LastKnownFacingSign) : "None")}\n" +
            $"PerceptionFacingMode: {(perception != null && perception.HasLastKnownFacingHint ? perception.LastKnownFacingMode : "None")}\n" +
            $"PerceptionFacingSource: {(perception != null && perception.HasLastKnownFacingHint ? perception.LastKnownFacingSource : "None")}\n" +
            $"ActiveSearchRecoveryFacingApplied: {activeSearchRecoveryFacingApplied}\n" +
            $"SearchRecoveryFacingAppliedVersion: {activeSearchRecoveryFacingAppliedVersion}\n" +
            $"CanApply: {canApply}\n" +
            $"BlockReason: {blockReason}";

        Debug.Log(message, this);
        if (writeSearchFacingHintDiagnosticsToFile)
            WriteEnemyDebugLogToFile("SearchFacingHintArrivalCheck", message);
    }

    private void LogSearchFacingHintSkipped(RuntimeSearchPoint point, Vector3 currentReference, float distanceToPoint, float arrivalTolerance, int pointIndex, string reason)
    {
        if (!logSearchFacingHintDiagnostics || !logSearchFacingHintSkipped)
            return;

        string key = $"Skipped|{reason}|{pointIndex}|{FormatVector3ForLog(point.WorldPosition)}";
        if (!ShouldLogSearchFacingHintDiagnostic(key))
            return;

        string message =
            $"[EnemyBrainBT25D] Search facing hint skipped\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"Reason: {reason}\n" +
            $"RuntimeIndex: {pointIndex}\n" +
            $"Point: {FormatVector3ForLog(point.WorldPosition)}\n" +
            $"CurrentRef: {FormatVector3ForLog(currentReference)}\n" +
            $"DistanceToPoint: {distanceToPoint:F2}\n" +
            $"ReachedDistance: {arrivalTolerance:F2}\n" +
            $"IsLastKnownInvestigationPoint: {point.IsLastKnownInvestigationPoint}\n" +
            $"PointHasFacingHint: {point.HasFacingHint}\n" +
            $"PointFacingSign: {(point.HasFacingHint ? FormatFacingSignForLog(point.FacingHintSign) : "None")}\n" +
            $"PointFacingMode: {point.FacingHintMode}\n" +
            $"PointFacingSource: {point.FacingHintSource}\n" +
            $"PointLastKnownVersionSnapshot: {point.LastKnownVersionSnapshot}\n" +
            $"PointLastKnownPositionSnapshot: {FormatVector3ForLog(point.LastKnownPositionSnapshot)}\n" +
            $"PerceptionLastKnownVersion: {(perception != null ? perception.LastKnownPositionVersion : -1)}\n" +
            $"PerceptionFacingSign: {(perception != null && perception.HasLastKnownFacingHint ? FormatFacingSignForLog(perception.LastKnownFacingSign) : "None")}\n" +
            $"ActiveSearchRecoveryFacingApplied: {activeSearchRecoveryFacingApplied}\n" +
            $"SearchRecoveryFacingAppliedVersion: {activeSearchRecoveryFacingAppliedVersion}";

        Debug.Log(message, this);
        if (writeSearchFacingHintDiagnosticsToFile)
            WriteEnemyDebugLogToFile("SearchFacingHintSkipped", message);
    }

    private void ScheduleSearchFacingHintPostApplyVerification(RuntimeSearchPoint point, int expectedSign)
    {
        if (!logSearchFacingHintDiagnostics || !logSearchFacingHintPostApplyVerification)
            return;

        pendingSearchFacingHintPostApplyVerification = true;
        pendingSearchFacingHintVerifyFrame = Time.frameCount + 2;
        pendingSearchFacingHintVerifyTime = Time.time + 0.05f;
        pendingSearchFacingHintExpectedSign = expectedSign >= 0 ? 1 : -1;
        pendingSearchFacingHintPoint = point.WorldPosition;
        pendingSearchFacingHintSource = point.FacingHintSource;
        pendingSearchFacingHintMode = point.FacingHintMode;
        pendingSearchFacingHintAppliedFrame = Time.frameCount;
        pendingSearchFacingHintAppliedTime = Time.time;
    }

    private void UpdateSearchFacingHintPostApplyVerification()
    {
        if (!pendingSearchFacingHintPostApplyVerification)
            return;

        if (Time.frameCount < pendingSearchFacingHintVerifyFrame && Time.time < pendingSearchFacingHintVerifyTime)
            return;

        pendingSearchFacingHintPostApplyVerification = false;

        if (!logSearchFacingHintDiagnostics || !logSearchFacingHintPostApplyVerification)
            return;

        int actualSign = character != null ? character.FacingSign : 0;
        bool matches = actualSign != 0 && actualSign == pendingSearchFacingHintExpectedSign;
        string possibleOverwriteReason = GetSearchFacingHintPossibleOverwriteReason();
        string key = $"PostVerify|{FormatVector3ForLog(pendingSearchFacingHintPoint)}|{pendingSearchFacingHintExpectedSign}|{actualSign}|{possibleOverwriteReason}";
        if (!ShouldLogSearchFacingHintDiagnostic(key))
            return;

        string message =
            $"[EnemyBrainBT25D] Search facing hint post-apply verification\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"Point: {FormatVector3ForLog(pendingSearchFacingHintPoint)}\n" +
            $"ExpectedFacingSign: {FormatFacingSignForLog(pendingSearchFacingHintExpectedSign)}\n" +
            $"ActualFacingSign: {FormatFacingSignForLog(actualSign)}\n" +
            $"FacingStillMatches: {matches}\n" +
            $"ElapsedTime: {Mathf.Max(0f, Time.time - pendingSearchFacingHintAppliedTime):F2}\n" +
            $"FramesElapsed: {Mathf.Max(0, Time.frameCount - pendingSearchFacingHintAppliedFrame)}\n" +
            $"Source: {pendingSearchFacingHintSource}\n" +
            $"Mode: {pendingSearchFacingHintMode}\n" +
            $"PossibleOverwriteReason: {possibleOverwriteReason}\n" +
            $"CurrentMoveInput: {(character != null ? character.MoveInputX : 0f):F2}\n" +
            $"IsMoving: {(character != null && Mathf.Abs(character.MoveInputX) > 0.01f)}\n" +
            $"IsTraversalActive: {(character != null && character.IsJumpTraversalActive)}\n" +
            $"TargetVisible: {(perception != null && perception.IsTargetVisible)}\n" +
            $"CurrentState: {currentState}\n" +
            $"CurrentAction: {currentAction}\n" +
            $"TemporaryFacingLockActive: {(character != null && character.IsTemporaryFacingLockActive)}\n" +
            $"TemporaryFacingLockSign: {(character != null ? FormatFacingSignForLog(character.TemporaryFacingLockSign) : "None")}\n" +
            $"TemporaryFacingLockReason: {(character != null ? character.TemporaryFacingLockReason : "None")}\n" +
            $"TemporaryFacingLockRemainingTime: {(character != null ? character.TemporaryFacingLockRemainingTime : 0f):F2}";

        Debug.Log(message, this);
        if (writeSearchFacingHintDiagnosticsToFile)
            WriteEnemyDebugLogToFile("SearchFacingHintPostApplyVerify", message);
    }

    private string GetSearchFacingHintPossibleOverwriteReason()
    {
        if (character != null && character.IsJumpTraversalActive)
            return "TraversalActive";

        if (currentState == BrainState.Combat)
            return "Combat";

        if (perception != null && perception.IsTargetVisible)
            return "TargetVisible";

        if (currentAction == EnemyAction25D.MoveToDynamicPoint)
            return "Moving";

        if (runtimePointMode == RuntimePointMode.Search && searchRecoveryActive && observedSearchLastKnownVersion >= 0 && perception != null && perception.LastKnownPositionVersion != observedSearchLastKnownVersion)
            return "SearchRetargeted";

        return "Unknown";
    }

    private void LogSearchFacingHintReset(string reason, int previousRuntimePointCount, RuntimePointMode previousMode)
    {
        if (!logSearchFacingHintDiagnostics)
            return;

        string key = $"Reset|{reason}|{activeSearchRecoveryFacingApplied}|{activeSearchRecoveryFacingAppliedVersion}|{previousMode}|{previousRuntimePointCount}";
        if (!ShouldLogSearchFacingHintDiagnostic(key))
            return;

        string message =
            $"[EnemyBrainBT25D] Search facing hint reset\n" +
            $"Enemy: {name}\n" +
            $"Reason: {reason}\n" +
            $"PreviousActiveSearchRecoveryFacingApplied: {activeSearchRecoveryFacingApplied}\n" +
            $"PreviousAppliedVersion: {activeSearchRecoveryFacingAppliedVersion}\n" +
            $"NewActiveSearchRecoveryFacingApplied: False\n" +
            $"NewAppliedVersion: -1\n" +
            $"PreviousRuntimePointCount: {previousRuntimePointCount}\n" +
            $"PreviousMode: {previousMode}\n" +
            $"CurrentState: {currentState}\n" +
            $"CurrentAction: {currentAction}";

        Debug.Log(message, this);
        if (writeSearchFacingHintDiagnosticsToFile)
            WriteEnemyDebugLogToFile("SearchFacingHintReset", message);
    }

    private bool ShouldLogSearchFacingHintDiagnostic(string key)
    {
        if (!logSearchFacingHintDiagnostics)
            return false;

        key = string.IsNullOrEmpty(key) ? "None" : key;
        if (Time.frameCount == lastSearchFacingHintDiagnosticLogFrame && string.Equals(lastSearchFacingHintDiagnosticKey, key, StringComparison.Ordinal))
            return false;

        if (searchFacingHintDiagnosticCooldown > 0f && Time.time < lastSearchFacingHintDiagnosticLogTime + searchFacingHintDiagnosticCooldown && string.Equals(lastSearchFacingHintDiagnosticKey, key, StringComparison.Ordinal))
            return false;

        lastSearchFacingHintDiagnosticLogTime = Time.time;
        lastSearchFacingHintDiagnosticLogFrame = Time.frameCount;
        lastSearchFacingHintDiagnosticKey = key;
        return true;
    }

    private bool TryGetCurrentRuntimePoint(out RuntimeSearchPoint point)
    {
        if (runtimePoints.Count > 0)
        {
            int index = Mathf.Clamp(runtimePointIndex, 0, runtimePoints.Count - 1);
            point = runtimePoints[index];
            return true;
        }

        point = new RuntimeSearchPoint(Vector3.zero, 0f, false, null, 0f);
        return false;
    }

    private int CountRuntimePointsWithSearchFacingHint()
    {
        int count = 0;
        for (int i = 0; i < runtimePoints.Count; i++)
        {
            if (runtimePoints[i].HasFacingHint)
                count++;
        }
        return count;
    }

    private int CountLastKnownInvestigationRuntimePoints()
    {
        int count = 0;
        for (int i = 0; i < runtimePoints.Count; i++)
        {
            if (runtimePoints[i].IsLastKnownInvestigationPoint)
                count++;
        }
        return count;
    }

    private string GetSearchFacingHintPointLogSuffix(RuntimeSearchPoint point)
    {
        string adjustedSuffix = point.HasAdjustedLastKnownSafePosition
            ? $" adjustedLastKnown=True originalLastKnown={FormatVector3ForLog(point.OriginalLastKnownPositionSnapshot)} adjustedSafePoint={FormatVector3ForLog(point.AdjustedLastKnownSafePosition)} adjustmentDistance={point.LastKnownSafeAdjustmentDistance:F2} adjustmentReason={point.LastKnownSafeAdjustmentReason}"
            : " adjustedLastKnown=False";

        return $" lastKnownInvestigation={point.IsLastKnownInvestigationPoint} hasFacingHint={point.HasFacingHint} facing={(point.HasFacingHint ? FormatFacingSignForLog(point.FacingHintSign) : "None")} mode={point.FacingHintMode} source={point.FacingHintSource} version={point.LastKnownVersionSnapshot}{adjustedSuffix}";
    }

    private bool TickRuntimePointList(bool loop, float arrivalTolerance)
    {
        if (character == null)
            return true;

        if (runtimePoints.Count == 0)
        {
            character.StopMovement();
            return true;
        }

        runtimePointIndex = Mathf.Clamp(runtimePointIndex, 0, runtimePoints.Count - 1);
        RuntimeSearchPoint point = runtimePoints[runtimePointIndex];

        if (point.RequiresJumpLink && point.JumpLink != null)
        {
            if (runtimePointMode == RuntimePointMode.Search && IsRecentlyCompletedDynamicJumpLink(point.JumpLink, BrainState.SearchDynamic))
            {
                currentAction = EnemyAction25D.WaitAtPoint;
                character.StopMovement();
                runtimePointWaitEndTime = 0f;
                runtimePointIndex++;
                if (runtimePointIndex >= runtimePoints.Count)
                {
                    if (loop)
                        runtimePointIndex = 0;
                    else
                        return true;
                }
                return false;
            }

            if (runtimePointMode == RuntimePointMode.DynamicPatrol)
            {
                if (TryHandleDynamicPatrolDeadEndReturn(point))
                    return false;

                if (IsDynamicPatrolJumpPointBlockedByPostJumpRule(point))
                {
                    currentAction = EnemyAction25D.WaitAtPoint;
                    character.StopMovement();
                    runtimePointWaitEndTime = 0f;
                    runtimePointIndex++;
                    if (runtimePointIndex >= runtimePoints.Count)
                    {
                        if (loop)
                            runtimePointIndex = 0;
                        else
                            return true;
                    }
                    return false;
                }
            }

            bool jumpTraversalHandled = point.HasExplicitJumpTraversal
                ? TickJumpLinkTraversal(point.JumpLink, point.JumpTraversalStart, point.JumpTraversalEnd, arrivalTolerance, false)
                : TickJumpLinkTraversal(point.JumpLink, point.WorldPosition, arrivalTolerance, false);
            if (jumpTraversalHandled)
            {
                currentAction = EnemyAction25D.UseJumpLink;
                return false;
            }

            // Jump-linked runtime points should never degrade into ordinary horizontal patrol points.
            // If traversal cannot currently be handled, advance to the next point instead of walking to the
            // link endpoint directly and potentially falling off an edge.
            runtimePointWaitEndTime = 0f;
            runtimePointIndex++;
            if (runtimePointIndex >= runtimePoints.Count)
            {
                if (loop)
                    runtimePointIndex = 0;
                else
                    return true;
            }
            return false;
        }

        Vector3 currentReference = GetTraversalReferencePosition();
        float deltaX = point.WorldPosition.x - currentReference.x;
        float deltaY = point.WorldPosition.y - currentReference.y;
        if (Mathf.Abs(deltaX) <= arrivalTolerance && Mathf.Abs(deltaY) <= dynamicPointSamePlatformVerticalTolerance)
        {
            currentAction = EnemyAction25D.WaitAtPoint;
            character.StopMovement();
            ApplyLastKnownFacingHintOnArrival(point, currentReference, Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY), arrivalTolerance, runtimePointIndex);

            if (runtimePointWaitEndTime <= 0f)
                runtimePointWaitEndTime = Time.time + point.WaitDuration;

            if (Time.time < runtimePointWaitEndTime)
                return false;

            runtimePointWaitEndTime = 0f;
            if (runtimePointMode == RuntimePointMode.DynamicPatrol)
                NotifyDynamicPatrolWalkPointCompleted();
            runtimePointIndex++;
            if (runtimePointIndex >= runtimePoints.Count)
            {
                if (loop)
                    runtimePointIndex = 0;
                else
                    return true;
            }
            return false;
        }

        currentAction = EnemyAction25D.MoveToDynamicPoint;
        MoveTowardsX(point.WorldPosition.x);
        return false;
    }

    private bool IsWithinJumpLinkApproachTolerance(Vector3 currentPosition, Vector3 traversalStart, EnemyJumpLink25D link)
    {
        if (link == null)
            return false;

        float horizontalTolerance = Mathf.Max(0.01f, link.ApproachHorizontalTolerance);
        float verticalTolerance = Mathf.Max(0.01f, link.ApproachVerticalTolerance);
        return Mathf.Abs(currentPosition.x - traversalStart.x) <= horizontalTolerance
            && Mathf.Abs(currentPosition.y - traversalStart.y) <= verticalTolerance;
    }

    private bool IsWithinJumpLinkApproachLevel(Vector3 currentPosition, Vector3 traversalStart, EnemyJumpLink25D link)
    {
        if (link == null)
            return false;

        float softVerticalTolerance = Mathf.Max(0.75f, Mathf.Max(0.01f, link.ApproachVerticalTolerance) * 1.5f);
        return Mathf.Abs(currentPosition.y - traversalStart.y) <= softVerticalTolerance;
    }

    private Vector3 GetTraversalReferencePosition()
    {
        return character != null ? character.TraversalReferencePosition : transform.position;
    }

    private Vector3 GetRuntimePointNavigationTarget(RuntimeSearchPoint point)
    {
        if (point.RequiresJumpLink && point.HasExplicitJumpTraversal)
            return point.JumpTraversalStart;

        return point.WorldPosition;
    }

    private Vector3 GetRuntimePointFinalDestination(RuntimeSearchPoint point)
    {
        if (point.RequiresJumpLink && point.HasExplicitJumpTraversal)
            return point.JumpTraversalEnd;

        return point.WorldPosition;
    }

    private bool TickJumpLinkTraversal(EnemyJumpLink25D link, Vector3 desiredPosition, float arrivalTolerance, bool holdOnCooldown = true)
    {
        if (character == null || link == null)
            return false;

        if (character.IsJumpTraversalActive)
        {
            character.SetAllowWalkingOffEdgesForTraversal(true);
            currentAction = EnemyAction25D.UseJumpLink;
            activeTraversalLink = link;
            return true;
        }

        if (activeTraversalLink == link)
        {
            float effectiveArrivalTolerance = Mathf.Max(arrivalTolerance, link.LandingTolerance);
            Vector3 currentReferencePosition = GetTraversalReferencePosition();
            if (Vector2.Distance(new Vector2(currentReferencePosition.x, currentReferencePosition.y), new Vector2(desiredPosition.x, desiredPosition.y)) <= effectiveArrivalTolerance)
            {
                RegisterCompletedDynamicJumpLink(link, desiredPosition, desiredPosition);
                activeTraversalLink = null;
                return false;
            }
        }

        if (Time.time < lastJumpLinkUseTime + link.JumpCooldownAfterUse)
        {
            if (!holdOnCooldown)
                return false;

            character.SetAllowWalkingOffEdgesForTraversal(true);
            currentAction = EnemyAction25D.UseJumpLink;
            return true;
        }

        Vector3 traversalReference = character.TraversalReferencePosition;
        if (!link.TryGetTraversal(traversalReference, desiredPosition, out Vector3 traversalStart, out Vector3 traversalEnd))
            return false;

        return TickJumpLinkTraversal(link, traversalStart, traversalEnd, arrivalTolerance, holdOnCooldown);
    }

    private bool TickJumpLinkTraversal(EnemyJumpLink25D link, Vector3 traversalStart, Vector3 traversalEnd, float arrivalTolerance, bool holdOnCooldown = true)
    {
        if (character == null || link == null)
            return false;

        Vector3 desiredPosition = traversalEnd;

        if (character.IsJumpTraversalActive)
        {
            character.SetAllowWalkingOffEdgesForTraversal(true);
            currentAction = EnemyAction25D.UseJumpLink;
            activeTraversalLink = link;
            return true;
        }

        if (activeTraversalLink == link)
        {
            float effectiveArrivalTolerance = Mathf.Max(arrivalTolerance, link.LandingTolerance);
            Vector3 currentReferencePosition = GetTraversalReferencePosition();
            if (Vector2.Distance(new Vector2(currentReferencePosition.x, currentReferencePosition.y), new Vector2(desiredPosition.x, desiredPosition.y)) <= effectiveArrivalTolerance)
            {
                RegisterCompletedDynamicJumpLink(link, traversalStart, desiredPosition);
                activeTraversalLink = null;
                return false;
            }
        }

        if (Time.time < lastJumpLinkUseTime + link.JumpCooldownAfterUse)
        {
            if (!holdOnCooldown)
                return false;

            character.SetAllowWalkingOffEdgesForTraversal(true);
            currentAction = EnemyAction25D.UseJumpLink;
            return true;
        }

        Vector3 traversalReference = character.TraversalReferencePosition;
        float horizontalDeltaToStart = traversalStart.x - traversalReference.x;
        float horizontalTolerance = Mathf.Max(0.01f, link.ApproachHorizontalTolerance);

        if (!IsWithinJumpLinkApproachLevel(traversalReference, traversalStart, link))
            return false;

        if (Mathf.Abs(horizontalDeltaToStart) > horizontalTolerance)
        {
            character.SetAllowWalkingOffEdgesForTraversal(true);
            currentAction = EnemyAction25D.UseJumpLink;
            MoveTowardsX(traversalStart.x);
            return true;
        }

        if (!IsWithinJumpLinkApproachTolerance(traversalReference, traversalStart, link))
            return false;

        character.SetAllowWalkingOffEdgesForTraversal(true);
        currentAction = EnemyAction25D.UseJumpLink;
        character.StopMovement();
        if (character.TryExecuteJumpLinkTraversal(link, traversalStart, traversalEnd))
        {
            activeTraversalLink = link;
            lastJumpLinkUseTime = Time.time;
            RegisterStartedDynamicPatrolJumpLink(link, traversalStart, traversalEnd);
            return true;
        }

        return false;
    }

    private bool TryMaintainOrAcquireCover()
    {
        if (!useCover || perception == null || perception.CurrentTarget == null)
            return false;

        if (selectedCover != null)
        {
            if (IsCoverStillValid(selectedCover))
                return true;

            ReleaseSelectedCover();
        }

        if (Time.time < nextCoverSearchTime)
            return false;

        nextCoverSearchTime = Time.time + coverRepathInterval;
        selectedCover = FindBestCoverPoint();
        if (selectedCover != null)
        {
            selectedCover.TryClaim(transform);
            isPeeking = false;
            nextPeekToggleTime = 0f;
            return true;
        }

        return false;
    }

    private EnemyCoverPoint25D FindBestCoverPoint()
    {
        EnemyCoverPoint25D[] all = FindObjectsByType<EnemyCoverPoint25D>(FindObjectsSortMode.None);
        if (all == null || all.Length == 0 || perception == null || perception.CurrentTarget == null)
            return null;

        Transform target = perception.CurrentTarget;
        Vector3 targetPos = perception.GetAimPosition();
        EnemyCoverPoint25D best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < all.Length; i++)
        {
            EnemyCoverPoint25D cover = all[i];
            if (cover == null || !cover.CanBeUsedBy(transform))
                continue;

            Transform stand = cover.StandPoint;
            if (stand == null)
                continue;

            float selfDistance = Mathf.Abs(stand.position.x - transform.position.x);
            if (selfDistance > Mathf.Min(coverSearchRadius, Mathf.Max(cover.MaxUseDistance, 0.01f)))
                continue;

            float targetDistance = Mathf.Abs(stand.position.x - targetPos.x);
            if (targetDistance < coverMinDistanceFromTarget)
                continue;

            float score = -selfDistance + cover.CoverScoreBias;
            if (cover.RequireBlockingLineOfSight)
            {
                Vector3 coverEye = stand.position;
                Vector3 toTarget = targetPos - coverEye;
                toTarget.z = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    RaycastHit[] hits = Physics.RaycastAll(coverEye, toTarget.normalized, toTarget.magnitude, perception.ObstructionMask, QueryTriggerInteraction.Ignore);
                    bool blocked = false;
                    if (hits != null && hits.Length > 0)
                    {
                        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                        for (int j = 0; j < hits.Length; j++)
                        {
                            if (hits[j].collider == null)
                                continue;
                            Transform hitTransform = hits[j].collider.transform;
                            if (hitTransform == target || hitTransform.IsChildOf(target))
                                continue;
                            blocked = true;
                            break;
                        }
                    }

                    if (!blocked)
                        continue;

                    score += 2f;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = cover;
            }
        }

        return best;
    }

    private bool IsCoverStillValid(EnemyCoverPoint25D cover)
    {
        if (cover == null || !cover.CanBeUsedBy(transform))
            return false;

        Transform stand = cover.StandPoint;
        if (stand == null)
            return false;

        float selfDistance = Mathf.Abs(stand.position.x - transform.position.x);
        if (selfDistance > Mathf.Min(coverSearchRadius * 1.5f, Mathf.Max(cover.MaxUseDistance, 0.01f) * 1.5f))
            return false;

        return true;
    }

    private void UpdatePeekState()
    {
        if (Time.time < nextPeekToggleTime)
            return;

        isPeeking = !isPeeking;
        nextPeekToggleTime = Time.time + (isPeeking
            ? UnityEngine.Random.Range(peekDurationMin, peekDurationMax)
            : UnityEngine.Random.Range(hideDurationMin, hideDurationMax));
    }

    private void RegisterStartedDynamicPatrolJumpLink(EnemyJumpLink25D link, Vector3 traversalStart, Vector3 traversalEnd)
    {
        if (link == null)
            return;

        if (currentState != BrainState.PatrolDynamic || runtimePointMode != RuntimePointMode.DynamicPatrol)
            return;

        int requiredWalks = Mathf.Max(0, dynamicPatrolWalkPointsRequiredAfterJumpLink);
        dynamicPatrolWalkPointsRemainingBeforeJumpLinkAllowed = requiredWalks;
        lastDynamicPatrolJumpLink = link;
        lastDynamicPatrolJumpStart = traversalStart;
        lastDynamicPatrolJumpEnd = traversalEnd;
        hasLastDynamicPatrolJump = requiredWalks > 0;
        CancelDynamicPatrolDeadEndDelay();
    }

    private void RegisterCompletedDynamicJumpLink(EnemyJumpLink25D link, Vector3 traversalStart, Vector3 endPosition)
    {
        if (link == null)
            return;

        if (currentState != BrainState.PatrolDynamic && currentState != BrainState.SearchDynamic)
            return;

        lastCompletedDynamicJumpLink = link;
        lastCompletedDynamicJumpLinkState = currentState;
        lastCompletedDynamicJumpLinkTime = Time.time;
        lastCompletedDynamicJumpLinkEnd = endPosition;

        if (currentState == BrainState.PatrolDynamic)
        {
            int requiredWalks = Mathf.Max(0, dynamicPatrolWalkPointsRequiredAfterJumpLink);
            dynamicPatrolWalkPointsRemainingBeforeJumpLinkAllowed = requiredWalks;
            lastDynamicPatrolJumpLink = link;
            lastDynamicPatrolJumpStart = traversalStart;
            lastDynamicPatrolJumpEnd = endPosition;
            hasLastDynamicPatrolJump = requiredWalks > 0;
            CancelDynamicPatrolDeadEndDelay();
        }
    }

    private bool IsRecentlyCompletedDynamicJumpLink(EnemyJumpLink25D link, BrainState contextState)
    {
        if (link == null || lastCompletedDynamicJumpLink != link)
            return false;

        float blockDuration = 0f;
        if (contextState == BrainState.PatrolDynamic)
            return false;
        else if (contextState == BrainState.SearchDynamic)
            blockDuration = dynamicSearchJumpLinkRepeatBlockDuration;
        else
            return false;

        return Time.time < lastCompletedDynamicJumpLinkTime + blockDuration;
    }

    private bool DoesJumpLinkImproveSearchTarget(Vector3 currentReferencePosition, Vector3 jumpEndPosition, Vector3 searchTarget)
    {
        Vector2 current = new Vector2(currentReferencePosition.x, currentReferencePosition.y);
        Vector2 jumpEnd = new Vector2(jumpEndPosition.x, jumpEndPosition.y);
        Vector2 target = new Vector2(searchTarget.x, searchTarget.y);

        float currentDistance = Vector2.Distance(current, target);
        float endDistance = Vector2.Distance(jumpEnd, target);
        return currentDistance - endDistance >= dynamicSearchJumpLinkMinProgress;
    }

    private bool IsAtPoint(Vector3 point, float tolerance)
    {
        return Mathf.Abs(point.x - GetTraversalReferencePosition().x) <= tolerance;
    }

    private EnemyJumpLink25D FindBestRecoveryJumpLinkToPoint(Vector3 recoveryPoint, out Vector3 bestTraversalStart, out Vector3 bestTraversalEnd)
    {
        bestTraversalStart = Vector3.zero;
        bestTraversalEnd = Vector3.zero;

        EnemyJumpLink25D[] allLinks = FindObjectsByType<EnemyJumpLink25D>(FindObjectsSortMode.None);
        if (allLinks == null || allLinks.Length == 0)
            return null;

        EnemyJumpLink25D bestLink = null;
        float bestScore = float.NegativeInfinity;
        Vector3 traversalReference = GetTraversalReferencePosition();
        float currentVerticalGap = Mathf.Abs(recoveryPoint.y - traversalReference.y);
        float currentHorizontalGap = Mathf.Abs(recoveryPoint.x - traversalReference.x);

        for (int i = 0; i < allLinks.Length; i++)
        {
            EnemyJumpLink25D link = allLinks[i];
            if (link == null || !link.EnabledLink)
                continue;

            if (IsRecentlyCompletedDynamicJumpLink(link, BrainState.SearchDynamic))
                continue;

            Transform serializedStart = link.StartPoint;
            Transform serializedEnd = link.EndPoint;
            if (serializedStart == null || serializedEnd == null)
                continue;

            TryEvaluateRecoveryJumpLinkDirection(
                link,
                recoveryPoint,
                traversalReference,
                currentVerticalGap,
                currentHorizontalGap,
                serializedStart.position,
                serializedEnd.position,
                ref bestLink,
                ref bestScore,
                ref bestTraversalStart,
                ref bestTraversalEnd);

            if (link.Bidirectional)
            {
                TryEvaluateRecoveryJumpLinkDirection(
                    link,
                    recoveryPoint,
                    traversalReference,
                    currentVerticalGap,
                    currentHorizontalGap,
                    serializedEnd.position,
                    serializedStart.position,
                    ref bestLink,
                    ref bestScore,
                    ref bestTraversalStart,
                    ref bestTraversalEnd);
            }
        }

        return bestLink;
    }

    private void TryEvaluateRecoveryJumpLinkDirection(
        EnemyJumpLink25D link,
        Vector3 recoveryPoint,
        Vector3 traversalReference,
        float currentVerticalGap,
        float currentHorizontalGap,
        Vector3 traversalStart,
        Vector3 traversalEnd,
        ref EnemyJumpLink25D bestLink,
        ref float bestScore,
        ref Vector3 bestTraversalStart,
        ref Vector3 bestTraversalEnd)
    {
        if (link == null)
            return;

        if (!IsDynamicJumpLinkEndpointReachable(traversalReference, traversalStart))
            return;

        if (!DoesJumpLinkImproveSearchTarget(traversalReference, traversalEnd, recoveryPoint))
            return;

        float startDistance = Mathf.Abs(traversalStart.x - traversalReference.x);
        if (startDistance > jumpLinkSearchRadius)
            return;

        float postVerticalGap = Mathf.Abs(recoveryPoint.y - traversalEnd.y);
        float verticalImprovement = currentVerticalGap - postVerticalGap;
        bool sameLevel = postVerticalGap <= combatJumpLinkSameLevelTolerance;
        if (!sameLevel && verticalImprovement < combatJumpLinkRequiredVerticalImprovement)
            return;

        float postHorizontalGap = Mathf.Abs(recoveryPoint.x - traversalEnd.x);
        if (postHorizontalGap > combatJumpLinkAcceptablePostJumpHorizontalGap && !sameLevel && verticalImprovement < combatJumpLinkRequiredVerticalImprovement * 1.5f)
            return;

        float score = 0f;
        score += verticalImprovement * 8f;
        if (sameLevel)
            score += 5f;
        score += (currentHorizontalGap - postHorizontalGap) * 0.5f;
        score += Mathf.Max(0f, combatJumpLinkAcceptablePostJumpHorizontalGap - postHorizontalGap) * 0.25f;
        score -= startDistance * 0.5f;
        score -= Mathf.Max(0f, link.TraversalCost) * 0.35f;

        if (score > bestScore)
        {
            bestScore = score;
            bestLink = link;
            bestTraversalStart = traversalStart;
            bestTraversalEnd = traversalEnd;
        }
    }

    private bool ShouldTryCombatJumpLinkPursuit(Transform target, Vector3 navigationPosition, float distanceToNavigationTarget)
    {
        if (!allowCombatJumpLinkPursuit)
        {
            LogCombatJumpLinkEligibility("DisabledBySetting", target, navigationPosition, distanceToNavigationTarget, false);
            return false;
        }

        if (character == null || perception == null)
        {
            LogCombatJumpLinkEligibility("MissingCharacterOrPerception", target, navigationPosition, distanceToNavigationTarget, false);
            return false;
        }

        if (target == null)
        {
            LogCombatJumpLinkEligibility("TargetNull", target, navigationPosition, distanceToNavigationTarget, false);
            return false;
        }

        if (isApproachingCombatJumpLink || character.IsJumpTraversalActive)
        {
            LogCombatJumpLinkEligibility("AlreadyApproachingOrTraversing", target, navigationPosition, distanceToNavigationTarget, false);
            return false;
        }

        if (Time.time < nextCombatJumpLinkDecisionTime)
        {
            LogCombatJumpLinkEligibility("DecisionCooldown", target, navigationPosition, distanceToNavigationTarget, false);
            return false;
        }

        if ((stun != null && stun.IsStunned) || (health != null && health.IsDead) || (knockback != null && (knockback.IsLaunched || knockback.IsRecovering)))
        {
            LogCombatJumpLinkEligibility("EnemyStunnedDeadLaunchedOrRecovering", target, navigationPosition, distanceToNavigationTarget, false);
            return false;
        }

        if ((grenadeThrower != null && grenadeThrower.IsInAnyGrenadeExclusiveState) || (closeRangeRepel != null && closeRangeRepel.IsInRepelFlow) || selectedCover != null)
        {
            LogCombatJumpLinkEligibility("GrenadeRepelOrCoverExclusive", target, navigationPosition, distanceToNavigationTarget, false);
            return false;
        }

        if (!perception.IsTargetVisible)
        {
            LogCombatJumpLinkEligibility("TargetNotVisible", target, navigationPosition, distanceToNavigationTarget, false);
            return false;
        }

        if (IsDirectCombatMovementViableToNavigationPosition(navigationPosition, distanceToNavigationTarget))
        {
            LogCombatJumpLinkEligibility("DirectCombatMovementViable", target, navigationPosition, distanceToNavigationTarget, false);
            return false;
        }

        LogCombatJumpLinkEligibility("AllowedToSearchCombatJumpLink", target, navigationPosition, distanceToNavigationTarget, true);
        return true;
    }

    private bool IsDirectCombatMovementViableToNavigationPosition(Vector3 navigationPosition, float distanceToNavigationTarget)
    {
        if (!requireJumpLinkForCrossPlatformCombatMovement)
            return true;

        Vector3 traversalReference = GetTraversalReferencePosition();
        float verticalGap = Mathf.Abs(navigationPosition.y - traversalReference.y);

        if (verticalGap <= combatNavigationSamePlatformVerticalTolerance)
            return IsNormallyReachableDynamicPoint(traversalReference, navigationPosition, combatNavigationSamePlatformVerticalTolerance);

        if (verticalGap <= combatJumpLinkAbortVerticalDelta && distanceToNavigationTarget <= desiredCombatMaxRange + 0.5f)
            return IsNormallyReachableDynamicPoint(
                traversalReference,
                navigationPosition,
                Mathf.Max(combatNavigationSamePlatformVerticalTolerance, combatJumpLinkAbortVerticalDelta));

        return false;
    }

    private bool TryFindBestCombatJumpLinkToTarget(Vector3 navigationPosition, out EnemyJumpLink25D bestLink, out Vector3 bestTraversalStart, out Vector3 bestTraversalEnd)
    {
        bestLink = null;
        bestTraversalStart = Vector3.zero;
        bestTraversalEnd = Vector3.zero;

        EnemyJumpLink25D[] allLinks = FindObjectsByType<EnemyJumpLink25D>(FindObjectsSortMode.None);
        Vector3 traversalReference = GetTraversalReferencePosition();
        Vector3 targetPosition = perception != null && perception.CurrentTarget != null ? perception.CurrentTarget.position : navigationPosition;
        Vector3 aimPosition = perception != null ? perception.GetAimPosition() : navigationPosition;
        bool directMovementViable = IsDirectCombatMovementViableToNavigationPosition(navigationPosition, Vector3.Distance(traversalReference, navigationPosition));

        int totalLinks = allLinks != null ? allLinks.Length : 0;
        int enabledLinks = 0;
        int traversalResolvedCandidates = 0;
        int viableCandidates = 0;
        int rejectedCandidates = 0;
        float bestScore = float.NegativeInfinity;
        CombatJumpLinkCandidateDebugInfo bestRejectedCandidate = default(CombatJumpLinkCandidateDebugInfo);
        bool hasBestRejectedCandidate = false;
        List<CombatJumpLinkCandidateDebugInfo> rejectedDebugInfos = logCombatJumpLinkDiagnostics ? new List<CombatJumpLinkCandidateDebugInfo>() : null;

        if (allLinks != null)
        {
            for (int i = 0; i < allLinks.Length; i++)
            {
                EnemyJumpLink25D link = allLinks[i];
                CombatJumpLinkCandidateDebugInfo debugInfo = CreateCombatJumpLinkDebugInfo(link, targetPosition, navigationPosition, traversalReference);

                if (link == null)
                {
                    debugInfo.RejectReason = "LinkNull";
                    RecordRejectedCombatJumpLinkCandidate(debugInfo, ref rejectedCandidates, ref hasBestRejectedCandidate, ref bestRejectedCandidate, rejectedDebugInfos);
                    continue;
                }

                if (!link.EnabledLink)
                {
                    debugInfo.RejectReason = "LinkDisabled";
                    RecordRejectedCombatJumpLinkCandidate(debugInfo, ref rejectedCandidates, ref hasBestRejectedCandidate, ref bestRejectedCandidate, rejectedDebugInfos);
                    continue;
                }

                enabledLinks++;

                if (link.StartPoint == null || link.EndPoint == null)
                {
                    debugInfo.RejectReason = "MissingStartOrEndPoint";
                    RecordRejectedCombatJumpLinkCandidate(debugInfo, ref rejectedCandidates, ref hasBestRejectedCandidate, ref bestRejectedCandidate, rejectedDebugInfos);
                    continue;
                }

                if (!link.TryGetTraversal(traversalReference, navigationPosition, out Vector3 traversalStart, out Vector3 traversalEnd))
                {
                    debugInfo.RejectReason = "TryGetTraversalFailed";
                    RecordRejectedCombatJumpLinkCandidate(debugInfo, ref rejectedCandidates, ref hasBestRejectedCandidate, ref bestRejectedCandidate, rejectedDebugInfos);
                    continue;
                }

                traversalResolvedCandidates++;
                debugInfo.TraversalResolved = true;
                debugInfo.TraversalStart = traversalStart;
                debugInfo.TraversalEnd = traversalEnd;
                debugInfo.Direction = GetCombatJumpLinkDirectionLabel(link, traversalStart, traversalEnd);

                if (!IsCombatJumpLinkViableDetailed(link, navigationPosition, traversalStart, traversalEnd, ref debugInfo))
                {
                    RecordRejectedCombatJumpLinkCandidate(debugInfo, ref rejectedCandidates, ref hasBestRejectedCandidate, ref bestRejectedCandidate, rejectedDebugInfos);
                    continue;
                }

                viableCandidates++;
                debugInfo.Viable = true;
                debugInfo.RejectReason = "CandidateViable";

                if (debugInfo.Score > bestScore)
                {
                    bestScore = debugInfo.Score;
                    bestLink = link;
                    bestTraversalStart = traversalStart;
                    bestTraversalEnd = traversalEnd;
                }
            }
        }

        if (bestLink != null)
        {
            LogCombatJumpLinkSelected(bestLink, targetPosition, navigationPosition, bestTraversalStart, bestTraversalEnd, bestScore);
            return true;
        }

        if (rejectedDebugInfos != null)
        {
            for (int i = 0; i < rejectedDebugInfos.Count; i++)
                LogCombatJumpLinkRejected(rejectedDebugInfos[i]);
        }

        LogCombatJumpLinkSearchSummary(
            aimPosition,
            targetPosition,
            navigationPosition,
            traversalReference,
            directMovementViable,
            totalLinks,
            enabledLinks,
            traversalResolvedCandidates,
            viableCandidates,
            rejectedCandidates,
            hasBestRejectedCandidate,
            bestRejectedCandidate,
            "No combat JumpLink found");

        return false;
    }

    private CombatJumpLinkCandidateDebugInfo CreateCombatJumpLinkDebugInfo(EnemyJumpLink25D link, Vector3 targetPosition, Vector3 navigationPosition, Vector3 traversalReference)
    {
        return new CombatJumpLinkCandidateDebugInfo
        {
            Link = link,
            Direction = "Unresolved",
            TraversalResolved = false,
            Viable = false,
            RejectReason = "Unspecified",
            TraversalStart = Vector3.zero,
            TraversalEnd = Vector3.zero,
            CurrentReference = traversalReference,
            TargetPosition = targetPosition,
            NavigationPosition = navigationPosition,
            MaxStartDistance = combatJumpLinkMaxStartDistance,
            RequiredVerticalImprovement = combatJumpLinkRequiredVerticalImprovement,
            SameLevelTolerance = combatJumpLinkSameLevelTolerance,
            AcceptablePostJumpHorizontalGap = combatJumpLinkAcceptablePostJumpHorizontalGap,
            Score = float.NegativeInfinity,
        };
    }

    private void RecordRejectedCombatJumpLinkCandidate(
        CombatJumpLinkCandidateDebugInfo debugInfo,
        ref int rejectedCandidates,
        ref bool hasBestRejectedCandidate,
        ref CombatJumpLinkCandidateDebugInfo bestRejectedCandidate,
        List<CombatJumpLinkCandidateDebugInfo> rejectedDebugInfos)
    {
        rejectedCandidates++;
        rejectedDebugInfos?.Add(debugInfo);

        if (!hasBestRejectedCandidate || debugInfo.Score > bestRejectedCandidate.Score)
        {
            hasBestRejectedCandidate = true;
            bestRejectedCandidate = debugInfo;
        }
    }

    private bool DoesJumpLinkAcquireNavigationLevel(EnemyJumpLink25D link, Vector3 navigationPosition, Vector3 traversalEnd, float currentVerticalGap, out float postVerticalGap, out float verticalImprovement)
    {
        postVerticalGap = float.PositiveInfinity;
        verticalImprovement = 0f;

        if (link == null)
            return false;

        postVerticalGap = Mathf.Abs(navigationPosition.y - traversalEnd.y);
        verticalImprovement = currentVerticalGap - postVerticalGap;

        if (postVerticalGap <= combatJumpLinkSameLevelTolerance)
            return true;

        return verticalImprovement >= combatJumpLinkRequiredVerticalImprovement;
    }

    private bool IsCombatJumpLinkViable(EnemyJumpLink25D link, Vector3 navigationPosition, Vector3 traversalStart, Vector3 traversalEnd)
    {
        Vector3 traversalReference = GetTraversalReferencePosition();
        Vector3 targetPosition = perception != null && perception.CurrentTarget != null ? perception.CurrentTarget.position : navigationPosition;
        CombatJumpLinkCandidateDebugInfo debugInfo = CreateCombatJumpLinkDebugInfo(link, targetPosition, navigationPosition, traversalReference);
        debugInfo.TraversalResolved = true;
        debugInfo.TraversalStart = traversalStart;
        debugInfo.TraversalEnd = traversalEnd;
        debugInfo.Direction = GetCombatJumpLinkDirectionLabel(link, traversalStart, traversalEnd);
        return IsCombatJumpLinkViableDetailed(link, navigationPosition, traversalStart, traversalEnd, ref debugInfo);
    }

    private bool IsCombatJumpLinkViableDetailed(EnemyJumpLink25D link, Vector3 navigationPosition, Vector3 traversalStart, Vector3 traversalEnd, ref CombatJumpLinkCandidateDebugInfo debugInfo)
    {
        if (link == null)
        {
            debugInfo.RejectReason = "LinkNull";
            return false;
        }

        Vector3 traversalReference = GetTraversalReferencePosition();
        debugInfo.CurrentReference = traversalReference;
        debugInfo.TraversalStart = traversalStart;
        debugInfo.TraversalEnd = traversalEnd;
        debugInfo.Direction = GetCombatJumpLinkDirectionLabel(link, traversalStart, traversalEnd);

        float startDistance = Mathf.Abs(traversalStart.x - traversalReference.x);
        debugInfo.StartDistance = startDistance;
        debugInfo.MaxStartDistance = combatJumpLinkMaxStartDistance;
        if (startDistance > combatJumpLinkMaxStartDistance)
        {
            debugInfo.RejectReason = "StartDistanceTooFar";
            return false;
        }

        if (!IsDynamicJumpLinkEndpointReachable(traversalReference, traversalStart))
        {
            debugInfo.RejectReason = "EntryEndpointNotReachable";
            return false;
        }

        float currentVerticalGap = Mathf.Abs(navigationPosition.y - traversalReference.y);
        debugInfo.CurrentVerticalGap = currentVerticalGap;
        if (currentVerticalGap < combatNavigationSamePlatformVerticalTolerance)
        {
            debugInfo.RejectReason = "CurrentVerticalGapTooSmall";
            return false;
        }

        if (!DoesJumpLinkAcquireNavigationLevel(link, navigationPosition, traversalEnd, currentVerticalGap, out float postVerticalGap, out float verticalImprovement))
        {
            debugInfo.PostVerticalGap = postVerticalGap;
            debugInfo.VerticalImprovement = verticalImprovement;
            debugInfo.RejectReason = "DoesNotAcquireTargetLevel";
            return false;
        }

        debugInfo.PostVerticalGap = postVerticalGap;
        debugInfo.VerticalImprovement = verticalImprovement;

        float postHorizontalGap = Mathf.Abs(navigationPosition.x - traversalEnd.x);
        float allowableHorizontalGap = combatJumpLinkAcceptablePostJumpHorizontalGap;

        if (postVerticalGap <= combatJumpLinkSameLevelTolerance)
            allowableHorizontalGap *= 1.75f;
        else if (verticalImprovement >= combatJumpLinkRequiredVerticalImprovement * 2f)
            allowableHorizontalGap *= 1.35f;

        debugInfo.PostHorizontalGap = postHorizontalGap;
        debugInfo.AcceptablePostJumpHorizontalGap = allowableHorizontalGap;
        if (postHorizontalGap > allowableHorizontalGap)
        {
            debugInfo.RejectReason = "PostHorizontalGapTooLarge";
            return false;
        }

        float startToTargetGap = Mathf.Abs(navigationPosition.x - traversalStart.x);
        float currentHorizontalGap = Mathf.Abs(navigationPosition.x - traversalReference.x);
        debugInfo.StartToTargetGap = startToTargetGap;
        debugInfo.CurrentHorizontalGap = currentHorizontalGap;
        if (startToTargetGap > currentHorizontalGap + combatJumpLinkAcceptablePostJumpHorizontalGap)
        {
            debugInfo.RejectReason = "StartToTargetGapTooLarge";
            return false;
        }

        debugInfo.Score = ScoreCombatJumpLink(link, navigationPosition, traversalStart, traversalEnd, currentVerticalGap, postVerticalGap, currentHorizontalGap, postHorizontalGap);
        debugInfo.Viable = true;
        debugInfo.RejectReason = "CandidateViable";
        return true;
    }

    private float ScoreCombatJumpLink(EnemyJumpLink25D link, Vector3 navigationPosition, Vector3 traversalStart, Vector3 traversalEnd, float currentVerticalGap, float postVerticalGap, float currentHorizontalGap, float postHorizontalGap)
    {
        Vector3 traversalReference = GetTraversalReferencePosition();
        float verticalImprovement = currentVerticalGap - postVerticalGap;
        bool acquiresTargetLevel = postVerticalGap <= combatJumpLinkSameLevelTolerance;
        float startDistance = Mathf.Abs(traversalStart.x - traversalReference.x);

        float score = 0f;
        score += verticalImprovement * 8f;
        if (acquiresTargetLevel)
            score += 6f;

        score += (currentHorizontalGap - postHorizontalGap) * 0.65f;
        score += Mathf.Max(0f, combatJumpLinkAcceptablePostJumpHorizontalGap - postHorizontalGap) * 0.35f;
        score -= startDistance * 0.65f;
        score -= postHorizontalGap * 0.2f;
        score -= postVerticalGap * 0.35f;
        score -= Mathf.Max(0f, link.TraversalCost) * 0.5f;
        return score;
    }

    private void BeginCombatJumpLinkApproach(EnemyJumpLink25D link, Vector3 navigationPosition, Vector3 traversalStart, Vector3 traversalEnd)
    {
        if (link == null)
            return;

        activeCombatJumpLink = link;
        isApproachingCombatJumpLink = true;
        nextCombatJumpLinkDecisionTime = Time.time + combatJumpLinkDecisionCooldown;
        nextCombatJumpLinkAbortCheckTime = Time.time;

        combatJumpLinkTargetSnapshot = navigationPosition;
        activeCombatJumpLinkStartPoint = traversalStart;
        activeCombatJumpLinkEndPoint = traversalEnd;
    }

    private void CancelCombatJumpLinkApproach()
    {
        activeCombatJumpLink = null;
        isApproachingCombatJumpLink = false;
        nextCombatJumpLinkAbortCheckTime = float.NegativeInfinity;
        activeCombatJumpLinkStartPoint = Vector3.zero;
        activeCombatJumpLinkEndPoint = Vector3.zero;
    }

    private bool ShouldAbortCombatJumpLinkApproach(Transform target, Vector3 navigationPosition)
    {
        if (!isApproachingCombatJumpLink || activeCombatJumpLink == null)
            return true;

        if (target == null || perception == null)
            return true;

        if ((stun != null && stun.IsStunned) || (health != null && health.IsDead) || (knockback != null && (knockback.IsLaunched || knockback.IsRecovering)))
            return true;

        if ((grenadeThrower != null && grenadeThrower.IsInAnyGrenadeExclusiveState) || (closeRangeRepel != null && closeRangeRepel.IsInRepelFlow))
            return true;

        Vector3 traversalReference = GetTraversalReferencePosition();
        float distanceToNavigationTarget = Vector3.Distance(traversalReference, navigationPosition);
        if (IsDirectCombatMovementViableToNavigationPosition(navigationPosition, distanceToNavigationTarget))
            return true;

        if (abortApproachIfTargetDescends)
        {
            bool activeLinkGoesUp = activeCombatJumpLinkEndPoint.y > traversalReference.y + combatNavigationSamePlatformVerticalTolerance;
            if (activeLinkGoesUp)
            {
                Vector3 targetVelocity = perception.TargetVelocityEstimate;
                if (targetVelocity.y < -0.1f && navigationPosition.y <= activeCombatJumpLinkEndPoint.y - 0.1f)
                    return true;

                float endPointGap = Mathf.Abs(navigationPosition.y - activeCombatJumpLinkEndPoint.y);
                if (targetVelocity.y < -0.1f && endPointGap > combatJumpLinkMinVerticalDelta + 0.25f)
                    return true;
            }
        }

        return false;
    }

    private bool TickCombatJumpLinkApproach(Transform target, Vector3 navigationPosition)
    {
        if (character == null || activeCombatJumpLink == null)
        {
            CancelCombatJumpLinkApproach();
            return false;
        }

        character.ClearManualFacingOverride();
        currentAction = EnemyAction25D.CombatJumpLinkApproach;

        if (Time.time >= nextCombatJumpLinkAbortCheckTime)
        {
            nextCombatJumpLinkAbortCheckTime = Time.time + combatJumpLinkAbortCheckInterval;
            if (ShouldAbortCombatJumpLinkApproach(target, navigationPosition))
            {
                CancelCombatJumpLinkApproach();
                character.StopMovement();
                return false;
            }
        }

        bool traversalActive = TickJumpLinkTraversal(activeCombatJumpLink, activeCombatJumpLinkEndPoint, activeCombatJumpLink.ApproachDistance);
        if (!traversalActive)
        {
            CancelCombatJumpLinkApproach();
            return false;
        }

        if (character.IsJumpTraversalActive)
        {
            currentAction = EnemyAction25D.UseJumpLink;
            CancelCombatJumpLinkApproach();
            return true;
        }

        currentAction = EnemyAction25D.CombatJumpLinkApproach;
        return true;
    }

    private NodeStatus RunBackpedalFire(Vector3 aimPosition)
    {
        if (character == null)
            return NodeStatus.Failure;

        int targetSign = GetTargetFacingSign(aimPosition);
        int moveSign = -targetSign;
        currentAction = EnemyAction25D.BackpedalFire;
        character.SetManualFacingOverride(true, targetSign);
        character.SetMoveInput(Mathf.Clamp(moveSign * backpedalSpeedMultiplier, -1f, 1f));

        bool primaryBlocked = (grenadeThrower != null && grenadeThrower.IsGrenadeActionBlockingPrimaryFire) || (closeRangeRepel != null && closeRangeRepel.IsInRepelFlow);
        if (!primaryBlocked && shooter != null && shooter.TryFirePrimaryAtPerceivedTarget())
            currentAction = EnemyAction25D.BackpedalFire;

        return NodeStatus.Running;
    }

    private int GetTargetFacingSign(Vector3 aimPosition)
    {
        float deltaX = aimPosition.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= 0.01f)
            return character != null ? character.FacingSign : 1;
        return deltaX >= 0f ? 1 : -1;
    }


    private void UpdateContextMovementSpeedMultiplier()
    {
        if (character == null)
            return;

        float multiplier = 1f;
        if (useAlertSearchBehavior && perception != null && perception.IsAlert && !HasVisibleTarget())
            multiplier = Mathf.Max(1f, alertSearchSpeedMultiplier > 0f ? alertSearchSpeedMultiplier : perception.AlertMoveSpeedMultiplierHint);

        character.SetExternalMoveSpeedMultiplier(multiplier);
    }

    private void MoveTowardsX(float targetX)
    {
        if (character == null)
            return;

        float deltaX = targetX - GetTraversalReferencePosition().x;
        if (Mathf.Abs(deltaX) <= 0.01f)
            character.StopMovement();
        else
            character.SetMoveInput(Mathf.Sign(deltaX));
    }

    private Transform GetCurrentFixedPatrolTransform()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return null;

        int attempts = patrolPoints.Length;
        while (attempts-- > 0)
        {
            patrolIndex = Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1);
            Transform point = patrolPoints[patrolIndex];
            if (point != null)
            {
                EnemyPatrolPoint25D meta = point.GetComponent<EnemyPatrolPoint25D>();
                if (meta == null || meta.EnabledForStartPatrol)
                    return point;
            }

            AdvanceFixedPatrolIndex();
        }

        return null;
    }

    private bool NeedJumpTraversalForTarget(Vector3 targetPosition)
    {
        Vector3 traversalReference = GetTraversalReferencePosition();
        return Mathf.Abs(targetPosition.y - traversalReference.y) > 0.75f;
    }

    private void AdvanceFixedPatrolIndex()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        Transform current = patrolPoints[Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1)];
        if (current != null)
        {
            EnemyPatrolPoint25D meta = current.GetComponent<EnemyPatrolPoint25D>();
            if (meta != null && meta.ExplicitNextPoints != null && meta.ExplicitNextPoints.Length > 0)
            {
                for (int i = 0; i < meta.ExplicitNextPoints.Length; i++)
                {
                    EnemyPatrolPoint25D explicitNext = meta.ExplicitNextPoints[i];
                    if (explicitNext == null)
                        continue;

                    Transform explicitTransform = explicitNext.Point;
                    for (int j = 0; j < patrolPoints.Length; j++)
                    {
                        if (patrolPoints[j] == explicitTransform)
                        {
                            patrolIndex = j;
                            return;
                        }
                    }
                }
            }
        }

        int nextIndex = patrolIndex + 1;
        if (nextIndex >= patrolPoints.Length)
            nextIndex = fixedPatrolLoop ? 0 : patrolPoints.Length - 1;
        patrolIndex = Mathf.Clamp(nextIndex, 0, patrolPoints.Length - 1);
    }

    private void ReleaseSelectedCover()
    {
        if (selectedCover != null)
            selectedCover.Release(transform);
        selectedCover = null;
        isPeeking = false;
        nextPeekToggleTime = 0f;
    }


    private void ResetBranchDebugFlags()
    {
        isInFixedPatrolBranch = false;
        isInDynamicPatrolBranch = false;
        isInDynamicSearchBranch = false;
    }

    private void LogCombatPlatformNavigation(
        string eventName,
        Vector3 aimPosition,
        Vector3 navigationPosition,
        Vector3 traversalReference,
        bool directMovementViable,
        EnemyJumpLink25D selectedLink,
        Vector3 traversalStart,
        Vector3 traversalEnd,
        string reason)
    {
        if (!logCombatPlatformNavigation)
            return;

        float verticalGap = Mathf.Abs(navigationPosition.y - traversalReference.y);
        float horizontalGap = Mathf.Abs(navigationPosition.x - traversalReference.x);
        string linkName = selectedLink != null ? selectedLink.name : "None";

        var sb = new StringBuilder(512);
        sb.AppendLine("[EnemyBrainBT25D] Combat platform navigation");
        sb.AppendLine($"Event: {eventName}");
        sb.AppendLine($"Enemy: {name}");
        sb.AppendLine($"State: {currentState} | Action: {currentAction}");
        sb.AppendLine($"AimPosition: {FormatVector3ForLog(aimPosition)}");
        sb.AppendLine($"NavigationPosition: {FormatVector3ForLog(navigationPosition)}");
        sb.AppendLine($"TraversalReference: {FormatVector3ForLog(traversalReference)}");
        sb.AppendLine($"VerticalGap: {verticalGap:F3}");
        sb.AppendLine($"HorizontalGap: {horizontalGap:F3}");
        sb.AppendLine($"DirectMovementViable: {directMovementViable}");
        sb.AppendLine($"SelectedLink: {linkName}");
        if (selectedLink != null)
        {
            sb.AppendLine($"Start: {FormatVector3ForLog(traversalStart)}");
            sb.AppendLine($"End: {FormatVector3ForLog(traversalEnd)}");
        }
        sb.AppendLine($"Reason: {reason}");

        string message = sb.ToString();
        Debug.Log(message, this);
        if (writeCombatPlatformNavigationLogsToFile)
            WriteEnemyDebugLogToFile("CombatPlatformNavigation", message);
    }

    private void WriteEnemyDebugLogToFile(string category, string message)
    {
        if (!writeEnemyDebugLogsToFile)
            return;

        EnemyDebugFileLogger25D.Write(category, message, this);

        if (logEnemyFilePathOnStart && !hasLoggedEnemyDebugFilePathThisSession)
        {
            hasLoggedEnemyDebugFilePathThisSession = true;
            Debug.Log($"[EnemyBrainBT25D] Enemy debug logs are written to:\n{EnemyDebugFileLogger25D.CurrentLogFilePath}", this);
        }
    }

    private string GetRuntimePointRebuildLogReason(RuntimePointMode mode)
    {
        switch (mode)
        {
            case RuntimePointMode.DynamicPatrol:
                return "DynamicPatrol rebuild";
            case RuntimePointMode.Search:
                return "SearchDynamic rebuild";
            default:
                return $"{mode} rebuild";
        }
    }

    private void LogCombatJumpLinkEligibility(string reason, Transform target, Vector3 navigationPosition, float distanceToNavigationTarget, bool allowed)
    {
        if (!logCombatJumpLinkDiagnostics)
            return;

        string key = $"Eligibility|{reason}|{allowed}";
        if (!ShouldLogCombatJumpLinkDiagnostic(key))
            return;

        Vector3 targetPosition = target != null ? target.position : Vector3.zero;
        Vector3 traversalReference = GetTraversalReferencePosition();
        float cooldownRemaining = Mathf.Max(0f, nextCombatJumpLinkDecisionTime - Time.time);
        string message =
            $"[EnemyBrainBT25D] Combat JumpLink eligibility\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"Reason: {reason}\n" +
            $"Allowed: {allowed}\n" +
            $"TargetVisible: {(perception != null && perception.IsTargetVisible)}\n" +
            $"HasLineOfSight: {(perception != null && perception.HasLineOfSight)}\n" +
            $"CurrentRef: {FormatVector3ForLog(traversalReference)}\n" +
            $"TargetPosition: {FormatVector3ForLog(targetPosition)}\n" +
            $"NavigationPosition: {FormatVector3ForLog(navigationPosition)}\n" +
            $"DistanceToNavigationTarget: {distanceToNavigationTarget:F2}\n" +
            $"DecisionCooldownRemaining: {cooldownRemaining:F2}\n" +
            $"IsApproachingCombatJumpLink: {isApproachingCombatJumpLink}\n" +
            $"IsTraversalActive: {(character != null && character.IsJumpTraversalActive)}";

        Debug.Log(message, this);
        if (writeCombatJumpLinkDiagnosticsToFile)
            WriteEnemyDebugLogToFile("CombatJumpLinkEligibility", message);
    }

    private string GetCombatJumpLinkDirectionLabel(EnemyJumpLink25D link, Vector3 traversalStart, Vector3 traversalEnd)
    {
        if (link == null || link.StartPoint == null || link.EndPoint == null)
            return "Unresolved";

        Vector3 serializedStart = link.StartPoint.position;
        Vector3 serializedEnd = link.EndPoint.position;
        bool forward = Vector3.Distance(serializedStart, traversalStart) <= 0.05f && Vector3.Distance(serializedEnd, traversalEnd) <= 0.05f;
        if (forward)
            return "SerializedStart->SerializedEnd";

        bool reverse = Vector3.Distance(serializedEnd, traversalStart) <= 0.05f && Vector3.Distance(serializedStart, traversalEnd) <= 0.05f;
        if (reverse)
            return "SerializedEnd->SerializedStart";

        return "ResolvedTraversal";
    }

    private bool ShouldLogCombatJumpLinkDiagnostic(string key)
    {
        if (!logCombatJumpLinkDiagnostics)
            return false;

        if (combatJumpLinkDiagnosticCooldown > 0f && lastCombatJumpLinkDiagnosticKey == key && Time.time < lastCombatJumpLinkDiagnosticLogTime + combatJumpLinkDiagnosticCooldown)
            return false;

        lastCombatJumpLinkDiagnosticKey = key;
        lastCombatJumpLinkDiagnosticLogTime = Time.time;
        lastCombatJumpLinkDiagnosticLogFrame = Time.frameCount;
        return true;
    }

    private void LogCombatJumpLinkRejected(CombatJumpLinkCandidateDebugInfo info)
    {
        if (!logCombatJumpLinkDiagnostics || !logCombatJumpLinkRejectedCandidates)
            return;

        string linkName = info.Link != null ? info.Link.name : "null";
        string key = $"Rejected|{linkName}|{info.Direction}|{info.RejectReason}";
        if (!ShouldLogCombatJumpLinkDiagnostic(key))
            return;

        string message =
            $"[EnemyBrainBT25D] Combat JumpLink candidate rejected\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"Link: {linkName}\n" +
            $"Direction: {info.Direction}\n" +
            $"Reason: {info.RejectReason}\n" +
            $"TraversalResolved: {info.TraversalResolved}\n" +
            $"CurrentRef: {FormatVector3ForLog(info.CurrentReference)}\n" +
            $"TargetPosition: {FormatVector3ForLog(info.TargetPosition)}\n" +
            $"NavigationPosition: {FormatVector3ForLog(info.NavigationPosition)}\n" +
            $"TraversalStart: {FormatVector3ForLog(info.TraversalStart)}\n" +
            $"TraversalEnd: {FormatVector3ForLog(info.TraversalEnd)}\n" +
            $"StartDistance: {info.StartDistance:F2}\n" +
            $"MaxStartDistance: {info.MaxStartDistance:F2}\n" +
            $"CurrentVerticalGap: {info.CurrentVerticalGap:F2}\n" +
            $"PostVerticalGap: {info.PostVerticalGap:F2}\n" +
            $"VerticalImprovement: {info.VerticalImprovement:F2}\n" +
            $"RequiredVerticalImprovement: {info.RequiredVerticalImprovement:F2}\n" +
            $"SameLevelTolerance: {info.SameLevelTolerance:F2}\n" +
            $"CurrentHorizontalGap: {info.CurrentHorizontalGap:F2}\n" +
            $"PostHorizontalGap: {info.PostHorizontalGap:F2}\n" +
            $"AcceptablePostJumpHorizontalGap: {info.AcceptablePostJumpHorizontalGap:F2}\n" +
            $"StartToTargetGap: {info.StartToTargetGap:F2}\n" +
            $"Score: {info.Score:F2}";

        Debug.Log(message, this);
        if (writeCombatJumpLinkDiagnosticsToFile)
            WriteEnemyDebugLogToFile("CombatJumpLinkRejected", message);
    }

    private void LogCombatJumpLinkSearchSummary(
        Vector3 aimPosition,
        Vector3 targetPosition,
        Vector3 navigationPosition,
        Vector3 traversalReference,
        bool directMovementViable,
        int totalLinks,
        int enabledLinks,
        int traversalResolvedCandidates,
        int viableCandidates,
        int rejectedCandidates,
        bool hasBestRejectedCandidate,
        CombatJumpLinkCandidateDebugInfo bestRejectedCandidate,
        string result)
    {
        if (!logCombatJumpLinkDiagnostics || !logCombatJumpLinkSearchSummary)
            return;

        string key = $"Summary|{result}|{FormatVector3ForLog(navigationPosition)}";
        if (!ShouldLogCombatJumpLinkDiagnostic(key))
            return;

        string bestLinkName = hasBestRejectedCandidate && bestRejectedCandidate.Link != null ? bestRejectedCandidate.Link.name : "None";
        string bestReason = hasBestRejectedCandidate ? bestRejectedCandidate.RejectReason : "None";
        float cooldownRemaining = Mathf.Max(0f, nextCombatJumpLinkDecisionTime - Time.time);
        string message =
            $"[EnemyBrainBT25D] Combat JumpLink search summary\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"TargetVisible: {(perception != null && perception.IsTargetVisible)}\n" +
            $"HasLineOfSight: {(perception != null && perception.HasLineOfSight)}\n" +
            $"AimPosition: {FormatVector3ForLog(aimPosition)}\n" +
            $"TargetPosition: {FormatVector3ForLog(targetPosition)}\n" +
            $"NavigationPosition: {FormatVector3ForLog(navigationPosition)}\n" +
            $"CurrentRef: {FormatVector3ForLog(traversalReference)}\n" +
            $"DirectMovementViable: {directMovementViable}\n" +
            $"TotalLinks: {totalLinks}\n" +
            $"EnabledLinks: {enabledLinks}\n" +
            $"TraversalResolvedCandidates: {traversalResolvedCandidates}\n" +
            $"ViableCandidates: {viableCandidates}\n" +
            $"RejectedCandidates: {rejectedCandidates}\n" +
            $"BestRejectedLink: {bestLinkName}\n" +
            $"BestRejectedReason: {bestReason}\n" +
            $"BestRejectedStart: {(hasBestRejectedCandidate ? FormatVector3ForLog(bestRejectedCandidate.TraversalStart) : "None")}\n" +
            $"BestRejectedEnd: {(hasBestRejectedCandidate ? FormatVector3ForLog(bestRejectedCandidate.TraversalEnd) : "None")}\n" +
            $"BestRejectedScore: {(hasBestRejectedCandidate ? bestRejectedCandidate.Score.ToString("F2") : "None")}\n" +
            $"DecisionCooldownRemaining: {cooldownRemaining:F2}\n" +
            $"Result: {result}";

        Debug.Log(message, this);
        if (writeCombatJumpLinkDiagnosticsToFile)
            WriteEnemyDebugLogToFile("CombatJumpLinkSearchSummary", message);
    }

    private void LogCombatJumpLinkSelected(EnemyJumpLink25D link, Vector3 targetPosition, Vector3 navigationPosition, Vector3 traversalStart, Vector3 traversalEnd, float score)
    {
        if (!logCombatJumpLinkDiagnostics || logCombatJumpLinkDiagnosticsOnlyWhenNoLinkFound)
            return;

        string linkName = link != null ? link.name : "null";
        string direction = GetCombatJumpLinkDirectionLabel(link, traversalStart, traversalEnd);
        string key = $"Selected|{linkName}|{direction}";
        if (!ShouldLogCombatJumpLinkDiagnostic(key))
            return;

        Vector3 traversalReference = GetTraversalReferencePosition();
        float currentVerticalGap = Mathf.Abs(navigationPosition.y - traversalReference.y);
        float postVerticalGap = Mathf.Abs(navigationPosition.y - traversalEnd.y);
        float currentHorizontalGap = Mathf.Abs(navigationPosition.x - traversalReference.x);
        float postHorizontalGap = Mathf.Abs(navigationPosition.x - traversalEnd.x);
        string message =
            $"[EnemyBrainBT25D] Combat JumpLink selected\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"Link: {linkName}\n" +
            $"Direction: {direction}\n" +
            $"CurrentRef: {FormatVector3ForLog(traversalReference)}\n" +
            $"TargetPosition: {FormatVector3ForLog(targetPosition)}\n" +
            $"NavigationPosition: {FormatVector3ForLog(navigationPosition)}\n" +
            $"TraversalStart: {FormatVector3ForLog(traversalStart)}\n" +
            $"TraversalEnd: {FormatVector3ForLog(traversalEnd)}\n" +
            $"Score: {score:F2}\n" +
            $"CurrentVerticalGap: {currentVerticalGap:F2}\n" +
            $"PostVerticalGap: {postVerticalGap:F2}\n" +
            $"CurrentHorizontalGap: {currentHorizontalGap:F2}\n" +
            $"PostHorizontalGap: {postHorizontalGap:F2}";

        Debug.Log(message, this);
        if (writeCombatJumpLinkDiagnosticsToFile)
            WriteEnemyDebugLogToFile("CombatJumpLinkSelected", message);
    }

    private void LogRejectedDynamicJumpLinkCandidate(EnemyJumpLink25D link, string directionLabel, string reason, Vector3 traversalStart, Vector3 traversalEnd)
    {
        if (!logRejectedDynamicJumpLinkCandidates)
            return;

        if (lastRejectedDynamicJumpLinkLogFrame == Time.frameCount)
            return;

        if (rejectedDynamicJumpLinkLogCooldown > 0f && Time.time < lastRejectedDynamicJumpLinkLogTime + rejectedDynamicJumpLinkLogCooldown)
            return;

        lastRejectedDynamicJumpLinkLogFrame = Time.frameCount;
        lastRejectedDynamicJumpLinkLogTime = Time.time;

        Vector3 currentReference = GetTraversalReferencePosition();
        string linkName = link != null ? link.name : "null";
        float entryDistanceFromAnchorX = Mathf.Abs(traversalStart.x - runtimePointsAnchor.x);
        float exitDistanceFromAnchorX = Mathf.Abs(traversalEnd.x - runtimePointsAnchor.x);
        int walkCount = CountRuntimeWalkPoints();
        int jumpCount = CountRuntimeJumpPoints();
        string message = $"[EnemyBrainBT25D] Rejected dynamic JumpLink candidate\nEnemy: {name}\nState: {currentState} | Action: {currentAction}\nMode: {runtimePointMode}\nLink: {linkName}\nDirection: {directionLabel}\nReason: {reason}\nCurrentRef: {FormatVector3ForLog(currentReference)}\nAnchor: {FormatVector3ForLog(runtimePointsAnchor)}\nEntry: {FormatVector3ForLog(traversalStart)}\nExit: {FormatVector3ForLog(traversalEnd)}\nRuntimePointsTotal: {runtimePoints.Count}\nRuntimeWalkPoints: {walkCount}\nRuntimeJumpPoints: {jumpCount}\nWalkBudget: {dynamicPointMaxCount}\nJumpBudget: {dynamicJumpLinkMaxCount}\nSeparateJumpBudget: {dynamicJumpLinksUseSeparateBudget}\nJumpCanExceedRuntimePointMax: {dynamicJumpLinksCanExceedRuntimePointMaxCount}\nEntryDistanceFromAnchorX: {entryDistanceFromAnchorX:F2}\nExitDistanceFromAnchorX: {exitDistanceFromAnchorX:F2}\nJumpLinkSearchRadius: {jumpLinkSearchRadius:F2}\nDynamicPatrolAnchorRadius: {dynamicPatrolAnchorRadius:F2}\nDynamicJumpLinkExitAnchorRadius: {dynamicJumpLinkExitAnchorRadius:F2}";
        Debug.Log(message, this);
        if (writeRejectedJumpLinkLogsToFile)
            WriteEnemyDebugLogToFile("RejectedJumpLink", message);
    }

    private void LogRejectedDynamicWalkPointEdgeClearance(
        RuntimePointMode mode,
        Vector3 point,
        DynamicWalkEdgeClearanceResult result,
        string candidateSource)
    {
        if (!logRejectedDynamicWalkPointEdgeClearance)
            return;

        string message =
            $"[EnemyBrainBT25D] Rejected dynamic WALK point\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"Mode: {mode}\n" +
            $"CandidateSource: {candidateSource}\n" +
            $"Reason: unsafe edge clearance\n" +
            $"EdgeRejectReason: {result.RejectReason}\n" +
            $"Point: {FormatVector3ForLog(point)}\n" +
            $"CurrentRef: {FormatVector3ForLog(GetTraversalReferencePosition())}\n" +
            $"Anchor: {FormatVector3ForLog(runtimePointsAnchor)}\n" +
            $"MinEdgeClearance: {result.MinEdgeClearance:F2}\n" +
            $"MaxGroundHeightDelta: {result.MaxGroundHeightDelta:F2}\n" +
            $"ProbeUpOffset: {dynamicWalkPointEdgeProbeUpOffset:F2}\n" +
            $"ProbeDownDistance: {dynamicWalkPointEdgeProbeDownDistance:F2}\n" +
            $"CenterProbeOrigin: {FormatVector3ForLog(result.CenterProbeOrigin)}\n" +
            $"CenterGroundFound: {result.CenterGroundFound}\n" +
            $"CenterGroundPoint: {FormatVector3ForLog(result.CenterGroundPoint)}\n" +
            $"CenterGroundObject: {result.CenterGroundObject}\n" +
            $"CenterGroundLayer: {result.CenterGroundLayer}\n" +
            $"LeftProbeOrigin: {FormatVector3ForLog(result.LeftProbeOrigin)}\n" +
            $"LeftGroundFound: {result.LeftGroundFound}\n" +
            $"LeftGroundPoint: {FormatVector3ForLog(result.LeftGroundPoint)}\n" +
            $"LeftGroundObject: {result.LeftGroundObject}\n" +
            $"LeftGroundLayer: {result.LeftGroundLayer}\n" +
            $"LeftHeightDelta: {result.LeftHeightDelta:F3}\n" +
            $"RightProbeOrigin: {FormatVector3ForLog(result.RightProbeOrigin)}\n" +
            $"RightGroundFound: {result.RightGroundFound}\n" +
            $"RightGroundPoint: {FormatVector3ForLog(result.RightGroundPoint)}\n" +
            $"RightGroundObject: {result.RightGroundObject}\n" +
            $"RightGroundLayer: {result.RightGroundLayer}\n" +
            $"RightHeightDelta: {result.RightHeightDelta:F3}";

        Debug.Log(message, this);
        if (writeDynamicWalkPointEdgeClearanceLogsToFile)
            WriteEnemyDebugLogToFile("RejectedDynamicWalkPoint", message);
    }

    private void LogLastKnownSafeAdjustment(RuntimeSearchPoint originalPoint, RuntimeSearchPoint adjustedPoint, LastKnownSafeAdjustmentResult result, string candidateSource)
    {
        if (!logLastKnownSafeAdjustment)
            return;

        string message =
            $"[EnemyBrainBT25D] LastKnown investigation point adjusted to safe position\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"CandidateSource: {candidateSource}\n" +
            $"OriginalPoint: {FormatVector3ForLog(result.OriginalPoint)}\n" +
            $"AdjustedPoint: {FormatVector3ForLog(result.AdjustedPoint)}\n" +
            $"AdjustmentDistance: {result.AdjustmentDistance:F2}\n" +
            $"SelectedDirection: {result.SelectedDirectionLabel}\n" +
            $"Reason: {result.Reason}\n" +
            $"OriginalUnsafeReason: {result.OriginalUnsafeReason}\n" +
            $"Attempts: {result.Attempts}\n" +
            $"MaxDistance: {result.MaxDistance:F2}\n" +
            $"Step: {result.Step:F2}\n" +
            $"ObservedSearchLastKnownVersion: {observedSearchLastKnownVersion}\n" +
            $"PointLastKnownVersionSnapshot: {adjustedPoint.LastKnownVersionSnapshot}\n" +
            $"HasFacingHint: {adjustedPoint.HasFacingHint}\n" +
            $"FacingSign: {(adjustedPoint.HasFacingHint ? FormatFacingSignForLog(adjustedPoint.FacingHintSign) : "None")}\n" +
            $"FacingMode: {adjustedPoint.FacingHintMode}\n" +
            $"FacingSource: {adjustedPoint.FacingHintSource}\n" +
            $"OriginalEdgeRejectReason: {result.OriginalEdgeResult.RejectReason}\n" +
            $"AdjustedEdgeRejectReason: {result.AdjustedEdgeResult.RejectReason}\n" +
            $"OriginalCenterGroundFound: {result.OriginalEdgeResult.CenterGroundFound}\n" +
            $"OriginalLeftGroundFound: {result.OriginalEdgeResult.LeftGroundFound}\n" +
            $"OriginalRightGroundFound: {result.OriginalEdgeResult.RightGroundFound}\n" +
            $"AdjustedCenterGroundFound: {result.AdjustedEdgeResult.CenterGroundFound}\n" +
            $"AdjustedLeftGroundFound: {result.AdjustedEdgeResult.LeftGroundFound}\n" +
            $"AdjustedRightGroundFound: {result.AdjustedEdgeResult.RightGroundFound}";

        Debug.Log(message, this);
        if (writeLastKnownSafeAdjustmentLogsToFile)
            WriteEnemyDebugLogToFile("LastKnownSafeAdjustment", message);
    }

    private void LogLastKnownSafeAdjustmentFailed(RuntimeSearchPoint point, LastKnownSafeAdjustmentResult result, string candidateSource)
    {
        if (!logLastKnownSafeAdjustment)
            return;

        string message =
            $"[EnemyBrainBT25D] LastKnown investigation point unsafe but no safe adjustment was found\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"CandidateSource: {candidateSource}\n" +
            $"OriginalPoint: {FormatVector3ForLog(result.OriginalPoint)}\n" +
            $"MaxDistance: {result.MaxDistance:F2}\n" +
            $"Step: {result.Step:F2}\n" +
            $"Attempts: {result.Attempts}\n" +
            $"OriginalUnsafeReason: {result.OriginalUnsafeReason}\n" +
            $"ObservedSearchLastKnownVersion: {observedSearchLastKnownVersion}\n" +
            $"PointLastKnownVersionSnapshot: {point.LastKnownVersionSnapshot}\n" +
            $"HasFacingHint: {point.HasFacingHint}\n" +
            $"FacingSign: {(point.HasFacingHint ? FormatFacingSignForLog(point.FacingHintSign) : "None")}\n" +
            $"FacingMode: {point.FacingHintMode}\n" +
            $"FacingSource: {point.FacingHintSource}\n" +
            $"Note: original point was kept";

        Debug.LogWarning(message, this);
        if (writeLastKnownSafeAdjustmentLogsToFile)
            WriteEnemyDebugLogToFile("LastKnownSafeAdjustmentFailed", message);
    }

    private void LogLastKnownInvestigationPointNearEdge(
        RuntimeSearchPoint point,
        DynamicWalkEdgeClearanceResult result,
        string reason)
    {
        string message =
            $"[EnemyBrainBT25D] LastKnown investigation point is near unsafe edge\n" +
            $"Enemy: {name}\n" +
            $"State: {currentState} | Action: {currentAction}\n" +
            $"Reason: {reason}\n" +
            $"Point: {FormatVector3ForLog(point.WorldPosition)}\n" +
            $"CurrentRef: {FormatVector3ForLog(GetTraversalReferencePosition())}\n" +
            $"ActiveSearchAnchor: {FormatVector3ForLog(runtimePointsAnchor)}\n" +
            $"ObservedSearchLastKnownVersion: {observedSearchLastKnownVersion}\n" +
            $"PointLastKnownVersionSnapshot: {point.LastKnownVersionSnapshot}\n" +
            $"HasFacingHint: {point.HasFacingHint}\n" +
            $"FacingSign: {FormatFacingSignForLog(point.FacingHintSign)}\n" +
            $"FacingMode: {point.FacingHintMode}\n" +
            $"FacingSource: {point.FacingHintSource}\n" +
            $"EdgeRejectReason: {result.RejectReason}\n" +
            $"MinEdgeClearance: {result.MinEdgeClearance:F2}\n" +
            $"CenterGroundFound: {result.CenterGroundFound}\n" +
            $"CenterGroundPoint: {FormatVector3ForLog(result.CenterGroundPoint)}\n" +
            $"LeftGroundFound: {result.LeftGroundFound}\n" +
            $"LeftGroundPoint: {FormatVector3ForLog(result.LeftGroundPoint)}\n" +
            $"LeftHeightDelta: {result.LeftHeightDelta:F3}\n" +
            $"RightGroundFound: {result.RightGroundFound}\n" +
            $"RightGroundPoint: {FormatVector3ForLog(result.RightGroundPoint)}\n" +
            $"RightHeightDelta: {result.RightHeightDelta:F3}\n" +
            $"Note: point was kept because it is LastKnown investigation point";

        Debug.LogWarning(message, this);
        if (writeDynamicWalkPointEdgeClearanceLogsToFile)
            WriteEnemyDebugLogToFile("LastKnownPointNearEdge", message);
    }

    private void LogRuntimePointList(string reason, Vector3 anchor, RuntimePointMode mode)
    {
        if (!logDynamicPointListOnRebuild)
            return;

        if (lastDynamicPointListLogFrame == Time.frameCount)
            return;

        if (dynamicPointListLogCooldown > 0f && Time.time < lastDynamicPointListLogTime + dynamicPointListLogCooldown)
            return;

        lastDynamicPointListLogFrame = Time.frameCount;
        lastDynamicPointListLogTime = Time.time;

        int walkCount = 0;
        int jumpCount = 0;
        int blockedJumpCount = 0;
        for (int i = 0; i < runtimePoints.Count; i++)
        {
            if (runtimePoints[i].RequiresJumpLink)
            {
                jumpCount++;
                if (mode == RuntimePointMode.DynamicPatrol && IsDynamicPatrolJumpPointBlockedForLog(runtimePoints[i]))
                    blockedJumpCount++;
            }
            else
            {
                walkCount++;
            }
        }

        Vector3 currentReference = GetTraversalReferencePosition();

        if (!logDynamicPointListVerbose)
        {
            string blockedSummary = blockedJumpCount > 0 ? $", BlockedJump={blockedJumpCount}" : string.Empty;
            string searchFacingSummary = mode == RuntimePointMode.Search ? $", SearchFacingHintPoints={CountRuntimePointsWithSearchFacingHint()}, LastKnownInvestigationPoints={CountLastKnownInvestigationRuntimePoints()}" : string.Empty;
            string edgeClearanceSummary = $", RejectedUnsafeEdgeWalkPoints={rejectedDynamicWalkPointUnsafeEdgeCount}, WarnedLastKnownNearEdgePoints={warnedLastKnownInvestigationPointNearEdgeCount}, AdjustedLastKnownSafePoints={adjustedLastKnownSafePointCount}, FailedLastKnownSafeAdjustments={failedLastKnownSafeAdjustmentCount}, DynamicWalkPointEdgeClearanceEnabled={requireDynamicWalkPointEdgeClearance}, DynamicWalkPointMinEdgeClearance={dynamicWalkPointMinEdgeClearance:F2}, DynamicWalkPointMaxGroundHeightDelta={dynamicWalkPointMaxGroundHeightDelta:F2}";
            string summaryMessage = $"[EnemyBrainBT25D] {name} rebuilt dynamic points. Reason={reason}, Mode={mode}, State={currentState}, Action={currentAction}, Points={runtimePoints.Count}, Walk={walkCount}, Jump={jumpCount}{blockedSummary}, WalkBudget={dynamicPointMaxCount}, JumpBudget={dynamicJumpLinkMaxCount}, SeparateJumpBudget={dynamicJumpLinksUseSeparateBudget}, JumpCanExceedRuntimePointMax={dynamicJumpLinksCanExceedRuntimePointMaxCount}, RuntimeIndex={runtimePointIndex}, CurrentRef={FormatVector3ForLog(currentReference)}, Anchor={FormatVector3ForLog(anchor)}{searchFacingSummary}{edgeClearanceSummary}";
            Debug.Log(summaryMessage, this);
            if (writeDynamicPointLogsToFile)
                WriteEnemyDebugLogToFile("DynamicPoints", summaryMessage);
            return;
        }

        StringBuilder sb = new StringBuilder(512 + runtimePoints.Count * 128);
        sb.AppendLine("[EnemyBrainBT25D] Dynamic points rebuilt");
        sb.AppendLine($"Reason: {reason}");
        sb.AppendLine($"Enemy: {name}");
        sb.AppendLine($"Mode: {mode}");
        sb.AppendLine($"State: {currentState} | Action: {currentAction}");
        sb.AppendLine($"CurrentRef: {FormatVector3ForLog(currentReference)}");
        sb.AppendLine($"Anchor: {FormatVector3ForLog(anchor)}");
        sb.AppendLine($"RuntimeIndex: {runtimePointIndex}");
        string blockedPointSummary = blockedJumpCount > 0 ? $" | {blockedJumpCount} BLOCKED" : string.Empty;
        sb.AppendLine($"Points: {runtimePoints.Count} total | {walkCount} WALK | {jumpCount} JUMP{blockedPointSummary}");
        sb.AppendLine($"WalkBudget: {dynamicPointMaxCount}");
        sb.AppendLine($"JumpBudget: {dynamicJumpLinkMaxCount}");
        sb.AppendLine($"SeparateJumpBudget: {dynamicJumpLinksUseSeparateBudget}");
        sb.AppendLine($"JumpCanExceedRuntimePointMax: {dynamicJumpLinksCanExceedRuntimePointMaxCount}");
        sb.AppendLine($"PreserveJumpLinksWhenTrimming: {preserveDynamicJumpLinksWhenTrimmingRuntimePoints}");
        sb.AppendLine($"RejectedUnsafeEdgeWalkPoints: {rejectedDynamicWalkPointUnsafeEdgeCount}");
        sb.AppendLine($"WarnedLastKnownNearEdgePoints: {warnedLastKnownInvestigationPointNearEdgeCount}");
        sb.AppendLine($"AdjustedLastKnownSafePoints: {adjustedLastKnownSafePointCount}");
        sb.AppendLine($"FailedLastKnownSafeAdjustments: {failedLastKnownSafeAdjustmentCount}");
        sb.AppendLine($"DynamicWalkPointEdgeClearanceEnabled: {requireDynamicWalkPointEdgeClearance}");
        sb.AppendLine($"DynamicWalkPointMinEdgeClearance: {dynamicWalkPointMinEdgeClearance:F2}");
        sb.AppendLine($"DynamicWalkPointMaxGroundHeightDelta: {dynamicWalkPointMaxGroundHeightDelta:F2}");
        if (mode == RuntimePointMode.Search)
        {
            sb.AppendLine($"LastKnownInvestigationPoints: {CountLastKnownInvestigationRuntimePoints()}");
            sb.AppendLine($"SearchFacingHintPoints: {CountRuntimePointsWithSearchFacingHint()}");
            sb.AppendLine($"ObservedSearchLastKnownVersion: {observedSearchLastKnownVersion}");
            sb.AppendLine($"ActiveSearchRecoveryHasFacingHint: {activeSearchRecoveryHasFacingHint}");
            sb.AppendLine($"ActiveSearchRecoveryFacingSign: {FormatFacingSignForLog(activeSearchRecoveryFacingSign)}");
            sb.AppendLine($"ActiveSearchRecoveryFacingMode: {activeSearchRecoveryFacingMode}");
            sb.AppendLine($"ActiveSearchRecoveryFacingSource: {activeSearchRecoveryFacingSource}");
        }

        if (runtimePoints.Count > 0)
            sb.AppendLine();

        for (int i = 0; i < runtimePoints.Count; i++)
        {
            RuntimeSearchPoint point = runtimePoints[i];
            string marker = i == runtimePointIndex ? "*" : " ";

            if (point.RequiresJumpLink)
            {
                string linkName = point.JumpLink != null ? point.JumpLink.name : "null";
                if (point.HasExplicitJumpTraversal)
                {
                    sb.Append($"[{i}]{marker} JUMP walkTo={FormatVector3ForLog(point.JumpTraversalStart)} jumpTo={FormatVector3ForLog(point.JumpTraversalEnd)} wait={point.WaitDuration:F2} score={point.Score:F2} link={linkName}");
                    if (mode == RuntimePointMode.DynamicPatrol && dynamicPatrolShowBlockedJumpLinksInLogs)
                        sb.Append(GetDynamicPatrolJumpPointLogSuffix(point));
                }
                else
                {
                    sb.Append($"[{i}]{marker} JUMP destination={FormatVector3ForLog(point.WorldPosition)} wait={point.WaitDuration:F2} score={point.Score:F2} link={linkName}");
                }
            }
            else
            {
                sb.Append($"[{i}]{marker} WALK pos={FormatVector3ForLog(point.WorldPosition)} wait={point.WaitDuration:F2} score={point.Score:F2}");
            }

            if (mode == RuntimePointMode.Search)
                sb.Append(GetSearchFacingHintPointLogSuffix(point));

            sb.AppendLine();
        }
        string message = sb.ToString();
        Debug.Log(message, this);
        if (writeDynamicPointLogsToFile)
            WriteEnemyDebugLogToFile("DynamicPoints", message);
    }

    private bool IsDynamicPatrolJumpPointBlockedForLog(RuntimeSearchPoint point)
    {
        if (!point.RequiresJumpLink)
            return false;

        return IsDynamicPatrolJumpPointBlockedByPostJumpRule(point) || IsDynamicPatrolDeadEndReturnPoint(point);
    }

    private string GetDynamicPatrolJumpPointLogSuffix(RuntimeSearchPoint point)
    {
        if (!point.RequiresJumpLink)
            return string.Empty;

        if (IsDynamicPatrolJumpPointBlockedByPostJumpRule(point))
        {
            int required = Mathf.Max(1, dynamicPatrolWalkPointsRequiredAfterJumpLink);
            int completed = Mathf.Clamp(required - dynamicPatrolWalkPointsRemainingBeforeJumpLinkAllowed, 0, required);
            if (RuntimePointsContainSelectableWalkPoint(GetTraversalReferencePosition()))
                return $" blocked=postJumpWalks {completed}/{required}";

            if (RuntimePointsContainNonReturnJumpPoint() && IsReturnToLastDynamicPatrolJump(point))
                return $" blocked=returnJumpOtherLinkAvailable {completed}/{required}";

            return $" blocked=postJumpRule {completed}/{required}";
        }

        if (IsDynamicPatrolDeadEndReturnPoint(point))
        {
            if (dynamicPatrolDeadEndDelayActive)
                return $" blocked=deadEndReturn until={Mathf.Max(0f, dynamicPatrolDeadEndResumeTime - Time.time):F2}s";

            return $" blocked=deadEndReturn delay={dynamicPatrolDeadEndDelay:F2}";
        }

        if (IsDynamicPatrolPostJumpRestrictionActive() && !RuntimePointsContainWalkPoint() && !IsReturnToLastDynamicPatrolJump(point))
            return " allowed=noWalkPointsOtherJump";

        return string.Empty;
    }

    private void LogRuntimePointClear(string reason, int previousCount, RuntimePointMode previousMode)
    {
        if (!logDynamicPointClearEvents)
            return;

        bool isNoOpClear = previousCount <= 0 && previousMode == RuntimePointMode.None;
        if (suppressNoOpRuntimePointsClearLogs && isNoOpClear)
            return;

        Vector3 currentReference = GetTraversalReferencePosition();
        string message = $"[EnemyBrainBT25D] Runtime dynamic points cleared\nReason: {reason}\nEnemy: {name}\nPreviousMode: {previousMode}\nState: {currentState} | Action: {currentAction}\nCurrentRef: {FormatVector3ForLog(currentReference)}\nPreviousPoints: {previousCount}";
        Debug.Log(message, this);
        if (writeRuntimePointClearLogsToFile)
            WriteEnemyDebugLogToFile("RuntimePointsClear", message);
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

    private void SetBranchDebugFlags(bool fixedPatrol = false, bool dynamicPatrol = false, bool dynamicSearch = false)
    {
        isInFixedPatrolBranch = fixedPatrol;
        isInDynamicPatrolBranch = dynamicPatrol;
        isInDynamicSearchBranch = dynamicSearch;
    }

    private void RecordDynamicPointGenerationResult(Vector3 anchor, RuntimePointMode mode, int pointCount)
    {
        lastDynamicGenerationAnchor = anchor;
        lastGeneratedPointSetType = mode;
        lastGeneratedDynamicPointCount = Mathf.Max(0, pointCount);
        lastDynamicPointGenerationSucceeded = pointCount > 0;

        if (mode == RuntimePointMode.DynamicPatrol)
            dynamicPatrolHasValidPoints = pointCount > 0;
        else if (mode == RuntimePointMode.Search)
            dynamicSearchHasValidPoints = pointCount > 0;

        if (logDynamicPointGenerationFailures && pointCount <= 0)
        {
            string branchName = mode == RuntimePointMode.DynamicPatrol ? "DynamicPatrol" : mode == RuntimePointMode.Search ? "DynamicSearch" : mode.ToString();
            Debug.LogWarning($"[{name}] {branchName} generated 0 valid runtime points at anchor {anchor}. Check Dynamic Point Ground Mask / Raycast Height / Raycast Depth / nearby geometry.", this);
        }
    }

    private void DynamicPatrolHasValidPointsInternal(bool value)
    {
        dynamicPatrolHasValidPoints = value;
    }

    private void DynamicSearchHasValidPointsInternal(bool value)
    {
        dynamicSearchHasValidPoints = value;
    }

    private void ClearRuntimePoints(string reason = "Unspecified")
    {
        int previousCount = runtimePoints.Count;
        RuntimePointMode previousMode = runtimePointMode;
        LogRuntimePointClear(reason, previousCount, previousMode);

        if (previousMode == RuntimePointMode.Search)
        {
            LogSearchFacingHintReset("ClearRuntimePointsPreviousModeSearch", previousCount, previousMode);
            if (clearSearchFacingLockOnSearchExit)
                ClearSearchFacingLockFromBrain("ClearRuntimePointsPreviousModeSearch");
            ResetIgnoredSearchRetargetLogState();
        }

        runtimePoints.Clear();
        runtimePointMode = RuntimePointMode.None;
        runtimePointIndex = 0;
        runtimePointWaitEndTime = 0f;
        hasRuntimePoints = false;
        activeTraversalLink = null;
    }

    private void SetState(BrainState nextState)
    {
        if (currentState == nextState)
            return;

        bool leavingSearchDynamic = currentState == BrainState.SearchDynamic && nextState != BrainState.SearchDynamic;
        if (leavingSearchDynamic)
        {
            LogSearchFacingHintReset("SetStateLeavingSearchDynamic", runtimePoints.Count, runtimePointMode);
            if (clearSearchFacingLockOnSearchExit)
                ClearSearchFacingLockFromBrain($"SearchExit:{nextState}");
            ResetIgnoredSearchRetargetLogState();
        }

        currentState = nextState;
        if (nextState != BrainState.PatrolFixed)
            patrolWaitEndTime = 0f;
        if (nextState != BrainState.SearchDynamic && nextState != BrainState.PatrolDynamic)
            runtimePointWaitEndTime = 0f;
        if (nextState == BrainState.SearchDynamic ||
            nextState == BrainState.Combat ||
            nextState == BrainState.PatrolFixed ||
            nextState == BrainState.Idle)
        {
            ResetDynamicPatrolPostJumpRestriction();
        }
    }

    private void UpdateBlackboard()
    {
        if (blackboard == null || perception == null)
            return;

        blackboard.SetTarget(perception.CurrentTarget);
        float distance = 0f;
        if (perception.CurrentTarget != null)
            distance = Mathf.Abs(perception.GetAimPosition().x - transform.position.x);

        blackboard.SetPerception(
            perception.IsTargetVisible,
            perception.HasLineOfSight,
            perception.HasLastKnownPosition,
            perception.LastKnownTargetPosition,
            perception.TargetVelocityEstimate,
            distance);

        bool canShoot = shooter != null && shooter.CanFireNow && currentState == BrainState.Combat && (!useCover || selectedCover == null || isPeeking) && (grenadeThrower == null || !grenadeThrower.IsGrenadeActionBlockingPrimaryFire) && (closeRangeRepel == null || !closeRangeRepel.IsInRepelFlow);
        bool shouldMove = character != null && Mathf.Abs(character.MoveInputX) > 0.01f;
        float desiredMoveX = character != null ? character.MoveInputX : 0f;
        blackboard.SetBrainState(currentState == BrainState.SearchDynamic || currentState == BrainState.Combat, currentState == BrainState.Combat, canShoot, shouldMove, desiredMoveX);
        blackboard.SetCoverState(currentState == BrainState.Combat && selectedCover != null, selectedCover);
        blackboard.SetPatrolContext(HasEnabledFixedPatrolPoints(), GetCurrentFixedPatrolMeta(), runtimePointMode == RuntimePointMode.DynamicPatrol, runtimePointMode == RuntimePointMode.Search, dynamicPatrolAnchor, hasDynamicPatrolAnchor);
        blackboard.SetTraversalLink(activeTraversalLink);
    }

    private EnemyPatrolPoint25D GetCurrentFixedPatrolMeta()
    {
        Transform point = GetCurrentFixedPatrolTransform();
        return point != null ? point.GetComponent<EnemyPatrolPoint25D>() : null;
    }


    private void OnDrawGizmos()
    {
        if (!drawOnlyWhenSelected)
            DrawNavigationDebugGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (drawOnlyWhenSelected)
            DrawNavigationDebugGizmos();
    }

    private void DrawNavigationDebugGizmos()
    {
        if (!drawNavigationGizmos)
            return;

        AutoAssign();

        if (drawCurrentTargetPoint)
        {
            if (isInFixedPatrolBranch)
                DrawCurrentFixedPatrolTargetGizmos();
            else if (isInDynamicPatrolBranch || isInDynamicSearchBranch)
                DrawCurrentRuntimePointGizmos();
        }

        if (drawGeneratedDynamicPoints)
            DrawGeneratedRuntimePointGizmos();

        if (drawDynamicPatrolAnchor)
            DrawDynamicAnchorGizmos();

        if (drawActiveTraversalLink)
            DrawTraversalGizmos();

        if (drawJumpLinkSearchRadiusGizmo)
            DrawJumpLinkSearchRadiusGizmo();

        if (drawDynamicPointGenerationWarnings)
            DrawDynamicPointGenerationWarningGizmos();
    }

    private void DrawJumpLinkSearchRadiusGizmo()
    {
        Vector3 center = OffsetGizmoPosition(transform.position);
        DrawWireCircle(center, jumpLinkSearchRadius, recoveryJumpLinkSearchRadiusColor);
        DrawWireCircle(center, combatJumpLinkMaxStartDistance, combatJumpLinkSearchRadiusColor);
    }

    private void DrawWireCircle(Vector3 center, float radius, Color color)
    {
        radius = Mathf.Max(0f, radius);
        if (radius <= 0.001f)
            return;

        Gizmos.color = color;
        const int segments = 36;
        Vector3 previous = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = t * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }

    private void DrawCurrentFixedPatrolTargetGizmos()
    {
        Transform point = GetCurrentFixedPatrolTransform();
        if (point == null)
            return;

        Vector3 targetPosition = GetPatrolPointWorldPosition(point);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(OffsetGizmoPosition(targetPosition), gizmoCurrentPointRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, OffsetGizmoPosition(targetPosition));
    }

    private void DrawCurrentRuntimePointGizmos()
    {
        if (!hasRuntimePoints || runtimePoints.Count == 0)
            return;

        int index = Mathf.Clamp(runtimePointIndex, 0, runtimePoints.Count - 1);
        RuntimeSearchPoint point = runtimePoints[index];
        Vector3 navigationTarget = GetRuntimePointNavigationTarget(point);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(OffsetGizmoPosition(navigationTarget), gizmoCurrentPointRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(OffsetGizmoPosition(GetTraversalReferencePosition()), OffsetGizmoPosition(navigationTarget));

        if (point.RequiresJumpLink && point.HasExplicitJumpTraversal)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(OffsetGizmoPosition(point.JumpTraversalEnd), gizmoPointRadius);

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(OffsetGizmoPosition(point.JumpTraversalStart), OffsetGizmoPosition(point.JumpTraversalEnd));
        }
    }

    private void DrawGeneratedRuntimePointGizmos()
    {
        if (!hasRuntimePoints || runtimePoints.Count == 0)
            return;

        int currentIndex = Mathf.Clamp(runtimePointIndex, 0, runtimePoints.Count - 1);
        Color fadedCyan = new Color(0.2f, 0.9f, 1f, 0.65f);

        for (int i = 0; i < runtimePoints.Count; i++)
        {
            RuntimeSearchPoint point = runtimePoints[i];
            if (i == currentIndex)
                continue;

            Vector3 navigationTarget = GetRuntimePointNavigationTarget(point);

            Gizmos.color = fadedCyan;
            Gizmos.DrawSphere(OffsetGizmoPosition(navigationTarget), gizmoPointRadius);

            if (point.RequiresJumpLink && point.HasExplicitJumpTraversal)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.55f);
                Gizmos.DrawSphere(OffsetGizmoPosition(point.JumpTraversalEnd), gizmoPointRadius * 0.8f);
                Gizmos.DrawLine(OffsetGizmoPosition(point.JumpTraversalStart), OffsetGizmoPosition(point.JumpTraversalEnd));
            }
        }
    }

    private void DrawDynamicAnchorGizmos()
    {
        if (!hasDynamicPatrolAnchor)
            return;

        Gizmos.color = new Color(0.7f, 0.2f, 1f, 0.9f);
        Gizmos.DrawSphere(OffsetGizmoPosition(dynamicPatrolAnchor), gizmoPointRadius);
    }


    private void DrawDynamicPointGenerationWarningGizmos()
    {
        if (isInDynamicPatrolBranch && !dynamicPatrolHasValidPoints)
        {
            Gizmos.color = Color.red;
            Vector3 warningPos = OffsetGizmoPosition(lastDynamicGenerationAnchor);
            Gizmos.DrawSphere(warningPos, gizmoCurrentPointRadius * 0.75f);
            Gizmos.DrawLine(warningPos + Vector3.left * gizmoCurrentPointRadius, warningPos + Vector3.right * gizmoCurrentPointRadius);
            Gizmos.DrawLine(warningPos + Vector3.up * gizmoCurrentPointRadius, warningPos + Vector3.down * gizmoCurrentPointRadius);
        }

        if (isInDynamicSearchBranch && !dynamicSearchHasValidPoints)
        {
            Gizmos.color = Color.magenta;
            Vector3 warningPos = OffsetGizmoPosition(lastDynamicGenerationAnchor);
            Gizmos.DrawSphere(warningPos, gizmoCurrentPointRadius * 0.65f);
            Gizmos.DrawLine(warningPos + Vector3.left * gizmoCurrentPointRadius, warningPos + Vector3.right * gizmoCurrentPointRadius);
            Gizmos.DrawLine(warningPos + Vector3.up * gizmoCurrentPointRadius, warningPos + Vector3.down * gizmoCurrentPointRadius);
        }
    }

    private void DrawTraversalGizmos()
    {
        if (activeTraversalLink == null)
            return;

        Transform start = activeTraversalLink.StartPoint;
        Transform end = activeTraversalLink.EndPoint;
        if (start == null || end == null)
            return;

        Gizmos.color = new Color(1f, 0.55f, 0f, 1f);
        Gizmos.DrawSphere(OffsetGizmoPosition(start.position), gizmoLinkPointRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(OffsetGizmoPosition(end.position), gizmoLinkPointRadius);

        Gizmos.color = new Color(1f, 0.45f, 0f, 1f);
        Gizmos.DrawLine(OffsetGizmoPosition(start.position), OffsetGizmoPosition(end.position));

        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, OffsetGizmoPosition(start.position));
    }

    private Vector3 GetPatrolPointWorldPosition(Transform pointTransform)
    {
        if (pointTransform == null)
            return transform.position;

        EnemyPatrolPoint25D meta = pointTransform.GetComponent<EnemyPatrolPoint25D>();
        Transform authoredPoint = meta != null ? meta.Point : null;
        return authoredPoint != null ? authoredPoint.position : pointTransform.position;
    }

    private Vector3 OffsetGizmoPosition(Vector3 worldPosition)
    {
        return worldPosition + Vector3.up * gizmoVerticalOffset;
    }

    private void AutoAssign()
    {
        if (character == null)
            character = GetComponent<EnemyCharacter25D>();
        if (health == null)
            health = GetComponent<EnemyHealth25D>();
        if (stun == null)
            stun = GetComponent<EnemyStun25D>();
        if (knockback == null)
            knockback = GetComponent<EnemyKnockbackReceiver25D>();
        if (perception == null)
            perception = GetComponent<EnemyPerception25D>();
        if (blackboard == null)
            blackboard = GetComponent<EnemyBlackboard25D>();
        if (shooter == null)
            shooter = GetComponent<EnemyBallisticShooter25D>();
        if (grenadeThrower == null)
            grenadeThrower = GetComponent<EnemyGrenadeThrower25D>();
        if (closeRangeRepel == null)
            closeRangeRepel = GetComponent<EnemyCloseRangeRepel25D>();
    }

    private void ClampSettings()
    {
        patrolPointReachDistance = Mathf.Max(0f, patrolPointReachDistance);
        patrolWaitDuration = Mathf.Max(0f, patrolWaitDuration);
        patrolMoveDeadZone = Mathf.Max(0f, patrolMoveDeadZone);
        dynamicPointNearDistance = Mathf.Max(0f, dynamicPointNearDistance);
        dynamicPointFarDistance = Mathf.Max(dynamicPointNearDistance, dynamicPointFarDistance);
        dynamicPointMaxCount = Mathf.Max(1, dynamicPointMaxCount);
        dynamicJumpLinkMaxCount = Mathf.Max(0, dynamicJumpLinkMaxCount);
        dynamicJumpLinkExitAnchorRadius = Mathf.Max(0f, dynamicJumpLinkExitAnchorRadius);
        combatJumpLinkDiagnosticCooldown = Mathf.Max(0f, combatJumpLinkDiagnosticCooldown);
        searchFacingHintDiagnosticCooldown = Mathf.Max(0f, searchFacingHintDiagnosticCooldown);
        dynamicPointMinSeparation = Mathf.Max(0f, dynamicPointMinSeparation);
        dynamicPointRaycastHeight = Mathf.Max(0f, dynamicPointRaycastHeight);
        dynamicPointRaycastDepth = Mathf.Max(0f, dynamicPointRaycastDepth);
        dynamicPatrolAnchorRadius = Mathf.Max(0f, dynamicPatrolAnchorRadius);
        dynamicSearchArrivalDistance = Mathf.Max(0f, dynamicSearchArrivalDistance);
        dynamicPatrolArrivalDistance = Mathf.Max(0f, dynamicPatrolArrivalDistance);
        dynamicSearchWaitDuration = Mathf.Max(0f, dynamicSearchWaitDuration);
        dynamicPatrolWaitDuration = Mathf.Max(0f, dynamicPatrolWaitDuration);
        dynamicPatrolMinTravelDistance = Mathf.Max(0f, dynamicPatrolMinTravelDistance);
        dynamicPointSamePlatformVerticalTolerance = Mathf.Max(0f, dynamicPointSamePlatformVerticalTolerance);
        dynamicPointWalkabilitySampleStep = Mathf.Max(0.1f, dynamicPointWalkabilitySampleStep);
        dynamicPointWalkabilityProbeHeight = Mathf.Max(0.05f, dynamicPointWalkabilityProbeHeight);
        dynamicPointWalkabilityProbeDepth = Mathf.Max(0.05f, dynamicPointWalkabilityProbeDepth);
        dynamicRegenerateMinAirborneTime = Mathf.Max(0f, dynamicRegenerateMinAirborneTime);
        dynamicWalkPointMinEdgeClearance = Mathf.Max(0f, dynamicWalkPointMinEdgeClearance);
        dynamicWalkPointEdgeProbeUpOffset = Mathf.Max(0f, dynamicWalkPointEdgeProbeUpOffset);
        dynamicWalkPointEdgeProbeDownDistance = Mathf.Max(0.01f, dynamicWalkPointEdgeProbeDownDistance);
        dynamicWalkPointMaxGroundHeightDelta = Mathf.Max(0f, dynamicWalkPointMaxGroundHeightDelta);
        dynamicPatrolJumpLinkSelectionWeight = Mathf.Clamp01(dynamicPatrolJumpLinkSelectionWeight);
        dynamicPatrolWalkPointsRequiredAfterJumpLink = Mathf.Max(0, dynamicPatrolWalkPointsRequiredAfterJumpLink);
        dynamicPatrolDeadEndDelay = Mathf.Max(0f, dynamicPatrolDeadEndDelay);
        dynamicPatrolReturnJumpMatchTolerance = Mathf.Max(0.01f, dynamicPatrolReturnJumpMatchTolerance);
        dynamicSearchJumpLinkRepeatBlockDuration = Mathf.Max(0f, dynamicSearchJumpLinkRepeatBlockDuration);
        dynamicSearchJumpLinkMinProgress = Mathf.Max(0f, dynamicSearchJumpLinkMinProgress);
        searchFacingDeadZone = Mathf.Max(0f, searchFacingDeadZone);
        searchRecoveryRetryDelay = Mathf.Max(0.05f, searchRecoveryRetryDelay);
        searchRecoveryMinCommitTime = Mathf.Max(0f, searchRecoveryMinCommitTime);
        searchRecoveryCompleteRadius = Mathf.Max(0.05f, searchRecoveryCompleteRadius);
        visibleReacquireConfirmDuration = Mathf.Max(0f, visibleReacquireConfirmDuration);
        rearAwarenessFocusDuration = Mathf.Max(0f, rearAwarenessFocusDuration);
        desiredCombatMinRange = Mathf.Max(0f, desiredCombatMinRange);
        desiredCombatMaxRange = Mathf.Max(desiredCombatMinRange, desiredCombatMaxRange);
        combatMoveDeadZone = Mathf.Max(0f, combatMoveDeadZone);
        backpedalStartRange = Mathf.Max(0f, backpedalStartRange);
        backpedalSpeedMultiplier = Mathf.Max(0f, backpedalSpeedMultiplier);
        alertSearchSpeedMultiplier = Mathf.Max(1f, alertSearchSpeedMultiplier);
        jumpLinkSearchRadius = Mathf.Max(0f, jumpLinkSearchRadius);
        gizmoPointRadius = Mathf.Max(0f, gizmoPointRadius);
        gizmoCurrentPointRadius = Mathf.Max(0f, gizmoCurrentPointRadius);
        gizmoLinkPointRadius = Mathf.Max(0f, gizmoLinkPointRadius);
        gizmoVerticalOffset = Mathf.Max(0f, gizmoVerticalOffset);
        coverSearchRadius = Mathf.Max(0f, coverSearchRadius);
        coverRepathInterval = Mathf.Max(0.05f, coverRepathInterval);
        coverStandReachDistance = Mathf.Max(0f, coverStandReachDistance);
        coverPeekReachDistance = Mathf.Max(0f, coverPeekReachDistance);
        peekDurationMin = Mathf.Max(0f, peekDurationMin);
        peekDurationMax = Mathf.Max(peekDurationMin, peekDurationMax);
        hideDurationMin = Mathf.Max(0f, hideDurationMin);
        hideDurationMax = Mathf.Max(hideDurationMin, hideDurationMax);
        coverMinDistanceFromTarget = Mathf.Max(0f, coverMinDistanceFromTarget);
    }
}
