using UnityEngine;

[DisallowMultipleComponent]
public sealed class FrameRateLimiter : MonoBehaviour
{
    public static FrameRateLimiter Instance { get; private set; }

    public enum VSyncMode
    {
        Off = 0,
        EveryVBlank = 1,
        EverySecondVBlank = 2
    }

    [Header("FPS / VSync")]
    [SerializeField] private int targetFPS = 60;

    [SerializeField] private VSyncMode vSyncMode = VSyncMode.Off;

    public int TargetFPS => targetFPS;
    public VSyncMode CurrentVSyncMode => vSyncMode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ApplySettings();
    }

    private void Start()
    {
        ApplySettings();
    }

    private void OnValidate()
    {
        if (targetFPS < 1)
            targetFPS = 1;

        if (Application.isPlaying && Instance == this)
            ApplySettings();
    }

    public void ApplySettings()
    {
        QualitySettings.vSyncCount = (int)vSyncMode;

        if (vSyncMode == VSyncMode.Off)
            Application.targetFrameRate = targetFPS;
        else
            Application.targetFrameRate = -1;
    }

    public void SetTargetFPS(int fps)
    {
        targetFPS = Mathf.Max(1, fps);
        ApplySettings();
    }

    public void SetVSyncMode(VSyncMode mode)
    {
        vSyncMode = mode;
        ApplySettings();
    }

    public void DisableVSyncAndSetFPS(int fps)
    {
        targetFPS = Mathf.Max(1, fps);
        vSyncMode = VSyncMode.Off;
        ApplySettings();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}