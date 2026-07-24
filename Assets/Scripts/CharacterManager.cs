using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;

public class CharacterManager : NetworkBehaviour
{
    [SerializeField]
    private CharacterProperties[] characters;
    [SerializeField]
    private CharacterSelectUI selectUI;

    [SerializeField]
    private SpawnPoint[] spawnPoints;

    private void Awake()
    {
        if (characters == null) return;

        foreach (var character in characters)
            CharacterProperties.Register(character);
    }

    private HashSet<int> _selectedCharacterIDs = new();

    private HashSet<int> _playersInSelection = new();

    // host telling fellow to choose a character
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void MakeSelectCharacterRPC([RpcTarget] PlayerRef player)
    {
        selectUI.OnSelectedCharacter -= OnSelectCharacter;
        selectUI.OnSelectedCharacter += OnSelectCharacter;

        selectUI.PopulateSelection(characters);

        selectUI.OpenMenu();
    }

    private void OnSelectCharacter(int characterID)
    {
        RequestCharacterRPC(characterID);
    }

    // request a chosen character from the host
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public async void RequestCharacterRPC(int characterID, RpcInfo info = default)
    {
        if (_playersInSelection.Contains(info.Source.PlayerId)) return;
        _playersInSelection.Add(info.Source.PlayerId);

        var characterProps = characters.First(x => x.CharacterID == characterID);

        if (characterProps == null)
        {
            CharacterInvalidRPC(info.Source, characterID);
            _playersInSelection.Remove(info.Source.PlayerId);
            return;
        }

        if (_selectedCharacterIDs.Contains(characterID))
        {
            CharacterAlreadyChosenRPC(info.Source, characterID);
            _playersInSelection.Remove(info.Source.PlayerId);
            return;
        }

        _selectedCharacterIDs.Add(characterID);
        CharacterChosensuccessfullyRPC(info.Source, characterID);
        UpdateSelectedCharactersRPC(_selectedCharacterIDs.ToArray());

        if (spawnPoints.Length == 0) return;

        var sp = spawnPoints[Random.Range(0, spawnPoints.Length)];

        TriggerSpawnRPC(info.Source, characterID, System.Array.IndexOf(spawnPoints, sp));

        _playersInSelection.Remove(info.Source.PlayerId);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public async void TriggerSpawnRPC(PlayerRef player, int characterID, int spawnPointIndex)
    {
        var character = characters.First(x => x.CharacterID == characterID);
        await spawnPoints[spawnPointIndex].SpawnGivenPlayer(player, character);
    }
    
    public Vector3 GetRandomSpawnPosition()
    {
        var sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
        return sp.GetSpawnPosition();
    }

    // when someone wants a character but its not available
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void CharacterInvalidRPC([RpcTarget] PlayerRef player, int characterID)
    {
        
    }

    // when someone wants a character but its not available
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void CharacterAlreadyChosenRPC([RpcTarget] PlayerRef player, int characterID)
    {
        
    }

    // when someone wants a character and managed to get it
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void CharacterChosensuccessfullyRPC([RpcTarget] PlayerRef player, int characterID)
    {
        selectUI.CloseMenu();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void UpdateSelectedCharactersRPC(int[] selectedChars)
    {
        selectUI.UpdateSelectedCharacters(selectedChars);
    }
}
