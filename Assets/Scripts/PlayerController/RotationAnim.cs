using UnityEngine;

public class RotationAnim : MonoBehaviour
{
    [SerializeField] private RBCharacter25DPlayerInput playerInput;
    [SerializeField] private RBCharacter25D character;
    [SerializeField] private float lockStanceRotationFinishEpsilon = 0.25f;

    public float leftYaw = 135f;
    public float rightYaw = 45f;
    public float turnSpeed = 540f;

    private float inputX;
    private Quaternion desiredRotation;

    public int FacingSign { get; private set; } = +1;

    public float InputX => inputX;
    public bool HasMoveInput { get; private set; }

    private void Awake()
    {
        desiredRotation = transform.rotation;

        if (playerInput == null)
            playerInput = GetComponent<RBCharacter25DPlayerInput>();

        if (character == null)
            character = GetComponent<RBCharacter25D>();
    }

    private void OnValidate()
    {
        if (playerInput == null)
            playerInput = GetComponent<RBCharacter25DPlayerInput>();

        if (character == null)
            character = GetComponent<RBCharacter25D>();
    }

    private void Update()
    {
        bool rotationBlockedByLockStance =
            character != null && character.IsLockStanceMovementActive;

        if (!rotationBlockedByLockStance)
        {
            inputX = playerInput != null ? playerInput.CurrentMoveX : 0f;
            HasMoveInput = Mathf.Abs(inputX) > 0.01f;

            if (inputX < -0.01f)
            {
                FacingSign = -1;
                desiredRotation = Quaternion.Euler(0f, leftYaw, 0f);
            }
            else if (inputX > 0.01f)
            {
                FacingSign = +1;
                desiredRotation = Quaternion.Euler(0f, rightYaw, 0f);
            }
        }
        else
        {
            inputX = 0f;
            HasMoveInput = false;
        }

        float angleToTarget = Quaternion.Angle(transform.rotation, desiredRotation);

        if (rotationBlockedByLockStance && angleToTarget <= lockStanceRotationFinishEpsilon)
            return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            desiredRotation,
            turnSpeed * Time.deltaTime
        );
    }
}
