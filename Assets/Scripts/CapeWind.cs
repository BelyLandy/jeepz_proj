using UnityEngine;

public class CapeWind : MonoBehaviour
{
    public Rigidbody2D heroRb;
    public float windStrength = 5f;   // подбираешь в инспекторе

    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        var v = heroRb.linearVelocity;
        float speed = Mathf.Abs(v.x);
        if (speed < 0.1f) return;

        // направление ветра — обратно движению героя
        Vector2 dir = new Vector2(-Mathf.Sign(v.x), 0f);

        // сила растёт с квадратом скорости, как в реале
        rb.AddForce(dir * windStrength * speed * speed);
    }
}
