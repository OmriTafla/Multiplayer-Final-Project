using System.Collections.Generic;
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
    [SerializeField] private CharacterProperties[] characters;
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private TeamsManager teamsManager;

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();

    private void Awake()
    {
        CharacterProperties.InitializeRegistry(characters);
    }

    private void OnValidate()
    {
        ValidateReferences();
    }

    public override void Spawned()
    {
        ValidateReferences();

        if (!playerPrefab)
            Debug.LogError("CharacterManager requires the Player network prefab", this);
    }

    public void SpawnPlayerAtRandomPoint(PlayerRef player)
    {
        if (!Object.HasStateAuthority || player == PlayerRef.None)
            return;

        if (spawnedPlayers.ContainsKey(player))
            return;

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

        SpawnPoint selectedSpawnPoint = null;
        var validSpawnPointCount = 0;

        foreach (var spawnPoint in spawnPoints)
        {
            if (!spawnPoint)
                continue;

            validSpawnPointCount++;

            if (Random.Range(0, validSpawnPointCount) == 0)
                selectedSpawnPoint = spawnPoint;
        }

        if (!selectedSpawnPoint)
            return false;

        spawnPosition = selectedSpawnPoint.GetSpawnPosition();

        return true;
    }

    private void ValidateReferences()
    {
        if (freeForAllSpawnPoints is null || freeForAllSpawnPoints.Length == 0)
            Debug.LogError("CharacterManager requires free-for-all spawn points", this);

        if (!teamsManager)
            Debug.LogError("CharacterManager requires a TeamsManager reference", this);
    }
}
