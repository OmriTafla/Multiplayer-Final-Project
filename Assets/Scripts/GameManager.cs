using System;
using System.Threading.Tasks;
using Enums;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : PersistentSingleton<GameManager>, IGameManager
{
    public const int MAX_PLAYERS = 20;

    [SerializeField] private string connectionSceneName = "LobbyScene";
    [SerializeField] private IOGameMode gameMode = IOGameMode.TwoTeams;

    private bool returningToMenu;

    public IOGameMode GameMode => NormalizeGameMode(gameMode);
    public bool IsFreeForAll => GameMode == IOGameMode.FreeForAll;
    public bool IsTwoTeams => GameMode == IOGameMode.TwoTeams;


    public void SetGameModeDropDown(int menuItem)
    {
        var mode = (IOGameMode)menuItem;
        SetGameMode(mode);
    }
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
        await ReturnToMenuAsync();
    }

    public async Task ReturnToMenuWithMessageAsync(string message)
    {
        await ReturnToMenuAsync();

        var uiManager = UIManager.Instance;

        if (!uiManager)
            return;

        uiManager.ShowStatus(
            string.IsNullOrWhiteSpace(message)
                ? "The host or server disconnected."
                : message.Trim());

        await Task.Delay(3000);

        if (uiManager)
            uiManager.ShowStartMenu();
    }

    private async Task ReturnToMenuAsync()
    {
        if (returningToMenu)
            return;

        returningToMenu = true;

        try
        {
            var manager = SinglePeer_NetworkRunnerManager.Instance;

            if (manager)
                await manager.ShutdownRunner(false);

            if (SceneManager.GetActiveScene().name != connectionSceneName)
            {
                var loadOperation = SceneManager.LoadSceneAsync(
                    connectionSceneName,
                    LoadSceneMode.Single);

                while (loadOperation is not null && !loadOperation.isDone)
                    await Task.Yield();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            returningToMenu = false;
        }
    }

    private static IOGameMode NormalizeGameMode(IOGameMode mode)
    {
        return mode == IOGameMode.TwoTeams
            ? IOGameMode.TwoTeams
            : IOGameMode.FreeForAll;
    }
}
