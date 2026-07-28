using System.Collections.Generic;
using System.Linq;
using Fusion;
using Managers;
using UnityEngine;

public class CharacterManager : NetworkBehaviour
{
    [SerializeField] private SpawnPoint[] spawnPoints;
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private TeamsManager teamsManager;

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();

    private void OnValidate()
    {
        ResolveReferences();
    }

    public override void Spawned()
    {
        ResolveReferences();

        if (playerPrefab == null)
            Debug.LogError("CharacterManager requires the Player network prefab", this);
    }

    public void SpawnPlayerAtRandomPoint(PlayerRef player)
    {
        if (!Object.HasStateAuthority || player == PlayerRef.None)
            return;

        if (spawnedPlayers.ContainsKey(player))
            return;

        ResolveReferences();

        var validSpawnPoints = spawnPoints
            .Where(spawnPoint => spawnPoint != null)
            .ToArray();

        if (validSpawnPoints.Length == 0)
        {
            SelectionFailedRpc(player, "No valid spawn points configured.");
            return;
        }

        if (playerPrefab == null)
        {
            SelectionFailedRpc(player, "Player prefab is not assigned.");
            return;
        }

        if (teamsManager == null)
        {
            SelectionFailedRpc(player, "TeamsManager is not assigned.");
            return;
        }

        var teamId = teamsManager.AutoAssignPlayerToTeam(player);

        if (teamId < 0)
        {
            SelectionFailedRpc(player, "Could not assign the player to a team.");
            return;
        }

        var playerColor = teamsManager.GetPlayerColor(teamId);
        var playerDataObject = Runner.GetPlayerObject(player);

        if (playerDataObject != null &&
            playerDataObject.TryGetComponent(out UI.PlayerData playerData))
        {
            playerData.SetTeam(teamId, playerColor);
        }

        var spawnPoint = validSpawnPoints[Random.Range(0, validSpawnPoints.Length)];
        var avatar = Runner.Spawn(
            playerPrefab,
            spawnPoint.GetSpawnPosition(),
            Quaternion.identity,
            player);

        if (avatar == null)
        {
            teamsManager.HandlePlayerLeft(player);
            SelectionFailedRpc(player, "Could not spawn the player prefab.");
            return;
        }

        if (!avatar.TryGetComponent(out Player playerAvatar))
        {
            Runner.Despawn(avatar);
            teamsManager.HandlePlayerLeft(player);
            SelectionFailedRpc(player, "The Player prefab is missing the Player component.");
            return;
        }

        playerAvatar.SetTeam(teamId, playerColor);
        spawnedPlayers[player] = avatar;
    }

    public void RemovePlayer(PlayerRef player)
    {
        if (!Object.HasStateAuthority)
            return;

        if (spawnedPlayers.TryGetValue(player, out var avatar))
        {
            if (avatar != null)
                Runner.Despawn(avatar);

            spawnedPlayers.Remove(player);
        }

        if (teamsManager != null)
            teamsManager.HandlePlayerLeft(player);
    }

    public Vector3 GetRandomSpawnPosition()
    {
        ResolveReferences();

        var validSpawnPoints = spawnPoints
            .Where(spawnPoint => spawnPoint != null)
            .ToArray();

        if (validSpawnPoints.Length == 0)
            return Vector3.zero;

        return validSpawnPoints[Random.Range(0, validSpawnPoints.Length)].GetSpawnPosition();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SelectionFailedRpc([RpcTarget] PlayerRef player, string reason)
    {
        Debug.LogWarning(reason);
    }

    private void ResolveReferences()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);

        if (teamsManager == null)
            teamsManager = FindAnyObjectByType<TeamsManager>();
    }
}
