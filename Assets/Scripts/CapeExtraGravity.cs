using UnityEngine;

public class CapeExtraGravity : MonoBehaviour
{
    public Rigidbody2D heroRb;
    public float extraGravity = 40f;      // сила “доп. гравитации”
    public float speedThreshold = 0.5f;   // при какой скорости усиливать падение

    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        float speed = Mathf.Abs(heroRb.linearVelocity.x);

        // Если герой почти не движется – тянем плащ вниз сильнее
        if (speed < speedThreshold)
        {
            rb.AddForce(Vector2.down * extraGravity);
        }
    }
}
