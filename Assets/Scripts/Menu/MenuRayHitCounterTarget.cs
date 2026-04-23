using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MenuRayHitCounterTarget : MonoBehaviour
{
    public enum ThresholdAction
    {
        None = 0,
        LoadSceneViaLoading = 1,
        QuitGame = 2,
    }

    [Header("Hit Count")]
    [SerializeField, Min(1)] private int requiredHits = 3;
    [SerializeField] private GameObject objectToHide;

    [Header("Disappear Mode")]
    [SerializeField] private bool destroyInsteadOfHide = false;

    [Header("Post Threshold Action")]
    [SerializeField] private ThresholdAction thresholdAction = ThresholdAction.None;
    [SerializeField] private GameSceneId loadingTargetScene = GameSceneId.GameplayScene;
    [SerializeField, Min(0f)] private float actionDelayAfterThreshold = 0.15f;

    private int currentHits;
    private bool thresholdReached;

    public int CurrentHits => currentHits;
    public int RequiredHits => requiredHits;
    public bool ThresholdReached => thresholdReached;

    private void OnValidate()
    {
        requiredHits = Mathf.Max(1, requiredHits);
        actionDelayAfterThreshold = Mathf.Max(0f, actionDelayAfterThreshold);
    }

    public void RegisterRayHit()
    {
        if (thresholdReached)
            return;

        currentHits++;
        if (currentHits < requiredHits)
            return;

        thresholdReached = true;

        GameObject targetObject = objectToHide != null ? objectToHide : gameObject;
        if (destroyInsteadOfHide)
            Destroy(targetObject);
        else
            targetObject.SetActive(false);

        if (thresholdAction != ThresholdAction.None)
            StartCoroutine(ThresholdActionRoutine());
    }

    private IEnumerator ThresholdActionRoutine()
    {
        if (actionDelayAfterThreshold > 0f)
            yield return new WaitForSecondsRealtime(actionDelayAfterThreshold);

        SceneLoadManager manager = SceneLoadManager.EnsureInstance();
        if (manager == null)
        {
            Debug.LogError("[MenuRayHitCounterTarget] Could not create or find SceneLoadManager for threshold action.");
            yield break;
        }

        switch (thresholdAction)
        {
            case ThresholdAction.LoadSceneViaLoading:
                manager.LoadSceneViaLoadingScene(loadingTargetScene);
                break;

            case ThresholdAction.QuitGame:
                manager.QuitGame();
                break;
        }
    }
}
