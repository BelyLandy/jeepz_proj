using UnityEngine;

public sealed class StraightProjectile : MonoBehaviour
{
    [Header("Motion")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private bool alignRotationToDirection = true;

    [Header("World Z Lock")]
    [SerializeField] private bool lockWorldZ = true;
    [SerializeField] private float worldZ = 0f;

    private Vector3 moveDirection = Vector3.right;
    private bool isInitialized;

    private void OnEnable()
    {
        Destroy(gameObject, lifeTime);
    }

    private void Start()
    {
        if (!isInitialized)
            Launch(transform.right);
    }

    private void Update()
    {
        transform.position += moveDirection * speed * Time.deltaTime;

        if (lockWorldZ)
        {
            Vector3 position = transform.position;
            position.z = worldZ;
            transform.position = position;
        }
    }

    public void Launch(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.right;

        moveDirection = direction.normalized;
        moveDirection.z = 0f;
        if (moveDirection.sqrMagnitude > 0.0001f)
            moveDirection.Normalize();
        else
            moveDirection = Vector3.right;

        isInitialized = true;

        if (alignRotationToDirection)
        {
            float angleZ = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angleZ);
        }

        if (lockWorldZ)
        {
            Vector3 position = transform.position;
            position.z = worldZ;
            transform.position = position;
        }
    }
}
