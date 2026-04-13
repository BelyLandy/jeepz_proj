using UnityEngine;

public class RotationAnim : MonoBehaviour
{
    [SerializeField] private RBCharacter25DPlayerInput playerInput;
    [SerializeField] private RBCharacter25D character;
    [SerializeField] private CharacterFacingResolver25D facingResolver;
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

        if (facingResolver == null)
            facingResolver = GetComponent<CharacterFacingResolver25D>();
    }

    private void OnValidate()
    {
        if (playerInput == null)
            playerInput = GetComponent<RBCharacter25DPlayerInput>();

        if (character == null)
            character = GetComponent<RBCharacter25D>();

        if (facingResolver == null)
            facingResolver = GetComponent<CharacterFacingResolver25D>();
    }

    private void Update()
    {
        if (facingResolver == null)
        {
            facingResolver = character != null ? character.FacingResolverComponent : null;
            if (facingResolver == null)
                facingResolver = GetComponent<CharacterFacingResolver25D>();
        }

        inputX = playerInput != null ? playerInput.CurrentMoveX : 0f;
        HasMoveInput = Mathf.Abs(inputX) > 0.01f;

        bool rotationBlockedByLockStance = character != null && character.IsLockStanceMovementActive;
        bool hasResolvedFacing = facingResolver != null;

        if (hasResolvedFacing)
        {
            ApplyFacingSign(facingResolver.ResolvedFacingSign);
        }
        else if (!rotationBlockedByLockStance)
        {
            if (inputX < -0.01f)
                ApplyFacingSign(-1);
            else if (inputX > 0.01f)
                ApplyFacingSign(+1);
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

    private void ApplyFacingSign(int sign)
    {
        FacingSign = sign >= 0 ? +1 : -1;
        desiredRotation = Quaternion.Euler(0f, FacingSign >= 0 ? rightYaw : leftYaw, 0f);
    }

}
