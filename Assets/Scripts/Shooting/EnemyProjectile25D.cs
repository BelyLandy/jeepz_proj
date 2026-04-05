using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class EnemyProjectile25D : MonoBehaviour
{
    [Header("Motion")]
    [SerializeField] private float speed = 12f;
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private bool alignRotationToDirection = true;

    [Header("World Z Lock")]
    [SerializeField] private bool lockWorldZ = true;
    [SerializeField] private float worldZ = 0f;

    [Header("Hit Behaviour")]
    [SerializeField] private bool destroyOnHeroHit = true;
    [SerializeField] private bool destroyOnWorldHit = true;

    private const float Epsilon = 0.0001f;

    private Vector3 direction = Vector3.right;
    private float destroyAtTime;
    private bool isLaunched;
    private bool isConsumed;

    public Vector3 Direction => direction;
    public float Speed => speed;

    private void OnEnable()
    {
        destroyAtTime = Time.time + lifeTime;
        isConsumed = false;
    }

    private void Start()
    {
        if (!isLaunched)
            Launch(transform.right);
    }

    private void Update()
    {
        if (isConsumed)
            return;

        transform.position += direction * speed * Time.deltaTime;

        if (lockWorldZ)
        {
            Vector3 position = transform.position;
            position.z = worldZ;
            transform.position = position;
        }

        if (Time.time >= destroyAtTime)
            Destroy(gameObject);
    }

    public void Launch(Vector3 launchDirection, float speedOverride = -1f)
    {
        if (launchDirection.sqrMagnitude <= Epsilon)
            launchDirection = Vector3.right;

        direction = launchDirection.normalized;
        direction.z = 0f;
        if (direction.sqrMagnitude <= Epsilon)
            direction = Vector3.right;
        else
            direction.Normalize();

        if (speedOverride > 0f)
            speed = speedOverride;

        isLaunched = true;
        destroyAtTime = Time.time + lifeTime;
        isConsumed = false;

        if (alignRotationToDirection)
        {
            float angleZ = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angleZ);
        }

        if (lockWorldZ)
        {
            Vector3 position = transform.position;
            position.z = worldZ;
            transform.position = position;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.collider);
    }

    private void HandleHit(Collider other)
    {
        if (isConsumed || other == null)
            return;

        HeroHurtbox25D hurtbox = other.GetComponent<HeroHurtbox25D>();
        if (hurtbox == null)
            hurtbox = other.GetComponentInParent<HeroHurtbox25D>();

        if (hurtbox != null)
        {
            hurtbox.ReceiveProjectileHit(this);

            if (destroyOnHeroHit)
                ConsumeAndDestroy();

            return;
        }

        if (destroyOnWorldHit)
            ConsumeAndDestroy();
    }

    private void ConsumeAndDestroy()
    {
        if (isConsumed)
            return;

        isConsumed = true;
        Destroy(gameObject);
    }
}
