
using Fusion;
using Singleton;
using UnityEngine;
using UnityEngine.Events;

public class MatchManager : Singleton<MatchManager>
{
    [SerializeField]
    private CharacterManager cm;
    [SerializeField]
    private PlacementManager pm;

    public void OnSceneLoaded(NetworkRunner runner)
    {
        if (!SinglePeer_NetworkRunnerManager.Instance.NetworkRunner.IsSceneAuthority) return;

        foreach (var player in SinglePeer_NetworkRunnerManager.Instance.NetworkRunner.ActivePlayers)
        {
            cm.MakeSelectCharacterRPC(player);
        }
    }

    [SerializeField] private UnityEvent OnMatchEnded;

    public void EndMatch()
    {
        if (!SinglePeer_NetworkRunnerManager.Instance.NetworkRunner.IsSceneAuthority)
        {
            print("Can't end match, not the scene authority");
            return;
        }
        
        OnMatchEnded.Invoke();
    }

    public void RequestPlacePlaceable(int characterID, Vector3 position)
    {
        pm.PlacePlaceableRPC(characterID, position);
    }

    public void RequestDeletePlaceable(NetworkId id)
    {
        pm.DeletePlaceableRPC(id);
    }
    
    public void RequestSpawnProjectile(int characterID, Vector3 origin, Vector3 direction)
    {
        pm.SpawnProjectileRPC(characterID, origin, direction);
    }
    
    public Vector3 GetRandomSpawnPosition()
    {
        return cm.GetRandomSpawnPosition();
    }
}
