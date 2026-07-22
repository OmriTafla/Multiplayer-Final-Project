using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DefaultNamespace
{
    public class PlayerInput : NetworkBehaviour, INetworkRunnerCallbacks
    {
        public override void Spawned()
        {
            if (!Object.HasInputAuthority) return;
            Runner?.AddCallbacks(this);
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => runner?.RemoveCallbacks(this);

        private GameplayInput _input;


        void Update()
        {
            if (!HasInputAuthority) return;

            _input.LookRotation += Mouse.current.delta.ReadValue();
        }


        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var keyboard = Keyboard.current;

            var moveDirection = Vector3.zero;
            if (keyboard.wKey.isPressed) moveDirection += Vector3.forward;
            if (keyboard.sKey.isPressed) moveDirection += Vector3.back;
            if (keyboard.aKey.isPressed) moveDirection += Vector3.left;
            if (keyboard.dKey.isPressed) moveDirection += Vector3.right;

            _input.MoveDirection = moveDirection.normalized;

            input.Set(_input);
            _input.LookRotation = Vector2.zero;
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request,
            byte[] token)
        {
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key,
            ArraySegment<byte> data)
        {
        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
        }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
        }
    }
}