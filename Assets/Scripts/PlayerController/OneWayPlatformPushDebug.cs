using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class OneWayPlatformPushDebug : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private CapsuleCollider playerCapsule;
    [SerializeField] private OneWayPlatformController oneWayController;

    [Header("Watch Platforms")]
    [Tooltip("Если список пуст, скрипт попробует автоматически наблюдать платформы из OneWayPlatformController, а если его нет — все OneWayBoxPlatform в сцене.")]
    [SerializeField] private OneWayBoxPlatform[] watchedPlatforms;

    [Header("Logging")]
    [SerializeField] private bool debugEnabled = true;
    [SerializeField] private bool logPhaseChanges = true;
    [SerializeField] private bool logCollisionContacts = true;
    [SerializeField] private bool logComputePenetration = true;
    [SerializeField] private bool logOnlyWhileFalling = false;
    [SerializeField] private bool logOnlyUpwardContacts = true;
    [SerializeField, Range(0f, 1f)] private float upwardNormalThreshold = 0.6f;
    [SerializeField, Range(0f, 1f)] private float upwardSeparationThreshold = 0.6f;
    [SerializeField] private float minRepeatLogInterval = 0.05f;

    [Header("Gizmos")]
    [SerializeField] private bool drawPenetrationGizmos = true;
    [SerializeField] private float gizmoRayScale = 1f;

    private readonly Dictionary<OneWayBoxPlatform, OneWayPlatformRuntimePhase> lastPhaseByPlatform = new Dictionary<OneWayBoxPlatform, OneWayPlatformRuntimePhase>(8);
    private readonly Dictionary<OneWayBoxPlatform, float> nextPenetrationLogTimeByPlatform = new Dictionary<OneWayBoxPlatform, float>(8);
    private readonly Dictionary<Collider, float> nextCollisionLogTimeByCollider = new Dictionary<Collider, float>(8);
    private readonly List<OneWayBoxPlatform> runtimeWatchedPlatforms = new List<OneWayBoxPlatform>(8);
    private readonly Dictionary<OneWayBoxPlatform, PenetrationInfo> lastPenetrationByPlatform = new Dictionary<OneWayBoxPlatform, PenetrationInfo>(8);
    private readonly List<OneWayPlatformRuntimeState> runtimeStateBuffer = new List<OneWayPlatformRuntimeState>(16);

    private struct PenetrationInfo
    {
        public bool HasOverlap;
        public Vector3 Direction;
        public float Distance;
        public float UpDot;
        public Vector3 Origin;
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void Reset()
    {
        CacheComponents();
    }

    private void OnValidate()
    {
        CacheComponents();
        upwardNormalThreshold = Mathf.Clamp01(upwardNormalThreshold);
        upwardSeparationThreshold = Mathf.Clamp01(upwardSeparationThreshold);
        minRepeatLogInterval = Mathf.Max(0f, minRepeatLogInterval);
        gizmoRayScale = Mathf.Max(0.01f, gizmoRayScale);
    }

    private void FixedUpdate()
    {
        if (!debugEnabled)
            return;

        CacheComponents();
        if (playerCapsule == null)
            return;

        CollectWatchedPlatforms(runtimeWatchedPlatforms);

        if (logPhaseChanges)
            LogPhaseChanges(runtimeWatchedPlatforms);

        if (logComputePenetration)
            ProbePenetration(runtimeWatchedPlatforms);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryLogCollision(collision, "Enter");
    }

    private void OnCollisionStay(Collision collision)
    {
        TryLogCollision(collision, "Stay");
    }

    private void CollectWatchedPlatforms(List<OneWayBoxPlatform> results)
    {
        results.Clear();

        if (watchedPlatforms != null && watchedPlatforms.Length > 0)
        {
            for (int i = 0; i < watchedPlatforms.Length; i++)
            {
                OneWayBoxPlatform platform = watchedPlatforms[i];
                if (platform == null)
                    continue;

                if (!results.Contains(platform))
                    results.Add(platform);
            }

            return;
        }

        if (oneWayController != null)
        {
            runtimeStateBuffer.Clear();
            oneWayController.GetRuntimeStates(runtimeStateBuffer);
            for (int i = 0; i < runtimeStateBuffer.Count; i++)
            {
                OneWayPlatformRuntimeState state = runtimeStateBuffer[i];
                if (state == null || state.Platform == null)
                    continue;

                if (!results.Contains(state.Platform))
                    results.Add(state.Platform);
            }
        }

        if (results.Count > 0)
            return;

        OneWayBoxPlatform[] allPlatforms = FindObjectsByType<OneWayBoxPlatform>(FindObjectsSortMode.None);
        for (int i = 0; i < allPlatforms.Length; i++)
        {
            OneWayBoxPlatform platform = allPlatforms[i];
            if (platform == null)
                continue;

            if (!results.Contains(platform))
                results.Add(platform);
        }
    }

    private void LogPhaseChanges(List<OneWayBoxPlatform> platforms)
    {
        for (int i = 0; i < platforms.Count; i++)
        {
            OneWayBoxPlatform platform = platforms[i];
            OneWayPlatformRuntimePhase phase = ResolvePhase(platform, out OneWayPlatformRuntimeState state, out bool hasState);

            OneWayPlatformRuntimePhase previousPhase;
            bool hadPrevious = lastPhaseByPlatform.TryGetValue(platform, out previousPhase);
            if (!hadPrevious || previousPhase != phase)
            {
                Debug.Log(ComposePhaseMessage(platform, previousPhase, phase, hadPrevious, state, hasState), platform);
                lastPhaseByPlatform[platform] = phase;
            }
        }
    }

    private void ProbePenetration(List<OneWayBoxPlatform> platforms)
    {
        for (int i = 0; i < platforms.Count; i++)
        {
            OneWayBoxPlatform platform = platforms[i];
            Collider platformCollider = platform != null ? platform.PlatformCollider : null;
            if (platformCollider == null || !platformCollider.enabled || playerCapsule == null || !playerCapsule.enabled)
                continue;

            Vector3 direction;
            float distance;
            bool overlapped = Physics.ComputePenetration(
                playerCapsule,
                playerCapsule.transform.position,
                playerCapsule.transform.rotation,
                platformCollider,
                platformCollider.transform.position,
                platformCollider.transform.rotation,
                out direction,
                out distance);

            PenetrationInfo info = default;
            info.HasOverlap = overlapped;
            info.Direction = overlapped ? direction : Vector3.zero;
            info.Distance = overlapped ? distance : 0f;
            info.UpDot = overlapped ? Vector3.Dot(direction, Vector3.up) : 0f;
            info.Origin = playerCapsule.bounds.center;
            lastPenetrationByPlatform[platform] = info;

            if (!overlapped)
                continue;

            if (logOnlyWhileFalling && rb != null && rb.linearVelocity.y > 0f)
                continue;

            if (info.UpDot < upwardSeparationThreshold)
                continue;

            float nextLogTime;
            if (nextPenetrationLogTimeByPlatform.TryGetValue(platform, out nextLogTime) && Time.unscaledTime < nextLogTime)
                continue;

            nextPenetrationLogTimeByPlatform[platform] = Time.unscaledTime + minRepeatLogInterval;

            OneWayPlatformRuntimePhase phase = ResolvePhase(platform, out OneWayPlatformRuntimeState state, out bool hasState);
            Debug.Log(ComposePenetrationMessage(platform, phase, state, hasState, info), platform);
        }
    }

    private void TryLogCollision(Collision collision, string eventLabel)
    {
        if (!debugEnabled || !logCollisionContacts || collision == null)
            return;

        OneWayBoxPlatform platform = ResolvePlatform(collision.collider);

        if (platform == null)
            return;

        if (!ShouldWatchPlatform(platform))
            return;

        if (logOnlyWhileFalling && rb != null && rb.linearVelocity.y > 0f)
            return;

        float nextLogTime;
        Collider keyCollider = platform.PlatformCollider != null ? platform.PlatformCollider : collision.collider;
        if (nextCollisionLogTimeByCollider.TryGetValue(keyCollider, out nextLogTime) && Time.unscaledTime < nextLogTime)
            return;

        int contactCount = collision.contactCount;
        if (contactCount <= 0)
            return;

        ContactPoint bestContact = default;
        float bestUpDot = float.NegativeInfinity;
        bool hasBest = false;

        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            float upDot = Vector3.Dot(contact.normal, Vector3.up);
            if (!hasBest || upDot > bestUpDot)
            {
                bestContact = contact;
                bestUpDot = upDot;
                hasBest = true;
            }
        }

        if (!hasBest)
            return;

        if (logOnlyUpwardContacts && bestUpDot < upwardNormalThreshold)
            return;

        nextCollisionLogTimeByCollider[keyCollider] = Time.unscaledTime + minRepeatLogInterval;

        OneWayPlatformRuntimePhase phase = ResolvePhase(platform, out OneWayPlatformRuntimeState state, out bool hasState);
        Debug.Log(ComposeCollisionMessage(platform, eventLabel, collision, bestContact, bestUpDot, phase, state, hasState), platform);
    }

    private bool ShouldWatchPlatform(OneWayBoxPlatform platform)
    {
        if (platform == null)
            return false;

        if (watchedPlatforms == null || watchedPlatforms.Length == 0)
            return true;

        for (int i = 0; i < watchedPlatforms.Length; i++)
        {
            if (watchedPlatforms[i] == platform)
                return true;
        }

        return false;
    }

    private OneWayPlatformRuntimePhase ResolvePhase(OneWayBoxPlatform platform, out OneWayPlatformRuntimeState state, out bool hasState)
    {
        state = null;
        hasState = false;

        if (platform == null)
            return OneWayPlatformRuntimePhase.Unknown;

        if (oneWayController == null)
            oneWayController = GetComponent<OneWayPlatformController>();

        if (oneWayController != null && oneWayController.TryGetRuntimeState(platform, out state) && state != null)
        {
            hasState = true;
            return state.Phase;
        }

        return OneWayPlatformRuntimePhase.Unknown;
    }

    private string ComposePhaseMessage(OneWayBoxPlatform platform, OneWayPlatformRuntimePhase previousPhase, OneWayPlatformRuntimePhase phase, bool hadPrevious, OneWayPlatformRuntimeState state, bool hasState)
    {
        string previous = hadPrevious ? previousPhase.ToString() : "<none>";
        string reasons = hasState ? state.ActiveReasons.ToString() : "NoRuntimeState";

        return string.Format(
            "[OneWayPushDebug][Phase] frame={0} fixedTime={1:F3} platform={2} {3} -> {4} | playerBottomY={5:F4} playerVelY={6:F4} topY={7:F4} passY={8:F4} solidifyY={9:F4} reasons={10}",
            Time.frameCount,
            Time.fixedTime,
            platform.name,
            previous,
            phase,
            GetPlayerBottomY(),
            rb != null ? rb.linearVelocity.y : 0f,
            platform.TopY,
            platform.PassThroughThresholdY,
            platform.SolidifyThresholdY,
            reasons);
    }

    private string ComposePenetrationMessage(OneWayBoxPlatform platform, OneWayPlatformRuntimePhase phase, OneWayPlatformRuntimeState state, bool hasState, PenetrationInfo info)
    {
        string reasons = hasState ? state.ActiveReasons.ToString() : "NoRuntimeState";

        return string.Format(
            "[OneWayPushDebug][Penetration] frame={0} fixedTime={1:F3} platform={2} phase={3} reasons={4} | overlap=true dir={5} upDot={6:F3} dist={7:F5} | playerBottomY={8:F4} playerVel={9} | topY={10:F4} passY={11:F4} solidifyY={12:F4}",
            Time.frameCount,
            Time.fixedTime,
            platform.name,
            phase,
            reasons,
            FormatVector3(info.Direction),
            info.UpDot,
            info.Distance,
            GetPlayerBottomY(),
            rb != null ? FormatVector3(rb.linearVelocity) : "(0.000, 0.000, 0.000)",
            platform.TopY,
            platform.PassThroughThresholdY,
            platform.SolidifyThresholdY);
    }

    private string ComposeCollisionMessage(OneWayBoxPlatform platform, string eventLabel, Collision collision, ContactPoint bestContact, float bestUpDot, OneWayPlatformRuntimePhase phase, OneWayPlatformRuntimeState state, bool hasState)
    {
        string reasons = hasState ? state.ActiveReasons.ToString() : "NoRuntimeState";

        return string.Format(
            "[OneWayPushDebug][Collision{0}] frame={1} fixedTime={2:F3} platform={3} phase={4} reasons={5} | point={6} normal={7} upDot={8:F3} relativeVel={9} playerVel={10} | playerBottomY={11:F4} topY={12:F4}",
            eventLabel,
            Time.frameCount,
            Time.fixedTime,
            platform.name,
            phase,
            reasons,
            FormatVector3(bestContact.point),
            FormatVector3(bestContact.normal),
            bestUpDot,
            FormatVector3(collision.relativeVelocity),
            rb != null ? FormatVector3(rb.linearVelocity) : "(0.000, 0.000, 0.000)",
            GetPlayerBottomY(),
            platform.TopY);
    }

    private float GetPlayerBottomY()
    {
        return playerCapsule != null ? playerCapsule.bounds.min.y : 0f;
    }

    private static string FormatVector3(Vector3 value)
    {
        return string.Format("({0:F3}, {1:F3}, {2:F3})", value.x, value.y, value.z);
    }

    private static OneWayBoxPlatform ResolvePlatform(Collider collider)
    {
        return OneWayPlatformUtility.ResolvePlatform(collider);
    }

    private void CacheComponents()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (playerCapsule == null)
            playerCapsule = GetComponent<CapsuleCollider>();

        if (oneWayController == null)
            oneWayController = GetComponent<OneWayPlatformController>();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawPenetrationGizmos || lastPenetrationByPlatform.Count == 0)
            return;

        foreach (KeyValuePair<OneWayBoxPlatform, PenetrationInfo> entry in lastPenetrationByPlatform)
        {
            OneWayBoxPlatform platform = entry.Key;
            PenetrationInfo info = entry.Value;
            if (platform == null || !info.HasOverlap)
                continue;

            Gizmos.color = info.UpDot >= upwardSeparationThreshold
                ? Color.red
                : Color.yellow;

            Vector3 start = info.Origin;
            Vector3 end = start + info.Direction * Mathf.Max(0.02f, info.Distance * gizmoRayScale);
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, 0.025f);
        }
    }
}
