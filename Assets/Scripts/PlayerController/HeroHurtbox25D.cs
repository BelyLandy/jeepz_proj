using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroHurtbox25D : MonoBehaviour
{
    [SerializeField] private HeroKnockbackReceiver25D knockbackReceiver;
    [SerializeField] private HeroHealth25D heroHealth;

    public HeroKnockbackReceiver25D KnockbackReceiver => knockbackReceiver;
    public HeroHealth25D HeroHealth => heroHealth;

    private void Reset()
    {
        if (knockbackReceiver == null)
            knockbackReceiver = GetComponentInParent<HeroKnockbackReceiver25D>();

        if (heroHealth == null)
            heroHealth = GetComponentInParent<HeroHealth25D>();
    }

    private void Awake()
    {
        if (knockbackReceiver == null)
            knockbackReceiver = GetComponentInParent<HeroKnockbackReceiver25D>();

        if (heroHealth == null)
            heroHealth = GetComponentInParent<HeroHealth25D>();
    }

    private void OnValidate()
    {
        if (knockbackReceiver == null)
            knockbackReceiver = GetComponentInParent<HeroKnockbackReceiver25D>();

        if (heroHealth == null)
            heroHealth = GetComponentInParent<HeroHealth25D>();
    }

    public bool ReceiveProjectileHit(EnemyProjectile25D projectile)
    {
        if (projectile == null)
            return false;

        bool handled = false;

        if (heroHealth != null)
        {
            if (projectile.Damage > 0 && heroHealth.CanTakeProjectileDamageNow())
            {
                if (heroHealth.ApplyDamage(projectile.Damage))
                {
                    heroHealth.MarkProjectileDamageTaken();
                    handled = true;
                }
            }

            if (knockbackReceiver != null && heroHealth.CanReceiveProjectileKnockbackNow())
            {
                bool knockbackApplied = knockbackReceiver.ApplyProjectileHit(projectile.Direction);
                if (knockbackApplied)
                {
                    heroHealth.MarkProjectileKnockbackApplied();
                    handled = true;
                }
            }

            return handled || knockbackReceiver != null;
        }

        if (knockbackReceiver == null)
            return false;

        return knockbackReceiver.ApplyProjectileHit(projectile.Direction);
    }
}
