using UnityEngine;

[ExecuteAlways]
public class PlanetLayerRotator : MonoBehaviour
{
    public Vector3 RotationAxis = Vector3.up;
    public float DegreesPerSecond = 2.5f;
    public bool UseLocalSpace = true;
    public bool RotateInEditMode = false;

    private void Update()
    {
        if (!Application.isPlaying && !RotateInEditMode)
        {
            return;
        }

        Vector3 axis = RotationAxis.sqrMagnitude > 0.0001f ? RotationAxis.normalized : Vector3.up;
        float deltaTime = Application.isPlaying ? Time.deltaTime : 0.0166667f;
        float angle = DegreesPerSecond * deltaTime;

        if (Mathf.Approximately(angle, 0f))
        {
            return;
        }

        if (UseLocalSpace)
        {
            transform.Rotate(axis, angle, Space.Self);
        }
        else
        {
            transform.Rotate(axis, angle, Space.World);
        }
    }
}
