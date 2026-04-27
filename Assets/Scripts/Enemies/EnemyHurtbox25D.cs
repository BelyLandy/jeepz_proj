using System.Text;
using UnityEngine;

public enum ProjectileHitAwarenessSource
{
    AttackerGroundedPosition,
    AttackerRawPosition,
    ProjectileSpawnPosition,
    HitDirectionProbe,
    HitPositionOnly
}

public struct ProjectileHitAwarenessContext
{
    public Transform AttackerRoot;
    public Vector3 AttackerPosition;
    public bool HasAttackerPosition;

    public Vector3 ProjectileSpawnPosition;
    public bool HasProjectileSpawnPosition;

    public Vector3 HitPosition;
    public bool HasHitPosition;

    public Vector3 HitDirection;
    public bool HasHitDirection;

    public Vector3 ProjectileVelocity;
    public bool HasProjectileVelocity;

    public string Reason;
}

[DisallowMultipleComponent]
public sealed class EnemyHurtbox25D : MonoBehaviour
{
    [SerializeField] private EnemyHealth25D health;
    [SerializeField] private EnemyStun25D stun;
    [SerializeField] private EnemyKnockbackReceiver25D knockback;
    [SerializeField] private EnemyCharacter25D character;
    [SerializeField] private EnemyPerception25D perception;
    [SerializeField] private EnemyBrainBT25D brain;

    [Header("Projectile Hit Awareness")]
    [SerializeField] private bool reactToPlayerProjectileHits = true;
    [SerializeField] private bool enterCombatOnProjectileHitIfTargetVisible = true;
    [SerializeField] private bool updateLastKnownFromProjectileHit = true;
    [SerializeField] private bool beginSearchFromProjectileHitWhenTargetNotVisible = true;
    [SerializeField] private ProjectileHitAwarenessSource projectileHitAwarenessSource = ProjectileHitAwarenessSource.AttackerGroundedPosition;
    [SerializeField, Min(0f)] private float projectileHitDirectionProbeDistance = 8f;
    [SerializeField] private bool forceFaceProjectileHitSource = true;
    [SerializeField] private bool logProjectileHitAwareness = true;
    [SerializeField] private bool writeProjectileHitAwarenessLogsToFile = true;

    [Header("Default Heavy Melee Response")]
    [SerializeField, Min(0f)] private float defaultHeavyMeleeDamage = 20f;
    [SerializeField, Min(0f)] private float defaultHeavyLaunchHorizontal = 12f;
    [SerializeField, Min(0f)] private float defaultHeavyLaunchVertical = 6f;
    [SerializeField, Min(0f)] private float defaultHeavyRecoveryDuration = 0.65f;

    public EnemyHealth25D Health => health;
    public EnemyStun25D Stun => stun;
    public EnemyKnockbackReceiver25D Knockback => knockback;
    public EnemyCharacter25D Character => character;

    private void Reset()
    {
        AutoAssign();
        ClampSettings();
    }

    private void Awake()
    {
        AutoAssign();
        ClampSettings();
    }

    private void OnValidate()
    {
        AutoAssign();
        ClampSettings();
    }


    public bool ReceiveExplosionHit(Vector3 explosionCenter, float damage, float horizontalKnockback, float verticalKnockback, float stunDuration = 0f)
    {
        if (health == null || health.IsDead)
            return false;

        bool tookDamage = health.ApplyDamage(damage);
        if (health.IsDead)
            return tookDamage;

        bool appliedKnockback = false;
        if (knockback != null && (horizontalKnockback > 0f || verticalKnockback > 0f))
            appliedKnockback = knockback.ApplyExplosionKnockback(explosionCenter, horizontalKnockback, verticalKnockback, stunDuration);
        else if (stun != null && stunDuration > 0f)
            stun.ApplyStun(stunDuration);

        return tookDamage || appliedKnockback;
    }


