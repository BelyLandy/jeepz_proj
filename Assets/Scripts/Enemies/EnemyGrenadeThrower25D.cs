using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyGrenadeThrower25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyCharacter25D character;
    [SerializeField] private EnemyPerception25D perception;
    [SerializeField] private Transform throwOrigin;
    [SerializeField] private EnemyProjectile25D grenadeProjectilePrefab;
    [SerializeField] private Transform projectileParent;

    [Header("Cooldown / Timing")]
    [SerializeField, Min(0f)] private float grenadeCooldown = 5f;
    [SerializeField, Min(0f)] private float grenadeWindupDuration = 0.7f;
    [SerializeField, Min(0f)] private float grenadeDecisionInterval = 0.75f;
    [SerializeField, Range(0f, 1f)] private float grenadeDecisionChance = 0.25f;
    [SerializeField, Min(0f)] private float minCombatTimeBeforeGrenade = 1.5f;
    [SerializeField, Min(0f)] private float grenadeAttemptLockoutDuration = 1.25f;
    [SerializeField, Min(0f)] private float postThrowRecoveryDuration = 0.4f;

    [Header("Range Conditions")]
    [SerializeField, Min(0f)] private float grenadeMinRange = 4f;
    [SerializeField, Min(0f)] private float grenadeMaxRange = 14f;

    [Header("Aiming")]
    [SerializeField] private bool requireLineOfSightForGrenade = true;
    [SerializeField] private bool usePreciseBallisticSolve = true;
    [SerializeField, Min(0f)] private float grenadeLeadPredictionFactor = 1f;
    [SerializeField, Min(0f)] private float grenadeAimInaccuracyRadius = 0f;
    [SerializeField] private bool preferHighArc = true;

    [Header("Behaviour")]
    [SerializeField] private bool stopMovementDuringWindup = true;
    [SerializeField] private bool useFallbackThrow = true;

    [Header("Post Grenade Retreat")]
    [SerializeField] private bool usePostGrenadeRetreat = true;
    [SerializeField, Min(0f)] private float postGrenadeRetreatDuration = 1.0f;
    [SerializeField, Min(0f)] private float postGrenadeRetreatDistance = 3.5f;
    [SerializeField, Min(0f)] private float postGrenadeRetreatSpeedMultiplier = 1.1f;
    [SerializeField] private bool stopRetreatIfBlocked = true;

    [Header("Fallback Throw")]
    [SerializeField, Min(0f)] private float fallbackVerticalSpeed = 7f;
    [SerializeField] private bool fallbackUseDistanceScaling = true;
    [SerializeField, Min(0f)] private float fallbackMinHorizontalSpeed = 6f;
    [SerializeField, Min(0f)] private float fallbackMaxHorizontalSpeed = 11f;
    [SerializeField, Min(0.01f)] private float fallbackDistanceForMaxSpeed = 10f;

    private bool isPreparingThrow;
    private float prepareThrowEndTime;
    private float nextGrenadeDecisionTime;
    private float nextGrenadeReadyTime;
    private float nextGrenadeAttemptAllowedTime;
    private bool isInPostThrowRecovery;
    private float postThrowRecoveryEndTime;
    private bool isInPostGrenadeRetreat;
    private float postGrenadeRetreatStartTime;
    private float postGrenadeRetreatEndTime;
    private float postGrenadeRetreatStartX;
    private int postGrenadeRetreatDirectionSign;
    private int lastGrenadeThrowDirectionSign;
    private int lastThrowEventVersion;

    public bool CanThrowNow => Time.time >= nextGrenadeReadyTime && grenadeProjectilePrefab != null && throwOrigin != null;
    public bool IsPreparingThrow => isPreparingThrow;
    public bool IsInPostThrowRecovery => isInPostThrowRecovery;
    public bool IsInPostGrenadeRetreat => isInPostGrenadeRetreat;
    public bool StopMovementDuringWindup => stopMovementDuringWindup;
    public bool IsGrenadeActionBlockingPrimaryFire => isPreparingThrow || isInPostThrowRecovery || isInPostGrenadeRetreat;
    public bool IsInAnyGrenadeExclusiveState => isPreparingThrow || isInPostThrowRecovery || isInPostGrenadeRetreat;
    public float GrenadeCooldownRemaining => Mathf.Max(0f, nextGrenadeReadyTime - Time.time);
    public float GrenadeAttemptLockoutRemaining => Mathf.Max(0f, nextGrenadeAttemptAllowedTime - Time.time);
    public float PostThrowRecoveryRemaining => Mathf.Max(0f, postThrowRecoveryEndTime - Time.time);
    public float PostGrenadeRetreatRemaining => Mathf.Max(0f, postGrenadeRetreatEndTime - Time.time);
    public float PostGrenadeRetreatMoveScale => Mathf.Max(0f, postGrenadeRetreatSpeedMultiplier);
    public int PostGrenadeRetreatDirectionSign => postGrenadeRetreatDirectionSign;
    public int LastThrowEventVersion => lastThrowEventVersion;

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

    private void OnDisable()
    {
        CancelPrepare();
        CancelPostGrenadeRetreat();
        isInPostThrowRecovery = false;
        postThrowRecoveryEndTime = 0f;
    }

    public bool CanBeginGrenadeAttempt(float combatTimeElapsed, float distanceToTarget, bool targetVisible, bool hasLineOfSight)
    {
        if (grenadeProjectilePrefab == null || throwOrigin == null)
            return false;

        if (isPreparingThrow || isInPostThrowRecovery || isInPostGrenadeRetreat)
            return false;

        if (Time.time < nextGrenadeReadyTime)
            return false;

        if (Time.time < nextGrenadeAttemptAllowedTime)
            return false;

        if (Time.time < nextGrenadeDecisionTime)
            return false;

        if (combatTimeElapsed < minCombatTimeBeforeGrenade)
            return false;

        if (distanceToTarget < grenadeMinRange || distanceToTarget > grenadeMaxRange)
            return false;

        if (perception == null)
            return false;

        if (!perception.HasTarget && !perception.HasLastKnownPosition)
            return false;

        if (!targetVisible)
            return false;

        if (requireLineOfSightForGrenade && !hasLineOfSight)
            return false;

        return true;
    }

    public bool TryBeginPrepare(float combatTimeElapsed, float distanceToTarget, bool targetVisible, bool hasLineOfSight)
    {
        if (!CanBeginGrenadeAttempt(combatTimeElapsed, distanceToTarget, targetVisible, hasLineOfSight))
            return false;

        nextGrenadeDecisionTime = Time.time + grenadeDecisionInterval;
        if (grenadeDecisionChance < 0.9999f && Random.value > grenadeDecisionChance)
            return false;

        isPreparingThrow = true;
        prepareThrowEndTime = Time.time + grenadeWindupDuration;
        nextGrenadeAttemptAllowedTime = Time.time + grenadeAttemptLockoutDuration;

        if (stopMovementDuringWindup && character != null)
            character.StopMovement();

        return true;
    }

    public bool TickPrepareAndTryThrow()
    {
        if (!isPreparingThrow)
            return false;

        if (Time.time < prepareThrowEndTime)
            return false;

        bool didThrow = TryExecuteCommittedThrow();
        isPreparingThrow = false;
        prepareThrowEndTime = 0f;

        if (!didThrow)
            return false;

        BeginGrenadeCooldown();
        if (usePostGrenadeRetreat)
            BeginPostGrenadeRetreat();
        else
            BeginPostThrowRecovery();
        lastThrowEventVersion++;
        return true;
    }

    public void TickRecovery()
    {
        if (!isInPostThrowRecovery)
            return;

        if (Time.time >= postThrowRecoveryEndTime)
        {
            isInPostThrowRecovery = false;
            postThrowRecoveryEndTime = 0f;
        }
    }

    public void CancelPrepare()
    {
        isPreparingThrow = false;
        prepareThrowEndTime = 0f;
    }

    public void TickPostGrenadeRetreat()
    {
        if (!isInPostGrenadeRetreat)
            return;

        bool durationComplete = Time.time >= postGrenadeRetreatEndTime;
        bool distanceComplete = false;
        if (character != null)
            distanceComplete = Mathf.Abs(character.transform.position.x - postGrenadeRetreatStartX) >= postGrenadeRetreatDistance;
        else
            distanceComplete = Mathf.Abs(transform.position.x - postGrenadeRetreatStartX) >= postGrenadeRetreatDistance;

        bool blocked = false;
        if (!durationComplete && !distanceComplete && stopRetreatIfBlocked && character != null && Time.time >= postGrenadeRetreatStartTime + 0.12f)
            blocked = Mathf.Abs(character.MoveInputX) > 0.01f && character.HorizontalSpeedAbs <= 0.05f;

        if (durationComplete || distanceComplete || blocked)
            CancelPostGrenadeRetreat();
    }

    public void CancelPostGrenadeRetreat()
    {
        isInPostGrenadeRetreat = false;
        postGrenadeRetreatStartTime = 0f;
        postGrenadeRetreatEndTime = 0f;
        postGrenadeRetreatStartX = 0f;
        postGrenadeRetreatDirectionSign = 0;
    }

    private bool TryExecuteCommittedThrow()
    {
        if (throwOrigin == null || grenadeProjectilePrefab == null)
            return false;

        if (usePreciseBallisticSolve && TryExecutePreciseThrow())
            return true;

        if (useFallbackThrow && TryExecuteFallbackThrow())
            return true;

        return false;
    }

    private bool TryExecutePreciseThrow()
    {
        Vector3 origin = GetOriginPosition();
        if (!TryGetTargetingData(out Vector3 targetPosition, out Vector3 targetVelocity, out bool hasUsefulTarget))
            return false;

        if (!hasUsefulTarget)
            return false;

        if (!TryBuildPreciseLaunchVelocity(origin, targetPosition, targetVelocity, out Vector3 launchVelocity))
            return false;

        EnemyProjectile25D projectile = CreateProjectile(origin);
        if (projectile == null)
            return false;

        projectile.Launch(launchVelocity, launchVelocity.magnitude);
        CacheThrowDirectionSign(launchVelocity);
        return true;
    }

    private bool TryExecuteFallbackThrow()
    {
        Vector3 origin = GetOriginPosition();
        TryGetTargetingData(out Vector3 targetPosition, out _, out bool hasUsefulTarget);

        float sign = 0f;
        float absDistance = 0f;
        if (hasUsefulTarget)
        {
            float dx = targetPosition.x - origin.x;
            absDistance = Mathf.Abs(dx);
            if (absDistance > 0.0001f)
                sign = Mathf.Sign(dx);
        }

        if (Mathf.Abs(sign) <= 0.0001f)
            sign = character != null ? character.FacingSign : 1f;

        float horizontalSpeed = fallbackMaxHorizontalSpeed;
        if (fallbackUseDistanceScaling)
        {
            float t = Mathf.Clamp01(absDistance / Mathf.Max(0.01f, fallbackDistanceForMaxSpeed));
            horizontalSpeed = Mathf.Lerp(fallbackMinHorizontalSpeed, fallbackMaxHorizontalSpeed, t);
        }

        Vector3 launchVelocity = new Vector3(sign * horizontalSpeed, fallbackVerticalSpeed, 0f);
        if (launchVelocity.sqrMagnitude <= 0.0001f)
            return false;

        EnemyProjectile25D projectile = CreateProjectile(origin);
        if (projectile == null)
            return false;

        projectile.Launch(launchVelocity, launchVelocity.magnitude);
        CacheThrowDirectionSign(launchVelocity);
        return true;
    }

    private bool TryBuildPreciseLaunchVelocity(Vector3 origin, Vector3 targetPosition, Vector3 targetVelocity, out Vector3 launchVelocity)
    {
        launchVelocity = Vector3.zero;

        float projectileSpeed = Mathf.Max(0f, grenadeProjectilePrefab != null ? grenadeProjectilePrefab.Speed : 0f);
        if (projectileSpeed <= 0.01f)
            return false;

        Vector3 solvedTarget = targetPosition;
        solvedTarget.z = 0f;

        if (grenadeProjectilePrefab == null)
            return false;

        if (!grenadeProjectilePrefab.UsesBallisticArc)
        {
            float straightDistance = Vector3.Distance(origin, solvedTarget);
            float straightTime = projectileSpeed > 0.01f ? straightDistance / projectileSpeed : 0f;
            solvedTarget += targetVelocity * (straightTime * Mathf.Max(0f, grenadeLeadPredictionFactor));
            solvedTarget = ApplyAimInaccuracy(solvedTarget);

            Vector3 directVelocity = solvedTarget - origin;
            directVelocity.z = 0f;
            if (directVelocity.sqrMagnitude <= 0.0001f)
            {
                int sign = character != null ? character.FacingSign : 1;
                directVelocity = sign >= 0 ? Vector3.right : Vector3.left;
            }

            launchVelocity = directVelocity.normalized * projectileSpeed;
            return true;
        }

        float gravity = Mathf.Max(0.01f, grenadeProjectilePrefab.GravityAcceleration);
        Vector3 predictedTarget = targetPosition;
        predictedTarget.z = 0f;

        bool solved = false;
        float flightTime = 0f;
        for (int i = 0; i < 3; i++)
        {
            predictedTarget = targetPosition + targetVelocity * (flightTime * Mathf.Max(0f, grenadeLeadPredictionFactor));
            predictedTarget.z = 0f;
            solved = TrySolveBallisticVelocity(origin, predictedTarget, projectileSpeed, gravity, preferHighArc, out launchVelocity, out flightTime);
            if (!solved)
                break;
        }

        if (!solved)
            return false;

        predictedTarget = ApplyAimInaccuracy(predictedTarget);
        return TrySolveBallisticVelocity(origin, predictedTarget, projectileSpeed, gravity, preferHighArc, out launchVelocity, out _);
    }

    private bool TryGetTargetingData(out Vector3 targetPosition, out Vector3 targetVelocity, out bool hasUsefulTarget)
    {
        targetPosition = transform.position;
        targetVelocity = Vector3.zero;
        hasUsefulTarget = false;

        if (perception == null)
            return false;

        targetPosition = perception.GetAimPosition();
        targetPosition.z = 0f;
        targetVelocity = perception.TargetVelocityEstimate;
        targetVelocity.z = 0f;

        hasUsefulTarget = perception.HasTarget || perception.HasLastKnownPosition || perception.HasTrackedTarget;
        return true;
    }

    private Vector3 GetOriginPosition()
    {
        Vector3 origin = throwOrigin != null ? throwOrigin.position : transform.position;
        origin.z = 0f;
        return origin;
    }

    private Vector3 ApplyAimInaccuracy(Vector3 targetPosition)
    {
        if (grenadeAimInaccuracyRadius <= 0.0001f)
            return targetPosition;

        targetPosition.x += Random.Range(-grenadeAimInaccuracyRadius, grenadeAimInaccuracyRadius);
        targetPosition.y += Random.Range(-grenadeAimInaccuracyRadius, grenadeAimInaccuracyRadius);
        targetPosition.z = 0f;
        return targetPosition;
    }

    private EnemyProjectile25D CreateProjectile(Vector3 origin)
    {
        if (grenadeProjectilePrefab == null)
            return null;

        EnemyProjectile25D projectile = Instantiate(grenadeProjectilePrefab, origin, Quaternion.identity, projectileParent);
        projectile.SetOwnerRoot(transform.root);
        return projectile;
    }

    private void BeginPostThrowRecovery()
    {
        isInPostThrowRecovery = true;
        postThrowRecoveryEndTime = Time.time + postThrowRecoveryDuration;
    }

    private void BeginPostGrenadeRetreat()
    {
        isInPostThrowRecovery = false;
        postThrowRecoveryEndTime = 0f;
        isInPostGrenadeRetreat = true;
        postGrenadeRetreatStartTime = Time.time;
        postGrenadeRetreatEndTime = Time.time + postGrenadeRetreatDuration;
        postGrenadeRetreatStartX = character != null ? character.transform.position.x : transform.position.x;

        int fallbackSign = character != null ? character.FacingSign : 1;
        int throwDirection = lastGrenadeThrowDirectionSign != 0 ? lastGrenadeThrowDirectionSign : fallbackSign;
        postGrenadeRetreatDirectionSign = -throwDirection;
        if (postGrenadeRetreatDirectionSign == 0)
            postGrenadeRetreatDirectionSign = -fallbackSign;
        if (postGrenadeRetreatDirectionSign == 0)
            postGrenadeRetreatDirectionSign = -1;
    }

    private void BeginGrenadeCooldown()
    {
        nextGrenadeReadyTime = Time.time + grenadeCooldown;
    }

    private void CacheThrowDirectionSign(Vector3 launchVelocity)
    {
        if (Mathf.Abs(launchVelocity.x) > 0.001f)
            lastGrenadeThrowDirectionSign = launchVelocity.x >= 0f ? 1 : -1;
        else if (character != null)
            lastGrenadeThrowDirectionSign = character.FacingSign;
        else
            lastGrenadeThrowDirectionSign = 1;
    }

    private static bool TrySolveBallisticVelocity(Vector3 origin, Vector3 target, float speed, float gravity, bool highArc, out Vector3 launchVelocity, out float flightTime)
    {
        launchVelocity = Vector3.zero;
        flightTime = 0f;

        Vector3 displacement = target - origin;
        displacement.z = 0f;

        float horizontalDistance = Mathf.Abs(displacement.x);
        float verticalDistance = displacement.y;
        float directionSign = Mathf.Abs(displacement.x) > 0.0001f ? Mathf.Sign(displacement.x) : 1f;
        float speedSquared = speed * speed;
        float discriminant = (speedSquared * speedSquared) - gravity * ((gravity * horizontalDistance * horizontalDistance) + (2f * verticalDistance * speedSquared));
        if (discriminant < 0f)
            return false;

        if (horizontalDistance <= 0.0001f)
        {
            float verticalSpeedSquared = speedSquared - 0.0001f;
            if (verticalSpeedSquared <= 0f)
                return false;

            float verticalSpeed = Mathf.Sqrt(verticalSpeedSquared);
            launchVelocity = new Vector3(0f, verticalDistance >= 0f ? verticalSpeed : -verticalSpeed, 0f);
            flightTime = Mathf.Abs(verticalDistance) / Mathf.Max(0.01f, Mathf.Abs(launchVelocity.y));
            return true;
        }

        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float tanThetaA = (speedSquared + sqrtDiscriminant) / (gravity * horizontalDistance);
        float tanThetaB = (speedSquared - sqrtDiscriminant) / (gravity * horizontalDistance);
        float tanTheta = highArc ? Mathf.Max(tanThetaA, tanThetaB) : Mathf.Min(tanThetaA, tanThetaB);
        float angle = Mathf.Atan(tanTheta);

        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        if (Mathf.Abs(cos) <= 0.0001f)
            return false;

        launchVelocity = new Vector3(directionSign * cos * speed, sin * speed, 0f);
        flightTime = horizontalDistance / Mathf.Max(0.01f, Mathf.Abs(launchVelocity.x));
        return true;
    }

    private void AutoAssign()
    {
        if (character == null)
            character = GetComponent<EnemyCharacter25D>();
        if (perception == null)
            perception = GetComponent<EnemyPerception25D>();
        if (throwOrigin == null)
            throwOrigin = transform;
    }

    private void ClampSettings()
    {
        grenadeCooldown = Mathf.Max(0f, grenadeCooldown);
        grenadeWindupDuration = Mathf.Max(0f, grenadeWindupDuration);
        grenadeDecisionInterval = Mathf.Max(0f, grenadeDecisionInterval);
        grenadeDecisionChance = Mathf.Clamp01(grenadeDecisionChance);
        minCombatTimeBeforeGrenade = Mathf.Max(0f, minCombatTimeBeforeGrenade);
        grenadeAttemptLockoutDuration = Mathf.Max(0f, grenadeAttemptLockoutDuration);
        postThrowRecoveryDuration = Mathf.Max(0f, postThrowRecoveryDuration);
        postGrenadeRetreatDuration = Mathf.Max(0f, postGrenadeRetreatDuration);
        postGrenadeRetreatDistance = Mathf.Max(0f, postGrenadeRetreatDistance);
        postGrenadeRetreatSpeedMultiplier = Mathf.Max(0f, postGrenadeRetreatSpeedMultiplier);
        grenadeMinRange = Mathf.Max(0f, grenadeMinRange);
        grenadeMaxRange = Mathf.Max(grenadeMinRange, grenadeMaxRange);
        grenadeLeadPredictionFactor = Mathf.Max(0f, grenadeLeadPredictionFactor);
        grenadeAimInaccuracyRadius = Mathf.Max(0f, grenadeAimInaccuracyRadius);
        fallbackVerticalSpeed = Mathf.Max(0f, fallbackVerticalSpeed);
        fallbackMinHorizontalSpeed = Mathf.Max(0f, fallbackMinHorizontalSpeed);
        fallbackMaxHorizontalSpeed = Mathf.Max(fallbackMinHorizontalSpeed, fallbackMaxHorizontalSpeed);
        fallbackDistanceForMaxSpeed = Mathf.Max(0.01f, fallbackDistanceForMaxSpeed);
    }
}
