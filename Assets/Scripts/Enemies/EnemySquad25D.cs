using System.Collections.Generic;
using UnityEngine;

public enum EnemySquadLeaderSelectionMode
{
    Random,
    FirstAlive,
    Manual
}

[DisallowMultipleComponent]
public sealed class EnemySquad25D : MonoBehaviour
{
    [Header("Members")]
    [SerializeField] private List<EnemySquadMember25D> members = new List<EnemySquadMember25D>();

    [Header("Leader Selection")]
    [SerializeField] private bool selectLeaderOnStart = true;
    [SerializeField] private EnemySquadLeaderSelectionMode leaderSelectionMode = EnemySquadLeaderSelectionMode.Random;
    [SerializeField] private EnemySquadMember25D manualLeader;

    [Header("Leader Death")]
    [SerializeField] private bool clearLeaderVisualOnDeath = true;
    [SerializeField] private bool reassignLeaderOnLeaderDeath;

    [Header("Leader Death Panic")]
    [SerializeField] private bool triggerPanicFleeOnLeaderDeath = true;
    [SerializeField, Min(0f)] private float leaderDeathPanicDuration = 3f;
    [SerializeField, Min(0f)] private float leaderDeathPanicDurationRandomRange = 0.35f;
    [SerializeField] private bool includeCombatMembersInPanic = true;
    [SerializeField] private bool skipDeadMembersForPanic = true;
    [SerializeField] private bool skipLeaderForPanic = true;
    [SerializeField] private bool usePerMemberFleeSource = true;
    [SerializeField] private bool fallbackToLeaderDeathPosition = true;
    [SerializeField] private bool logLeaderDeathPanic = true;

    [Header("Debug")]
    [SerializeField] private bool logSquadEvents = true;

    private EnemySquadMember25D currentLeader;
    private bool initialized;

    public event System.Action<EnemySquadMember25D> LeaderSelected;
    public event System.Action<EnemySquadMember25D> LeaderDied;
    public event System.Action<EnemySquadMember25D> MemberDied;
    public event System.Action<EnemySquadMember25D, Vector3, float> LeaderDeathPanicTriggered;

    public IReadOnlyList<EnemySquadMember25D> Members => members;
    public EnemySquadMember25D CurrentLeader => currentLeader;
    public bool HasLeader => currentLeader != null;
    public bool IsInitialized => initialized;

    private void Awake()
    {
        NormalizeMembers();
        AssignMembersToThisSquad();
    }

    private void Start()
    {
        initialized = true;

        LogSquad(
            "SquadInitialized",
            $"SelectionMode: {leaderSelectionMode}\n" +
            $"SelectLeaderOnStart: {selectLeaderOnStart}\n" +
            $"AliveMembers: {CountAliveMembers()}\n" +
            $"TotalMembers: {members.Count}");

        if (selectLeaderOnStart)
            SelectInitialLeader();
    }

    private void OnValidate()
    {
        NormalizeMembers();
    }

    public void NotifyMemberDied(EnemySquadMember25D member)
    {
        if (member == null)
            return;

        bool wasLeader = member == currentLeader;

        MemberDied?.Invoke(member);

        LogSquad(
            wasLeader ? "LeaderDied" : "MemberDied",
            $"Member: {member.name}\n" +
            $"WasLeader: {wasLeader}\n" +
            $"RemainingAliveMembers: {CountAliveMembers()}\n" +
            $"CurrentLeaderBeforeHandling: {(currentLeader != null ? currentLeader.name : "None")}");

        if (!wasLeader)
            return;

        HandleLeaderDied(member);
    }

    public void SelectInitialLeader()
    {
        EnemySquadMember25D leader = PickLeader();

        if (leader == null)
        {
            LogSquad(
                "LeaderSelectionFailed",
                "No alive member available.");
            return;
        }

        SetCurrentLeader(leader, "InitialSelection");
    }

