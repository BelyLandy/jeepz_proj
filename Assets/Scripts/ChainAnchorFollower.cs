using UnityEngine;

public class ChainAnchorFollower : MonoBehaviour
{
    public Rigidbody2D heroRb;
    public Rigidbody2D anchorRb;          // Kinematic
    public Vector2 hookPointOnHero;       // локальная точка на герое
    public float followStiffness = 1f;    // 1 = жёстко, 0.2 = мягче

    void FixedUpdate()
    {
        Vector2 target = heroRb.transform.TransformPoint(hookPointOnHero);
        // мягкий MovePosition к цели
        Vector2 pos = Vector2.Lerp(anchorRb.position, target, followStiffness);
        anchorRb.MovePosition(pos);
        // Если нужно, можешь ещё ориентировать якорь под угол героя:
        // anchorRb.MoveRotation(heroRb.rotation);
    }
}

