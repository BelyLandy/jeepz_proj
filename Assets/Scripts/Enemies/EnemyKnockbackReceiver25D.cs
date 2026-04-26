using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyKnockbackReceiver25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyCharacter25D character;
    [SerializeField] private EnemyStun25D stun;
    [SerializeField] private EnemyHealth25D health;
    [SerializeField] private Rigidbody enemyRb;

    [Header("Default Knockback")]
    [SerializeField, Min(0f)] private float defaultKnockbackHorizontal = 5f;
    [SerializeField, Min(0f)] private float defaultKnockbackVertical = 1.5f;
    [SerializeField, Min(0f)] private float stunOnKnockbackDuration = 0.2f;
    [SerializeField] private bool overrideExistingVelocity = true;

    [Header("Heavy Launch")]
    [SerializeField, Min(0f)] private float defaultLaunchHorizontal = 12f;
    [SerializeField, Min(0f)] private float defaultLaunchVertical = 6f;
    [SerializeField, Min(0f)] private float launchRecoveryDuration = 0.65f;
    [SerializeField, Min(0f)] private float groundedRecoveryDelay = 0.08f;

    [Header("Wall Bounce")]
    [SerializeField, Min(0f)] private float wallBounceDamping = 0.55f;
    [SerializeField, Min(0f)] private float wallBounceMinSpeed = 2.5f;
    [SerializeField, Min(0f)] private float wallImpactDamageThreshold = 5f;
    [SerializeField, Min(0f)] private float wallImpactDamageMultiplier = 0.5f;
    [SerializeField, Range(0f, 1f)] private float wallNormalMaxY = 0.45f;
    [SerializeField, Min(0)] private int maxWallBounces = 2;

    [Header("Floor Bounce")]
    [SerializeField, Min(0f)] private float floorBounceDamping = 0.25f;
    [SerializeField, Range(0f, 1f)] private float floorHorizontalRetain = 0.55f;
    [SerializeField, Min(0f)] private float floorBounceMinSpeed = 3.5f;
    [SerializeField, Min(0f)] private float floorImpactDamageThreshold = 6f;
    [SerializeField, Min(0f)] private float floorImpactDamageMultiplier = 0.35f;
    [SerializeField, Range(0f, 1f)] private float floorNormalMinY = 0.55f;
    [SerializeField, Min(0)] private int maxFloorBounces = 2;

    [Header("Recovery")]
    [SerializeField, Min(0f)] private float launchedSettleHorizontalSpeed = 0.35f;
    [SerializeField, Min(0f)] private float launchedSettleVerticalSpeed = 0.35f;

    private enum ReactionState
    {
        None,
        Launched,
        Recovering
    }

    private ReactionState reactionState;
    private int wallBounceCount;
    private int floorBounceCount;
    private float recoveryEndTime;
    private float earliestRecoveryTime;
    private float currentRecoveryDuration;
    private int lastImpactEventVersion;
    private int lastRecoveryEventVersion;

    public EnemyCharacter25D Character => character;
    public Rigidbody EnemyRigidbody => enemyRb;
    public bool IsLaunched => reactionState == ReactionState.Launched;
    public bool IsRecovering => reactionState == ReactionState.Recovering;
    public int LastImpactEventVersion => lastImpactEventVersion;
    public int LastRecoveryEventVersion => lastRecoveryEventVersion;

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

    private void Update()
    {
        if (reactionState == ReactionState.Recovering && Time.time >= recoveryEndTime)
        {
            reactionState = ReactionState.None;
            if (character != null && !character.IsDead)
                character.SetReactionControlLocked(false);
        }
    }

    private void FixedUpdate()
    {
        if (reactionState != ReactionState.Launched || enemyRb == null || character == null || character.IsDead)
            return;

        if (Time.time < earliestRecoveryTime)
            return;

        if (!character.IsGrounded)
            return;

        Vector3 velocity = enemyRb.linearVelocity;
        if (Mathf.Abs(velocity.x) > launchedSettleHorizontalSpeed)
            return;
        if (Mathf.Abs(velocity.y) > launchedSettleVerticalSpeed)
            return;

        BeginRecovery();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (reactionState != ReactionState.Launched || enemyRb == null || health == null || health.IsDead)
            return;

        ContactPoint? best = GetBestContact(collision);
        if (!best.HasValue)
            return;

        Vector3 normal = best.Value.normal.normalized;
        Vector3 velocityBefore = enemyRb.linearVelocity;

        if (Mathf.Abs(normal.y) <= wallNormalMaxY)
        {
            HandleWallImpact(normal, velocityBefore);
            return;
        }

        if (normal.y >= floorNormalMinY)
        {
            HandleFloorImpact(normal, velocityBefore);
        }
    }


    public bool ApplyExplosionKnockback(Vector3 explosionCenter, float horizontalForce, float verticalForce, float stunDuration = 0f)
    {
        float dx = transform.position.x - explosionCenter.x;
        float signX = Mathf.Abs(dx) > 0.0001f ? Mathf.Sign(dx) : (character != null ? character.FacingSign : 1f);
        Vector3 velocity = new Vector3(signX * Mathf.Max(0f, horizontalForce), Mathf.Max(0f, verticalForce), 0f);
        return ApplyGenericKnockback(velocity, stunDuration);
    }

    public bool ApplyProjectileKnockback(Vector3 hitDirection)
    {
        return ApplyKnockbackFromHit(hitDirection, defaultKnockbackHorizontal, defaultKnockbackVertical, stunOnKnockbackDuration);
    }

    public bool ApplyGenericKnockback(Vector3 velocity, float stunDuration)
    {
        if (enemyRb == null || enemyRb.isKinematic || (health != null && health.IsDead))
            return false;

        velocity.z = 0f;

        if (overrideExistingVelocity)
            enemyRb.linearVelocity = velocity;
        else
            enemyRb.linearVelocity += velocity;

        if (character != null && Mathf.Abs(velocity.x) > 0.01f)
            character.ForceFacingSign(velocity.x >= 0f ? 1 : -1);

        if (stun != null && stunDuration > 0f)
            stun.ApplyStun(stunDuration);

        return true;
    }

    public bool ApplyKnockbackFromHit(Vector3 hitDirection, float horizontalForce, float verticalForce, float stunDuration)
    {
        if (enemyRb == null || enemyRb.isKinematic || (health != null && health.IsDead))
            return false;

        Vector2 planar = new Vector2(hitDirection.x, hitDirection.y);
        if (planar.sqrMagnitude <= 0.0001f)
            planar = Vector2.right;
        else
            planar.Normalize();

        float signX = Mathf.Abs(planar.x) > 0.0001f ? Mathf.Sign(planar.x) : 1f;
        Vector3 velocity = new Vector3(signX * Mathf.Max(0f, horizontalForce), Mathf.Max(0f, verticalForce), 0f);
        return ApplyGenericKnockback(velocity, stunDuration);
    }

    public bool ApplyHeavyLaunch(Vector3 hitDirection)
    {
        return ApplyLaunchFromHit(hitDirection, defaultLaunchHorizontal, defaultLaunchVertical, launchRecoveryDuration);
    }

    public bool ApplyLaunchFromHit(Vector3 hitDirection, float horizontalForce, float verticalForce, float recoveryDuration)
    {
        if (enemyRb == null || enemyRb.isKinematic || (health != null && health.IsDead))
            return false;

        Vector2 planar = new Vector2(hitDirection.x, hitDirection.y);
        if (planar.sqrMagnitude <= 0.0001f)
            planar = Vector2.right;
        else
            planar.Normalize();

        float signX = Mathf.Abs(planar.x) > 0.0001f ? Mathf.Sign(planar.x) : 1f;
        Vector3 launchVelocity = new Vector3(signX * Mathf.Max(0f, horizontalForce), Mathf.Max(0f, verticalForce), 0f);

        wallBounceCount = 0;
        floorBounceCount = 0;
        earliestRecoveryTime = Time.time + groundedRecoveryDelay;
        recoveryEndTime = 0f;
        reactionState = ReactionState.Launched;

        if (character != null)
        {
            character.SetReactionControlLocked(true);
            character.ForceFacingSign(launchVelocity.x >= 0f ? 1 : -1);
        }

        if (overrideExistingVelocity)
            enemyRb.linearVelocity = launchVelocity;
        else
            enemyRb.linearVelocity += launchVelocity;

        currentRecoveryDuration = Mathf.Max(0f, recoveryDuration);
        return true;
    }

    private void HandleWallImpact(Vector3 normal, Vector3 velocityBefore)
    {
        float horizontalSpeed = Mathf.Abs(velocityBefore.x);
        if (horizontalSpeed <= wallBounceMinSpeed || wallBounceCount >= maxWallBounces)
            return;

        wallBounceCount++;

        ApplyImpactDamage(horizontalSpeed, wallImpactDamageThreshold, wallImpactDamageMultiplier);

        Vector3 reflected = Vector3.Reflect(new Vector3(velocityBefore.x, 0f, 0f), normal);
        Vector3 newVelocity = enemyRb.linearVelocity;
        newVelocity.x = reflected.x * wallBounceDamping;
        newVelocity.z = 0f;
        enemyRb.linearVelocity = newVelocity;

        lastImpactEventVersion++;
        earliestRecoveryTime = Time.time + groundedRecoveryDelay;
    }

    private void HandleFloorImpact(Vector3 normal, Vector3 velocityBefore)
    {
        float downwardSpeed = Mathf.Max(0f, -velocityBefore.y);
        if (downwardSpeed <= floorBounceMinSpeed || floorBounceCount >= maxFloorBounces)
        {
            ApplyImpactDamage(downwardSpeed, floorImpactDamageThreshold, floorImpactDamageMultiplier);
            return;
        }

        floorBounceCount++;
        ApplyImpactDamage(downwardSpeed, floorImpactDamageThreshold, floorImpactDamageMultiplier);

        Vector3 newVelocity = enemyRb.linearVelocity;
        newVelocity.y = downwardSpeed * floorBounceDamping;
        newVelocity.x *= floorHorizontalRetain;
        newVelocity.z = 0f;
        enemyRb.linearVelocity = newVelocity;

        lastImpactEventVersion++;
        earliestRecoveryTime = Time.time + groundedRecoveryDelay;
    }

    private void ApplyImpactDamage(float impactSpeed, float threshold, float multiplier)
    {
        if (health == null || health.IsDead)
            return;

        float excess = impactSpeed - threshold;
        if (excess <= 0f || multiplier <= 0f)
            return;

        health.ApplyDamage(excess * multiplier);
    }

    private void BeginRecovery()
    {
        reactionState = ReactionState.Recovering;
        recoveryEndTime = Time.time + Mathf.Max(0f, currentRecoveryDuration > 0f ? currentRecoveryDuration : launchRecoveryDuration);
        if (enemyRb != null)
        {
            Vector3 velocity = enemyRb.linearVelocity;
            velocity.x = 0f;
            velocity.y = 0f;
            velocity.z = 0f;
            enemyRb.linearVelocity = velocity;
        }

        lastRecoveryEventVersion++;
    }

    private ContactPoint? GetBestContact(Collision collision)
    {
        if (collision == null || collision.contactCount <= 0)
            return null;

        ContactPoint best = collision.GetContact(0);
        for (int i = 1; i < collision.contactCount; i++)
        {
            ContactPoint c = collision.GetContact(i);
            if (c.normal.y > best.normal.y)
                best = c;
        }

        return best;
    }

    private void AutoAssign()
    {
        if (character == null)
            character = GetComponent<EnemyCharacter25D>();
        if (stun == null)
            stun = GetComponent<EnemyStun25D>();
        if (health == null)
            health = GetComponent<EnemyHealth25D>();
        if (enemyRb == null && character != null)
            enemyRb = character.Rigidbody;
        else if (enemyRb == null)
            enemyRb = GetComponent<Rigidbody>();
    }

    private void ClampSettings()
    {
        defaultKnockbackHorizontal = Mathf.Max(0f, defaultKnockbackHorizontal);
        defaultKnockbackVertical = Mathf.Max(0f, defaultKnockbackVertical);
        stunOnKnockbackDuration = Mathf.Max(0f, stunOnKnockbackDuration);
        defaultLaunchHorizontal = Mathf.Max(0f, defaultLaunchHorizontal);
        defaultLaunchVertical = Mathf.Max(0f, defaultLaunchVertical);
        launchRecoveryDuration = Mathf.Max(0f, launchRecoveryDuration);
        groundedRecoveryDelay = Mathf.Max(0f, groundedRecoveryDelay);
        wallBounceDamping = Mathf.Max(0f, wallBounceDamping);
        wallBounceMinSpeed = Mathf.Max(0f, wallBounceMinSpeed);
        wallImpactDamageThreshold = Mathf.Max(0f, wallImpactDamageThreshold);
        wallImpactDamageMultiplier = Mathf.Max(0f, wallImpactDamageMultiplier);
        wallNormalMaxY = Mathf.Clamp01(wallNormalMaxY);
        maxWallBounces = Mathf.Max(0, maxWallBounces);
        floorBounceDamping = Mathf.Max(0f, floorBounceDamping);
        floorHorizontalRetain = Mathf.Clamp01(floorHorizontalRetain);
        floorBounceMinSpeed = Mathf.Max(0f, floorBounceMinSpeed);
        floorImpactDamageThreshold = Mathf.Max(0f, floorImpactDamageThreshold);
        floorImpactDamageMultiplier = Mathf.Max(0f, floorImpactDamageMultiplier);
        floorNormalMinY = Mathf.Clamp01(floorNormalMinY);
        maxFloorBounces = Mathf.Max(0, maxFloorBounces);
        launchedSettleHorizontalSpeed = Mathf.Max(0f, launchedSettleHorizontalSpeed);
        launchedSettleVerticalSpeed = Mathf.Max(0f, launchedSettleVerticalSpeed);
    }
}
