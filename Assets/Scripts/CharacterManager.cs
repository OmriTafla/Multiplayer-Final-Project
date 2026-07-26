using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

public class CharacterManager : NetworkBehaviour
{
    [SerializeField] private CharacterProperties[] characters;
    [SerializeField] private CharacterSelectUI selectUI;
    [SerializeField] private SpawnPoint[] spawnPoints;
    [SerializeField] private NetworkObject playerPrefab;

    private readonly HashSet<int> selectedCharacterIds = new();
    private readonly HashSet<PlayerRef> playersInSelection = new();
    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();
    private PlayerRef localSelectingPlayer;

    private void Awake()
    {
        RegisterCharacters();
    }

    private void OnValidate()
    {
        if (selectUI == null)
            selectUI = FindAnyObjectByType<CharacterSelectUI>();

        if (spawnPoints == null || spawnPoints.Length == 0)
            spawnPoints = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
    }

    public override void Spawned()
    {
        RegisterCharacters();

        if (selectUI == null)
            Debug.LogError("CharacterManager requires a CharacterSelectUI reference", this);

        if (playerPrefab == null)
            Debug.LogError("CharacterManager requires the Player network prefab", this);
    }

    public void StartSelection(PlayerRef player)
    {
        if (!Object.HasStateAuthority)
            return;

        if (spawnedPlayers.ContainsKey(player))
            return;

        if (selectUI == null)
        {
            Debug.LogError("CharacterManager cannot open selection because CharacterSelectUI is missing", this);
            return;
        }

        OpenSelectionRpc(player);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void OpenSelectionRpc([RpcTarget] PlayerRef player)
    {
        if (selectUI == null)
        {
            Debug.LogError("CharacterManager cannot open selection because CharacterSelectUI is missing", this);
            return;
        }

        localSelectingPlayer = player;
        selectUI.OnSelectedCharacter -= OnSelectCharacter;
        selectUI.OnSelectedCharacter += OnSelectCharacter;
        selectUI.PopulateSelection(characters);
        selectUI.UpdateSelectedCharacters(selectedCharacterIds.ToArray());
        selectUI.OpenMenu();
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

        if (playersInSelection.Contains(player))
            return;

        if (spawnedPlayers.ContainsKey(player))
            return;

        playersInSelection.Add(player);

        var character = characters?.FirstOrDefault(x => x != null && x.CharacterID == characterId);

        if (character == null)
        {
            FailSelection(player, "Invalid character.");
            return;
        }

        if (selectedCharacterIds.Contains(characterId))
        {
            FailSelection(player, "Character is already selected.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            FailSelection(player, "No spawn points configured.");
            return;
        }

        if (playerPrefab == null)
        {
            FailSelection(player, "Player prefab is not assigned.");
            return;
        }

        selectedCharacterIds.Add(characterId);

        var spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        if (spawnPoint == null)
        {
            selectedCharacterIds.Remove(characterId);
            FailSelection(player, "Selected spawn point is missing.");
            return;
        }

        var avatar = Runner.Spawn(
            playerPrefab,
            spawnPoint.GetSpawnPosition(),
            Quaternion.identity,
            player,
            (_, networkObject) =>
            {
                var playerComponent = networkObject.GetComponent<Player>();

                if (playerComponent != null)
                    playerComponent.SetCharacter(character);
            });

        if (avatar == null)
        {
            selectedCharacterIds.Remove(characterId);
            FailSelection(player, "Could not spawn the player prefab.");
            return;
        }

        spawnedPlayers[player] = avatar;
        SelectionSucceededRpc(player);
        UpdateSelectedCharactersRpc(selectedCharacterIds.ToArray());
        playersInSelection.Remove(player);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SelectionSucceededRpc([RpcTarget] PlayerRef player)
    {
        if (selectUI != null)
            selectUI.CloseMenu();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void SelectionFailedRpc([RpcTarget] PlayerRef player, string reason)
    {
        Debug.LogWarning(reason);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void UpdateSelectedCharactersRpc(int[] selectedCharacters)
    {
        if (selectUI != null)
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

        var spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        return spawnPoint != null ? spawnPoint.GetSpawnPosition() : Vector3.zero;
    }

    private void RegisterCharacters()
    {
        if (characters == null)
            return;

        foreach (var character in characters)
        {
            if (character != null)
                CharacterProperties.Register(character);
        }
    }

    private void FailSelection(PlayerRef player, string reason)
    {
        SelectionFailedRpc(player, reason);
        playersInSelection.Remove(player);
    }
}
