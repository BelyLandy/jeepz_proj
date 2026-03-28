using UnityEngine;

public class RotationAnim : MonoBehaviour
{
    [SerializeField] private RBCharacter25DPlayerInput playerInput;

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
    }

    private void OnValidate()
    {
        if (playerInput == null)
            playerInput = GetComponent<RBCharacter25DPlayerInput>();
    }

    private void Update()
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

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            desiredRotation,
            turnSpeed * Time.deltaTime
        );
    }
}
