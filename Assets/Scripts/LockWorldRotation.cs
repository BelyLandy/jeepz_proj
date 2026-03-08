using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class LockWorldRotation : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform _flipSourceTransform;

    [Header("World Rotation")]
    [SerializeField] private bool enableWorldRotation = true;
    [SerializeField] private Vector3 targetWorldEuler = Vector3.zero;

    [Header("FlipX")]
    [SerializeField] private bool enableFlipX = false;
    [Tooltip("Угол, после которого флип происходит (в градусах).")]
    [SerializeField] private float flipThreshold = 90f;
    [Tooltip("Хистерезис вокруг порога, чтобы избежать дрожания.")]
    [SerializeField] private float flipHysteresis = 2f;

    [Tooltip("Если нужен особый порог для головы — включи и задай ниже.")]
    [SerializeField] private bool useHeadOverride = false;
    [SerializeField] private float headFlipThreshold = 45f;

    private SpriteRenderer _spriteRenderer;
    private bool _lastFlipX;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_flipSourceTransform == null && transform.parent != null)
            _flipSourceTransform = transform.parent;
    }

    private void LateUpdate()
    {
        if (enableWorldRotation)
            ApplyWorldRotationCompensated();

        if (enableFlipX)
            ApplyFlipX();
    }

    private void ApplyWorldRotationCompensated()
    {
        Quaternion targetWorldRotation = Quaternion.Euler(targetWorldEuler);

        Transform realParent = transform.parent;

        if (realParent != null)
            transform.localRotation = Quaternion.Inverse(realParent.rotation) * targetWorldRotation;
        else
            transform.rotation = targetWorldRotation;
    }

    private void ApplyFlipX()
    {
        if (_spriteRenderer == null)
            return;

        Transform source = _flipSourceTransform != null ? _flipSourceTransform : transform.parent;
        if (source == null)
            return;

        float y = source.eulerAngles.y;
        float signedY = Mathf.DeltaAngle(0f, y);

        float threshold = useHeadOverride ? headFlipThreshold : flipThreshold;
        float absY = Mathf.Abs(signedY);

        bool shouldFlip;
        if (_lastFlipX)
            shouldFlip = absY > Mathf.Max(0f, threshold - flipHysteresis);
        else
            shouldFlip = absY > threshold;

        if (shouldFlip != _lastFlipX)
        {
            _spriteRenderer.flipX = shouldFlip;
            _lastFlipX = shouldFlip;
        }
    }

    private void OnTransformParentChanged()
    {
        if (_flipSourceTransform == null)
            _flipSourceTransform = transform.parent;
    }
}