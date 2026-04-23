using UnityEngine;

[DisallowMultipleComponent]
public sealed class SceneLoadOverlayView : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;

    public CanvasGroup CanvasGroup => canvasGroup;

    private void Reset()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);
    }

    public void SetVisibleImmediate(bool visible, bool blockRaycasts)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = visible && blockRaycasts;
    }
}
