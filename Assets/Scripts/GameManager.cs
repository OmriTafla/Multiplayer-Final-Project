using Enums;
using UnityEngine;

public class GameManager : PersistentSingleton<GameManager>, IGameManager
{
    public const int MAX_PLAYERS = 32;

    [SerializeField] private string connectionSceneName = "LobbyScene";
    [SerializeField] private IOGameMode gameMode = IOGameMode.TwoTeams;

    public IOGameMode GameMode => NormalizeGameMode(gameMode);
    public bool IsFreeForAll => GameMode == IOGameMode.FreeForAll;
    public bool IsTwoTeams => GameMode == IOGameMode.TwoTeams;

    public void SetGameMode(IOGameMode newGameMode)
    {
        gameMode = NormalizeGameMode(newGameMode);
    }

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

    private static IOGameMode NormalizeGameMode(IOGameMode mode)
    {
        return mode == IOGameMode.TwoTeams
            ? IOGameMode.TwoTeams
            : IOGameMode.FreeForAll;
    }
}