    public void ClearCurrentLeader(string reason = "Unspecified")
    {
        if (currentLeader == null)
            return;

        EnemySquadMember25D previousLeader = currentLeader;
        previousLeader.SetLeader(false);
        currentLeader = null;

        LogSquad(
            "LeaderCleared",
            $"Reason: {reason}\n" +
            $"PreviousLeader: {previousLeader.name}");
    }

    public void SetCurrentLeader(EnemySquadMember25D leader, string reason = "ManualSet")
    {
        if (leader != null && !members.Contains(leader))
        {
            LogSquad(
                "LeaderSelectionRejected",
                $"Reason: {reason}\n" +
                $"RejectedLeader: {leader.name}\n" +
                "Details: selected leader is not in squad members list.");
            return;
        }

        if (leader != null && !leader.IsAlive)
        {
            LogSquad(
                "LeaderSelectionRejected",
                $"Reason: {reason}\n" +
                $"RejectedLeader: {leader.name}\n" +
                "Details: selected leader is not alive.");
            return;
        }

        if (currentLeader == leader)
            return;

        if (currentLeader != null)
            currentLeader.SetLeader(false);

        currentLeader = leader;

        if (currentLeader != null)
        {
            currentLeader.AssignToSquad(this);
            currentLeader.SetLeader(true);
            LeaderSelected?.Invoke(currentLeader);
        }

        LogSquad(
            "LeaderSelected",
            $"Reason: {reason}\n" +
            $"Leader: {(currentLeader != null ? currentLeader.name : "None")}\n" +
            $"Mode: {leaderSelectionMode}\n" +
            $"AliveMembers: {CountAliveMembers()}\n" +
            $"TotalMembers: {members.Count}");
    }

    private void NormalizeMembers()
    {
        if (members == null)
            members = new List<EnemySquadMember25D>();

        for (int i = members.Count - 1; i >= 0; i--)
        {
            if (members[i] == null)
                members.RemoveAt(i);
        }

        for (int i = members.Count - 1; i >= 0; i--)
        {
            EnemySquadMember25D member = members[i];
            if (member != null && members.IndexOf(member) != i)
                members.RemoveAt(i);
        }
    }

    private void AssignMembersToThisSquad()
    {
        for (int i = 0; i < members.Count; i++)
        {
            EnemySquadMember25D member = members[i];
            if (member == null)
                continue;

            member.AssignToSquad(this);
            member.SetLeader(false);
        }
    }

    private EnemySquadMember25D PickLeader()
    {
        List<EnemySquadMember25D> aliveMembers = GetAliveMembers();

        if (aliveMembers.Count <= 0)
            return null;

        switch (leaderSelectionMode)
        {
            case EnemySquadLeaderSelectionMode.Manual:
                if (manualLeader != null && members.Contains(manualLeader) && manualLeader.IsAlive)
                    return manualLeader;
                return aliveMembers[0];

            case EnemySquadLeaderSelectionMode.FirstAlive:
                return aliveMembers[0];

            case EnemySquadLeaderSelectionMode.Random:
            default:
                int index = Random.Range(0, aliveMembers.Count);
                return aliveMembers[index];
        }
    }

    private List<EnemySquadMember25D> GetAliveMembers()
    {
        List<EnemySquadMember25D> alive = new List<EnemySquadMember25D>();

        for (int i = 0; i < members.Count; i++)
        {
            EnemySquadMember25D member = members[i];
            if (member == null)
                continue;

            if (member.IsAlive)
                alive.Add(member);
        }

        return alive;
    }

    private int CountAliveMembers()
    {
        int count = 0;

        for (int i = 0; i < members.Count; i++)
        {
            EnemySquadMember25D member = members[i];
            if (member != null && member.IsAlive)
                count++;
        }

        return count;
    }

    private void HandleLeaderDied(EnemySquadMember25D leader)
    {
        if (leader == null)
            return;

        LeaderDied?.Invoke(leader);

        if (clearLeaderVisualOnDeath)
            leader.SetLeader(false);

        currentLeader = null;

        LogSquad(
            "LeaderCleared",
            $"Leader: {leader.name}\n" +
            $"ReassignLeaderOnLeaderDeath: {reassignLeaderOnLeaderDeath}");

        if (triggerPanicFleeOnLeaderDeath)
            TriggerLeaderDeathPanic(leader);

        if (reassignLeaderOnLeaderDeath)
        {
            EnemySquadMember25D newLeader = PickLeader();
            if (newLeader != null && newLeader != leader)
                SetCurrentLeader(newLeader, "LeaderDeathReassign");
        }
    }

