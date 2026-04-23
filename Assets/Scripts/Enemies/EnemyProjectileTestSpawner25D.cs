using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyProjectileTestSpawner25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyProjectile25D projectilePrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform projectileParent;

    [Header("Spawn Timing")]
    [SerializeField] private float spawnInterval = 1f;
    [SerializeField] private bool spawnOnEnable;

    [Header("Projectile Motion")]
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private bool useSpawnPointRightAsDirection = true;
    [SerializeField] private Vector2 spawnDirection = Vector2.right;

    private float nextSpawnTime;

    private void OnValidate()
    {
        spawnInterval = Mathf.Max(0.01f, spawnInterval);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
    }

    private void OnEnable()
    {
        nextSpawnTime = Time.time + spawnInterval;

        if (spawnOnEnable)
            SpawnProjectile();
    }

    private void Update()
    {
        if (projectilePrefab == null)
            return;

        if (Time.time < nextSpawnTime)
            return;

        SpawnProjectile();
        nextSpawnTime = Time.time + spawnInterval;
    }

    private void SpawnProjectile()
    {
        Transform source = spawnPoint != null ? spawnPoint : transform;
        Vector3 direction = ResolveSpawnDirection(source);

        EnemyProjectile25D projectile = Instantiate(projectilePrefab, source.position, Quaternion.identity, projectileParent);
        projectile.Launch(direction, Mathf.Max(0.01f, projectileSpeed));
    }

    private Vector3 ResolveSpawnDirection(Transform source)
    {
        Vector3 direction;

        if (useSpawnPointRightAsDirection)
        {
            direction = source.right;
        }
        else
        {
            direction = new Vector3(spawnDirection.x, spawnDirection.y, 0f);
        }

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.right;

        direction.z = 0f;
        return direction.normalized;
    }
}