    private bool TryInterruptJumpTraversalFromProjectileHit(Vector3 hitDirection, float stunDuration)
    {
        if (character == null || !character.IsJumpTraversalActive)
            return false;

        EnemyJumpLink25D jumpLink = character.ActiveJumpLink;
        if (jumpLink == null || !jumpLink.InterruptOnProjectileHit)
            return false;

        Vector3 planar = new Vector3(hitDirection.x, hitDirection.y, 0f);
        if (planar.sqrMagnitude <= 0.0001f)
            planar = Vector3.right;
        else
            planar.Normalize();

        float signX = Mathf.Abs(planar.x) > 0.0001f ? Mathf.Sign(planar.x) : 1f;
        Vector3 interruptVelocity = new Vector3(signX * jumpLink.InterruptKnockbackHorizontalForce, jumpLink.InterruptKnockbackVerticalForce, 0f);

        bool appliedKnockback = false;
        if (knockback != null)
        {
            character.InterruptJumpLinkTraversal(Vector3.zero);
            appliedKnockback = knockback.ApplyKnockbackFromHit(planar, jumpLink.InterruptKnockbackHorizontalForce, jumpLink.InterruptKnockbackVerticalForce, stunDuration);
        }
        else
        {
            appliedKnockback = character.InterruptJumpLinkTraversal(interruptVelocity);
            if (stun != null && stunDuration > 0f)
                stun.ApplyStun(stunDuration);
        }

        return appliedKnockback;
    }

    public bool ReceiveProjectileHit(
        Vector3 hitDirection,
        float damage,
        float stunDuration,
        float horizontalKnockback,
        float verticalKnockback,
        bool horizontalOnlyKnockback,
        bool preserveVerticalVelocity,
        ProjectileHitAwarenessContext awarenessContext)
    {
        if (health == null || health.IsDead)
            return false;

        damage = Mathf.Max(0f, damage);
        stunDuration = Mathf.Max(0f, stunDuration);
        horizontalKnockback = Mathf.Max(0f, horizontalKnockback);
        verticalKnockback = Mathf.Max(0f, verticalKnockback);

        bool tookDamage = health.ApplyDamage(damage);

        if (health.IsDead)
            return tookDamage;

        if (TryInterruptJumpTraversalFromProjectileHit(hitDirection, stunDuration))
        {
            if (reactToPlayerProjectileHits && !health.IsDead)
                NotifyProjectileHitAwareness(awarenessContext, hitDirection, damage, stunDuration, horizontalKnockback, verticalKnockback, horizontalOnlyKnockback, preserveVerticalVelocity);

            return true;
        }

        bool appliedKnockback = false;
        if (knockback != null)
        {
            if (horizontalOnlyKnockback)
            {
                appliedKnockback = knockback.ApplyHorizontalKnockbackFromHit(
                    hitDirection,
                    horizontalKnockback,
                    stunDuration,
                    preserveVerticalVelocity);
            }
            else
            {
                appliedKnockback = knockback.ApplyKnockbackFromHit(hitDirection, horizontalKnockback, verticalKnockback, stunDuration);
            }
        }
        else if (stun != null && stunDuration > 0f)
        {
            stun.ApplyStun(stunDuration);
        }

        if (reactToPlayerProjectileHits && !health.IsDead)
            NotifyProjectileHitAwareness(awarenessContext, hitDirection, damage, stunDuration, horizontalKnockback, verticalKnockback, horizontalOnlyKnockback, preserveVerticalVelocity);

        return tookDamage || appliedKnockback;
    }


    private void NotifyProjectileHitAwareness(
        ProjectileHitAwarenessContext context,
        Vector3 hitDirection,
        float damage,
        float stunDuration,
        float horizontalKnockback,
        float verticalKnockback,
        bool horizontalOnlyKnockback,
        bool preserveVerticalVelocity)
    {
        if (!reactToPlayerProjectileHits)
            return;

        bool targetVisible = perception != null && perception.IsTargetVisible && perception.CurrentTarget != null;
        if (targetVisible && enterCombatOnProjectileHitIfTargetVisible)
        {
            brain?.NotifyProjectileHitVisibleTarget(context);
            LogProjectileHitAwareness(context, damage, hitDirection, "VisibleTargetCombat", true, false, false, Vector3.zero);
            return;
        }

        if (!updateLastKnownFromProjectileHit)
        {
            LogProjectileHitAwareness(context, damage, hitDirection, "LastKnownUpdateDisabled", targetVisible, false, false, Vector3.zero);
            return;
        }

        if (TryResolveProjectileHitAwarenessPoint(context, out Vector3 awarenessPoint, out string awarenessReason, out bool hasFacingHint, out int facingSign, out string facingSource))
        {
            bool updatedPerception = perception != null && perception.NotifyProjectileHitLastKnown(
                awarenessPoint,
                awarenessReason,
                hasFacingHint,
                facingSign,
                facingSource,
                ShouldProjectAwarenessPointToGround(awarenessReason));

            bool searchRequested = false;
            if (updatedPerception && beginSearchFromProjectileHitWhenTargetNotVisible)
            {
                brain?.NotifyProjectileHitSearchRequested(awarenessPoint, awarenessReason);
                searchRequested = true;
            }

            LogProjectileHitAwareness(context, damage, hitDirection, awarenessReason, targetVisible, updatedPerception, searchRequested, awarenessPoint);
            return;
        }

        if (forceFaceProjectileHitSource)
            TryFaceProjectileHitSource(context, hitDirection);

        brain?.NotifyProjectileHitLocalAlert(context);
        LogProjectileHitAwareness(context, damage, hitDirection, "FallbackLocalAlert", targetVisible, false, false, Vector3.zero);
    }

