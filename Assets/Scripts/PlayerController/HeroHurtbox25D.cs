using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroHurtbox25D : MonoBehaviour
{
    [SerializeField] private HeroKnockbackReceiver25D knockbackReceiver;

    public HeroKnockbackReceiver25D KnockbackReceiver => knockbackReceiver;

    private void Reset()
    {
        if (knockbackReceiver == null)
            knockbackReceiver = GetComponentInParent<HeroKnockbackReceiver25D>();
    }

    private void Awake()
    {
        if (knockbackReceiver == null)
            knockbackReceiver = GetComponentInParent<HeroKnockbackReceiver25D>();
    }

    private void OnValidate()
    {
        if (knockbackReceiver == null)
            knockbackReceiver = GetComponentInParent<HeroKnockbackReceiver25D>();
    }

    public bool ReceiveProjectileHit(EnemyProjectile25D projectile)
    {
        if (projectile == null || knockbackReceiver == null)
            return false;

        return knockbackReceiver.ApplyProjectileHit(projectile.Direction);
    }
}
