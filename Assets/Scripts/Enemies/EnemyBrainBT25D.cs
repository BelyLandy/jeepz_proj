using System;
using System.Collections.Generic;
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

        public RuntimeSearchPoint(Vector3 worldPosition, float waitDuration, bool requiresJumpLink, EnemyJumpLink25D jumpLink, float score)
        {
            WorldPosition = worldPosition;
            WaitDuration = Mathf.Max(0f, waitDuration);
            RequiresJumpLink = requiresJumpLink;
            JumpLink = jumpLink;
            Score = score;
        }
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

    [Header("Search")]
    [SerializeField, Min(0f)] private float searchFacingDeadZone = 0.05f;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float desiredCombatMinRange = 5f;
    [SerializeField, Min(0f)] private float desiredCombatMaxRange = 10f;
    [SerializeField, Min(0f)] private float combatMoveDeadZone = 0.2f;
    [SerializeField] private bool allowGrenadeAttack = true;
    [SerializeField] private bool allowBackpedalFire = true;
    [SerializeField, Min(0f)] private float backpedalStartRange = 5.5f;
    [SerializeField, Min(0f)] private float backpedalSpeedMultiplier = 0.9f;
    [SerializeField] private bool allowCloseRepel = true;

    [Header("Jump Links")]
    [SerializeField, Min(0f)] private float jumpLinkSearchRadius = 6f;
    [SerializeField] private bool allowJumpLinksInFixedPatrol = true;
    [SerializeField] private bool allowJumpLinksInDynamicSearch = true;
    [SerializeField] private bool allowJumpLinksInDynamicPatrol = true;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawNavigationGizmos = true;
    [SerializeField] private bool drawCurrentTargetPoint = true;
    [SerializeField] private bool drawGeneratedDynamicPoints = true;
    [SerializeField] private bool drawActiveTraversalLink = true;
    [SerializeField] private bool drawDynamicPatrolAnchor = true;
    [SerializeField] private bool drawOnlyWhenSelected = true;
    [SerializeField] private bool drawDynamicPointGenerationWarnings = true;
    [SerializeField] private bool logDynamicPointGenerationFailures = false;
    [SerializeField, Min(0f)] private float gizmoPointRadius = 0.2f;
    [SerializeField, Min(0f)] private float gizmoCurrentPointRadius = 0.3f;
    [SerializeField, Min(0f)] private float gizmoLinkPointRadius = 0.22f;
    [SerializeField, Min(0f)] private float gizmoVerticalOffset = 0.15f;

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

    public BrainState CurrentState => currentState;
    public EnemyAction25D CurrentAction => currentAction;
    public bool IsAlert => currentState == BrainState.SearchDynamic || currentState == BrainState.Combat;
    public bool IsInCombat => currentState == BrainState.Combat;
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

    private void Reset()
    {
        AutoAssign();
        ClampSettings();
    }

    private void Awake()
    {
        AutoAssign();
        ClampSettings();
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
            character.ClearManualFacingOverride();
    }

    private void Update()
    {
        AutoAssign();
        if (rootNode == null)
            BuildTree();

        ResetBranchDebugFlags();
        currentAction = EnemyAction25D.None;
        if (grenadeThrower != null)
            grenadeThrower.TickRecovery();

        if (perception != null && perception.IsTargetVisible && perception.CurrentTarget != null)
        {
            hasDynamicPatrolAnchor = false;
            if (blackboard != null)
                blackboard.MarkPlayerDetectedForever();
        }

        rootNode.Tick();

        if (currentState != BrainState.Combat)
        {
            wasInCombatLastFrame = false;
            combatEnterTime = 0f;
            if (grenadeThrower != null && grenadeThrower.IsPreparingThrow)
                grenadeThrower.CancelPrepare();
            if (closeRangeRepel != null && closeRangeRepel.IsInRepelFlow)
                closeRangeRepel.CancelRepel();
            if (character != null)
                character.ClearManualFacingOverride();
        }

        UpdateBlackboard();
    }

    private void BuildTree()
    {
        rootNode = new SelectorNode(
            new SequenceNode(new ConditionNode(IsDead), new ActionNode(RunDeadBranch)),
            new SequenceNode(new ConditionNode(IsDisabled), new ActionNode(RunDisabledBranch)),
            new SequenceNode(new ConditionNode(ShouldRunCombatBranch), new ActionNode(RunCombatBranch)),
            new SequenceNode(new ConditionNode(HasLastKnownTarget), new ActionNode(RunDynamicSearchBranch)),
            new ActionNode(RunPatrolOrIdleBranch));
    }

    private bool IsDead() => health != null && health.IsDead;
    private bool IsDisabled() => (stun != null && stun.IsStunned) || (knockback != null && (knockback.IsLaunched || knockback.IsRecovering)) || (character != null && character.IsJumpTraversalActive);
    private bool HasVisibleTarget() => perception != null && perception.IsTargetVisible && perception.CurrentTarget != null;
    private bool ShouldRunCombatBranch()
    {
        bool grenadeActive = grenadeThrower != null && grenadeThrower.IsInAnyGrenadeExclusiveState;
        bool repelActive = closeRangeRepel != null && closeRangeRepel.IsInRepelFlow;
        return grenadeActive || repelActive || HasVisibleTarget();
    }
    private bool HasLastKnownTarget() => perception != null && perception.HasLastKnownPosition;

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
        float distanceToTarget = Vector3.Distance(transform.position, aimPosition);
        int targetSign = GetTargetFacingSign(aimPosition);

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

        if (allowBackpedalFire && perception.IsTargetVisible && distanceToTarget <= backpedalStartRange)
            return RunBackpedalFire(aimPosition);

        if (useCover && TryMaintainOrAcquireCover())
            return RunCoverCombat(aimPosition);

        ReleaseSelectedCover();
        return RunOpenCombat(aimPosition);
    }

    private NodeStatus RunOpenCombat(Vector3 aimPosition)
    {
        if (character != null)
            character.ClearManualFacingOverride();

        float deltaX = aimPosition.x - transform.position.x;
        float absDeltaX = Mathf.Abs(deltaX);

        float moveX = 0f;
        if (absDeltaX > desiredCombatMaxRange)
            moveX = Mathf.Sign(deltaX);
        else if (absDeltaX < desiredCombatMinRange)
            moveX = -Mathf.Sign(deltaX);

        if (Mathf.Abs(moveX) <= combatMoveDeadZone)
            moveX = 0f;

        character.SetMoveInput(moveX);

        bool primaryBlocked = grenadeThrower != null && grenadeThrower.IsGrenadeActionBlockingPrimaryFire;
        if (!primaryBlocked && shooter != null && moveX == 0f && shooter.TryFirePrimaryAtPerceivedTarget())
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

        Vector3 anchor = perception.LastKnownTargetPosition;
        EnsureRuntimePoints(anchor, RuntimePointMode.Search, allowJumpLinksInDynamicSearch, dynamicSearchWaitDuration);
        if (runtimePoints.Count == 0)
        {
            DynamicSearchHasValidPointsInternal(false);
            FinishDynamicSearch(anchor);
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
        if (perception != null)
            perception.ClearLastKnownPosition();

        dynamicPatrolAnchor = anchor;
        hasDynamicPatrolAnchor = true;
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
            dynamicPatrolAnchor = transform.position;
            hasDynamicPatrolAnchor = true;
            ClearRuntimePoints();
        }

        Vector3 anchor = dynamicPatrolAnchor;
        EnsureRuntimePoints(anchor, RuntimePointMode.DynamicPatrol, allowJumpLinksInDynamicPatrol, dynamicPatrolWaitDuration);
        if (runtimePoints.Count == 0)
        {
            DynamicPatrolHasValidPointsInternal(false);
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
        float[] candidateOffsets = new float[]
        {
            0f,
            -dynamicPointNearDistance,
            dynamicPointNearDistance,
            -midDistance,
            midDistance,
            -dynamicPointFarDistance,
            dynamicPointFarDistance,
        };

        for (int i = 0; i < candidateOffsets.Length; i++)
        {
            if (runtimePoints.Count >= dynamicPointMaxCount)
                break;

            float targetX = anchor.x + candidateOffsets[i];
            if (TryBuildGroundedRuntimePoint(targetX, anchor, waitDuration, false, out RuntimeSearchPoint point))
                runtimePoints.Add(point);
        }

        if (includeJumpLinks)
            AppendNearbyJumpLinkedRuntimePoints(anchor, waitDuration);

        FinalizeRuntimePointGeneration(anchor);
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
            if (runtimePoints.Count >= dynamicPointMaxCount)
                break;

            float targetX = anchor.x + candidateOffsets[i];
            if (TryBuildGroundedRuntimePoint(targetX, anchor, waitDuration, true, out RuntimeSearchPoint point))
                runtimePoints.Add(point);
        }

        if (includeJumpLinks)
            AppendNearbyJumpLinkedRuntimePoints(anchor, waitDuration);

        FinalizeRuntimePointGeneration(anchor);
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
    }

    private void FinalizeRuntimePointGeneration(Vector3 anchor)
    {
        runtimePoints.Sort((a, b) => a.Score.CompareTo(b.Score));
        if (runtimePoints.Count > dynamicPointMaxCount)
            runtimePoints.RemoveRange(dynamicPointMaxCount, runtimePoints.Count - dynamicPointMaxCount);

        if (runtimePoints.Count == 0)
        {
            hasRuntimePoints = false;
            runtimePointIndex = 0;
            RecordDynamicPointGenerationResult(anchor, runtimePointMode, 0);
            return;
        }

        if (runtimePointMode == RuntimePointMode.DynamicPatrol)
        {
            int bestIndex = -1;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < runtimePoints.Count; i++)
            {
                float travelDistance = Mathf.Abs(runtimePoints[i].WorldPosition.x - transform.position.x);
                if (travelDistance < dynamicPatrolMinTravelDistance)
                    continue;

                float score = travelDistance;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            runtimePointIndex = bestIndex >= 0 ? bestIndex : 0;
        }
        else
        {
            runtimePointIndex = 0;
        }

        RecordDynamicPointGenerationResult(anchor, runtimePointMode, runtimePoints.Count);
    }

    private bool TryBuildGroundedRuntimePoint(float targetX, Vector3 anchor, float waitDuration, bool enforcePatrolTravelDistance, out RuntimeSearchPoint point)
    {
        point = default;
        Vector3 rayOrigin = new Vector3(targetX, anchor.y + dynamicPointRaycastHeight, 0f);
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, dynamicPointRaycastDepth, dynamicPointGroundMask, QueryTriggerInteraction.Ignore))
            return false;

        Vector3 worldPosition = hit.point;
        worldPosition.z = 0f;
        if (Mathf.Abs(worldPosition.x - anchor.x) > dynamicPatrolAnchorRadius)
            return false;

        if (enforcePatrolTravelDistance && Mathf.Abs(worldPosition.x - transform.position.x) < dynamicPatrolMinTravelDistance)
            return false;

        for (int i = 0; i < runtimePoints.Count; i++)
        {
            if (Mathf.Abs(runtimePoints[i].WorldPosition.x - worldPosition.x) < dynamicPointMinSeparation && Mathf.Abs(runtimePoints[i].WorldPosition.y - worldPosition.y) < 0.5f)
                return false;
        }

        float score = Mathf.Abs(worldPosition.x - anchor.x);
        point = new RuntimeSearchPoint(worldPosition, waitDuration, false, null, score);
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

            TryAppendJumpLinkCandidate(anchor, waitDuration, link, start.position, end.position);
            if (link.Bidirectional)
                TryAppendJumpLinkCandidate(anchor, waitDuration, link, end.position, start.position);
        }
    }

    private void TryAppendJumpLinkCandidate(Vector3 anchor, float waitDuration, EnemyJumpLink25D link, Vector3 startPos, Vector3 endPos)
    {
        if (runtimePoints.Count >= dynamicPointMaxCount)
            return;

        if (Mathf.Abs(startPos.x - anchor.x) > jumpLinkSearchRadius)
            return;

        Vector3 worldPosition = endPos;
        worldPosition.z = 0f;
        if (Mathf.Abs(worldPosition.x - anchor.x) > dynamicPatrolAnchorRadius * 1.5f)
            return;

        for (int i = 0; i < runtimePoints.Count; i++)
        {
            if (Mathf.Abs(runtimePoints[i].WorldPosition.x - worldPosition.x) < dynamicPointMinSeparation && Mathf.Abs(runtimePoints[i].WorldPosition.y - worldPosition.y) < 0.5f)
                return;
        }

        float score = Mathf.Abs(worldPosition.x - anchor.x) + Mathf.Max(0f, link.TraversalCost);
        runtimePoints.Add(new RuntimeSearchPoint(worldPosition, waitDuration, true, link, score));
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
            if (TickJumpLinkTraversal(point.JumpLink, point.WorldPosition, arrivalTolerance))
            {
                currentAction = EnemyAction25D.UseJumpLink;
                return false;
            }
        }

        float deltaX = point.WorldPosition.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= arrivalTolerance)
        {
            currentAction = EnemyAction25D.WaitAtPoint;
            character.StopMovement();

            if (runtimePointWaitEndTime <= 0f)
                runtimePointWaitEndTime = Time.time + point.WaitDuration;

            if (Time.time < runtimePointWaitEndTime)
                return false;

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

        currentAction = EnemyAction25D.MoveToDynamicPoint;
        MoveTowardsX(point.WorldPosition.x);
        return false;
    }

    private bool TickJumpLinkTraversal(EnemyJumpLink25D link, Vector3 desiredPosition, float arrivalTolerance)
    {
        if (character == null || link == null)
            return false;

        if (character.IsJumpTraversalActive)
        {
            currentAction = EnemyAction25D.UseJumpLink;
            activeTraversalLink = link;
            return true;
        }

        if (activeTraversalLink == link)
        {
            if (Mathf.Abs(transform.position.x - desiredPosition.x) <= Mathf.Max(arrivalTolerance, link.LandingTolerance))
            {
                activeTraversalLink = null;
                return false;
            }
        }

        if (Time.time < lastJumpLinkUseTime + link.JumpCooldownAfterUse)
        {
            currentAction = EnemyAction25D.UseJumpLink;
            return true;
        }

        if (!link.TryGetTraversal(transform.position, desiredPosition, out Vector3 traversalStart, out _))
            return false;

        float deltaToStart = traversalStart.x - transform.position.x;
        if (Mathf.Abs(deltaToStart) > link.ApproachDistance)
        {
            currentAction = EnemyAction25D.UseJumpLink;
            MoveTowardsX(traversalStart.x);
            return true;
        }

        currentAction = EnemyAction25D.UseJumpLink;
        character.StopMovement();
        if (character.TryExecuteJumpLinkTraversal(link, desiredPosition))
        {
            activeTraversalLink = link;
            lastJumpLinkUseTime = Time.time;
            return true;
        }

        return true;
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

    private bool IsAtPoint(Vector3 point, float tolerance)
    {
        return Mathf.Abs(point.x - transform.position.x) <= tolerance;
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

    private void MoveTowardsX(float targetX)
    {
        if (character == null)
            return;

        float deltaX = targetX - transform.position.x;
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
        return Mathf.Abs(targetPosition.y - transform.position.y) > 0.75f;
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

    private void ClearRuntimePoints()
    {
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

        currentState = nextState;
        if (nextState != BrainState.PatrolFixed)
            patrolWaitEndTime = 0f;
        if (nextState != BrainState.SearchDynamic && nextState != BrainState.PatrolDynamic)
            runtimePointWaitEndTime = 0f;
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

        if (drawDynamicPointGenerationWarnings)
            DrawDynamicPointGenerationWarningGizmos();
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

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(OffsetGizmoPosition(point.WorldPosition), gizmoCurrentPointRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, OffsetGizmoPosition(point.WorldPosition));
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

            Gizmos.color = fadedCyan;
            Gizmos.DrawSphere(OffsetGizmoPosition(point.WorldPosition), gizmoPointRadius);
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
        dynamicPointMinSeparation = Mathf.Max(0f, dynamicPointMinSeparation);
        dynamicPointRaycastHeight = Mathf.Max(0f, dynamicPointRaycastHeight);
        dynamicPointRaycastDepth = Mathf.Max(0f, dynamicPointRaycastDepth);
        dynamicPatrolAnchorRadius = Mathf.Max(0f, dynamicPatrolAnchorRadius);
        dynamicSearchArrivalDistance = Mathf.Max(0f, dynamicSearchArrivalDistance);
        dynamicPatrolArrivalDistance = Mathf.Max(0f, dynamicPatrolArrivalDistance);
        dynamicSearchWaitDuration = Mathf.Max(0f, dynamicSearchWaitDuration);
        dynamicPatrolWaitDuration = Mathf.Max(0f, dynamicPatrolWaitDuration);
        dynamicPatrolMinTravelDistance = Mathf.Max(0f, dynamicPatrolMinTravelDistance);
        searchFacingDeadZone = Mathf.Max(0f, searchFacingDeadZone);
        desiredCombatMinRange = Mathf.Max(0f, desiredCombatMinRange);
        desiredCombatMaxRange = Mathf.Max(desiredCombatMinRange, desiredCombatMaxRange);
        combatMoveDeadZone = Mathf.Max(0f, combatMoveDeadZone);
        backpedalStartRange = Mathf.Max(0f, backpedalStartRange);
        backpedalSpeedMultiplier = Mathf.Max(0f, backpedalSpeedMultiplier);
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
