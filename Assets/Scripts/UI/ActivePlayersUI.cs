using System.Collections.Generic;
using Fusion;
using TMPro;
using UI;
using UnityEngine;

public class ActivePlayersUI : MonoBehaviour
{
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private TMP_Text playerItemPrefab;

    private NetworkRunner currentRunner;

    private void OnEnable()
    {
        PlayerData.PlayerDataChanged += Refresh;
    }

    private void OnDisable()
    {
        PlayerData.PlayerDataChanged -= Refresh;
    }

    public void HandlePlayerJoinLeave(NetworkRunner runner, PlayerRef player)
    {
        currentRunner = runner;
        UpdateActivePlayers(runner);
    }

    public void UpdateActivePlayers(NetworkRunner runner)
    {
        currentRunner = runner;
        ClearList();

        if (runner == null)
            return;

        foreach (var player in runner.ActivePlayers)
        {
            var playerObject = runner.GetPlayerObject(player);

            if (playerObject == null)
                continue;

            var playerData = playerObject.GetComponent<PlayerData>();

            if (playerData == null)
                continue;

            var newItem = Instantiate(playerItemPrefab, playerListContainer);
            newItem.text = playerData.NickName.ToString();
            newItem.color = playerData.Color;
        }
    }

    private void Refresh()
    {
        if (currentRunner != null)
            UpdateActivePlayers(currentRunner);
    }

    private void ClearList()
    {
        if (playerListContainer == null)
            return;

        var objectsToDelete = new List<GameObject>();

        foreach (Transform child in playerListContainer)
            objectsToDelete.Add(child.gameObject);

        foreach (var objectToDelete in objectsToDelete)
            Destroy(objectToDelete);
    }
}
