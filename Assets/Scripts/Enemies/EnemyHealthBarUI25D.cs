using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EnemyHealthBarUI25D : MonoBehaviour
{
    [SerializeField] private EnemyHealth25D health;
    [SerializeField] private Transform worldAnchor;
    [SerializeField] private Slider slider;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private Vector3 worldOffset = Vector3.up * 1.5f;
    [SerializeField] private bool hideWhenDead;

    private RectTransform rectTransform;

    private void Reset()
    {
        AutoAssign();
    }

    private void Awake()
    {
        AutoAssign();
    }

    private void OnValidate()
    {
        AutoAssign();
    }

    private void LateUpdate()
    {
        if (slider == null || health == null)
            return;

        slider.value = health.HealthNormalized;

        bool shouldHide = hideWhenDead && health.IsDead;
        if (shouldHide)
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
            return;
        }

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || worldAnchor == null || parentCanvas == null)
            return;

        Vector3 worldPosition = worldAnchor.position + worldOffset;
        Vector3 screenPoint = cam.WorldToScreenPoint(worldPosition);
        if (screenPoint.z < 0f)
            return;

        RectTransform canvasRect = parentCanvas.transform as RectTransform;
        if (canvasRect == null || rectTransform == null)
            return;

        Camera uiCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
            rectTransform.localPosition = localPoint;
    }

    private void AutoAssign()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;
        if (slider == null)
            slider = GetComponent<Slider>();
        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();
    }
}
