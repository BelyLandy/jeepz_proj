using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CapsuleCollider))]
public sealed class CollisionIgnoreCoordinator : MonoBehaviour
{
    [Serializable]
    public sealed class CollisionIgnoreEntry
    {
        public Collider Target;
        public OneWayPassThroughReason ActiveReasons;
        public bool FinalIgnoreApplied;
        public int LastUpdatedFrame;
        public float LastChangedTime;
    }

    [Header("Lifecycle")]
    [Tooltip("Разрешает coordinator-у реально применять Physics.IgnoreCollision. Если выключено, coordinator работает как теневой лог состояний.")]
    [SerializeField] private bool applyPhysicsIgnores = true;

    private CapsuleCollider ownerCapsule;
    private readonly Dictionary<Collider, CollisionIgnoreEntry> entries = new Dictionary<Collider, CollisionIgnoreEntry>(16);
    private readonly List<Collider> cleanupBuffer = new List<Collider>(16);

    public bool ApplyPhysicsIgnores => applyPhysicsIgnores;
    public int EntryCount => entries.Count;
    public CapsuleCollider OwnerCapsule => ownerCapsule != null ? ownerCapsule : (ownerCapsule = GetComponent<CapsuleCollider>());

    private void Awake()
    {
        CacheComponents();
    }

    private void OnValidate()
    {
        CacheComponents();
    }

    private void OnDisable()
    {
        RestoreAll();
    }

    private void OnDestroy()
    {
        RestoreAll();
    }

    public void SetReason(Collider target, OneWayPassThroughReason reason, bool enabled)
    {
        if (target == null || reason == OneWayPassThroughReason.None)
            return;

        if (!EnsureRuntimeState())
            return;

        CleanupNullTargets();

        CollisionIgnoreEntry entry = GetOrCreateEntry(target);
        bool wasIgnored = entry.FinalIgnoreApplied;

        if (enabled)
            entry.ActiveReasons |= reason;
        else
            entry.ActiveReasons &= ~reason;

        bool shouldIgnore = entry.ActiveReasons != OneWayPassThroughReason.None;
        ApplyIgnoreState(entry, shouldIgnore);

        if (!shouldIgnore)
            entries.Remove(target);
    }

    public void ClearReasonEverywhere(OneWayPassThroughReason reason)
    {
        if (reason == OneWayPassThroughReason.None)
            return;

        CleanupNullTargets();
        cleanupBuffer.Clear();

        foreach (KeyValuePair<Collider, CollisionIgnoreEntry> pair in entries)
        {
            CollisionIgnoreEntry entry = pair.Value;
            if (entry == null)
                continue;

            entry.ActiveReasons &= ~reason;
            bool shouldIgnore = entry.ActiveReasons != OneWayPassThroughReason.None;
            ApplyIgnoreState(entry, shouldIgnore);
            if (!shouldIgnore)
                cleanupBuffer.Add(pair.Key);
        }

        for (int i = 0; i < cleanupBuffer.Count; i++)
            entries.Remove(cleanupBuffer[i]);

        cleanupBuffer.Clear();
    }

    public void ClearTarget(Collider target)
    {
        if (target == null)
            return;

        if (!entries.TryGetValue(target, out CollisionIgnoreEntry entry) || entry == null)
            return;

        entry.ActiveReasons = OneWayPassThroughReason.None;
        ApplyIgnoreState(entry, false);
        entries.Remove(target);
    }

    public void RestoreAll()
    {
        CleanupNullTargets();
        cleanupBuffer.Clear();
        cleanupBuffer.AddRange(entries.Keys);

        for (int i = 0; i < cleanupBuffer.Count; i++)
        {
            Collider target = cleanupBuffer[i];
            if (target == null)
                continue;

            if (entries.TryGetValue(target, out CollisionIgnoreEntry entry) && entry != null)
            {
                entry.ActiveReasons = OneWayPassThroughReason.None;
                ApplyIgnoreState(entry, false);
            }
        }

        entries.Clear();
        cleanupBuffer.Clear();
    }

    public bool IsIgnored(Collider target)
    {
        return TryGetActiveReasons(target, out OneWayPassThroughReason reasons) && reasons != OneWayPassThroughReason.None;
    }

    public bool HasReason(Collider target, OneWayPassThroughReason reason)
    {
        return TryGetActiveReasons(target, out OneWayPassThroughReason reasons) && (reasons & reason) != 0;
    }

    public bool TryGetActiveReasons(Collider target, out OneWayPassThroughReason reasons)
    {
        reasons = OneWayPassThroughReason.None;
        if (target == null)
            return false;

        CleanupNullTargets();

        if (!entries.TryGetValue(target, out CollisionIgnoreEntry entry) || entry == null)
            return false;

        reasons = entry.ActiveReasons;
        return reasons != OneWayPassThroughReason.None;
    }

    public void GetEntries(List<CollisionIgnoreEntry> results)
    {
        if (results == null)
            return;

        CleanupNullTargets();
        results.Clear();
        foreach (CollisionIgnoreEntry entry in entries.Values)
        {
            if (entry != null)
                results.Add(entry);
        }
    }

    private void CacheComponents()
    {
        if (ownerCapsule == null)
            ownerCapsule = GetComponent<CapsuleCollider>();
    }

    private bool EnsureRuntimeState()
    {
        CacheComponents();
        return ownerCapsule != null;
    }

    private CollisionIgnoreEntry GetOrCreateEntry(Collider target)
    {
        if (entries.TryGetValue(target, out CollisionIgnoreEntry entry) && entry != null)
            return entry;

        entry = new CollisionIgnoreEntry
        {
            Target = target,
            ActiveReasons = OneWayPassThroughReason.None,
            FinalIgnoreApplied = false,
            LastUpdatedFrame = Time.frameCount,
            LastChangedTime = Time.time,
        };

        entries[target] = entry;
        return entry;
    }

    private void ApplyIgnoreState(CollisionIgnoreEntry entry, bool shouldIgnore)
    {
        if (entry == null)
            return;

        if (entry.Target == null)
        {
            entry.ActiveReasons = OneWayPassThroughReason.None;
            entry.FinalIgnoreApplied = false;
            return;
        }

        if (applyPhysicsIgnores && ownerCapsule != null)
            Physics.IgnoreCollision(ownerCapsule, entry.Target, shouldIgnore);

        entry.FinalIgnoreApplied = shouldIgnore;
        entry.LastUpdatedFrame = Time.frameCount;
        entry.LastChangedTime = Time.time;
    }

    private void CleanupNullTargets()
    {
        cleanupBuffer.Clear();
        foreach (KeyValuePair<Collider, CollisionIgnoreEntry> pair in entries)
        {
            if (pair.Key == null || pair.Value == null || pair.Value.Target == null)
                cleanupBuffer.Add(pair.Key);
        }

        for (int i = 0; i < cleanupBuffer.Count; i++)
            entries.Remove(cleanupBuffer[i]);

        cleanupBuffer.Clear();
    }
}
