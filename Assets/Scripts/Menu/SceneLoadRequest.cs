using UnityEngine;

public sealed class SceneLoadRequest : MonoBehaviour
{
    public enum RequestType
    {
        LoadScene = 0,
        ReloadCurrent = 1,
        LoadMainMenu = 2,
        QuitGame = 3,
    }

    [SerializeField] private RequestType requestType = RequestType.LoadScene;
    [SerializeField] private GameSceneId targetScene = GameSceneId.GameplayScene;

    public void ExecuteRequest()
    {
        SceneLoadManager manager = SceneLoadManager.EnsureInstance();
        if (manager == null)
        {
            Debug.LogError("[SceneLoadRequest] Could not create or find SceneLoadManager.");
            return;
        }

        switch (requestType)
        {
            case RequestType.LoadScene:
                manager.LoadScene(targetScene);
                break;
            case RequestType.ReloadCurrent:
                manager.ReloadCurrentScene();
                break;
            case RequestType.LoadMainMenu:
                manager.LoadMainMenu();
                break;
            case RequestType.QuitGame:
                manager.QuitGame();
                break;
            default:
                Debug.LogError($"[SceneLoadRequest] Unsupported request type: {requestType}");
                break;
        }
    }

    public void LoadTargetScene()
    {
        requestType = RequestType.LoadScene;
        ExecuteRequest();
    }
}
