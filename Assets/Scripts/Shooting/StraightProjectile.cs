using UnityEngine;

public sealed class StraightProjectile : MonoBehaviour
{
    private enum TrajectoryMode
    {
        Straight = 0,
        Ballistic = 1,
    }

    [Header("Motion")]
    [SerializeField] private TrajectoryMode trajectoryMode = TrajectoryMode.Straight;
    [SerializeField] private float speed = 25f;
    [SerializeField] private float gravityAcceleration = 25f;
    [SerializeField] private float lifeTime = 10f;
    [SerializeField] private bool alignRotationToDirection = true;

    [Header("World Collision")]
    [SerializeField] private LayerMask worldHitMask = 0;
    [SerializeField, Min(0f)] private float collisionRadius = 0.04f;
    [SerializeField] private bool destroyOnWorldHit = true;

    [Header("Enemy Collision")]
    [SerializeField] private LayerMask enemyHurtboxMask = 0;
    [SerializeField] private bool destroyOnEnemyHit = true;

    [Header("World Z Lock")]
    [SerializeField] private bool lockWorldZ = true;
    [SerializeField] private float worldZ = 0f;

    private Vector3 currentVelocity = Vector3.right;
    private Vector3 previousPosition;
    private Transform ownerRoot;
    private float age;
    private bool isInitialized;
    private bool hasImpacted;

    private void OnEnable()
    {
        age = 0f;
        hasImpacted = false;
        previousPosition = transform.position;
    }

    private void Start()
    {
        if (!isInitialized)
            Launch(transform.right);
    }

    private void Update()
    {
        if (!isInitialized || hasImpacted)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        age += dt;
        if (age >= lifeTime)
        {
            DestroyProjectile();
            return;
        }

        previousPosition = transform.position;

        UpdateVelocity(dt);

        Vector3 nextPosition = previousPosition + currentVelocity * dt;
        if (lockWorldZ)
            nextPosition.z = worldZ;

        bool hitEnemy = TryTraceEnemyHit(previousPosition, nextPosition, out RaycastHit enemyHit, out EnemyHurtbox25D enemyHurtbox);
        bool hitWorld = TryTraceWorldHit(previousPosition, nextPosition, out RaycastHit worldHit);

        if (hitEnemy && hitWorld)
        {
            if (enemyHit.distance <= worldHit.distance)
                HandleEnemyImpact(enemyHurtbox, enemyHit, previousPosition, nextPosition);
            else
                HandleWorldImpact(worldHit, previousPosition, nextPosition);

            return;
        }

        if (hitEnemy)
        {
            HandleEnemyImpact(enemyHurtbox, enemyHit, previousPosition, nextPosition);
            return;
        }

        if (hitWorld)
        {
            HandleWorldImpact(worldHit, previousPosition, nextPosition);
            return;
        }

        transform.position = nextPosition;
        UpdateVisualAlignment();
    }

    public void Launch(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.right;

        direction.z = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();
        else
            direction = Vector3.right;

        currentVelocity = direction * speed;
        currentVelocity.z = 0f;

        isInitialized = true;
        hasImpacted = false;
        age = 0f;
        previousPosition = transform.position;

        UpdateVisualAlignment();

        if (lockWorldZ)
        {
            Vector3 position = transform.position;
            position.z = worldZ;
            transform.position = position;
            previousPosition = position;
        }
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
        bool didHit;

        if (collisionRadius > 0.0001f)
        {
            didHit = Physics.SphereCast(
                from,
                collisionRadius,
                direction,
                out hit,
                distance,
                worldHitMask,
                QueryTriggerInteraction.Ignore);
        }
        else
        {
            didHit = Physics.Raycast(
                from,
                direction,
                out hit,
                distance,
                worldHitMask,
                QueryTriggerInteraction.Ignore);
        }

        if (!didHit)
            return false;

        if (hit.collider != null && IsOwnerTransform(hit.collider.transform))
            return false;

        return true;
    }

    private bool TryTraceEnemyHit(Vector3 from, Vector3 to, out RaycastHit hit, out EnemyHurtbox25D hurtbox)
    {
        hit = default;
        hurtbox = null;

        if (enemyHurtboxMask.value == 0)
            return false;

        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return false;

        Vector3 direction = delta / distance;
        bool found = false;
        float closestDistance = float.MaxValue;

        if (collisionRadius > 0.0001f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                from,
                collisionRadius,
                direction,
                distance,
                enemyHurtboxMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hits.Length; i++)
                TrySelectEnemyHit(hits[i], ref found, ref closestDistance, ref hit, ref hurtbox);
        }
        else
        {
            RaycastHit[] hits = Physics.RaycastAll(
                from,
                direction,
                distance,
                enemyHurtboxMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hits.Length; i++)
                TrySelectEnemyHit(hits[i], ref found, ref closestDistance, ref hit, ref hurtbox);
        }

        return found;
    }

    private void TrySelectEnemyHit(RaycastHit candidateHit, ref bool found, ref float closestDistance, ref RaycastHit bestHit, ref EnemyHurtbox25D bestHurtbox)
    {
        Collider candidateCollider = candidateHit.collider;
        if (candidateCollider == null)
            return;

        Transform hitTransform = candidateCollider.transform;
        if (IsOwnerTransform(hitTransform))
            return;

        EnemyHurtbox25D candidateHurtbox = candidateCollider.GetComponent<EnemyHurtbox25D>();
        if (candidateHurtbox == null)
            candidateHurtbox = candidateCollider.GetComponentInParent<EnemyHurtbox25D>();

        if (candidateHurtbox == null)
            return;

        if (candidateHit.distance > closestDistance)
            return;

        found = true;
        closestDistance = candidateHit.distance;
        bestHit = candidateHit;
        bestHurtbox = candidateHurtbox;
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

        if (destroyOnWorldHit)
            DestroyProjectile();
    }

    private void HandleEnemyImpact(EnemyHurtbox25D hurtbox, RaycastHit hit, Vector3 from, Vector3 to)
    {
        hasImpacted = true;

        Vector3 impactPosition = hit.point;
        Vector3 travelDirection = to - from;
        if (collisionRadius > 0.0001f && travelDirection.sqrMagnitude > 0.000001f)
            impactPosition -= travelDirection.normalized * collisionRadius;

        if (lockWorldZ)
            impactPosition.z = worldZ;

        transform.position = impactPosition;

        Vector3 hitDirection = currentVelocity;
        if (hitDirection.sqrMagnitude <= 0.0001f)
            hitDirection = travelDirection;
        if (hitDirection.sqrMagnitude <= 0.0001f)
            hitDirection = transform.right;

        hitDirection.z = 0f;
        if (hitDirection.sqrMagnitude > 0.0001f)
            hitDirection.Normalize();
        else
            hitDirection = Vector3.right;

        if (hurtbox != null)
            hurtbox.ReceiveProjectileHit(hitDirection);

        if (destroyOnEnemyHit)
            DestroyProjectile();
    }

    private bool IsOwnerTransform(Transform target)
    {
        if (ownerRoot == null || target == null)
            return false;

        return target == ownerRoot || target.IsChildOf(ownerRoot);
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

    private void DestroyProjectile()
    {
        Destroy(gameObject);
    }
}
