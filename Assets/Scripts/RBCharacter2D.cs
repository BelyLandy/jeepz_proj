using UnityEngine;

#region Rigidbody2D compatibility (linearVelocity/velocity)
static class RB2DCompat
{
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
    public static void SetLinearVelocity(Rigidbody2D rb, Vector2 v) => rb.linearVelocity = v;
    public static Vector2 GetLinearVelocity(Rigidbody2D rb) => rb.linearVelocity;
#else
    public static void SetLinearVelocity(Rigidbody2D rb, Vector2 v) => rb.velocity = v;
    public static Vector2 GetLinearVelocity(Rigidbody2D rb) => rb.velocity;
#endif
}
#endregion

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class RBCharacter2D : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 7f;
    public float acceleration = 60f;
    public float deceleration = 80f;
    [Range(0f, 1f)] public float airControl = 0.6f;

    [Header("Jump")]
    [Tooltip("Импульс прыжка (будет перезаписан авто-настройкой, если autoTuneJump=true)")]
    public float jumpImpulse = 12f;
    public float coyoteTime = 0.1f;
    public float jumpBuffer = 0.1f;
    public bool cutJumpOnRelease = false;

    [Header("Double Jump")]
    public bool enableDoubleJump = true;
    public bool doubleJumpOnlyInAir = true;
    [Tooltip("Импульс второго прыжка (будет перезаписан авто-настройкой, если autoTuneJump=true)")]
    public float doubleJumpImpulse = 12f;

    [Header("Grounding")]
    public LayerMask groundMask;
    public float groundCheckRadius = 0.1f;
    public Vector2 groundCheckOffset = new Vector2(0, -0.9375f);

    [Header("Wall stop")]
    [Tooltip("Дистанция проверки стены в сторону движения (в world units)")]
    public float wallCheckDistance = 0.5f;
    [Tooltip("Насколько 'высоко' по телу проверяем стену (0 = центр коллайдера)")]
    public float wallCheckHeightOffset = 0.0f;
    [Tooltip("Радиус для CircleCast (обычно чуть меньше половины толщины коллайдера)")]
    public float wallCheckRadius = 0.08f;

    [Header("Corner slide (anti-stuck on box corners)")]
    public bool enableCornerSlide = true;

    [Tooltip("Минимальная скорость вниз, когда упёрлись в угол/стену (units/s). 1 unit = 32px при PPU=32.")]
    public float cornerSlideDownSpeed = 12f;

    [Tooltip("Не трогаем, если летим вверх быстрее этого (units/s).")]
    public float cornerSlideOnlyWhenVyBelow = 0.1f;

    [Tooltip("Порог 'плоской земли' по нормали. 1 = идеально плоско.")]
    [Range(0.5f, 1f)] public float flatGroundNormalY = 0.9f;

    [Header("Auto tune jump")]
    public bool autoTuneJump = true;

    public float pixelsPerUnit = 32f;
    public float jumpHeightPixels = 128f;
    public float timeToApex = 0.4265f;

    public float doubleJumpHeightPixels = 128f;
    public float doubleJumpTimeToApex = 0.4265f;

    public bool allowShortHop = true;

    private Rigidbody2D rb;
    private Collider2D col;

    private float inputX;
    private float lastGroundedTime = -999f;
    private float lastJumpPressedTime = -999f;
    private bool isGrounded;

    private bool wasGroundedLastFixed;
    private int jumpsRemaining;

    private float lastJumpExecutedTime = -999f;

    private ContactFilter2D contactFilter;
    private ContactPoint2D[] contactPoints = new ContactPoint2D[16];

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.freezeRotation = true;

        contactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = groundMask,
            useTriggers = false
        };

        if (autoTuneJump)
            TuneJump();

        jumpsRemaining = enableDoubleJump ? 2 : 1;
    }

    void Update()
    {
        inputX = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump"))
            lastJumpPressedTime = Time.time;

        if (allowShortHop && cutJumpOnRelease && Input.GetButtonUp("Jump"))
        {
            var vel = RB2DCompat.GetLinearVelocity(rb);
            if (vel.y > 0f)
                RB2DCompat.SetLinearVelocity(rb, new Vector2(vel.x, vel.y * 0.5f));
        }
    }

    void FixedUpdate()
    {
        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        isGrounded = Physics2D.OverlapCircle(checkPos, groundCheckRadius, groundMask);

        bool leftGroundThisFixed = wasGroundedLastFixed && !isGrounded;

        if (isGrounded)
        {
            lastGroundedTime = Time.time;

            if (!wasGroundedLastFixed)
                jumpsRemaining = enableDoubleJump ? 2 : 1;
        }
        else
        {
            if (leftGroundThisFixed)
            {
                var velNow = RB2DCompat.GetLinearVelocity(rb);

                bool recentlyJumped =
                    (Time.time - lastJumpExecutedTime) <= (Time.fixedDeltaTime * 1.5f);

                if (!recentlyJumped && velNow.y <= 0.01f)
                {
                    jumpsRemaining = enableDoubleJump ? Mathf.Min(jumpsRemaining, 1) : 0;
                }
            }
        }

        wasGroundedLastFixed = isGrounded;

        bool blockedLeft, blockedRight;
        CheckWalls(out blockedLeft, out blockedRight);

        var vel = RB2DCompat.GetLinearVelocity(rb);

        float target = inputX * moveSpeed;

        if (target > 0f && blockedRight) target = 0f;
        if (target < 0f && blockedLeft)  target = 0f;

        float factor = isGrounded ? 1f : airControl;
        float accel = (Mathf.Abs(target) > 0.01f ? acceleration : deceleration) * factor;

        float newVX = Mathf.MoveTowards(vel.x, target, accel * Time.fixedDeltaTime);

        if (newVX > 0f && blockedRight) newVX = 0f;
        if (newVX < 0f && blockedLeft)  newVX = 0f;

        RB2DCompat.SetLinearVelocity(rb, new Vector2(newVX, vel.y));

        if (enableCornerSlide)
        {
            bool pushingIntoWall =
                (inputX > 0.01f && blockedRight) ||
                (inputX < -0.01f && blockedLeft);

            if (pushingIntoWall)
            {
                bool hasFlatGround = HasFlatGroundContact();

                if (!hasFlatGround)
                {
                    var v = RB2DCompat.GetLinearVelocity(rb);

                    if (v.y <= cornerSlideOnlyWhenVyBelow)
                    {
                        float desiredY = -Mathf.Abs(cornerSlideDownSpeed);
                        if (v.y > desiredY)
                            RB2DCompat.SetLinearVelocity(rb, new Vector2(v.x, desiredY));
                    }
                }
            }
        }

        bool canCoyote = Time.time - lastGroundedTime <= coyoteTime;
        bool buffered  = Time.time - lastJumpPressedTime <= jumpBuffer;

        if (buffered)
        {
            bool groundJumpAllowed = (isGrounded || canCoyote) && jumpsRemaining > 0;
            bool airJumpAllowed = enableDoubleJump && jumpsRemaining > 0 && (!isGrounded && !canCoyote);

            if (!doubleJumpOnlyInAir)
                airJumpAllowed = enableDoubleJump && jumpsRemaining > 0 && !isGrounded;

            if (groundJumpAllowed || airJumpAllowed)
            {
                bool isFirstJump = (!enableDoubleJump) || (jumpsRemaining >= 2);
                float impulse = isFirstJump ? jumpImpulse : doubleJumpImpulse;

                vel = RB2DCompat.GetLinearVelocity(rb);
                RB2DCompat.SetLinearVelocity(rb, new Vector2(vel.x, 0f));
                rb.AddForce(Vector2.up * impulse, ForceMode2D.Impulse);

                jumpsRemaining = Mathf.Max(0, jumpsRemaining - 1);
                lastJumpPressedTime = -999f;
                lastJumpExecutedTime = Time.time;
            }
        }
    }

    bool HasFlatGroundContact()
    {
        int count = rb.GetContacts(contactFilter, contactPoints);
        for (int i = 0; i < count; i++)
        {
            if (contactPoints[i].normal.y >= flatGroundNormalY)
                return true;
        }
        return false;
    }

    void CheckWalls(out bool blockedLeft, out bool blockedRight)
    {
        Bounds b = col.bounds;
        Vector2 origin = new Vector2(b.center.x, b.center.y + wallCheckHeightOffset);
        float r = Mathf.Max(0.001f, wallCheckRadius);

        RaycastHit2D hitR = Physics2D.CircleCast(origin, r, Vector2.right, wallCheckDistance, groundMask);
        RaycastHit2D hitL = Physics2D.CircleCast(origin, r, Vector2.left,  wallCheckDistance, groundMask);

        blockedRight = hitR.collider != null;
        blockedLeft  = hitL.collider != null;
    }

    void TuneJump()
    {
        float globalG = Mathf.Abs(Physics2D.gravity.y);
        if (globalG < 1e-5f) globalG = 9.81f;

        float hUnits = jumpHeightPixels / Mathf.Max(1e-5f, pixelsPerUnit);
        float t = Mathf.Max(1e-5f, timeToApex);

        float gNeeded = (2f * hUnits) / (t * t);
        float v0 = gNeeded * t;

        rb.gravityScale = gNeeded / globalG;
        jumpImpulse = rb.mass * v0;

        float t2 = Mathf.Max(1e-5f, doubleJumpTimeToApex);
        float v02 = gNeeded * t2;
        doubleJumpImpulse = rb.mass * v02;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 gpos = (Vector2)transform.position + groundCheckOffset;
        Gizmos.DrawWireSphere(gpos, groundCheckRadius);

        var c = GetComponent<Collider2D>();
        if (c != null)
        {
            Gizmos.color = Color.cyan;
            Bounds b = c.bounds;
            Vector2 origin = new Vector2(b.center.x, b.center.y + wallCheckHeightOffset);
            Gizmos.DrawWireSphere(origin + Vector2.right * wallCheckDistance, wallCheckRadius);
            Gizmos.DrawWireSphere(origin + Vector2.left  * wallCheckDistance, wallCheckRadius);
        }
    }
}
