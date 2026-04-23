using UnityEngine;

[DisallowMultipleComponent]
public sealed class MenuTimedEffect : MonoBehaviour
{
    [Header("Lifetime")]
    [SerializeField, Min(0f)] private float lifeTime = 0.25f;

    [Header("Burst On Enable")]
    [SerializeField] private bool spawnBurstOnEnable = true;
    [SerializeField] private GameObject burstPrefab;
    [SerializeField] private Transform burstSpawnPoint;
    [SerializeField] private Transform burstParent;
    [SerializeField] private bool burstUseOwnerRotation = true;
    [SerializeField] private Vector3 burstPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 burstRotationOffsetEuler = Vector3.zero;

    private void OnEnable()
    {
        SpawnBurstIfNeeded();

        if (lifeTime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Destroy(gameObject, lifeTime);
    }

    private void SpawnBurstIfNeeded()
    {
        if (!spawnBurstOnEnable || burstPrefab == null)
            return;

        Transform source = burstSpawnPoint != null ? burstSpawnPoint : transform;
        Quaternion sourceRotation = source.rotation;
        Vector3 spawnPosition = source.position + sourceRotation * burstPositionOffset;

        Quaternion baseRotation = burstUseOwnerRotation ? sourceRotation : Quaternion.identity;
        Quaternion spawnRotation = baseRotation * Quaternion.Euler(burstRotationOffsetEuler);

        if (burstParent != null)
            Instantiate(burstPrefab, spawnPosition, spawnRotation, burstParent);
        else
            Instantiate(burstPrefab, spawnPosition, spawnRotation);
    }

    private void OnValidate()
    {
        lifeTime = Mathf.Max(0f, lifeTime);
    }
}
