using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleCharacterMotor : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 2.5f;
    public float rotationSpeed = 180f;
    public float gravity = -20f;
    public float jumpHeight = 1.1f;

    [Header("Optional References")]
    public Animator animator;
    public IKFootPlacement footPlacement;

    private CharacterController controller;
    private float verticalVelocity;
    private bool jumpRequested;
    private int speedHash;
    private int groundedHash;
    private bool hasSpeedParameter;
    private bool hasGroundedParameter;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (footPlacement == null)
            footPlacement = GetComponent<IKFootPlacement>();

        speedHash = Animator.StringToHash("Speed");
        groundedHash = Animator.StringToHash("Grounded");

        if (animator != null)
        {
            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == speedHash && parameter.type == AnimatorControllerParameterType.Float)
                    hasSpeedParameter = true;
                else if (parameter.nameHash == groundedHash && parameter.type == AnimatorControllerParameterType.Bool)
                    hasGroundedParameter = true;
            }
        }
    }

    private void Update()
    {
        float moveInput = Input.GetAxisRaw("Vertical");
        float turnInput = Input.GetAxisRaw("Horizontal");

        transform.Rotate(Vector3.up, turnInput * rotationSpeed * Time.deltaTime);

        bool wasGrounded = controller.isGrounded;
        if (wasGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (Input.GetKeyDown(KeyCode.Space) && wasGrounded)
            jumpRequested = true;

        if (jumpRequested)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpRequested = false;
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 move = transform.forward * moveInput * moveSpeed;
        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);

        bool isGroundedNow = controller.isGrounded;
        bool isMovingNow = Mathf.Abs(moveInput) > 0.01f || Mathf.Abs(turnInput) > 0.01f;

        if (footPlacement != null)
        {
            if (!isGroundedNow && wasGrounded)
                footPlacement.jumped = true;

            footPlacement.isMoving = isMovingNow;
            footPlacement.isGrounded = isGroundedNow;
        }

        if (animator != null)
        {
            if (hasSpeedParameter)
                animator.SetFloat(speedHash, Mathf.Abs(moveInput));
            if (hasGroundedParameter)
                animator.SetBool(groundedHash, isGroundedNow);
        }
    }
}
