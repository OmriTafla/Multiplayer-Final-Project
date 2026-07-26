using Fusion;
using Singleton;
using UnityEngine;
using UnityEngine.Events;

public class MatchManager : Singleton<MatchManager>
{
    [SerializeField] private CharacterManager characterManager;
    [SerializeField] private PlacementManager placementManager;
    [SerializeField] private UnityEvent onMatchEnded;

    public void OnSceneLoaded(NetworkRunner runner)
    {
        if (!runner.IsServer)
            return;

        foreach (var player in runner.ActivePlayers)
            characterManager.StartSelection(player);
    }

    public void OnPlayerJoined(
        NetworkRunner runner,
        PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        characterManager.StartSelection(player);
    }

    public void OnPlayerLeft(
        NetworkRunner runner,
        PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        characterManager.RemovePlayer(player);
    }

    public void EndMatch()
    {
        var runner =
            SinglePeer_NetworkRunnerManager.Instance.NetworkRunner;

        if (runner == null || !runner.IsServer)
            return;

        onMatchEnded?.Invoke();
    }

    public Vector3 GetRandomSpawnPosition()
    {
        return characterManager.GetRandomSpawnPosition();
    }
}