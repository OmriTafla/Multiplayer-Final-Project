using Fusion;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private Vector2 minRandomOffset;
    [SerializeField] private Vector2 maxRandomOffset;

    public Vector3 GetSpawnPosition(bool useRandomOffset = true)
    {
        var offset = Vector3.up;

        if (useRandomOffset)
        {
            offset += new Vector3(
                Random.Range(minRandomOffset.x, maxRandomOffset.x),
                0f,
                Random.Range(minRandomOffset.y, maxRandomOffset.y));
        }

        return transform.position + offset;
    }
}