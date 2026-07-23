using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class LobbyPlayerDataSpawner : MonoBehaviour
{
    [SerializeField] private NetworkPrefabRef playerDataPrefab;
    [SerializeField] private ActivePlayersUI activePlayersUI;

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsSceneAuthority) return;

        var dataObject = runner.Spawn(playerDataPrefab, inputAuthority: player);
        runner.SetPlayerObject(player, dataObject);

        activePlayersUI.UpdateActivePlayers(runner);
    }
}