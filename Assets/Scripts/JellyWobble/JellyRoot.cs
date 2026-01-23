using UnityEngine;

public class JellyRoot : MonoBehaviour
{
    public Vector2 Velocity { get; private set; }
    public Vector2 Acceleration { get; private set; }

    Vector2 _lastPos;
    Vector2 _lastVel;

    void OnEnable()
    {
        _lastPos = transform.position;
        _lastVel = Vector2.zero;
        Velocity = Vector2.zero;
        Acceleration = Vector2.zero;
    }

    void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector2 pos = transform.position;

        Velocity = (pos - _lastPos) / dt;
        Acceleration = (Velocity - _lastVel) / dt;

        _lastPos = pos;
        _lastVel = Velocity;
    }
}
