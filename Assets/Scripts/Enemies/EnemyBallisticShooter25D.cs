using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyBallisticShooter25D : MonoBehaviour
{
    public enum EnemyAimSolveMode
    {
        DirectNoLead = 0,
        LimitedLead = 1,
        FullLead = 2,
    }

    [Header("References")]
    [SerializeField] private EnemyCharacter25D character;
    [SerializeField] private EnemyPerception25D perception;
    [SerializeField] private Transform muzzle;
    [SerializeField] private EnemyProjectile25D projectilePrefab;
    [SerializeField] private Transform projectileParent;

    [Header("Fire")]
    [SerializeField, Min(0.01f)] private float fireInterval = 0.8f;
    [SerializeField, Min(0f)] private float maxFireRange = 16f;
    [SerializeField] private bool requireLineOfSight = true;

    [Header("Primary Aim")]
    [SerializeField] private EnemyAimSolveMode primaryAimSolveMode = EnemyAimSolveMode.DirectNoLead;
    [SerializeField, Min(0f)] private float leadPredictionFactor = 1f;
    [SerializeField, Min(0f)] private float ballisticAimCompensationFactor = 1f;
    [SerializeField, Min(0f)] private float aimInaccuracyRadius = 0.1f;

    private float nextFireTime;
    private int lastFireEventVersion;

    public bool CanFireNow => Time.time >= nextFireTime && projectilePrefab != null && muzzle != null;
    public int LastFireEventVersion => lastFireEventVersion;
    public float PrimaryCooldownRemaining => Mathf.Max(0f, nextFireTime - Time.time);

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

    public bool TryFireAtPerceivedTarget()
    {
        return TryFirePrimaryAtPerceivedTarget();
    }

    public bool TryFirePrimaryAtPerceivedTarget()
    {
        if (perception == null || !perception.HasTarget)
            return false;

        if (requireLineOfSight && !perception.HasLineOfSight)
            return false;

        return TryFirePrimaryAt(perception.GetAimPosition(), perception.TargetVelocityEstimate);
    }

    public bool TryFireAt(Vector3 targetPosition, Vector3 targetVelocity)
    {
        return TryFirePrimaryAt(targetPosition, targetVelocity);
    }

    public bool TryFirePrimaryAt(Vector3 targetPosition, Vector3 targetVelocity)
    {
        if (!CanFireNow || projectilePrefab == null || muzzle == null)
            return false;

        Vector3 muzzlePosition = muzzle.position;
        muzzlePosition.z = 0f;
        targetPosition.z = 0f;

        float projectileSpeed = projectilePrefab.Speed;
        if (projectileSpeed <= 0.01f)
            return false;

        float distance = Vector3.Distance(muzzlePosition, targetPosition);
        if (distance > maxFireRange)
            return false;

        Vector3 aimPoint = BuildPrimaryAimPoint(muzzlePosition, targetPosition, targetVelocity, projectileSpeed);
        return SpawnProjectileTowards(muzzlePosition, aimPoint);
    }

    private Vector3 BuildPrimaryAimPoint(Vector3 muzzlePosition, Vector3 targetPosition, Vector3 targetVelocity, float projectileSpeed)
    {
        float distance = Vector3.Distance(muzzlePosition, targetPosition);
        float predictedTime = projectileSpeed > 0.01f ? distance / projectileSpeed : 0f;
        float effectiveLeadFactor = GetEffectiveLeadFactor();

        Vector3 predictedPosition = targetPosition + targetVelocity * (predictedTime * effectiveLeadFactor);

        if (projectilePrefab != null && projectilePrefab.UsesBallisticArc)
        {
            float gravity = Mathf.Max(0f, projectilePrefab.GravityAcceleration);
            predictedPosition += Vector3.up * (0.5f * gravity * predictedTime * predictedTime * ballisticAimCompensationFactor);
        }

        if (aimInaccuracyRadius > 0.0001f)
        {
            predictedPosition.x += Random.Range(-aimInaccuracyRadius, aimInaccuracyRadius);
            predictedPosition.y += Random.Range(-aimInaccuracyRadius, aimInaccuracyRadius);
        }

        predictedPosition.z = 0f;
        return predictedPosition;
    }

    private float GetEffectiveLeadFactor()
    {
        switch (primaryAimSolveMode)
        {
            case EnemyAimSolveMode.DirectNoLead:
                return 0f;
            case EnemyAimSolveMode.LimitedLead:
                return Mathf.Max(0f, leadPredictionFactor) * 0.5f;
            case EnemyAimSolveMode.FullLead:
            default:
                return Mathf.Max(0f, leadPredictionFactor);
        }
    }

    private bool SpawnProjectileTowards(Vector3 muzzlePosition, Vector3 aimPoint)
    {
        Vector3 launchDirection = aimPoint - muzzlePosition;
        launchDirection.z = 0f;
        if (launchDirection.sqrMagnitude <= 0.0001f)
        {
            int sign = character != null ? character.FacingSign : 1;
            launchDirection = sign >= 0 ? Vector3.right : Vector3.left;
        }

        EnemyProjectile25D projectile = Instantiate(projectilePrefab, muzzlePosition, Quaternion.identity, projectileParent);
        projectile.SetOwnerRoot(transform.root);
        projectile.Launch(launchDirection);

        nextFireTime = Time.time + fireInterval;
        lastFireEventVersion++;
        return true;
    }

    private void AutoAssign()
    {
        if (character == null)
            character = GetComponent<EnemyCharacter25D>();
        if (perception == null)
            perception = GetComponent<EnemyPerception25D>();
        if (muzzle == null)
            muzzle = transform;
    }

    private void ClampSettings()
    {
        fireInterval = Mathf.Max(0.01f, fireInterval);
        maxFireRange = Mathf.Max(0f, maxFireRange);
        leadPredictionFactor = Mathf.Max(0f, leadPredictionFactor);
        ballisticAimCompensationFactor = Mathf.Max(0f, ballisticAimCompensationFactor);
        aimInaccuracyRadius = Mathf.Max(0f, aimInaccuracyRadius);
    }
}
