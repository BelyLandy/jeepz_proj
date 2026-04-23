using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyPatrolPoint25D : MonoBehaviour
{
    [Header("Point")]
    [SerializeField] private Transform point;
    [SerializeField, Min(0f)] private float waitDuration = 0.5f;
    [SerializeField] private int facingOverride = 0;
    [SerializeField] private bool stopAndLook = true;
    [SerializeField] private bool enabledForStartPatrol = true;

    [Header("Traversal")]
    [SerializeField] private EnemyJumpLink25D preferredJumpLink;
    [SerializeField] private EnemyPatrolPoint25D[] explicitNextPoints;

    public Transform Point => point != null ? point : transform;
    public float WaitDuration => waitDuration;
    public int FacingOverride => facingOverride < 0 ? -1 : (facingOverride > 0 ? 1 : 0);
    public bool StopAndLook => stopAndLook;
    public bool EnabledForStartPatrol => enabledForStartPatrol;
    public EnemyJumpLink25D PreferredJumpLink => preferredJumpLink;
    public EnemyPatrolPoint25D[] ExplicitNextPoints => explicitNextPoints;

    private void Reset()
    {
        if (point == null)
            point = transform;
    }

    private void Awake()
    {
        if (point == null)
            point = transform;
    }
}
