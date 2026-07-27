using Enums;
using EnumUtils;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SessionItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text sessionName;
    [SerializeField] private TMP_Text playerCount;
    [SerializeField] private TMP_Text gameMode;
    [SerializeField] private TMP_Text mapName;
    [SerializeField] private Button joinButton;

    public void SetSessionInfo(SessionInfo sessionInfo)
    {
        SetSessionName(sessionInfo.Name);
        SetPlayerCount(sessionInfo.PlayerCount, sessionInfo.MaxPlayers);
        bool canJoin = sessionInfo.MaxPlayers > sessionInfo.PlayerCount &&
                       sessionInfo.IsOpen;
        SetCanJoin(canJoin);
        SetGameMode((IOGameMode)sessionInfo.Properties[SessionJoiner.GAMEMODE_PROPERTY_NAME].PropertyValue);
        SetMapName((string)sessionInfo.Properties[SessionJoiner.MAP_PROPERTY_NAME].PropertyValue);
    }
    
    private void SetSessionName(string name)
    {
        sessionName.text = name;
    }

    private void SetPlayerCount(int current, int max)
    {
        playerCount.text = $"{current}/{max} players";
    }

    private void SetCanJoin(bool canJoin)
    {
        joinButton.interactable = canJoin;
    }

    private void SetGameMode(IOGameMode gameMode)
    {
        this.gameMode.text = gameMode.GetDisplayName();
    }

    private void SetMapName(string name)
    {
        mapName.text = $"Map: {name}";
    }

    public void SetCallback(UnityAction callback)
    {
        joinButton.onClick.AddListener(callback);
    }
}
