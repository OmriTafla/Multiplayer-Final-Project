using System.Collections.Generic;
using System.Linq;
using Fusion;
using Managers;
using UnityEngine;
using UnityEngine.Serialization;

public class CharacterManager : NetworkBehaviour
{
    [FormerlySerializedAs("spawnPoints")]
    [SerializeField] private SpawnPoint[] freeForAllSpawnPoints;
    [SerializeField] private SpawnPoint[] teamZeroSpawnPoints;
    [SerializeField] private SpawnPoint[] teamOneSpawnPoints;
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

        if (!playerPrefab)
            Debug.LogError("CharacterManager requires the Player network prefab", this);
    }

    public void SpawnPlayerAtRandomPoint(PlayerRef player)
    {
        if (!Object.HasStateAuthority || player == PlayerRef.None)
            return;

        if (spawnedPlayers.ContainsKey(player))
            return;

        ResolveReferences();

        if (!playerPrefab)
        {
            SelectionFailedRpc(player, "Player prefab is not assigned.");
            return;
        }

        if (!teamsManager)
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

        if (!TryGetSpawnPosition(teamId, out var spawnPosition))
        {
            teamsManager.HandlePlayerLeft(player);
            SelectionFailedRpc(player, "No valid spawn points are configured for this game mode.");
            return;
        }

        var playerColor = teamsManager.GetPlayerColor(teamId);
        var playerDataObject = Runner.GetPlayerObject(player);

        if (playerDataObject &&
            playerDataObject.TryGetComponent(out UI.PlayerData playerData))
        {
            playerData.SetTeam(teamId, playerColor);
        }

        var avatar = Runner.Spawn(
            playerPrefab,
            spawnPosition,
            Quaternion.identity,
            player);

        if (!avatar)
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
            if (avatar)
                Runner.Despawn(avatar);

            spawnedPlayers.Remove(player);
        }

        if (teamsManager)
            teamsManager.HandlePlayerLeft(player);
    }

    public Vector3 GetSpawnPosition(PlayerRef player)
    {
        ResolveReferences();

        var teamId = -1;

        if (teamsManager)
            teamsManager.TryGetTeam(player, out teamId);

        return TryGetSpawnPosition(teamId, out var spawnPosition)
            ? spawnPosition
            : Vector3.zero;
    }

    public Vector3 GetRandomSpawnPosition()
    {
        return TryGetRandomPosition(freeForAllSpawnPoints, out var spawnPosition)
            ? spawnPosition
            : Vector3.zero;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SelectionFailedRpc([RpcTarget] PlayerRef player, string reason)
    {
        Debug.LogWarning(reason);
    }

    private bool TryGetSpawnPosition(int teamId, out Vector3 spawnPosition)
    {
        if (teamsManager && teamsManager.IsTwoTeams)
        {
            var teamSpawnPoints = teamId == 0
                ? teamZeroSpawnPoints
                : teamOneSpawnPoints;

            if (TryGetRandomPosition(teamSpawnPoints, out spawnPosition))
                return true;
        }

        return TryGetRandomPosition(freeForAllSpawnPoints, out spawnPosition);
    }

    private static bool TryGetRandomPosition(
        SpawnPoint[] spawnPoints,
        out Vector3 spawnPosition)
    {
        spawnPosition = Vector3.zero;

        if (spawnPoints is null || spawnPoints.Length == 0)
            return false;

        var validSpawnPoints = spawnPoints
            .Where(spawnPoint => spawnPoint)
            .ToArray();

        if (validSpawnPoints.Length == 0)
            return false;

        spawnPosition = validSpawnPoints[
            Random.Range(0, validSpawnPoints.Length)].GetSpawnPosition();

        return true;
    }

    private void ResolveReferences()
    {
        if (freeForAllSpawnPoints is null || freeForAllSpawnPoints.Length == 0)
            freeForAllSpawnPoints = FindObjectsByType<SpawnPoint>();

        if (!teamsManager)
            teamsManager = FindAnyObjectByType<TeamsManager>();
    }
}
