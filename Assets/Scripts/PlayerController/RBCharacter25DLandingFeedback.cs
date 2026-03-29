using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RBCharacter25D), typeof(Rigidbody))]
public sealed class RBCharacter25DLandingFeedback : MonoBehaviour
{
    private const float InvalidPastTime = -999f;

    [Header("Landing Audio")]
    [SerializeField] private bool enableLandingFeedback = true;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] landingClips;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    [Header("Cinemachine Impulse")]
    [SerializeField] private CinemachineImpulseSource landingImpulseSource;

    [Header("Trigger Conditions")]
    [Tooltip("Минимальное время в воздухе перед приземлением, чтобы сработал звук.")]
    [SerializeField] private float minAirborneTimeToTrigger = 0.05f;

    [Tooltip("Самая слабая отрицательная скорость Y за время полёта, при которой считается, что было реальное приземление.")]
    [SerializeField] private float minDownwardSpeedToTrigger = 1.25f;

    [Tooltip("Короткий кулдаун от повторных срабатываний на одном приземлении.")]
    [SerializeField] private float retriggerCooldown = 0.05f;

    [Tooltip("После завершения vault обычный landing feedback кратко подавляется.")]
    [SerializeField] private float suppressLandingAfterVaultWindow = 0.12f;

    [Tooltip("После завершения wall slide обычный landing feedback кратко подавляется.")]
    [SerializeField] private float suppressLandingAfterWallSlideWindow = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool debugLogLanding = false;

    private RBCharacter25D controller;
    private Rigidbody rb;

    private bool wasGroundedLastFixed;
    private bool hasAirborneState;
    private float airborneStartTime = InvalidPastTime;
    private float mostNegativeAirborneYSpeed;
    private float triggerBlockedUntilTime = InvalidPastTime;

    private void Awake()
    {
        CacheComponents();
        ClampSettings();

        if (controller != null)
            wasGroundedLastFixed = controller.IsGroundedNow;

        if (!wasGroundedLastFixed)
            BeginAirborneState();
    }

    private void OnValidate()
    {
        CacheComponents();
        ClampSettings();
    }

    private void FixedUpdate()
    {
        CacheComponents();
        if (controller == null || rb == null)
            return;

        bool groundedNow = controller.IsGroundedNow;

        if (!enableLandingFeedback)
        {
            SyncStateWithoutFeedback(groundedNow);
            return;
        }

        if (!groundedNow)
        {
            if (wasGroundedLastFixed)
                BeginAirborneState();

            TrackAirborneMotion();
        }
        else if (!wasGroundedLastFixed)
        {
            TryTriggerLandingFeedback();
            ResetAirborneState();
        }

        wasGroundedLastFixed = groundedNow;
    }

    private void CacheComponents()
    {
        if (controller == null) controller = GetComponent<RBCharacter25D>();
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void ClampSettings()
    {
        volume = Mathf.Clamp01(volume);
        minAirborneTimeToTrigger = Mathf.Max(0f, minAirborneTimeToTrigger);
        minDownwardSpeedToTrigger = Mathf.Max(0f, minDownwardSpeedToTrigger);
        retriggerCooldown = Mathf.Max(0f, retriggerCooldown);
        suppressLandingAfterVaultWindow = Mathf.Max(0f, suppressLandingAfterVaultWindow);
        suppressLandingAfterWallSlideWindow = Mathf.Max(0f, suppressLandingAfterWallSlideWindow);
    }

    private void SyncStateWithoutFeedback(bool groundedNow)
    {
        if (!groundedNow)
        {
            if (wasGroundedLastFixed)
                BeginAirborneState();

            TrackAirborneMotion();
        }
        else if (!wasGroundedLastFixed)
        {
            ResetAirborneState();
        }

        wasGroundedLastFixed = groundedNow;
    }

    private void BeginAirborneState()
    {
        hasAirborneState = true;
        airborneStartTime = Time.time;
        mostNegativeAirborneYSpeed = Mathf.Min(0f, rb != null ? rb.linearVelocity.y : 0f);
    }

    private void TrackAirborneMotion()
    {
        if (!hasAirborneState || rb == null)
            return;

        float y = rb.linearVelocity.y;
        if (y < mostNegativeAirborneYSpeed)
            mostNegativeAirborneYSpeed = y;
    }

    private void ResetAirborneState()
    {
        hasAirborneState = false;
        airborneStartTime = InvalidPastTime;
        mostNegativeAirborneYSpeed = 0f;
    }

    private void TryTriggerLandingFeedback()
    {
        if (!hasAirborneState)
            return;

        if (controller != null &&
            controller.LastVaultFinishedTime > InvalidPastTime &&
            (Time.time - controller.LastVaultFinishedTime) <= suppressLandingAfterVaultWindow)
        {
            if (debugLogLanding)
            {
                Debug.Log(
                    $"[RBCharacter25DLandingFeedback] Landing suppressed after vault on {name}. " +
                    $"timeSinceVault={(Time.time - controller.LastVaultFinishedTime):F3}",
                    this);
            }

            return;
        }

        if (controller != null &&
            controller.LastWallSlideFinishedTime > InvalidPastTime &&
            (Time.time - controller.LastWallSlideFinishedTime) <= suppressLandingAfterWallSlideWindow)
        {
            if (debugLogLanding)
            {
                Debug.Log(
                    $"[RBCharacter25DLandingFeedback] Landing suppressed after wall slide on {name}. " +
                    $"timeSinceWallSlide={(Time.time - controller.LastWallSlideFinishedTime):F3}",
                    this);
            }

            return;
        }

        if (Time.time < triggerBlockedUntilTime)
            return;

        float airborneTime = airborneStartTime > InvalidPastTime
            ? (Time.time - airborneStartTime)
            : 0f;

        if (airborneTime < minAirborneTimeToTrigger)
            return;

        if (mostNegativeAirborneYSpeed > -minDownwardSpeedToTrigger)
            return;

        PlayLandingClip();
        TriggerLandingImpulse();
        triggerBlockedUntilTime = Time.time + retriggerCooldown;

        if (debugLogLanding)
        {
            Debug.Log(
                $"[RBCharacter25DLandingFeedback] Landing triggered on {name}. " +
                $"airborneTime={airborneTime:F3}, mostNegativeAirborneYSpeed={mostNegativeAirborneYSpeed:F3}",
                this);
        }
    }

    private void PlayLandingClip()
    {
        AudioClip clip = GetRandomClip(landingClips);
        if (clip == null)
            return;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, volume);
            return;
        }

        AudioSource.PlayClipAtPoint(clip, transform.position, volume);
    }

    private void TriggerLandingImpulse()
    {
        if (landingImpulseSource == null)
            return;

        landingImpulseSource.GenerateImpulse();
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
}
