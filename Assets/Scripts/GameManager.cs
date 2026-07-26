using System;
using Fusion;
using UnityEngine;

public class GameManager : PersistentSingleton<GameManager>, IGameManager
{
    [SerializeField] private string gameScenePath = "Assets/Scenes/GameScene.unity";
    [SerializeField] private string connectionSceneName = "LobbyScene";

    public void QuitGame()
    {
        Debug.Log("Quitting game");
        Application.Quit();
    }

    public async void StartGame()
    {
        var manager = SinglePeer_NetworkRunnerManager.Instance;
        var runner = manager != null ? manager.NetworkRunner : null;

        if (runner == null || !runner.IsRunning)
        {
            Debug.LogWarning("Cannot start game because no NetworkRunner is running");
            return;
        }

        if (!runner.IsServer)
        {
            Debug.LogWarning("Only the Host or dedicated server can start the game");
            return;
        }

        var buildIndex = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(gameScenePath);

        if (buildIndex < 0)
        {
            Debug.LogError($"Game scene is not included in Build Profiles: {gameScenePath}");
            return;
        }

        var sceneRef = SceneRef.FromIndex(buildIndex);

        runner.SessionInfo.IsOpen = false;

        if (UIManager.Instance != null)
            UIManager.Instance.ShowWaitingScreen();

        try
        {
            Debug.Log($"Loading network game scene at build index {buildIndex}: {gameScenePath}");

            await runner.LoadScene(
                sceneRef,
                UnityEngine.SceneManagement.LoadSceneMode.Single);

            Debug.Log("Network game scene loaded");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
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
