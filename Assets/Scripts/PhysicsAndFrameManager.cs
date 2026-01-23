using UnityEngine;

public class FrameRateLimiter : MonoBehaviour
{
    [Header("Настройки FPS и VSync")]
    [Tooltip("Целевой FPS (работает только если VSync выключен)")]
    public int targetFPS = 60;

    [Tooltip("Количество вертикальных синхронизаций (0 = выкл, 1 = 1 кадр, 2 = 2 кадра и т.д.)")]
    public int vSyncCount = 0;

    void Start()
    {
        ApplySettings();
    }

    void OnValidate()
    {
        ApplySettings();
    }

    void ApplySettings()
    {
        QualitySettings.vSyncCount = vSyncCount;

        if (vSyncCount == 0)
        {
            Application.targetFrameRate = targetFPS;
        }
        else
        {
            Application.targetFrameRate = -1;
        }

        Debug.Log($"Настройки применены: VSync={vSyncCount}, FPS={Application.targetFrameRate}");
    }
}
