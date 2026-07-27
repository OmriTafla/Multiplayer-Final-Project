using Enums;
using UnityEngine;

public class GameManager : PersistentSingleton<GameManager>, IGameManager
{
    public const int MAX_PLAYERS = 32;

    [SerializeField] private string connectionSceneName = "LobbyScene";
    [SerializeField] private IOGameMode gameMode = IOGameMode.TwoTeams;

    public IOGameMode GameMode => gameMode;

    public void QuitGame()
    {
        Application.Quit();
    }

    public void StartGame()
    {
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
