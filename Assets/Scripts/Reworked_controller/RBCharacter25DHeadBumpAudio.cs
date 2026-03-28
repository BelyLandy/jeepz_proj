using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RBCharacter25D), typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class RBCharacter25DHeadBumpAudio : MonoBehaviour
{
    private const float InvalidPastTime = -999f;
    private const float HitDistanceTieEpsilon = 0.0025f;

    [Header("Head Bump Audio")]
    [SerializeField] private bool enableHeadBumpAudio = true;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] singleJumpHeadBumpClips;
    [SerializeField] private AudioClip[] doubleJumpHeadBumpClips;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Header("Cinemachine Impulse")]
    [Tooltip("Cinemachine Impulse Source, который должен сработать после удара головой после обычного прыжка.")]
    [SerializeField] private CinemachineImpulseSource singleJumpImpulseSource;

    [Tooltip("Cinemachine Impulse Source, который должен сработать после удара головой после двойного прыжка.")]
    [SerializeField] private CinemachineImpulseSource doubleJumpImpulseSource;

    [Header("Detection")]
    [Tooltip("Минимальная скорость вверх, чтобы удар о потолок считался именно ударом после своего прыжка.")]
    [SerializeField] private float minUpwardSpeedToTrigger = 0.05f;

    [Tooltip("Насколько далеко вверх искать потолок от верхней части капсулы.")]
    [SerializeField] private float ceilingProbeDistance = 0.08f;

    [Tooltip("Насколько уменьшать радиус верхней проверки, чтобы меньше цепляться краями.")]
    [SerializeField] private float ceilingProbeInset = 0.03f;

    [Tooltip("Насколько нормаль должна смотреть вниз по Y, чтобы поверхность считалась потолком.")]
    [SerializeField, Range(0f, 1f)] private float minCeilingNormalDownY = 0.2f;

    [Header("Anti Spam")]
    [Tooltip("Короткий кулдаун от дребезга контакта с потолком.")]
    [SerializeField] private float retriggerCooldown = 0.04f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugWhileSelected = true;
    [SerializeField] private float debugHitMarkerRadius = 0.04f;

    private RBCharacter25D controller;
    private Rigidbody rb;
    private CapsuleCollider col;

    private readonly RaycastHit[] castHits = new RaycastHit[12];

    private int observedJumpStateVersion = -1;
    private RBCharacter25D.SelfJumpKind armedJumpKind = RBCharacter25D.SelfJumpKind.None;
    private bool isArmed;
    private bool wasTouchingCeilingLastFixed;
    private float triggerBlockedUntilTime = InvalidPastTime;

    private Vector3 lastProbeOrigin;
    private float lastProbeRadius;
    private bool hadLastValidHit;
    private RaycastHit lastValidHit;

    private void Awake()
    {
        CacheComponents();
        ClampSettings();
    }

    private void OnValidate()
    {
        CacheComponents();
        ClampSettings();
    }

    private void FixedUpdate()
    {
        CacheComponents();

        if (controller == null || rb == null || col == null)
            return;

        SyncArmedJumpState();

        if (!enableHeadBumpAudio)
        {
            wasTouchingCeilingLastFixed = false;
            hadLastValidHit = false;
            return;
        }

        if (isArmed && rb.linearVelocity.y <= 0f)
            Disarm();

        bool touchingCeiling = TryGetCeilingHit(out RaycastHit ceilingHit, out Vector3 probeOrigin, out float probeRadius);
        bool movingUp = rb.linearVelocity.y >= minUpwardSpeedToTrigger;

        lastProbeOrigin = probeOrigin;
        lastProbeRadius = probeRadius;
        hadLastValidHit = touchingCeiling;
        lastValidHit = ceilingHit;

        bool canTrigger =
            isArmed &&
            movingUp &&
            touchingCeiling &&
            !wasTouchingCeilingLastFixed &&
            Time.time >= triggerBlockedUntilTime;

        if (canTrigger)
        {
            TriggerArmedFeedback();
            Disarm();
            triggerBlockedUntilTime = Time.time + retriggerCooldown;
        }

        wasTouchingCeilingLastFixed = touchingCeiling;

        DrawRuntimeDebug();
    }

    private void CacheComponents()
    {
        if (controller == null) controller = GetComponent<RBCharacter25D>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (col == null) col = GetComponent<CapsuleCollider>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void ClampSettings()
    {
        volume = Mathf.Clamp01(volume);
        minUpwardSpeedToTrigger = Mathf.Max(0f, minUpwardSpeedToTrigger);
        ceilingProbeDistance = Mathf.Max(0.001f, ceilingProbeDistance);
        ceilingProbeInset = Mathf.Max(0f, ceilingProbeInset);
        minCeilingNormalDownY = Mathf.Clamp01(minCeilingNormalDownY);
        retriggerCooldown = Mathf.Max(0f, retriggerCooldown);
        debugHitMarkerRadius = Mathf.Max(0.005f, debugHitMarkerRadius);
    }

    private void SyncArmedJumpState()
    {
        int controllerVersion = controller.LastSelfJumpStateVersion;
        if (controllerVersion == observedJumpStateVersion)
            return;

        observedJumpStateVersion = controllerVersion;
        armedJumpKind = controller.LastSelfJumpType;
        isArmed = armedJumpKind != RBCharacter25D.SelfJumpKind.None;
        wasTouchingCeilingLastFixed = false;
    }

    private void Disarm()
    {
        armedJumpKind = RBCharacter25D.SelfJumpKind.None;
        isArmed = false;
    }

    private void TriggerArmedFeedback()
    {
        PlayArmedClip();
        TriggerArmedImpulse();
    }

    private void PlayArmedClip()
    {
        AudioClip clip = GetRandomArmedClip();
        if (clip == null)
            return;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
            return;
        }

        AudioSource.PlayClipAtPoint(clip, transform.position, volume);
    }

    private void TriggerArmedImpulse()
    {
        CinemachineImpulseSource impulseSource = GetArmedImpulseSource();
        if (impulseSource == null)
            return;

        impulseSource.GenerateImpulse();
    }

    private CinemachineImpulseSource GetArmedImpulseSource()
    {
        switch (armedJumpKind)
        {
            case RBCharacter25D.SelfJumpKind.SingleJump:
                return singleJumpImpulseSource;

            case RBCharacter25D.SelfJumpKind.DoubleJump:
                return doubleJumpImpulseSource;

            default:
                return null;
        }
    }

    private AudioClip GetRandomArmedClip()
    {
        switch (armedJumpKind)
        {
            case RBCharacter25D.SelfJumpKind.SingleJump:
                return GetRandomClip(singleJumpHeadBumpClips);

            case RBCharacter25D.SelfJumpKind.DoubleJump:
                return GetRandomClip(doubleJumpHeadBumpClips);

            default:
                return null;
        }
    }

    private static AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int validCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return null;

        int targetValidIndex = Random.Range(0, validCount);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;

            if (targetValidIndex == 0)
                return clips[i];

            targetValidIndex--;
        }

        return null;
    }

    private bool TryGetCeilingHit(
        out RaycastHit bestHit,
        out Vector3 probeOrigin,
        out float probeRadius)
    {
        bestHit = default;

        Bounds bounds = col.bounds;
        probeRadius = Mathf.Max(0.02f, Mathf.Min(bounds.extents.x, bounds.extents.z) - ceilingProbeInset);

        float z = controller.UsesLockedZ ? controller.LockedZPosition : bounds.center.z;
        probeOrigin = new Vector3(
            bounds.center.x,
            bounds.max.y - probeRadius,
            z
        );

        int hitCount = Physics.SphereCastNonAlloc(
            probeOrigin,
            probeRadius,
            Vector3.up,
            castHits,
            ceilingProbeDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        bool found = false;
        float bestDistance = float.MaxValue;
        float bestNormalY = 1f;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = castHits[i];

            if (hit.collider == null)
                continue;

            if (hit.collider == col)
                continue;

            if (hit.collider.attachedRigidbody == rb)
                continue;

            if (!RBCharacter25DHeadBumpSurface.AllowsHeadBump(hit.collider, controller.GroundMask))
                continue;

            if (hit.normal.y > -minCeilingNormalDownY)
                continue;

            bool betterHit =
                !found ||
                hit.distance < bestDistance - HitDistanceTieEpsilon ||
                (Mathf.Abs(hit.distance - bestDistance) <= HitDistanceTieEpsilon && hit.normal.y < bestNormalY);

            if (!betterHit)
                continue;

            found = true;
            bestHit = hit;
            bestDistance = hit.distance;
            bestNormalY = hit.normal.y;
        }

        return found;
    }

    private void DrawRuntimeDebug()
    {
        if (!Application.isPlaying || !drawDebugWhileSelected)
            return;

        Debug.DrawLine(lastProbeOrigin, lastProbeOrigin + Vector3.up * ceilingProbeDistance, hadLastValidHit ? Color.green : Color.cyan);

        if (!hadLastValidHit)
            return;

        Debug.DrawLine(lastValidHit.point, lastValidHit.point + lastValidHit.normal * 0.2f, Color.yellow);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugWhileSelected)
            return;

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null)
            return;

        RBCharacter25D maybeController = GetComponent<RBCharacter25D>();

        Bounds bounds = capsule.bounds;
        float probeRadius = Mathf.Max(0.02f, Mathf.Min(bounds.extents.x, bounds.extents.z) - Mathf.Max(0f, ceilingProbeInset));
        float z = maybeController != null && maybeController.UsesLockedZ ? maybeController.LockedZPosition : bounds.center.z;

        Vector3 probeOrigin = new Vector3(
            bounds.center.x,
            bounds.max.y - probeRadius,
            z
        );

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(probeOrigin, probeRadius);
        Gizmos.DrawWireSphere(probeOrigin + Vector3.up * ceilingProbeDistance, probeRadius);
        Gizmos.DrawLine(probeOrigin, probeOrigin + Vector3.up * ceilingProbeDistance);

        if (!Application.isPlaying || !hadLastValidHit)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(lastValidHit.point, lastValidHit.point + lastValidHit.normal * 0.2f);

        float r = debugHitMarkerRadius;
        Gizmos.DrawLine(lastValidHit.point + Vector3.left * r, lastValidHit.point + Vector3.right * r);
        Gizmos.DrawLine(lastValidHit.point + Vector3.down * r, lastValidHit.point + Vector3.up * r);
    }
}
