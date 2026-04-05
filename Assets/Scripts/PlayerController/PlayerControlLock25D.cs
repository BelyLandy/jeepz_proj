using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerControlLock25D : MonoBehaviour
{
    private enum ControlLockKind
    {
        None = 0,
        Horizontal = 1,
        Diagonal = 2,
    }

    [Header("References")]
    [SerializeField] private RBCharacter25D character;

    [Header("Diagonal Lock")]
    [SerializeField] private bool clearDiagonalLockOnLanding = true;

    private const float InvalidPastTime = -999f;

    private ControlLockKind currentLockKind = ControlLockKind.None;
    private float controlLockUntilTime = InvalidPastTime;
    private bool suppressTraversalWhileLocked;

    public bool IsControlLocked => currentLockKind != ControlLockKind.None && Time.time < controlLockUntilTime;
    public bool IsHorizontalLockActive => currentLockKind == ControlLockKind.Horizontal && Time.time < controlLockUntilTime;
    public bool IsDiagonalLockActive => currentLockKind == ControlLockKind.Diagonal && Time.time < controlLockUntilTime;

    private void Reset()
    {
        if (character == null)
            character = GetComponent<RBCharacter25D>();
    }

    private void Awake()
    {
        if (character == null)
            character = GetComponent<RBCharacter25D>();
    }

    private void OnValidate()
    {
        if (character == null)
            character = GetComponent<RBCharacter25D>();
    }

    private void Update()
    {
        if (currentLockKind == ControlLockKind.None)
            return;

        if (currentLockKind == ControlLockKind.Diagonal &&
            clearDiagonalLockOnLanding &&
            character != null &&
            character.IsGroundedNow)
        {
            ClearLock();
            return;
        }

        if (Time.time >= controlLockUntilTime)
            ClearLock();
    }

    public void StartHorizontalLock(float duration, bool suppressWallSlide = false)
    {
        if (currentLockKind != ControlLockKind.None)
            ClearLock();

        if (duration <= 0f)
        {
            ClearLock();
            return;
        }

        currentLockKind = ControlLockKind.Horizontal;
        controlLockUntilTime = Time.time + duration;
        suppressTraversalWhileLocked = suppressWallSlide;

        if (suppressTraversalWhileLocked && character != null)
        {
            character.SuppressWallSlide(duration, clearCurrentWallSlide: true);
            character.SuppressVaultStart(duration);
        }
    }

    public void StartDiagonalLock(float duration, bool suppressWallSlide = false)
    {
        if (currentLockKind != ControlLockKind.None)
            ClearLock();

        if (duration <= 0f)
        {
            ClearLock();
            return;
        }

        currentLockKind = ControlLockKind.Diagonal;
        controlLockUntilTime = Time.time + duration;
        suppressTraversalWhileLocked = suppressWallSlide;

        if (suppressTraversalWhileLocked && character != null)
        {
            character.SuppressWallSlide(duration, clearCurrentWallSlide: true);
            character.SuppressVaultStart(duration);
        }
    }

    public void ClearLock()
    {
        if (suppressTraversalWhileLocked && character != null)
        {
            character.ClearWallSlideSuppression();
            character.ClearVaultStartSuppression();
        }

        currentLockKind = ControlLockKind.None;
        controlLockUntilTime = InvalidPastTime;
        suppressTraversalWhileLocked = false;
    }
}
