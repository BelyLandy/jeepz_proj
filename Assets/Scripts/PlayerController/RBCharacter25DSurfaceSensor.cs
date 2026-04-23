using UnityEngine;

public enum SurfaceSensorQuery25D
{
    Support = 0,
    GroundSurface = 1,
    Wall = 2,
    WallInteraction = 3,
}

[System.Serializable]
public sealed class RBCharacter25DSurfaceSensor
{
    private const float DotEpsilon = 1e-5f;
    private const float HitDistanceTieEpsilon = 0.0025f;

    private Rigidbody rb;
    private CapsuleCollider col;
    private readonly RaycastHit[] castHits = new RaycastHit[16];

    private LayerMask groundMask;
    private LayerMask wallInteractionMask;
    private float groundProbeDistance;
    private float groundProbeStartOffset;
    private float groundProbeInset;
    private float wallCheckDistance;
    private float wallCheckHeightOffset;
    private float wallCheckRadius;
    private float wallMinNormalX;
    private float wallMaxNormalY;
    private bool enableSlopeHandling;
    private LayerMask slopeLayerMask;
    private float slopeMinAngle;
    private bool lockZ;
    private float lockedZ;
    private OneWayPlatformController oneWayController;

    public void Initialize(Rigidbody rigidbody, CapsuleCollider capsuleCollider)
    {
        rb = rigidbody;
        col = capsuleCollider;
    }

    public void SetOneWayController(OneWayPlatformController controller)
    {
        oneWayController = controller;
    }

    public void SyncSettings(
        LayerMask groundMask,
        LayerMask wallInteractionMask,
        float groundProbeDistance,
        float groundProbeStartOffset,
        float groundProbeInset,
        float wallCheckDistance,
        float wallCheckHeightOffset,
        float wallCheckRadius,
        float wallMinNormalX,
        float wallMaxNormalY,
        bool enableSlopeHandling,
        LayerMask slopeLayerMask,
        float slopeMinAngle,
        bool lockZ,
        float lockedZ)
    {
        this.groundMask = groundMask;
        this.wallInteractionMask = wallInteractionMask.value != 0 ? wallInteractionMask : groundMask;
        this.groundProbeDistance = Mathf.Max(0.001f, groundProbeDistance);
        this.groundProbeStartOffset = Mathf.Max(0f, groundProbeStartOffset);
        this.groundProbeInset = Mathf.Max(0f, groundProbeInset);
        this.wallCheckDistance = Mathf.Max(0.001f, wallCheckDistance);
        this.wallCheckHeightOffset = wallCheckHeightOffset;
        this.wallCheckRadius = Mathf.Max(0.001f, wallCheckRadius);
        this.wallMinNormalX = Mathf.Clamp01(wallMinNormalX);
        this.wallMaxNormalY = Mathf.Clamp01(wallMaxNormalY);
        this.enableSlopeHandling = enableSlopeHandling;
        this.slopeLayerMask = slopeLayerMask;
        this.slopeMinAngle = Mathf.Clamp(slopeMinAngle, 0f, 89f);
        this.lockZ = lockZ;
        this.lockedZ = lockedZ;
    }

