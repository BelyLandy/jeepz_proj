using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAnimatorBridge25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyCharacter25D character;
    [SerializeField] private EnemyHealth25D health;
    [SerializeField] private EnemyStun25D stun;
    [SerializeField] private EnemyKnockbackReceiver25D knockback;
    [SerializeField] private EnemyBrainBT25D btBrain;
    [SerializeField] private EnemyBallisticShooter25D shooter;
    [SerializeField] private EnemyGrenadeThrower25D grenadeThrower;

    [Header("Parameter Names")]
    [SerializeField] private string facingBlendParam = "FacingBlend";
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string verticalSpeedParam = "VerticalSpeed";
    [SerializeField] private string isGroundedParam = "IsGrounded";
    [SerializeField] private string isStunnedParam = "IsStunned";
    [SerializeField] private string isDeadParam = "IsDead";
    [SerializeField] private string isLaunchedParam = "IsLaunched";
    [SerializeField] private string isRecoveringParam = "IsRecovering";
    [SerializeField] private string isAlertParam = "IsAlert";
    [SerializeField] private string isInCombatParam = "IsInCombat";
    [SerializeField] private string isTakingCoverParam = "IsTakingCover";
    [SerializeField] private string hitTriggerParam = "Hit";
    [SerializeField] private string deathTriggerParam = "Death";
    [SerializeField] private string impactTriggerParam = "LandHard";
    [SerializeField] private string getUpTriggerParam = "GetUp";
    [SerializeField] private string fireTriggerParam = "Fire";
    [SerializeField] private string isPreparingGrenadeParam = "IsPreparingGrenade";
    [SerializeField] private string throwGrenadeTriggerParam = "ThrowGrenade";

    private int lastSeenHitEventVersion = -1;
    private int lastSeenDeathEventVersion = -1;
    private int lastSeenImpactEventVersion = -1;
    private int lastSeenRecoveryEventVersion = -1;
    private int lastSeenFireEventVersion = -1;
    private int lastSeenGrenadeThrowEventVersion = -1;

    private void Reset()
    {
        AutoAssign();
    }

    private void Awake()
    {
        AutoAssign();
    }

    private void OnValidate()
    {
        AutoAssign();
    }

    private void Update()
    {
        if (animator == null)
            return;

        if (character != null)
        {
            animator.SetFloat(facingBlendParam, character.FacingSign >= 0 ? 1f : -1f);
            animator.SetFloat(speedParam, character.HorizontalSpeedAbs);
            animator.SetFloat(verticalSpeedParam, character.VerticalSpeed);
            animator.SetBool(isGroundedParam, character.IsGrounded);
        }

        animator.SetBool(isStunnedParam, stun != null && stun.IsStunned);
        animator.SetBool(isDeadParam, health != null && health.IsDead);
        animator.SetBool(isLaunchedParam, knockback != null && knockback.IsLaunched);
        animator.SetBool(isRecoveringParam, knockback != null && knockback.IsRecovering);
        animator.SetBool(isAlertParam, GetIsAlert());
        animator.SetBool(isInCombatParam, GetIsInCombat());
        animator.SetBool(isTakingCoverParam, GetIsTakingCover());
        animator.SetBool(isPreparingGrenadeParam, grenadeThrower != null && grenadeThrower.IsPreparingThrow);

        if (health != null)
        {
            if (health.LastHitEventVersion != lastSeenHitEventVersion)
            {
                lastSeenHitEventVersion = health.LastHitEventVersion;
                if (!string.IsNullOrWhiteSpace(hitTriggerParam))
                    animator.SetTrigger(hitTriggerParam);
            }

            if (health.LastDeathEventVersion != lastSeenDeathEventVersion)
            {
                lastSeenDeathEventVersion = health.LastDeathEventVersion;
                if (!string.IsNullOrWhiteSpace(deathTriggerParam))
                    animator.SetTrigger(deathTriggerParam);
            }
        }

        if (knockback != null)
        {
            if (knockback.LastImpactEventVersion != lastSeenImpactEventVersion)
            {
                lastSeenImpactEventVersion = knockback.LastImpactEventVersion;
                if (!string.IsNullOrWhiteSpace(impactTriggerParam))
                    animator.SetTrigger(impactTriggerParam);
            }

            if (knockback.LastRecoveryEventVersion != lastSeenRecoveryEventVersion)
            {
                lastSeenRecoveryEventVersion = knockback.LastRecoveryEventVersion;
                if (!string.IsNullOrWhiteSpace(getUpTriggerParam))
                    animator.SetTrigger(getUpTriggerParam);
            }
        }

        if (shooter != null && shooter.LastFireEventVersion != lastSeenFireEventVersion)
        {
            lastSeenFireEventVersion = shooter.LastFireEventVersion;
            if (!string.IsNullOrWhiteSpace(fireTriggerParam))
                animator.SetTrigger(fireTriggerParam);
        }

        if (grenadeThrower != null && grenadeThrower.LastThrowEventVersion != lastSeenGrenadeThrowEventVersion)
        {
            lastSeenGrenadeThrowEventVersion = grenadeThrower.LastThrowEventVersion;
            if (!string.IsNullOrWhiteSpace(throwGrenadeTriggerParam))
                animator.SetTrigger(throwGrenadeTriggerParam);
        }
    }

    private bool GetIsAlert()
    {
        if (btBrain != null)
            return btBrain.IsAlert;
        return false;
    }

    private bool GetIsInCombat()
    {
        if (btBrain != null)
            return btBrain.IsInCombat;
        return false;
    }

    private bool GetIsTakingCover()
    {
        if (btBrain != null)
            return btBrain.IsTakingCover;
        return false;
    }

    private void AutoAssign()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (character == null)
            character = GetComponent<EnemyCharacter25D>();
        if (health == null)
            health = GetComponent<EnemyHealth25D>();
        if (stun == null)
            stun = GetComponent<EnemyStun25D>();
        if (knockback == null)
            knockback = GetComponent<EnemyKnockbackReceiver25D>();
        if (btBrain == null)
            btBrain = GetComponent<EnemyBrainBT25D>();
        if (shooter == null)
            shooter = GetComponent<EnemyBallisticShooter25D>();
        if (grenadeThrower == null)
            grenadeThrower = GetComponent<EnemyGrenadeThrower25D>();
    }
}
