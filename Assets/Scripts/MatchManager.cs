using Fusion;
using Singleton;
using UnityEngine;
using UnityEngine.Serialization;

public class MatchManager : Singleton<MatchManager>
{
    [FormerlySerializedAs("cm")]
    [SerializeField] private CharacterManager characterManager;
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
        if (!runner || !runner.IsServer)
            return;

        if (!ResolveReferences())
            return;

        foreach (var player in runner.ActivePlayers)
            PreparePlayer(runner, player);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner || !runner.IsServer)
            return;

        if (!ResolveReferences())
            return;

        PreparePlayer(runner, player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner || !runner.IsServer)
            return;

        ResolveReferences();
        var scoreManager = ScoreManager.Instance;

        if (scoreManager)
            scoreManager.RemovePlayer(player);

        if (characterManager)
            characterManager.RemovePlayer(player);

        var playerDataObject = runner.GetPlayerObject(player);

        if (playerDataObject)
            runner.Despawn(playerDataObject);
    }

    public void EndMatch()
    {
    }

    public Vector3 GetSpawnPosition(PlayerRef player)
    {
        return ResolveReferences()
            ? characterManager.GetSpawnPosition(player)
            : Vector3.zero;
    }

    public Vector3 GetRandomSpawnPosition()
    {
        return ResolveReferences()
            ? characterManager.GetRandomSpawnPosition()
            : Vector3.zero;
    }

    private void PreparePlayer(NetworkRunner runner, PlayerRef player)
    {
        EnsurePlayerData(runner, player);
        var scoreManager = ScoreManager.Instance;

        if (scoreManager)
            scoreManager.AddPlayer(player);

        characterManager.SpawnPlayerAtRandomPoint(player);
    }

    private void EnsurePlayerData(NetworkRunner runner, PlayerRef player)
    {
        if (runner.GetPlayerObject(player))
            return;

        if (!playerDataPrefab)
        {
            Debug.LogError("MatchManager requires the PlayerData network prefab", this);
            return;
        }

        var playerDataObject = runner.Spawn(playerDataPrefab, inputAuthority: player);

        if (playerDataObject)
            runner.SetPlayerObject(player, playerDataObject);
    }

    private bool ResolveReferences()
    {
        if (!characterManager)
            characterManager = FindAnyObjectByType<CharacterManager>();

        if (characterManager)
            return true;

        Debug.LogError("MatchManager requires a CharacterManager reference", this);
        return false;
    }
}
