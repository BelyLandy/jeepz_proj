using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyHealth25D : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private bool startAtMaxHealth = true;
    [SerializeField, Min(0f)] private float currentHealth = 100f;

    private bool isDead;
    private int lastHitEventVersion;
    private int lastDeathEventVersion;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthNormalized => maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    public bool IsDead => isDead;
    public int LastHitEventVersion => lastHitEventVersion;
    public int LastDeathEventVersion => lastDeathEventVersion;

    private void Reset()
    {
        ClampSettings();
        InitializeHealth();
    }

    private void Awake()
    {
        ClampSettings();
        InitializeHealth();
    }

    private void OnValidate()
    {
        ClampSettings();

        if (!Application.isPlaying)
        {
            InitializeHealth();
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
            isDead = currentHealth <= 0f;
        }
    }

    public bool ApplyDamage(float amount)
    {
        if (amount <= 0f || isDead || maxHealth <= 0f)
            return false;

        float before = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);

        if (currentHealth < before)
            lastHitEventVersion++;

        if (currentHealth <= 0f)
            Kill();

        return currentHealth < before;
    }

    public float Heal(float amount)
    {
        if (amount <= 0f || maxHealth <= 0f || isDead)
            return 0f;

        float before = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        return currentHealth - before;
    }

    public void Kill()
    {
        if (isDead)
            return;

        isDead = true;
        currentHealth = 0f;
        lastDeathEventVersion++;
    }

    public void ResetHealthToMax()
    {
        ClampSettings();
        currentHealth = maxHealth;
        isDead = false;
    }

    private void InitializeHealth()
    {
        if (startAtMaxHealth)
            currentHealth = maxHealth;
        else
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        isDead = currentHealth <= 0f;
    }

    private void ClampSettings()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }
}
