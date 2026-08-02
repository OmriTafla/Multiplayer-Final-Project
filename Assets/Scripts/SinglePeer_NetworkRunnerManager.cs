#if UNITY_SERVER
#define DEDICATED_SERVER
#else
#define HOST_OR_CLIENT
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Enums;
using EnumUtils;
using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class SinglePeer_NetworkRunnerManager : PersistentSingleton<SinglePeer_NetworkRunnerManager>
{
    [SerializeField] private NetworkRunner networkRunnerPrefab;
    [SerializeField] private NetworkEvents networkEvents;
    [SerializeField] private string persistentSessionName = "MainWorld";
    [SerializeField] private string customLobbyName = "Cool";
    [SerializeField] private string gameScenePath = "Assets/Scenes/GameScene.unity";
    [SerializeField] private string defaultMapName = "GameScene";
    [SerializeField] private int maximumPlayers = 32;
    [SerializeField] private UnityEvent<NetworkRunner> onRunnerInstantiated;
    [SerializeField] private UnityEvent onConnectionStarted;
    [SerializeField] private UnityEvent onConnectionSucceeded;
    [SerializeField] private UnityEvent<string> onConnectionFailed;
    [SerializeField] private UnityEvent onShutdownCompleted;

    private NetworkRunner networkRunner;
    private NetworkSceneManagerDefault networkSceneManager;
    private FusionInputProvider inputProvider;
    private bool operationInProgress;
    private bool isInLeaderboardLobby;
    private bool shutdownEventRequested;
    private Task shutdownTask;

    public NetworkRunner NetworkRunner => networkRunner;
    public bool OperationInProgress => operationInProgress;
    public bool HasRunner => networkRunner;
    public bool IsRunning => networkRunner && networkRunner.IsRunning;
    public bool IsServer => IsRunning && networkRunner.IsServer;
    public bool IsClient => IsRunning && networkRunner.IsClient;
    public bool IsInLeaderboardLobby =>
        isInLeaderboardLobby && networkRunner;
    public string PersistentSessionName => persistentSessionName;
    public string CustomLobbyName => customLobbyName;
    public NetworkEvents NetworkEvents => networkEvents;

    public bool IsDedicatedBuild
    {
        get
        {
#if DEDICATED_SERVER
            return true;
#else
            return false;
#endif
        }
    }

    public string RuntimeMode
    {
        get
        {
#if DEDICATED_SERVER
            return "Dedicated Server";
#else
            return "Host or Client";
#endif
        }
    }

    public NetworkRunner CreateRunner()
    {
        if (networkRunner)
            return networkRunner;

        if (!networkRunnerPrefab)
        {
            Debug.LogError("NetworkRunner prefab is not assigned", this);
            return null;
        }

        networkRunner = Instantiate(networkRunnerPrefab);
        networkRunner.name = "NetworkRunner";
        isInLeaderboardLobby = false;

#if DEDICATED_SERVER
        networkRunner.ProvideInput = false;
#else
        networkRunner.ProvideInput = true;
#endif

        networkRunner.gameObject.AddComponent<RunnerSimulatePhysics3D>();
        networkSceneManager =
            networkRunner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        if (networkEvents)
            networkRunner.AddCallbacks(networkEvents);

#if HOST_OR_CLIENT
        inputProvider = networkRunner.GetComponent<FusionInputProvider>();

        if (inputProvider)
            inputProvider.RegisterCallbacks(networkRunner);
#endif

        onRunnerInstantiated?.Invoke(networkRunner);
        return networkRunner;
    }

    public void ConfigureNetworkEvents(NetworkEvents newNetworkEvents)
    {
        if (networkEvents == newNetworkEvents)
            return;

        if (networkRunner && networkEvents)
            networkRunner.RemoveCallbacks(networkEvents);

        networkEvents = newNetworkEvents;

        if (networkRunner && networkEvents)
            networkRunner.AddCallbacks(networkEvents);
    }

    public string GetSessionNameForMode(IOGameMode gameMode, string mapName = null)
    {
        var resolvedMapName = ResolveMapName(mapName);
        return $"{resolvedMapName}-{gameMode.GetSessionSuffix()}";
    }

    public Task<StartGameResult> StartForCurrentBuild(
        string sessionNameOverride = null,
        string gameSceneOverride = null,
        ushort port = 27015,
        string lobbyNameOverride = null)
    {
#if DEDICATED_SERVER
        return StartDedicatedServer(
            sessionNameOverride,
            gameSceneOverride,
            port,
            lobbyNameOverride);
#else
        return StartHostOrClient(sessionNameOverride);
#endif
    }

    public Task<StartGameResult> StartHostOrClient(string sessionNameOverride = null)
    {
#if DEDICATED_SERVER
        return Task.FromResult(default(StartGameResult));
#else
        if (!TryCreateGameSceneInfo(null, out var sceneInfo, out _, out _))
            return Task.FromResult(default(StartGameResult));

        var gameMode = GameManager.Instance
            ? GameManager.Instance.GameMode
            : IOGameMode.FreeForAll;

        return StartSession(new StartGameArgs
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = ResolveSessionName(sessionNameOverride),
            PlayerCount = GetMaximumPlayers(),
            IsOpen = true,
            IsVisible = true,
            EnableClientSessionCreation = true,
            CustomLobbyName = customLobbyName,
            Scene = sceneInfo,
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                { SessionPropertyKeys.Map, ResolveMapName(defaultMapName) },
                { SessionPropertyKeys.GameMode, (int)gameMode },
                { SessionPropertyKeys.Leaderboard, string.Empty }
            }
        });
