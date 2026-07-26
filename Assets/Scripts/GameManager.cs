using Fusion;
using UnityEngine;

public class GameManager : PersistentSingleton<GameManager>, IGameManager
{
    [SerializeField] private string connectionSceneName = "LobbyScene";

    public void QuitGame()
    {
        Debug.Log("Quitting game");
        Application.Quit();
    }

    public void StartGame()
    {
        Debug.Log("The persistent world starts automatically. No manual match start is required.");
    }

    public async void ReturnToMenu()
    {
        var manager = SinglePeer_NetworkRunnerManager.Instance;

        if (manager != null)
            await manager.ShutdownRunner(false);

        UnityEngine.SceneManagement.SceneManager.LoadScene(
            connectionSceneName,
            UnityEngine.SceneManagement.LoadSceneMode.Single);

        if (manager != null)
            manager.CreateRunner();
    }
}
