using UnityEngine;

[DisallowMultipleComponent]
public class LockWorldRotation : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform _parentTransform;

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
    private Quaternion _lastParentRotation;
    private bool _hasLastParentRotation;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_parentTransform == null && transform.parent != null)
            _parentTransform = transform.parent;
    }

    private void LateUpdate()
    {
        bool parentRotChanged = true;
        if (_parentTransform != null)
        {
            if (_hasLastParentRotation && _lastParentRotation == _parentTransform.rotation)
                parentRotChanged = false;

            _lastParentRotation = _parentTransform.rotation;
            _hasLastParentRotation = true;
        }

        if (enableWorldRotation && parentRotChanged)
            ApplyWorldRotation();

        if (enableFlipX && parentRotChanged)
            ApplyFlipX();
    }

    private void ApplyWorldRotation()
    {
        if (transform.rotation.eulerAngles != targetWorldEuler)
            transform.rotation = Quaternion.Euler(targetWorldEuler);
    }

    private void ApplyFlipX()
    {
        if (_spriteRenderer == null || _parentTransform == null)
            return;

        float y = _parentTransform.eulerAngles.y;
        float signedY = Mathf.DeltaAngle(0f, y);

        float threshold = useHeadOverride ? headFlipThreshold : flipThreshold;

        bool shouldFlip;
        float absY = Mathf.Abs(signedY);

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
        _parentTransform = transform.parent;
        _hasLastParentRotation = false;
    }
}
