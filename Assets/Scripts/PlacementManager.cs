using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlacementManager : NetworkBehaviour
{
    [SerializeField] private Projectile projectilePrefab;
    
    private readonly Dictionary<PlayerRef, List<NetworkObject>> playerPlaceables = new();

    public void SpawnProjectile(NetworkObject requestingPlayer, Vector3 origin, Vector3 direction)
    {
        if (!Object.HasStateAuthority)
            return;

        if (!requestingPlayer)
            return;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        var normalizedDirection = direction.normalized;

        Runner.Spawn(
            projectilePrefab,
            origin,
            Quaternion.LookRotation(normalizedDirection),
            PlayerRef.None,
            (spawnRunner, spawnedObject) =>
            {
                spawnedObject.GetComponent<Projectile>().OwnerPlayerRef = requestingPlayer.InputAuthority;
            });
    }

    public void RemovePlayer(PlayerRef player)
    {
        if (!Object.HasStateAuthority)
            return;

        if (!playerPlaceables.TryGetValue(player, out var placeables))
            return;

        foreach (var placeable in placeables)
        {
            if (placeable != null)
                Runner.Despawn(placeable);
        }

        playerPlaceables.Remove(player);
    }

    // private void TrackPlaceable(PlayerRef player, NetworkObject placeable)
    // {
    //     if (!playerPlaceables.TryGetValue(player, out var placeables))
    //     {
    //         placeables = new List<NetworkObject>();
    //         playerPlaceables.Add(player, placeables);
    //     }
    //
    //     placeables.Add(placeable);
    //
    //     while (placeables.Count > maximumPlaceablesPerPlayer)
    //     {
    //         var oldest = placeables[0];
    //         placeables.RemoveAt(0);
    //
    //         if (oldest != null)
    //             Runner.Despawn(oldest);
    //     }
    // }

    private void RemoveTrackedPlaceable(PlayerRef player, NetworkObject placeable)
    {
        if (!playerPlaceables.TryGetValue(player, out var placeables))
            return;

        placeables.Remove(placeable);

        if (placeables.Count == 0)
            playerPlaceables.Remove(player);
    }

    private static bool ValidatePlayer(NetworkObject requestingPlayer, int characterId)
    {
        if (requestingPlayer == null)
            return false;

        if (!requestingPlayer.TryGetComponent(out Player player))
            return false;

        if (player.IsDead)
            return false;

        return player.CharacterID == characterId;
    }
}
