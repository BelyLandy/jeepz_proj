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
    public float moveSpeed = 7f;           // скорость по земле
    public float acceleration = 60f;       // разгон
    public float deceleration = 80f;       // торможение
    public float airControl = 0.6f;        // управление в воздухе (0..1)

    [Header("Jump")]
    public float jumpImpulse = 12f;        // сила прыжка (импульс)
    public float coyoteTime = 0.1f;        // “время койота” после схода с земли
    public float jumpBuffer = 0.1f;        // буфер прыжка, если нажали чуть раньше касания
    public bool  cutJumpOnRelease = true;  // “обрезать” прыжок, если отпустили кнопку

    [Header("Grounding")]
    public LayerMask groundMask;
    public float groundCheckRadius = 0.1f; // радиус проверки под ногами
    public Vector2 groundCheckOffset = new Vector2(0, -0.5f);

    Rigidbody2D rb;
    Collider2D col;
    float inputX;
    bool wantJump;
    float lastGroundedTime = -999f;     // важно: инициализация "в прошлом"
    float lastJumpPressedTime = -999f;  // чтобы jumpBuffer не сработал на старте
    bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.freezeRotation = true; // чтобы не падал набок
    }

    void Update()
    {
        // Ввод — простейший (старый Input)
        inputX = Input.GetAxisRaw("Horizontal");      // A/D, стрелки
        if (Input.GetButtonDown("Jump"))
        {
            lastJumpPressedTime = Time.time;
            // Debug.Log("Jump pressed at: " + lastJumpPressedTime);
        }
        if (Input.GetButton("Jump")) wantJump = true;

        

        // Обрезание прыжка: отпустили — уменьшаем вертикальную скорость
        var vel = RB2DCompat.GetLinearVelocity(rb);
        if (cutJumpOnRelease && Input.GetButtonUp("Jump") && vel.y > 0f)
        {
            RB2DCompat.SetLinearVelocity(rb, new Vector2(vel.x, vel.y * 0.5f));
        }
    }

    void FixedUpdate()
    {
        // Движение по X с плавным разгоном/торможением
        var vel = RB2DCompat.GetLinearVelocity(rb);

        float target = inputX * moveSpeed;
        float factor = isGrounded ? 1f : airControl;
        float accel  = (Mathf.Abs(target) > 0.01f ? acceleration : deceleration) * factor;

        float newVX = Mathf.MoveTowards(vel.x, target, accel * Time.fixedDeltaTime);
        RB2DCompat.SetLinearVelocity(rb, new Vector2(newVX, vel.y));

        // Прыжок с “койотом” и буфером
        bool canCoyote = Time.time - lastGroundedTime <= coyoteTime;
        bool buffered  = Time.time - lastJumpPressedTime <= jumpBuffer;
        if (buffered && (isGrounded || canCoyote))
        {
            // сбросить вертикаль и дать импульс вверх
            vel = RB2DCompat.GetLinearVelocity(rb);
            RB2DCompat.SetLinearVelocity(rb, new Vector2(vel.x, 0f));
            rb.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);

            lastJumpPressedTime = -999f; // погасить буфер
            wantJump = false;
        }

        // Проверка земли
        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        isGrounded = Physics2D.OverlapCircle(checkPos, groundCheckRadius, groundMask);

        if (isGrounded) lastGroundedTime = Time.time;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 checkPos = (Vector2)transform.position + groundCheckOffset;
        Gizmos.DrawWireSphere(checkPos, groundCheckRadius);
    }
}
