using System;
using System.Collections.Generic;
using System.Linq;
using Enums;
using Fusion;
using ScriptableObjects;
using Singleton;
using UnityEngine;
using UnityEngine.Events;

public class SessionJoiner : Singleton<SessionJoiner>
{
    public const string GAMEMODE_PROPERTY_NAME = "GameMode";
    public const string MAP_PROPERTY_NAME = "Map";

    public string SessionName { get; set; }

    [Header("Custom Session Settings")]
    [SerializeField] private int playerCapacity = 2;
    [SerializeField] private int maxCapacity = 8;
    [SerializeField] private bool isVisible = true;
    [SerializeField] private GameModes gameMode = GameModes.Fun;
    [SerializeField] private string mapName;
    [SerializeField] private MapList mapList;

    [Header("Events")]
    public UnityEvent<int> OnCapacityChanged;
    public UnityEvent OnStartJoin;
    public UnityEvent<NetworkRunner> OnJoined;
    public UnityEvent OnCancelJoin;
    public UnityEvent<List<SessionInfo>> OnAvaliableSessionsChanged;

    private List<SessionInfo> availableSessions = new();
    private bool busy;

    private void OnValidate()
    {
        if (mapList != null && mapList.GetMapNames().Any())
            mapName = mapList.GetMapNames().First();
    }

    private void Start()
    {
        OnCapacityChanged?.Invoke(playerCapacity);
    }

    public void JoinCustomSession()
    {
        if (busy)
            return;

        if (string.IsNullOrWhiteSpace(SessionName))
        {
            Debug.LogWarning("Session name cannot be empty");
            return;
        }

        var args = new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = SessionName.Trim(),
            CustomLobbyName = LobbyJoiner.Instance.LobbyName,
            PlayerCount = playerCapacity,
            IsOpen = true,
            IsVisible = isVisible,
            EnableClientSessionCreation = true,
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                { GAMEMODE_PROPERTY_NAME, (int)gameMode },
                { MAP_PROPERTY_NAME, mapName }
            }
        };

        JoinSession(args);
    }

    public void JoinSpecificSession(SessionInfo sessionInfo)
    {
        if (busy || sessionInfo == null)
            return;

        if (!sessionInfo.IsOpen || sessionInfo.PlayerCount >= sessionInfo.MaxPlayers)
            return;

        var args = new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = sessionInfo.Name,
            CustomLobbyName = LobbyJoiner.Instance.LobbyName,
            EnableClientSessionCreation = false
        };

        JoinSession(args);
    }

    private async void JoinSession(StartGameArgs args)
    {
        busy = true;
        OnStartJoin?.Invoke();

        var result = await SinglePeer_NetworkRunnerManager.Instance.StartSession(args);

        busy = false;

        if (result.Ok)
        {
            OnJoined?.Invoke(SinglePeer_NetworkRunnerManager.Instance.NetworkRunner);
            return;
        }

        OnCancelJoin?.Invoke();
    }

    public void IncreasePlayerCapacity()
    {
        playerCapacity = Mathf.Clamp(playerCapacity + 1, 1, maxCapacity);
        OnCapacityChanged?.Invoke(playerCapacity);
    }

    public void DecreasePlayerCapacity()
    {
        playerCapacity = Mathf.Clamp(playerCapacity - 1, 1, maxCapacity);
        OnCapacityChanged?.Invoke(playerCapacity);
    }

    public void SetVisibleFromPrivate(bool isPrivate)
    {
        isVisible = !isPrivate;
    }

    public void SetGameMode(int gameModeInt)
    {
        var chosenGameMode = (GameModes)(gameModeInt + 1);

        if (chosenGameMode == GameModes.Any)
            throw new ArgumentOutOfRangeException(nameof(gameModeInt));

        gameMode = chosenGameMode;
    }

    public void SetMapNameByIndex(int index)
    {
        mapName = mapList.GetMapNames().ToArray()[index];
    }

    public void UpdateAvailableSessions(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        availableSessions = new List<SessionInfo>(sessionList);
        OnAvaliableSessionsChanged?.Invoke(availableSessions);
    }

    public IEnumerable<SessionInfo> GetAvailableSessions()
    {
        return availableSessions;
    }
}
