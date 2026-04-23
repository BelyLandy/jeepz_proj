using UnityEngine;

[DisallowMultipleComponent]
public sealed class MenuSphereProjectile : MonoBehaviour
{
    public enum TrajectoryMode
    {
        Straight = 0,
        Ballistic = 1,
    }

    public enum CrosshairTrackingStyle
    {
        Hard = 0,
        Soft = 1,
    }

    public enum ForwardCastShape
    {
        Ray = 0,
        Sphere = 1,
    }

    [Header("Motion")]
    [SerializeField] private TrajectoryMode trajectoryMode = TrajectoryMode.Straight;
    [SerializeField, Min(0f)] private float speed = 22f;
    [SerializeField, Min(0f)] private float gravityAcceleration = 20f;
    [SerializeField, Min(0f)] private float lifeTime = 4f;
    [SerializeField] private bool alignToDirection = true;

    [Header("Optional World Z Lock")]
    [SerializeField] private bool lockWorldZ = false;
    [SerializeField] private float worldZ = 0f;

    [Header("Z Constraints")]
    [SerializeField] private bool clampZToLaunchCrosshair = false;
    [SerializeField] private bool despawnOnZPlaneBeforeCrosshair = false;
    [SerializeField, Min(0f)] private float despawnOffsetBeforeCrosshair = 0.5f;

    [Header("Despawn Effect")]
    [SerializeField] private GameObject despawnEffectPrefab;
    [SerializeField] private bool orientDespawnEffectToVelocity = false;
    [SerializeField, Min(0f)] private float despawnDelayAfterZPlane = 0f;

    [Header("Forward Ray On Effect Despawn")]
    [SerializeField] private bool castForwardRayOnEffectDespawn = true;
    [SerializeField] private ForwardCastShape forwardCastShape = ForwardCastShape.Ray;
    [SerializeField, Min(0f)] private float forwardRayDistance = 10f;
    [SerializeField, Min(0f)] private float forwardCastRadius = 0.25f;
    [SerializeField] private LayerMask forwardRayMask = ~0;

    [Header("Forward Cast Gizmos")]
    [SerializeField] private bool drawForwardCastGizmos = true;
    [SerializeField] private Color forwardCastGizmoColor = new Color(0.2f, 1f, 1f, 0.95f);


    private Vector3 currentVelocity = Vector3.forward;
    private Vector3 previousPosition;
    private float age;
    private bool isLaunched;

    private bool trackingEnabled;
    //private bool hasPassedCrosshair;
    private MenuPointerController pointerController;
    private bool zBoundsInitialized;
    private float launchCrosshairZ;
    private float despawnPlaneZ;
    private int zTravelSign;
    private CrosshairTrackingStyle trackingStyle = CrosshairTrackingStyle.Hard;
    private float desiredCrosshairTravelSpeed = 20f;
    private bool zPlaneDespawnPending;
    private float zPlaneDespawnTimer;
    private float minTimeToCrosshair = 0.22f;
    private float maxTimeToCrosshair = 0.70f;
    private float crosshairPassRadius = 0.5f;
    private float softTrackingResponsiveness = 10f;

    public TrajectoryMode CurrentTrajectoryMode => trajectoryMode;
    public float GravityAcceleration => gravityAcceleration;
    public float Speed => speed;
    public bool LockWorldZ => lockWorldZ;
    public float WorldZ => worldZ;

    private void OnEnable()
    {
        age = 0f;
        previousPosition = transform.position;
        //hasPassedCrosshair = false;
        zBoundsInitialized = false;
        zTravelSign = 0;
        zPlaneDespawnPending = false;
        zPlaneDespawnTimer = 0f;
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        gravityAcceleration = Mathf.Max(0f, gravityAcceleration);
        lifeTime = Mathf.Max(0f, lifeTime);
        desiredCrosshairTravelSpeed = Mathf.Max(0.0001f, desiredCrosshairTravelSpeed);
        minTimeToCrosshair = Mathf.Max(0.01f, minTimeToCrosshair);
        maxTimeToCrosshair = Mathf.Max(minTimeToCrosshair, maxTimeToCrosshair);
        crosshairPassRadius = Mathf.Max(0.0001f, crosshairPassRadius);
        softTrackingResponsiveness = Mathf.Max(0f, softTrackingResponsiveness);
        despawnOffsetBeforeCrosshair = Mathf.Max(0f, despawnOffsetBeforeCrosshair);
        despawnDelayAfterZPlane = Mathf.Max(0f, despawnDelayAfterZPlane);
        forwardRayDistance = Mathf.Max(0f, forwardRayDistance);
        forwardCastRadius = Mathf.Max(0f, forwardCastRadius);
    }

