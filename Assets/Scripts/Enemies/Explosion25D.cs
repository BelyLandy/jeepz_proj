using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Explosion25D : MonoBehaviour
{
    [Header("Radius / Damage")]
    [SerializeField, Min(0f)] private float explosionRadius = 3f;
    [SerializeField, Min(0f)] private float baseExplosionDamage = 30f;

    [Header("Falloff")]
    [SerializeField, Min(0f)] private float minDamageMultiplier = 0.4f;
    [SerializeField, Min(0f)] private float maxDamageMultiplier = 1.0f;
    [SerializeField, Min(0f)] private float minKnockbackMultiplier = 0.6f;
    [SerializeField, Min(0f)] private float maxKnockbackMultiplier = 1.25f;

    [Header("Knockback")]
    [SerializeField] private bool applyKnockback = true;
    [SerializeField, Min(0f)] private float baseKnockbackHorizontalForce = 10f;
    [SerializeField, Min(0f)] private float baseKnockbackVerticalForce = 6f;

    [Header("Filters")]
    [SerializeField] private LayerMask overlapMask = ~0;
    [SerializeField] private bool affectHero = true;
    [SerializeField] private bool affectEnemies = false;
    [SerializeField] private bool affectOwner = false;
    [SerializeField] private bool useFriendlyFire = false;

    [Header("Line Of Effect")]
    [SerializeField] private bool requireLineOfEffect = false;
    [SerializeField] private LayerMask obstacleMask = 0;

    [Header("VFX / SFX")]
    [SerializeField] private GameObject explosionVfxPrefab;
    [SerializeField] private AudioClip explosionSfx;
    [SerializeField, Min(0f)] private float vfxLifetime = 3f;
    [SerializeField] private bool destroySelfAfterExplode = true;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawExplosionRadiusGizmos = true;
    [SerializeField] private Color explosionRadiusGizmoColor = new Color(1f, 0.4f, 0.1f, 0.5f);

    private readonly HashSet<GameObject> processedTargets = new HashSet<GameObject>();
    private bool hasExploded;

    private void OnValidate()
    {
        explosionRadius = Mathf.Max(0f, explosionRadius);
        baseExplosionDamage = Mathf.Max(0f, baseExplosionDamage);
        minDamageMultiplier = Mathf.Max(0f, minDamageMultiplier);
        maxDamageMultiplier = Mathf.Max(minDamageMultiplier, maxDamageMultiplier);
        minKnockbackMultiplier = Mathf.Max(0f, minKnockbackMultiplier);
        maxKnockbackMultiplier = Mathf.Max(minKnockbackMultiplier, maxKnockbackMultiplier);
        baseKnockbackHorizontalForce = Mathf.Max(0f, baseKnockbackHorizontalForce);
        baseKnockbackVerticalForce = Mathf.Max(0f, baseKnockbackVerticalForce);
        vfxLifetime = Mathf.Max(0f, vfxLifetime);
    }

    public void Explode(Vector3 center, GameObject owner = null)
    {
        if (hasExploded)
            return;

        hasExploded = true;
        processedTargets.Clear();

        Collider[] hits = Physics.OverlapSphere(center, explosionRadius, overlapMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
                continue;

            GameObject targetRoot = ResolveTargetRoot(hit);
            if (targetRoot == null)
                continue;

            if (processedTargets.Contains(targetRoot))
                continue;

            if (!IsValidExplosionTarget(targetRoot, owner))
                continue;

            Vector3 targetPoint = GetTargetImpactPoint(hit, targetRoot, center);
            if (!HasLineOfEffect(center, targetPoint))
                continue;

            processedTargets.Add(targetRoot);

            float distance = Vector3.Distance(center, targetPoint);
            float proximity = 1f - Mathf.Clamp01(explosionRadius > 0.0001f ? distance / explosionRadius : 1f);
            float damageMultiplier = Mathf.Lerp(minDamageMultiplier, maxDamageMultiplier, proximity);
            float knockbackMultiplier = Mathf.Lerp(minKnockbackMultiplier, maxKnockbackMultiplier, proximity);

            float finalDamage = baseExplosionDamage * damageMultiplier;
            float finalHorizontalForce = baseKnockbackHorizontalForce * knockbackMultiplier;
            float finalVerticalForce = baseKnockbackVerticalForce * knockbackMultiplier;

            TryApplyExplosionDamage(targetRoot, Mathf.RoundToInt(finalDamage), owner);

            if (applyKnockback)
                TryApplyExplosionKnockback(targetRoot, center, finalHorizontalForce, finalVerticalForce, owner);
        }

        SpawnExplosionVfx(center);
        PlayExplosionSfx(center);

        if (destroySelfAfterExplode)
            Destroy(gameObject);
    }

    private GameObject ResolveTargetRoot(Collider hit)
    {
        if (hit == null)
            return null;

        HeroHurtbox25D heroHurtbox = hit.GetComponent<HeroHurtbox25D>();
        if (heroHurtbox == null)
            heroHurtbox = hit.GetComponentInParent<HeroHurtbox25D>();
        if (heroHurtbox != null)
            return heroHurtbox.transform.root.gameObject;

        EnemyHurtbox25D enemyHurtbox = hit.GetComponent<EnemyHurtbox25D>();
        if (enemyHurtbox == null)
            enemyHurtbox = hit.GetComponentInParent<EnemyHurtbox25D>();
        if (enemyHurtbox != null)
            return enemyHurtbox.transform.root.gameObject;

        EnemyHealth25D enemyHealth = hit.GetComponent<EnemyHealth25D>();
        if (enemyHealth == null)
            enemyHealth = hit.GetComponentInParent<EnemyHealth25D>();
        if (enemyHealth != null)
            return enemyHealth.transform.root.gameObject;

        HeroHealth25D heroHealth = hit.GetComponent<HeroHealth25D>();
        if (heroHealth == null)
            heroHealth = hit.GetComponentInParent<HeroHealth25D>();
        if (heroHealth != null)
            return heroHealth.transform.root.gameObject;

        Rigidbody attached = hit.attachedRigidbody;
        if (attached != null)
            return attached.transform.root.gameObject;

        return hit.transform.root.gameObject;
    }

    private bool IsValidExplosionTarget(GameObject targetRoot, GameObject owner)
    {
        if (targetRoot == null)
            return false;

        if (!affectOwner && owner != null)
        {
            Transform ownerRoot = owner.transform.root;
            if (targetRoot.transform == ownerRoot || targetRoot.transform.IsChildOf(ownerRoot))
                return false;
        }

        bool isHeroTarget = GetHeroHurtbox(targetRoot) != null || targetRoot.GetComponentInChildren<HeroHealth25D>(true) != null;
        bool isEnemyTarget = GetEnemyHurtbox(targetRoot) != null || targetRoot.GetComponentInChildren<EnemyHealth25D>(true) != null;

        if (isHeroTarget && !affectHero)
            return false;
        if (isEnemyTarget && !affectEnemies)
            return false;
        if (!isHeroTarget && !isEnemyTarget)
            return false;

        if (!useFriendlyFire && owner != null)
        {
            bool ownerIsHero = owner.GetComponentInChildren<HeroHealth25D>(true) != null || owner.GetComponentInChildren<HeroHurtbox25D>(true) != null;
            bool ownerIsEnemy = owner.GetComponentInChildren<EnemyHealth25D>(true) != null || owner.GetComponentInChildren<EnemyHurtbox25D>(true) != null;

            if (ownerIsHero && isHeroTarget)
                return false;
            if (ownerIsEnemy && isEnemyTarget)
                return false;
        }

        return true;
    }

    private bool HasLineOfEffect(Vector3 center, Vector3 targetPoint)
    {
        if (!requireLineOfEffect || obstacleMask.value == 0)
            return true;

        Vector3 delta = targetPoint - center;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return true;

        Vector3 direction = delta / distance;
        return !Physics.Raycast(center, direction, distance, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    private Vector3 GetTargetImpactPoint(Collider hit, GameObject targetRoot, Vector3 center)
    {
        if (hit != null)
        {
            Vector3 closest = hit.ClosestPoint(center);
            if ((closest - center).sqrMagnitude > 0.000001f)
            {
                closest.z = 0f;
                return closest;
            }
        }

        Vector3 point = targetRoot != null ? targetRoot.transform.position : center;
        point.z = 0f;
        return point;
    }

    private void TryApplyExplosionDamage(GameObject targetRoot, int finalDamage, GameObject owner)
    {
        if (targetRoot == null || finalDamage <= 0)
            return;

        HeroHurtbox25D heroHurtbox = GetHeroHurtbox(targetRoot);
        if (heroHurtbox != null)
        {
            heroHurtbox.ReceiveExplosionHit(transform.position, finalDamage, 0f, 0f);
            return;
        }

        EnemyHurtbox25D enemyHurtbox = GetEnemyHurtbox(targetRoot);
        if (enemyHurtbox != null)
        {
            enemyHurtbox.ReceiveExplosionHit(transform.position, finalDamage, 0f, 0f, 0f);
            return;
        }

        HeroHealth25D heroHealth = targetRoot.GetComponentInChildren<HeroHealth25D>(true);
        if (heroHealth != null)
        {
            heroHealth.ApplyDamage(finalDamage);
            return;
        }

        EnemyHealth25D enemyHealth = targetRoot.GetComponentInChildren<EnemyHealth25D>(true);
        if (enemyHealth != null)
            enemyHealth.ApplyDamage(finalDamage);
    }

    private void TryApplyExplosionKnockback(GameObject targetRoot, Vector3 explosionCenter, float finalHorizontalForce, float finalVerticalForce, GameObject owner)
    {
        if (targetRoot == null || (finalHorizontalForce <= 0f && finalVerticalForce <= 0f))
            return;

        HeroHurtbox25D heroHurtbox = GetHeroHurtbox(targetRoot);
        if (heroHurtbox != null)
        {
            heroHurtbox.ReceiveExplosionHit(explosionCenter, 0, finalHorizontalForce, finalVerticalForce);
            return;
        }

        EnemyHurtbox25D enemyHurtbox = GetEnemyHurtbox(targetRoot);
        if (enemyHurtbox != null)
        {
            enemyHurtbox.ReceiveExplosionHit(explosionCenter, 0f, finalHorizontalForce, finalVerticalForce, 0f);
            return;
        }

        EnemyKnockbackReceiver25D enemyKnockback = targetRoot.GetComponentInChildren<EnemyKnockbackReceiver25D>(true);
        if (enemyKnockback != null)
        {
            enemyKnockback.ApplyExplosionKnockback(explosionCenter, finalHorizontalForce, finalVerticalForce, 0f);
            return;
        }

        HeroKnockbackReceiver25D heroKnockback = targetRoot.GetComponentInChildren<HeroKnockbackReceiver25D>(true);
        if (heroKnockback != null)
        {
            heroKnockback.ApplyExplosionKnockback(explosionCenter, finalHorizontalForce, finalVerticalForce);
            return;
        }

        Rigidbody rb = targetRoot.GetComponentInChildren<Rigidbody>(true);
        if (rb == null || rb.isKinematic)
            return;

        float dx = targetRoot.transform.position.x - explosionCenter.x;
        float sign = Mathf.Abs(dx) > 0.0001f ? Mathf.Sign(dx) : 1f;
        rb.linearVelocity = new Vector3(sign * finalHorizontalForce, finalVerticalForce, 0f);
    }

    private HeroHurtbox25D GetHeroHurtbox(GameObject targetRoot)
    {
        return targetRoot != null ? targetRoot.GetComponentInChildren<HeroHurtbox25D>(true) : null;
    }

    private EnemyHurtbox25D GetEnemyHurtbox(GameObject targetRoot)
    {
        return targetRoot != null ? targetRoot.GetComponentInChildren<EnemyHurtbox25D>(true) : null;
    }

    private void SpawnExplosionVfx(Vector3 center)
    {
        if (explosionVfxPrefab == null)
            return;

        GameObject vfx = Instantiate(explosionVfxPrefab, center, Quaternion.identity);
        if (vfxLifetime > 0f)
            Destroy(vfx, vfxLifetime);
    }

    private void PlayExplosionSfx(Vector3 center)
    {
        if (explosionSfx == null)
            return;

        AudioSource.PlayClipAtPoint(explosionSfx, center);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawExplosionRadiusGizmos)
            return;

        Gizmos.color = explosionRadiusGizmoColor;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