    private bool TryResolveProjectileHitAwarenessPoint(
        ProjectileHitAwarenessContext context,
        out Vector3 point,
        out string reason,
        out bool hasFacingHint,
        out int facingSign,
        out string facingSource)
    {
        point = Vector3.zero;
        reason = "None";
        hasFacingHint = false;
        facingSign = 0;
        facingSource = "None";

        ProjectileHitAwarenessSource[] order = GetAwarenessSourceResolutionOrder();
        for (int i = 0; i < order.Length; i++)
        {
            ProjectileHitAwarenessSource source = order[i];
            if (!TryResolveProjectileHitAwarenessPointForSource(context, source, out point, out reason))
                continue;

            TryResolveAttackerFacingHint(context, out hasFacingHint, out facingSign, out facingSource);
            return true;
        }

        return false;
    }

    private ProjectileHitAwarenessSource[] GetAwarenessSourceResolutionOrder()
    {
        switch (projectileHitAwarenessSource)
        {
            case ProjectileHitAwarenessSource.AttackerRawPosition:
                return new[]
                {
                    ProjectileHitAwarenessSource.AttackerRawPosition,
                    ProjectileHitAwarenessSource.AttackerGroundedPosition,
                    ProjectileHitAwarenessSource.ProjectileSpawnPosition,
                    ProjectileHitAwarenessSource.HitDirectionProbe,
                    ProjectileHitAwarenessSource.HitPositionOnly
                };
            case ProjectileHitAwarenessSource.ProjectileSpawnPosition:
                return new[]
                {
                    ProjectileHitAwarenessSource.ProjectileSpawnPosition,
                    ProjectileHitAwarenessSource.AttackerGroundedPosition,
                    ProjectileHitAwarenessSource.AttackerRawPosition,
                    ProjectileHitAwarenessSource.HitDirectionProbe,
                    ProjectileHitAwarenessSource.HitPositionOnly
                };
            case ProjectileHitAwarenessSource.HitDirectionProbe:
                return new[]
                {
                    ProjectileHitAwarenessSource.HitDirectionProbe,
                    ProjectileHitAwarenessSource.ProjectileSpawnPosition,
                    ProjectileHitAwarenessSource.AttackerGroundedPosition,
                    ProjectileHitAwarenessSource.AttackerRawPosition,
                    ProjectileHitAwarenessSource.HitPositionOnly
                };
            case ProjectileHitAwarenessSource.HitPositionOnly:
                return new[]
                {
                    ProjectileHitAwarenessSource.HitPositionOnly,
                    ProjectileHitAwarenessSource.HitDirectionProbe,
                    ProjectileHitAwarenessSource.ProjectileSpawnPosition,
                    ProjectileHitAwarenessSource.AttackerGroundedPosition,
                    ProjectileHitAwarenessSource.AttackerRawPosition
                };
            default:
                return new[]
                {
                    ProjectileHitAwarenessSource.AttackerGroundedPosition,
                    ProjectileHitAwarenessSource.AttackerRawPosition,
                    ProjectileHitAwarenessSource.ProjectileSpawnPosition,
                    ProjectileHitAwarenessSource.HitDirectionProbe,
                    ProjectileHitAwarenessSource.HitPositionOnly
                };
        }
    }

