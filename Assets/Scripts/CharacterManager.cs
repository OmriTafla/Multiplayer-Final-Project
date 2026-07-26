using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

public class CharacterManager : NetworkBehaviour
{
    [SerializeField] private SpawnPoint[] spawnPoints;
    [SerializeField] private NetworkObject playerPrefab;

    private readonly HashSet<int> selectedCharacterIds = new();
    private readonly HashSet<PlayerRef> playersInSelection = new();
    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();
    private PlayerRef localSelectingPlayer;

    private void OnValidate()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
    }

    public override void Spawned()
    {
        if (playerPrefab == null)
            Debug.LogError("CharacterManager requires the Player network prefab", this);
    }

    public void StartSelection(PlayerRef player)
    {
        if (!Object.HasStateAuthority)
            return;

        if (spawnedPlayers.ContainsKey(player) || playersInSelection.Contains(player))
            return;

        playersInSelection.Add(player);
    }

    private void OnSelectCharacter(int characterId)
    {
        RequestCharacterRpc(localSelectingPlayer, characterId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RequestCharacterRpc(PlayerRef requestedPlayer, int characterId, RpcInfo info = default)
    {
        var player = info.Source == PlayerRef.None ? requestedPlayer : info.Source;

        if (player == PlayerRef.None)
        {
            Debug.LogWarning("Character selection request had no player source", this);
            return;
        }

        if (info.Source != PlayerRef.None && requestedPlayer != info.Source)
            return;

        if (!playersInSelection.Contains(player) || spawnedPlayers.ContainsKey(player))
            return;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            SelectionFailedRpc(player, "No spawn points configured.");
            return;
        }

        if (playerPrefab == null)
        {
            SelectionFailedRpc(player, "Player prefab is not assigned.");
            return;
        }

        var validSpawnPoints = spawnPoints.Where(x => x != null).ToArray();

        if (validSpawnPoints.Length == 0)
        {
            SelectionFailedRpc(player, "No valid spawn points configured.");
            return;
        }

        var spawnPoint = validSpawnPoints[UnityEngine.Random.Range(0, validSpawnPoints.Length)];

        var avatar = Runner.Spawn(
            playerPrefab,
            spawnPoint.GetSpawnPosition(),
            Quaternion.identity,
            player);

        if (avatar == null)
        {
            SelectionFailedRpc(player, "Could not spawn the player prefab.");
            return;
        }

        spawnedPlayers[player] = avatar;
        playersInSelection.Remove(player);

    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SelectionFailedRpc([RpcTarget] PlayerRef player, string reason)
    {
        Debug.LogWarning(reason);
    }

    public void RemovePlayer(PlayerRef player)
    {
        if (!Object.HasStateAuthority)
            return;

        playersInSelection.Remove(player);

        if (!spawnedPlayers.TryGetValue(player, out var avatar))
            return;

        if (avatar != null)
        {
            Runner.Despawn(avatar);
        }

        spawnedPlayers.Remove(player);
    }

    public Vector3 GetRandomSpawnPosition()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return Vector3.zero;

        var validSpawnPoints = spawnPoints.Where(x => x != null).ToArray();

        if (validSpawnPoints.Length == 0)
            return Vector3.zero;

        return validSpawnPoints[UnityEngine.Random.Range(0, validSpawnPoints.Length)].GetSpawnPosition();
    }
}
