using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class EnemyHeroSlowZone25D : MonoBehaviour
{
    [Header("Slow")]
    [SerializeField, Range(0.05f, 1f)] private float movementSpeedMultiplier = 0.6f;
    [SerializeField] private bool affectOnlyPlayer = true;

    [Header("Target Filtering")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool requireRBCharacter25D = true;

    [Header("Debug")]
    [SerializeField] private bool logSlowZoneEvents = false;

    private readonly Dictionary<RBCharacter25D, int> overlapCounts = new Dictionary<RBCharacter25D, int>();

    private void Reset()
    {
        EnsureTriggerCollider();
        ClampSettings();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        ClampSettings();
    }

    private void OnValidate()
    {
        ClampSettings();
        EnsureTriggerCollider();
    }

    private void OnDisable()
    {
        RemoveSlowFromAllCharacters("ZoneDisabled");
    }

    private void OnDestroy()
    {
        RemoveSlowFromAllCharacters("ZoneDestroyed");
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRegisterSlow(other, true, "Enter");
    }

    private void OnTriggerStay(Collider other)
    {
        TryRegisterSlow(other, false, "Stay");
    }

    private void OnTriggerExit(Collider other)
    {
        TryUnregisterSlow(other, "Exit");
    }

    private void TryRegisterSlow(Collider other, bool incrementOverlapCount, string eventName)
    {
        if (other == null)
            return;

        RBCharacter25D character = other.GetComponentInParent<RBCharacter25D>();

        if (requireRBCharacter25D && character == null)
            return;

        if (character == null)
            return;

        if (affectOnlyPlayer && !IsPlayerCollider(other, character))
            return;

        if (!overlapCounts.ContainsKey(character))
            overlapCounts.Add(character, 0);

        if (incrementOverlapCount)
            overlapCounts[character] = Mathf.Max(0, overlapCounts[character]) + 1;
        else if (overlapCounts[character] <= 0)
            overlapCounts[character] = 1;

        character.AddExternalMoveSpeedMultiplier(this, movementSpeedMultiplier, "EnemyHeroSlowZone");

        if (logSlowZoneEvents && eventName != "Stay")
        {
            Debug.Log(
                $"[EnemyHeroSlowZone25D] Slow {eventName}\n" +
                $"Zone: {name}\n" +
                $"Character: {character.name}\n" +
                $"Multiplier: {movementSpeedMultiplier:0.00}\n" +
                $"OverlapCount: {overlapCounts[character]}",
                this);
        }
    }

    private void TryUnregisterSlow(Collider other, string eventName)
    {
        if (other == null)
            return;

        RBCharacter25D character = other.GetComponentInParent<RBCharacter25D>();
        if (character == null)
            return;

        if (!overlapCounts.TryGetValue(character, out int count))
            return;

        count = Mathf.Max(0, count - 1);

        if (count > 0)
        {
            overlapCounts[character] = count;
            return;
        }

        overlapCounts.Remove(character);
        character.RemoveExternalMoveSpeedMultiplier(this);

        if (logSlowZoneEvents)
        {
            Debug.Log(
                $"[EnemyHeroSlowZone25D] Slow {eventName}\n" +
                $"Zone: {name}\n" +
                $"Character: {character.name}\n" +
                $"MultiplierRemoved: {movementSpeedMultiplier:0.00}",
                this);
        }
    }

    private bool IsPlayerCollider(Collider other, RBCharacter25D character)
    {
        if (string.IsNullOrWhiteSpace(playerTag))
            return true;

        if (other.CompareTag(playerTag))
            return true;

        if (character != null && character.CompareTag(playerTag))
            return true;

        Transform root = character != null ? character.transform.root : other.transform.root;
        return root != null && root.CompareTag(playerTag);
    }

    private void RemoveSlowFromAllCharacters(string reason)
    {
        if (overlapCounts.Count <= 0)
            return;

        List<RBCharacter25D> characters = new List<RBCharacter25D>(overlapCounts.Keys);
        overlapCounts.Clear();

        for (int i = 0; i < characters.Count; i++)
        {
            RBCharacter25D character = characters[i];
            if (character == null)
                continue;

            character.RemoveExternalMoveSpeedMultiplier(this);

            if (logSlowZoneEvents)
            {
                Debug.Log(
                    $"[EnemyHeroSlowZone25D] Slow removed from all\n" +
                    $"Zone: {name}\n" +
                    $"Character: {character.name}\n" +
                    $"Reason: {reason}",
                    this);
            }
        }
    }

    private void EnsureTriggerCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void ClampSettings()
    {
        movementSpeedMultiplier = Mathf.Clamp(movementSpeedMultiplier, 0.05f, 1f);
    }
}
