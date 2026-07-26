using Fusion;
using UnityEngine;

public class PlacementManager : NetworkBehaviour
{
    [SerializeField] private float maximumPlacementDistance = 20f;
    [SerializeField] private float projectileSpawnOffset = 1f;

    public void PlacePlaceable(
        NetworkObject requestingPlayer,
        int characterId,
        Vector3 requestedPosition)
    {
        if (!Object.HasStateAuthority)
            return;

        if (!ValidatePlayer(
                requestingPlayer,
                characterId))
            return;

        var distance = Vector3.Distance(
            requestingPlayer.transform.position,
            requestedPosition);

        if (distance > maximumPlacementDistance)
            return;

        var properties =
            CharacterProperties.GetByID(characterId);

        if (properties == null)
            return;

        if (!properties.spawnObject.TryGetComponent(
                out PlaceableObject placeable))
            return;

        Runner.Spawn(
            properties.spawnObject,
            requestedPosition + placeable.GetGPOffset(),
            Quaternion.identity,
            requestingPlayer.InputAuthority);
    }

    public void DeletePlaceable(
        NetworkObject requestingPlayer,
        NetworkObject target)
    {
        if (!Object.HasStateAuthority)
            return;

        if (requestingPlayer == null || target == null)
            return;

        if (target.GetComponentInChildren<PlaceableObject>() == null)
            return;

        if (target.InputAuthority !=
            requestingPlayer.InputAuthority)
            return;

        Runner.Despawn(target);
    }

    public void SpawnProjectile(
        NetworkObject requestingPlayer,
        int characterId,
        Vector3 origin,
        Vector3 direction)
    {
        if (!Object.HasStateAuthority)
            return;

        if (!ValidatePlayer(
                requestingPlayer,
                characterId))
            return;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        var properties =
            CharacterProperties.GetByID(characterId);

        if (properties == null)
            return;

        var normalizedDirection = direction.normalized;
        var spawnPosition =
            origin +
            normalizedDirection * projectileSpawnOffset;

        Runner.Spawn(
            properties.bullet,
            spawnPosition,
            Quaternion.LookRotation(normalizedDirection),
            requestingPlayer.InputAuthority);
    }

    private static bool ValidatePlayer(
        NetworkObject requestingPlayer,
        int characterId)
    {
        if (requestingPlayer == null)
            return false;

        if (!requestingPlayer.TryGetComponent(
                out Player player))
            return false;

        if (player.IsDead)
            return false;

        return player.CharacterID == characterId;
    }
}