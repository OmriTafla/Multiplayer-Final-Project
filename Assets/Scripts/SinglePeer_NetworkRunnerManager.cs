#if UNITY_SERVER
#define DEDICATED_SERVER
#else
#define HOST_OR_CLIENT
#endif

using System;
using System.Threading.Tasks;
using Fusion;
using Fusion.Addons.Physics;
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
    [SerializeField] private int maximumPlayers = 32;
    [SerializeField] private UnityEvent<NetworkRunner> onRunnerInstantiated;
    [SerializeField] private UnityEvent onConnectionStarted;
    [SerializeField] private UnityEvent onConnectionSucceeded;
    [SerializeField] private UnityEvent<string> onConnectionFailed;
    [SerializeField] private UnityEvent onShutdownCompleted;

    private NetworkRunner networkRunner;
    private bool operationInProgress;

    public NetworkRunner NetworkRunner => networkRunner;
    public bool OperationInProgress => operationInProgress;
    public bool HasRunner => networkRunner != null;
    public bool IsRunning => networkRunner != null && networkRunner.IsRunning;
    public bool IsServer => IsRunning && networkRunner.IsServer;
    public bool IsClient => IsRunning && networkRunner.IsClient;
    public string PersistentSessionName => persistentSessionName;
    public string CustomLobbyName => customLobbyName;

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
        if (networkRunner != null)
            return networkRunner;

        if (networkRunnerPrefab == null)
        {
            Debug.LogError("NetworkRunner prefab is not assigned", this);
            return null;
        }

        networkRunner = Instantiate(networkRunnerPrefab);
        networkRunner.name = "NetworkRunner";

#if DEDICATED_SERVER
        networkRunner.ProvideInput = false;
#else
        networkRunner.ProvideInput = true;
#endif

        if (networkRunner.GetComponent<RunnerSimulatePhysics3D>() == null)
            networkRunner.gameObject.AddComponent<RunnerSimulatePhysics3D>();

        if (networkEvents != null)
            networkRunner.AddCallbacks(networkEvents);

#if HOST_OR_CLIENT
        var inputProvider = networkRunner.GetComponent<FusionInputProvider>();

        if (inputProvider != null)
            inputProvider.RegisterCallbacks(networkRunner);
#endif

        onRunnerInstantiated?.Invoke(networkRunner);
        return networkRunner;
    }

    public void ConfigureNetworkEvents(NetworkEvents newNetworkEvents)
    {
        if (networkEvents == newNetworkEvents)
            return;

        if (networkRunner != null && networkEvents != null)
            networkRunner.RemoveCallbacks(networkEvents);

        networkEvents = newNetworkEvents;

        if (networkRunner != null && networkEvents != null)
            networkRunner.AddCallbacks(networkEvents);
    }

    public Task<StartGameResult> StartForCurrentBuild(string sessionNameOverride = null)
    {
#if DEDICATED_SERVER
        return StartDedicatedServer(sessionNameOverride);
#else
        return StartHostOrClient(sessionNameOverride);
#endif
    }

    public Task<StartGameResult> StartHostOrClient(string sessionNameOverride = null)
    {
#if DEDICATED_SERVER
        return Task.FromResult(default(StartGameResult));
#else
        if (!TryCreateGameSceneInfo(out var sceneInfo))
            return Task.FromResult(default(StartGameResult));

        return StartSession(new StartGameArgs
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = ResolveSessionName(sessionNameOverride),
            PlayerCount = maximumPlayers,
            IsOpen = true,
            IsVisible = true,
            EnableClientSessionCreation = true,
            Scene = sceneInfo
        });
#endif
    }

    public Task<StartGameResult> StartDedicatedServer(string sessionNameOverride = null)
    {
#if DEDICATED_SERVER
        if (!TryCreateGameSceneInfo(out var sceneInfo))
            return Task.FromResult(default(StartGameResult));

        return StartSession(new StartGameArgs
        {
            GameMode = GameMode.Server,
            SessionName = ResolveSessionName(sessionNameOverride),
            PlayerCount = maximumPlayers,
            IsOpen = true,
            IsVisible = true,
            EnableClientSessionCreation = true,
            Scene = sceneInfo
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
        if (!TryCreateGameSceneInfo(out var sceneInfo))
            return Task.FromResult(default(StartGameResult));

        return StartSession(new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = ResolveSessionName(sessionName),
            PlayerCount = maximumPlayers,
            IsOpen = true,
            IsVisible = true,
            EnableClientSessionCreation = true,
            Scene = sceneInfo
        });
#endif
    }

    public Task<StartGameResult> StartClient(string sessionName)
    {
#if DEDICATED_SERVER
        return Task.FromResult(default(StartGameResult));
#else
        return StartSession(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = ResolveSessionName(sessionName),
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
        onConnectionStarted?.Invoke();

        await ShutdownRunner(false);

        var runner = CreateRunner();

        if (runner == null)
        {
            operationInProgress = false;
            onConnectionFailed?.Invoke("Could not create NetworkRunner");
            return default;
        }

        var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();

        if (sceneManager == null)
            sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

        args.SessionName = args.SessionName.Trim();
        args.SceneManager = sceneManager;

        StartGameResult result;

        try
        {
            result = await runner.StartGame(args);
        }
        catch (Exception exception)
        {
            operationInProgress = false;
            onConnectionFailed?.Invoke(exception.Message);
            Debug.LogException(exception);
            await ShutdownRunner(false);
            return default;
        }

        operationInProgress = false;

        if (result.Ok)
        {
            onConnectionSucceeded?.Invoke();
            Debug.Log($"Network started as {runner.GameMode} in session '{args.SessionName}'");
            return result;
        }

        var message = string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? result.ShutdownReason.ToString()
            : result.ErrorMessage;

        onConnectionFailed?.Invoke(message);
        await ShutdownRunner(false);
        return result;
    }

    public async Task ShutdownRunner(bool invokeEvent = true)
    {
        if (networkRunner == null)
            return;

        var runnerToShutdown = networkRunner;
        networkRunner = null;

        try
        {
#if HOST_OR_CLIENT
            var inputProvider = runnerToShutdown.GetComponent<FusionInputProvider>();

            if (inputProvider != null)
                inputProvider.UnregisterCallbacks(runnerToShutdown);
#endif

            if (networkEvents != null)
                runnerToShutdown.RemoveCallbacks(networkEvents);

            if (runnerToShutdown.IsRunning)
                await runnerToShutdown.Shutdown();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        if (runnerToShutdown != null)
            Destroy(runnerToShutdown.gameObject);

        if (invokeEvent)
            onShutdownCompleted?.Invoke();
    }

    public async Task RecreateRunner()
    {
        await ShutdownRunner(false);
        CreateRunner();
    }

    private string ResolveSessionName(string sessionNameOverride)
    {
        return string.IsNullOrWhiteSpace(sessionNameOverride)
            ? persistentSessionName
            : sessionNameOverride.Trim();
    }

    private bool TryCreateGameSceneInfo(out NetworkSceneInfo sceneInfo)
    {
        sceneInfo = default;

        var buildIndex = SceneUtility.GetBuildIndexByScenePath(gameScenePath);

        if (buildIndex < 0)
        {
            var message = $"Game scene is not included in Build Profiles: {gameScenePath}";
            Debug.LogError(message, this);
            onConnectionFailed?.Invoke(message);
            return false;
        }

        sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(buildIndex), LoadSceneMode.Single);
        return true;
    }
}
