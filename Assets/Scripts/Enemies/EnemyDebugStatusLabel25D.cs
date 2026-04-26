using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyDebugStatusLabel25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyBrainBT25D brain;
    [SerializeField] private EnemyPerception25D perception;
    [SerializeField] private EnemyBallisticShooter25D shooter;
    [SerializeField] private EnemyGrenadeThrower25D grenadeThrower;
    [SerializeField] private EnemyCloseRangeRepel25D closeRangeRepel;
    [SerializeField] private Transform labelAnchor;
    [SerializeField] private TextMesh label;

    [Header("Display")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.75f, 0f);
    [SerializeField] private bool faceCamera = true;
    [SerializeField, Min(0.01f)] private float refreshInterval = 0.05f;
    [SerializeField] private bool colorByState = true;

    private float nextRefreshTime;

    private void Reset()
    {
        AutoAssign();
        EnsureLabelExists();
        ApplyLabelDefaults();
    }

    private void Awake()
    {
        AutoAssign();
        EnsureLabelExists();
        ApplyLabelDefaults();
        RefreshNow();
    }

    private void OnValidate()
    {
        AutoAssign();
        refreshInterval = Mathf.Max(0.01f, refreshInterval);
        if (!Application.isPlaying)
        {
            EnsureLabelExists();
            ApplyLabelDefaults();
        }
    }

    private void LateUpdate()
    {
        if (label == null)
            return;

        if (labelAnchor != null)
            label.transform.position = labelAnchor.position + worldOffset;
        else
            label.transform.position = transform.position + worldOffset;

        if (faceCamera)
            FaceMainCamera();

        if (Time.time >= nextRefreshTime)
            RefreshNow();
    }

    private void RefreshNow()
    {
        nextRefreshTime = Time.time + refreshInterval;
        if (label == null)
            return;

        string stateText = brain != null ? brain.CurrentState.ToString() : "NoBrain";
        string actionText = brain != null ? brain.CurrentAction.ToString() : "None";

        bool hasTarget = perception != null && perception.HasTarget;
        bool hasTrackedPosition = perception != null && (perception.HasLastKnownPosition || perception.HasTrackedTarget);
        float distance = 0f;
        if (perception != null && (hasTarget || hasTrackedPosition))
            distance = Vector3.Distance(transform.position, perception.GetAimPosition());

        string losText = perception != null && perception.HasLineOfSight ? "Y" : "N";
        string targetText = perception != null && perception.IsTargetVisible ? "Visible" : (hasTarget || hasTrackedPosition ? "Tracked" : "None");
        string rearText = perception != null && (perception.IsTargetInRearAwarenessNow || perception.RearAwarenessTriggeredThisFrame) ? "Y" : "N";
        string alertText = perception != null && perception.IsAlert ? perception.AlertRemaining.ToString("0.0") : "-";
        string primaryCooldown = shooter != null ? shooter.PrimaryCooldownRemaining.ToString("0.00") : "-";
        string grenadeCooldown = grenadeThrower != null ? grenadeThrower.GrenadeCooldownRemaining.ToString("0.0") : "-";
        string grenadeLockout = grenadeThrower != null ? grenadeThrower.GrenadeAttemptLockoutRemaining.ToString("0.0") : "-";
        string grenadeRecovery = grenadeThrower != null ? grenadeThrower.PostThrowRecoveryRemaining.ToString("0.0") : "-";
        string grenadeRetreat = grenadeThrower != null ? grenadeThrower.PostGrenadeRetreatRemaining.ToString("0.0") : "-";
        string repelCooldown = closeRangeRepel != null ? closeRangeRepel.RepelCooldownRemaining.ToString("0.0") : "-";

        string secondLine = $"LOS:{losText} Dist:{distance:0.0} Prim:{primaryCooldown} Gren:{grenadeCooldown}";
        if (grenadeThrower != null && grenadeThrower.IsInPostGrenadeRetreat)
            secondLine += $" Ret:{grenadeRetreat}";
        else if (grenadeThrower != null && grenadeThrower.IsInPostThrowRecovery)
            secondLine += $" Rec:{grenadeRecovery}";
        else
            secondLine += $" Lock:{grenadeLockout}";

        if (closeRangeRepel != null)
            secondLine += $" RepCD:{repelCooldown}";

        secondLine += $" Rear:{rearText} Alert:{alertText}";

        if (brain != null && brain.RuntimePointCount > 0)
            secondLine += $" Pts:{brain.RuntimePointProgress}/{brain.RuntimePointCount}";
        else
            secondLine += $" Target:{targetText}";

        label.text = $"{stateText} | {actionText}\n{secondLine}";
        label.color = colorByState ? GetColorForState(brain != null ? brain.CurrentState : EnemyBrainBT25D.BrainState.Idle) : Color.white;
    }

    private void FaceMainCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        label.transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
    }

    private Color GetColorForState(EnemyBrainBT25D.BrainState state)
    {
        switch (state)
        {
            case EnemyBrainBT25D.BrainState.PatrolFixed:
            case EnemyBrainBT25D.BrainState.PatrolDynamic:
                return Color.yellow;
            case EnemyBrainBT25D.BrainState.SearchDynamic:
                return Color.cyan;
            case EnemyBrainBT25D.BrainState.Combat:
                return Color.red;
            case EnemyBrainBT25D.BrainState.Disabled:
                return Color.gray;
            default:
                return Color.white;
        }
    }

    private void AutoAssign()
    {
        if (brain == null)
            brain = GetComponent<EnemyBrainBT25D>();
        if (perception == null)
            perception = GetComponent<EnemyPerception25D>();
        if (shooter == null)
            shooter = GetComponent<EnemyBallisticShooter25D>();
        if (grenadeThrower == null)
            grenadeThrower = GetComponent<EnemyGrenadeThrower25D>();
        if (closeRangeRepel == null)
            closeRangeRepel = GetComponent<EnemyCloseRangeRepel25D>();
        if (labelAnchor == null)
            labelAnchor = transform;
        if (label == null)
            label = GetComponentInChildren<TextMesh>(true);
    }

    private void EnsureLabelExists()
    {
        if (label != null)
            return;

        Transform parent = labelAnchor != null ? labelAnchor : transform;
        GameObject go = new GameObject("EnemyDebugStatusLabel_Text");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = worldOffset;
        label = go.AddComponent<TextMesh>();
    }

    private void ApplyLabelDefaults()
    {
        if (label == null)
            return;

        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 32;
        label.characterSize = 0.06f;
        label.richText = false;
    }
}
