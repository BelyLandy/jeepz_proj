using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-1000)]
public sealed class SceneLoadManager : MonoBehaviour
{
    private const string DefaultConfigResourcePath = "SceneLoadManagerConfig";

    public static SceneLoadManager Instance { get; private set; }

    [Header("Optional Config Override")]
    [SerializeField] private SceneLoadManagerConfig configOverride;

    private SceneLoadManagerConfig config;
    private SceneLoadOverlayView overlayInstance;
    private CanvasGroup overlayCanvasGroup;
    private bool initialized;
    private bool autoCreatedInstance;
    private bool hasAutoLoadBeenEvaluated;

    private bool isLoading;
    private bool isLoadingViaLoadingScene;
    private bool hasPendingTargetScene;
    private bool hasQueuedPendingSceneLoad;
    private GameSceneId pendingTargetScene = GameSceneId.GameplayScene;

    public bool IsLoading => isLoading || isLoadingViaLoadingScene || hasPendingTargetScene || hasQueuedPendingSceneLoad;

    public static SceneLoadManager EnsureInstance()
    {
        if (Instance != null)
        {
            Instance.InitializeIfNeeded();
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER
        SceneLoadManager found = FindFirstObjectByType<SceneLoadManager>();
#else
        SceneLoadManager found = FindObjectOfType<SceneLoadManager>();
#endif
        if (found != null)
        {
            Instance = found;
            Instance.InitializeIfNeeded();
            return Instance;
        }

        GameObject go = new GameObject("SceneLoadManager_AutoCreated");
        SceneLoadManager manager = go.AddComponent<SceneLoadManager>();
        manager.autoCreatedInstance = true;
        manager.InitializeIfNeeded();
        return manager;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeIfNeeded();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (overlayInstance != null && overlayInstance.gameObject != null)
            Destroy(overlayInstance.gameObject);

        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        EvaluateAutoLoadInitialSceneIfNeeded();
    }

    private void EvaluateAutoLoadInitialSceneIfNeeded()
    {
        if (hasAutoLoadBeenEvaluated)
            return;

        hasAutoLoadBeenEvaluated = true;
        if (autoCreatedInstance)
            return;

        InitializeIfNeeded();
        if (config == null || !config.AutoLoadInitialScene)
            return;

        if (!TryGetSceneName(config.InitialScene, out string initialSceneName))
            return;

        string activeSceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(activeSceneName, initialSceneName, StringComparison.Ordinal))
            return;

        LoadScene(config.InitialScene);
    }

    public void LoadScene(GameSceneId sceneId)
    {
        EnsureInstance();
        if (Instance.IsLoading)
            return;

        if (!Instance.TryGetSceneName(sceneId, out string sceneName))
        {
            Debug.LogError($"[SceneLoadManager] Scene mapping for '{sceneId}' was not found.");
            return;
        }

        Instance.StartCoroutine(Instance.LoadSceneRoutine(sceneName));
    }

    public void LoadSceneViaLoadingScene(GameSceneId targetScene)
    {
        EnsureInstance();
        if (Instance.IsLoading)
            return;

        if (targetScene == GameSceneId.Loading)
        {
            Instance.LoadScene(GameSceneId.Loading);
            return;
        }

        if (!Instance.TryGetSceneName(GameSceneId.Loading, out string loadingSceneName))
        {
            Debug.LogError("[SceneLoadManager] Scene mapping for 'Loading' was not found.");
            return;
        }

        if (!Instance.TryGetSceneName(targetScene, out _))
        {
            Debug.LogError($"[SceneLoadManager] Scene mapping for '{targetScene}' was not found.");
            return;
        }

        Instance.pendingTargetScene = targetScene;
        Instance.hasPendingTargetScene = true;
        Instance.isLoadingViaLoadingScene = true;
        Instance.hasQueuedPendingSceneLoad = false;
        Instance.StartCoroutine(Instance.LoadSceneRoutine(loadingSceneName));
    }

    public void LoadSceneViaLoadingScene(GameSceneId targetScene, float delay)
    {
        EnsureInstance();
        if (delay <= 0f)
        {
            Instance.LoadSceneViaLoadingScene(targetScene);
            return;
        }

        if (Instance.IsLoading)
            return;

        Instance.StartCoroutine(Instance.DelayedLoadViaLoadingSceneRoutine(targetScene, delay));
    }

    public void ReloadCurrentScene()
    {
        EnsureInstance();
        if (Instance.IsLoading)
            return;

        string currentSceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrWhiteSpace(currentSceneName))
        {
            Debug.LogError("[SceneLoadManager] Cannot reload current scene because its name is empty.");
            return;
        }

