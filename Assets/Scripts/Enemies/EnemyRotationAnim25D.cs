using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyRotationAnim25D : MonoBehaviour
{
    [SerializeField] private EnemyCharacter25D character;
    [SerializeField, Min(0f)] private float turnSpeed = 540f;
    [SerializeField] private float leftYaw = 135f;
    [SerializeField] private float rightYaw = 45f;

    private Quaternion desiredRotation;

    private void Reset()
    {
        if (character == null)
            character = GetComponentInParent<EnemyCharacter25D>();

        desiredRotation = transform.rotation;
    }

    private void Awake()
    {
        if (character == null)
            character = GetComponentInParent<EnemyCharacter25D>();

        desiredRotation = transform.rotation;
    }

    private void OnValidate()
    {
        if (character == null)
            character = GetComponentInParent<EnemyCharacter25D>();

        turnSpeed = Mathf.Max(0f, turnSpeed);
    }

    private void Update()
    {
        int facingSign = character != null ? character.FacingSign : 1;
        desiredRotation = Quaternion.Euler(0f, facingSign >= 0 ? rightYaw : leftYaw, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, turnSpeed * Time.deltaTime);
    }
}
