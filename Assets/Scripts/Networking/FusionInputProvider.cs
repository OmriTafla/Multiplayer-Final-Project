using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

public class FusionInputProvider : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string playerActionMapName = "Player";
    [SerializeField] private string uiActionMapName = "UI";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string pointerActionName = "Point";
    [SerializeField] private string fireActionName = "Attack";
    [SerializeField] private string placeActionName = "RightClick";
    [SerializeField] private string deleteActionName = "MiddleClick";
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private LayerMask aimMask = -1;
    [SerializeField] private float gamepadAimDistance = 30f;
    [SerializeField] private float gamepadAimDeadZone = 0.15f;

    private InputActionAsset runtimeInputActions;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction pointerAction;
    private InputAction fireAction;
    private InputAction placeAction;
    private InputAction deleteAction;

    private GameplayInput currentInput;
    private Vector2 gamepadAimInput;
    private bool placePressed;
    private bool deletePressed;
    private bool callbacksRegistered;
    private bool actionsResolved;

    public static FusionInputProvider Instance { get; private set; }

    private void Awake()
    {
        Instance = this;

        if (inputActions)
            runtimeInputActions = Instantiate(inputActions);

        ResolveActions();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (runtimeInputActions)
            Destroy(runtimeInputActions);
    }

    public Vector2 CurrentMoveInput =>
        actionsResolved ? moveAction.ReadValue<Vector2>() : Vector2.zero;

    private void OnEnable()
    {
        if (Application.isBatchMode)
            return;

        ResolveActions();
        EnableActions();
    }

    private void OnDisable()
    {
        DisableActions();
        ClearInput();
    }

    private void Update()
    {
        if (Application.isBatchMode || !actionsResolved)
            return;

        if (placeAction.WasPressedThisFrame())
            placePressed = true;

        if (deleteAction.WasPressedThisFrame())
            deletePressed = true;

        var lookValue = lookAction.ReadValue<Vector2>();
        gamepadAimInput = lookAction.activeControl?.device is Gamepad
            ? lookValue
            : Vector2.zero;

        UpdateAimPosition();
    }

    public void RegisterCallbacks(NetworkRunner runner)
    {
        if (!runner || callbacksRegistered)
            return;

        runner.AddCallbacks(this);
        callbacksRegistered = true;
    }

    public void UnregisterCallbacks(NetworkRunner runner)
    {
        if (!runner || !callbacksRegistered)
            return;

        runner.RemoveCallbacks(this);
        callbacksRegistered = false;
    }

    public void EnableGameplayInput()
    {
        if (Application.isBatchMode)
            return;

        ResolveActions();
        EnableActions();
    }

    public void DisableGameplayInput()
    {
        DisableActions();
        ClearInput();
    }

    public void SetGameplayCamera(Camera camera)
    {
        gameplayCamera = camera;
    }

    public void ClearGameplayCamera(Camera camera)
    {
        if (gameplayCamera == camera)
            gameplayCamera = null;
    }

    private void ResolveActions()
    {
        if (actionsResolved)
            return;

        if (!runtimeInputActions)
        {
            Debug.LogError("FusionInputProvider requires an InputActionAsset");
            return;
        }

        var playerMap = runtimeInputActions.FindActionMap(playerActionMapName, true);
        var uiMap = runtimeInputActions.FindActionMap(uiActionMapName, true);

        moveAction = playerMap.FindAction(moveActionName, true);
        lookAction = playerMap.FindAction(lookActionName, true);
        pointerAction = uiMap.FindAction(pointerActionName, true);
        fireAction = playerMap.FindAction(fireActionName, true);
        placeAction = uiMap.FindAction(placeActionName, true);
        deleteAction = uiMap.FindAction(deleteActionName, true);

        actionsResolved = true;
    }

    private void EnableActions()
    {
        if (!actionsResolved)
            return;

        moveAction.Enable();
        lookAction.Enable();
        pointerAction.Enable();
        fireAction.Enable();
        placeAction.Enable();
        deleteAction.Enable();
    }

    private void DisableActions()
    {
        if (!actionsResolved)
            return;

        moveAction.Disable();
        lookAction.Disable();
        pointerAction.Disable();
        fireAction.Disable();
        placeAction.Disable();
        deleteAction.Disable();
    }

    private void ClearInput()
    {
        currentInput = default;
        gamepadAimInput = Vector2.zero;
        placePressed = false;
        deletePressed = false;
    }

    private void UpdateAimPosition()
    {
#if !UNITY_SERVER
        if (!gameplayCamera)
        {
            var localPlayerCamera = LocalPlayerCamera.Instance;

            if (localPlayerCamera)
                gameplayCamera = localPlayerCamera.GameplayCamera;
        }
#endif

        if (!gameplayCamera)
            return;

        if (gamepadAimInput.sqrMagnitude >=
            gamepadAimDeadZone * gamepadAimDeadZone)
        {
            UpdateGamepadAim();
            return;
        }

        UpdatePointerAim();
    }

    private void UpdatePointerAim()
    {
        var pointerPosition = pointerAction.ReadValue<Vector2>();
        var ray = gameplayCamera.ScreenPointToRay(pointerPosition);

        if (Physics.Raycast(
                ray,
                out var hit,
                1000f,
                aimMask,
                QueryTriggerInteraction.Ignore))
        {
            currentInput.AimPosition = hit.point;
            return;
        }

        currentInput.AimPosition = ray.origin + ray.direction * 100f;
    }

    private void UpdateGamepadAim()
    {
        var cameraForward = gameplayCamera.transform.forward;
        var cameraRight = gameplayCamera.transform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        var worldDirection =
            cameraRight * gamepadAimInput.x +
            cameraForward * gamepadAimInput.y;

        if (worldDirection.sqrMagnitude < 0.0001f)
            return;

        currentInput.AimPosition =
            gameplayCamera.transform.position +
            worldDirection.normalized * gamepadAimDistance;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (Application.isBatchMode || !actionsResolved)
            return;

        currentInput.Move = Vector2.ClampMagnitude(
            moveAction.ReadValue<Vector2>(),
            1f);

        currentInput.Buttons.Set(
            GameplayButton.Fire,
            fireAction.IsPressed());

        currentInput.Buttons.Set(
            GameplayButton.Place,
            placePressed);

        currentInput.Buttons.Set(
            GameplayButton.Delete,
            deletePressed);

        input.Set(currentInput);

        placePressed = false;
        deletePressed = false;
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        callbacksRegistered = false;
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
    }

    public void OnConnectRequest(
        NetworkRunner runner,
        NetworkRunnerCallbackArgs.ConnectRequest request,
        byte[] token)
    {
    }

    public void OnConnectFailed(
        NetworkRunner runner,
        NetAddress remoteAddress,
        NetConnectFailedReason reason)
    {
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
    }

    public void OnCustomAuthenticationResponse(
        NetworkRunner runner,
        Dictionary<string, object> data)
    {
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
    }

    public void OnReliableDataReceived(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        ArraySegment<byte> data)
    {
    }

    public void OnReliableDataProgress(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        float progress)
    {
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }
}