    private void TriggerLeaderDeathPanic(EnemySquadMember25D deadLeader)
    {
        if (!triggerPanicFleeOnLeaderDeath || deadLeader == null)
            return;

        Vector3 leaderDeathPosition = deadLeader.transform.position;
        int requestedCount = 0;
        int skippedCount = 0;

        for (int i = 0; i < members.Count; i++)
        {
            EnemySquadMember25D member = members[i];

            if (member == null)
            {
                skippedCount++;
                continue;
            }

            if (skipLeaderForPanic && member == deadLeader)
            {
                skippedCount++;
                continue;
            }

            if (skipDeadMembersForPanic && !member.IsAlive)
            {
                skippedCount++;
                continue;
            }

            if (!includeCombatMembersInPanic && member.IsInCombat)
            {
                skippedCount++;
                continue;
            }

            if (RequestPanicForMember(member, deadLeader, leaderDeathPosition))
                requestedCount++;
            else
                skippedCount++;
        }

        LeaderDeathPanicTriggered?.Invoke(deadLeader, leaderDeathPosition, leaderDeathPanicDuration);
        LogLeaderDeathPanic(deadLeader, leaderDeathPosition, requestedCount, skippedCount);
    }

    private bool RequestPanicForMember(EnemySquadMember25D member, EnemySquadMember25D deadLeader, Vector3 leaderDeathPosition)
    {
        if (member == null || member.Brain == null)
            return false;

        Vector3 fleeFrom;
        string sourceReason;

        if (usePerMemberFleeSource && member.TryResolveSquadPanicFleeSource(leaderDeathPosition, out fleeFrom, out sourceReason))
        {
            // Resolved by member perception.
        }
        else if (fallbackToLeaderDeathPosition)
        {
            fleeFrom = leaderDeathPosition;
            sourceReason = "LeaderDeathPositionFallback";
        }
        else
        {
            return false;
        }

        float duration = leaderDeathPanicDuration;
        if (leaderDeathPanicDurationRandomRange > 0f)
        {
            duration += Random.Range(-leaderDeathPanicDurationRandomRange, leaderDeathPanicDurationRandomRange);
            duration = Mathf.Max(0f, duration);
        }

        return member.Brain.RequestSquadPanicFlee(fleeFrom, duration, "SquadLeaderDied:" + sourceReason);
    }

    private void LogLeaderDeathPanic(EnemySquadMember25D deadLeader, Vector3 leaderDeathPosition, int requestedCount, int skippedCount)
    {
        if (!logLeaderDeathPanic)
            return;

        Debug.Log(
            $"[EnemySquad25D] Leader death panic triggered\n" +
            $"Squad: {name}\n" +
            $"DeadLeader: {(deadLeader != null ? deadLeader.name : "None")}\n" +
            $"LeaderDeathPosition: {leaderDeathPosition}\n" +
            $"Duration: {leaderDeathPanicDuration:F2}\n" +
            $"DurationRandomRange: {leaderDeathPanicDurationRandomRange:F2}\n" +
            $"RequestedMembers: {requestedCount}\n" +
            $"SkippedMembers: {skippedCount}\n" +
            $"TotalMembers: {(members != null ? members.Count : 0)}",
            this);
    }

    private void LogSquad(string eventName, string details)
    {
        if (!logSquadEvents)
            return;

        Debug.Log(
            $"[EnemySquad25D] {eventName}\n" +
            $"Squad: {name}\n" +
            $"Members: {(members != null ? members.Count : 0)}\n" +
            $"CurrentLeader: {(currentLeader != null ? currentLeader.name : "None")}\n" +
            details,
            this);
    }
}
