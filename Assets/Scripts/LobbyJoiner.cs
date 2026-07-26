using System;
using System.Threading.Tasks;
using Fusion;
using Singleton;
using UnityEngine;
using UnityEngine.Events;

public class LobbyJoiner : Singleton<LobbyJoiner>
{
    [SerializeField] private UnityEvent onJoinedLobby;
    [SerializeField] private UnityEvent onStartJoin;
    [SerializeField] private UnityEvent onCancelJoin;
    [SerializeField] private UnityEvent<string> onJoinFailed;

    [field: SerializeField]
    public string LobbyName { get; private set; }

    private bool busy;

    public bool IsBusy => busy;

    public void JoinLobby(string lobbyName)
    {
        _ = JoinLobbyAsync(lobbyName);
    }

    public async Task<bool> JoinLobbyAsync(string lobbyName)
    {
        if (busy)
        {
            Debug.LogWarning("Lobby operation is already in progress");
            return false;
        }

        if (string.IsNullOrWhiteSpace(lobbyName))
        {
            const string lobbyNameError = "Lobby name cannot be empty";
            Debug.LogError(lobbyNameError);
            onJoinFailed?.Invoke(lobbyNameError);
            return false;
        }

        busy = true;
        onStartJoin?.Invoke();

        try
        {
            return await JoinLobbyInternalAsync(lobbyName.Trim());
        }
        finally
        {
            busy = false;
        }
    }

    public void ExitToLobby()
    {
        Debug.Log("Change Room button pressed");
        _ = ExitToLobbyAsync();
    }

    public async Task ExitToLobbyAsync()
    {
        if (busy)
        {
            Debug.LogWarning("Cannot change room because another lobby operation is already in progress");
            return;
        }

        busy = true;

        var lobbyName = LobbyName;
        var uiManager = UIManager.Instance;

        uiManager?.ShowWaitingScreen();

        try
        {
            if (string.IsNullOrWhiteSpace(lobbyName))
            {
                const string lobbyNameError = "Cannot change room because no lobby name was saved";
                Debug.LogError(lobbyNameError);
                onJoinFailed?.Invoke(lobbyNameError);
                onCancelJoin?.Invoke();
                uiManager?.ShowLobbyMenu();
                return;
            }

            var manager = SinglePeer_NetworkRunnerManager.Instance;

            if (manager == null)
            {
                const string managerError = "No active SinglePeer_NetworkRunnerManager exists";
                Debug.LogError(managerError);
                onJoinFailed?.Invoke(managerError);
                onCancelJoin?.Invoke();
                uiManager?.ShowLobbyMenu();
                return;
            }

            Debug.Log("Leaving current session...");
            await manager.ShutdownRunner(false);

            var runner = manager.CreateRunner();

            if (runner == null)
            {
                const string runnerError = "The NetworkRunner could not be recreated";
                Debug.LogError(runnerError);
                onJoinFailed?.Invoke(runnerError);
                onCancelJoin?.Invoke();
                uiManager?.ShowLobbyMenu();
                return;
            }

            Debug.Log($"Rejoining lobby '{lobbyName}'...");

            var result = await runner.JoinSessionLobby(
                SessionLobby.Custom,
                lobbyName);

            if (!result.Ok)
            {
                var failureMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? result.ShutdownReason.ToString()
                    : result.ErrorMessage;

                Debug.LogError($"Could not return to lobby '{lobbyName}': {failureMessage}");
                onJoinFailed?.Invoke(failureMessage);
                onCancelJoin?.Invoke();
                uiManager?.ShowLobbyMenu();
                await manager.RecreateRunner();
                return;
            }

            LobbyName = lobbyName;
            Debug.Log($"Left the session and returned to lobby '{LobbyName}'");
            uiManager?.ShowSessionsMenu();
            onJoinedLobby?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            onJoinFailed?.Invoke(exception.Message);
            onCancelJoin?.Invoke();
            uiManager?.ShowLobbyMenu();

            var manager = SinglePeer_NetworkRunnerManager.Instance;

            if (manager != null)
                await manager.RecreateRunner();
        }
        finally
        {
            busy = false;
        }
    }

    private async Task<bool> JoinLobbyInternalAsync(string lobbyName)
    {
        var manager = SinglePeer_NetworkRunnerManager.Instance;

        if (manager == null)
        {
            const string managerError = "No active SinglePeer_NetworkRunnerManager exists";
            Debug.LogError(managerError);
            onJoinFailed?.Invoke(managerError);
            onCancelJoin?.Invoke();
            return false;
        }

        var runner = manager.CreateRunner();

        if (runner == null)
        {
            const string runnerError = "The NetworkRunner could not be created";
            Debug.LogError(runnerError);
            onJoinFailed?.Invoke(runnerError);
            onCancelJoin?.Invoke();
            return false;
        }

        try
        {
            Debug.Log($"Calling JoinSessionLobby for '{lobbyName}'");

            var result = await runner.JoinSessionLobby(
                SessionLobby.Custom,
                lobbyName);

            if (result.Ok)
            {
                LobbyName = lobbyName;
                Debug.Log($"JoinSessionLobby succeeded for '{LobbyName}'");
                onJoinedLobby?.Invoke();
                return true;
            }

            var failureMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? result.ShutdownReason.ToString()
                : result.ErrorMessage;

            Debug.LogError($"JoinSessionLobby failed: {failureMessage}");
            onJoinFailed?.Invoke(failureMessage);
            onCancelJoin?.Invoke();
            await manager.RecreateRunner();
            return false;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            onJoinFailed?.Invoke(exception.Message);
            onCancelJoin?.Invoke();
            await manager.RecreateRunner();
            return false;
        }
    }
}
