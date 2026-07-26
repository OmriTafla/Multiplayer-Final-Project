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
            return false;

        if (string.IsNullOrWhiteSpace(lobbyName))
        {
            onJoinFailed?.Invoke("Lobby name cannot be empty.");
            return false;
        }

        busy = true;
        onStartJoin?.Invoke();

        var manager = SinglePeer_NetworkRunnerManager.Instance;
        var runner = manager.CreateRunner();

        var result = await runner.JoinSessionLobby(
            SessionLobby.Custom,
            lobbyName.Trim());

        busy = false;

        if (result.Ok)
        {
            LobbyName = lobbyName.Trim();
            onJoinedLobby?.Invoke();
            return true;
        }

        var message = result.ShutdownReason.ToString();

        Debug.LogError($"Lobby join failed: {message}");
        onJoinFailed?.Invoke(message);
        onCancelJoin?.Invoke();

        await manager.RecreateRunner();

        return false;
    }

    public void ExitToLobby()
    {
        _ = ExitToLobbyAsync();
    }

    public async Task ExitToLobbyAsync()
    {
        if (busy)
            return;

        busy = true;

        var savedLobbyName = LobbyName;
        var manager = SinglePeer_NetworkRunnerManager.Instance;

        await manager.RecreateRunner();

        busy = false;

        if (!string.IsNullOrWhiteSpace(savedLobbyName))
            await JoinLobbyAsync(savedLobbyName);
    }
}