    private void Update()
    {
        if (!isLaunched)
            return;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        age += dt;
        if (age >= lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        if (zPlaneDespawnPending)
        {
            zPlaneDespawnTimer -= dt;
            if (zPlaneDespawnTimer <= 0f)
            {
                DoEffectDespawn(transform.position);
                return;
            }
        }

        previousPosition = transform.position;

        if (trackingEnabled && trajectoryMode == TrajectoryMode.Ballistic)
        {
            if (TrySolveVelocityToCurrentCrosshair(out Vector3 solvedVelocity))
            {
                ApplyTrackingVelocity(solvedVelocity, dt);
            }
            else
            {
                trackingEnabled = false;
            }
        }

        StepMotion(dt);


        if (TryHandleZPlaneDespawn(previousPosition, transform.position))
            return;

        ApplyLaunchCrosshairZClamp();

        UpdateVisualAlignment();
    }

    public void Launch(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.000001f)
            direction = transform.forward.sqrMagnitude > 0.000001f ? transform.forward : Vector3.forward;

        if (lockWorldZ)
        {
            direction.z = 0f;
            if (direction.sqrMagnitude <= 0.000001f)
                direction = Vector3.right;
        }

        direction.Normalize();
        LaunchVelocity(direction * speed);
    }

    public void LaunchVelocity(Vector3 initialVelocity)
    {
        if (lockWorldZ)
            initialVelocity.z = 0f;

        if (initialVelocity.sqrMagnitude <= 0.000001f)
            initialVelocity = lockWorldZ ? Vector3.right * speed : Vector3.forward * speed;

        currentVelocity = initialVelocity;
        age = 0f;
        isLaunched = true;
        trackingEnabled = false;
        //hasPassedCrosshair = false;
        zPlaneDespawnPending = false;
        zPlaneDespawnTimer = 0f;

        if (lockWorldZ)
        {
            Vector3 p = transform.position;
            p.z = worldZ;
            transform.position = p;
        }

        previousPosition = transform.position;
        UpdateVisualAlignment();
    }

    public void ConfigureLaunchCrosshairTarget(Vector3 crosshairWorldPosition)
    {
        if (lockWorldZ)
        {
            zBoundsInitialized = false;
            zTravelSign = 0;
            return;
        }

        float spawnZ = transform.position.z;
        launchCrosshairZ = crosshairWorldPosition.z;

        float deltaZ = launchCrosshairZ - spawnZ;
        if (Mathf.Abs(deltaZ) > 0.0001f)
        {
            zTravelSign = deltaZ > 0f ? 1 : -1;
        }
        else if (Mathf.Abs(currentVelocity.z) > 0.0001f)
        {
            zTravelSign = currentVelocity.z > 0f ? 1 : -1;
        }
        else
        {
            zTravelSign = 0;
        }

        if (zTravelSign == 0)
        {
            zBoundsInitialized = false;
            return;
        }

        despawnPlaneZ = launchCrosshairZ - (zTravelSign * despawnOffsetBeforeCrosshair);
        zBoundsInitialized = true;
    }

    public void ConfigureTracking(
        MenuPointerController pointer,
        CrosshairTrackingStyle style,
        float desiredSpeed,
        float minTime,
        float maxTime,
        float passRadius,
        float responsiveness)
    {
        pointerController = pointer;
        trackingStyle = style;
        desiredCrosshairTravelSpeed = Mathf.Max(0.0001f, desiredSpeed);
        minTimeToCrosshair = Mathf.Max(0.01f, minTime);
        maxTimeToCrosshair = Mathf.Max(minTimeToCrosshair, maxTime);
        crosshairPassRadius = Mathf.Max(0.0001f, passRadius);
        softTrackingResponsiveness = Mathf.Max(0f, responsiveness);

        trackingEnabled = pointerController != null && trajectoryMode == TrajectoryMode.Ballistic;
        //hasPassedCrosshair = false;
    }

    private void StepMotion(float dt)
    {
        Vector3 acceleration = GetAcceleration();
        Vector3 nextPosition = transform.position + currentVelocity * dt + 0.5f * acceleration * (dt * dt);
        currentVelocity += acceleration * dt;

        if (lockWorldZ)
        {
            nextPosition.z = worldZ;
            currentVelocity.z = 0f;
        }

        transform.position = nextPosition;
    }

    private Vector3 GetAcceleration()
    {
        if (trajectoryMode != TrajectoryMode.Ballistic)
            return Vector3.zero;

        Vector3 acceleration = Vector3.down * gravityAcceleration;
        if (lockWorldZ)
            acceleration.z = 0f;

        return acceleration;
    }

