using UnityEngine;

public class CapeExtraGravity : MonoBehaviour
{
    public Rigidbody2D heroRb;
    public float extraGravity = 40f;
    public float speedThreshold = 0.5f;

    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        float speed = Mathf.Abs(heroRb.linearVelocity.x);

        if (speed < speedThreshold)
        {
            rb.AddForce(Vector2.down * extraGravity);
        }
    }
}
