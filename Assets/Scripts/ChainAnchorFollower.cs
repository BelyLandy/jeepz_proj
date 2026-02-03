using UnityEngine;

public class ChainAnchorFollower : MonoBehaviour
{
    public Rigidbody2D heroRb;
    public Rigidbody2D anchorRb;
    public Vector2 hookPointOnHero;
    public float followStiffness = 1f;

    void FixedUpdate()
    {
        Vector2 target = heroRb.transform.TransformPoint(hookPointOnHero);
        Vector2 pos = Vector2.Lerp(anchorRb.position, target, followStiffness);
        anchorRb.MovePosition(pos);
        // anchorRb.MoveRotation(heroRb.rotation);
    }
}