    private bool TrySolveVelocityToCurrentCrosshair(out Vector3 solvedVelocity)
    {
        solvedVelocity = Vector3.zero;
        if (pointerController == null)
            return false;

        Vector3 origin = transform.position;
        Vector3 target = pointerController.CrosshairWorldPosition;

        if (lockWorldZ)
        {
            origin.z = worldZ;
            target.z = worldZ;
        }

        Vector3 delta = target - origin;
        float distance = delta.magnitude;

        float timeToCrosshair = distance / desiredCrosshairTravelSpeed;
        timeToCrosshair = Mathf.Clamp(timeToCrosshair, minTimeToCrosshair, maxTimeToCrosshair);
        if (timeToCrosshair <= 0.0001f)
            return false;

        Vector3 acceleration = GetAcceleration();
        solvedVelocity = (delta - 0.5f * acceleration * timeToCrosshair * timeToCrosshair) / timeToCrosshair;

        if (lockWorldZ)
            solvedVelocity.z = 0f;

        return IsFiniteVector(solvedVelocity) && solvedVelocity.sqrMagnitude > 0.000001f;
    }

    private void ApplyTrackingVelocity(Vector3 solvedVelocity, float dt)
    {
        if (trackingStyle == CrosshairTrackingStyle.Hard)
        {
            currentVelocity = solvedVelocity;
            return;
        }

        float alpha = 1f - Mathf.Exp(-softTrackingResponsiveness * Mathf.Max(0f, dt));
        currentVelocity = Vector3.Lerp(currentVelocity, solvedVelocity, alpha);
    }

    private bool HasPassedCurrentCrosshair(Vector3 from, Vector3 to)
    {
        if (pointerController == null)
            return true;

        Vector3 crosshairPosition = pointerController.CrosshairWorldPosition;
        if (lockWorldZ)
            crosshairPosition.z = worldZ;

        float radius = Mathf.Max(0.0001f, crosshairPassRadius);
        float radiusSqr = radius * radius;

        if ((to - crosshairPosition).sqrMagnitude <= radiusSqr)
            return true;

        return SegmentIntersectsSphere(from, to, crosshairPosition, radiusSqr);
    }


    private bool TryHandleZPlaneDespawn(Vector3 from, Vector3 to)
    {
        if (!despawnOnZPlaneBeforeCrosshair || !zBoundsInitialized || zTravelSign == 0 || zPlaneDespawnPending)
            return false;

        if (!TryGetZPlaneIntersection(from, to, despawnPlaneZ, out float t))
            return false;

        t = Mathf.Clamp01(t);
        Vector3 hitPoint = Vector3.Lerp(from, to, t);

        if (!ShouldUseDespawnDelayAtZPlaneCrossing())
        {
            transform.position = hitPoint;
            DoEffectDespawn(hitPoint);
            return true;
        }

        zPlaneDespawnPending = true;
        zPlaneDespawnTimer = despawnDelayAfterZPlane;
        return false;
    }

    private bool ShouldUseDespawnDelayAtZPlaneCrossing()
    {
        if (despawnDelayAfterZPlane <= 0f)
            return false;

        if (pointerController == null)
            return false;

        return pointerController.HasNavigateInputThisFrame || pointerController.DidCursorMoveThisFrame;
    }

    private void ApplyLaunchCrosshairZClamp()
    {
        if (!clampZToLaunchCrosshair || !zBoundsInitialized || zTravelSign == 0)
            return;

        Vector3 position = transform.position;
        bool clamped = false;

        if (zTravelSign > 0)
        {
            if (position.z > launchCrosshairZ)
            {
                position.z = launchCrosshairZ;
                clamped = true;
            }
        }
        else
        {
            if (position.z < launchCrosshairZ)
            {
                position.z = launchCrosshairZ;
                clamped = true;
            }
        }

        if (!clamped)
            return;

        transform.position = position;
        currentVelocity.z = 0f;
    }

    private void DoEffectDespawn(Vector3 position)
    {
        CastForwardOnEffectDespawnAll(position);
        SpawnDespawnEffect(position);
        Destroy(gameObject);
    }

