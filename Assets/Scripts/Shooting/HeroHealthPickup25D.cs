using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroHealthPickup25D : MonoBehaviour
{
    [Header("Payload")]
    [SerializeField, Min(1)] private int totalHeal = 25;
    [SerializeField, Min(1)] private int shardCount = 5;
    [SerializeField] private HeroHealthShard25D shardPrefab;

    [Header("Spawn")]
    [SerializeField] private Transform shardSpawnRoot;
    [SerializeField, Min(0f)] private float spawnRadius = 0.15f;
    [SerializeField, Min(0f)] private float spawnHeightJitter = 0.08f;

    [Header("Consume")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Collider pickupCollider;

    [Header("Filtering")]
    [Tooltip("Если включено, pickup ищет HeroHealth25D и receive target в родителях/детях иерархии героя.")]
    [SerializeField] private bool searchInParents = true;

    private readonly List<Renderer> cachedRenderers = new List<Renderer>(8);
    private bool isConsumed;

    public int TotalHeal => totalHeal;
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
        totalHeal = Mathf.Max(1, totalHeal);
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

        HeroHealth25D heroHealth = FindHeroHealth(other);
        if (heroHealth == null)
            return;
        if (!heroHealth.IsAlive)
            return;
        if (heroHealth.CurrentHealth >= heroHealth.MaxHealth)
            return;
        if (shardPrefab == null)
            return;

        Transform receiveTarget = FindReceiveTarget(other, heroHealth);
        if (receiveTarget == null)
            receiveTarget = heroHealth.transform;

        int actualShardCount = Mathf.Min(Mathf.Max(1, shardCount), Mathf.Max(1, totalHeal));
        if (actualShardCount <= 0)
            return;

        ConsumePickup(heroHealth, receiveTarget, actualShardCount);
    }

    private void ConsumePickup(HeroHealth25D heroHealth, Transform receiveTarget, int actualShardCount)
    {
        if (isConsumed)
            return;

        isConsumed = true;

        if (pickupCollider != null)
            pickupCollider.enabled = false;

        HideVisuals();
        SpawnShards(heroHealth, receiveTarget, actualShardCount);
        Destroy(gameObject);
    }

    private void SpawnShards(HeroHealth25D heroHealth, Transform receiveTarget, int actualShardCount)
    {
        if (heroHealth == null || receiveTarget == null || shardPrefab == null)
            return;

        int baseAmount = totalHeal / actualShardCount;
        int remainder = totalHeal % actualShardCount;
        Vector3 spawnCenter = shardSpawnRoot != null ? shardSpawnRoot.position : transform.position;

        for (int i = 0; i < actualShardCount; i++)
        {
            int healForShard = baseAmount + (i < remainder ? 1 : 0);
            if (healForShard <= 0)
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

            HeroHealthShard25D shardInstance = Instantiate(shardPrefab, spawnPosition, Quaternion.identity);
            Vector3 scatterDirection = (planarRandom + Vector3.up * Random.Range(0.35f, 1f)).normalized;
            shardInstance.Initialize(heroHealth, receiveTarget, healForShard, scatterDirection);
        }
    }

    private HeroHealth25D FindHeroHealth(Collider other)
    {
        HeroHealth25D heroHealth = other.GetComponent<HeroHealth25D>();
        if (heroHealth != null)
            return heroHealth;

        if (searchInParents)
        {
            heroHealth = other.GetComponentInParent<HeroHealth25D>();
            if (heroHealth != null)
                return heroHealth;
        }

        return null;
    }

    private Transform FindReceiveTarget(Collider other, HeroHealth25D heroHealth)
    {
        HeroHealthReceiveTarget25D receiveTarget = other.GetComponent<HeroHealthReceiveTarget25D>();
        if (receiveTarget != null)
            return receiveTarget.transform;

        if (searchInParents)
        {
            receiveTarget = other.GetComponentInParent<HeroHealthReceiveTarget25D>();
            if (receiveTarget != null)
                return receiveTarget.transform;
        }

        if (heroHealth != null)
        {
            receiveTarget = heroHealth.GetComponentInChildren<HeroHealthReceiveTarget25D>(true);
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
