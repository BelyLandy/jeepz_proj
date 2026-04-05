using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroKnockbackReceiver25D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RBCharacter25D character;
    [SerializeField] private Rigidbody heroRb;
    [SerializeField] private PlayerControlLock25D controlLock;
    [SerializeField] private CharacterCrouch25D crouch;

    [Header("Movement Detection")]
    [SerializeField] private float movingThreshold = 0.25f;

    [Header("Ground Knockback")]
    [SerializeField] private float horizontalKnockbackX = 5f;

    [Header("Diagonal Knockback")]
    [SerializeField] private float diagonalKnockbackX = 4f;
    [SerializeField] private float diagonalKnockbackY = 4f;
    [SerializeField, Range(0f, 1f)] private float movingDiagonalChance = 0.5f;

    [Header("Horizontal Control Lock")]
    [SerializeField] private bool useHorizontalControlLock = true;
    [SerializeField] private float horizontalControlLockTime = 0.1f;

    [Header("Diagonal Control Lock")]
    [SerializeField] private bool useDiagonalControlLock = true;
    [SerializeField] private float diagonalControlLockTime = 0.1f;

    private const float Epsilon = 0.0001f;

    public RBCharacter25D Character => character;
    public Rigidbody HeroRigidbody => heroRb;

    private void Reset()
    {
        if (character == null)
            character = GetComponent<RBCharacter25D>();

        if (heroRb == null && character != null)
            heroRb = character.RigidbodyComponent;
        else if (heroRb == null)
            heroRb = GetComponent<Rigidbody>();

        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();
        if (crouch == null)
            crouch = GetComponent<CharacterCrouch25D>();
    }

    private void Awake()
    {
        if (character == null)
            character = GetComponent<RBCharacter25D>();

        if (heroRb == null && character != null)
            heroRb = character.RigidbodyComponent;
        else if (heroRb == null)
            heroRb = GetComponent<Rigidbody>();

        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();
        if (crouch == null)
            crouch = GetComponent<CharacterCrouch25D>();

        ClampSettings();
    }

    private void OnValidate()
    {
        ClampSettings();

        if (character == null)
            character = GetComponent<RBCharacter25D>();

        if (heroRb == null && character != null)
            heroRb = character.RigidbodyComponent;
        else if (heroRb == null)
            heroRb = GetComponent<Rigidbody>();

        if (controlLock == null)
            controlLock = GetComponent<PlayerControlLock25D>();
        if (crouch == null)
            crouch = GetComponent<CharacterCrouch25D>();
    }

    public bool ApplyProjectileHit(Vector3 projectileDirection)
    {
        if (heroRb == null || heroRb.isKinematic || character == null)
            return false;

        if (character.IsVaultingNow)
            return true;

        Vector2 direction2D = new Vector2(projectileDirection.x, projectileDirection.y);
        if (direction2D.sqrMagnitude <= Epsilon)
        {
            int fallbackSign = character.VaultFacingSignFromInput;
            direction2D = fallbackSign >= 0 ? Vector2.right : Vector2.left;
        }
        else
        {
            direction2D.Normalize();
        }

        float horizontalSign = Mathf.Abs(direction2D.x) > Epsilon
            ? Mathf.Sign(direction2D.x)
            : character.VaultFacingSignFromInput;

        bool isGrounded = character.IsGroundedNow;
        bool isMovingHorizontally = Mathf.Abs(heroRb.linearVelocity.x) > movingThreshold;

        bool useDiagonal = !isGrounded || (isMovingHorizontally && Random.value < movingDiagonalChance);
        Vector2 knockbackVelocity = useDiagonal
            ? new Vector2(diagonalKnockbackX * horizontalSign, diagonalKnockbackY)
            : new Vector2(horizontalKnockbackX * horizontalSign, 0f);

        character.SetLockStanceHeld(false);
        if (crouch != null)
            crouch.ForceExitCrouchFromHit();

        character.ResetMotionForExternalHit(clearInput: true, clearCurrentWallSlide: true);

        heroRb.linearVelocity = new Vector3(knockbackVelocity.x, knockbackVelocity.y, 0f);

        ApplyControlLockForKnockback(useDiagonal, suppressTraversal: true);
        return true;
    }

    private void ApplyControlLockForKnockback(bool useDiagonal, bool suppressTraversal)
    {
        if (controlLock == null)
            return;

        if (useDiagonal)
        {
            if (useDiagonalControlLock && diagonalControlLockTime > 0f)
                controlLock.StartDiagonalLock(diagonalControlLockTime, suppressTraversal);
            else
                controlLock.ClearLock();

            return;
        }

        if (useHorizontalControlLock && horizontalControlLockTime > 0f)
            controlLock.StartHorizontalLock(horizontalControlLockTime, suppressTraversal);
        else
            controlLock.ClearLock();
    }

    private void ClampSettings()
    {
        movingThreshold = Mathf.Max(0f, movingThreshold);
        horizontalKnockbackX = Mathf.Max(0f, horizontalKnockbackX);
        diagonalKnockbackX = Mathf.Max(0f, diagonalKnockbackX);
        diagonalKnockbackY = Mathf.Max(0f, diagonalKnockbackY);
        movingDiagonalChance = Mathf.Clamp01(movingDiagonalChance);
        horizontalControlLockTime = Mathf.Max(0f, horizontalControlLockTime);
        diagonalControlLockTime = Mathf.Max(0f, diagonalControlLockTime);
    }
}
