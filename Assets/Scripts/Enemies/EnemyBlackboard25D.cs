using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyBlackboard25D : MonoBehaviour
{
    [SerializeField] private Transform self;
    [SerializeField] private bool useFixedPatrolOnStart = true;

    public Transform Self => self != null ? self : transform;
    public Transform CurrentTarget { get; private set; }
    public bool TargetVisible { get; private set; }
    public bool TargetInSight { get; private set; }
    public bool HasLastKnownTargetPosition { get; private set; }
    public Vector3 LastKnownTargetPosition { get; private set; }
    public Vector3 TargetVelocityEstimate { get; private set; }
    public float DistanceToTarget { get; private set; }
    public bool IsAlert { get; private set; }
    public bool IsInCombat { get; private set; }
    public bool CanShoot { get; private set; }
    public bool ShouldMove { get; private set; }
    public float DesiredMoveX { get; private set; }
    public bool IsTakingCover { get; private set; }
    public EnemyCoverPoint25D SelectedCover { get; private set; }
    public bool HasSelectedCover => SelectedCover != null;

    public bool HasEverDetectedPlayer { get; private set; }
    public bool UseFixedPatrolOnStart => useFixedPatrolOnStart;
    public bool HasFixedPatrolRoute { get; private set; }
    public EnemyPatrolPoint25D CurrentFixedPatrolPoint { get; private set; }
    public Vector3 DynamicPatrolAnchor { get; private set; }
    public bool HasDynamicPatrolAnchor { get; private set; }
    public EnemyJumpLink25D CurrentTraversalLink { get; private set; }
    public bool IsUsingDynamicPatrol { get; private set; }
    public bool IsUsingDynamicSearch { get; private set; }

    private void Reset()
    {
        if (self == null)
            self = transform;
    }

    private void Awake()
    {
        if (self == null)
            self = transform;
    }

    public void SetTarget(Transform target)
    {
        CurrentTarget = target;
    }

    public void SetPerception(bool visible, bool inSight, bool hasLastKnownPosition, Vector3 lastKnownPosition, Vector3 velocityEstimate, float distanceToTarget)
    {
        TargetVisible = visible;
        TargetInSight = inSight;
        HasLastKnownTargetPosition = hasLastKnownPosition;
        LastKnownTargetPosition = lastKnownPosition;
        TargetVelocityEstimate = velocityEstimate;
        DistanceToTarget = distanceToTarget;

        if (visible && CurrentTarget != null)
            HasEverDetectedPlayer = true;
    }

    public void SetBrainState(bool alert, bool inCombat, bool canShoot, bool shouldMove, float desiredMoveX)
    {
        IsAlert = alert;
        IsInCombat = inCombat;
        CanShoot = canShoot;
        ShouldMove = shouldMove;
        DesiredMoveX = Mathf.Clamp(desiredMoveX, -1f, 1f);
    }

    public void SetCoverState(bool takingCover, EnemyCoverPoint25D selectedCover)
    {
        IsTakingCover = takingCover;
        SelectedCover = selectedCover;
    }

    public void SetPatrolContext(bool hasFixedPatrolRoute, EnemyPatrolPoint25D currentFixedPatrolPoint, bool isUsingDynamicPatrol, bool isUsingDynamicSearch, Vector3 dynamicPatrolAnchor, bool hasDynamicPatrolAnchor)
    {
        HasFixedPatrolRoute = hasFixedPatrolRoute;
        CurrentFixedPatrolPoint = currentFixedPatrolPoint;
        IsUsingDynamicPatrol = isUsingDynamicPatrol;
        IsUsingDynamicSearch = isUsingDynamicSearch;
        DynamicPatrolAnchor = dynamicPatrolAnchor;
        HasDynamicPatrolAnchor = hasDynamicPatrolAnchor;
    }

    public void SetTraversalLink(EnemyJumpLink25D link)
    {
        CurrentTraversalLink = link;
    }

    public void MarkPlayerDetectedForever()
    {
        HasEverDetectedPlayer = true;
    }
}
