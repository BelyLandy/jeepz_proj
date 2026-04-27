using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemySquadMember25D : MonoBehaviour
{
    [Header("Squad")]
    [SerializeField] private EnemySquad25D squad;

    [Header("References")]
    [SerializeField] private EnemyHealth25D health;
    [SerializeField] private EnemyHealthBarPresenter25D healthBarPresenter;
    [SerializeField] private EnemyBrainBT25D brain;
    [SerializeField] private EnemyPerception25D perception;

    [Header("Debug")]
    [SerializeField] private bool logMemberEvents;

    private bool isLeader;
    private bool subscribedToHealth;

    public EnemySquad25D Squad => squad;
    public bool IsLeader => isLeader;
    public EnemyHealth25D Health => health;
    public EnemyHealthBarPresenter25D HealthBarPresenter => healthBarPresenter;
    public EnemyBrainBT25D Brain => brain;
    public EnemyPerception25D Perception => perception;

    public bool IsAlive => health == null || !health.IsDead;
    public bool IsInCombat => brain != null && brain.IsInCombat;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeHealth();
    }

    private void OnDisable()
    {
        UnsubscribeHealth();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    public void AssignToSquad(EnemySquad25D newSquad)
    {
        squad = newSquad;
    }

    public void SetLeader(bool value)
    {
        if (isLeader == value)
            return;

        isLeader = value;

        if (healthBarPresenter != null)
        {
            healthBarPresenter.SetLeaderVisual(value);
        }
        else if (logMemberEvents)
        {
            Debug.LogWarning(
                "[EnemySquadMember25D] Cannot apply leader visual because EnemyHealthBarPresenter25D is missing.\n" +
                $"Member: {name}\n" +
                $"IsLeader: {isLeader}",
                this);
        }

        if (logMemberEvents)
        {
            Debug.Log(
                "[EnemySquadMember25D] Leader state changed\n" +
                $"Member: {name}\n" +
                $"Squad: {(squad != null ? squad.name : "None")}\n" +
                $"IsLeader: {isLeader}",
                this);
        }
    }

    public bool TryResolveSquadPanicFleeSource(Vector3 fallbackPosition, out Vector3 fleeFrom, out string reason)
    {
        fleeFrom = fallbackPosition;
        reason = "LeaderDeathPositionFallback";

        if (perception != null)
        {
            if (perception.IsTargetVisible && perception.CurrentTarget != null)
            {
                fleeFrom = perception.CurrentTarget.position;
                reason = "VisibleTarget";
                return true;
            }

            if (perception.HasLastKnownPosition)
            {
                fleeFrom = perception.LastKnownTargetPosition;
                reason = "LastKnown";
                return true;
            }
        }

        return true;
    }

    private void ResolveReferences()
    {
        if (health == null)
            health = GetComponentInParent<EnemyHealth25D>();

        if (healthBarPresenter == null)
            healthBarPresenter = GetComponentInParent<EnemyHealthBarPresenter25D>();

        if (brain == null)
            brain = GetComponentInParent<EnemyBrainBT25D>();

        if (perception == null)
            perception = GetComponentInParent<EnemyPerception25D>();
    }

    private void SubscribeHealth()
    {
        if (subscribedToHealth || health == null)
            return;

        health.Died += HandleDied;
        subscribedToHealth = true;
    }

    private void UnsubscribeHealth()
    {
        if (!subscribedToHealth || health == null)
            return;

        health.Died -= HandleDied;
        subscribedToHealth = false;
    }

    private void HandleDied()
    {
        if (logMemberEvents)
        {
            Debug.Log(
                "[EnemySquadMember25D] Member died\n" +
                $"Member: {name}\n" +
                $"Squad: {(squad != null ? squad.name : "None")}\n" +
                $"WasLeader: {isLeader}",
                this);
        }

        squad?.NotifyMemberDied(this);
    }
}