    private bool TryResolveProjectileHitAwarenessPointForSource(ProjectileHitAwarenessContext context, ProjectileHitAwarenessSource source, out Vector3 point, out string reason)
    {
        point = Vector3.zero;
        reason = "None";

        switch (source)
        {
            case ProjectileHitAwarenessSource.AttackerGroundedPosition:
                if (context.HasAttackerPosition || context.AttackerRoot != null)
                {
                    Vector3 raw = context.HasAttackerPosition ? context.AttackerPosition : context.AttackerRoot.position;
                    point = raw;
                    if (perception != null && perception.TryProjectExternalPointToLastKnownGround(raw, out Vector3 groundedPoint))
                        point = groundedPoint;
                    reason = "ProjectileHitAttackerGrounded";
                    return true;
                }
                break;

            case ProjectileHitAwarenessSource.AttackerRawPosition:
                if (context.HasAttackerPosition || context.AttackerRoot != null)
                {
                    point = context.HasAttackerPosition ? context.AttackerPosition : context.AttackerRoot.position;
                    point.z = 0f;
                    reason = "ProjectileHitAttackerRaw";
                    return true;
                }
                break;

            case ProjectileHitAwarenessSource.ProjectileSpawnPosition:
                if (context.HasProjectileSpawnPosition)
                {
                    point = context.ProjectileSpawnPosition;
                    if (perception != null && perception.TryProjectExternalPointToLastKnownGround(point, out Vector3 groundedSpawn))
                        point = groundedSpawn;
                    reason = "ProjectileHitShotOrigin";
                    return true;
                }
                break;

            case ProjectileHitAwarenessSource.HitDirectionProbe:
                if (TryResolveHitDirectionProbePoint(context, out point))
                {
                    reason = "ProjectileHitDirectionProbe";
                    return true;
                }
                break;

            case ProjectileHitAwarenessSource.HitPositionOnly:
                if (context.HasHitPosition)
                {
                    point = context.HitPosition;
                    if (perception != null && perception.TryProjectExternalPointToLastKnownGround(point, out Vector3 groundedHit))
                        point = groundedHit;
                    reason = "ProjectileHitPositionOnly";
                    return true;
                }
                break;
        }

        return false;
    }

    private bool TryResolveHitDirectionProbePoint(ProjectileHitAwarenessContext context, out Vector3 point)
    {
        point = Vector3.zero;
        Vector3 direction = context.HasHitDirection ? context.HitDirection : Vector3.zero;
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        direction.Normalize();
        Vector3 origin = context.HasHitPosition ? context.HitPosition : transform.position;
        Vector3 probe = origin - direction * projectileHitDirectionProbeDistance;
        probe.z = 0f;
        point = probe;

        if (perception != null && perception.TryProjectExternalPointToLastKnownGround(probe, out Vector3 groundedProbe))
            point = groundedProbe;

        return true;
    }

    private bool TryResolveAttackerFacingHint(ProjectileHitAwarenessContext context, out bool hasFacingHint, out int facingSign, out string facingSource)
    {
        hasFacingHint = false;
        facingSign = 0;
        facingSource = "None";

        if (context.AttackerRoot == null)
            return false;

        RBCharacter25D rbCharacter = context.AttackerRoot.GetComponent<RBCharacter25D>();
        if (rbCharacter == null)
            rbCharacter = context.AttackerRoot.GetComponentInChildren<RBCharacter25D>();
        if (rbCharacter == null)
            rbCharacter = context.AttackerRoot.GetComponentInParent<RBCharacter25D>();

        if (rbCharacter == null)
            return false;

        facingSign = rbCharacter.ResolvedFacingSign >= 0 ? 1 : -1;
        facingSource = "ProjectileHit:RBCharacter25D.ResolvedFacingSign";
        hasFacingHint = true;
        return true;
    }

    private bool ShouldProjectAwarenessPointToGround(string awarenessReason)
    {
        return !string.Equals(awarenessReason, "ProjectileHitAttackerRaw", System.StringComparison.Ordinal);
    }

    private bool TryFaceProjectileHitSource(ProjectileHitAwarenessContext context, Vector3 fallbackHitDirection)
    {
        if (character == null)
            return false;

        Vector3 direction = context.HasHitDirection ? context.HitDirection : fallbackHitDirection;
        direction.z = 0f;
        if (Mathf.Abs(direction.x) <= 0.0001f)
            return false;

        int faceSign = direction.x >= 0f ? -1 : 1;
        character.ForceFacingSign(faceSign);
        return true;
    }

