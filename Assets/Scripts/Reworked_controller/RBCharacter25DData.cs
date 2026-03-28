using UnityEngine;

public struct FrameInput25D
{
    public float RawX;
    public float SmoothedX;
    public bool JumpPressed;
    public bool JumpHeld;
    public bool JumpReleased;
    public int FacingSign;
}

public struct SurfaceContacts25D
{
    public bool IsGrounded;
    public bool HasSupport;
    public RaycastHit SupportHit;
    public bool HasGroundSurface;
    public RaycastHit GroundHit;
    public Vector3 GroundNormal;
    public bool OnSlope;
    public float SlopeAngle;
    public Vector3 SlopeTangent;
    public float DownhillSign;
    public bool BlockedLeft;
    public bool BlockedRight;
    public RaycastHit LeftWallHit;
    public RaycastHit RightWallHit;
}

public struct LocomotionState25D
{
    public bool IsGrounded;
    public bool WasGroundedLastFixed;
    public bool IsWallSliding;
    public int WallSlideSide;
    public int JumpsRemaining;
    public float LastGroundedTime;
    public float LastJumpPressedTime;
    public float LastJumpExecutedTime;
    public float WallReattachLockUntilTime;
    public int WallReattachLockedSide;
    public float WallDetachHoldTimer;
    public bool UsedDoubleJumpSinceLastGrounded;
    public bool IsDoubleJumpSpeedBoostActive;
    public float JumpBlockedUntilTime;
    public bool PendingSlopeStickAfterJump;
    public float SlopeLockUntilTime;
    public RBCharacter25D.SelfJumpKind LastSelfJumpKind;
    public int LastSelfJumpStateVersion;
}

public struct VelocityCommand25D
{
    public Vector3 TargetVelocity;
    public bool OverrideX;
    public bool OverrideY;
    public bool ConsumedGroundJump;
    public bool ConsumedWallJump;
}
