using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemySimpleBrain25D : MonoBehaviour
{
    public enum BrainState
    {
        Idle = 0,
        Patrol = 1,
        Search = 2,
        Combat = 3,
        Disabled = 4,
    }

    [Header("References")]
    [SerializeField] private EnemyCharacter25D character;
    [SerializeField] private EnemyHealth25D health;
    [SerializeField] private EnemyStun25D stun;
    [SerializeField] private EnemyKnockbackReceiver25D knockback;
    [SerializeField] private EnemyPerception25D perception;
    [SerializeField] private EnemyBlackboard25D blackboard;
    [SerializeField] private EnemyBallisticShooter25D shooter;

    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField, Min(0f)] private float patrolPointReachDistance = 0.4f;
    [SerializeField, Min(0f)] private float patrolWaitDuration = 0.75f;
    [SerializeField, Min(0f)] private float patrolMoveDeadZone = 0.1f;

    [Header("Search")]
    [SerializeField, Min(0f)] private float searchArrivalDistance = 0.5f;
    [SerializeField, Min(0f)] private float searchWaitDuration = 1.25f;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float desiredCombatMinRange = 5f;
    [SerializeField, Min(0f)] private float desiredCombatMaxRange = 10f;
    [SerializeField, Min(0f)] private float combatMoveDeadZone = 0.2f;
    [SerializeField] private bool strafeAroundTarget = false;

    private BrainState currentState;
    private int patrolIndex;
    private float stateEnterTime;
    private float patrolWaitEndTime;
    private float searchWaitEndTime;

    public BrainState CurrentState => currentState;
    public bool IsAlert => currentState == BrainState.Search || currentState == BrainState.Combat;
    public bool IsInCombat => currentState == BrainState.Combat;

    private void Reset()
    {
        AutoAssign();
        ClampSettings();
    }

    private void Awake()
    {
        AutoAssign();
        ClampSettings();
        EnterState(BrainState.Idle);
    }

    private void OnValidate()
    {
        AutoAssign();
        ClampSettings();
    }

    private void Update()
    {
        AutoAssign();
        UpdateStateSelection();
        TickCurrentState();
        UpdateBlackboard();
    }

    private void UpdateStateSelection()
    {
        if (health != null && health.IsDead)
        {
            EnterState(BrainState.Disabled);
            return;
        }

        if ((stun != null && stun.IsStunned) || (knockback != null && (knockback.IsLaunched || knockback.IsRecovering)))
        {
            EnterState(BrainState.Disabled);
            return;
        }

        bool visible = perception != null && perception.IsTargetVisible;
        bool hasLastKnown = perception != null && perception.HasLastKnownPosition;

        if (visible)
        {
            EnterState(BrainState.Combat);
            return;
        }

        if (hasLastKnown)
        {
            EnterState(BrainState.Search);
            return;
        }

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            EnterState(BrainState.Patrol);
            return;
        }

        EnterState(BrainState.Idle);
    }

    private void TickCurrentState()
    {
        switch (currentState)
        {
            case BrainState.Disabled:
                TickDisabled();
                break;
            case BrainState.Combat:
                TickCombat();
                break;
            case BrainState.Search:
                TickSearch();
                break;
            case BrainState.Patrol:
                TickPatrol();
                break;
            default:
                TickIdle();
                break;
        }
    }

    private void TickDisabled()
    {
        if (character != null)
            character.StopMovement();
    }

    private void TickIdle()
    {
        if (character != null)
            character.StopMovement();
    }

    private void TickPatrol()
    {
        if (character == null || patrolPoints == null || patrolPoints.Length == 0)
            return;

        Transform point = patrolPoints[Mathf.Clamp(patrolIndex, 0, patrolPoints.Length - 1)];
        if (point == null)
        {
            AdvancePatrolIndex();
            return;
        }

        float deltaX = point.position.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= patrolPointReachDistance)
        {
            character.StopMovement();
            character.ForceFacingSign(deltaX >= 0f ? 1 : -1);

            if (Time.time >= patrolWaitEndTime)
            {
                patrolWaitEndTime = Time.time + patrolWaitDuration;
                AdvancePatrolIndex();
            }
            return;
        }

        float moveX = Mathf.Abs(deltaX) <= patrolMoveDeadZone ? 0f : Mathf.Sign(deltaX);
        character.SetMoveInput(moveX);
    }

    private void TickSearch()
    {
        if (character == null || perception == null)
            return;

        if (!perception.HasLastKnownPosition)
        {
            character.StopMovement();
            return;
        }

        Vector3 target = perception.LastKnownTargetPosition;
        float deltaX = target.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= searchArrivalDistance)
        {
            character.StopMovement();
            if (searchWaitEndTime <= 0f)
                searchWaitEndTime = Time.time + searchWaitDuration;
            else if (Time.time >= searchWaitEndTime)
                perception.ClearLastKnownPosition();
            return;
        }

        searchWaitEndTime = 0f;
        character.SetMoveInput(Mathf.Sign(deltaX));
    }

    private void TickCombat()
    {
        if (character == null || perception == null)
            return;

        Transform target = perception.CurrentTarget;
        if (target == null)
        {
            character.StopMovement();
            return;
        }

        Vector3 aimPosition = perception.GetAimPosition();
        float deltaX = aimPosition.x - transform.position.x;
        float absDeltaX = Mathf.Abs(deltaX);
        int desiredFacing = deltaX >= 0f ? 1 : -1;
        character.ForceFacingSign(desiredFacing);

        float moveX = 0f;
        if (absDeltaX > desiredCombatMaxRange)
            moveX = Mathf.Sign(deltaX);
        else if (absDeltaX < desiredCombatMinRange)
            moveX = -Mathf.Sign(deltaX);
        else if (strafeAroundTarget)
            moveX = Mathf.Sign(deltaX) * 0.5f;

        if (Mathf.Abs(moveX) <= combatMoveDeadZone)
            character.StopMovement();
        else
            character.SetMoveInput(Mathf.Clamp(moveX, -1f, 1f));

        if (shooter != null)
            shooter.TryFireAtPerceivedTarget();
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

        bool canShoot = shooter != null && shooter.CanFireNow && currentState == BrainState.Combat;
        bool shouldMove = character != null && Mathf.Abs(character.MoveInputX) > 0.01f;
        float desiredMove = character != null ? character.MoveInputX : 0f;
        blackboard.SetBrainState(IsAlert, IsInCombat, canShoot, shouldMove, desiredMove);
    }

    private void EnterState(BrainState next)
    {
        if (currentState == next)
            return;

        currentState = next;
        stateEnterTime = Time.time;

        if (currentState == BrainState.Search)
            searchWaitEndTime = 0f;
    }

    private void AdvancePatrolIndex()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
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
    }

    private void ClampSettings()
    {
        patrolPointReachDistance = Mathf.Max(0f, patrolPointReachDistance);
        patrolWaitDuration = Mathf.Max(0f, patrolWaitDuration);
        patrolMoveDeadZone = Mathf.Max(0f, patrolMoveDeadZone);
        searchArrivalDistance = Mathf.Max(0f, searchArrivalDistance);
        searchWaitDuration = Mathf.Max(0f, searchWaitDuration);
        desiredCombatMinRange = Mathf.Max(0f, desiredCombatMinRange);
        desiredCombatMaxRange = Mathf.Max(desiredCombatMinRange, desiredCombatMaxRange);
        combatMoveDeadZone = Mathf.Max(0f, combatMoveDeadZone);
    }
}
