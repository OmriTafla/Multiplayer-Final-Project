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

    private void Start()
    {
        CreateRunner();
    }

    public NetworkRunner CreateRunner()
    {
        if (networkRunner != null)
            return networkRunner;

        if (networkRunnerPrefab == null)
        {
            Debug.LogError("NetworkRunner prefab is not assigned");
            return null;
        }

        networkRunner = Instantiate(networkRunnerPrefab);
        networkRunner.name = "NetworkRunner";
        networkRunner.ProvideInput = !Application.isBatchMode;

        if (networkRunner.GetComponent<RunnerSimulatePhysics3D>() == null)
            networkRunner.gameObject.AddComponent<RunnerSimulatePhysics3D>();

        if (networkEvents != null)
            networkRunner.AddCallbacks(networkEvents);

        var inputProvider = networkRunner.GetComponent<FusionInputProvider>();

        if (inputProvider != null)
            inputProvider.RegisterCallbacks(networkRunner);

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

    public Task<StartGameResult> JoinPersistentWorld()
    {
        return StartSession(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = persistentSessionName,
            CustomLobbyName = customLobbyName,
            EnableClientSessionCreation = false
        });
    }

    public Task<StartGameResult> StartPersistentServer(string sessionNameOverride = null)
    {
        var sessionName = string.IsNullOrWhiteSpace(sessionNameOverride)
            ? persistentSessionName
            : sessionNameOverride.Trim();

        if (!TryCreateGameSceneInfo(out var sceneInfo))
            return Task.FromResult(default(StartGameResult));

        return StartSession(new StartGameArgs
        {
            GameMode = GameMode.Server,
            SessionName = sessionName,
            CustomLobbyName = customLobbyName,
            PlayerCount = maximumPlayers,
            IsOpen = true,
            IsVisible = true,
            EnableClientSessionCreation = true,
            Scene = sceneInfo
        });
    }

    public Task<StartGameResult> StartPersistentHostForDevelopment()
    {
        if (!TryCreateGameSceneInfo(out var sceneInfo))
            return Task.FromResult(default(StartGameResult));

        return StartSession(new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = persistentSessionName,
            CustomLobbyName = customLobbyName,
            PlayerCount = maximumPlayers,
            IsOpen = true,
            IsVisible = true,
            EnableClientSessionCreation = true,
            Scene = sceneInfo
        });
    }

    public Task<StartGameResult> StartHost(string sessionName)
    {
        return StartSession(new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = sessionName,
            CustomLobbyName = customLobbyName,
            PlayerCount = maximumPlayers,
            IsOpen = true,
            IsVisible = true,
            EnableClientSessionCreation = true
        });
    }

    public Task<StartGameResult> StartClient(string sessionName)
    {
        return StartSession(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = sessionName,
            CustomLobbyName = customLobbyName,
            EnableClientSessionCreation = false
        });
    }

    public Task<StartGameResult> StartDedicatedServer(string sessionName)
    {
        return StartPersistentServer(sessionName);
    }

    public async Task<StartGameResult> StartSession(StartGameArgs args)
    {
        if (operationInProgress)
        {
            Debug.LogWarning("A network operation is already in progress");
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
            var inputProvider = runnerToShutdown.GetComponent<FusionInputProvider>();

            if (inputProvider != null)
                inputProvider.UnregisterCallbacks(runnerToShutdown);

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

    private bool TryCreateGameSceneInfo(out NetworkSceneInfo sceneInfo)
    {
        sceneInfo = default;

        var buildIndex = SceneUtility.GetBuildIndexByScenePath(gameScenePath);

        if (buildIndex < 0)
        {
            Debug.LogError($"Game scene is not included in Build Profiles: {gameScenePath}");
            return false;
        }

        sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(buildIndex), LoadSceneMode.Single);
        return true;
    }
}
