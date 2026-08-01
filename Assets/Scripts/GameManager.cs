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
                SceneManager.LoadScene(
                    connectionSceneName,
                    LoadSceneMode.Single);
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
