using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlacementManager : NetworkBehaviour
{
    [SerializeField] private Projectile projectilePrefab;

    private readonly Dictionary<PlayerRef, List<NetworkObject>> playerPlaceables = new();

    public void DeletePlaceable(NetworkObject requestingPlayer, NetworkObject target)
    {
        if (!Object.HasStateAuthority)
            return;

        if (!requestingPlayer || !target)
            return;

        if (!target.TryGetComponent(out PlaceableObject _))
            return;

        if (target.InputAuthority != requestingPlayer.InputAuthority)
            return;

        RemoveTrackedPlaceable(target.InputAuthority, target);
        Runner.Despawn(target);
    }

    public Projectile SpawnProjectile(NetworkObject requestingPlayer, Vector3 origin, Vector3 direction)
    {
        if (!Object.HasStateAuthority)
            return null;

        if (!requestingPlayer)
            return null;

        if (direction.sqrMagnitude < 0.0001f)
            return null;

        var normalizedDirection = direction.normalized;

        return Runner.Spawn(
            projectilePrefab,
            origin,
            Quaternion.LookRotation(normalizedDirection),
            requestingPlayer.InputAuthority);
    }

    public void RemovePlayer(PlayerRef player)
    {
        if (!Object.HasStateAuthority)
            return;

        if (!playerPlaceables.TryGetValue(player, out var placeables))
            return;

        foreach (var placeable in placeables)
        {
            if (placeable)
                Runner.Despawn(placeable);
        }

        playerPlaceables.Remove(player);
    }

    private void RemoveTrackedPlaceable(PlayerRef player, NetworkObject placeable)
    {
        if (!playerPlaceables.TryGetValue(player, out var placeables))
            return;

        placeables.Remove(placeable);

        if (placeables.Count == 0)
            playerPlaceables.Remove(player);
    }
}
