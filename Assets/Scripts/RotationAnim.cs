using UnityEngine;

public class RotationAnim : MonoBehaviour
{
    private float inputX;

    public float leftYaw = 135f;
    public float rightYaw = 45f;
    public float turnSpeed = 540f;

    private Quaternion desiredRotation;

    // -1 = смотрит влево, +1 = вправо
    public int FacingSign { get; private set; } = +1;

    public float InputX => inputX;
    public bool HasMoveInput { get; private set; }

    void Awake()
    {
        desiredRotation = transform.rotation;
    }

    void Update()
    {
        inputX = Input.GetAxisRaw("Horizontal");
        HasMoveInput = Mathf.Abs(inputX) > 0.01f;

        if (inputX < -0.01f)
        {
            FacingSign = -1;
            desiredRotation = Quaternion.Euler(0f, leftYaw, 0f);
        }
        else if (inputX > 0.01f)
        {
            FacingSign = +1;
            desiredRotation = Quaternion.Euler(0f, rightYaw, 0f);
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            desiredRotation,
            turnSpeed * Time.deltaTime
        );
    }
}