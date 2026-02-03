using UnityEngine;

public class MainCharacter : MonoBehaviour
{
    public int health { get; set; }
    public float moveSpeed = 5f;

    public MainCharacter(int health)
    {
        this.health = health;
    }

    public MainCharacter()
    {
        
    }

}
