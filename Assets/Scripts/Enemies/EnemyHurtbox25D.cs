using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyHurtbox25D : MonoBehaviour
{
    [SerializeField] private EnemyHealth25D health;
    [SerializeField] private EnemyStun25D stun;
    [SerializeField] private EnemyKnockbackReceiver25D knockback;
    [SerializeField] private EnemyCharacter25D character;

    [Header("Default Projectile Response")]
    [SerializeField, Min(0f)] private float defaultProjectileDamage = 10f;
    [SerializeField, Min(0f)] private float defaultProjectileStunDuration = 0.2f;
    [SerializeField, Min(0f)] private float defaultProjectileKnockbackHorizontal = 5f;
    [SerializeField, Min(0f)] private float defaultProjectileKnockbackVertical = 1.5f;

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

    public bool ReceiveProjectileHit(Vector3 hitDirection)
    {
        return ReceiveProjectileHit(
            hitDirection,
            defaultProjectileDamage,
            defaultProjectileStunDuration,
            defaultProjectileKnockbackHorizontal,
            defaultProjectileKnockbackVertical);
    }

    public bool ReceiveProjectileHit(Vector3 hitDirection, float damage, float stunDuration, float horizontalKnockback, float verticalKnockback)
    {
        if (health == null || health.IsDead)
            return false;

        bool tookDamage = health.ApplyDamage(damage);

        if (health.IsDead)
            return tookDamage;

        if (TryInterruptJumpTraversalFromProjectileHit(hitDirection, stunDuration))
            return true;

        bool appliedKnockback = false;
        if (knockback != null)
            appliedKnockback = knockback.ApplyKnockbackFromHit(hitDirection, horizontalKnockback, verticalKnockback, stunDuration);
        else if (stun != null && stunDuration > 0f)
            stun.ApplyStun(stunDuration);

        return tookDamage || appliedKnockback;
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
    }

    private void ClampSettings()
    {
        defaultProjectileDamage = Mathf.Max(0f, defaultProjectileDamage);
        defaultProjectileStunDuration = Mathf.Max(0f, defaultProjectileStunDuration);
        defaultProjectileKnockbackHorizontal = Mathf.Max(0f, defaultProjectileKnockbackHorizontal);
        defaultProjectileKnockbackVertical = Mathf.Max(0f, defaultProjectileKnockbackVertical);
        defaultHeavyMeleeDamage = Mathf.Max(0f, defaultHeavyMeleeDamage);
        defaultHeavyLaunchHorizontal = Mathf.Max(0f, defaultHeavyLaunchHorizontal);
        defaultHeavyLaunchVertical = Mathf.Max(0f, defaultHeavyLaunchVertical);
        defaultHeavyRecoveryDuration = Mathf.Max(0f, defaultHeavyRecoveryDuration);
    }
}
