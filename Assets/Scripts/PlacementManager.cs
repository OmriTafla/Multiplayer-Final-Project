using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PlacementManager : NetworkBehaviour
{
    // [SerializeField] private float maximumPlacementDistance = 20f;
    // [SerializeField] private float projectileSpawnOffset = 1f;
    // [SerializeField] private int maximumPlaceablesPerPlayer = 20;
    [SerializeField] private GameObject projectilePrefab;
    
    private readonly Dictionary<PlayerRef, List<NetworkObject>> playerPlaceables = new();

    // public void PlacePlaceable(NetworkObject requestingPlayer, int characterId, Vector3 requestedPosition)
    // {
    //     if (!Object.HasStateAuthority)
    //         return;
    //
    //     if (!ValidatePlayer(requestingPlayer, characterId))
    //         return;
    //
    //     var distance = Vector3.Distance(requestingPlayer.transform.position, requestedPosition);
    //
    //     if (distance > maximumPlacementDistance)
    //         return;
    //
    //     var properties = CharacterProperties.GetByID(characterId);
    //
    //     if (properties == null)
    //         return;
    //
    //     if (!properties.spawnObject.TryGetComponent(out PlaceableObject placeable))
    //         return;
    //
    //     var spawnedObject = Runner.Spawn(
    //         properties.spawnObject,
    //         requestedPosition + placeable.GetGPOffset(),
    //         Quaternion.identity,
    //         requestingPlayer.InputAuthority);
    //
    //     if (spawnedObject == null)
    //         return;
    //
    //     TrackPlaceable(requestingPlayer.InputAuthority, spawnedObject);
    // }

    public void DeletePlaceable(NetworkObject requestingPlayer, NetworkObject target)
    {
        if (!Object.HasStateAuthority)
            return;

        if (requestingPlayer == null || target == null)
            return;

        if (target.GetComponentInChildren<PlaceableObject>() == null)
            return;

        if (target.InputAuthority != requestingPlayer.InputAuthority)
            return;

        RemoveTrackedPlaceable(target.InputAuthority, target);
        Runner.Despawn(target);
    }

    public void SpawnProjectile(NetworkObject requestingPlayer, Vector3 origin, Vector3 direction)
    {
        if (!Object.HasStateAuthority)
            return;

        // if (!ValidatePlayer(requestingPlayer, characterId))
        //     return;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        // var properties = CharacterProperties.GetByID(characterId);

        // if (properties == null)
            // return;

        var normalizedDirection = direction.normalized;
        // var spawnPosition = origin + normalizedDirection * projectileSpawnOffset;

        Runner.Spawn(
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
