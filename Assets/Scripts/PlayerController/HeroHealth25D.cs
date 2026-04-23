using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroHealth25D : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private bool startAtMaxHealth = true;
    [SerializeField] private int currentHealth = 100;

    [Header("Projectile Protection")]
    [SerializeField] private float projectileDamageInvulnerabilityDuration = 0.35f;
    [SerializeField] private float projectileKnockbackCooldownDuration = 0.10f;

    private const float InvalidPastTime = -999f;

    private float nextProjectileDamageAllowedTime = InvalidPastTime;
    private float nextProjectileKnockbackAllowedTime = InvalidPastTime;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float HealthNormalized => maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
    public bool IsAlive => currentHealth > 0;
    public bool IsDamageInvulnerable => Time.time < nextProjectileDamageAllowedTime;
    public float ProjectileDamageInvulnerabilityDuration => projectileDamageInvulnerabilityDuration;
    public float ProjectileKnockbackCooldownDuration => projectileKnockbackCooldownDuration;

    private void Reset()
    {
        ClampSettings();
        if (startAtMaxHealth)
            currentHealth = maxHealth;
        else
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    private void Awake()
    {
        ClampSettings();

        if (startAtMaxHealth)
            currentHealth = maxHealth;
        else
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    private void OnValidate()
    {
        ClampSettings();

        if (!Application.isPlaying)
        {
            if (startAtMaxHealth)
                currentHealth = maxHealth;
            else
                currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }
    }

    public bool CanTakeProjectileDamageNow()
    {
        return Time.time >= nextProjectileDamageAllowedTime;
    }

    public bool CanReceiveProjectileKnockbackNow()
    {
        return Time.time >= nextProjectileKnockbackAllowedTime;
    }

    public bool ApplyDamage(int amount)
    {
        if (amount <= 0 || maxHealth <= 0 || currentHealth <= 0)
            return false;

        currentHealth = Mathf.Clamp(currentHealth - amount, 0, maxHealth);
        return true;
    }

    public int Heal(int amount)
    {
        if (amount <= 0 || maxHealth <= 0)
            return 0;

        int before = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        return currentHealth - before;
    }

    public void RefillToFull()
    {
        currentHealth = maxHealth;
    }

    public void MarkProjectileDamageTaken()
    {
        nextProjectileDamageAllowedTime = Time.time + projectileDamageInvulnerabilityDuration;
    }

    public void MarkProjectileKnockbackApplied()
    {
        nextProjectileKnockbackAllowedTime = Time.time + projectileKnockbackCooldownDuration;
    }

    private void ClampSettings()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        projectileDamageInvulnerabilityDuration = Mathf.Max(0f, projectileDamageInvulnerabilityDuration);
        projectileKnockbackCooldownDuration = Mathf.Max(0f, projectileKnockbackCooldownDuration);
    }
}
