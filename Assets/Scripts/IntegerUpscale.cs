using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(RawImage))]
public class IntegerUpscale : MonoBehaviour
{
    public int refWidth = 320;
    public int refHeight = 180;

    RectTransform rt;
    RawImage ri;

#if UNITY_EDITOR
    bool _scheduled;
#endif

    void OnEnable()
    {
        Cache();
        SafeUpdateScale();
        Canvas.willRenderCanvases += SafeUpdateScale;
    }

    void OnDisable()
    {
        Canvas.willRenderCanvases -= SafeUpdateScale;
    }

    void Update()
    {
        if (Application.isPlaying)
            SafeUpdateScale();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        Cache();
        if (!_scheduled)
        {
            _scheduled = true;
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                _scheduled = false;
                SafeUpdateScale();
            };
        }
    }
#endif

    void Cache()
    {
        if (rt == null) rt = (RectTransform)transform;
        if (ri == null) ri = GetComponent<RawImage>();
    }

    void SafeUpdateScale()
    {
        if (!isActiveAndEnabled || rt == null || ri == null) return;

        int sw = Screen.width;
        int sh = Screen.height;
        if (sw <= 0 || sh <= 0 || refWidth <= 0 || refHeight <= 0) return;

        int scale = Mathf.Max(1, Mathf.Min(sw / refWidth, sh / refHeight));
        int w = refWidth * scale;
        int h = refHeight * scale;

        var center = new Vector2(0.5f, 0.5f);
        if (rt.anchorMin != center) rt.anchorMin = center;
        if (rt.anchorMax != center) rt.anchorMax = center;
        if (rt.pivot    != center) rt.pivot    = center;
        if (rt.anchoredPosition != Vector2.zero) rt.anchoredPosition = Vector2.zero;

        Vector2 size = new Vector2(w, h);
        if (rt.sizeDelta != size) rt.sizeDelta = size;
    }
}