    private void CastForwardOnEffectDespawnAll(Vector3 origin)
    {
        if (!castForwardRayOnEffectDespawn || forwardRayDistance <= 0f)
            return;

        RaycastHit[] hits = GetForwardCastHits(origin);
        if (hits == null || hits.Length == 0)
            return;

        System.Array.Sort(hits, CompareHitsByDistance);

        var processedTargets = new System.Collections.Generic.HashSet<MenuRayHitCounterTarget>();
        var processedLogos = new System.Collections.Generic.HashSet<MenuLogoHitReactionVisual>();

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Collider collider = hit.collider;
            if (collider == null)
                continue;

            MenuRayHitCounterTarget target = collider.GetComponent<MenuRayHitCounterTarget>()
                ?? collider.GetComponentInParent<MenuRayHitCounterTarget>();
            if (target != null && processedTargets.Add(target))
            {
                target.RegisterRayHit();
            }

            MenuLogoHitReactionVisual logo = collider.GetComponent<MenuLogoHitReactionVisual>()
                ?? collider.GetComponentInParent<MenuLogoHitReactionVisual>();
            if (logo != null && processedLogos.Add(logo))
            {
                logo.ApplyHitReaction(hit.point, Vector3.forward, collider);
            }
        }
    }

    private RaycastHit[] GetForwardCastHits(Vector3 origin)
    {
        if (forwardCastShape == ForwardCastShape.Sphere && forwardCastRadius > 0f)
            return Physics.SphereCastAll(origin, forwardCastRadius, Vector3.forward, forwardRayDistance, forwardRayMask, QueryTriggerInteraction.Collide);

        return Physics.RaycastAll(origin, Vector3.forward, forwardRayDistance, forwardRayMask, QueryTriggerInteraction.Collide);
    }

    private static int CompareHitsByDistance(RaycastHit a, RaycastHit b)
    {
        return a.distance.CompareTo(b.distance);
    }

    private void SpawnDespawnEffect(Vector3 position)
    {
        if (despawnEffectPrefab == null)
            return;

        Quaternion rotation = Quaternion.identity;
        Vector3 effectDirection = currentVelocity;
        if (orientDespawnEffectToVelocity && effectDirection.sqrMagnitude > 0.000001f)
            rotation = Quaternion.LookRotation(effectDirection.normalized, Vector3.up);

        Instantiate(despawnEffectPrefab, position, rotation);
    }

    private static bool TryGetZPlaneIntersection(Vector3 from, Vector3 to, float planeZ, out float t)
    {
        t = 0f;
        float deltaZ = to.z - from.z;
        if (Mathf.Abs(deltaZ) <= 0.000001f)
        {
            if (Mathf.Abs(from.z - planeZ) <= 0.000001f)
            {
                t = 0f;
                return true;
            }

            return false;
        }

        float rawT = (planeZ - from.z) / deltaZ;
        if (rawT < 0f || rawT > 1f)
            return false;

        t = rawT;
        return true;
    }


    private static bool SegmentIntersectsSphere(Vector3 a, Vector3 b, Vector3 center, float radiusSqr)
    {
        Vector3 ab = b - a;
        float abSqr = ab.sqrMagnitude;
        if (abSqr <= 0.000001f)
            return (a - center).sqrMagnitude <= radiusSqr;

        float t = Vector3.Dot(center - a, ab) / abSqr;
        t = Mathf.Clamp01(t);
        Vector3 closestPoint = a + ab * t;
        return (closestPoint - center).sqrMagnitude <= radiusSqr;
    }

    private static bool IsFiniteVector(Vector3 value)
    {
        return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    private void UpdateVisualAlignment()
    {
        if (!alignToDirection)
            return;

        Vector3 alignmentVector = currentVelocity;
        if (lockWorldZ)
            alignmentVector.z = 0f;

        if (alignmentVector.sqrMagnitude <= 0.000001f)
            return;

        transform.rotation = Quaternion.LookRotation(alignmentVector.normalized, Vector3.up);
    }
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawForwardCastGizmos || !castForwardRayOnEffectDespawn || forwardRayDistance <= 0f)
            return;

        Vector3 origin = transform.position;
        Vector3 direction = Vector3.forward;
        Vector3 end = origin + direction * forwardRayDistance;

        Gizmos.color = forwardCastGizmoColor;

        if (forwardCastShape == ForwardCastShape.Sphere && forwardCastRadius > 0f)
        {
            DrawSphereCastGizmo(origin, end, forwardCastRadius);
        }
        else
        {
            Gizmos.DrawLine(origin, end);
            float markerRadius = 0.05f;
            Gizmos.DrawWireSphere(origin, markerRadius);
            Gizmos.DrawWireSphere(end, markerRadius);
        }
    }

    private void DrawSphereCastGizmo(Vector3 origin, Vector3 end, float radius)
    {
        Gizmos.DrawWireSphere(origin, radius);
        Gizmos.DrawWireSphere(end, radius);

        Vector3 right = Vector3.right * radius;
        Vector3 up = Vector3.up * radius;

        Gizmos.DrawLine(origin + right, end + right);
        Gizmos.DrawLine(origin - right, end - right);
        Gizmos.DrawLine(origin + up, end + up);
        Gizmos.DrawLine(origin - up, end - up);
        Gizmos.DrawLine(origin, end);
    }
#endif

}