#endif
    }

    public Task<StartGameResult> StartDedicatedServer(
        string sessionNameOverride = null,
        string gameSceneOverride = null,
        ushort port = 27015,
        string lobbyNameOverride = null)
    {
#if DEDICATED_SERVER
        if (!TryCreateGameSceneInfo(
                gameSceneOverride,
                out var sceneInfo,
                out var resolvedScenePath,
                out _))
        {
            return Task.FromResult(default(StartGameResult));
        }

        var gameMode = GameManager.Instance
            ? GameManager.Instance.GameMode
            : IOGameMode.TwoTeams;

        var resolvedSessionName = string.IsNullOrWhiteSpace(sessionNameOverride)
            ? GetSessionNameForMode(gameMode, resolvedScenePath)
            : ResolveSessionName(sessionNameOverride);

        var resolvedLobbyName = string.IsNullOrWhiteSpace(lobbyNameOverride)
            ? customLobbyName
            : lobbyNameOverride.Trim();

        return StartSession(new StartGameArgs
        {
            GameMode = GameMode.Server,
            SessionName = resolvedSessionName,
            PlayerCount = GetMaximumPlayers(),
            IsOpen = true,
            IsVisible = true,
            CustomLobbyName = resolvedLobbyName,
            Address = NetAddress.Any(port),
            Scene = sceneInfo,
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                { SessionPropertyKeys.Map, ResolveMapName(resolvedScenePath) },
                { SessionPropertyKeys.GameMode, (int)gameMode },
                { SessionPropertyKeys.Leaderboard, string.Empty }
            }
        });
#else
        Debug.LogWarning("StartDedicatedServer is available only in a Unity Dedicated Server build", this);
        return Task.FromResult(default(StartGameResult));
#endif
    }

    public Task<StartGameResult> JoinPersistentWorld()
    {
        return StartClient(persistentSessionName);
    }

    public Task<StartGameResult> StartPersistentServer(string sessionNameOverride = null)
    {
        return StartDedicatedServer(sessionNameOverride);
    }

    public Task<StartGameResult> StartPersistentHostForDevelopment()
    {
        return StartHost(persistentSessionName);
    }

    public Task<StartGameResult> StartHost(string sessionName)
    {
#if DEDICATED_SERVER
        return Task.FromResult(default(StartGameResult));
#else
        var gameMode = GameManager.Instance
            ? GameManager.Instance.GameMode
            : IOGameMode.FreeForAll;

        return StartHostSession(
            sessionName,
            customLobbyName,
            GetMaximumPlayers(),
            true,
            gameMode,
            defaultMapName);
#endif
    }

    public Task<StartGameResult> StartHostSession(
        string sessionName,
        string lobbyName,
        int playerCount,
        bool isVisible,
        IOGameMode gameMode,
        string mapName)
    {
#if DEDICATED_SERVER
        return Task.FromResult(default(StartGameResult));
#else
        if (!TryCreateGameSceneInfo(null, out var sceneInfo, out _, out _))
            return Task.FromResult(default(StartGameResult));

        return StartSession(new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = ResolveSessionName(sessionName),
            PlayerCount = Mathf.Clamp(playerCount, 1, GetMaximumPlayers()),
            IsOpen = true,
            IsVisible = isVisible,
            EnableClientSessionCreation = true,
            CustomLobbyName = string.IsNullOrWhiteSpace(lobbyName)
                ? customLobbyName
                : lobbyName.Trim(),
            Scene = sceneInfo,
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                { SessionPropertyKeys.Map, ResolveMapName(defaultMapName) },
                { SessionPropertyKeys.GameMode, (int)gameMode },
                { SessionPropertyKeys.Leaderboard, string.Empty }
            }
        });