    private void LogProjectileHitAwareness(
        ProjectileHitAwarenessContext context,
        float damage,
        Vector3 hitDirection,
        string resolvedReason,
        bool targetVisible,
        bool willUpdateLastKnown,
        bool willBeginSearch,
        Vector3 resolvedAwarenessPoint)
    {
        if (!logProjectileHitAwareness)
            return;

        StringBuilder sb = new StringBuilder(1024);
        sb.AppendLine("[EnemyHurtbox25D] Projectile hit awareness");
        sb.AppendLine($"Enemy: {name}");
        sb.AppendLine($"Damage: {damage:0.###}");
        sb.AppendLine($"HitDirection: {FormatVector3ForLog(hitDirection)}");
        sb.AppendLine($"AttackerRoot: {(context.AttackerRoot != null ? context.AttackerRoot.name : "None")}");
        sb.AppendLine($"HasAttackerPosition: {context.HasAttackerPosition}");
        sb.AppendLine($"AttackerPosition: {FormatVector3ForLog(context.AttackerPosition)}");
        sb.AppendLine($"HasProjectileSpawnPosition: {context.HasProjectileSpawnPosition}");
        sb.AppendLine($"ProjectileSpawnPosition: {FormatVector3ForLog(context.ProjectileSpawnPosition)}");
        sb.AppendLine($"HasHitPosition: {context.HasHitPosition}");
        sb.AppendLine($"HitPosition: {FormatVector3ForLog(context.HitPosition)}");
        sb.AppendLine($"SelectedAwarenessSource: {projectileHitAwarenessSource}");
        sb.AppendLine($"ResolvedReason: {resolvedReason}");
        sb.AppendLine($"ResolvedAwarenessPoint: {FormatVector3ForLog(resolvedAwarenessPoint)}");
        sb.AppendLine($"TargetVisible: {targetVisible}");
        sb.AppendLine($"WillEnterCombat: {targetVisible && enterCombatOnProjectileHitIfTargetVisible}");
        sb.AppendLine($"WillUpdateLastKnown: {willUpdateLastKnown}");
        sb.AppendLine($"WillBeginSearch: {willBeginSearch}");

        string message = sb.ToString();
        Debug.Log(message, this);
        if (writeProjectileHitAwarenessLogsToFile)
            EnemyDebugFileLogger25D.Write("ProjectileHitAwareness", message, this);
    }

    private static string FormatVector3ForLog(Vector3 value)
    {
        return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
    }

    public bool ReceiveHeavyMeleeHit(Vector3 hitDirection)
    {
        return ReceiveHeavyMeleeHit(hitDirection, defaultHeavyMeleeDamage, defaultHeavyLaunchHorizontal, defaultHeavyLaunchVertical, defaultHeavyRecoveryDuration);
    }

    public bool ReceiveHeavyMeleeHit(Vector3 hitDirection, float damage, float horizontalLaunch, float verticalLaunch, float recoveryDuration)
    {
        if (health == null || health.IsDead)
            return false;

        bool tookDamage = health.ApplyDamage(damage);
        if (health.IsDead)
            return tookDamage;

        bool launched = knockback != null && knockback.ApplyLaunchFromHit(hitDirection, horizontalLaunch, verticalLaunch, recoveryDuration);
        return tookDamage || launched;
    }

    private void AutoAssign()
    {
        if (health == null)
            health = GetComponentInParent<EnemyHealth25D>();
        if (stun == null)
            stun = GetComponentInParent<EnemyStun25D>();
        if (knockback == null)
            knockback = GetComponentInParent<EnemyKnockbackReceiver25D>();
        if (character == null)
            character = GetComponentInParent<EnemyCharacter25D>();
        if (perception == null)
            perception = GetComponentInParent<EnemyPerception25D>();
        if (brain == null)
            brain = GetComponentInParent<EnemyBrainBT25D>();
    }

    private void ClampSettings()
    {
        projectileHitDirectionProbeDistance = Mathf.Max(0f, projectileHitDirectionProbeDistance);
        defaultHeavyMeleeDamage = Mathf.Max(0f, defaultHeavyMeleeDamage);
        defaultHeavyLaunchHorizontal = Mathf.Max(0f, defaultHeavyLaunchHorizontal);
        defaultHeavyLaunchVertical = Mathf.Max(0f, defaultHeavyLaunchVertical);
        defaultHeavyRecoveryDuration = Mathf.Max(0f, defaultHeavyRecoveryDuration);
    }
}
