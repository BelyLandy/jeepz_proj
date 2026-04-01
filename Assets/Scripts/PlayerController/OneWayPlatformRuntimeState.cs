using System;
using UnityEngine;

[Flags]
public enum OneWayPassThroughReason
{
    None = 0,
    LegacyResolver = 1 << 0,
    UpwardCross = 1 << 1,
    Vault = 1 << 2,
    DropDown = 1 << 3,
    ExternalOverride = 1 << 4,
}

public enum OneWayPlatformRuntimePhase
{
    Unknown = 0,
    Solid = 1,
    CandidateBelow = 2,
    PassingUp = 3,
    Supported = 4,
    SuppressedByVault = 5,
    SuppressedByDropDown = 6,
}

[Serializable]
public struct OneWayActorSnapshot
{
    public bool IsValid;
    public bool CapsuleEnabled;
    public Bounds Bounds;
    public Vector3 Velocity;
    public float BottomY;
    public float TopY;
    public float CenterY;

    public static OneWayActorSnapshot Capture(CapsuleCollider capsule, Rigidbody rigidbody)
    {
        OneWayActorSnapshot snapshot = default;
        if (capsule == null)
            return snapshot;

        Bounds bounds = capsule.bounds;
        snapshot.IsValid = true;
        snapshot.CapsuleEnabled = capsule.enabled;
        snapshot.Bounds = bounds;
        snapshot.Velocity = rigidbody != null ? rigidbody.linearVelocity : Vector3.zero;
        snapshot.BottomY = bounds.min.y;
        snapshot.TopY = bounds.max.y;
        snapshot.CenterY = bounds.center.y;
        return snapshot;
    }
}

[Serializable]
public struct OneWaySupportInfo
{
    public bool HasSupport;
    public Collider SupportCollider;
    public RaycastHit SupportHit;
    public OneWayBoxPlatform SupportPlatform;

    public static OneWaySupportInfo FromContacts(SurfaceContacts25D contacts)
    {
        OneWaySupportInfo info = default;
        if (!contacts.HasSupport || contacts.SupportHit.collider == null)
            return info;

        info.HasSupport = true;
        info.SupportHit = contacts.SupportHit;
        info.SupportCollider = contacts.SupportHit.collider;
        info.SupportPlatform = OneWayPlatformUtility.ResolvePlatform(contacts.SupportHit.collider);
        return info;
    }
}

[Serializable]
public sealed class OneWayPlatformRuntimeState
{
    public OneWayBoxPlatform Platform;
    public OneWayPlatformRuntimePhase Phase;
    public OneWayPassThroughReason ActiveReasons;

    public bool IsNearby;
    public bool HasHorizontalOverlap;
    public bool WasBelowTopLastFixed;
    public bool CrossedTopPlaneUpThisFixed;
    public bool IsBelowPassThroughThreshold;
    public bool IsAboveSolidifyThreshold;
    public bool IsCurrentSupport;
    public bool FinalIgnoreRequested;
    public bool LegacyIgnoreObserved;
    public bool WasPassingUpLastFixed;

    public float PlatformTopY;
    public float PassThroughThresholdY;
    public float SolidifyThresholdY;
    public float PreviousActorBottomY;
    public float ActorBottomY;
    public float ActorVerticalSpeed;
    public float DropDownUntilTime;

    public int LastUpdatedFrame;
    public float LastUpdatedFixedTime;
    public float LastBecameIgnoredTime;
    public float LastReturnedSolidTime;

