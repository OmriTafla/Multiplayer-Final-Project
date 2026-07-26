using Fusion;
using Singleton;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

public class MatchManager : Singleton<MatchManager>
{
    [FormerlySerializedAs("cm")]
    [SerializeField] private CharacterManager characterManager;

    [FormerlySerializedAs("pm")]
    [SerializeField] private PlacementManager placementManager;

    [FormerlySerializedAs("OnMatchEnded")]
    [SerializeField] private UnityEvent onMatchEnded;

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
            characterManager.StartSelection(player);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || !runner.IsServer)
            return;

        if (!ResolveReferences())
            return;

        characterManager.StartSelection(player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || !runner.IsServer)
            return;

        if (!ResolveReferences())
            return;

        characterManager.RemovePlayer(player);
    }

    public void EndMatch()
    {
        var manager = SinglePeer_NetworkRunnerManager.Instance;
        var runner = manager != null ? manager.NetworkRunner : null;

        if (runner == null || !runner.IsServer)
            return;

        onMatchEnded?.Invoke();
    }

    public Vector3 GetRandomSpawnPosition()
    {
        if (!ResolveReferences())
            return Vector3.zero;

        return characterManager.GetRandomSpawnPosition();
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
