using UnityEngine;
using UnityEngine.Animations.Rigging; // <- важно

public class HeadLook25D : MonoBehaviour
{
    [Header("References")]
    public Transform[] targets;
    public Transform headBone;
    public RotationAnim rotationAnim;

    [Header("Optional: Spine tracking constraint")]
    public Rig spineTracking; // <- сюда перетащи SpineTracking (Multi-Rotation Constraint)
    [Range(0f, 1f)] public float spineWeightWhenSeen = 1f;
    public float spineWeightUpSpeed = 4f;    // скорость подъёма 0->1
    public float spineWeightDownSpeed = 6f;  // скорость спуска 1->0

    [Header("Look limits (local Z)")]
    public float minZ = -60f;
    public float maxZ = -25f;

    [Header("Fallback when no valid target")]
    public float defaultZ = -27.319f;

    [Header("Field of view")]
    [Range(0f, 180f)]
    public float fovHalfAngle = 70f;

    [Header("View distance")]
    public float viewDistance = 8f;

    [Header("Tuning")]
    public float zOffset = -20f;
    public bool invertY = true;
    public float smoothSpeed = 10f;

    [Header("Debug")]
    public bool debugLogOnSee = true;

    private Transform lastSeenTarget;

    void Awake()
    {
        // Чтобы стартовал с нуля (по желанию)
        if (spineTracking != null)
            spineTracking.weight = 0f;
    }

    void LateUpdate()
    {
        if (!headBone || !rotationAnim) return;

        Transform best = FindBestTarget(out Vector2 bestToTargetWorld);
        bool hasTarget = best != null;

        // Плавно ведём weight для SpineTracking
        UpdateSpineWeight(hasTarget);

        // Debug только по смене
        if (debugLogOnSee)
        {
            if (best != null && lastSeenTarget == null)
                Debug.Log($"[HeadLook25D] Saw target: {best.name}", best);
            else if (best != null && lastSeenTarget != best)
                Debug.Log($"[HeadLook25D] Switched target: {lastSeenTarget?.name ?? "None"} -> {best.name}", best);
            else if (best == null && lastSeenTarget != null)
                Debug.Log($"[HeadLook25D] Lost target: {lastSeenTarget.name}", lastSeenTarget);
        }

        lastSeenTarget = best;

        float desiredZ;

        if (!hasTarget)
        {
            desiredZ = defaultZ;
        }
        else
        {
            Vector2 toTarget = bestToTargetWorld;
            toTarget.x *= rotationAnim.FacingSign;

            if (invertY) toTarget.y = -toTarget.y;

            float z = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            z += zOffset;

            z = Mathf.DeltaAngle(0f, z);
            desiredZ = Mathf.Clamp(z, minZ, maxZ);
        }

        // Меняем только Z локально
        Vector3 e = headBone.localEulerAngles;
        float currentZ = Mathf.DeltaAngle(0f, e.z);
        float newZ = Mathf.LerpAngle(currentZ, desiredZ, smoothSpeed * Time.deltaTime);

        headBone.localRotation = Quaternion.Euler(e.x, e.y, newZ);
    }

    void UpdateSpineWeight(bool hasTarget)
    {
        if (spineTracking == null) return;

        float targetW = hasTarget ? spineWeightWhenSeen : 0f;

        float speed = (targetW > spineTracking.weight)
            ? spineWeightUpSpeed
            : spineWeightDownSpeed;

        spineTracking.weight = Mathf.MoveTowards(
            spineTracking.weight,
            targetW,
            speed * Time.deltaTime
        );
    }

    Transform FindBestTarget(out Vector2 bestToTargetWorld)
    {
        bestToTargetWorld = Vector2.zero;

        if (targets == null || targets.Length == 0) return null;

        float maxDistSqr = viewDistance * viewDistance;

        Transform best = null;
        float bestDistSqr = float.PositiveInfinity;

        for (int i = 0; i < targets.Length; i++)
        {
            Transform t = targets[i];
            if (!t) continue;

            Vector2 toTargetWorld = (Vector2)(t.position - headBone.position);
            float distSqr = toTargetWorld.sqrMagnitude;

            if (distSqr < 0.0001f) continue;
            if (distSqr > maxDistSqr) continue;

            Vector2 toTarget = toTargetWorld;
            toTarget.x *= rotationAnim.FacingSign;
            if (invertY) toTarget.y = -toTarget.y;

            float angle = Vector2.Angle(Vector2.right, toTarget.normalized);
            if (angle > fovHalfAngle) continue;

            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                best = t;
                bestToTargetWorld = toTargetWorld;
            }
        }

        return best;
    }
}