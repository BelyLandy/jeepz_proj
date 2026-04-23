using UnityEngine;

[DefaultExecutionOrder(20000)]
public class Parallax3DLayer : MonoBehaviour
{
    [Header("Reference movement source")]
    public Transform reference;   // обычно MainCamera.transform или герой

    [Header("Parallax factors (0..1)")]
    [Range(0f, 1f)] public float factorX = 0.2f;
    [Range(0f, 1f)] public float factorY = 0.05f;
    public bool lockY = true;

    Vector3 _refStart;
    Vector3 _layerStart;

    void Awake()
    {
        if (!reference && Camera.main) reference = Camera.main.transform;

        _refStart = reference.position;
        _layerStart = transform.position;
    }

    void LateUpdate()
    {
        var d = reference.position - _refStart;
        float y = lockY ? 0f : d.y;

        // „ем меньше factor Ч тем "дальше" слой (движетс€ слабее)
        transform.position = _layerStart + new Vector3(d.x * factorX, y * factorY, 0f);
    }
}
