using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

public class CharacterManager : NetworkBehaviour
{
    [SerializeField] private CharacterProperties[] characters;
    [SerializeField] private CharacterSelectUI selectUI;
    [SerializeField] private SpawnPoint[] spawnPoints;
    [SerializeField] private NetworkPrefabRef playerPrefab;

    private readonly HashSet<int> selectedCharacterIds = new();
    private readonly HashSet<PlayerRef> playersInSelection = new();
    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();

    private void Awake()
    {
        if (characters == null)
            return;

        foreach (var character in characters)
            CharacterProperties.Register(character);
    }

    public void StartSelection(PlayerRef player)
    {
        if (!Object.HasStateAuthority)
            return;

        if (spawnedPlayers.ContainsKey(player))
            return;

        OpenSelectionRpc(player);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void OpenSelectionRpc([RpcTarget] PlayerRef player)
    {
        selectUI.OnSelectedCharacter -= OnSelectCharacter;
        selectUI.OnSelectedCharacter += OnSelectCharacter;

        selectUI.PopulateSelection(characters);
        selectUI.UpdateSelectedCharacters(selectedCharacterIds.ToArray());
        selectUI.OpenMenu();
    }

    private void OnSelectCharacter(int characterId)
    {
        RequestCharacterRpc(characterId);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestCharacterRpc(
        int characterId,
        RpcInfo info = default)
    {
        var player = info.Source;

        if (playersInSelection.Contains(player))
            return;

        if (spawnedPlayers.ContainsKey(player))
            return;

        playersInSelection.Add(player);

        var character =
            characters.FirstOrDefault(x => x.CharacterID == characterId);

        if (character == null)
        {
            SelectionFailedRpc(player, "Invalid character.");
            playersInSelection.Remove(player);
            return;
        }

        if (selectedCharacterIds.Contains(characterId))
        {
            SelectionFailedRpc(player, "Character is already selected.");
            playersInSelection.Remove(player);
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            SelectionFailedRpc(player, "No spawn points configured.");
            playersInSelection.Remove(player);
            return;
        }

        selectedCharacterIds.Add(characterId);

        var spawnPoint =
            spawnPoints[Random.Range(0, spawnPoints.Length)];

        var avatar = Runner.Spawn(
            playerPrefab,
            spawnPoint.GetSpawnPosition(),
            Quaternion.identity,
            player,
            (_, networkObject) =>
            {
                var playerComponent =
                    networkObject.GetComponent<Player>();

                playerComponent.SetCharacter(character);
            });

        spawnedPlayers[player] = avatar;

        SelectionSucceededRpc(player);
        UpdateSelectedCharactersRpc(selectedCharacterIds.ToArray());

        playersInSelection.Remove(player);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SelectionSucceededRpc(
        [RpcTarget] PlayerRef player)
    {
        selectUI.CloseMenu();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SelectionFailedRpc(
        [RpcTarget] PlayerRef player,
        string reason)
    {
        Debug.LogWarning(reason);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void UpdateSelectedCharactersRpc(
        int[] selectedCharacters)
    {
        selectUI.UpdateSelectedCharacters(selectedCharacters);
    }

    public void RemovePlayer(PlayerRef player)
    {
        if (!Object.HasStateAuthority)
            return;

        if (!spawnedPlayers.TryGetValue(player, out var avatar))
            return;

        if (avatar != null)
        {
            var playerComponent = avatar.GetComponent<Player>();

            if (playerComponent != null)
                selectedCharacterIds.Remove(playerComponent.CharacterID);

            Runner.Despawn(avatar);
        }

        spawnedPlayers.Remove(player);
        playersInSelection.Remove(player);

        UpdateSelectedCharactersRpc(selectedCharacterIds.ToArray());
    }

    public Vector3 GetRandomSpawnPosition()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return Vector3.zero;

        return spawnPoints[
            Random.Range(0, spawnPoints.Length)
        ].GetSpawnPosition();
    }
}