#endif
    }

    public async Task<bool> JoinLeaderboardLobby()
    {
#if DEDICATED_SERVER
        return false;
#else
        if (IsInLeaderboardLobby)
            return true;

        if (operationInProgress)
            return false;

        operationInProgress = true;

        try
        {
            const int maximumAttempts = 3;

            for (var attempt = 1; attempt <= maximumAttempts; attempt++)
            {
                await ShutdownRunner(false);

                var runner = CreateRunner();

                if (!runner)
                    return false;

                try
                {
                    var result = await runner.JoinSessionLobby(
                        SessionLobby.Custom,
                        customLobbyName);

                    if (result.Ok)
                    {
                        isInLeaderboardLobby = true;
                        Debug.Log(
                            $"Joined leaderboard lobby '{customLobbyName}'",
                            this);
                        return true;
                    }

                    var message = string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? result.ShutdownReason.ToString()
                        : result.ErrorMessage;

                    Debug.LogWarning(
                        $"Could not join leaderboard lobby '{customLobbyName}' " +
                        $"on attempt {attempt}/{maximumAttempts}: {message}",
                        this);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }

                await ShutdownRunner(false);

                if (attempt < maximumAttempts)
                    await Task.Delay(500);
            }
        }
        finally
        {
            operationInProgress = false;
        }

        return false;
#endif
    }

    public Task<StartGameResult> StartClient(
        string sessionName,
        string lobbyName = null)
    {
#if DEDICATED_SERVER
        return Task.FromResult(default(StartGameResult));
#else
        return StartSession(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = ResolveSessionName(sessionName),
            CustomLobbyName = string.IsNullOrWhiteSpace(lobbyName)
                ? customLobbyName
                : lobbyName.Trim(),
            EnableClientSessionCreation = false
        });
