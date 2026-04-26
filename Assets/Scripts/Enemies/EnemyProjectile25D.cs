using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class EnemyProjectile25D : MonoBehaviour
{
    public enum TrajectoryMode
    {
        Straight = 0,
        Ballistic = 1,
    }

    [Header("Motion")]
    [SerializeField] private TrajectoryMode trajectoryMode = TrajectoryMode.Straight;
    [SerializeField] private float speed = 12f;
    [SerializeField] private float gravityAcceleration = 20f;
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private bool alignRotationToDirection = true;

    [Header("World Collision")]
    [SerializeField] private LayerMask worldHitMask = 0;
    [SerializeField, Min(0f)] private float collisionRadius = 0.04f;
    [SerializeField] private bool destroyOnWorldHit = true;

    [Header("World Z Lock")]
    [SerializeField] private bool lockWorldZ = true;
    [SerializeField] private float worldZ = 0f;

    [Header("Damage")]
    [SerializeField] private int damage = 10;

    [Header("Hit Behaviour")]
    [SerializeField] private bool destroyOnHeroHit = true;

    [Header("Explosion")]
    [SerializeField] private bool useExplosion = false;
    [SerializeField] private Explosion25D explosionPrefab;
    [SerializeField] private bool explodeOnImpact = true;
    [SerializeField] private bool explodeOnLifetimeEnd = true;
    [SerializeField, Min(0f)] private float explodeAfterImpactDelay = 0f;

    private Vector3 currentVelocity = Vector3.right;
    private Vector3 previousPosition;
    private Transform ownerRoot;
    private float age;
    private bool isInitialized;
    private bool hasImpacted;
    private bool hasDetonated;
    private bool impactDetonationScheduled;
    private float impactDetonationTime;

    public Vector3 Direction => currentVelocity.sqrMagnitude > 0.0001f ? currentVelocity.normalized : Vector3.right;
    public float Speed => speed;
    public int Damage => damage;
    public float GravityAcceleration => gravityAcceleration;
    public bool UsesBallisticArc => trajectoryMode == TrajectoryMode.Ballistic;

    private void OnEnable()
    {
        age = 0f;
        hasImpacted = false;
        hasDetonated = false;
        impactDetonationScheduled = false;
        impactDetonationTime = 0f;
        previousPosition = transform.position;
    }

    private void Start()
    {
        if (!isInitialized)
            Launch(transform.right);
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        gravityAcceleration = Mathf.Max(0f, gravityAcceleration);
        lifeTime = Mathf.Max(0f, lifeTime);
        damage = Mathf.Max(0, damage);
        collisionRadius = Mathf.Max(0f, collisionRadius);
        explodeAfterImpactDelay = Mathf.Max(0f, explodeAfterImpactDelay);
    }

    private void Update()
    {
        if (!isInitialized || hasDetonated)
            return;

        if (impactDetonationScheduled)
        {
            if (Time.time >= impactDetonationTime)
                Detonate();
            return;
        }

        if (hasImpacted)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        age += dt;
        if (age >= lifeTime)
        {
            if (useExplosion && explodeOnLifetimeEnd)
                Detonate();
            else
                DestroyProjectile();
            return;
        }

        previousPosition = transform.position;
        UpdateVelocity(dt);

        Vector3 nextPosition = previousPosition + currentVelocity * dt;
        if (lockWorldZ)
            nextPosition.z = worldZ;

        if (TryTraceWorldHit(previousPosition, nextPosition, out RaycastHit hit))
        {
            HandleWorldImpact(hit, previousPosition, nextPosition);
            return;
        }

        transform.position = nextPosition;
        UpdateVisualAlignment();
    }

    public void Launch(Vector3 launchDirection, float speedOverride = -1f)
    {
        if (launchDirection.sqrMagnitude <= 0.0001f)
            launchDirection = Vector3.right;

        launchDirection.z = 0f;
        if (launchDirection.sqrMagnitude > 0.0001f)
            launchDirection.Normalize();
        else
            launchDirection = Vector3.right;

        if (speedOverride > 0f)
            speed = speedOverride;

        currentVelocity = launchDirection * speed;
        currentVelocity.z = 0f;
        isInitialized = true;
        hasImpacted = false;
        hasDetonated = false;
        impactDetonationScheduled = false;
        impactDetonationTime = 0f;
        age = 0f;
        previousPosition = transform.position;

        if (lockWorldZ)
        {
            Vector3 p = transform.position;
            p.z = worldZ;
            transform.position = p;
            previousPosition = p;
        }

        UpdateVisualAlignment();
    }

    public void SetOwnerRoot(Transform root)
    {
        ownerRoot = root;
    }

    private void UpdateVelocity(float dt)
    {
        if (trajectoryMode != TrajectoryMode.Ballistic)
            return;

        currentVelocity += Vector3.down * gravityAcceleration * dt;
        currentVelocity.z = 0f;
    }

    private bool TryTraceWorldHit(Vector3 from, Vector3 to, out RaycastHit hit)
    {
        hit = default;

        if (worldHitMask.value == 0)
            return false;

        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return false;

        Vector3 direction = delta / distance;
        bool didHit = collisionRadius > 0.0001f
            ? Physics.SphereCast(from, collisionRadius, direction, out hit, distance, worldHitMask, QueryTriggerInteraction.Ignore)
            : Physics.Raycast(from, direction, out hit, distance, worldHitMask, QueryTriggerInteraction.Ignore);

        if (!didHit)
            return false;

        if (ownerRoot != null && hit.collider != null)
        {
            Transform hitTransform = hit.collider.transform;
            if (hitTransform == ownerRoot || hitTransform.IsChildOf(ownerRoot))
                return false;
        }

        return true;
    }

    private void HandleWorldImpact(RaycastHit hit, Vector3 from, Vector3 to)
    {
        hasImpacted = true;

        Vector3 impactPosition = hit.point;
        if (collisionRadius > 0.0001f)
        {
            Vector3 direction = to - from;
            if (direction.sqrMagnitude > 0.000001f)
                impactPosition -= direction.normalized * collisionRadius;
        }

        if (lockWorldZ)
            impactPosition.z = worldZ;

        transform.position = impactPosition;

        if (useExplosion && explodeOnImpact)
        {
            if (explodeAfterImpactDelay > 0f)
            {
                impactDetonationScheduled = true;
                impactDetonationTime = Time.time + explodeAfterImpactDelay;
            }
            else
            {
                Detonate();
            }
            return;
        }

        if (destroyOnWorldHit)
            DestroyProjectile();
    }

    private void UpdateVisualAlignment()
    {
        if (!alignRotationToDirection)
            return;

        Vector3 alignmentVector = currentVelocity;
        alignmentVector.z = 0f;
        if (alignmentVector.sqrMagnitude <= 0.0001f)
            return;

        float angleZ = Mathf.Atan2(alignmentVector.y, alignmentVector.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angleZ);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        HandleHit(collision.collider);
    }

    private void HandleHit(Collider other)
    {
        if (hasImpacted || hasDetonated || other == null)
            return;

        if (ownerRoot != null)
        {
            Transform hitTransform = other.transform;
            if (hitTransform == ownerRoot || hitTransform.IsChildOf(ownerRoot))
                return;
        }

        HeroHurtbox25D hurtbox = other.GetComponent<HeroHurtbox25D>();
        if (hurtbox == null)
            hurtbox = other.GetComponentInParent<HeroHurtbox25D>();

        if (hurtbox == null)
            return;

        hasImpacted = true;

        if (useExplosion && explodeOnImpact)
        {
            Detonate();
            return;
        }

        hurtbox.ReceiveProjectileHit(this);

        if (destroyOnHeroHit)
            DestroyProjectile();
    }

    private void Detonate()
    {
        if (hasDetonated)
            return;

        hasDetonated = true;
        impactDetonationScheduled = false;

        if (useExplosion && explosionPrefab != null)
        {
            Explosion25D explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            explosion.Explode(transform.position, ownerRoot != null ? ownerRoot.gameObject : null);
        }

        DestroyProjectile();
    }

    private void DestroyProjectile()
    {
        Destroy(gameObject);
    }
}
