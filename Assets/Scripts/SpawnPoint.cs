using System.Threading.Tasks;
using Fusion;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private Vector2 minRandomOffset;
    [SerializeField] private Vector2 maxRandomOffset;
    [SerializeField] private GameObject _playerPrefab;

    public Vector3 GetSpawnPosition(bool useRandomOffset = true)
    {
        var offset = Vector3.up;
        if (useRandomOffset)
        {
            offset += new Vector3(
                Random.Range(minRandomOffset.x, maxRandomOffset.x),
                0,
                Random.Range(minRandomOffset.y, maxRandomOffset.y)
            );
        }
        return transform.position + offset;
    }

    public async Task SpawnGivenPlayer(PlayerRef player, CharacterProperties character, bool useRandomOffset = true)
    {
        var runner = SinglePeer_NetworkRunnerManager.Instance.NetworkRunner;
        if (runner.LocalPlayer != player) return;

        var spawned = await runner.SpawnAsync(_playerPrefab, GetSpawnPosition(useRandomOffset), null, player);
        spawned.GetComponent<Player>().SetCharacter(character);
    }
}
