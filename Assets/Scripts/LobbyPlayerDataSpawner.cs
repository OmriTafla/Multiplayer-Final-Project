using Fusion;
using UnityEngine;

public class LobbyPlayerDataSpawner : MonoBehaviour
{
    [SerializeField] private NetworkPrefabRef playerDataPrefab;
    [SerializeField] private ActivePlayersUI activePlayersUI;

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        if (runner.GetPlayerObject(player) != null)
            return;

        var playerDataObject = runner.Spawn(
            playerDataPrefab,
            inputAuthority: player);

        runner.SetPlayerObject(player, playerDataObject);

        if (activePlayersUI != null)
            activePlayersUI.UpdateActivePlayers(runner);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        var playerDataObject = runner.GetPlayerObject(player);

        if (playerDataObject != null)
            runner.Despawn(playerDataObject);

        if (activePlayersUI != null)
            activePlayersUI.UpdateActivePlayers(runner);
    }
}