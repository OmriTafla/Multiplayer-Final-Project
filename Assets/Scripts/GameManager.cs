using Enums;
using Fusion;
using UnityEngine;

public class GameManager : PersistentSingleton<GameManager>, IGameManager
{
    public const int MAX_PLAYERS = 20; 
    
    [SerializeField] private string connectionSceneName = "LobbyScene";
    
    [field: SerializeField]
    public IOGameMode GameMode {get; private set;}

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
