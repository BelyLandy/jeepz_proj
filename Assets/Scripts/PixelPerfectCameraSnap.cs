using UnityEngine;

public class PixelPerfectCameraSnap : MonoBehaviour
{
    public float pixelsPerUnit = 64f; // или какое у тебя значение

    void LateUpdate()
    {
        var pos = transform.position;
        float unitsPerPixel = 1f / pixelsPerUnit;

        pos.x = Mathf.Round(pos.x / unitsPerPixel) * unitsPerPixel;
        pos.y = Mathf.Round(pos.y / unitsPerPixel) * unitsPerPixel;

        transform.position = pos;
    }
}