        Instance.StartCoroutine(Instance.LoadSceneRoutine(currentSceneName));
    }

    public void LoadMainMenu()
    {
        LoadScene(GameSceneId.MainMenu);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public bool TryGetSceneName(GameSceneId sceneId, out string sceneName)
    {
        InitializeIfNeeded();

        if (config != null)
        {
            SceneLoadManagerConfig.SceneEntry[] entries = config.SceneEntries;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].sceneId != sceneId)
                    continue;

                sceneName = entries[i].sceneName;
                return !string.IsNullOrWhiteSpace(sceneName);
            }
        }

        sceneName = GetFallbackSceneName(sceneId);
        return !string.IsNullOrWhiteSpace(sceneName);
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
            return;

        if (config == null)
            config = configOverride != null ? configOverride : Resources.Load<SceneLoadManagerConfig>(DefaultConfigResourcePath);

        if (config == null)
            Debug.LogWarning($"[SceneLoadManager] Could not find SceneLoadManagerConfig at Resources/{DefaultConfigResourcePath}. Falling back to enum scene names and no overlay.");

        if (overlayInstance == null && config != null && config.OverlayPrefab != null)
        {
            overlayInstance = Instantiate(config.OverlayPrefab);
            overlayInstance.name = config.OverlayPrefab.name;
            DontDestroyOnLoad(overlayInstance.gameObject);
            overlayCanvasGroup = overlayInstance.CanvasGroup;
        }
        else if (overlayInstance != null)
        {
            overlayCanvasGroup = overlayInstance.CanvasGroup;
        }

        ApplyOverlayHiddenStateImmediate();
        initialized = true;
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        InitializeIfNeeded();
        isLoading = true;

        float overlayShownTimestamp = GetTime();
        if (overlayCanvasGroup != null)
        {
            if (GetBlockRaycastsDuringLoading())
                overlayCanvasGroup.blocksRaycasts = true;

            overlayCanvasGroup.interactable = false;
            yield return FadeOverlay(overlayCanvasGroup.alpha, 1f, GetFadeDuration());
            overlayShownTimestamp = GetTime();
        }

        AsyncOperation operation;
        try
        {
            operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SceneLoadManager] Failed to start async load for scene '{sceneName}'.\n{ex}");
            ApplyOverlayHiddenStateImmediate();
            ResetLoadingStateAfterFailure();
            yield break;
        }

        if (operation == null)
        {
            Debug.LogError($"[SceneLoadManager] SceneManager.LoadSceneAsync returned null for {sceneName}.");
            ApplyOverlayHiddenStateImmediate();
            ResetLoadingStateAfterFailure();
            yield break;
        }

        while (!operation.isDone)
            yield return null;

        if (overlayCanvasGroup != null && GetMinimumOverlayVisibleTime() > 0f)
        {
            float visibleFor = GetTime() - overlayShownTimestamp;
            while (visibleFor < GetMinimumOverlayVisibleTime())
            {
                yield return null;
                visibleFor = GetTime() - overlayShownTimestamp;
            }
        }

        if (overlayCanvasGroup != null)
        {
            yield return FadeOverlay(overlayCanvasGroup.alpha, 0f, GetFadeDuration());
            overlayCanvasGroup.interactable = false;
            overlayCanvasGroup.blocksRaycasts = false;
        }

        isLoading = false;
    }

    private IEnumerator DelayedLoadViaLoadingSceneRoutine(GameSceneId targetScene, float delay)
    {
        float elapsed = 0f;
        while (elapsed < delay)
        {
            elapsed += GetDeltaTime();
            yield return null;
        }

        LoadSceneViaLoadingScene(targetScene);
    }

    private IEnumerator ContinuePendingTargetSceneAfterLoadingSceneRoutine()
    {
        while (isLoading)
            yield return null;

        if (!isLoadingViaLoadingScene || !hasPendingTargetScene)
        {
            hasQueuedPendingSceneLoad = false;
            yield break;
        }

        GameSceneId targetScene = pendingTargetScene;
        if (!TryGetSceneName(targetScene, out string targetSceneName))
        {
            Debug.LogError($"[SceneLoadManager] Scene mapping for '{targetScene}' was not found when continuing from Loading.");
            ResetLoadingStateAfterFailure();
            hasQueuedPendingSceneLoad = false;
            yield break;
        }

        yield return LoadSceneRoutine(targetSceneName);

        hasPendingTargetScene = false;
        isLoadingViaLoadingScene = false;
        hasQueuedPendingSceneLoad = false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isLoadingViaLoadingScene || !hasPendingTargetScene || hasQueuedPendingSceneLoad)
            return;

        if (!TryGetSceneName(GameSceneId.Loading, out string loadingSceneName))
            return;

        if (!string.Equals(scene.name, loadingSceneName, StringComparison.Ordinal))
            return;

        hasQueuedPendingSceneLoad = true;
        StartCoroutine(ContinuePendingTargetSceneAfterLoadingSceneRoutine());
    }

    private IEnumerator FadeOverlay(float from, float to, float duration)
    {
        if (overlayCanvasGroup == null)
            yield break;

        if (duration <= 0f)
        {
            overlayCanvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        overlayCanvasGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);
            overlayCanvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        overlayCanvasGroup.alpha = to;
    }

    private float GetFadeDuration() => config != null ? config.FadeDuration : 0.2f;
    private float GetMinimumOverlayVisibleTime() => config != null ? config.MinimumOverlayVisibleTime : 0.2f;
    private bool GetBlockRaycastsDuringLoading() => config == null || config.BlockRaycastsDuringLoading;
    private bool UseUnscaledTimeForFade() => config == null || config.UseUnscaledTimeForFade;
    private float GetTime() => UseUnscaledTimeForFade() ? Time.unscaledTime : Time.time;
    private float GetDeltaTime() => UseUnscaledTimeForFade() ? Time.unscaledDeltaTime : Time.deltaTime;

    private void ApplyOverlayHiddenStateImmediate()
    {
        if (overlayCanvasGroup == null)
            return;

        overlayCanvasGroup.alpha = 0f;
        overlayCanvasGroup.interactable = false;
        overlayCanvasGroup.blocksRaycasts = false;
    }

    private string GetFallbackSceneName(GameSceneId sceneId)
    {
        switch (sceneId)
        {
            case GameSceneId.MainMenu:
                return "MainMenu";
            case GameSceneId.Loading:
                return "Loading";
            case GameSceneId.GameplayScene:
                return "GameplayScene";
            default:
                return string.Empty;
        }
    }

    private void ResetLoadingStateAfterFailure()
    {
        isLoading = false;
        isLoadingViaLoadingScene = false;
        hasPendingTargetScene = false;
        hasQueuedPendingSceneLoad = false;
    }
}