    public void BeginFrame()
    {
        WasPassingUpLastFixed = HasReason(OneWayPassThroughReason.UpwardCross)
            || Phase == OneWayPlatformRuntimePhase.PassingUp;

        IsNearby = false;
        HasHorizontalOverlap = false;
        WasBelowTopLastFixed = false;
        CrossedTopPlaneUpThisFixed = false;
        IsBelowPassThroughThreshold = false;
        IsAboveSolidifyThreshold = false;
        IsCurrentSupport = false;
        FinalIgnoreRequested = false;
        LegacyIgnoreObserved = false;
        PreviousActorBottomY = ActorBottomY;
        ActorBottomY = 0f;
        ActorVerticalSpeed = 0f;
        PlatformTopY = 0f;
        PassThroughThresholdY = 0f;
        SolidifyThresholdY = 0f;
        LastUpdatedFrame = Time.frameCount;
        LastUpdatedFixedTime = Time.fixedTime;
        ActiveReasons &= ~(OneWayPassThroughReason.LegacyResolver | OneWayPassThroughReason.UpwardCross | OneWayPassThroughReason.Vault | OneWayPassThroughReason.DropDown | OneWayPassThroughReason.ExternalOverride);
        Phase = OneWayPlatformRuntimePhase.Unknown;
    }

    public void CapturePlatformSnapshot()
    {
        if (Platform == null)
            return;

        PlatformTopY = Platform.TopY;
        PassThroughThresholdY = Platform.PassThroughThresholdY;
        SolidifyThresholdY = Platform.SolidifyThresholdY;
    }

    public void CaptureActorSnapshot(OneWayActorSnapshot snapshot)
    {
        if (!snapshot.IsValid)
            return;

        ActorBottomY = snapshot.BottomY;
        ActorVerticalSpeed = snapshot.Velocity.y;
    }

    public void CapturePreviousActorSnapshot(OneWayActorSnapshot snapshot)
    {
        if (!snapshot.IsValid)
            return;

        PreviousActorBottomY = snapshot.BottomY;
    }

    public void SetReason(OneWayPassThroughReason reason, bool enabled)
    {
        if (enabled)
            ActiveReasons |= reason;
        else
            ActiveReasons &= ~reason;

        FinalIgnoreRequested = ActiveReasons != OneWayPassThroughReason.None;
    }

    public bool HasReason(OneWayPassThroughReason reason)
    {
        return (ActiveReasons & reason) != 0;
    }

    public bool IsDropDownActive(float currentTime)
    {
        return DropDownUntilTime > currentTime;
    }

    public void StartDropDown(float currentTime, float duration)
    {
        DropDownUntilTime = Mathf.Max(DropDownUntilTime, currentTime + Mathf.Max(0f, duration));
        SetReason(OneWayPassThroughReason.DropDown, true);
    }
}

public static class OneWayPlatformUtility
{
    public static OneWayBoxPlatform ResolvePlatform(Collider hit)
    {
        if (hit == null)
            return null;

        OneWayBoxPlatform platform = hit.GetComponent<OneWayBoxPlatform>();
        if (platform != null)
            return platform;

        return hit.GetComponentInParent<OneWayBoxPlatform>();
    }

    public static Color GetPhaseColor(OneWayPlatformRuntimePhase phase)
    {
        switch (phase)
        {
            case OneWayPlatformRuntimePhase.Solid:
                return Color.white;
            case OneWayPlatformRuntimePhase.CandidateBelow:
                return Color.yellow;
            case OneWayPlatformRuntimePhase.PassingUp:
                return Color.red;
            case OneWayPlatformRuntimePhase.Supported:
                return Color.green;
            case OneWayPlatformRuntimePhase.SuppressedByVault:
                return new Color(1f, 0f, 1f, 1f);
            case OneWayPlatformRuntimePhase.SuppressedByDropDown:
                return new Color(1f, 0.5f, 0f, 1f);
            default:
                return new Color(0.6f, 0.6f, 0.6f, 1f);
        }
    }

    public static string GetPhaseLabel(OneWayPlatformRuntimePhase phase)
    {
        switch (phase)
        {
            case OneWayPlatformRuntimePhase.Solid:
                return "Solid";
            case OneWayPlatformRuntimePhase.CandidateBelow:
                return "CandidateBelow";
            case OneWayPlatformRuntimePhase.PassingUp:
                return "PassingUp";
            case OneWayPlatformRuntimePhase.Supported:
                return "Supported";
            case OneWayPlatformRuntimePhase.SuppressedByVault:
                return "Vault";
            case OneWayPlatformRuntimePhase.SuppressedByDropDown:
                return "DropDown";
            default:
                return "Unknown";
        }
    }
}