    public SurfaceContacts25D ProbeContacts()
    {
        SurfaceContacts25D contacts = default;
        contacts.SlopeTangent = Vector3.right;
        contacts.DownhillSign = 1f;

        if (rb == null || col == null)
            return contacts;

        if (TryGetSupportHit(out RaycastHit supportHit))
        {
            contacts.HasSupport = true;
            contacts.SupportHit = supportHit;
            contacts.IsGrounded = true;
        }

        if (TryGetGroundSurfaceHit(out RaycastHit groundHit))
        {
            contacts.HasGroundSurface = true;
            contacts.GroundHit = groundHit;
            contacts.GroundNormal = groundHit.normal;
            contacts.IsGrounded = contacts.IsGrounded || groundHit.normal.y > 0.05f;
            contacts.IsSlopeSurfaceAuthorized = IsSlopeSurfaceAuthorized(groundHit.collider);
            FillSlopeData(ref contacts);
        }
        else if (contacts.HasSupport)
        {
            contacts.GroundNormal = contacts.SupportHit.normal;
        }

        Bounds bounds = col.bounds;
        float z = lockZ ? lockedZ : bounds.center.z;
        Vector3 wallOrigin = new Vector3(bounds.center.x, bounds.center.y + wallCheckHeightOffset, z);

        contacts.BlockedRight = TryGetWallHit(wallOrigin, wallCheckRadius, Vector3.right, wallCheckDistance, groundMask, SurfaceSensorQuery25D.Wall, out contacts.RightWallHit);
        contacts.BlockedLeft = TryGetWallHit(wallOrigin, wallCheckRadius, Vector3.left, wallCheckDistance, groundMask, SurfaceSensorQuery25D.Wall, out contacts.LeftWallHit);
        contacts.WallInteractableRight = TryGetWallHit(wallOrigin, wallCheckRadius, Vector3.right, wallCheckDistance, wallInteractionMask, SurfaceSensorQuery25D.WallInteraction, out contacts.RightWallInteractionHit);
        contacts.WallInteractableLeft = TryGetWallHit(wallOrigin, wallCheckRadius, Vector3.left, wallCheckDistance, wallInteractionMask, SurfaceSensorQuery25D.WallInteraction, out contacts.LeftWallInteractionHit);
        return contacts;
    }

    public void DrawGizmos()
    {
        if (col == null)
            return;

        Bounds bounds = col.bounds;
        float baseRadius = Mathf.Min(bounds.extents.x, bounds.extents.z);
        float probeRadius = Mathf.Max(0.02f, baseRadius - groundProbeInset);
        float z = lockZ ? lockedZ : bounds.center.z;

        Vector3 supportOrigin = new Vector3(
            bounds.center.x,
            bounds.min.y + probeRadius + 0.005f,
            z);

        Vector3 surfaceOrigin = new Vector3(
            bounds.center.x,
            bounds.min.y + probeRadius + groundProbeStartOffset,
            z);

        float supportDepth = groundProbeDistance + 0.005f;
        float surfaceDepth = groundProbeDistance + groundProbeStartOffset;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(supportOrigin, probeRadius);
        Gizmos.DrawLine(supportOrigin, supportOrigin + Vector3.down * supportDepth);
        Gizmos.DrawWireSphere(supportOrigin + Vector3.down * supportDepth, probeRadius);

        Gizmos.color = new Color(1f, 0.5f, 0.1f, 1f);
        Gizmos.DrawWireSphere(surfaceOrigin, probeRadius);
        Gizmos.DrawLine(surfaceOrigin, surfaceOrigin + Vector3.down * surfaceDepth);
        Gizmos.DrawWireSphere(surfaceOrigin + Vector3.down * surfaceDepth, probeRadius);

        Vector3 wallOrigin = new Vector3(bounds.center.x, bounds.center.y + wallCheckHeightOffset, z);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(wallOrigin, wallOrigin + Vector3.right * wallCheckDistance);
        Gizmos.DrawLine(wallOrigin, wallOrigin + Vector3.left * wallCheckDistance);
        Gizmos.DrawWireSphere(wallOrigin + Vector3.right * wallCheckDistance, wallCheckRadius);
        Gizmos.DrawWireSphere(wallOrigin + Vector3.left * wallCheckDistance, wallCheckRadius);
    }

    private bool TryGetSupportHit(out RaycastHit bestHit)
    {
        return TryGetGroundHitInternal(0.005f, groundProbeDistance + 0.005f, SurfaceSensorQuery25D.Support, out bestHit);
    }

    private bool TryGetGroundSurfaceHit(out RaycastHit bestHit)
    {
        return TryGetGroundHitInternal(groundProbeStartOffset, groundProbeDistance + groundProbeStartOffset, SurfaceSensorQuery25D.GroundSurface, out bestHit);
    }