#endif
    }

    public async Task<StartGameResult> StartSession(StartGameArgs args)
    {
        if (operationInProgress)
        {
            Debug.LogWarning("A network operation is already in progress", this);
            return default;
        }

        if (string.IsNullOrWhiteSpace(args.SessionName))
        {
            onConnectionFailed?.Invoke("Session name cannot be empty");
            return default;
        }

        operationInProgress = true;

        try
        {
            onConnectionStarted?.Invoke();
            await ShutdownRunner(false);

            var runner = CreateRunner();

            if (!runner)
            {
                onConnectionFailed?.Invoke("Could not create NetworkRunner");
                return default;
            }

            args.SessionName = args.SessionName.Trim();
            args.SceneManager = networkSceneManager;

            StartGameResult result;

            try
            {
                result = await runner.StartGame(args);
            }
            catch (Exception exception)
            {
                onConnectionFailed?.Invoke(exception.Message);
                Debug.LogException(exception);
                await ShutdownRunner(false);
                return default;
            }

            if (result.Ok)
            {
                onConnectionSucceeded?.Invoke();
                Debug.Log(
                    $"Network started as {runner.GameMode} " +
                    $"in session '{args.SessionName}'");
                return result;
            }

            var message = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? result.ShutdownReason.ToString()
                : result.ErrorMessage;

            onConnectionFailed?.Invoke(message);
            await ShutdownRunner(false);
            return result;
        }
        finally
        {
            operationInProgress = false;
        }
    }

    public Task ShutdownRunner(bool invokeEvent = true)
    {
        if (invokeEvent)
            shutdownEventRequested = true;

        if (shutdownTask is not null)
            return shutdownTask;

        if (!networkRunner)
        {
            shutdownEventRequested = false;
            return Task.CompletedTask;
        }

        var runnerToShutdown = networkRunner;
        var inputProviderToShutdown = inputProvider;
        networkRunner = null;
        networkSceneManager = null;
        inputProvider = null;
        isInLeaderboardLobby = false;
        shutdownTask = ShutdownRunnerInternal(
            runnerToShutdown,
            inputProviderToShutdown);
        return shutdownTask;
    }

    private async Task ShutdownRunnerInternal(
        NetworkRunner runnerToShutdown,
        FusionInputProvider inputProviderToShutdown)
    {
        try
        {
#if HOST_OR_CLIENT
            if (inputProviderToShutdown)
                inputProviderToShutdown.UnregisterCallbacks(runnerToShutdown);
#endif

            if (networkEvents)
                runnerToShutdown.RemoveCallbacks(networkEvents);

            if (runnerToShutdown.IsRunning)
                await runnerToShutdown.Shutdown();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        if (runnerToShutdown)
            Destroy(runnerToShutdown.gameObject);

        await Task.Yield();

        var invokeEvent = shutdownEventRequested;
        shutdownEventRequested = false;
        shutdownTask = null;

        if (invokeEvent)
            onShutdownCompleted?.Invoke();
    }

    public async Task RecreateRunner()
    {
        await ShutdownRunner(false);
        CreateRunner();
    }

    private int GetMaximumPlayers()
    {
        return Mathf.Clamp(maximumPlayers, 1, GameManager.MAX_PLAYERS);
    }

    private string ResolveSessionName(string sessionNameOverride)
    {
        return string.IsNullOrWhiteSpace(sessionNameOverride)
            ? persistentSessionName
            : sessionNameOverride.Trim();
    }

    private string ResolveMapName(string mapName)
    {
        var resolvedMapName = string.IsNullOrWhiteSpace(mapName)
            ? defaultMapName
            : mapName.Trim();

        if (string.IsNullOrWhiteSpace(resolvedMapName))
            resolvedMapName = Path.GetFileNameWithoutExtension(gameScenePath);

        resolvedMapName = Path.GetFileNameWithoutExtension(
            resolvedMapName.Replace('\\', '/'));

        return string.IsNullOrWhiteSpace(resolvedMapName)
            ? "GameScene"
            : resolvedMapName.Replace(' ', '-');
    }

    private bool TryCreateGameSceneInfo(
        string gameSceneOverride,
        out NetworkSceneInfo sceneInfo,
        out string resolvedScenePath,
        out int buildIndex)
    {
        sceneInfo = default;
        resolvedScenePath = ResolveGameScenePath(gameSceneOverride);
        buildIndex = SceneUtility.GetBuildIndexByScenePath(resolvedScenePath);

        if (buildIndex < 0)
        {
            var requestedScene = string.IsNullOrWhiteSpace(gameSceneOverride)
                ? gameScenePath
                : gameSceneOverride;

            var message =
                $"Game scene '{requestedScene}' is not included in the active Build Profile";

            Debug.LogError(message, this);
            onConnectionFailed?.Invoke(message);
            return false;
        }

        sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(
            SceneRef.FromIndex(buildIndex),
            LoadSceneMode.Single);

        return true;
    }

    private string ResolveGameScenePath(string gameSceneOverride)
    {
        var requestedScene = string.IsNullOrWhiteSpace(gameSceneOverride)
            ? gameScenePath
            : gameSceneOverride.Trim();

        requestedScene = requestedScene.Replace('\\', '/');

        if (requestedScene.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
            requestedScene.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
            return requestedScene;
        }

        var requestedName = Path.GetFileNameWithoutExtension(requestedScene);

        for (var buildIndex = 0;
             buildIndex < SceneManager.sceneCountInBuildSettings;
             buildIndex++)
        {
            var candidatePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
            var candidateName = Path.GetFileNameWithoutExtension(candidatePath);

            if (string.Equals(
                    candidateName,
                    requestedName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidatePath;
            }
        }

        return requestedScene;
    }
}
