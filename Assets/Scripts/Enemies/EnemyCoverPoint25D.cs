using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyCoverPoint25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform standPoint;
    [SerializeField] private Transform peekPoint;

    [Header("Scoring")]
    [SerializeField] private float coverScoreBias = 0f;
    [SerializeField, Min(0f)] private float maxUseDistance = 12f;
    [SerializeField] private bool requireBlockingLineOfSight = true;

    private Transform occupiedBy;

    public Transform StandPoint => standPoint != null ? standPoint : transform;
    public Transform PeekPoint => peekPoint != null ? peekPoint : StandPoint;
    public float CoverScoreBias => coverScoreBias;
    public float MaxUseDistance => maxUseDistance;
    public bool RequireBlockingLineOfSight => requireBlockingLineOfSight;
    public bool IsOccupied => occupiedBy != null;
    public Transform OccupiedBy => occupiedBy;

    private void Reset()
    {
        if (standPoint == null)
            standPoint = transform;
    }

    private void Awake()
    {
        if (standPoint == null)
            standPoint = transform;
    }

    public bool CanBeUsedBy(Transform user)
    {
        return !IsOccupied || occupiedBy == user;
    }

    public bool TryClaim(Transform user)
    {
        if (user == null)
            return false;

        if (!CanBeUsedBy(user))
            return false;

        occupiedBy = user;
        return true;
    }

    public void Release(Transform user)
    {
        if (occupiedBy == null)
            return;

        if (user == null || occupiedBy == user)
            occupiedBy = null;
    }
}
