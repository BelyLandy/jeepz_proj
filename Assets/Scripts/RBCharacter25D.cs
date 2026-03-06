using UnityEngine;

#region Rigidbody compatibility (linearVelocity/velocity)
static class RBCompat
{
#if UNITY_6000_0_OR_NEWER || UNITY_2023_3_OR_NEWER
    public static void SetLinearVelocity(Rigidbody rb, Vector3 v) => rb.linearVelocity = v;
    public static Vector3 GetLinearVelocity(Rigidbody rb) => rb.linearVelocity;
#else
    public static void SetLinearVelocity(Rigidbody rb, Vector3 v) => rb.velocity = v;
    public static Vector3 GetLinearVelocity(Rigidbody rb) => rb.velocity;
#endif
}
#endregion

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class RBCharacter25D : MonoBehaviour
{
    [Header("2.5D constraint")]
    public bool lockZ = true;
    public float lockedZ = 0f;

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
    public float groundCheckRadius = 0.12f;
    public Vector3 groundCheckOffset = new Vector3(0, -0.95f, 0);

    [Header("Wall stop")]
    [Tooltip("Дистанция проверки стены в сторону движения (в world units)")]
    public float wallCheckDistance = 0.5f;
    [Tooltip("Насколько 'высоко' по телу проверяем стену (0 = центр коллайдера)")]
    public float wallCheckHeightOffset = 0.0f;
    [Tooltip("Радиус для SphereCast (обычно чуть меньше половины толщины коллайдера)")]
    public float wallCheckRadius = 0.12f;

    [Header("Corner slide (anti-stuck on box corners)")]
    public bool enableCornerSlide = true;

    [Tooltip("Минимальная скорость вниз, когда упёрлись в угол/стену (units/s).")]
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

    private Rigidbody rb;
    private CapsuleCollider col;

    private float inputX;
    private float lastGroundedTime = -999f;
    private float lastJumpPressedTime = -999f;
    private bool isGrounded;

    private bool wasGroundedLastFixed;
    private int jumpsRemaining;

    private float lastJumpExecutedTime = -999f;

    // Для autoTuneJump: "множитель" к глобальной гравитации
    private float gravityMultiplier = 1f;

    // NonAlloc буферы
    private readonly Collider[] overlapHits = new Collider[24];
    private readonly RaycastHit[] castHits = new RaycastHit[16];

    // --- ДОБАВЛЕНО: наружные геттеры для IK/анимации ---
    public bool IsGroundedNow => isGrounded;
    public float LastJumpTime => lastJumpExecutedTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        rb.freezeRotation = true;

        if (lockZ)
        {
            rb.constraints |= RigidbodyConstraints.FreezePositionZ;
            lockedZ = transform.position.z;
        }

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
            var vel = RBCompat.GetLinearVelocity(rb);
            if (vel.y > 0f)
                RBCompat.SetLinearVelocity(rb, new Vector3(vel.x, vel.y * 0.5f, vel.z));
        }
    }

    void FixedUpdate()
    {
        ApplyGravityMultiplier();

        // Жёстко держим Z (на случай, если что-то пытается сдвинуть)
        if (lockZ)
        {
            var p = rb.position;
            p.z = lockedZ;
            rb.position = p;
        }

        isGrounded = CheckGrounded();

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
                var velNow = RBCompat.GetLinearVelocity(rb);

                bool recentlyJumped =
                    (Time.time - lastJumpExecutedTime) <= (Time.fixedDeltaTime * 1.5f);

                if (!recentlyJumped && velNow.y <= 0.01f)
                {
                    jumpsRemaining = enableDoubleJump ? Mathf.Min(jumpsRemaining, 1) : 0;
                }
            }
        }

        wasGroundedLastFixed = isGrounded;

        CheckWalls(out bool blockedLeft, out bool blockedRight);

        var vel = RBCompat.GetLinearVelocity(rb);

        float target = inputX * moveSpeed;

        if (target > 0f && blockedRight) target = 0f;
        if (target < 0f && blockedLeft)  target = 0f;

        float factor = isGrounded ? 1f : airControl;
        float accel = (Mathf.Abs(target) > 0.01f ? acceleration : deceleration) * factor;

        float newVX = Mathf.MoveTowards(vel.x, target, accel * Time.fixedDeltaTime);

        if (newVX > 0f && blockedRight) newVX = 0f;
        if (newVX < 0f && blockedLeft)  newVX = 0f;

        // 2.5D: X — ходьба, Y — прыжок, Z фиксируем
        RBCompat.SetLinearVelocity(rb, new Vector3(newVX, vel.y, 0f));

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
                    var v = RBCompat.GetLinearVelocity(rb);

                    if (v.y <= cornerSlideOnlyWhenVyBelow)
                    {
                        float desiredY = -Mathf.Abs(cornerSlideDownSpeed);
                        if (v.y > desiredY)
                            RBCompat.SetLinearVelocity(rb, new Vector3(v.x, desiredY, 0f));
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

                vel = RBCompat.GetLinearVelocity(rb);
                RBCompat.SetLinearVelocity(rb, new Vector3(vel.x, 0f, 0f));
                rb.AddForce(Vector3.up * impulse, ForceMode.Impulse);

                jumpsRemaining = Mathf.Max(0, jumpsRemaining - 1);
                lastJumpPressedTime = -999f;
                lastJumpExecutedTime = Time.time;
            }
        }
    }

    void ApplyGravityMultiplier()
    {
        if (!autoTuneJump) return;

        float diff = gravityMultiplier - 1f;
        if (Mathf.Abs(diff) < 1e-4f) return;

        // Делает итоговую гравитацию: Physics.gravity * gravityMultiplier
        rb.AddForce(Physics.gravity * diff, ForceMode.Acceleration);
    }

    bool CheckGrounded()
    {
        Vector3 checkPos = transform.position + groundCheckOffset;

        int count = Physics.OverlapSphereNonAlloc(
            checkPos,
            groundCheckRadius,
            overlapHits,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            var c = overlapHits[i];
            if (c == null) continue;
            if (c.attachedRigidbody == rb) continue; // игнорим себя
            return true;
        }

        return false;
    }

    // Вариант без GetContacts (чтобы работало в любых версиях Unity):
    // Проверяем "плоскую землю" через SphereCast вниз.
    bool HasFlatGroundContact()
    {
        Vector3 basePos = transform.position + groundCheckOffset + Vector3.up * 0.1f;
        float r = Mathf.Max(0.001f, groundCheckRadius);

        int count = Physics.SphereCastNonAlloc(
            basePos,
            r,
            Vector3.down,
            castHits,
            0.25f,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            var h = castHits[i];
            if (h.collider == null) continue;
            if (h.collider.attachedRigidbody == rb) continue;

            if (h.normal.y >= flatGroundNormalY)
                return true;
        }

        return false;
    }

    void CheckWalls(out bool blockedLeft, out bool blockedRight)
    {
        Bounds b = col.bounds;
        Vector3 origin = new Vector3(b.center.x, b.center.y + wallCheckHeightOffset, lockedZ);

        float r = Mathf.Max(0.001f, wallCheckRadius);

        blockedRight = SphereCastHitsSomething(origin, r, Vector3.right, wallCheckDistance);
        blockedLeft  = SphereCastHitsSomething(origin, r, Vector3.left,  wallCheckDistance);
    }

    bool SphereCastHitsSomething(Vector3 origin, float radius, Vector3 dir, float dist)
    {
        int count = Physics.SphereCastNonAlloc(
            origin,
            radius,
            dir,
            castHits,
            dist,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            var h = castHits[i];
            if (h.collider == null) continue;
            if (h.collider.attachedRigidbody == rb) continue; // игнорим себя
            return true;
        }
        return false;
    }

    void TuneJump()
    {
        float globalG = Mathf.Abs(Physics.gravity.y);
        if (globalG < 1e-5f) globalG = 9.81f;

        float hUnits = jumpHeightPixels / Mathf.Max(1e-5f, pixelsPerUnit);
        float t = Mathf.Max(1e-5f, timeToApex);

        float gNeeded = (2f * hUnits) / (t * t);
        float v0 = gNeeded * t;

        gravityMultiplier = gNeeded / globalG;
        jumpImpulse = rb.mass * v0;

        float t2 = Mathf.Max(1e-5f, doubleJumpTimeToApex);
        float v02 = gNeeded * t2;
        doubleJumpImpulse = rb.mass * v02;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 gpos = transform.position + groundCheckOffset;
        Gizmos.DrawWireSphere(gpos, groundCheckRadius);

        var cc = GetComponent<CapsuleCollider>();
        if (cc != null)
        {
            Gizmos.color = Color.cyan;
            Bounds b = cc.bounds;
            Vector3 origin = new Vector3(b.center.x, b.center.y + wallCheckHeightOffset, transform.position.z);
            Gizmos.DrawWireSphere(origin + Vector3.right * wallCheckDistance, wallCheckRadius);
            Gizmos.DrawWireSphere(origin + Vector3.left  * wallCheckDistance, wallCheckRadius);
        }
    }
}