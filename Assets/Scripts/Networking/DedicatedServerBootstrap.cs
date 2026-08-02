using UnityEngine;

#if UNITY_SERVER
using System;
using System.Threading.Tasks;
using Enums;
#endif

public class DedicatedServerBootstrap : MonoBehaviour
{
#if UNITY_SERVER
    [SerializeField] private string defaultSessionName;
    [SerializeField] private string defaultMapName = "GameScene";
    [SerializeField] private IOGameMode defaultGameMode = IOGameMode.TwoTeams;
    [SerializeField] private int defaultFreeForAllPort = 27015;
    [SerializeField] private int defaultTwoTeamsPort = 27016;

    private void Start()
    {
        _ = StartDedicatedServerAsync();
    }

    private async Task StartDedicatedServerAsync()
    {
        var manager = SinglePeer_NetworkRunnerManager.Instance;

        if (!manager)
        {
            Debug.LogError("Dedicated server cannot start because the runner manager is missing", this);
            Application.Quit(1);
            return;
        }

        if (manager.IsRunning || manager.OperationInProgress)
            return;

        var mapName = GetCommandLineValue("-map", defaultMapName);
        var modeValue = GetCommandLineValue("-mode", GetModeArgument(defaultGameMode));

        if (!TryParseGameMode(modeValue, out var gameMode))
        {
            Debug.LogError($"Unknown game mode '{modeValue}'. Use 'ffa' or 'teams'.", this);
            Application.Quit(1);
            return;
        }

        var fallbackPort = gameMode == IOGameMode.FreeForAll
            ? defaultFreeForAllPort
            : defaultTwoTeamsPort;

        var portValue = GetCommandLineValue("-port", fallbackPort.ToString());

        if (!int.TryParse(portValue, out var parsedPort) ||
            parsedPort < 1 ||
            parsedPort > ushort.MaxValue)
        {
            Debug.LogError($"Invalid port '{portValue}'. Use a value from 1 to {ushort.MaxValue}.", this);
            Application.Quit(1);
            return;
        }

        var gameManager = GameManager.Instance;

        if (!gameManager)
        {
            Debug.LogError("Dedicated server cannot set the game mode because GameManager is missing", this);
            Application.Quit(1);
            return;
        }

        gameManager.SetGameMode(gameMode);

        var sessionName = GetCommandLineValue("-session", defaultSessionName);

        if (string.IsNullOrWhiteSpace(sessionName))
            sessionName = manager.GetSessionNameForMode(gameMode, mapName);

        var lobbyName = GetCommandLineValue("-lobby", manager.CustomLobbyName);

        var result = await manager.StartForCurrentBuild(
            sessionName,
            mapName,
            (ushort)parsedPort,
            lobbyName);

        if (!result.Ok)
        {
            var message = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? result.ShutdownReason.ToString()
                : result.ErrorMessage;

            Debug.LogError($"Dedicated server start failed: {message}", this);
            Application.Quit(1);
            return;
        }

        Debug.Log(
            $"Dedicated server started session '{sessionName}' " +
            $"in lobby '{lobbyName}' on map '{mapName}' " +
            $"in mode '{gameMode}' using UDP port {parsedPort}");
    }

    private static bool TryParseGameMode(string value, out IOGameMode gameMode)
    {
        var normalized = Normalize(value);

        switch (normalized)
        {
            case "ffa":
            case "freeforall":
                gameMode = IOGameMode.FreeForAll;
                return true;

            case "team":
            case "teams":
            case "2teams":
            case "twoteams":
                gameMode = IOGameMode.TwoTeams;
                return true;

            default:
                gameMode = default;
                return false;
        }
    }

    private static string GetModeArgument(IOGameMode gameMode)
    {
        return gameMode == IOGameMode.FreeForAll
            ? "ffa"
            : "teams";
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value
                .Trim()
                .Replace("-", string.Empty)
                .Replace("_", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
    }

    private static string GetCommandLineValue(string key, string fallback)
    {
        var arguments = Environment.GetCommandLineArgs();

        for (var index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], key, StringComparison.OrdinalIgnoreCase))
                return arguments[index + 1];
        }

        return fallback;
    }
#else
    private void Start()
    {
        enabled = false;
    }
#endif
}
