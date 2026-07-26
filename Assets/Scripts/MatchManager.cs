using Fusion;
using Singleton;
using UnityEngine;
using UnityEngine.Serialization;

public class MatchManager : Singleton<MatchManager>
{
    [FormerlySerializedAs("cm")]
    [SerializeField] private CharacterManager characterManager;

    [FormerlySerializedAs("pm")]
    [SerializeField] private PlacementManager placementManager;

    [SerializeField] private NetworkObject playerDataPrefab;

    protected override void Awake()
    {
        base.Awake();
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    public void OnSceneLoaded(NetworkRunner runner)
    {
        if (runner == null || !runner.IsServer)
            return;

        if (!ResolveReferences())
            return;

        foreach (var player in runner.ActivePlayers)
            PreparePlayer(runner, player);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || !runner.IsServer)
            return;

        if (!ResolveReferences())
            return;

        PreparePlayer(runner, player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || !runner.IsServer)
            return;

        if (!ResolveReferences())
            return;

        characterManager.RemovePlayer(player);
        placementManager?.RemovePlayer(player);
        ScoreManager.Instance?.RemovePlayer(player);

        var playerDataObject = runner.GetPlayerObject(player);

        if (playerDataObject != null)
            runner.Despawn(playerDataObject);
    }

    public void EndMatch()
    {
        Debug.Log("EndMatch is disabled because this is a persistent world.");
    }

    public Vector3 GetRandomSpawnPosition()
    {
        if (!ResolveReferences())
            return Vector3.zero;

        return characterManager.GetRandomSpawnPosition();
    }

    private void PreparePlayer(NetworkRunner runner, PlayerRef player)
    {
        EnsurePlayerData(runner, player);
        characterManager.StartSelection(player);
    }

    private void EnsurePlayerData(NetworkRunner runner, PlayerRef player)
    {
        if (runner.GetPlayerObject(player) != null)
            return;

        if (playerDataPrefab == null)
        {
            Debug.LogError("MatchManager requires the PlayerData network prefab", this);
            return;
        }

        var playerDataObject = runner.Spawn(playerDataPrefab, inputAuthority: player);

        if (playerDataObject != null)
            runner.SetPlayerObject(player, playerDataObject);
    }

    private bool ResolveReferences()
    {
        if (characterManager == null)
            characterManager = FindAnyObjectByType<CharacterManager>();

        if (placementManager == null)
            placementManager = FindAnyObjectByType<PlacementManager>();

        if (characterManager != null)
            return true;

        Debug.LogError("MatchManager requires a CharacterManager reference", this);
        return false;
    }
}
