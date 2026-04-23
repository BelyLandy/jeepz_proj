using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SceneLoadManagerConfig", menuName = "Scene Loading/Scene Load Manager Config")]
public sealed class SceneLoadManagerConfig : ScriptableObject
{
    [Serializable]
    public struct SceneEntry
    {
        public GameSceneId sceneId;
        public string sceneName;
    }

    [Header("Scene Registry")]
    [SerializeField] private SceneEntry[] sceneEntries = Array.Empty<SceneEntry>();

    [Header("Initial Scene")]
    [SerializeField] private GameSceneId initialScene = GameSceneId.MainMenu;
    [SerializeField] private bool autoLoadInitialScene = false;

    [Header("Overlay")]
    [SerializeField] private SceneLoadOverlayView overlayPrefab;
    [SerializeField, Min(0f)] private float fadeDuration = 0.2f;
    [SerializeField, Min(0f)] private float minimumOverlayVisibleTime = 0.2f;
    [SerializeField] private bool blockRaycastsDuringLoading = true;
    [SerializeField] private bool useUnscaledTimeForFade = true;

    public SceneEntry[] SceneEntries => sceneEntries;
    public GameSceneId InitialScene => initialScene;
    public bool AutoLoadInitialScene => autoLoadInitialScene;
    public SceneLoadOverlayView OverlayPrefab => overlayPrefab;
    public float FadeDuration => fadeDuration;
    public float MinimumOverlayVisibleTime => minimumOverlayVisibleTime;
    public bool BlockRaycastsDuringLoading => blockRaycastsDuringLoading;
    public bool UseUnscaledTimeForFade => useUnscaledTimeForFade;
}
