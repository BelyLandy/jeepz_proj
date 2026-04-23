using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlasmaAmmoPickup25D : MonoBehaviour
{
    [Header("Payload")]
    [SerializeField, Min(1)] private int totalAmmo = 10;
    [SerializeField, Min(1)] private int shardCount = 5;
    [SerializeField] private PlasmaAmmoShard25D shardPrefab;

    [Header("Spawn")]
    [SerializeField] private Transform shardSpawnRoot;
    [SerializeField, Min(0f)] private float spawnRadius = 0.15f;
    [SerializeField, Min(0f)] private float spawnHeightJitter = 0.08f;

    [Header("Consume")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Collider pickupCollider;

    [Header("Filtering")]
    [Tooltip("Если включено, pickup ищет CharacterPlasmaGlove25D и receive target в родителях/детях иерархии героя.")]
    [SerializeField] private bool searchInParents = true;

    private readonly List<Renderer> cachedRenderers = new List<Renderer>(8);
    private bool isConsumed;

    public int TotalAmmo => totalAmmo;
    public int ShardCount => shardCount;

    private void Reset()
    {
        if (pickupCollider == null)
            pickupCollider = GetComponent<Collider>();
        if (visualRoot == null)
            visualRoot = transform;
    }

    private void Awake()
    {
        if (pickupCollider == null)
            pickupCollider = GetComponent<Collider>();
        if (visualRoot == null)
            visualRoot = transform;

        CacheRenderers();
    }

    private void OnValidate()
    {
        totalAmmo = Mathf.Max(1, totalAmmo);
        shardCount = Mathf.Max(1, shardCount);
        spawnRadius = Mathf.Max(0f, spawnRadius);
        spawnHeightJitter = Mathf.Max(0f, spawnHeightJitter);

        if (pickupCollider == null)
            pickupCollider = GetComponent<Collider>();
        if (visualRoot == null)
            visualRoot = transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isConsumed || other == null)
            return;

        CharacterPlasmaGlove25D plasmaGlove = FindPlasmaGlove(other);
        if (plasmaGlove == null)
            return;
        if (plasmaGlove.IsAmmoFullNow)
            return;
        if (shardPrefab == null)
            return;

        Transform receiveTarget = FindReceiveTarget(other, plasmaGlove);
        if (receiveTarget == null)
            receiveTarget = plasmaGlove.transform;

        int actualShardCount = Mathf.Min(Mathf.Max(1, shardCount), Mathf.Max(1, totalAmmo));
        if (actualShardCount <= 0)
            return;

        ConsumePickup(plasmaGlove, receiveTarget, actualShardCount);
    }

    private void ConsumePickup(CharacterPlasmaGlove25D plasmaGlove, Transform receiveTarget, int actualShardCount)
    {
        if (isConsumed)
            return;

        isConsumed = true;

        if (pickupCollider != null)
            pickupCollider.enabled = false;

        HideVisuals();
        SpawnShards(plasmaGlove, receiveTarget, actualShardCount);
        Destroy(gameObject);
    }

    private void SpawnShards(CharacterPlasmaGlove25D plasmaGlove, Transform receiveTarget, int actualShardCount)
    {
        if (plasmaGlove == null || receiveTarget == null || shardPrefab == null)
            return;

        int baseAmount = totalAmmo / actualShardCount;
        int remainder = totalAmmo % actualShardCount;
        Vector3 spawnCenter = shardSpawnRoot != null ? shardSpawnRoot.position : transform.position;

        for (int i = 0; i < actualShardCount; i++)
        {
            int ammoForShard = baseAmount + (i < remainder ? 1 : 0);
            if (ammoForShard <= 0)
                continue;

            Vector3 planarRandom = Random.insideUnitSphere;
            planarRandom.z = 0f;
            if (planarRandom.sqrMagnitude <= 0.0001f)
                planarRandom = Vector3.right;
            else
                planarRandom.Normalize();

            Vector3 spawnPosition = spawnCenter + planarRandom * spawnRadius;
            spawnPosition.y += Random.Range(0f, spawnHeightJitter);
            spawnPosition.z = spawnCenter.z;

            PlasmaAmmoShard25D shardInstance = Instantiate(shardPrefab, spawnPosition, Quaternion.identity);
            Vector3 scatterDirection = (planarRandom + Vector3.up * Random.Range(0.35f, 1f)).normalized;
            shardInstance.Initialize(plasmaGlove, receiveTarget, ammoForShard, scatterDirection);
        }
    }

    private CharacterPlasmaGlove25D FindPlasmaGlove(Collider other)
    {
        CharacterPlasmaGlove25D plasmaGlove = other.GetComponent<CharacterPlasmaGlove25D>();
        if (plasmaGlove != null)
            return plasmaGlove;

        if (searchInParents)
        {
            plasmaGlove = other.GetComponentInParent<CharacterPlasmaGlove25D>();
            if (plasmaGlove != null)
                return plasmaGlove;
        }

        return null;
    }

    private Transform FindReceiveTarget(Collider other, CharacterPlasmaGlove25D plasmaGlove)
    {
        PlasmaAmmoReceiveTarget25D receiveTarget = other.GetComponent<PlasmaAmmoReceiveTarget25D>();
        if (receiveTarget != null)
            return receiveTarget.transform;

        if (searchInParents)
        {
            receiveTarget = other.GetComponentInParent<PlasmaAmmoReceiveTarget25D>();
            if (receiveTarget != null)
                return receiveTarget.transform;
        }

        if (plasmaGlove != null)
        {
            receiveTarget = plasmaGlove.GetComponentInChildren<PlasmaAmmoReceiveTarget25D>(true);
            if (receiveTarget != null)
                return receiveTarget.transform;
        }

        return null;
    }

    private void CacheRenderers()
    {
        cachedRenderers.Clear();
        if (visualRoot == null)
            return;

        visualRoot.GetComponentsInChildren(true, cachedRenderers);
    }

    private void HideVisuals()
    {
        if (visualRoot == null)
            return;

        if (cachedRenderers.Count == 0)
            CacheRenderers();

        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = false;
        }
    }
}