    private bool TryGetGroundHitInternal(float startOffset, float castDistance, SurfaceSensorQuery25D queryType, out RaycastHit bestHit)
    {
        bestHit = default;

        if (rb == null || col == null)
            return false;

        Bounds bounds = col.bounds;
        float baseRadius = Mathf.Min(bounds.extents.x, bounds.extents.z);
        float probeRadius = Mathf.Max(0.02f, baseRadius - groundProbeInset);
        float z = lockZ ? lockedZ : bounds.center.z;

        Vector3 origin = new Vector3(
            bounds.center.x,
            bounds.min.y + probeRadius + startOffset,
            z);

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            probeRadius,
            Vector3.down,
            castHits,
            castDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestDistance = float.MaxValue;
        float bestNormalY = -1f;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = castHits[i];
            if (hit.collider == null)
                continue;
            if (hit.collider.attachedRigidbody == rb)
                continue;
            if (!ShouldAcceptHit(hit, queryType))
                continue;
            if (hit.normal.y <= 0.05f)
                continue;

            bool betterHit =
                !found ||
                hit.distance < bestDistance - HitDistanceTieEpsilon ||
                (Mathf.Abs(hit.distance - bestDistance) <= HitDistanceTieEpsilon && hit.normal.y > bestNormalY);

            if (!betterHit)
                continue;

            found = true;
            bestDistance = hit.distance;
            bestNormalY = hit.normal.y;
            bestHit = hit;
        }

        return found;
    }

    private bool TryGetWallHit(Vector3 origin, float radius, Vector3 direction, float distance, LayerMask mask, SurfaceSensorQuery25D queryType, out RaycastHit bestHit)
    {
        bestHit = default;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0.001f, radius),
            direction,
            castHits,
            distance,
            mask,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = castHits[i];
            if (hit.collider == null)
                continue;
            if (hit.collider.attachedRigidbody == rb)
                continue;
            if (!ShouldAcceptHit(hit, queryType))
                continue;
            if (Vector3.Dot(hit.normal, direction) >= -0.1f)
                continue;
            if (!IsWallNormal(hit.normal))
                continue;

            if (!found || hit.distance < bestDistance)
            {
                found = true;
                bestDistance = hit.distance;
                bestHit = hit;
            }
        }

        return found;
    }

    private bool ShouldAcceptHit(RaycastHit hit, SurfaceSensorQuery25D queryType)
    {
        if (hit.collider == null)
            return false;

        if (oneWayController == null)
            return true;

        return oneWayController.ShouldAcceptSensorHit(hit, queryType);
    }

    private bool IsSlopeSurfaceAuthorized(Collider collider)
    {
        if (collider == null)
            return false;

        return (slopeLayerMask.value & (1 << collider.gameObject.layer)) != 0;
    }

    private void FillSlopeData(ref SurfaceContacts25D contacts)
    {
        if (!enableSlopeHandling || !contacts.HasGroundSurface || contacts.GroundHit.collider == null || !contacts.IsSlopeSurfaceAuthorized)
            return;

        contacts.SlopeAngle = Vector3.Angle(contacts.GroundHit.normal, Vector3.up);
        if (contacts.SlopeAngle < slopeMinAngle || contacts.SlopeAngle >= 89f)
            return;

        Vector3 tangent;
        if (lockZ)
        {
            tangent = Vector3.Cross(Vector3.forward, contacts.GroundHit.normal);
            tangent.z = 0f;
        }
        else
        {
            tangent = Vector3.ProjectOnPlane(Vector3.right, contacts.GroundHit.normal);
        }

        float magnitude = tangent.magnitude;
        if (magnitude < DotEpsilon)
            return;

        tangent /= magnitude;
        if (Vector3.Dot(tangent, Vector3.right) < 0f)
            tangent = -tangent;

        Vector3 downhill = Vector3.ProjectOnPlane(Physics.gravity, contacts.GroundHit.normal);
        contacts.OnSlope = true;
        contacts.SlopeTangent = tangent;
        contacts.DownhillSign = Vector3.Dot(tangent, downhill) >= 0f ? 1f : -1f;
    }

    private bool IsWallNormal(Vector3 normal)
    {
        return Mathf.Abs(normal.x) >= wallMinNormalX && Mathf.Abs(normal.y) <= wallMaxNormalY;
    }
}
